# Plan: Retinue — a squad that fights so you don't have to

**Source**: free-form request, 2026-09-01
**Target game**: Quasimorph `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Mod id**: `QuasimorphRetinue` · **Folder**: `Quasimorph_Retinue/`
**Complexity**: Medium — 6 phases, one runtime probe, no new UI
**Companion to**: `Quasimorph_Ruthless` (not a dependency; either works alone)

## Summary

You want to play Hardcore Tactical Ruthless as a **support player**: walk with a
squad, heal them, hand them ammo, call the shots, and let them do the killing.
Quasimorph already has almost all of that — a full ally system with follow/wait
and shoot/hold-fire orders, ally inventories, and medkits you can apply to them.
What it does not have is a **reliable supply of allies** or allies **strong enough
to matter** on the hardest settings.

This mod supplies both, and nothing else. It hands you a retinue that spawns with
you and is topped up every floor, makes each ally meaningfully stronger than the
mob it was built from, widens the game's own gift-to-ally mechanic so you can
recruit more of what you meet, and offers an opt-in switch that makes enemies stop
targeting you entirely.

## The design rule this mod is built on

The Ruthless mod is governed by *"difficulty should cost the player decisions, not
time"*, and its first test is **NOT A CHEAT**. This mod openly fails that test, and
that is the point — so it ships as a **separate mod**, never as a Ruthless layer.
Ruthless stays a statement about enemy competence; Retinue is a statement about who
does the fighting. Install both and you get a hard game you watch. Install one and
you get exactly what that one promises.

Retinue has its own three tests instead:

| Test | Meaning | What it rejects |
|---|---|---|
| **Your squad, not your stats** | Does the power sit in the allies, or in the player? | Buffing the player's merc, free loot, damage immunity by default |
| **Vanilla mechanics first** | Does the game already do this? | New UI, new item types, a custom command system |
| **Enemies untouched** | Could this accidentally buff a hostile? | Anything written to a shared `AiPresetRecord` or `MobClassRecord` |

The third test is why this mod works at the **creature-instance** level and never at
the config-record level. It is the mirror image of the Ruthless mod's guarantee that
it cannot reach the player's mercenaries.

## What I verified in the game, and how

Nothing below is assumed. `Assembly-CSharp.dll` (3,818 types) was decompiled in full
with `ilspycmd` and read directly. Line numbers are into that decompilation.

### The ally system exists and is complete

| Question | Answer | Evidence |
|---|---|---|
| How is an ally represented? | A `Monster` whose `CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance`. `Creatures` holds exactly one `Player` plus a `List<Creature> Monsters` — an ally is a monster on your side, nothing more. | `class Creatures`, `enum CreatureAlliance` |
| Can allies be told apart from enemies? | **Yes, per creature.** `CreatureAlliance` is a `[Save]` field on every `CreatureData`. | `CreatureData:66` |
| Do allies actually fight? | **Yes.** `FollowTarget : FightState` — it picks targets, shoots, closes to melee, and follows the leader when there is nothing to shoot. | `class FollowTarget` |
| Can the player command them? | **Yes.** Inspecting an ally shows a *follow / wait* toggle and a *shoot at will / hold fire* toggle, both writing `FollowTarget.Wait` and `FollowTarget.CanShoot`. | `ShootAtWillButtonOnValueChanged`, `FollowButtonOnValueChanged` |
| Can the player support them? | **Yes.** `HealSupplyWindow` applies your medkits to an ally's wounds, and `PlayerInteractionSystem.CanUseInventory` opens their backpack so you can hand over guns and ammo. | inspect-window fields `_healWindow`, `_allyBackpackWarningText` |
| Do allies follow you between floors? | **Yes.** `TakeTransferableMonsters` carries every `IsTransferable` monster within radius 10 of the player to the next floor and respawns it beside you with its AI state intact. | `TakeTransferableMonsters`, `SpawnMonsters` |
| Do allies break mission objectives? | **No.** `KillAllPercent` explicitly skips `PlayerAlliance` creatures when counting. | `WinCondition.ProcessMonsterKilled` |
| Are ally corpses auto-looted? | **No** — the auto-loot path skips `PlayerAlliance`. Their gear stays on them. | line 67178 |

### The game already spawns an ally squad — this mod copies that code path

`DungeonGenerator.SpawnAllySquads()` builds a `Unit` of 1–4 faction CEO guards and
spawns it beside the player, then per member sets exactly four things:

```csharp
item.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
item.Behaviour.SetEndlessHunt(value: true, force: true);
item.Behaviour.WaitTransferAtElevator = true;
item.IsTransferable = true;
```

It is gated behind the Proxy Corp department perk, `RaidType.ProcMission`, a matching
beneficiary faction, and `stage1` only — which is why you have probably never seen it.
Everything it calls is public and reachable from a mod:

| Piece | Accessibility |
|---|---|
| `SpawnSystem.SpawnFixedGroup(...)` | `public static`, takes every system it needs as a parameter |
| `CreatureSystem.SpawnMonsterFromMobClass(...)` | `public static`, has a `CreatureAlliance` parameter and registers turn contenders itself |
| `MGSC.Unit` | plain public class — `LeaderMobClassId`, `Members`, `FactionId`, `TechLevelLimit` |
| `FactionRecord.GuardCreatureId` / `.CEOGuardCreatureId` | public, so escort mob classes are **read from game data, never hardcoded** |
| `State.Get<T>()` | public; `IModContext` already hands us the `State` |

### The gift-to-ally mechanic is pure data

Any AI preset with a non-empty `ItemsClassesAsGifts` or `ItemsIdsAsGifts` will, on
seeing you drop such an item, walk to it, pick it up, **permanently join your
alliance and start following you** — from `Idle`, `IdleFollow`, `IdleMigrate`,
`Investigate`, `Attack`, `Defense`, `Panic`, `Surrender` or `PickWeapon`.

```
FoundGift()  ->  SeekGift  ->  OnGiftFound():
                    _owner.Behaviour.StartFollowing(_creatures.Player);
                    _owner.CreatureData.CreatureAlliance = PlayerAlliance;
                    CombatLogSystem.AddTurnedAllyEntry(...);
```

Widening recruitment therefore needs **no new code path at all** — only two list
fields populated on more AI presets. That is the cheapest, most vanilla feature in
this plan.

### Every ally stat lever is a public field on `CreatureData`

| Lever | Field | Note |
|---|---|---|
| Damage dealt | `BaseOverallDmgMult` | The game itself writes `difficulty.Preset.EnemyDamageMult` here at spawn |
| Damage resisted | `OverallResistMult` | ditto `EnemyResistance` |
| Max health | `BaseHealth` + `Health.ReinitializePreservingCurrent(v)` | Raises the ceiling without a free full heal |
| Turns per round | `Monster.ActionPoints` | Set at spawn from `BaseActionPoints`; contenders are registered per point |
| Sight | `BaseLosLevel` | |
| Hit chance | `BaseRangeAccuracy`, `BaseMeleeAccuracy` | |
| Evasion | `BaseDodge`, `BaseOverallDodgeMult` | |
| Crippling | `ReceiveWoundChanceMult`, `ReceiveAmputationChance` | Keeps allies in the fight rather than limbless |
| Nerve | `IgnorePain`, `PainThresholdLimit`, `PainThresholdRegen` | |
| Second wind | `HasSecondChance` | |

`HealthInfo.DamageMultiplier` is **not** usable — it is recomputed every turn from
`GetIncomeDmgMult()` (wounds + perks) and any write is overwritten.

### The spectator switch is a vanilla flag

`CreatureData.IgnoreByMonsters` is read in `AIVision.GetVisibleEnemies` — a creature
carrying it is never added to any monster's enemy list. Vanilla sets it on quest
captives (`JaneGreeting`). Setting it on the player is a one-line, engine-supported
spectator mode.

### Deliberately still open — the probe answers these

Config tables live inside Unity assets, not loose files, so vanilla *values* cannot
be read statically. No number in this mod may be finalised until Phase 0 has run:

- (a) The real `Data.Factions` ids and their `GuardCreatureId` / `CEOGuardCreatureId`,
      and whether those mob classes are actually competent (armed, armoured, thinking).
- (b) Which AI presets already carry gift lists, and which `ItemClass` values are
      plausible gifts, so widening recruitment does not turn every corpse-eater into a friend.
- (c) Whether the escort AI preset panics or surrenders, which decides whether
      Phase 4 needs the `AllowTransitions` pin at all.
- (d) Baseline `BaseHealth` / `ActionPoints` on the chosen escort classes, so the
      multipliers below can be sanity-checked rather than trusted.
- (e) What a floor transfer actually does to a modded ally's baked-in stats.

## Patterns to mirror

The sibling mods are the house style. This one follows them rather than inventing.

| Category | Source | Pattern |
|---|---|---|
| Entry point | `QuasimorphRuthless/RuthlessMod.cs:44` | `[Hook(ModHookType...)]` public static; `State` from `IModContext`. **Never** patch `State`, `GameLoop`, `Data` or the bootstrap path — a Workshop mod black-screened this game doing exactly that |
| Failure isolation | `RuthlessMod.cs:118` | Every hook body wrapped in `Guard(...)`; a broken mod must leave a working game |
| Config | `QuasimorphRuthless/ModConfig.cs` | Plain `key=value` `config.txt`, self-documenting on first write, clamped ranges, unreadable file → defaults + one log line, no MCM dependency |
| Logging | `QuasimorphRuthless/ModLog.cs` | Own log file beside the assembly, mirrored into `Player.log`, never throws |
| Snapshot / restore | `TacticalAi.cs:83` `CaptureVanilla()` | Capture before writing anything; restore is a copy back, never inverted arithmetic |
| Probe | `QuasimorphRuthless/DataProbe.cs` | `probe=true` writes `probe.txt` of every value the mod reasons about. Shipped, not throwaway |
| Build | `QuasimorphRuthless/build.ps1` | Compile → stage → **verify every game reference resolves** → optional `-Install` |
| Ship-safety | `tools/apicheck.py` | Walks the built DLL's TypeRef/MemberRef tables against the real assemblies. Non-negotiable |
| Referencing | `QuasimorphRuthless.csproj:36` | `<Private>false</Private>` on every game reference; never ship our own `0Harmony` |
| State | `PROJECT_STATE.md` | Phase table, decisions with dates, known risks, single Next Action |

## Architecture

Four layers. Layers 2–4 are opt-out; layer 1 is the mod.

```
AfterConfigsLoaded ─┬─ read config.txt
                    ├─ snapshot vanilla AiPresets gift lists     (restore source)
                    ├─ probe dump, if probe=true
                    └─ widen gift lists                          [layer 3]

DungeonStarted     ─┬─ raid type eligible?  (not Station / EditorTestGeneration)
(every floor)       ├─ count living PlayerAlliance monsters
                    ├─ top up to squad_size via SpawnFixedGroup  [layer 1]
                    ├─ equip each new ally: medkit + spare ammo  [layer 1]
                    ├─ buff every unbuffed ally, exactly once    [layer 2]
                    └─ apply spectator flag to the player        [layer 4]

DungeonUpdateAfterGameLoop (throttled)
                    └─ buff allies that appeared mid-raid        [layer 2]
                       (gift accepted, converted, summoned, quest)
```

**Nothing is gated on difficulty by default.** You installed this deliberately. An
`only_on_difficulty=` key exists for players who want it confined to one preset —
set it to `HardcoreTacticalRuthless` and it behaves like a Ruthless companion layer.

### Why a top-up and not a spawn

`DungeonStarted` fires on every floor *and* on every mid-floor save load. Counting
first and spawning only the difference makes all four cases correct with one rule:

| Situation | Result |
|---|---|
| New floor, squad followed you down | already at strength → nothing spawns |
| New floor, one died last floor | one replacement arrives |
| Save loaded mid-floor | squad already present → nothing spawns, no duplicates |
| First floor of a raid | full squad spawns |

### Why buffs are baked into the creature

`CreatureData` fields are `[Save]`, so a buff written at spawn persists in the save
— exactly how the game applies its own difficulty multipliers
(`creatureData.BaseOverallDmgMult = difficulty.Preset.EnemyDamageMult` in
`CreateMonsterFromMobClass`). This is simpler and cheaper than re-deriving stats
every turn, at one honest cost, which goes in the README: **allies already buffed in
a save keep those stats if you uninstall the mod.** Idempotence is enforced by a
`HashSet<int>` of `CreatureData.UniqueId` plus a per-creature marker, so the sweep
can never buff the same ally twice.

## Files to change

| File | Action | Why |
|---|---|---|
| `Quasimorph_Retinue/Quasimorph_Retinue_src/mod_src/QuasimorphRetinue/RetinueMod.cs` | CREATE | Hook entry points, `Guard`, layer dispatch |
| `.../ModConfig.cs` | CREATE | `config.txt` — squad size, stance, multipliers, spectator, recruiting, probe |
| `.../ModLog.cs` | CREATE | Log file + `ModInfo` constants |
| `.../AllyIdentity.cs` | CREATE | The single definition of "is an ally", and the sweep that finds them |
| `.../AllyPower.cs` | CREATE | Layer 2 — per-creature stat buffs, applied exactly once |
| `.../RetinueSquad.cs` | CREATE | Layer 1 — top-up spawn, escort mob class selection, starting kit, stance |
| `.../Recruiting.cs` | CREATE | Layer 3 — widen `ItemsClassesAsGifts` / `ItemsIdsAsGifts`, with snapshot + restore |
| `.../PlayerRole.cs` | CREATE | Layer 4 — `IgnoreByMonsters` spectator switch |
| `.../DataProbe.cs` | CREATE | Phase 0 — dump factions, guard mob classes, AI preset gift lists and panic values |
| `.../ConflictCheck.cs` | CREATE | Warn (never block) when Ruthless or Big Pack is also loaded |
| `.../modmanifest.json`, `.csproj` | CREATE | Mirror `QuasimorphRuthless` exactly |
| `Quasimorph_Retinue/Quasimorph_Retinue_src/build.ps1` | CREATE | Copy of the Ruthless script, renamed paths |
| `.../tools/apicheck.py`, `tools/cli_meta.py` | CREATE | Copied verbatim from `Quasimorph_Ruthless/tools/` |
| `.../README.md` | CREATE | Includes a **how to actually play support** section — the vanilla ally orders, healing and backpack UI most players never find |
| `.../PROJECT_STATE.md` | CREATE | Phase table, decisions, risks, Next Action |
| `Quasimorph_Ruthless/**` | UNCHANGED | Its Phase 6 live test stays independently valid |

## Tasks

### Phase 0 — Probe (folded into the shipped mod, as Ruthless did)
- **Action**: `DataProbe.Dump` writes `probe.txt`: every `FactionRecord` with its
  `GuardCreatureId` / `CEOGuardCreatureId` / `Enabled` / `InitialTechLevel`; every
  `MobClassRecord` those ids point at, with `Los`, `HealthMod`, `ActionPointsMod`,
  `AiPresetId`, weapon and armour tables; every `AiPresetRecord` with its gift
  lists, `CanUseItems`, `CanPanic`, `SurrenderChanceByDamage`.
- **Mirror**: `QuasimorphRuthless/DataProbe.cs` structure and section headers.
- **Validate**: `probe.txt` exists, non-empty, and names at least one faction with a
  non-empty `CEOGuardCreatureId`. Reaching the **main menu is enough** — the dump runs
  at `AfterConfigsLoaded`.

### Phase 1 — Skeleton
- **Action**: Project, manifest, `build.ps1`, `tools/`, `ModLog`, `ModConfig`,
  `ConflictCheck`, `RetinueMod` with hooks that do nothing but log.
- **Mirror**: `QuasimorphRuthless` file-for-file.
- **Validate**: `.\build.ps1` → *Build succeeded*, **zero warnings**, apicheck reports
  every reference resolved.

### Phase 2 — Ally identity and the buff layer
- **Action**: `AllyIdentity.IsAlly(Creature)` — `is Monster` **and**
  `CreatureAlliance == PlayerAlliance`. `AllyPower.Apply(monster)` writes the stat
  table below once per `UniqueId`. Sweep runs at `DungeonStarted` and on a throttled
  `DungeonUpdateAfterGameLoop`.
- **Mirror**: `TacticalAi.CaptureVanilla` for the snapshot discipline; `Guard(...)`
  for isolation.
- **Validate**: log line per buffed ally with before/after values; re-entering a floor
  and reloading a save produce **no** second buff line for the same `UniqueId`.

Opening tuning table — every value is a *multiplier on what that creature already
has*, so an ally inherits the difficulty's own scaling and this stacks above it:

| Lever | Default | Reasoning |
|---|---|---|
| `BaseHealth` | ×1.60 | They must survive a Ruthless engagement without you |
| `BaseOverallDmgMult` | ×1.50 | They have to actually kill things, not chip them |
| `OverallResistMult` | ×1.25 | Survives the wrong damage type |
| `BaseRangeAccuracy` | ×1.25 | An ally that misses is a spectator too |
| `BaseDodge` | ×1.20 | |
| `BaseLosLevel` | +1 | They spot before you do — the point of a screen |
| `ActionPoints` | +1 | The single biggest competence knob, and the riskiest for turn length |
| `ReceiveWoundChanceMult` | ×0.70 | Fewer permanently crippled allies |
| `IgnorePain` | true | They do not fold at the wrong moment |
| `HasSecondChance` | true | One survivable mistake per ally per floor |

Every one of these is a `power=` scalar in `config.txt`; `power=0` disables layer 2
entirely and leaves allies vanilla.

### Phase 3 — The retinue
- **Action**: at `DungeonStarted`, skip ineligible raid types, count living allies,
  build a `Unit` from the best available faction guard mob class, call
  `SpawnSystem.SpawnFixedGroup(...)` for the shortfall, then per member set
  `CreatureAlliance`, `IsTransferable`, `WaitTransferAtElevator`, and the configured
  stance — `StartFollowing(player)` for *escort* or `SetEndlessHunt(true, force:true)`
  for *hunter*. Give each new ally a medkit and spare ammo via `monster.AddItem`.
- **Mirror**: `DungeonGenerator.SpawnAllySquads()` — the same four calls, in the same
  order, for the same reasons.
- **Escort selection**: prefer the mission's beneficiary faction's `CEOGuardCreatureId`,
  fall back to any enabled faction's `GuardCreatureId`, fall back to the highest-scoring
  armed humanoid mob class. **No id is ever hardcoded** — the same discipline that keeps
  the Ruthless mod alive across game patches.
- **Validate**: three allies beside you on floor 1; walk to the elevator, descend,
  confirm the same three arrive with you and no fourth spawns; kill one, descend,
  confirm exactly one replacement.

### Phase 4 — Recruiting and ally nerve
- **Action**: snapshot then widen `ItemsClassesAsGifts` / `ItemsIdsAsGifts` on AI
  presets that already reason (`CanUseItems`, or a non-empty firemode/grenade chance),
  using the item classes the probe shows vanilla already uses. Restore on demand,
  exactly as `TacticalAi.Restore()` does, because these are shared config records.
  If the probe shows escorts panic or surrender, pin allies with
  `Behaviour.AllowTransitions = false` while in `FollowTarget` — the flag vanilla
  itself uses to hold a monster in a state.
- **Mirror**: `TacticalAi` snapshot/apply/restore lifecycle verbatim.
- **Validate**: drop a gift near a widened-preset enemy in sight; the combat log shows
  a *turned ally* entry and it starts following. Disable the layer, restart, confirm
  the same enemy ignores the same item.

### Phase 5 — Player role and packaging
- **Action**: `spectator=true` sets `Player.CreatureData.IgnoreByMonsters = true` at
  `DungeonStarted` and clears it when the switch is off, so toggling it never leaves a
  stale flag in a save. README with the tuning table, the uninstall caveat, and the
  **how to play support** section. `PROJECT_STATE.md`.
- **Validate**: with `spectator=true`, stand in the open in front of an armed enemy for
  three turns and take no fire; with `spectator=false` in the same spot, take fire.

### Phase 6 — Live test and tuning
- **Action**: play several floors at Hardcore Tactical Ruthless with Retinue installed.
- **Validate**: against the three tests. Specifically: does the squad clear a room
  without you firing; does a floor take longer in *minutes* than it did before (the
  sponge failure, wearing the other side's uniform); is `ActionPoints +1` making turns
  visibly slow. Retune, then record measured values in `PROJECT_STATE.md`.

## Validation

```powershell
cd C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Retinue\Quasimorph_Retinue_src
.\build.ps1                 # Build succeeded, 0 warnings, apicheck: all references resolved
.\build.ps1 -Install        # into LocalUserPresets\QuasimorphRetinue
```

Then, in order: main menu (probe) → start a raid (squad spawns) → descend a floor
(squad transfers, shortfall replaced) → reload a save (no duplicates) → drop a gift
(recruit) → toggle `spectator` (targeting changes).

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `KillMonster` objectives counting a dead ally of the target mob class | Medium | Read `WinCondition.WinConditionParameters[0]` and exclude that mob class from escort selection |
| Turn length becomes tedious with more creatures acting | **High** | `squad_size` default 3, capped; `ActionPoints +1` is the first knob to cut in Phase 6 if turns drag |
| `SpawnFixedGroup` finds no valid spawn point | Low | It logs and returns an empty list; treat as "no squad this floor", never throw. Retry next floor |
| Buffs persist in a save after uninstall | **Certain** | Documented in the README uninstall section. Baked-in is the deliberate choice; the alternative is a per-turn recompute this mod does not need |
| Allies block corridors | Medium | Vanilla already answers this — allies are pushable (`CanBeAllyPushed`) |
| Widened gift lists reach an unintended preset | Medium | Only presets that already reason are touched, and the whole layer snapshots and restores. Probe first, widen second |
| Interaction with Ruthless: allies also receive `EnemyDamageMult` etc. | Certain | Not a bug — it is why the multipliers are relative. Documented, and measured in Phase 6 |
| Big Pack + Retinue + Ruthless together | Low | `ConflictCheck` warns in the log, blocks nothing |

## Acceptance

- [ ] `.\build.ps1` succeeds with zero warnings and apicheck resolves every reference
- [ ] `probe.txt` names at least one usable escort mob class, read from game data
- [ ] A squad of `squad_size` spawns on floor 1 and transfers down intact
- [ ] Reloading a save spawns nothing and buffs nothing twice
- [ ] Every ally, however acquired, is buffed exactly once
- [ ] No enemy anywhere is stronger than it was without the mod
- [ ] `spectator=true` measurably stops enemies targeting the player
- [ ] `enabled=false` leaves the game bit-for-bit vanilla
- [ ] `Quasimorph_Ruthless` is unmodified and still builds
- [ ] README documents the uninstall caveat and teaches the vanilla ally controls
- [ ] `PROJECT_STATE.md` is accurate, with honest PASS / UNTESTED / BLOCKED states
