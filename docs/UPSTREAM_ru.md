# Как применить эти исправления прямо в ItemRoulette (для автора ItemRoulette)

*English version: [UPSTREAM_en.md](UPSTREAM_en.md)*

> Перевод подготовлен с помощью ИИ. Если что-то непонятно или звучит неточно, ориентируйтесь на английскую версию и на код в репозитории.

Здравствуйте! Этот репозиторий правит ItemRoulette **1.4.4** снаружи, в памяти, не изменяя ваши файлы. Всё описанное ниже можно сделать
прямо в вашем исходном коде, и тогда патчер станет не нужен. В каждом разделе: что происходит, почему и как исправить. Имена классов и
методов взяты из вашей сборки 1.4.4; исходники патчера (`src/ItemRoulettePatcher/Patches/`) - рабочий пример каждого изменения.

Вы вправе взять любое из исправлений, все сразу или ни одного. Два пункта (артефакт и опция Ставки жизни) - это вопрос дизайна, а не ошибки.

---

## 1. Качественное (Quality) снаряжение выпадает как обычное

**Симптом.** С модом Quality держите качественное снаряжение (например, Executive Card, Tier 4) и выигрываете в рулетке другое снаряжение.
Старое выпадает как обычная ванильная Executive Card.

**Причина.** Quality перехватывает `Inventory.currentEquipmentIndex` (и `Inventory.GetActiveEquipment`), чтобы они всегда возвращали
*базовое* снаряжение. `RouletteManager.ExecuteGrantEquipmentServer` вызывает
`PickupCatalog.FindPickupIndex(master.inventory.currentEquipmentIndex)` и поэтому находит ванильный предмет. Настоящий (качественный)
индекс по-прежнему хранится в слоте; `Inventory.GetEquipment(location)` не перехвачен и возвращает именно его.

**Исправление.** Добавьте вспомогательный метод и используйте его везде, где нужно *фактическое* снаряжение:

```csharp
// например, в PlayerUtil
public static EquipmentIndex GetStoredEquipmentIndex(Inventory inventory)
{
    if (!inventory) return EquipmentIndex.None;
    var location = inventory.activeEquipmentLocation;
    return inventory.GetEquipment(in location).equipmentIndex; // содержит уровень качества
}
```

Замените `inventory.currentEquipmentIndex` на `GetStoredEquipmentIndex(inventory)` в:

| Где | Зачем |
|---|---|
| `RouletteManager.ExecuteGrantEquipmentServer` | какой предмет выбросить (сама ошибка) |
| `ChaosEventManager.ExecuteGrantElementalAspectServer` | та же ошибка при выдаче аспекта |
| `RouletteUI.Update`, `RouletteUI.FinishSpin`, `RouletteUI.HandleVoidDecisionResultReceived` | окно решения о предмете Бездны ждёт `currentEquipmentIndex == _wonItem.PickupDef.equipmentIndex`; для качественного снаряжения слева базовый индекс, справа качественный, поэтому окно само не закрывается |

Без мода Quality оба значения совпадают, так что для остальных игроков ничего не меняется.

---

## 2. Несовершенный дубликатор (Substandard Duplicator) не работает для выигрышей в рулетке

**Симптом.** При наличии Substandard Duplicator (`DLC3Content.Items.Duplicator`) предметы, выигранные в рулетке, не дают временной копии.

**Причина.** В ванильной игре дубликатор срабатывает внутри `ItemDef.AttemptGrant` (подбор предмета). ItemRoulette выдаёт предметы напрямую через
`inventory.GiveItemPermanent(...)` (`ServerSpinService.GrantPendingAwardServer` и ветка принятия в `ServerSpinService.ExecuteVoidDecisionOnServer`),
поэтому этот код не выполняется.

**Исправление.** После постоянной выдачи повторите ванильную ветку:

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

Вызывайте её сразу после `inventory.GiveItemPermanent(pendingAward.PickupDef.itemIndex, 1)` в `GrantPendingAwardServer` и после
`GiveItemPermanent` в ветке принятия предмета Бездны.

*Замечание про Quality:* мод Quality меняет число временных копий, патча приватную локальную функцию ванили
`ItemDef.<AttemptGrant>g__HandleDuplicator|…`. Чтобы качественные дубликаторы продолжали работать и для выигрышей в рулетке, вызывайте эту функцию
через рефлексию вместо собственного `GiveItemTemp` - см. `DuplicatorPatch.cs` в этом репозитории.

---

## 3. (По желанию) Ставка жизни игнорирует Dio's Best Friend / Seed of Life

**Поведение.** При проигрыше Ставки жизни `CasinoServerService.ExecutePendingCasinoAward` наносит огромный урон и затем вызывает
`master.TrueKill()`. `TrueKill` превращает все Dio's Best Friend / Pluripotent Larva в «использованные», расходует Seed of Life и отменяет
запланированное возрождение, поэтому никакая запасная жизнь не спасёт игрока. Возможно, так и задумано - ставка жизни это жёсткая ставка, -
поэтому предлагаю сделать опцию в конфиге; значение по умолчанию = нынешнее поведение:

```csharp
LifeBetRespectsExtraLives = config.Bind("Casino", "LifeBetRespectsExtraLives", false,
    "Если true, проигранная Ставка жизни - обычный смертельный удар, и запасные жизни (Dio's, Seed of Life) могут воскресить игрока. " +
    "Если false, игрок убивается жёстко, а запасные жизни расходуются.");
```

и в `ExecutePendingCasinoAward` замените `master.TrueKill();` на:

```csharp
if (ModConfig.LifeBetRespectsExtraLives.Value)
{
    // Смертельный TakeDamage выше уже запустил обычную смерть (и возможное воскрешение).
    // Добиваем только если тело каким-то образом выжило (бафф неуязвимости и т.п.).
    if (body && body.healthComponent && body.healthComponent.alive)
        body.healthComponent.Suicide(null, null, default);
}
else
{
    master.TrueKill();
}
```

---

## 4. Русский текст при английском языке / Auto

`Localization.IsRussian` и определение `Auto` работают правильно. Утечка идёт от строковых литералов только на русском, которые не проходят через
`Localization.Get(ru, en)` / `Format(ru, en, ...)` и не используют токены языка. Замените каждый на пару (или на токен в
`ItemRoulette.Language.en/ru.ItemRoulette.json`). Ниже - точные литералы из 1.4.4 и английский текст, который использует патчер.

**`ChestRewardHooks`** - вызовы `ServerEvents.Broadcast(...)` с одной русской строкой (префикс `[ItemRoulette:Tag:...]` оставьте как есть):

| Русский | English |
|---|---|
| `открыл Лунную Капсулу! Всем игрокам начислена 1 бесплатная крутка Лунных предметов!` | `opened a Lunar Pod! All players received 1 free Lunar item spin!` |
| `открыл Лунную Капсулу! Начислена 1 бесплатная крутка Лунных предметов.` | `opened a Lunar Pod! Awarded 1 free Lunar item spin.` |
| `открыл Лунный Мешок Падальщика! Всем игрокам начислено по 10 Лунных монет!` | `opened a Lunar Scavenger Sack! All players received 10 Lunar Coins each!` |
| `открыл Мешок Мусорщика! Всем игрокам начислено по {n} бесплатных круток ({m} на команду с округлением)!` | `opened a Scavenger's Sack! All players received {n} free spins each ({m} per team, rounded)!` |
| `открыл Колыбель Бездны! Предоставлен выбор бесплатной крутки Бездны.` (2 места) | `opened a Void Cradle! A free Void spin choice was granted.` |
| `открыл Потенциал Бездны! Начислена 1 бесплатная крутка (80% зеленый / 15% красный / 5% босс)!` (2 места) | `opened a Void Potential! Awarded 1 free spin (80% Green / 15% Red / 5% Boss)!` |

(Фрагменты `Выгодная звезда` в остальных местах `ChestRewardHooks` уже правильно спарены с английским вариантом - `text4`/`text5`, а во втором
методе `text5`/`text6`, - там менять ничего не нужно.)

**`ChaosEventManager`** - `chaosEventDefinition.Description = "..."` записывает одну русскую строку и в `DescriptionRu`, и в `DescriptionEn`
(сеттер `Description` пишет в оба поля). Задавайте `DescriptionRu`/`DescriptionEn` отдельно или используйте токен. Затронуты: вторая копия, +10
временных копий / 60 с, +5 спектральных копий, кэшбэк (`Возврат $…`), удвоение золота, копирование предмета (два варианта), потеря золота, потеря
предмета, нет предметов для жертвы, получено проклятие (два варианта); а также возвращаемые строки `Получен элементальный аспект!` /
`Призваны союзные дроны поддержки!`, запасное имя `Элементальный Аспект` и подзаголовки `"Спутник " + имя` / `"Дрон " + имя` (и слово `игрока`),
которые записываются в `body.subtitleNameToken`.

**`ItemDescriptionHooks`** (описание Sale Star) - переменная `text` равна `"при убийстве боссов"` / `"при зарядке портала"` и подставляется также в
*английский* шаблон. Сделайте пару, например `"on boss kill"` / `"on teleporter charge"` для английского.

**`NotificationEntry`** - `StageFormatted` по умолчанию равен `"Этап 1"` (конструктор без параметров не берёт значение из токена).

**`ServerSpinService`** (3 места) - запасные названия предмета `"Предмет"` / `"Предмет Бездны"`, когда `LocalizedName` пуст.

**`ModConfig`** - все описания параметров BepInEx только на русском. Чтобы англоязычные игроки могли понять `.cfg`, добавьте английский текст рядом с
русским (описания в конфиге не могут меняться вместе с языком игры).

**`ItemRoulettePlugin.Awake`** - строка лога `Все модули успешно загружены!`.

---

## 5. «Пауза в одиночной игре» (`PauseGameWhileOpen`) ничего не делает

**Симптом.** Параметр создаётся в `ModConfig` и показан в `RiskOfOptionsCompat`, но нигде не читается, поэтому одиночная игра никогда не встаёт на паузу.

**Исправление.** Пока открыто окно рулетки (состояния `RouletteUI` Spinning / Finished / VoidDecision, `CasinoRouletteUI.IsOpen`, `AdminMenuUI.IsOpen`) в
одиночной игре (`RoR2Application.isInSinglePlayer`) при включённом параметре задавайте `Time.timeScale = 0` и возвращайте прежнее значение при закрытии окна
(только если `PauseManager.isPaused` равно false, чтобы не снять паузу из-под настоящего меню паузы). Ваш интерфейс уже анимируется по нескалированному времени, поэтому лента
продолжает двигаться. Также возвращайте масштаб времени при завершении забега. См. `PauseWhileOpenPatch.cs`.

---

## 6. (По желанию) Включение всего мода артефактом

Патчер добавляет *Артефакт Рулетки* и включает/выключает ItemRoulette прямо во время игры, без перезапуска. Для этого он использует два уже
существующих модуля, так что новая логика вкл/выкл в вашем коде не понадобилась:

- `DropSuppressionModule` - `Initialize()` / `Cleanup()` регистрируют / снимают все игровые хуки (все ваши пары `*Hooks.Init/Cleanup` симметричны,
  благодаря этому всё и работает).
- `RouletteUIModule` - `Initialize()` / `Cleanup()` создают / уничтожают корневой объект интерфейса: полосу рулетки на HUD, казино, админ-меню и горячие клавиши.

Если хотите сделать это нативно: объявите `ArtifactDef`, зарегистрируйте через `R2API.ContentAddition.AddArtifactDef` и вызывайте
`Initialize`/`Cleanup` этих двух модулей при изменении выбора артефакта (читайте его из `PreGameController.readOnlyRuleBook` в лобби и `Run.ruleBook` в забеге - `RunArtifactManager` появляется только после старта забега, а для первой стадии это уже поздно) - см. `RouletteGate.cs` и `RouletteArtifact.cs` в этом репозитории.
Оговорка: `Cleanup()` *остальных* модулей (`Network`, `ServerSpinService`, `CasinoServerService`, `ServerEvents`, `RunState`) пустой, поэтому их
обработчики сообщений остаются зарегистрированными. Пока интерфейс и хуки выключены, это безвредно, но полноценной «разборкой» не является.

Для иконки артефакта используйте свою графику: задайте `ArtifactDef.smallIconSelectedSprite` / `smallIconDeselectedSprite` (квадратные, например 128x128 или 256x256; иконка выключенного состояния обычно темнее). Иконка в этом репозитории - заглушка, созданная ИИ и нарисованная в коде (`ArtifactIcon.cs`); пожалуйста, замените её.

---

Спасибо за мод - надеюсь, эти заметки будут полезны. Если что-то здесь неверно или вы предпочитаете другой подход, напишите, пожалуйста, в issue этого репозитория.
