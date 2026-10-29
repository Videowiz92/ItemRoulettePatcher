using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ItemRoulettePatcher.Patches
{
    /// <summary>
    /// Translation table for the Russian-only strings that ItemRoulette 1.4.4 hard-codes in C# instead of routing
    /// through <c>Localization.Get(ru, en)</c> / a language token. Each entry is the exact Russian fragment from the
    /// ItemRoulette source and its English counterpart; dynamic parts (player names, numbers) sit between fragments
    /// and are untouched.
    ///
    /// The upstream fix for every entry is to replace the literal with a localized pair - see docs/UPSTREAM_en.md.
    /// </summary>
    internal static class RussianTextTranslator
    {
        private static readonly Regex Cyrillic = new Regex("[Ѐ-ӿ]", RegexOptions.Compiled);

        // Templates with captured numbers / names. Applied first.
        private static readonly (Regex pattern, string replacement)[] Patterns =
        {
            // ChaosEventManager: cashback event
            (new Regex(@"Возврат \$(\d+) \(x(\d+) за ([^)]*)\)!", RegexOptions.Compiled), "Refund $$$1 (x$2 for $3)!"),
            // ChestRewardHooks: Scavenger's Sack
            (new Regex(@" открыл Мешок Мусорщика! Всем игрокам начислено по (\d+) бесплатных круток \((\d+) на команду с округлением\)!", RegexOptions.Compiled),
                " opened a Scavenger's Sack! All players received $1 free spins each ($2 per team, rounded)!"),
        };

        // Plain fragments. Applied longest-first so a long phrase wins over any shorter phrase it contains.
        private static readonly (string ru, string en)[] Literals = new (string, string)[]
        {
            // --- ChaosEventManager: event descriptions (assigned to both RU and EN by the original code) ---
            ("Получена вторая копия: ", "Received a second copy: "),
            ("Получено +10 временных копий: ", "Received +10 temporary copies: "),
            (" (на 60 сек)!", " (for 60 sec)!"),
            ("Получено +5 спектральных копий: ", "Received +5 spectral copies: "),
            (" (до конца этапа)!", " (until the end of the stage)!"),
            ("Баланс золота удвоен (+$", "Gold balance doubled (+$"),
            ("Скопирован предмет того же тира у напарника!", "Copied an item of the same tier from a teammate!"),
            ("Скопирован предмет: ", "Copied item: "),
            ("Потеряно $", "Lost $"),
            (" (30% баланса)!", " (30% of balance)!"),
            ("Потерян предмет: ", "Lost item: "),
            ("Нет предметов подходящего тира для пожертвования!", "No items of a suitable tier to sacrifice!"),
            ("Получено проклятие: ", "Received a curse: "),
            ("Получен предмет-проклятие!", "Received a curse item!"),
            ("Получен элементальный аспект!", "Received an elemental aspect!"),
            ("Элементальный Аспект", "Elemental Aspect"),
            ("Призваны союзные дроны поддержки!", "Summoned allied support drones!"),

            // --- ItemDescriptionHooks: Sale Star description inserts these into an English sentence ---
            ("при убийстве боссов", "on boss kill"),
            ("при зарядке портала", "on teleporter charge"),

            // --- ChestRewardHooks: server broadcasts that have no English variant ---
            (" открыл Лунную Капсулу! Всем игрокам начислена 1 бесплатная крутка Лунных предметов!",
                " opened a Lunar Pod! All players received 1 free Lunar item spin!"),
            (" открыл Лунную Капсулу! Начислена 1 бесплатная крутка Лунных предметов.",
                " opened a Lunar Pod! Awarded 1 free Lunar item spin."),
            (" открыл Лунный Мешок Падальщика! Всем игрокам начислено по 10 Лунных монет!",
                " opened a Lunar Scavenger Sack! All players received 10 Lunar Coins each!"),
            (" открыл Колыбель Бездны! Предоставлен выбор бесплатной крутки Бездны.",
                " opened a Void Cradle! A free Void spin choice was granted."),
            (" открыл Потенциал Бездны! Начислена 1 бесплатная крутка (80% зеленый / 15% красный / 5% босс)!",
                " opened a Void Potential! Awarded 1 free spin (80% Green / 15% Red / 5% Boss)!"),
            ("Выгодная звезда", "Sale Star"),
            ("Выгодную звезду", "Sale Star"),

            // --- NotificationEntry: default value of StageFormatted ---
            ("Этап ", "Stage "),
        }.OrderByDescending(entry => entry.Item1.Length).ToArray();

        // Names the original code builds for the in-world subtitle of summoned allies / hostile clones.
        private static readonly (string ru, string en)[] SubtitleLiterals =
        {
            ("Спутник ", "Companion of "),
            ("Дрон ", "Drone of "),
            ("игрока", "player"),
        };

        private static readonly HashSet<string> AlreadyReported = new HashSet<string>();
        private const int MaxReports = 64;

        public static bool ContainsCyrillic(string text) => !string.IsNullOrEmpty(text) && Cyrillic.IsMatch(text);

        /// <summary>Translates known Russian-only fragments. Returns the input unchanged if it has no Cyrillic.</summary>
        public static string Translate(string text, string source)
        {
            if (!ContainsCyrillic(text))
            {
                return text;
            }

            var result = text;
            foreach (var (pattern, replacement) in Patterns)
            {
                result = pattern.Replace(result, replacement);
            }

            foreach (var (ru, en) in Literals)
            {
                if (result.Contains(ru))
                {
                    result = result.Replace(ru, en);
                }
            }

            ReportIfStillRussian(result, source);
            return result;
        }

        public static string TranslateSubtitle(string text)
        {
            if (!ContainsCyrillic(text))
            {
                return text;
            }

            var result = text;
            foreach (var (ru, en) in SubtitleLiterals)
            {
                result = result.Replace(ru, en);
            }

            ReportIfStillRussian(result, "subtitle");
            return result;
        }

        private static void ReportIfStillRussian(string text, string source)
        {
            if (!PluginConfig.LogUntranslatedRussian.Value || !ContainsCyrillic(text))
            {
                return;
            }

            lock (AlreadyReported)
            {
                if (AlreadyReported.Count >= MaxReports || !AlreadyReported.Add(text))
                {
                    return;
                }
            }

            Plugin.Log.LogWarning($"Untranslated Russian text ({source}) shown while ItemRoulette language is not Russian: \"{text}\"");
        }
    }
}
