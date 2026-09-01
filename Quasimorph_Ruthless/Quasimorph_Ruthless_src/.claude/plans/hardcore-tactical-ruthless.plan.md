# Plan: Hardcore Tactical Ruthless

**Source**: free-form request, 2026-09-01
**Target game**: Quasimorph `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Mod id**: `QuasimorphRuthless` · **Preset id**: `HardcoreTacticalRuthless`
**Complexity**: Medium — 6 phases, one of them a mandatory runtime spike

## Summary

A new, self-contained difficulty that appears as its own panel on the difficulty
screen next to Normal / Unfair / Custom. Choosing it makes the game harder in the
places that reward playing well — enemy competence, enemy numbers, information,
scarcity and consequence — and deliberately **not** in the place that only makes
fights longer: enemy hit points. Nothing about it helps the player. When the
preset is not the active one, the game is bit-for-bit vanilla.

## The design rule this mod is built on

> Difficulty should cost the player *decisions*, not *time*.

Every change is checked against three tests. A change ships only if it passes all three:

| Test | Meaning | What it rejects |
|---|---|---|
| **Not a cheat** | Does it help the player in any way? | Extra inventory, free loot, buffed mercs |
| **Not a sponge** | Does it just extend the same fight? | `EnemyHealth` inflation, flat damage-soak |
| **Answerable** | Can good play beat it? | Unavoidable damage, coin-flip deaths |

That is why `EnemyHealth` stays at the vanilla baseline while `EnemyLos`,
`EnemyActionPoint`, `HuntMemory` and `GrenadeChance` go up. An enemy that sees you
first, acts more often, remembers where you went and throws a grenade into your
cover is *harder to out-think*. An enemy with triple health is only harder to out-click.

## What I verified in the game, and how

Nothing below is assumed. Each row was read out of the shipped assemblies with
`Quasimorph_Thai/trainer_tools/inspect_types.py`.

| Question | Answer | Evidence |
|---|---|---|
| Does the game have a difficulty-preset system? | **Yes** — `MGSC.DifficultyPreset`, 45 fields | `layout DifficultyPreset` |
| Can a mod add one? | **Yes.** `Data.DifficultyPresets` is a plain `Dictionary<string, DifficultyPreset>` | `method Data:get_DifficultyPresets` |
| Will a new entry reach the UI? | **Yes.** `DifficultyScreen.OnEnable` enumerates the whole dictionary and calls `AddPanel(key, value)` per entry | `il DifficultyScreen:OnEnable` |
| What does a panel need to render? | An icon, from `preset.ContentDescriptor` → `.Icon` / `.ActiveIcon`. **Null would throw.** | `il DifficultyScreen:AddPanel`, `layout DifficultyPresetDescriptor` |
| Where does the panel's name come from? | `ui.difficulty.<Id>.name` / `.desc` | `Quasimorph_Thai_src/translations/078_ui.json`, 69 `ui.difficulty.*` keys |
| Which preset ids ship? | `Easy` (shown as Normal), `Normal`, `Hard` (shown as Unfair), `Custom`, `Custom_saved` | same file, `*.name` keys |
| Can a mod add a localization key? | `Localization.DuplicateKey(old, copy)` is **public static** and writes into every language dict | `il Localization:DuplicateKey` |
| Are AI behaviours moddable? | **Yes** — `Data.AiPresets` is `ConfigRecordCollection<AiPresetRecord>`, 30 behaviour fields | `layout AiPresetRecord` |
| Can records be enumerated? | **Yes**, all public: `Ids`, `Records`, `GetRecord`, `AddRecord`, `RemoveRecord` | methods of `ConfigRecordCollection` |
| Are enemy loadouts moddable? | **Yes** — `Data.MobClasses` is `ConfigRecordCollection<MobClassRecord>`, 40 fields | `layout MobClassRecord` |
| When can a mod edit all this? | `ModHookType.AfterConfigsLoaded` = 1, before any run starts | `enum ModHookType`, 20 hooks |
| Does a save remember the difficulty? | **Yes** — `SavedGameMetadata.DifficultyPresetId` is a string | `layout SavedGameMetadata` |

**Deliberately still open** — the config tables live inside Unity assets, not loose
JSON, so they cannot be read statically. Phase 0 exists to answer these from a
running game, and nothing after Phase 0 may be tuned until it has:

- (a) The vanilla field values of the `Hard` preset — the baseline every number is relative to.
- (b) The real `Data.AiPresets` record ids, and their vanilla values.
- (c) The real `Data.MobClasses` record ids, and which are player-side vs. hostile.
- (d) Whether `Localization.Get` is one funnel or two independent overloads.

## Patterns to mirror

The sibling Big Pack mod is the house style; this one follows it rather than inventing.

| Category | Source | Pattern |
|---|---|---|
| Entry point | `QuasimorphBigPack/BigPackMod.cs:44` | `[Hook(ModHookType...)]` public static, `State` taken from `IModContext` — **never** patch `State`, `GameLoop`, `Data` or anything on the bootstrap path |
| Failure isolation | `BigPackMod.cs:104` | Every hook body wrapped in `Guard(...)`; a broken mod must leave a working game |
| Config | `ModConfig.cs` | Plain `key=value` `config.txt`, written self-documenting on first run, clamped ranges, bad file → defaults + logged, no MCM dependency |
| Logging | `ModLog.cs` | Own log file next to the assembly, mirrored into `Player.log`, never throws |
| Build | `build.ps1` | Compile → stage → **verify every game reference resolves** → optional `-Install` |
| Ship-safety | `tools/apicheck.py` | Walks the built DLL's TypeRef/MemberRef tables against the real game assemblies. Non-negotiable: it is what catches a dropped overload before a user does |
| Referencing | `QuasimorphBigPack.csproj:36` | `<Private>false</Private>` on every game reference; never ship our own `0Harmony` |
| API discipline | `PROJECT_STATE.md` | Public members only where possible; reflection is a documented exception, not a habit |

## Architecture

Three layers, each switched off completely unless our preset is the active one.

```
AfterConfigsLoaded ─┬─ snapshot vanilla AiPresets + MobClasses   (restore source)
                    ├─ clone Data.DifficultyPresets["Hard"]      (baseline)
                    ├─ apply deltas -> HardcoreTacticalRuthless
                    ├─ register in Data.DifficultyPresets
                    └─ register localization for name + desc

SpaceStarted /     ─┬─ is State.Get<Difficulty>().Preset.Id == ours?
DungeonStarted /    │      yes -> apply tactical + loadout layers
AfterSaveLoaded     └─      no  -> restore vanilla snapshot
```

**Layer 1 — the preset (numbers).** Cloned from vanilla `Hard` and adjusted by
delta, never hardcoded absolutely. This matters: the mod inherits whatever the
game's own baseline is, so a patch that retunes `Hard` cannot silently make our
preset *easier* than the difficulty it is supposed to sit above. `ImmutableDifficulty`
is set, so a run committed to Ruthless stays there.

Consequence knobs, per the chosen harsh-but-recoverable ceiling: `DeathPenalty =
DieButMissionGone`, `RevivePenalty = TimePenalty`, `DropPenalty = Bag`, `EvacRules =
ByChip`, `DeathGift = false`, `LosePerks = false`. You can lose a mission badly and
still have a campaign.

**Layer 2 — tactical AI (`Data.AiPresets`).** The reason the mod exists. Sliders
cannot express "the enemy keeps hunting you", "the enemy opens the door you hid
behind", or "the enemy throws a grenade instead of walking into your kill zone".
These can. Direction of travel: `HuntMemory` and `InvestigateMemory` up,
`GrenadeChance` and `BestFiremodeChance` up, `CanOpenDoor` / `CanMeleeAttackDoor` /
`CanUseItems` on, `AvoidMineChance` and `AvoidDangerTerrainChances` up (your traps
are no longer free), `PeriodicallySleeps` off (no free opening kills).

**Layer 3 — loadouts and scarcity (`Data.MobClasses`).** `EquipmentTechLevelBonus`
up so enemies carry better gear — which cuts *both* ways, since that gear is also
your salvage, turning a harder fight into a real risk/reward decision.
`ItemConditionPercent` and `AdditAmmo` down so what you recover is worn and light.

**Restore, not reload.** Layers 2 and 3 mutate shared config records, so the
vanilla values are snapshotted at `AfterConfigsLoaded` and written back whenever a
run starts on a different preset. That is what keeps a vanilla save vanilla in the
same session.

## Files to create

| File | Action | Why |
|---|---|---|
| `mod_src/QuasimorphRuthless/QuasimorphRuthless.csproj` | CREATE | Mirrors Big Pack's csproj, including the `MSB3277` demotion and `<Private>false</Private>` |
| `mod_src/QuasimorphRuthless/modmanifest.json` | CREATE | `UniqueModName`, single assembly, no dependencies |
| `mod_src/QuasimorphRuthless/RuthlessMod.cs` | CREATE | Hook entry points + `Guard`, mirroring `BigPackMod.cs` |
| `mod_src/QuasimorphRuthless/ModLog.cs` | CREATE | Port of Big Pack's, renamed log file |
| `mod_src/QuasimorphRuthless/ModConfig.cs` | CREATE | `enabled`, per-layer switches, intensity clamps |
| `mod_src/QuasimorphRuthless/DifficultyRegistration.cs` | CREATE | Layer 1 — clone `Hard`, apply deltas, register, reuse `Hard`'s `ContentDescriptor` |
| `mod_src/QuasimorphRuthless/PresetTuning.cs` | CREATE | The delta table itself, in one place, commented with the reason per knob |
| `mod_src/QuasimorphRuthless/TacticalAi.cs` | CREATE | Layer 2 — snapshot / apply / restore over `Data.AiPresets` |
| `mod_src/QuasimorphRuthless/MobLoadouts.cs` | CREATE | Layer 3 — snapshot / apply / restore over `Data.MobClasses` |
| `mod_src/QuasimorphRuthless/ModStrings.cs` | CREATE | Thai + English name/desc, and the registration path for them |
| `mod_src/QuasimorphRuthless/ConflictCheck.cs` | CREATE | Warns when a loaded mod contradicts the intent (see Risks) |
| `mod_src/QuasimorphRuthless/DataProbe.cs` | CREATE | Phase 0 only — dumps ids and vanilla values to a text file |
| `build.ps1` | CREATE | Adapted from Big Pack's, same three stages |
| `tools/apicheck.py`, `tools/cli_meta.py` | CREATE | Copied unchanged from Big Pack |
| `README.md` | CREATE | What it changes and why, in the voice of the sibling READMEs |
| `PROJECT_STATE.md` | CREATE | Phase table, decisions, known risks, next action — per the project-state rule |

## Phases

### Phase 0 — Runtime data spike **(blocking)**
- **Action**: Build a probe-only DLL that, at `AfterConfigsLoaded`, writes every
  `Data.DifficultyPresets` entry field-by-field, every `Data.AiPresets` record and
  every `Data.MobClasses` record to `probe.txt`. Also logs which `Localization.Get`
  overload is the real funnel.
- **Why it blocks**: the config tables are inside Unity assets. Tuning before this
  is guessing, and guessed numbers are exactly what makes a difficulty mod unfun.
- **Validate**: `probe.txt` contains the `Hard` preset's 45 values and a non-empty id list.

### Phase 1 — Skeleton
- **Action**: csproj, manifest, `ModLog`, `ModConfig`, `build.ps1`, `tools/`.
- **Mirror**: Big Pack, file for file.
- **Validate**: `.\build.ps1` reports `Build succeeded`, `apicheck` reports 0 unresolved.

### Phase 2 — The preset appears
- **Action**: Clone `Hard`, apply the Phase-0-informed deltas, reuse `Hard`'s
  `ContentDescriptor` so the icon is never null, register the entry, register the
  Thai and English name/desc.
- **Validate**: In game — a fourth panel appears, reads correctly in both languages
  and is selectable. Starting a run on it records `DifficultyPresetId` in the save.

### Phase 3 — Tactical AI layer
- **Action**: Snapshot at config load; apply on run start when our preset is active;
  restore otherwise.
- **Validate**: In game — an enemy opens a closed door to reach you; an enemy throws
  a grenade at a covered position; an alerted enemy is still searching several turns
  later. Then start a vanilla Normal run in the same session and confirm none of it happens.

### Phase 4 — Loadouts and scarcity
- **Action**: `EquipmentTechLevelBonus`, `ItemConditionPercent`, `AdditAmmo`, `Los`.
- **Guard**: must apply to hostiles only — Phase 0 output decides which records those are.
- **Validate**: In game — enemy gear is a tier better, recovered gear is visibly worn,
  and player mercenaries are untouched.

### Phase 5 — Safety and honesty
- **Action**: Uninstall risk — a save written under `HardcoreTacticalRuthless` carries
  that id. Establish from a real test what the game does with an unknown preset id and,
  if it is not graceful, document it loudly in the README exactly as Big Pack documents
  its own destructive-uninstall case. Add the conflict warning.
- **Validate**: Save on our preset → disable the mod → load. Record the real behaviour.

### Phase 6 — Package, live test, tuning pass
- **Action**: Play several full missions. Tune against the three tests, not toward a
  target number.
- **Validate**: Honest state per phase in `PROJECT_STATE.md` — `PASS` only where a
  test was actually run.

## Validation

```powershell
# Build, verify every game reference resolves, install for testing
cd C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Ruthless\Quasimorph_Ruthless_src
.\build.ps1 -Install

# Reference resolution on its own
python tools\apicheck.py build\mod\QuasimorphRuthless.dll "C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game\Quasimorph_Data\Managed"
```

In-game evidence is required for every phase from 2 onward. "It compiles" is not a pass.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| A null `ContentDescriptor` throws in `AddPanel` and breaks the difficulty screen | **High if unhandled** | Reuse `Hard`'s descriptor reference. Verified as the exact field the panel reads |
| Uninstalling with a save on our preset leaves an unknown `DifficultyPresetId` | Medium | Phase 5 establishes the real behaviour before shipping; documented, as Big Pack documents its own |
| Layer 2/3 leak into a vanilla run in the same session | Medium | Snapshot-and-restore on every run start, not just on ours. Phase 3's validation explicitly tests this |
| Tuning lands on "tedious" instead of "hard" | Medium | The three tests are the acceptance criteria, and `EnemyHealth` is held at baseline by design. Phase 6 is a tuning pass, not a formality |
| Reducing panic/surrender removes fun mechanics and lengthens fights | Medium | Treat both as *modest* reductions and revisit in Phase 6 — they are the knobs most likely to fail the "not a sponge" test |
| `Localization.Get` is a hot path if patched | Low | Prefer the public `DuplicateKey` route confirmed in Phase 0; a prefix, if needed, does one string prefix check |
| **Big Pack, the sibling mod, contradicts this one** | High if both installed | Big Pack grants unlimited inventory and zero carry weight — a direct fail of the "not a cheat" test. `ConflictCheck` logs a clear warning and the README states they are not designed to run together. Not blocked; the player's call |
| A game patch retunes `Hard` under us | Low | Deltas from the live `Hard`, never absolute values, so we always sit above it |

## Explicit non-goals

No player buffs of any kind. No inventory, weight, carry or stack changes. No loot
added anywhere. No changes to any preset the player did not select. No new items,
enemies or missions — this is a difficulty mod, not a content mod.

## Acceptance

- [ ] Phase 0 evidence file exists and every tuned number traces to a value in it
- [ ] The panel appears, in Thai and in English, with a working icon
- [ ] A vanilla run started in the same session behaves exactly like vanilla
- [ ] Every change passes the not-a-cheat / not-a-sponge / answerable tests
- [ ] `apicheck` reports 0 unresolved references
- [ ] `PROJECT_STATE.md` states PASS / PARTIAL / UNTESTED honestly per phase
