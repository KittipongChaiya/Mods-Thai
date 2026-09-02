# Project State

**Version**: v0.1 | **Phase**: 6 of 6 — In-game verification | **Status**: BUILT, UNTESTED
**Updated**: 2026-09-02

Sibling of Signals, Retinue, Ruthless, Big Pack, Big Stack and Thai. Lifts the Run
stance's ban on interaction so a running character can open doors and pick things up,
one category at a time, without making any of it free.

## Completed

- [x] **Phase 0 — Scaffold.** Branch `feat/run-actions`, directory tree mirroring the
      siblings, csproj (Assembly-CSharp, 0Harmony, UnityEngine, UnityEngine.CoreModule,
      Unity.TextMeshPro — all `Private=false`), manifest, `build.ps1`,
      `tools/apicheck.py`, `ModLog`, `ModConfig`.
- [x] **Phase 1 — Doors and obstacles.** `RunActions.cs`,
      `CanInteractObstaclesPatch`. One postfix, additive only, discriminating on the
      obstacle so doors / containers / elevators are separate config keys. Re-runs the
      `MutatedQuasimorph` and scenario checks vanilla short-circuited past.
- [x] **Phase 2 — Ally inventory.** `CanOpenAllyInventoryPatch`. Same shape; re-runs the
      `ChangedMercenary` check. Adjacency and follower refusals preserved via the
      `limitType` discriminator.
- [x] **Phase 3 — Picking things up.** `PickupScope.cs`. Depth-counted scope opened by
      prefix/finalizer pairs on `TakeItemOrLootCorpse`, `ProcessCmd` (only when the
      command is `TakeItem`) and `InteractVestSlot`; `CanUseInventoryPatch` grants only
      inside it, or unconditionally under `run_full_inventory`.
- [x] **Phase 4 — Tooltip.** `RunTooltip.cs`. Postfix on
      `TooltipFactory.BuildMovementStateTooltip` appending a green line naming what the
      config actually permits, generated from the same booleans the patches read.
- [x] **Phase 5 — Verification and docs.** `PatchVerify.cs` (7 expected patches, probe
      file), `ConflictCheck.cs` (8 known neighbours, informational only), README, plan
      artifact at `.claude/plans/run-actions.plan.md`.
- [x] **Build.** 0 warnings, 0 errors. `tools/apicheck.py`: **20 member references
      resolved, all present in `Assembly-CSharp` 1.0.3.**

### Offline evidence, before anything is claimed about play

Everything here was checked without a running game, and is stated only because it was
actually run:

- **All 7 patch attributes are present in the built assembly**, confirmed by decompiling
  `build/mod/QuasimorphStride.dll` and reading them back.
- **No patch target is overloaded** in `Assembly-CSharp` 1.0.3, so each
  `[HarmonyPatch(typeof(X), nameof(X.Y))]` resolves to exactly one method. Counted per
  declaring type: all seven are 1.
- **Every injected parameter name and type matches the target's real signature**, checked
  against the decompiled game source method by method — `creatures`, `scenarios`,
  `contextObstacle`, `limitType`, `cmd`, `movementState`, plus `__result`, `__state` and
  `__instance`.
- **`HarmonyLib.HarmonyFinalizer` exists in the game's shipped `0Harmony.dll`**, so the
  prefix/finalizer pairing in `PickupScope` is supported by the Harmony the game loads.
  Verified against the assembly, not assumed from the version number.
- **All 10 config keys agree across four places** — the self-documenting template, the
  keys `Load()` reads, the field defaults, and the README table. No drift in either
  direction, no undocumented key, no documented key the code does not read. This one is
  now a build gate rather than a one-off: `tools/cfgcheck.py`, step 4 of `build.ps1`,
  which fails the build on drift. It is the same idea as `apicheck.py` one level up —
  that stops the mod shipping a call to a method the game no longer has, this stops it
  shipping a setting the mod no longer reads.

What none of this establishes is that the mod *works*, which is the Active item below.

### Code review, and what came of it

A C# review was run against the three postfixes before the first commit. Four findings,
two acted on, one documented, one rejected on evidence:

- **Exception containment (HIGH) — fixed.** `Safety.cs`. The startup hooks were wrapped
  in `Guard` and the gameplay patches were not, which is backwards: the gameplay ones run
  inside the turn loop and the UI, where an escaping exception interrupts whatever the
  game was doing. Three sites now contain: the perk lookup in `RunGate.WasTheReason`, the
  virtual `BaseDungeonScenario.CanInteractObstacles` call, and the whole tooltip panel
  chain. Failures are reported once per site per session and always resolve *toward*
  vanilla — a contained failure refuses, so it is identical to the mod not being
  installed. This mod is a convenience; it must never be able to break a raid.
- **Silent config parse failure (MEDIUM) — fixed.** `Bool()` returned the fallback both
  for a missing key and for a present-but-unreadable one. Four keys default to `true`, so
  a mistyped `run_open_doors=flase` meant to switch a permission *off* left it on and
  reported the wrong value with no hint. A present key that cannot be read now warns and
  names the line.
- **`PickupScope` does not record which key opened the scope (MEDIUM) — documented, not
  changed.** A reason token was considered and rejected: `CanUseInventory` is told
  nothing about what is asking, so even with a token the answer would still be "whichever
  scope opened last" rather than "the thing being requested". The assumption it actually
  rests on is now written down at `PickupScope.IsOpen`, along with what to do if a future
  build breaks it.
- **Ally movement tooltip could misreport permissions (HIGH) — rejected, not reachable.**
  The concern was that `BuildMovementStateTooltip` takes a generic `Mercenary` and might
  be built for an ally, who receives none of these grants. It cannot be, in 1.0.3:
  the method has exactly one caller, `MoveStatePanel.OnPointerEnter`, and
  `MoveStatePanel.Player` is `_creatures.Player`. Verified by reading the call graph, not
  argued from the signature. Recorded as a watch item under Pending.

## Active

- [ ] **In-game verification.** Everything below is UNTESTED in a running game. The mod
      builds with 0 warnings and every game reference resolves — but no patch has been
      observed attaching, and no door has been opened at a run.

## Pending

- [ ] Release folder is named `Quasimorph_Stride_v0.1` and the mod reports v0.1, so
      these agree for now. Keep them in step with `-OutDir` at the next version.
- [ ] **Watch item.** `TooltipFactory.BuildMovementStateTooltip` takes a `Mercenary`, but
      in 1.0.3 its only caller passes the player. If a later build ever calls it for an
      ally, the corrective line would appear on a creature that receives none of these
      grants — the exact "tooltip that lies" failure the patch exists to prevent. Re-run
      the caller check on any game update; the fix would be to compare the `mercenary`
      argument against the player's before appending.
- [ ] The corrective tooltip line is written in English. The sibling Thai mod translates
      the game; if this mod is ever shipped alongside it for a Thai-reading player, that
      line is the one thing here that will not follow. Not worth a `Localization.Get`
      dependency — the most contested method in the installed set — unless asked for.

## Known Failures

- Nothing observed. Nothing yet observable — see Active.

## Decisions

Recorded in full in `.claude/plans/run-actions.plan.md`. The load-bearing ones:

- 2026-09-02 — **Three narrow postfixes, not one on `PerkSystem.GetPerkParameterBool`.**
  The one-line version would grant the same thing to every ally and enemy the game asks
  about, and runs for every perk parameter in the game rather than these three.
- 2026-09-02 — **Action points deliberately untouched.** `Player.FreeInteractObstacles`
  and `Player.FreeInventoryUse` are both `MovementState == Slow`. Interacting while
  running still ends the turn exactly as at walking pace. This is the entire balance
  answer and it cost nothing, because the flags are simply never patched.
- 2026-09-02 — **`CanUseInventory` is scoped rather than lifted.** It takes no argument
  saying what is asking; one check guards floor pickup, corpse looting, vest slots, the
  inventory screen and the healing screen. The request named two of those. Three
  prefix/finalizer pairs open a depth-counted window around the wanted paths and the
  grant lives inside it. `run_full_inventory=true` is the opt-in blunt version.
- 2026-09-02 — **Finalizers, not postfixes, close the scope.** A postfix does not run
  when the original throws, and a scope stuck open silently becomes the unconditional
  grant that the scoping exists to prevent.
- 2026-09-02 — **Lifting a short-circuited check means owning the ones below it.**
  Vanilla returns on the Run test and never evaluates `MutatedQuasimorph`/elevator, the
  scenario's own `CanInteractObstacles`, or `ChangedMercenary`. All three are re-tested
  here before anything is granted, so the mod cannot hand a Baron the elevator or a
  tutorial the object it is withholding.
- 2026-09-02 — **`limitType` is an exact discriminator, not a heuristic.** Vanilla sets
  `RunNoObstacles` before its first check and overwrites it before any other refusal
  returns, so on a `false` result `limitType == RunNoObstacles` means the Run stance was
  the cause. This is what keeps an ally across the room out of reach.
- 2026-09-02 — **A null `contextObstacle` leaves vanilla's answer alone.** `MovePlayer`
  reaches the gate with a null obstacle when a cell is flagged `ClosedDoor` but no door
  object is found there. Unclassifiable, so untouched.
- 2026-09-02 — **Corpses answer to `run_take_items`.** A corpse is a container to the
  game, but searching one is how you pick things up off a body.
- 2026-09-02 — **Elevators and vest slots off by default.** Sprinting into an extraction
  changes how a raid ends; a grenade mid-sprint is a combat capability. Both are the
  player's decision, not a bug fix.
- 2026-09-02 — **`OpenTheDoor` deliberately not patched.** It is the method the Workshop
  mod *Run and Door* uses and it is already two-way contested here (*Speed Toggle*,
  *Vanilla Set Bonuses*). Patching the permission check in front of it is a smaller
  surface, an uncontested one, and it covers items as well.
- 2026-09-02 — **No private members at all.** Unlike Signals, every member this mod calls
  is public, so `tools/apicheck.py` covers the whole call surface structurally. Only the
  patch targets are addressed by name, and `nameof` turns a rename into a build failure
  here while `PatchVerify` turns it into a log line elsewhere.
- 2026-09-02 — **`PatchAll` runs even when individual unlocks are off**, because the
  config is read inside each patch. The one exception is when *every* unlock is off, in
  which case no patches are applied at all and the log says why — a mod that can only
  ever repeat the game's own answer should not be in the patch table.

## Conflict map, as measured

Scanned all 104 installed Workshop assemblies for references to every patch target.

| Target | Referenced by | Meaning |
|---|---|---|
| `CanInteractObstacles` | `QM_SpeedToggle` (calls, does not patch) | Composes — it asks, we answer wider |
| `CanUseInventory` | nothing | Clear |
| `CanOpenAllyInventory` | nothing | Clear |
| `TakeItemOrLootCorpse` | nothing | Clear |
| `InteractVestSlot` | nothing | Clear |
| `BuildMovementStateTooltip` | nothing | Clear |
| `ProcessCmd` | `RedsOptionalTweaks`, `AllyRoamPatrol` | Our prefix sets a bool and never returns false |
| `OpenTheDoor` | `QM_SpeedToggle`, `VanillaSetBonuses` | **Not patched here, by design** |

## Next Action

Launch the game with the mod installed and check, in order:

1. `QuasimorphStride.log` says *"all 7 patches attached"*. If not, stop — the rest cannot
   work.
2. Run stance, adjacent closed door, click it → **it opens**, and the turn ends.
3. Run stance, click a cell across a room with a door on the path → the character runs,
   opens the door, and **the rest of the move is not thrown away**. This is the one that
   matters most; it is the vanilla behaviour that actually hurts.
4. Run stance, standing on loot, pick it up → the floor storage opens.
5. Run stance, corpse → it can be searched.
6. Run stance, press the **inventory button** on the HUD → **still refused**. This is the
   scoping working. Set `run_full_inventory=true`, restart, confirm it now opens.
7. Run stance, elevator with `run_use_elevators=false` → still refused. Set it true,
   restart, confirm it works.
8. Run stance, vest slot with `run_use_vest=false` → still refused.
9. Tutorial or scenario object the game is withholding → **still withheld** while
   running. Same for an elevator as a mutated Baron. These are the re-run checks.
10. **Slow stance still gives free interaction; Normal and Run still end the turn.** The
    mod must not have made anything free.
11. Run tooltip no longer claims actions are forbidden, and the green line matches the
    config.
12. `enabled=false`, restart → vanilla behaviour returns exactly, and the log says no
    patches were applied.

Then, with **Speed Toggle** active and accelerated: doors still open, without the pause.
