# Project State — Quasimorph Hardcore Tactical Ruthless

**Mod version**: 0.1.0 — a new difficulty with a tactical AI and loadout layer
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Phase**: 6 of 6 — Live test | **Status**: BUILT AND REFERENCE-VERIFIED, NOT YET RUN IN GAME
**Updated**: 2026-09-01
**Branch**: `feat/hardcore-tactical-ruthless`

Plan: `.claude/plans/hardcore-tactical-ruthless.plan.md`

## Phases

| Phase | What | Status |
|---|---|---|
| 0 | Runtime data spike | **FOLDED IN** — shipped as `probe=true` rather than a throwaway build. Not yet run. |
| 1 | Skeleton, config, logging, build | **COMPLETE** — builds clean, 0 warnings, apicheck 154/154 |
| 2 | Preset registration + localization | **IMPLEMENTED, UNTESTED** |
| 3 | Tactical AI layer | **IMPLEMENTED, UNTESTED** |
| 4 | Loadout and scarcity layer | **IMPLEMENTED, UNTESTED** |
| 5 | Safety — conflict warning, uninstall risk | **PARTIAL** — warning implemented; the uninstall behaviour itself is still unmeasured |
| 6 | Package and live test | **BLOCKED** — needs a game launch |

## What "UNTESTED" means here

The mod compiles with zero warnings and **all 154 of its game member references resolve
against the shipped assemblies**. Every API assumption was read out of the game's own
IL rather than guessed — see the evidence table in the plan.

**None of it has been run.** Harmony applies patches at runtime, and the following are
unproven: that a new dictionary entry really does render a panel, that the localization
prefix really does reach `LocalizableLabel`, that `State.Get<Difficulty>()` resolves,
and whether the tuning is any fun.

## Phase 0 — answered from IL, not from a spike

| Question | Answer | Evidence |
|---|---|---|
| Can a mod add a difficulty? | **Yes.** `Data.DifficultyPresets` is a plain `Dictionary<string, DifficultyPreset>` | `method Data:get_DifficultyPresets` |
| Will it reach the UI? | **Yes.** `DifficultyScreen.OnEnable` enumerates the whole dictionary, `AddPanel` per entry | `il DifficultyScreen:OnEnable` |
| What breaks a panel? | A null `ContentDescriptor` — `AddPanel` dereferences `.Icon` with no null check | `il DifficultyScreen:AddPanel` |
| Panel name source | `ui.difficulty.<Id>.name` / `.desc` | Thai mod's `078_ui.json`, 69 `ui.difficulty.*` keys |
| Is `Localization.Get` one funnel? | **No.** Two independent overloads, each reading a different private field. Both need patching. | `il Localization:Get` |
| Public way to add a key? | `DuplicateKey` is public but only *copies* an existing string — not enough for new text | `il Localization:DuplicateKey` |
| Are AI behaviours moddable? | **Yes.** `ConfigRecordCollection<AiPresetRecord>`, all accessors public | `layout AiPresetRecord` |
| Are player mercs in `MobClasses`? | **No** — `Data.MercenaryClasses` is a separate collection | `Data` static field dump |

Still open, and only a real game can answer:

- (a) Does the new panel render, and is it selectable and startable?
- (b) Do the Thai and English strings reach the label?
- (c) Does `State.Get<Difficulty>()` resolve, or does the layer gate fail closed forever?
- (d) What does the game do loading a save whose difficulty id no longer exists?
- (e) Are `GrenadeChance` and the panic chances 0–1 or 0–100? `Tuning.ScaleChance`
      calibrates per value so it is correct either way, but the probe should confirm.
- (f) Is the result actually *fun*, or merely hard?

## Architecture

Three layers, all gated on the active preset, none of them on the bootstrap path.
The rule inherited from the sibling mods holds: nothing patches `State`, `GameLoop`,
`Data` or the bootstrap, after a Workshop mod black-screened the game doing exactly that.

| Piece | How |
|---|---|
| Preset | Cloned from vanilla `Hard` and adjusted **by delta, never absolutely**, so a patch that retunes Hard cannot leave this mode below it. Shares Hard's `ContentDescriptor` so the icon can never be null. |
| Localization | Prefix on both `Localization.Get` overloads, answering for two keys of our own and passing everything else through. Thai vs English decided by reading a vanilla string back and looking for Thai codepoints — same technique as the Thai mod, so load order does not matter. |
| Layer gate | `State.Get<Difficulty>().Preset.Id`, compared ordinally. **Fails closed**: if the difficulty cannot be read, the layers stay off and the game stays vanilla. |
| Restore | Vanilla values snapshotted at `AfterConfigsLoaded` before anything is written, and copied back whenever a run starts on another difficulty. Config records are shared global state; this is what keeps a vanilla save vanilla in the same session. |
| Chance scaling | `ScaleChance` never raises a zero (a behaviour the designers disabled stays disabled) and reads its ceiling off the vanilla value, so it is correct whether a field is 0–1 or 0–100. |
| Tool use | Only presets that already use items, throw grenades or pick firemodes get doors and item use. A mindless horror does not learn to turn a handle. |

## Decisions

- 2026-09-01 — Delivery: a new selectable preset plus gated behaviour layers, not an
  always-on overlay. Vanilla difficulties stay bit-for-bit vanilla. (Owner.)
- 2026-09-01 — Ruthlessness ceiling: harsh but recoverable. No permadeath, no full
  drop, perks and rank kept. The run-to-run loop is where the fun is. (Owner.)
- 2026-09-01 — Language: Thai and English, carried by the mod, no dependency on the
  sibling translation. (Owner.)
- 2026-09-01 — `EnemyHealth` held at vanilla. It is the canonical sponge knob and
  fails the not-a-sponge test by definition. This is the mod's clearest design statement.
- 2026-09-01 — `PeriodicallySleeps` held at vanilla. Removing sleeping enemies deletes
  a stealth option, making the game less tactical rather than more.
- 2026-09-01 — `MagnumCraftingTime` and `FactionGrowthSpeed` left untouched: the field
  names and the UI labels imply opposite directions and the difference is unverified.
  Tuning them would be a coin flip on whether the mode gets harder or easier.
- 2026-09-01 — `MissionStageCountMod` untouched: more floors is more minutes, not more
  difficulty.
- 2026-09-01 — Dropped the planned `affect_allies` config key. Allied NPCs cannot be
  told apart from hostiles in `MobClasses`, so the switch could not have honoured its
  own description. Shipping no switch beats shipping one that lies.
- 2026-09-01 — Panic and surrender reduced only 25%. They are good mechanics; gutting
  them would lengthen every fight, which is the sponge failure in a different costume.

## Known risks

- **Uninstalling with a save made on this difficulty.** The save records
  `HardcoreTacticalRuthless` in `SavedGameMetadata.DifficultyPresetId`. Whether the
  game falls back gracefully or refuses the save is **unmeasured**. Documented in the
  README; measuring it is the first job of the live test.
- Tuning may land on tedious rather than hard. Phase 6 is a real tuning pass, judged
  against the three tests, not a formality.
- Big Pack contradicts this mod's premise. Warned about in the log, not blocked.

## Probe checklist

`probe=true` is currently set in the installed config at
`%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphRuthless\config.txt`.
That file is generated at runtime and lives outside this repository, so it is not
tracked here.

Reaching the **main menu is enough** — the dump runs at `AfterConfigsLoaded`. It writes
`probe.txt` and `QuasimorphRuthless.log` beside the DLL. `HardcoreTacticalRuthless`
will *not* appear in `probe.txt`: the dump runs before registration on purpose, so it
is a clean vanilla baseline.

### The check that answers most of it

Read each preset field **across `Easy` → `Normal` → `Hard`** rather than reading `Hard`
alone. The direction the vanilla ladder moves is the ground truth for which way is
"harder", and it validates the sign of every multiplier in `PresetTuning` in one pass.
(`Easy` displays as *Normal* in game, `Hard` as *Unfair*.)

| Field | Assumed harder = | Expect across the ladder |
|---|---|---|
| `EnemyLos`, `EnemyActionPoint`, `EnemyDamageMult`, `EnemyDodgeMult`, `EnemyResistance` | higher | rising |
| `MonsterPoints`, `ExpMult`, `WeightSatietyDrain`, `QmorphLevelGrowth` | higher | rising |
| `ItemPoints`, `KilledMobsItemsCond`, `BarterValue`, `MissionRewardPoints`, `FactionReputation`, `ProcMissionLifetime` | lower | falling |

Any field moving the other way means that multiplier is inverted and is currently
making the mode **easier**.

### Blocking — a failure here means something is broken

- `--- Hard ---` exists and reports `icon descriptor present`. A `NULL` here, or a
  missing `Hard`, is the one condition that can break the whole difficulty screen.
- The log reports `patch targets resolved: both Localization.Get overloads`. Without
  it the panel renders a raw localization key instead of a name.
- `Data.MobClasses` is non-empty and no id looks player-side (`merc`, `player`,
  `ally`). This is the evidence behind the claim that the player's squad is
  structurally out of reach of layer 3.

### Tuning — a failure here means numbers need changing

- Multiply the deltas out by hand. `ItemPoints x0.70` and `KilledMobsItemsCond x0.60`
  are the two most likely to overshoot from scarcity into misery if `Hard` is already low.
- `EnemyLos x1.30`: if `Hard` is already near 1.5 the product approaches 2.0, at which
  point the player never gets the first shot. That fails the answerable test.
- Count the `[thinker]` tags. Zero means the heuristic caught nothing and the door
  layer is inert; every record tagged means it is too loose. Humanoids tagged and
  monsters untagged is the intended shape.
- Check whether `CanOpenDoor` is *already* true on the thinkers in vanilla. If it is,
  the README's "doors no longer stop anyone" is overstated and must be softened.
- `HuntMemory` / `InvestigateMemory` magnitude. Values of 3-8 make x1.75 meaningful;
  values in the hundreds mean the units are something else and x1.75 may amount to
  "hunts forever".
- Any chance field above 1.0 proves that table is 0-100 rather than 0-1.
  `Tuning.ScaleChance` calibrates per value so it is safe either way, but it should
  be recorded.
- `EquipTechLevelBonus` baseline and the `ItemConditionPercent` range. If condition is
  already something like `10..30`, x0.70 makes salvage close to worthless.

### Settles the two deliberately untuned knobs

`MagnumCraftingTime` and `FactionGrowthSpeed` are untouched because the field names and
the UI labels imply opposite directions. The ladder resolves both:

- `MagnumCraftingTime` **rising** Easy→Hard means it is a duration, so tuning multiplies
  *up*. **Falling** means the UI label was right, it is a speed, and tuning multiplies *down*.
- Same reading for `FactionGrowthSpeed`.

## Next action

Install and launch:

```powershell
cd C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Ruthless\Quasimorph_Ruthless_src
.\build.ps1 -Install
```

Then, in order:

1. Launch to the main menu and work through the probe checklist above. Retune
   `PresetTuning`, `TacticalAi` and `MobLoadouts` against the measured values, and
   record the answers to (d), (e) and the two untuned knobs.
2. Open the difficulty screen. Confirm the fourth panel renders with an icon and the
   right text, in both languages.
3. Start a run on it. Confirm from the log that the layers switched ON.
4. Start a vanilla Normal run in the same session. Confirm from the log that the layers
   switched OFF, and that enemies behave vanilla.
5. Play several missions. Judge against the three tests and retune.
6. Save on Ruthless, disable the mod, load. Record what actually happens and update the
   README's uninstall section with the measured answer.
