# Project State

**Version**: v0.3 | **Phase**: 8 of 8 — In-game verification | **Status**: BUILT, UNTESTED
**Updated**: 2026-09-02

Sibling of Retinue, Ruthless, Big Pack, Big Stack and Thai. Adds a Move-to button and an
Escort/Roam control to the ally panel, and lets the player command an ally that is out of
sight — including sending one to any cell on the floor, seen or unseen.

## Completed

- [x] **Phase 0 — Scaffold.** Branch `feat/ally-signals`, directory tree mirroring the
      siblings, csproj (Assembly-CSharp, 0Harmony, UnityEngine, UnityEngine.UI,
      Unity.TextMeshPro — all `Private=false`), manifest, `build.ps1`, `tools/apicheck.py`.
      Builds clean, 0 warnings.
- [x] **Phase 1 — Spike, resolved offline.** All three unknowns answered by reading the
      game's CLI metadata with `tools/cli_meta.py`, without needing a running game.
      See Decisions.
- [x] **Phase 2 — Orders model.** `AllyOrders.cs`. Public API only, no patches. Keyed by
      `CreatureData.UniqueId`. Re-asserted per turn, and only where the ally has actually
      drifted (`IsEndlessHunt` disagreement), so a following ally is never re-ordered
      into a stutter.
- [x] **Phase 3 — The control.** `CommandUiPatch.cs`. One postfix on
      `MonsterInspectWindow.RefreshFollowButton`; the click needs no patch because
      `OnValueChanged` is a public event.
- [x] **Phase 4 — Out of sight.** `RemoteOrdersPatch.cs`. Postfixes on
      `Monster.get_ShowSignal` and `MonsterInspectWindow.IsFollowerAlly`, both ally-gated
      and both additive (false → true only).
- [x] **Phase 5 — Patch verification.** `PatchVerify.cs` + `Targets.cs`.
- [x] **Phase 6 — Docs.** README, config template, `ConflictCheck`.
- [x] **Phase 8 — Fire discipline (v0.3).** `FireDiscipline.cs`. One prefix on
      `FightState.TryRangeAttack(CellPosition)`, ally-gated, holding fire beyond the
      weapon's effective range so the ally closes instead. Plan:
      `.claude/plans/fire-discipline.plan.md`. Builds clean, 0 warnings, 49 references
      resolve.
- [x] **Phase 7 — Move orders (v0.2).** `MoveOrders.cs`, `MoveTargeting.cs`,
      `MoveButton.cs`. A **Move to...** button on the ally panel arms a one-shot
      destination picker; the next right-click on the map sends that ally there and it
      holds position on arrival. Builds clean, 0 warnings, 37 references resolve.

## Active

- [ ] **In-game verification.** Everything below is UNTESTED in a running game. The mod
      builds with 0 warnings, all 49 game references resolve, and it is installed to
      LocalUserPresets — but no part of the UI, the move order, the fire discipline or the
      line-of-sight behaviour has been observed working.

## Pending

- [ ] Release folder is still named `Quasimorph_Signals_v0.1` while the mod reports
      v0.2. Cosmetic; rename with the `-OutDir` parameter at release time.
- [ ] No on-screen targeting cursor while a destination is awaited — the notification
      line is the only feedback. If that reads as unclear in play, the next step is a
      cursor tint via `MapRenderer`, not more text.

## Known Failures

- Nothing observed. Nothing yet observable — see Active.

## Decisions

- 2026-09-02 — **Separate mod, not a Retinue feature.** Retinue documents that it applies
  no Harmony patches at all; the control and the out-of-sight layer cannot be built
  without them. Splitting keeps that guarantee true and follows the Big Pack / Big Stack
  precedent. Retinue needs no code change.
- 2026-09-02 — **Signals never references Retinue.** It reads live `AiBehaviour` state off
  the creature, so there is no assembly reference, no load-order requirement and no shared
  type. Either mod can be uninstalled alone.
- 2026-09-02 — **A second toggle, not a three-way cycle.**
  `ToggleAllyStateButton._currentSide` is a `Side`, a nested enum of exactly `Left` and
  `Right`. It cannot represent three states. This is the likely root cause of the
  *Ally Roam/Patrol* Roam option failing to appear, and the reason this mod clones the
  button rather than relabelling it.
- 2026-09-02 — **Orders keyed by `CreatureData.UniqueId`** (public `int32`), which is
  stable across saves, loads and floors; the creature object is not.
- 2026-09-02 — **`EvaluateSecondaryCursorAction` deliberately not patched.**
  ~~It is a private static taking eight systems and returning a bool. Without reading
  its body there is no honest way to write a safe prefix.~~
  **REVERSED the same day, in v0.2.** The constraint behind this was tooling, not
  design: that session could only read CLI metadata (`tools/cli_meta.py`), which gives
  signatures but no method bodies. A full ILSpy decompilation of `Assembly-CSharp.dll`
  removes the guesswork — the method is 35 lines, and its contract is plain: return true
  and the caller treats the click as handled. The prefix now consumes exactly one
  right-click, only while a destination is being awaited, and returns control to vanilla
  in every other case including on exception. The original reasoning was right; only its
  premise expired.
- 2026-09-02 — **Captions written directly to the TMP components** rather than through
  `ToggleAllyStateButton.Initialize`, which takes localization tags. `Localization.Get` is
  the single most contested method across the installed mod set (7 mods); this avoids
  adding an eighth.
- 2026-09-02 — **`AllyTest.IsAlly` duplicated from Retinue's `AllyIdentity`** (~15 lines).
  Sharing it would mean a hard inter-mod dependency, which is the thing the split exists
  to avoid. Documented in the file.
- 2026-09-02 — **Move orders ride the vanilla `Investigate` state**, not a mod-defined
  `AIState`. States live in a `[Save]` list on the behaviour, so a mod-defined one is a
  save-compatibility hazard the moment the mod is removed. `Investigate` is already in
  every behaviour, exposes `SetInvestigateCell` publicly, and paths with the AI's own
  pathfinder — which never consulted player line of sight, so "order out of sight" needed
  no work at all.
- 2026-09-02 — **The investigation timer does not threaten a long walk.**
  `InvestigationAITimer.ProcessAfterState` only decrements while
  `IsAtInterestPosition || CantMove`. It runs after arrival or while stuck, never while
  walking. Verified by reading the timer, not assumed.
- 2026-09-02 — **Endless hunt is cleared when a move order is issued.** `Investigate`
  carries a transition to `Attack` named "Endless Hunt" that fires immediately for a
  hunting creature, so a roaming ally would abandon the order on the same turn.
- 2026-09-02 — **A `CommonButton` clone, not a third toggle.** "Go where I point" is an
  action, not a stance, and `ToggleAllyStateButton` is two-state by construction. The
  clone's inherited click handlers are cleared through the event's backing field; if that
  field cannot be resolved the button is destroyed rather than shipped with unknown
  behaviour, because the handler it would inherit closes the window.
- 2026-09-02 — **The Move button does not yield to *Ally Roam/Patrol*.** The yield exists
  because both mods relabel the *vanilla* follow button. The Move button is a new control
  with its own name that nothing else writes to, so yielding it would hide the feature
  from anyone running that mod — which is the majority case on this machine.
- 2026-09-02 — **Fire discipline gates on effective range, computed properly.**
  `WeaponComponent.Range + CreatureData.GetFirearmRangeBonus(record)`, not the raw
  `weaponRecord.Range` that vanilla's own `Attack` check and the *Squad: More operatives*
  patch both use. The component value already folds in the loaded ammunition's range
  bonus, `EffectiveRangeStarts`, and an `IAddedEffectiveRange` item trait; the creature
  bonus adds perks and augments. Reading the record alone would send an ally with
  long-range rounds walking closer for nothing. All members public.
- 2026-09-02 — **Effective range is the boundary because the game says so.**
  `DamageSystem.FalloffDamage` begins reducing damage the moment `distance > range`. The
  threshold is not invented; it is where the game starts punishing the shot.
- 2026-09-02 — **Declining a shot needs an escape hatch, and it is not optional.** Every
  caller turns a declined shot into movement, so "hold fire" only means "close in" while
  the ally can actually move. An ally on Wait, immobile, or in a blocked corridor takes
  the weak shot instead — a frozen bodyguard is worse than a bad shot. Plus a give-up
  counter after six declines without the range shrinking, for targets that are visible
  but unreachable, where vanilla's own gate would loop forever too.
- 2026-09-02 — **Fire discipline applies to allies only.** Teaching enemies not to waste
  ammunition is a difficulty change wearing a bug fix's clothes, and both sibling mods
  hold the line that a patch must never make an enemy stronger unasked. One config key
  later if wanted.
- 2026-09-02 — **`FirearmSystem.ApproximateHits` considered and held.** It is public and
  models scatter, accuracy and pellet count properly — the better metric in principle, and
  the natural next step if shotguns still feel wrong *inside* effective range. It costs
  three trajectory calculations per AI decision per creature and its threshold cannot be
  tuned without play data. Effective range costs one subtraction.
- 2026-09-02 — **`MoveOrders.Enforce` re-issues from a whitelist of states**
  (`Idle`, `IdleFollow`, `IdleMigrate`, `FollowTarget`, `Stay`), never a blacklist. An
  ally that is fighting, panicking, surrendering or fetching a weapon is left alone and
  the order resumes afterwards. An ally shot at en route should shoot back.

## Diagnosis on record — why allies shot from too far

Read off the decompiled `Assembly-CSharp`, not inferred. The distance gate exists in
exactly one of the five states an ally can fight from:

| State | Range check? |
|---|---|
| `Attack.ProcessTacticMode` | **Yes** — `weaponRecord.Range >= distance`, else *"Target out of eff. range. Approaching"* |
| `Attack.ProcessDesperateMode` | No |
| `Defense.TryAttack` | No |
| `Rage` | No |
| `FollowTarget.TryAttack` | **No** — and this is where every escorting ally lives |

Neither function underneath filters by distance. `FightState.TryRangeAttack` checks only
that the weapon is unbroken and can fire; `AiBehaviour.TryShoot` adds
`ShootTargetReachable`, which sounds like a range test and is a line-of-fire raycast.

The Workshop mod *Squad: More operatives* ships the identical prefix on the identical
method — independent confirmation of both diagnosis and remedy — but gates on its own
`IsSquadAlly`, so it covers only the operatives it deploys.

## Next Action

Launch the game with the mod installed and check, in order:

1. `QuasimorphSignals.log` says *"all 5 patches attached and all private members
   resolved"*. If not, stop — the rest cannot work.
2. An ally panel shows a **Move to...** button. (The Escort/Roam toggle will be absent
   while *Ally Roam/Patrol* is installed and `yield_to_ally_roam_patrol=true` — that is
   correct, and the Move button should still be there.)
3. Press **Move to...**; the panel closes and a notification line appears. Right-click a
   visible cell across the room. The ally walks there and stops.
4. Repeat, pointing into a room that has never been entered. Same result — this is the
   whole feature.
5. Right-click a wall. The order is refused with a message, and the cursor is not stuck
   in targeting mode.
6. Re-open the panel; the button now reads **Cancel move**. Press it, confirm the ally is
   released where it stands.
7. Order an ally across a room containing an enemy. It should fight, then resume walking.
8. Give a move order, then press **Escort**. The move order must be dropped, not fought
   over — watch for an ally oscillating between the two, which would mean
   `MoveOrders.Has` is not being consulted in `AllyOrders.Sweep`.
9. Save and reload mid-order, and take an elevator mid-order. Orders are dropped on a
   floor change by design; confirm nothing throws.
10. **Enemies gained no visibility, and no enemy can be given an order** — the check that
    matters most.

### Fire discipline (v0.3)

11. Give an ally a shotgun. Long corridor, enemy at the far end. It **walks instead of
    firing**, and the log names the distance and the effective range it computed.
12. It fires once inside that range.
13. Same test with a rifle: it fires from much further, because its effective range is
    larger. This is what proves the rule is per weapon and not a flat number.
14. Swap to long-range ammunition; the ally should open fire sooner. This is the part
    both vanilla and the Squad mod get wrong by reading the raw record value.
15. Set the ally to Wait, enemy at long range: it **fires anyway** rather than standing
    idle. Same for an immobile ally.
16. Put a visible but unreachable target (across a chasm, behind glass) at long range:
    the ally fires after roughly six declines rather than circling forever.
17. **An enemy with a shotgun still opens fire from across the room** — enemies untouched.
18. `fire_discipline=false`, restart, confirm the old behaviour returns.
