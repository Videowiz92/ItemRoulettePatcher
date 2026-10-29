using System;
using System.Reflection;
using HarmonyLib;
using RoR2;
using UnityEngine;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Optional change: a lost Life Bet ignored Dio's Best Friend, Pluripotent Larva and Seed of Life.
    ///
    /// Cause: ItemRoulette first deals lethal damage (which would trigger a revive), then calls
    /// <c>CharacterMaster.TrueKill()</c>. TrueKill converts every extra-life item to its "consumed" form and
    /// cancels the pending respawn, so nothing can save the player.
    ///
    /// Behaviour (config <c>LifeBetRespectsExtraLives</c>, default OFF = unchanged): while a lost Life Bet is
    /// being executed for a player, TrueKill for that player is replaced by a normal suicide, which goes through
    /// the usual death flow and therefore lets extra lives revive the player.
    /// </summary>
    internal static class LifeBetPatch
    {
        [ThreadStatic] private static CharacterMaster _lostLifeBetMaster;

        private static FieldInfo _isLifeBet;
        private static FieldInfo _lifeSurvived;

        public static void Apply(Harmony harmony)
        {
            var execute = ItemRouletteBridge.FindMethod("ItemRoulette.Core.CasinoServerService", "ExecutePendingCasinoAward");
            var awardType = ItemRouletteBridge.FindType("ItemRoulette.Core.PendingCasinoAward");
            if (execute == null || awardType == null)
            {
                return;
            }

            _isLifeBet = AccessTools.Field(awardType, "isLifeBet");
            _lifeSurvived = AccessTools.Field(awardType, "lifeSurvived");
            if (_isLifeBet == null || _lifeSurvived == null)
            {
                Plugin.Log.LogWarning("PendingCasinoAward.isLifeBet/lifeSurvived not found; Life Bet patch disabled.");
                return;
            }

            var trueKill = AccessTools.Method(typeof(CharacterMaster), nameof(CharacterMaster.TrueKill),
                new[] { typeof(GameObject), typeof(GameObject), typeof(DamageTypeCombo) });
            if (trueKill == null)
            {
                Plugin.Log.LogWarning("CharacterMaster.TrueKill(GameObject, GameObject, DamageTypeCombo) not found; Life Bet patch disabled.");
                return;
            }

            harmony.Patch(execute,
                prefix: new HarmonyMethod(typeof(LifeBetPatch), nameof(EnterAward)),
                finalizer: new HarmonyMethod(typeof(LifeBetPatch), nameof(ExitAward)));
            harmony.Patch(trueKill, prefix: new HarmonyMethod(typeof(LifeBetPatch), nameof(OnTrueKill)));
        }

        // ReSharper disable InconsistentNaming
        private static void EnterAward(CharacterMaster master, object award, out CharacterMaster __state)
        {
            __state = _lostLifeBetMaster;
            _lostLifeBetMaster = null;

            if (!PluginConfig.LifeBetRespectsExtraLives.Value || !master || award == null)
            {
                return;
            }

            if ((bool)_isLifeBet.GetValue(award) && !(bool)_lifeSurvived.GetValue(award))
            {
                _lostLifeBetMaster = master;
            }
        }

        private static void ExitAward(CharacterMaster __state)
        {
            _lostLifeBetMaster = __state;
        }

        private static bool OnTrueKill(CharacterMaster __instance, GameObject killerOverride, GameObject inflictorOverride, DamageTypeCombo damageTypeOverride)
        {
            if (!_lostLifeBetMaster || __instance != _lostLifeBetMaster)
            {
                return true;
            }

            // ItemRoulette already dealt lethal damage just before this call. If that killed the body, the normal
            // death flow is already running and any revive is scheduled - leave it alone. If the body somehow
            // survived (invulnerability buff etc.) finish it with an ordinary kill so the bet still costs a life.
            var body = __instance.GetBody();
            if (body && body.healthComponent && body.healthComponent.alive)
            {
                body.healthComponent.Suicide(killerOverride, inflictorOverride, damageTypeOverride);
            }

            return false;
        }
        // ReSharper restore InconsistentNaming
    }
}
