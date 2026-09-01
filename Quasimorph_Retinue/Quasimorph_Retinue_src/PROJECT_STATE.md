# Project State — Quasimorph Retinue

**Mod version**: 0.1.0 — an ally squad, and allies worth having
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Phase**: 6 of 6 — Live test | **Status**: BUILT AND REFERENCE-VERIFIED, NOT YET RUN IN GAME
**Updated**: 2026-09-01
**Branch**: `feat/retinue-ally-squad`

Plan: `.claude/plans/retinue.plan.md`

## Phases

| Phase | What | Status |
|---|---|---|
| 0 | Probe | **FOLDED IN** — shipped as `probe=true` rather than a throwaway build. Not yet run. |
| 1 | Skeleton, config, logging, build | **COMPLETE** — builds clean, 0 warnings, apicheck 93/93 |
| 2 | Ally identity + strength layer | **IMPLEMENTED, UNTESTED** |
| 3 | The retinue spawn | **IMPLEMENTED, UNTESTED** |
| 4 | Recruiting | **IMPLEMENTED, UNTESTED** |
| 5 | Spectator switch, README | **COMPLETE** — implemented and documented |
| 6 | Live test and tuning | **BLOCKED** — needs a game launch |

## What "UNTESTED" means here

The mod compiles with zero warnings and **all 93 of its game member references resolve
against the shipped assemblies**. Every API assumption was read out of a full ILSpy
decompilation of `Assembly-CSharp.dll` rather than guessed — the evidence table is in
the plan.

**None of it has been run.** Unproven: that `SpawnFixedGroup` finds room beside the
player at `DungeonStarted`, that a spawned ally actually follows and shoots, that the
transfer between floors carries them, that widened gift lists really do produce a
defection, and whether any of the tuning is any fun.

## Architecture

Four layers. The mod applies **no Harmony patches at all** — every layer is a plain
write through a public API from inside a mod hook. The rule inherited from the sibling
mods holds: nothing patches `State`, `GameLoop`, `Data` or the bootstrap, after a
Workshop mod black-screened the game doing exactly that.

| Piece | How |
|---|---|
| Ally identity | `CreatureAlliance.PlayerAlliance` on a `Monster`, i.e. per creature **instance**, never per config record. This is the guarantee that no enemy can be strengthened: there is no code path from an ally instance to a hostile one. |
| The squad | `SpawnSystem.SpawnFixedGroup`, called the way vanilla's own `DungeonGenerator.SpawnAllySquads` calls it, then the same four writes: alliance, stance, `WaitTransferAtElevator`, `IsTransferable`. |
| Top-up, not spawn | Living allies are counted first and only the shortfall is spawned. One rule covers four cases: floor transfer, save reload, casualties, and allies you recruited yourself. |
| Ally strength | Every stat is **computed and assigned**, never multiplied in place, from a base the mod never writes to. Idempotent by construction — reloads and repeated hook calls cannot compound. |
| Escort choice | `FactionRecord.CEOGuardCreatureId` / `GuardCreatureId`, read from game data at runtime. **No mob class id is hardcoded**, so a game patch cannot leave the mod pointing at something that no longer exists. |
| Recruiting | The one layer that writes to shared `AiPresetRecord`s. Snapshot before writing, restore on demand — the same lifecycle `TacticalAi` uses in the sibling mod. |
| Layer gate | `only_on_difficulty` is empty by default (this mod is opt-in by installation). When set, it **fails closed**: if the difficulty cannot be read, every layer stays off. |

## Decisions

- 2026-09-01 — Ships as a **separate sibling mod**, not a fourth Ruthless layer. Ruthless
  is a statement that nothing helps the player; this mod openly contradicts it. Keeping
  them apart preserves both, and leaves Ruthless independently testable. (Owner.)
- 2026-09-01 — Player protection is **configurable, defaulting to support**. `spectator`
  ships off; the player is still targetable unless they ask otherwise. (Owner.)
- 2026-09-01 — Ally supply is **escort squad plus widened recruiting**, not one or the
  other. The squad guarantees you are never empty-handed; recruiting is the part you
  earn. (Owner.)
- 2026-09-01 — Buffs are **assigned from a recomputed base**, not multiplied in place.
  The obvious implementation compounds across a save/reload because `CreatureData` is
  `[Save]` and an in-memory "already done" set is not. This is the single most important
  correctness decision in the mod.
- 2026-09-01 — Accuracy held at vanilla. It derives from the body type record, which
  cannot be recomputed from saved data alone, so it could not be made idempotent.
  Damage and action points cover the same ground without the risk.
- 2026-09-01 — `HasSecondChance` held at vanilla. `PerkSystem.RefreshPerkPassives` owns
  it, and an ally reviving mid-fight reads as a bug rather than as strength.
- 2026-09-01 — Escort mob classes read from faction guard ids rather than hardcoded.
  Same discipline that keeps the sibling mods alive across game patches.
- 2026-09-01 — Recruiting leaves already-bribable presets alone. What those specific
  creatures want is part of their design.
- 2026-09-01 — Bribes are consumables and valuables only, never weapons or ammunition.
  An enemy that defects for a dropped rifle turns every firefight into an auction.
- 2026-09-01 — No Harmony patches. Nothing here needs to intercept a call; every lever
  is a public field or method reachable from a hook.

## Known risks

- **Ally strength persists in a save after uninstall.** `CreatureData` is `[Save]`, and
  baking the values in is the deliberate choice — it is how the game applies its own
  difficulty multipliers. Documented in the README.
- **`spectator` persists the same way.** Recovery is to set it false and play one more
  floor before uninstalling. Documented.
- **Turn length.** Every ally acts on its own initiative. `squad_size` 3 and the `+1`
  turn bonus are the two knobs to cut first if floors start to drag.
- **`SpawnFixedGroup` may find no room** and return empty. Treated as "no squad this
  floor", retried next floor. Never throws.
- **A `KillMonster` objective** counts by mob class regardless of who did the killing.
  The escort chooser already excludes the objective's mob class; whether any faction
  guard id ever collides with one is unmeasured.
- Tuning may land on tedious rather than safe. Phase 6 is a real tuning pass.

## Probe checklist

Set `probe=true` in `config.txt` next to the installed DLL. **Reaching the main menu is
enough** — the dump runs at `AfterConfigsLoaded`. It writes `probe.txt` and
`QuasimorphRetinue.log` beside the DLL.

### Blocking — a failure here means a layer is inert

- At least one faction reports a `CEOGuardCreatureId` or `GuardCreatureId` that is not
  `(none)` and not `[MISSING from Data.MobClasses]`. All empty means the squad can
  never spawn and `RetinueSquad` needs a different source.
- At least one `Medpack` item is listed. None means allies spawn without a medkit.
- Some AI presets are tagged `[thinker]`. Zero means recruiting is inert; every record
  tagged means the heuristic is too loose.

### Tuning — a failure here means numbers need changing

- Read `EnemyHealth`, `EnemyDamageMult`, `EnemyResistance`, `EnemyLos` across
  `Easy` → `Normal` → `Hard`, and on `HardcoreTacticalRuthless` if the sibling mod is
  installed. Those are the baselines every ally stat multiplies, so an ally's real
  strength is this table times the one in the README.
- Check the guard mob classes' `hp+` / `ap+` / `los`. A guard with a large negative
  `HealthMod` is a poor escort however hard it is buffed.
- Count `[already bribable]` presets. Those are left untouched by design; if the count
  is high, recruiting is doing less than it appears to.

### Live test, in order

1. Launch to the main menu; work through the probe checklist above.
2. Start a raid. Confirm three allies spawn beside you and that the log names the mob
   class and faction they were built from.
3. Click one. Confirm the follow/wait and shoot/hold-fire buttons are present and that
   it is carrying a medkit.
4. Fight a room without firing. Confirm the squad wins it, and time how long the turns
   take.
5. Walk to the elevator and descend. Confirm the same allies arrive and that **no
   fourth spawns** (the log should say "retinue at strength").
6. Kill one deliberately, descend, confirm exactly one replacement.
7. Save mid-floor, reload. Confirm nothing spawns and that no ally's health, damage or
   action points changed — this is the test for the compounding bug the assign-not-
   multiply design exists to prevent.
8. Drop food near a thinking enemy in its line of sight. Confirm a *turned ally* entry
   in the combat log.
9. Set `spectator=true`, restart, stand in the open in front of an armed enemy for
   three turns. Confirm no incoming fire. Set it back to `false` and confirm fire
   resumes.
10. Set `enabled=false`, restart, confirm the game is bit-for-bit vanilla.

## Next action

Install and launch:

```powershell
cd C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Retinue\Quasimorph_Retinue_src
.\build.ps1 -Install
```

Then set `probe=true` in the installed `config.txt` and work the checklist above,
recording measured values here.
