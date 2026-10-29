using System;
using ItemRoulettePatcher.Patches;

// Stand-ins for the two things RussianTextTranslator reaches for in the real plugin.
namespace ItemRoulettePatcher
{
    internal sealed class FakeEntry { public bool Value => false; }

    internal static class PluginConfig { public static readonly FakeEntry LogUntranslatedRussian = new FakeEntry(); }

    internal static class Plugin { public static readonly FakeLog Log = new FakeLog(); }

    internal sealed class FakeLog { public void LogWarning(string message) => Console.WriteLine("  [warn] " + message); }
}

internal static class Program
{
    private static int _failures;

    // Each case is a string built exactly the way the ItemRoulette 1.4.4 source builds it (placeholders filled in),
    // paired with the English text we expect. Sources are noted so a future ItemRoulette update is easy to diff.
    private static int Main()
    {
        // ChaosEventManager.cs
        Check("Получена вторая копия: <b>Syringe</b>!", "Received a second copy: <b>Syringe</b>!");
        Check("Получено +10 временных копий: <b>Syringe</b> (на 60 сек)!", "Received +10 temporary copies: <b>Syringe</b> (for 60 sec)!");
        Check("Получено +5 спектральных копий: <b>Syringe</b> (до конца этапа)!", "Received +5 spectral copies: <b>Syringe</b> (until the end of the stage)!");
        Check("Возврат $75 (x3 за Uncommon)!", "Refund $75 (x3 for Uncommon)!");
        Check("Баланс золота удвоен (+$120)!", "Gold balance doubled (+$120)!");
        Check("Скопирован предмет: <b>Syringe</b>!", "Copied item: <b>Syringe</b>!");
        Check("Скопирован предмет того же тира у напарника!", "Copied an item of the same tier from a teammate!");
        Check("Потеряно $36 (30% баланса)!", "Lost $36 (30% of balance)!");
        Check("Потерян предмет: <color=#FF6666><b>Syringe</b></color>!", "Lost item: <color=#FF6666><b>Syringe</b></color>!");
        Check("Нет предметов подходящего тира для пожертвования!", "No items of a suitable tier to sacrifice!");
        Check("Получено проклятие: <color=#FF4444><b>Shaped Glass</b></color>!", "Received a curse: <color=#FF4444><b>Shaped Glass</b></color>!");
        Check("Получен предмет-проклятие!", "Received a curse item!");
        Check("Получен элементальный аспект!", "Received an elemental aspect!");
        Check("Элементальный Аспект", "Elemental Aspect");
        Check("Призваны союзные дроны поддержки!", "Summoned allied support drones!");

        // ItemDescriptionHooks.cs (Sale Star): Russian phrase substituted into the English template
        Check("...stage boss reward при убийстве боссов (granting...", "...stage boss reward on boss kill (granting...");
        Check("...stage boss reward при зарядке портала (granting...", "...stage boss reward on teleporter charge (granting...");

        // ChestRewardHooks.cs broadcasts (tag already stripped by ItemRoulette before it becomes a notification)
        Check("<color=#38BDF8>Bob открыл Лунную Капсулу! Всем игрокам начислена 1 бесплатная крутка Лунных предметов!</color>",
              "<color=#38BDF8>Bob opened a Lunar Pod! All players received 1 free Lunar item spin!</color>");
        Check("<color=#38BDF8>Bob открыл Лунную Капсулу! Начислена 1 бесплатная крутка Лунных предметов.</color>",
              "<color=#38BDF8>Bob opened a Lunar Pod! Awarded 1 free Lunar item spin.</color>");
        Check("<color=#4BE0FF>Bob открыл Лунный Мешок Падальщика! Всем игрокам начислено по 10 Лунных монет!</color>",
              "<color=#4BE0FF>Bob opened a Lunar Scavenger Sack! All players received 10 Lunar Coins each!</color>");
        Check("<color=#FFE853>Bob открыл Мешок Мусорщика! Всем игрокам начислено по 4 бесплатных круток (7 на команду с округлением)!</color>",
              "<color=#FFE853>Bob opened a Scavenger's Sack! All players received 4 free spins each (7 per team, rounded)!</color>");
        Check("<color=#D070FF>Bob открыл Колыбель Бездны! Предоставлен выбор бесплатной крутки Бездны.</color>",
              "<color=#D070FF>Bob opened a Void Cradle! A free Void spin choice was granted.</color>");
        Check("<color=#BF58FF>Bob открыл Потенциал Бездны! Начислена 1 бесплатная крутка (80% зеленый / 15% красный / 5% босс)!</color>",
              "<color=#BF58FF>Bob opened a Void Potential! Awarded 1 free spin (80% Green / 15% Red / 5% Boss)!</color>");

        // NotificationEntry.cs default
        Check("Этап 1", "Stage 1");

        // Subtitles (ChaosEventManager.cs): "Спутник " / "Дрон " + name-or-"игрока"
        CheckSubtitle("Спутник Bob", "Companion of Bob");
        CheckSubtitle("Дрон игрока", "Drone of player");

        // Text with no Cyrillic must pass through untouched, including odd characters.
        Check("[ROULETTE] +1 free spin! 100% <color=#fff>ok</color> $5", "[ROULETTE] +1 free spin! 100% <color=#fff>ok</color> $5");
        Check(null, null);
        Check("", "");

        Console.WriteLine(_failures == 0 ? "All translator checks passed." : $"{_failures} translator check(s) FAILED.");
        return _failures == 0 ? 0 : 1;
    }

    private static void Check(string input, string expected) => Assert(RussianTextTranslator.Translate(input, "test"), expected, input);

    private static void CheckSubtitle(string input, string expected) => Assert(RussianTextTranslator.TranslateSubtitle(input), expected, input);

    private static void Assert(string actual, string expected, string input)
    {
        var ok = actual == expected && !RussianTextTranslator.ContainsCyrillic(actual);
        if (!ok)
        {
            _failures++;
            Console.WriteLine($"FAIL\n  input:    {input}\n  expected: {expected}\n  actual:   {actual}");
        }
    }
}
