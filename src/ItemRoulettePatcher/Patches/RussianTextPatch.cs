using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RoR2;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Fix: "Language = Auto" (or English) still showed Russian text.
    ///
    /// The language detection itself is fine (<c>Localization.IsRussian</c> resolves Auto from the game
    /// language). The leak is hard-coded Russian string literals that never go through it. This patch puts a
    /// translation at the few places where such text ends up on screen, and only when ItemRoulette itself
    /// considers the current language non-Russian. See <see cref="RussianTextTranslator"/> for the table.
    /// </summary>
    internal static class RussianTextPatch
    {
        private static PropertyInfo _stageFormatted;

        public static void Apply(Harmony harmony)
        {
            // Every toast and journal entry is built through this constructor (host side before it is sent,
            // client side when it is received).
            var entryType = ItemRouletteBridge.FindType("ItemRoulette.Core.NotificationEntry");
            if (entryType != null)
            {
                var fullConstructor = entryType.GetConstructors().FirstOrDefault(c => c.GetParameters().Length == 6);
                if (fullConstructor != null)
                {
                    harmony.Patch(fullConstructor, prefix: new HarmonyMethod(typeof(RussianTextPatch), nameof(TranslateNotification)));
                }

                var emptyConstructor = entryType.GetConstructor(Type.EmptyTypes);
                _stageFormatted = AccessTools.Property(entryType, "StageFormatted");
                if (emptyConstructor != null && _stageFormatted != null)
                {
                    harmony.Patch(emptyConstructor, postfix: new HarmonyMethod(typeof(RussianTextPatch), nameof(TranslateDefaultStage)));
                }
            }

            // Chaos event descriptions are assigned as one Russian string for both languages.
            var description = ItemRouletteBridge.FindType("ItemRoulette.Core.ChaosEventDefinition");
            var descriptionGetter = description == null ? null : AccessTools.PropertyGetter(description, "Description");
            if (descriptionGetter != null)
            {
                harmony.Patch(descriptionGetter, postfix: new HarmonyMethod(typeof(RussianTextPatch), nameof(TranslateResult)));
            }

            // Sale Star description: Russian phrases are substituted into the English sentence.
            var descriptionHooks = ItemRouletteBridge.FindMethod("ItemRoulette.Hooks.ItemDescriptionHooks", "TryGetModDescription");
            if (descriptionHooks != null)
            {
                harmony.Patch(descriptionHooks, postfix: new HarmonyMethod(typeof(RussianTextPatch), nameof(TranslateExtraDescription)));
            }

            // "Companion of <player>" / "Drone of <player>" nameplates of summoned allies and hostile clones.
            var subtitle = AccessTools.Method(typeof(CharacterBody), nameof(CharacterBody.GetSubtitle));
            if (subtitle != null)
            {
                harmony.Patch(subtitle, postfix: new HarmonyMethod(typeof(RussianTextPatch), nameof(TranslateSubtitle)));
            }
        }

        private static bool ShouldTranslate()
        {
            return PluginConfig.TranslateRussianFallbackText.Value && !ItemRouletteBridge.IsRussian();
        }

        // ReSharper disable InconsistentNaming
        private static void TranslateNotification(ref string message)
        {
            if (ShouldTranslate())
            {
                message = RussianTextTranslator.Translate(message, "notification");
            }
        }

        private static void TranslateDefaultStage(object __instance)
        {
            if (!ShouldTranslate())
            {
                return;
            }

            var current = (string)_stageFormatted.GetValue(__instance);
            _stageFormatted.SetValue(__instance, RussianTextTranslator.Translate(current, "stage label"));
        }

        private static void TranslateResult(ref string __result)
        {
            if (ShouldTranslate())
            {
                __result = RussianTextTranslator.Translate(__result, "chaos event");
            }
        }

        private static void TranslateExtraDescription(ref string extraDesc)
        {
            if (ShouldTranslate())
            {
                extraDesc = RussianTextTranslator.Translate(extraDesc, "item description");
            }
        }

        private static void TranslateSubtitle(ref string __result)
        {
            if (ShouldTranslate())
            {
                __result = RussianTextTranslator.TranslateSubtitle(__result);
            }
        }
        // ReSharper restore InconsistentNaming
    }
}
