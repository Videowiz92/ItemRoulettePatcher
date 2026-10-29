# In-game test checklist

Install the built DLL (the build copies it to `BepInEx/plugins/ItemRoulettePatcher` when `DeployDir` is set) in a profile with ItemRoulette,
Quality and Risk of Options. Look for these lines in `BepInEx/LogOutput.log` on startup:

- `ItemRoulettePatcher 1.0.0 loaded (patching ItemRoulette 1.4.4).`
- no `Patch '...' failed` errors, and no `ItemRoulette method not found` / `type not found` warnings.

## Artifact
1. Main menu → start a lobby. **Artifact of the Roulette** is in the artifact list (wheel icon).
1b. Artifact **on**, stage 1: ItemRoulette's suppression is already active when the stage is generated - no chests, printers or
   Shrines of Chance spawn on the very first stage (previously the hooks came on only after stage 1 had been populated).
2. Artifact **off**: start a run. Chests, shrines and printers spawn normally; `B`, `V`, `N`, `J` do nothing; no roulette HUD.
   Log: `Roulette artifact not enabled: ItemRoulette mechanics OFF.`
3. Quit to lobby, enable the artifact, start a run: ItemRoulette behaves as before (chests removed, `B` spins, HUD reel, casino with `N`).
   Log: `Roulette artifact enabled: ItemRoulette mechanics ON.`
4. Repeat 2 and 3 once more without restarting the game (this is the "switch without restart" claim).
5. Set `RequireArtifact = false`: ItemRoulette is always on, as without the patcher.
6. Multiplayer: the host's artifact choice applies to every client; both need the patcher.

## Quality equipment drop
1. Give yourself a Quality equipment (e.g. Executive Card Tier 4) and make the next roulette win an equipment (use the equipment spin / free equipment rolls).
2. The old equipment drops as the **Quality** item with the same tier, not the vanilla one.
3. With a quality equipment as the Void-decision prize, accepting it closes the banner on its own.
4. `PreserveQualityEquipment = false` restores the old behaviour (vanilla drop).

## Substandard Duplicator
1. Have a Substandard Duplicator; spin with `B` and win an item.
2. You get the permanent item **and** a temporary copy (duplicator effect plays, buff-bar shows the temporary item).
3. Accept a Void item from a decision: same.
4. `DuplicatorOnRouletteItems = false` restores the old behaviour.

## Life Bet
1. Casino (`N`) → place a Life Bet and lose, with Dio's Best Friend in the inventory.
2. Default (`LifeBetRespectsExtraLives = false`): you die and Dio's is consumed without saving you (ItemRoulette's original behaviour).
3. Set it to `true`: you die, Dio's triggers and you respawn. Repeat with Seed of Life as equipment.

## Language
1. ItemRoulette `Language = Auto` with the game in English. Open Lunar Pods, Void Cradles, scavenger sacks, trigger chaos events, read the Sale Star description.
2. No Russian text in toasts, journal (`J`) or descriptions.
3. If any Russian still appears, copy the `Untranslated Russian text (...)` line from `LogOutput.log` into an issue.
4. Set ItemRoulette `Language = Russian`: text stays Russian (the translator must stay out of the way).
