# Plan: Stride — act while running

**Source**: free-form request, 2026-09-02 — *"a mod that modifies the running sequence so
that the character can do things like open doors and pick up items"*
**Target game**: Quasimorph `1.0.3`
**Host mod**: `QuasimorphStride` v0.1 — a new mod, sibling to Signals
**Complexity**: Small–Medium — 7 Harmony patches on 7 public methods, no private members,
no save data touched

## The restriction, and where it lives

The Run stance does not fail to interact by accident. Three checks in
`PlayerInteractionSystem` forbid it, all written identically:

```csharp
if (player.MovementState == CreatureMovementState.Run
    && !PerkSystem.GetPerkParameterBool(player.CreatureData, "BRunActions"))
    return false;
```

| Gate | Line (1.0.3) | Blocks |
|---|---|---|
| `CanUseInventory` | `:807` | Floor pickup, corpse loot, vest slots, inventory screen, healing screen |
| `CanInteractObstacles` | `:818` | Doors, containers, terminals, elevators, ladders |
| `CanOpenAllyInventory` | `:840` | Ally inventory, wound fixation |

`BRunActions` is `ParameterNames.PARAM_RUN_ACTIONS`, a real vanilla perk parameter. The
game already models "can act while running" and already grants it as a perk reward. This
mod grants it per config; it invents no behaviour.

Two door paths, one gate — which is why one patch covers both:

- click an adjacent door → `InteractObjCommand` → `InteractObstacle` (`:1172`) → gate
- run somewhere with a door on the path → `MoveCommand` → `MovePlayer` (`:403`) → gate,
  refusal sets `clearCmdQueue`, **the rest of the move is discarded**
- interact-in-front hotkey → `InteractObstacleInFrontOfPlayer` (`:522`) → gate

## Decisions

- **Postfixes on the three gates, not one postfix on `PerkSystem.GetPerkParameterBool`.**
  The single-patch version is one line and covers everything, and is rejected: that
  method takes a `CreatureData`, so it would silently grant the same thing to every ally
  and enemy the game asks about, and it is called for every perk parameter in the game
  rather than these three.

- **Action points are not touched, and that is the whole balance answer.**
  `Player.FreeInteractObstacles` and `Player.FreeInventoryUse` are both
  `MovementState == Slow`. Interacting while running still ends the turn through
  `EndPlayerTurn(MapObstacleInteraction)` exactly as it does at walking pace. The mod
  buys convenience, never action economy. Verified by reading the flags, not assumed.

- **`CanUseInventory` is scoped, not lifted.** It takes no argument saying what is
  asking; one check stands in front of five things and the request named two. So
  `TakeItemOrLootCorpse`, `ProcessCmd`-when-`TakeItem`, and `InteractVestSlot` open a
  depth-counted scope around themselves and the grant lives inside it. The HUD inventory
  button, healing screen and ally wound panel reach the check with no scope open and get
  vanilla's answer. `run_full_inventory` is the opt-in blunt version.

- **Finalizers, not postfixes, close the scope.** A postfix does not run when the
  original throws, and a scope stuck open silently becomes the unconditional grant the
  scoping exists to avoid.

- **The lifted gates re-run the checks vanilla short-circuited past.** Vanilla returns on
  the Run test and never evaluates `MutatedQuasimorph`/elevator, the scenario's own
  `CanInteractObstacles`, or `ChangedMercenary`. Lifting the first check means owning the
  rest; all three are re-tested before anything is granted.

- **`limitType` is the discriminator.** Vanilla writes `RunNoObstacles` before its first
  check and overwrites it before any other refusal returns. On a `false` result,
  `limitType == RunNoObstacles` is therefore an exact test for "the Run stance was the
  reason", not a guess. This is what keeps an out-of-reach ally out of reach.

- **A null `contextObstacle` leaves vanilla's answer alone.** `MovePlayer` can reach the
  gate with a null obstacle when a cell is flagged `ClosedDoor` but no door object is
  found. That state cannot be classified, so it is not touched.

- **Corpses answer to `run_take_items`, not `run_use_containers`.** A corpse is a
  container to the game, but searching one is how you pick things up off a body.

- **Elevators and vest off by default.** Sprinting into an extraction changes how a raid
  ends; a grenade mid-sprint is a combat capability. Both are decisions, not fixes.

- **The tooltip is corrected, not left lying.** Vanilla's Run tooltip states in red that
  inventory and actions are forbidden. A green line is appended naming what the config
  actually permits, generated from the same booleans the patches read. Written with
  `TooltipProperty.SetName`, **not** `Localization.Get` — the most contested method in
  the installed set (7 mods), and there is no vanilla key that says this anyway.

- **No private members anywhere.** Every member this mod calls is public, so
  `tools/apicheck.py` covers all of it structurally. Only the patch targets are addressed
  by name; `nameof` catches a rename at build time here, `PatchVerify` catches it at
  runtime elsewhere.

- **No per-turn hook.** The mod holds no world state, keys nothing to a creature and
  writes nothing to a save. `DungeonStarted` exists only to reset the scope depth.

## Files

| File | Action |
|---|---|
| `mod_src/QuasimorphStride/QuasimorphStride.csproj` | CREATE — mirrors Signals |
| `mod_src/QuasimorphStride/modmanifest.json` | CREATE |
| `mod_src/QuasimorphStride/StrideMod.cs` | CREATE — entry point, two hooks |
| `mod_src/QuasimorphStride/ModConfig.cs` | CREATE — 10 keys, self-documenting template |
| `mod_src/QuasimorphStride/ModLog.cs` | CREATE |
| `mod_src/QuasimorphStride/RunActions.cs` | CREATE — the three gate postfixes |
| `mod_src/QuasimorphStride/PickupScope.cs` | CREATE — three prefix/finalizer pairs |
| `mod_src/QuasimorphStride/RunTooltip.cs` | CREATE |
| `mod_src/QuasimorphStride/ConflictCheck.cs` | CREATE — informational only |
| `mod_src/QuasimorphStride/PatchVerify.cs` | CREATE |
| `build.ps1`, `tools/{apicheck,cli_meta}.py`, `.gitignore` | CREATE — copied from Signals |
| `README.md`, `PROJECT_STATE.md` | CREATE |

## Conflict check — measured, not assumed

Scanned all 104 installed Workshop assemblies for references to every target:

| Target | Referenced by | Verdict |
|---|---|---|
| `CanInteractObstacles` | `QM_SpeedToggle` — **calls** it, does not patch it | Composes |
| `CanUseInventory`, `CanOpenAllyInventory` | nothing | Clear |
| `TakeItemOrLootCorpse`, `InteractVestSlot`, `BuildMovementStateTooltip` | nothing | Clear |
| `ProcessCmd` | `RedsOptionalTweaks`, `AllyRoamPatrol` | Prefix sets a bool, never returns false — order-independent |
| `OpenTheDoor` (**not patched here**) | `QM_SpeedToggle`, `VanillaSetBonuses` | Avoided by design |

The Workshop mod **Run and Door** (2,794 subs) does this by patching `OpenTheDoor` — the
already-two-way-contested method — and was last updated seven days before 1.0 shipped.
Patching the gate instead of the door is both a smaller surface and an uncontested one,
and it covers items as well.

## Validation

```powershell
.\build.ps1 -Install
```

Build gate: 0 warnings, every game reference resolves. **PASS** — 20 member references
resolved.

In-game checklist — see `PROJECT_STATE.md` § Next Action. None of it has been run yet.

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| A refusal that should have stood is lifted | Medium | `limitType` discriminator + all short-circuited checks re-run. Tests 6 and 9. |
| `InventoryScreen` behaves oddly when opened mid-run by floor pickup | Medium | Nothing re-checks the gate once the screen is open — read, **not observed**. Test 4. |
| Scope left open by a vanilla exception | Low | Harmony finalizers + `DungeonStarted` reset |
| Balance drift toward free actions | Low | Impossible by construction — the Slow-only flags are untouched |

## Acceptance

- [x] Builds clean, 0 warnings, all references resolve
- [ ] All 12 in-game checks observed
- [ ] `enabled=false` leaves the game untouched
- [x] No enemy and no ally gains anything — all three patches read `creatures.Player`
- [x] README, `PROJECT_STATE.md` and this plan land with the code
