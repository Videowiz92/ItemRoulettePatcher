using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoR2;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Fix: items won on the roulette never triggered the Substandard Duplicator.
    ///
    /// Cause: vanilla applies the Duplicator inside <c>ItemDef.AttemptGrant</c> (the normal pickup path).
    /// ItemRoulette bypasses that path and calls <c>Inventory.GiveItemPermanent</c> directly.
    ///
    /// Fix: while ItemRoulette is granting a roulette award (or an accepted Void item) to a player, the first
    /// <c>GiveItemPermanent</c> on that player's inventory is followed by the same temporary-copy logic vanilla
    /// uses. The game's own <c>HandleDuplicator</c> local function is invoked, so the duplicator effect and the
    /// Quality mod's Duplicator changes both still apply.
    /// </summary>
    internal static class DuplicatorPatch
    {
        private sealed class Scope
        {
            public CharacterMaster Master;
            public bool Done;

            /// <summary>The one item this grant is allowed to duplicate; None = any (used for the Void accept path).</summary>
            public ItemIndex Expected = ItemIndex.None;
            public bool AnyItem;
        }

        [ThreadStatic] private static Scope _scope;

        private static MethodInfo _handleDuplicator;
        private static FieldInfo _pendingAward;
        private static PropertyInfo _pickupDef;

        public static void Apply(Harmony harmony)
        {
            _handleDuplicator = typeof(ItemDef)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name.StartsWith("<AttemptGrant>g__HandleDuplicator|", StringComparison.Ordinal));
            if (_handleDuplicator == null)
            {
                Plugin.Log.LogWarning("Could not find the game's HandleDuplicator; falling back to a plain temporary-item grant.");
            }

            var exit = new HarmonyMethod(typeof(DuplicatorPatch), nameof(ExitScope));

            var stateType = ItemRouletteBridge.FindType("ItemRoulette.Core.PlayerRouletteState");
            var entryType = ItemRouletteBridge.FindType("ItemRoulette.Core.RouletteItemEntry");
            _pendingAward = stateType == null ? null : AccessTools.Field(stateType, "PendingAward");
            _pickupDef = entryType == null ? null : AccessTools.Property(entryType, "PickupDef");
            if (_pendingAward == null || _pickupDef == null)
            {
                Plugin.Log.LogWarning("PlayerRouletteState.PendingAward / RouletteItemEntry.PickupDef not found; Duplicator patch disabled.");
                return;
            }

            // Roulette award being handed to the player: only the awarded item may be duplicated.
            Wrap(harmony, "GrantPendingAwardServer", new HarmonyMethod(typeof(DuplicatorPatch), nameof(EnterAwardScope)), exit);
            // Void item accepted from the decision banner: its only permanent grant is the accepted item.
            Wrap(harmony, "ExecuteVoidDecisionOnServer", new HarmonyMethod(typeof(DuplicatorPatch), nameof(EnterVoidScope)), exit);

            var give = AccessTools.Method(typeof(Inventory), nameof(Inventory.GiveItemPermanent), new[] { typeof(ItemIndex), typeof(int) });
            if (give == null)
            {
                Plugin.Log.LogWarning("Inventory.GiveItemPermanent(ItemIndex, int) not found; Duplicator patch disabled.");
                return;
            }

            harmony.Patch(give, postfix: new HarmonyMethod(typeof(DuplicatorPatch), nameof(OnGiveItemPermanent)));
        }

        private static void Wrap(Harmony harmony, string methodName, HarmonyMethod enter, HarmonyMethod exit)
        {
            var target = ItemRouletteBridge.FindMethod("ItemRoulette.Core.ServerSpinService", methodName);
            if (target != null)
            {
                harmony.Patch(target, prefix: enter, finalizer: exit);
            }
        }

        // ReSharper disable InconsistentNaming
        private static void EnterAwardScope(CharacterMaster master, object state, out Scope __state)
        {
            __state = _scope;
            _scope = null;
            if (!PluginConfig.DuplicatorOnRouletteItems.Value || !master || state == null)
            {
                return;
            }

            // Equipment awards have no item index (None), so the scope never matches and nothing is duplicated.
            var award = _pendingAward.GetValue(state);
            var pickupDef = award == null ? null : _pickupDef.GetValue(award) as PickupDef;
            if (pickupDef != null && pickupDef.itemIndex != ItemIndex.None)
            {
                _scope = new Scope { Master = master, Expected = pickupDef.itemIndex };
            }
        }

        private static void EnterVoidScope(CharacterMaster master, out Scope __state)
        {
            __state = _scope;
            _scope = PluginConfig.DuplicatorOnRouletteItems.Value && master ? new Scope { Master = master, AnyItem = true } : null;
        }

        private static void ExitScope(Scope __state)
        {
            _scope = __state;
        }

        private static void OnGiveItemPermanent(Inventory __instance, ItemIndex itemIndex)
        {
            var scope = _scope;
            if (scope == null || scope.Done || !scope.Master || scope.Master.inventory != __instance ||
                (!scope.AnyItem && scope.Expected != itemIndex))
            {
                return;
            }

            scope.Done = true;
            try
            {
                Duplicate(scope.Master, itemIndex);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Substandard Duplicator patch failed for item {itemIndex}: {e}");
            }
        }
        // ReSharper restore InconsistentNaming

        /// <summary>Mirrors the Duplicator branch of vanilla <c>ItemDef.AttemptGrant</c> for a permanent pickup.</summary>
        private static void Duplicate(CharacterMaster master, ItemIndex itemIndex)
        {
            var inventory = master.inventory;
            if (!inventory || inventory.GetItemCountEffective(DLC3Content.Items.Duplicator) <= 0)
            {
                return;
            }

            var itemDef = ItemCatalog.GetItemDef(itemIndex);
            if (!itemDef)
            {
                return;
            }

            var toGrant = ItemIndex.None;
            if (itemDef.ContainsTag(ItemTag.CanBeTemporary))
            {
                toGrant = itemIndex;
            }
            else if (itemDef.DoesNotContainTag(ItemTag.ObjectiveRelated) &&
                     itemDef.DoesNotContainTag(ItemTag.Scrap) &&
                     itemDef.DoesNotContainTag(ItemTag.WorldUnique))
            {
                toGrant = Util.RollTemporaryItemFromItemIndex(itemIndex);
            }

            if (toGrant == ItemIndex.None)
            {
                return;
            }

            var body = master.GetBody();
            if (body && _handleDuplicator != null)
            {
                _handleDuplicator.Invoke(null, new object[] { toGrant, body, 1f });
            }
            else
            {
                inventory.GiveItemTemp(toGrant, 1f);
            }
        }
    }
}
