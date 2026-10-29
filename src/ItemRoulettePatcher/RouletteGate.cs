using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RoR2;

namespace ItemRoulettePatcher
{
    /// <summary>
    /// Turns ItemRoulette's mechanics on and off at runtime, driven by the roulette artifact.
    ///
    /// ItemRoulette is built from modules (<c>ModuleManager</c>). Two of them own everything that changes how a run
    /// plays, and both have a real, symmetric Initialize/Cleanup:
    ///   * DropSuppression - every gameplay hook (chest/printer/shrine/multishop removal, reward replacement,
    ///     teleporter rewards, droplet interception, curse penalty, item-description additions)
    ///   * RouletteUI      - the HUD reel, casino, admin menu, notification UI and all hotkeys
    /// The other modules only register network/message handlers and are idle without the UI or the hooks.
    /// So "off" is: run Cleanup on those two; "on" is: run Initialize again. No restart, no ItemRoulette edits.
    /// </summary>
    internal static class RouletteGate
    {
        // Initialization order (Cleanup runs in reverse), matching ItemRoulette's own priority ordering.
        private static readonly string[] GatedModules = { "DropSuppression", "RouletteUI" };

        // ItemRoulette initializes everything in its own Awake, so it starts out active.
        private static bool _active = true;
        private static bool _unsupported;
        private static int _unknownTicks;

        // With no lobby and no run (main menu, scene transitions) the artifact state is unknown. Hold the current
        // state through the short gap between lobby and run, and only switch off if it lasts this many polls.
        private const int UnknownTicksBeforeOff = 12; // ~3 seconds

        public static void Init()
        {
            // Apply the moment the choice is known, not on the next poll: the first stage is populated right after
            // the run starts, and ItemRoulette's spawn-suppression hooks must already be registered by then.
            Run.onRunStartGlobal += _ => Tick();
            PreGameController.onPreGameControllerSetRuleBookGlobal += (_, __) => Tick();
        }

        /// <summary>
        /// True/false when the artifact choice is known, null when there is neither a run nor a lobby.
        /// Read from the rule book rather than RunArtifactManager: that only exists once the run has started,
        /// which is too late to affect stage 1.
        /// </summary>
        public static bool? Desired
        {
            get
            {
                if (!PluginConfig.RequireArtifact.Value)
                {
                    return true;
                }

                var def = RouletteArtifact.Def;
                if (!def)
                {
                    return false;
                }

                RuleBook ruleBook = null;
                if (Run.instance)
                {
                    ruleBook = Run.instance.ruleBook;
                }
                else if (PreGameController.instance)
                {
                    ruleBook = PreGameController.instance.readOnlyRuleBook;
                }

                if (ruleBook == null)
                {
                    return null;
                }

                for (int i = 0, count = RuleCatalog.ruleCount; i < count; i++)
                {
                    if (ruleBook.GetRuleChoice(i).artifactIndex == def.artifactIndex)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static void Tick()
        {
            if (_unsupported)
            {
                return;
            }

            var desired = Desired;
            if (desired == null)
            {
                if (_active && ++_unknownTicks >= UnknownTicksBeforeOff)
                {
                    _unknownTicks = 0;
                    SetActive(false);
                }

                return;
            }

            _unknownTicks = 0;
            if (desired.Value != _active)
            {
                SetActive(desired.Value);
            }
        }

        /// <summary>Leave ItemRoulette fully on if the patcher is unloaded while it was switched off.</summary>
        public static void Release()
        {
            // At game shutdown ItemRoulette has already cleared its module list; nothing left to restore then.
            if (!_unsupported && !_active && FindGatedModules() != null)
            {
                SetActive(true);
            }
        }

        private static void SetActive(bool active)
        {
            var modules = FindGatedModules();
            if (modules == null)
            {
                _unsupported = true;
                Plugin.Log.LogError("Could not find ItemRoulette's DropSuppression/RouletteUI modules; the artifact cannot gate it. " +
                                    "ItemRoulette will stay always-on.");
                return;
            }

            try
            {
                if (active)
                {
                    foreach (var module in modules)
                    {
                        Invoke(module, "Initialize");
                    }
                }
                else
                {
                    for (var i = modules.Count - 1; i >= 0; i--)
                    {
                        Invoke(modules[i], "Cleanup");
                    }
                }

                _active = active;
                Plugin.Log.LogInfo(active
                    ? "Roulette artifact enabled: ItemRoulette mechanics ON."
                    : "Roulette artifact not enabled: ItemRoulette mechanics OFF.");
            }
            catch (Exception e)
            {
                _unsupported = true;
                Plugin.Log.LogError($"Failed to switch ItemRoulette {(active ? "on" : "off")}; gating disabled: {e}");
            }
        }

        private static List<object> FindGatedModules()
        {
            var managerType = ItemRouletteBridge.FindType("ItemRoulette.Core.Modules.ModuleManager");
            var field = managerType == null ? null : AccessTools.Field(managerType, "_modules");
            if (!(field?.GetValue(null) is IEnumerable registered))
            {
                return null;
            }

            var byName = new Dictionary<string, object>();
            foreach (var module in registered)
            {
                var name = module.GetType().GetProperty("Name")?.GetValue(module) as string;
                if (name != null)
                {
                    byName[name] = module;
                }
            }

            var result = new List<object>(GatedModules.Length);
            foreach (var name in GatedModules)
            {
                if (!byName.TryGetValue(name, out var module))
                {
                    return null;
                }

                result.Add(module);
            }

            return result;
        }

        private static void Invoke(object module, string method)
        {
            module.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.Instance)?.Invoke(module, null);
        }
    }
}
