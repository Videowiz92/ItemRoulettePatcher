using System.Runtime.CompilerServices;
using RiskOfOptions;
using RiskOfOptions.Options;

namespace ItemRoulettePatcher
{
    /// <summary>Kept in its own class so the plugin loads even when Risk of Options is not installed.</summary>
    internal static class RiskOfOptionsSetup
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Setup()
        {
            ModSettingsManager.SetModDescription(
                "Fixes and an on/off artifact for the ItemRoulette mod.", Plugin.Guid, Plugin.Name);

            Add(PluginConfig.RequireArtifact);
            Add(PluginConfig.PreserveQualityEquipment);
            Add(PluginConfig.DuplicatorOnRouletteItems);
            Add(PluginConfig.LifeBetRespectsExtraLives);
            Add(PluginConfig.PauseWhileOpen);
            Add(PluginConfig.TranslateRussianFallbackText);
        }

        private static void Add(BepInEx.Configuration.ConfigEntry<bool> entry)
        {
            ModSettingsManager.AddOption(new CheckBoxOption(entry), Plugin.Guid, Plugin.Name);
        }
    }
}
