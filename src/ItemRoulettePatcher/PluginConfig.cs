using BepInEx.Configuration;

namespace ItemRoulettePatcher
{
    internal static class PluginConfig
    {
        public static ConfigEntry<bool> RequireArtifact;
        public static ConfigEntry<bool> PreserveQualityEquipment;
        public static ConfigEntry<bool> DuplicatorOnRouletteItems;
        public static ConfigEntry<bool> LifeBetRespectsExtraLives;
        public static ConfigEntry<bool> PauseWhileOpen;
        public static ConfigEntry<bool> TranslateRussianFallbackText;
        public static ConfigEntry<bool> LogUntranslatedRussian;

        public static void Bind(ConfigFile config)
        {
            RequireArtifact = config.Bind("General", "RequireArtifact", true,
                "When true, every ItemRoulette mechanic (item reel, chest/drop replacement, casino, chaos events, HUD, hotkeys) " +
                "is only active in runs where the 'Artifact of the Roulette' is enabled on the lobby screen. " +
                "When false, ItemRoulette behaves exactly as it does without this patcher (always on).");

            PreserveQualityEquipment = config.Bind("Fixes", "PreserveQualityEquipment", true,
                "When a roulette win replaces your current equipment, drop the old equipment at its real Quality tier " +
                "instead of as the plain vanilla version. Also lets the Void-decision banner close for Quality equipment.");

            DuplicatorOnRouletteItems = config.Bind("Fixes", "DuplicatorOnRouletteItems", true,
                "Items won on the roulette (and accepted Void items) trigger the Substandard Duplicator like a normal pickup would: " +
                "a temporary copy is added on top of the permanent one.");

            LifeBetRespectsExtraLives = config.Bind("Fixes", "LifeBetRespectsExtraLives", false,
                "When false (ItemRoulette's original behaviour) losing a Life Bet is a hard kill: Dio's Best Friend, Pluripotent Larva " +
                "and Seed of Life are consumed and cannot save you. When true, a lost Life Bet is an ordinary lethal hit and your " +
                "extra lives / Seed of Life revive you as usual.");

            PauseWhileOpen = config.Bind("Fixes", "PauseWhileOpen", true,
                "Makes ItemRoulette's own 'PauseGameWhileOpen' option work. In 1.4.4 that option is shown in the settings but never read, " +
                "so it does nothing. When true (and that option is on), single-player is paused while the roulette reel, casino or admin " +
                "menu is open. Multiplayer is never paused. Set to false to leave the option inert as in ItemRoulette 1.4.4.");

            TranslateRussianFallbackText = config.Bind("Localization", "TranslateRussianFallbackText", true,
                "ItemRoulette 1.4.4 contains some Russian-only messages that appear even when its Language setting resolves to English " +
                "(chest broadcasts, chaos-event descriptions, Sale Star description, ...). When true, those are translated to English.");

            LogUntranslatedRussian = config.Bind("Localization", "LogUntranslatedRussian", true,
                "Log a warning (once per distinct text) when Russian text is shown while ItemRoulette is set to a non-Russian language " +
                "and the patcher has no translation for it. Useful for reporting leftovers to the ItemRoulette author.");
        }
    }
}
