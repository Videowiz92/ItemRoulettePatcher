using System;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using HarmonyLib;
using ItemRoulettePatcher.Patches;
using UnityEngine;

namespace ItemRoulettePatcher
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(ItemRouletteBridge.PluginGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.bepis.r2api.content_management", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.bepis.r2api.language", BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.rune580.riskofoptions", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("Gorakh.ItemQualities", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.itemroulettepatcher.fixes";
        public const string Name = "ItemRoulettePatcher";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;


        private Harmony _harmony;
        private float _nextGateCheck;

        private void Awake()
        {
            Log = Logger;

            if (!ItemRouletteBridge.Init())
            {
                Log.LogError("ItemRoulette is not loaded; the patcher has nothing to patch and will stay idle.");
                enabled = false;
                return;
            }

            PluginConfig.Bind(Config);

            _harmony = new Harmony(Guid);
            ApplyPatch("Quality equipment drop", () => QualityEquipmentPatch.Apply(_harmony));
            ApplyPatch("Substandard Duplicator", () => DuplicatorPatch.Apply(_harmony));
            ApplyPatch("Life Bet extra lives", () => LifeBetPatch.Apply(_harmony));
            ApplyPatch("Russian text fallback", () => RussianTextPatch.Apply(_harmony));

            ApplyPatch("Roulette artifact", RouletteArtifact.Register);
            ApplyPatch("Artifact gate", RouletteGate.Init);

            if (Chainloader.PluginInfos.ContainsKey("com.rune580.riskofoptions"))
            {
                ApplyPatch("Risk of Options entries", RiskOfOptionsSetup.Setup);
            }

            Log.LogInfo($"{Name} {Version} loaded (patching ItemRoulette {ItemRouletteBridge.TestedVersion}).");
        }

        private void Update()
        {
            // Cheap poll (4x/second) instead of juggling lobby/run/scene events: it also covers clients,
            // whose artifact state is replicated from the host at arbitrary times.
            if (Time.unscaledTime < _nextGateCheck)
            {
                return;
            }

            _nextGateCheck = Time.unscaledTime + 0.25f;
            RouletteGate.Tick();
        }

        private void OnDestroy()
        {
            RouletteGate.Release();
            _harmony?.UnpatchSelf();
        }

        private static void ApplyPatch(string label, Action apply)
        {
            try
            {
                apply();
            }
            catch (Exception e)
            {
                Log.LogError($"Patch '{label}' failed and was skipped: {e}");
            }
        }
    }
}
