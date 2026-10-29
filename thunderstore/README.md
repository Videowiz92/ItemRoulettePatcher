# ItemRoulettePatcher

A side-loaded patch for **[ItemRoulette](https://thunderstore.io/package/HorinisTeam/ItemRoulette/)**. It changes nothing on disk: it
runs next to ItemRoulette and patches it in memory, so ItemRoulette stays untouched and updates normally. Remove this mod and
ItemRoulette goes back to exactly what it was.

Built against ItemRoulette **1.4.4**. If a future ItemRoulette release moves something, each patch checks that its target still
exists and is skipped with a log warning instead of breaking the game.

## What it does

### Artifact of the Roulette
Every ItemRoulette mechanic - the HUD reel, replaced chests and drops, the casino, Life Bet, chaos events, the hotkeys - is now
behind an artifact on the lobby screen. Enable it for a run to play with ItemRoulette; leave it off to play the run without it.
No client restart needed. (Config `RequireArtifact = false` brings back the old always-on behaviour.)

### Fixes

| Problem | Fix | Config |
|---|---|---|
| Replacing a Quality equipment through the roulette dropped the plain vanilla equipment (Quality Executive Card Tier 4 became a vanilla Executive Card). | The old equipment is dropped at its real Quality tier. | `PreserveQualityEquipment` (on) |
| With a Substandard Duplicator, items won on the roulette never got their temporary copy. | Roulette wins (and accepted Void items) trigger the Duplicator like a normal pickup. | `DuplicatorOnRouletteItems` (on) |
| Losing a Life Bet ignores Dio's Best Friend, Pluripotent Larva and Seed of Life. | Optional: a lost bet becomes an ordinary lethal hit that extra lives can save you from. | `LifeBetRespectsExtraLives` (**off**, ItemRoulette's behaviour) |
| Russian text appears even when the language is English / Auto (chest broadcasts, chaos-event descriptions, the Sale Star description, ...). | Those Russian-only messages are translated to English. | `TranslateRussianFallbackText` (on) |

Config file: `BepInEx/config/com.itemroulettepatcher.fixes.cfg`. All options are also in Risk of Options.

## Multiplayer
Everyone in the lobby needs this mod (it adds an artifact). The equipment, Duplicator and Life Bet fixes run on the host.
Translation runs on each player's own client.

## Icons / AI note
The mod's icons (the in-game artifact icon and this page's icon) were made with AI as placeholders. You can use your own artifact icon without
rebuilding: put `artifact_on.png` and `artifact_off.png` next to `ItemRoulettePatcher.dll` (square, e.g. 128x128 or 256x256). See the GitHub README for details.

*Иконки мода (артефакт в игре и иконка страницы) созданы с помощью ИИ и служат заглушками. Свои иконки артефакта можно добавить без пересборки: положите `artifact_on.png` и `artifact_off.png` рядом с `ItemRoulettePatcher.dll`.*

## Reporting leftovers
If Russian text still shows up while ItemRoulette is set to English, the game log gets a line starting with
`Untranslated Russian text` - please include it in a bug report.

## Source and notes for the ItemRoulette author
https://github.com/Videowiz92/ItemRoulettePatcher - includes step-by-step instructions (English and Russian) for applying every
fix directly in ItemRoulette so this patcher is no longer needed.

---

## ItemRoulettePatcher (по-русски)

Дополнение-«патчер» для **ItemRoulette**: работает рядом с модом и правит его в памяти, не изменяя его файлы. Если удалить патчер,
ItemRoulette работает как раньше. Версия под ItemRoulette **1.4.4**.

- **Артефакт Рулетки** - вся механика ItemRoulette включается артефактом на экране лобби, перезапуск клиента не нужен.
- **Качественное снаряжение** при замене через рулетку выпадает с настоящим уровнем качества, а не обычным.
- **Несовершенный дубликатор** теперь даёт временную копию предметов, выигранных в рулетке.
- **Ставка жизни** (по желанию, `LifeBetRespectsExtraLives`): запасные жизни (Dio's Best Friend, Seed of Life) могут спасти после проигрыша.
- **Русский текст** при английском/Auto-языке переводится на английский.

Исходный код и инструкции для автора мода (на русском и английском): https://github.com/Videowiz92/ItemRoulettePatcher
