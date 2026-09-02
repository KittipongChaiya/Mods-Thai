# Plan: Fire discipline — stop allies wasting shots at long range

**Source**: free-form bug report, 2026-09-02
**Target game**: Quasimorph `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Host mod**: `QuasimorphSignals` v0.3 — a new layer, not a new mod
**Complexity**: Small — one Harmony prefix, one private field, one config key

## The bug, and why it happens

An escorting ally with a shotgun opens fire the instant it sees anything, at any
distance, down a corridor, for almost no damage. That is not a tuning problem. It is a
missing check, and the game already has the check somewhere else.

I read every path from an AI deciding to attack to the shot being fired. The distance
gate exists in exactly one of five:

| State | Where an ally ends up | Distance gate? |
|---|---|---|
| `Attack.ProcessTacticMode` | a hunting ally that picked a target | **Yes** — `weaponRecord.Range >= distance`, else *"Target out of eff. range. Approaching"* |
| `Attack.ProcessDesperateMode` | low health / cornered | **No** |
| `Defense.TryAttack` | holding a position | **No** |
| `Rage` | enraged | **No** |
| `FollowTarget.TryAttack` | **every escorting ally, all the time** | **No** |

`FollowTarget` is where an ally lives whenever it is escorting you — which is Retinue's
default stance and the state the vanilla follow/wait and shoot/hold-fire buttons drive.
So the one state your squad is always in is the one with no range check.

Neither of the two functions underneath it filters by distance either:

```
FightState.TryRangeAttack(CellPosition)   weapon not broken?  CanAttack?     -> shoot
AiBehaviour.TryShoot(...)                 not melee? not broken? line of fire? -> shoot
```

`ShootTargetReachable` sounds like a range check and is not — it is a raycast for
line of fire. Nothing anywhere on the `FollowTarget` path asks how far away the target is.

## Someone else already found this, and fixed half of it

The **Squad: More operatives** Workshop mod you have installed ships this patch:

```csharp
[HarmonyPatch(typeof(FightState), "TryRangeAttack", new[] { typeof(CellPosition) })]
Prefix:
  if (!(__instance is FollowTarget)) return true;
  if (!IsSquadAlly(monster))         return true;     // <- its own operatives only
  if (weaponRecord.Range >= distance) return true;
  __result = false; return false;                     // out of range: do not shoot
```

That is independent confirmation of the diagnosis, and the same remedy this plan
proposes. It does not help you because of one line: `IsSquadAlly`. It covers only the
operatives that mod deploys. **Retinue's spawned guards, allies you bribed with a gift,
summons and quest allies are all outside it** — which is exactly the squad you are
watching miss.

So this plan is not inventing an approach. It is taking a proven one and fixing its
coverage, in the mod that already knows what an ally is.

## Two things that fix are missing

Both vanilla's own check and the Squad mod's patch read `weaponRecord.Range` — the raw
config value. That is not the range the projectile code actually uses:

```csharp
// Creature.Shoot, the real firing path:
int weaponRange = weaponComponent.Range + CreatureData.GetFirearmRangeBonus(weaponRecord);

// WeaponComponent.Range is itself:
record.Range + OverridenAmmo.RangeBonus + EffectiveRangeStarts + trait "IAddedEffectiveRange"
```

Reading the record alone silently ignores **ammunition type, item traits, and the
creature's own range perks and augments**. An ally carrying long-range ammo would be
told to walk closer for no reason. Every member above is public, so there is no cost to
being correct:

| Member | Accessibility |
|---|---|
| `WeaponComponent.Range` | `public int` |
| `CreatureData.GetFirearmRangeBonus(WeaponRecord, WeaponComponent = null)` | `public int` |
| `CellPosition.Distance(a, b)` | `public static int` |

## Why "effective range" is the honest boundary

It is not an invented threshold. It is the distance at which the game itself starts
taking damage away:

```csharp
DamageSystem.FalloffDamage(damage, distance, rangeBegins, range, falloff, out wasOutOfRange)
    distance > range  ->  damage reduced, and wasOutOfRange set
```

Below it, full damage. Above it, the game is actively punishing the shot. Holding fire
there and closing in is doing what the designers already documented in the one AI state
they gated — this layer simply applies it everywhere an ally can be.

## Patterns to mirror

| Category | Source | Pattern |
|---|---|---|
| Ally gate | `QuasimorphSignals/AllyTest.cs:31` | Every patch asks `IsAlly` first and returns the vanilla answer for everything else, on the creature in hand, on every call |
| Private members | `QuasimorphSignals/Targets.cs` | Resolved once, in one place, by name; `apicheck.py` cannot see a string, so `PatchVerify` checks it at runtime and says so loudly |
| Patch registration | `QuasimorphSignals/PatchVerify.cs:29` | Every expected patch listed as `{ type, method }` and confirmed attached at startup |
| Config | `QuasimorphSignals/ModConfig.cs` | Plain `key=value`, self-documenting template, unreadable file → defaults + one log line |
| Failure isolation | `QuasimorphSignals/MoveTargeting.cs:150` | On exception, hand the decision back to vanilla unchanged and record why |
| Prefix shape | `QuasimorphSignals/RemoteOrdersPatch.cs:44` | Ally-gated, minimal, returns `true` (run original) in every case that is not ours |

## Files to change

| File | Action | Why |
|---|---|---|
| `mod_src/QuasimorphSignals/FireDiscipline.cs` | CREATE | The prefix, the range calculation, and the cannot-close escape hatch |
| `mod_src/QuasimorphSignals/Targets.cs` | UPDATE | Resolve `HasTargetState._owner` (private `Creature`) |
| `mod_src/QuasimorphSignals/PatchVerify.cs` | UPDATE | Expect `FightState.TryRangeAttack`; describe the new field in `probe.txt` |
| `mod_src/QuasimorphSignals/ModConfig.cs` | UPDATE | `fire_discipline=true` |
| `mod_src/QuasimorphSignals/ConflictCheck.cs` | UPDATE | Record what the Squad mod's overlapping patch means |
| `README.md`, `PROJECT_STATE.md` | UPDATE | Document the layer, the diagnosis, and the test |

## Tasks

### Task 1 — Reach the creature from the state
- **Action**: add `HasTargetState._owner` to `Targets`. It is a **private `Creature`**,
  declared on `HasTargetState` rather than on `FightState`. Add a `FireDisciplineUsable`
  flag alongside the existing `MoveButtonUsable`, so a missing field disables this layer
  only and leaves the rest of the mod working.
- **Mirror**: `Targets.CloseButton` / `Targets.MoveButtonUsable`, added for the move layer.
- **Validate**: `probe.txt` reports `HasTargetState._owner : found`.

### Task 2 — The prefix
- **Action**: prefix `FightState.TryRangeAttack(CellPosition)`. In order:
  1. layer off, or `Targets` unusable → run the original
  2. owner is not an ally (`AllyTest.IsAlly`) → run the original. **Enemies keep vanilla
     behaviour exactly**, and this is checked on the creature in hand every call
  3. no weapon, or melee → run the original
  4. `distance <= weaponComponent.Range + GetFirearmRangeBonus(record)` → run the original
  5. the ally cannot or will not close (below) → run the original
  6. otherwise `__result = false`, skip the original
- **Mirror**: the Squad mod's proven prefix shape, widened from `IsSquadAlly` to
  `AllyTest.IsAlly` and from `FollowTarget` to every fight state.
- **Validate**: log line the first time each ally holds fire, naming weapon, distance
  and computed effective range.

### Task 3 — Make "hold fire" turn into "close in", not "stand there"
- **Action**: returning `false` already produces the right behaviour in every caller,
  because each one falls through to movement:

  | Caller | Fallback when the shot is declined |
  |---|---|
  | `FollowTarget.TryAttack` | `TryMoveToTarget` — walks at the enemy |
  | `Attack` (both modes) | `MoveToTarget` — *"Couldn't fire successfully"* |
  | `Rage` | `MoveToTarget` |
  | `Defense.ProcessTacticMode` | `TryWalk` |

  The escape hatch matters as much as the rule: an ally that **cannot** close must not
  stand and do nothing. Decline the shot only when movement is actually available —
  `_owner.CanMove()` is true, the creature is not `Immobile`, and, in `FollowTarget`,
  `Wait` is not set. A held-position ally, a rooted one, or one in a blocked corridor
  takes the weak shot instead of being useless.
- **Validate**: order an ally to Wait, put an enemy at long range, confirm it still fires.

### Task 4 — Give up rather than grind
- **Action**: count consecutive turns an ally has declined a shot without the distance
  shrinking. After a small number, let the shot through and log once. This is the
  pathological case — an enemy on the far side of a chasm, or behind glass — where
  vanilla's own `Attack` gate would loop forever too.
- **Mirror**: `MoveOrders.GiveUpAfterStuckTurns`, same idea, same reason.
- **Validate**: place an ally where the target is visible but unreachable; confirm it
  fires after the timeout rather than freezing.

### Task 5 — Config, docs, conflict note
- **Action**: `fire_discipline=true`. Record in `ConflictCheck` that the Squad mod
  patches the same method for its own operatives: both prefixes compute the same
  condition from the same data, so they agree, and whichever runs first wins with an
  identical answer.
- **Validate**: `enabled=false` and `fire_discipline=false` both leave the game vanilla.

## Validation

```powershell
cd C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Signals\Quasimorph_Signals_src
.\build.ps1            # Build succeeded, 0 warnings, apicheck: all references resolve
.\build.ps1 -Install
```

In game, in order:

1. Log says *"all 5 patches attached and all private members resolved"*.
2. Give an ally a shotgun. Long corridor, enemy at the far end. **It walks instead of
   firing**, and the log names the distance and the effective range it computed.
3. It fires once inside that range.
4. Same test with a rifle: it fires from much further, because its effective range is
   larger. This is the check that proves the rule is per weapon and not a flat number.
5. Swap to long-range ammunition and confirm the ally opens fire sooner — the thing both
   vanilla and the Squad mod get wrong.
6. Set the ally to Wait. It fires at long range rather than standing idle.
7. **An enemy with a shotgun still opens fire from across the room.** Enemies are
   untouched; this is the check that matters most.
8. `fire_discipline=false`, restart, confirm the old behaviour returns.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| Ally walks into a killzone to close distance | **Medium** | This is the trade being asked for. Vanilla `Attack` already does exactly this. Mitigated by the Wait escape hatch and by the give-up counter |
| Ally never fires because it can never close | Low | Task 4's counter; plus the cannot-move escape hatch |
| Double patch with the Squad mod | Certain, harmless | Both compute the same condition from the same fields. Recorded in `ConflictCheck` rather than fought |
| A melee-only or weaponless ally hits an unexpected path | Low | Step 3 of the prefix returns the original for null and melee weapons before any distance maths |
| `HasTargetState._owner` renamed by a game update | Low | `PatchVerify` reports it at startup; the layer disables itself and the rest of the mod keeps working |
| Ally closes on a target it should not (a turret, an obstacle) | Low | `TryRangeAttack` is also called for obstacle targets; the gate applies equally and declining only ever means "walk closer" |

## Considered and deliberately not done

- **`EffectiveRangeStarts` / minimum range.** `FalloffDamage` also penalises firing
  *closer* than `rangeBegins` for weapons that have one. Backing away from a target is
  strange-looking behaviour for an escort, and the field is already folded into
  `WeaponComponent.Range`, so a minimum-range rule would need its own careful design.
  Not part of the reported problem.
- **`FirearmSystem.ApproximateHits` as the gate.** It is public and models scatter,
  accuracy and pellet count properly — the better metric in principle. It costs three
  trajectory calculations per AI decision per creature, and its threshold cannot be
  tuned without play data. Effective range is the game's own boundary and costs one
  subtraction. If shotguns still feel wrong *inside* effective range after this ships,
  this is the next step and the plan already knows where it lives.
- **Applying the rule to enemies.** It would make them stop wasting ammunition, which is
  a difficulty change wearing a bug-fix costume. Both sibling mods hold the line that a
  patch must never make an enemy stronger without being asked. If wanted later it is one
  config key, not a redesign.

## Acceptance

- [ ] Builds clean, 0 warnings, apicheck resolves every reference
- [ ] `PatchVerify` reports all 5 patches attached and `HasTargetState._owner` resolved
- [ ] A shotgun ally closes before firing; a rifle ally does not
- [ ] Effective range reflects ammunition, traits and creature range bonuses
- [ ] A Wait / immobile / blocked ally still fires rather than standing idle
- [ ] No enemy anywhere changed behaviour
- [ ] `fire_discipline=false` and `enabled=false` both restore vanilla exactly
- [ ] README and PROJECT_STATE record the diagnosis, not just the fix
