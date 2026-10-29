using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RoR2;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Fix: replacing a Quality equipment through the roulette dropped the plain vanilla equipment.
    ///
    /// Cause: the Quality mod hooks <c>Inventory.currentEquipmentIndex</c> so it always reports the BASE
    /// equipment (so vanilla code keeps working). ItemRoulette reads that property to decide which pickup to
    /// drop, so "Executive Card (Tier 4)" came back as "Executive Card".
    ///
    /// Fix: in the ItemRoulette methods that read the property, call <see cref="GetRawCurrentEquipmentIndex"/>
    /// instead. It reads the real stored equipment state, which Quality does not strip. Without Quality
    /// installed both return the same value, so behaviour is unchanged.
    /// </summary>
    internal static class QualityEquipmentPatch
    {
        private static readonly MethodInfo RawGetter =
            AccessTools.Method(typeof(QualityEquipmentPatch), nameof(GetRawCurrentEquipmentIndex));

        public static void Apply(Harmony harmony)
        {
            var transpiler = new HarmonyMethod(typeof(QualityEquipmentPatch), nameof(Transpiler));

            // Server side: the equipment that gets dropped when it is replaced.
            Patch(harmony, transpiler, "ItemRoulette.Core.RouletteManager", "ExecuteGrantEquipmentServer");
            Patch(harmony, transpiler, "ItemRoulette.Core.ChaosEventManager", "ExecuteGrantElementalAspectServer");

            // Client side: the Void-decision banner closes when it sees the won equipment arrive.
            // It compares against the (base-index) currentEquipmentIndex, so it never matched Quality equipment.
            Patch(harmony, transpiler, "ItemRoulette.UI.RouletteUI", "Update");
            Patch(harmony, transpiler, "ItemRoulette.UI.RouletteUI", "FinishSpin");
            Patch(harmony, transpiler, "ItemRoulette.UI.RouletteUI", "HandleVoidDecisionResultReceived");
        }

        private static void Patch(Harmony harmony, HarmonyMethod transpiler, string typeName, string methodName)
        {
            var target = ItemRouletteBridge.FindMethod(typeName, methodName);
            if (target == null)
            {
                return;
            }

            harmony.Patch(target, transpiler: transpiler);
        }

        public static EquipmentIndex GetRawCurrentEquipmentIndex(Inventory inventory)
        {
            if (!inventory)
            {
                return EquipmentIndex.None;
            }

            if (!PluginConfig.PreserveQualityEquipment.Value)
            {
                return inventory.currentEquipmentIndex;
            }

            var location = inventory.activeEquipmentLocation;
            return inventory.GetEquipment(in location).equipmentIndex;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            var replaced = 0;
            foreach (var instruction in instructions)
            {
                if ((instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call) &&
                    instruction.operand is MethodInfo method &&
                    method.DeclaringType == typeof(Inventory) &&
                    method.Name == "get_currentEquipmentIndex")
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = RawGetter;
                    replaced++;
                }

                yield return instruction;
            }

            if (replaced == 0)
            {
                Plugin.Log.LogWarning($"Quality equipment patch found nothing to replace in {original.DeclaringType?.Name}.{original.Name}; " +
                                      "ItemRoulette may have changed.");
            }
        }
    }
}
