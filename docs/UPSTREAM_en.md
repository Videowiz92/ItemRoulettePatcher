# Applying these fixes inside ItemRoulette (for the ItemRoulette author)

*Русская версия: [UPSTREAM_ru.md](UPSTREAM_ru.md)*

Hello! This repository patches ItemRoulette **1.4.4** from the outside, in memory, without changing your files. Everything below can
be done directly in your source so the patcher is no longer needed. Each section says what goes wrong, why, and shows the change.
Class and method names are the ones in your 1.4.4 assembly; the patcher's source (`src/ItemRoulettePatcher/Patches/`) is a working
reference for every change.

You are free to take any, all, or none of these. Two of them (the artifact and the Life Bet option) are design decisions, not bugs.

---

## 1. Quality equipment is dropped as the vanilla equipment

**Symptom.** With the Quality mod, hold a quality equipment (e.g. Executive Card, Tier 4) and win a different equipment on the
roulette. The old equipment drops as a plain vanilla Executive Card.

**Cause.** Quality hooks `Inventory.currentEquipmentIndex` (and `Inventory.GetActiveEquipment`) so that they always report the *base*
equipment. `RouletteManager.ExecuteGrantEquipmentServer` does
`PickupCatalog.FindPickupIndex(master.inventory.currentEquipmentIndex)`, which therefore finds the vanilla pickup. The real (quality)
index is still stored in the equipment slot; `Inventory.GetEquipment(location)` is not hooked and returns it.

**Fix.** Add a helper and use it wherever you need the *actual* equipment:

```csharp
// e.g. in PlayerUtil
public static EquipmentIndex GetStoredEquipmentIndex(Inventory inventory)
{
    if (!inventory) return EquipmentIndex.None;
    var location = inventory.activeEquipmentLocation;
    return inventory.GetEquipment(in location).equipmentIndex; // includes the Quality tier
}
```

Replace `inventory.currentEquipmentIndex` with `GetStoredEquipmentIndex(inventory)` in:

| Where | Why |
|---|---|
| `RouletteManager.ExecuteGrantEquipmentServer` | which pickup to drop (the bug) |
| `ChaosEventManager.ExecuteGrantElementalAspectServer` | same bug for aspect grants |
| `RouletteUI.Update`, `RouletteUI.FinishSpin`, `RouletteUI.HandleVoidDecisionResultReceived` | the Void-decision banner waits for `currentEquipmentIndex == _wonItem.PickupDef.equipmentIndex`; for a Quality equipment the left side is the base index and the right side is the quality index, so the banner never closes by itself |

Without Quality installed both values are identical, so nothing changes for other players.

---

## 2. Substandard Duplicator does nothing for roulette wins

**Symptom.** With a Substandard Duplicator (`DLC3Content.Items.Duplicator`), items won on the roulette do not get a temporary copy.

**Cause.** In vanilla the Duplicator is applied inside `ItemDef.AttemptGrant` (the pickup path). ItemRoulette grants with
`inventory.GiveItemPermanent(...)` directly (`ServerSpinService.GrantPendingAwardServer`, and the accept branch of
`ServerSpinService.ExecuteVoidDecisionOnServer`), so that code never runs.

**Fix.** After the permanent grant, repeat the vanilla branch:

```csharp
public static void ApplyDuplicator(CharacterMaster master, ItemIndex itemIndex)
{
    var inventory = master.inventory;
    if (!inventory || inventory.GetItemCountEffective(DLC3Content.Items.Duplicator) <= 0) return;

    var itemDef = ItemCatalog.GetItemDef(itemIndex);
    if (!itemDef) return;

    var toGrant = ItemIndex.None;
    if (itemDef.ContainsTag(ItemTag.CanBeTemporary))
        toGrant = itemIndex;
    else if (itemDef.DoesNotContainTag(ItemTag.ObjectiveRelated) &&
             itemDef.DoesNotContainTag(ItemTag.Scrap) &&
             itemDef.DoesNotContainTag(ItemTag.WorldUnique))
        toGrant = Util.RollTemporaryItemFromItemIndex(itemIndex);

    if (toGrant == ItemIndex.None) return;

    var body = master.GetBody();
    if (body)
        EffectManager.SpawnEffect(CharacterBody.CommonAssets.duplicatorPrefab,
            new EffectData { origin = body.corePosition, start = body.corePosition, scale = body.radius }, true);
    inventory.GiveItemTemp(toGrant, 1f);
}
```

Call it right after `inventory.GiveItemPermanent(pendingAward.PickupDef.itemIndex, 1)` in `GrantPendingAwardServer`, and after the
`GiveItemPermanent` in the Void accept branch.

*Note for Quality users:* the Quality mod changes the number of temporary copies by patching vanilla's private local function
`ItemDef.<AttemptGrant>g__HandleDuplicator|…`. If you want quality Duplicators to keep working for roulette wins, invoke that function by
reflection instead of calling `GiveItemTemp` yourself - see `DuplicatorPatch.cs` in this repo.

---

## 3. (Optional) Life Bet ignores Dio's Best Friend / Seed of Life

**Behaviour.** When a Life Bet is lost, `CasinoServerService.ExecutePendingCasinoAward` deals huge damage and then calls
`master.TrueKill()`. `TrueKill` turns every Dio's Best Friend / Pluripotent Larva into its consumed version, consumes Seed of Life
and cancels the pending respawn, so no extra life can ever save the player. This may well be what you intended - a Life Bet is a
hard bet - so I suggest making it a config option, default = your current behaviour:

```csharp
LifeBetRespectsExtraLives = config.Bind("Casino", "LifeBetRespectsExtraLives", false,
    "If true, a lost Life Bet is an ordinary lethal hit and extra lives (Dio's, Seed of Life) can revive the player. " +
    "If false, the player is hard-killed and extra lives are consumed.");
```

and in `ExecutePendingCasinoAward`, replace `master.TrueKill();` with:

```csharp
if (ModConfig.LifeBetRespectsExtraLives.Value)
{
    // The lethal TakeDamage above already started the normal death flow (and any revive).
    // Only finish the job if the body somehow survived (invulnerability buff etc.).
    if (body && body.healthComponent && body.healthComponent.alive)
        body.healthComponent.Suicide(null, null, default);
}
else
{
    master.TrueKill();
}
```

---

## 4. Russian text that appears when the language is English / Auto

`Localization.IsRussian` and the `Auto` detection work correctly. The leak comes from string literals that are Russian-only and never go
through `Localization.Get(ru, en)`/`Format(ru, en, ...)` or a language token. Replace each with a pair (or a token in
`ItemRoulette.Language.en/ru.ItemRoulette.json`). Each row below is the exact literal in 1.4.4 and the English text the patcher uses.

**`ChestRewardHooks`** - the `ServerEvents.Broadcast(...)` calls with a single Russian string (the `[ItemRoulette:Tag:...]` prefix must stay as is):

| Russian | English |
|---|---|
| `открыл Лунную Капсулу! Всем игрокам начислена 1 бесплатная крутка Лунных предметов!` | `opened a Lunar Pod! All players received 1 free Lunar item spin!` |
| `открыл Лунную Капсулу! Начислена 1 бесплатная крутка Лунных предметов.` | `opened a Lunar Pod! Awarded 1 free Lunar item spin.` |
| `открыл Лунный Мешок Падальщика! Всем игрокам начислено по 10 Лунных монет!` | `opened a Lunar Scavenger Sack! All players received 10 Lunar Coins each!` |
| `открыл Мешок Мусорщика! Всем игрокам начислено по {n} бесплатных круток ({m} на команду с округлением)!` | `opened a Scavenger's Sack! All players received {n} free spins each ({m} per team, rounded)!` |
| `открыл Колыбель Бездны! Предоставлен выбор бесплатной крутки Бездны.` (2 places) | `opened a Void Cradle! A free Void spin choice was granted.` |
| `открыл Потенциал Бездны! Начислена 1 бесплатная крутка (80% зеленый / 15% красный / 5% босс)!` (2 places) | `opened a Void Potential! Awarded 1 free spin (80% Green / 15% Red / 5% Boss)!` |

(The `Выгодная звезда` fragments elsewhere in `ChestRewardHooks` are already correctly paired with an English variant - `text4`/`text5`, and
`text5`/`text6` in the second method - so nothing to change there.)

**`ChaosEventManager`** - `chaosEventDefinition.Description = "..."` assigns one Russian string to both `DescriptionRu` and `DescriptionEn`
(the `Description` setter writes both). Set `DescriptionRu`/`DescriptionEn` separately, or use a token. Affected: second copy, +10 temporary
copies / 60 s, +5 spectral copies, cashback (`Возврат $…`), gold doubled, item copied (two variants), lost gold, lost item, no item to sacrifice,
curse received (two variants); plus the return values `Получен элементальный аспект!` / `Призваны союзные дроны поддержки!`, the fallback
name `Элементальный Аспект`, and the subtitles `"Спутник " + name` / `"Дрон " + name` (and the word `игрока`) assigned to `body.subtitleNameToken`.

**`ItemDescriptionHooks`** (Sale Star description) - the variable `text` is `"при убийстве боссов"` / `"при зарядке портала"` and is inserted into
the *English* template too. Make it a pair, e.g. `"on boss kill"` / `"on teleporter charge"` for English.

**`NotificationEntry`** - `StageFormatted` defaults to `"Этап 1"` (the parameterless constructor never sets it from a token).

**`ServerSpinService`** (3 places) - the fallback item names `"Предмет"` / `"Предмет Бездны"` used when `LocalizedName` is empty.

**`ModConfig`** - every BepInEx config description is Russian only. If you want English readers to be able to understand the `.cfg`, add English
text next to the Russian (config descriptions cannot change with the in-game language).

**`ItemRoulettePlugin.Awake`** - the log line `Все модули успешно загружены!`.

---

## 5. (Optional) Switching the whole mod with an artifact

The patcher adds an *Artifact of the Roulette* and turns ItemRoulette on/off from it at runtime without a restart. It does this with
two existing modules, so no new on/off machinery was needed in your code:

- `DropSuppressionModule` - `Initialize()` / `Cleanup()` register / remove every gameplay hook (all of your `*Hooks.Init/Cleanup` pairs
  are symmetric, which is what makes this work).
- `RouletteUIModule` - `Initialize()` / `Cleanup()` create / destroy the UI root with the HUD reel, casino, admin menu and hotkeys.

If you would like this natively, define an `ArtifactDef`, register it with `R2API.ContentAddition.AddArtifactDef`, and call those two modules'
`Initialize`/`Cleanup` when the artifact's rule choice changes (read it from `PreGameController.readOnlyRuleBook` in the lobby and `Run.ruleBook` in a run - `RunArtifactManager` only exists after the run has started, which is too late for stage 1) - see `RouletteGate.cs` and `RouletteArtifact.cs` in this repo.
One caveat: `Cleanup()` of the *other* modules (`Network`, `ServerSpinService`, `CasinoServerService`, `ServerEvents`, `RunState`) is empty,
so their message handlers stay registered. That is harmless while the UI and hooks are off, but it means they are not a full tear-down.

---

Thank you for the mod - I hope these notes are useful. If something here is wrong or you prefer a different approach, please say so in an
issue on this repository.
