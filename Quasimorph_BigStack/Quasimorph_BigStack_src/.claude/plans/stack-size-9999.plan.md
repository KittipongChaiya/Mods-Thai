# Plan: Big Stack — item stacks to 9999

**Target game**: Quasimorph `1.0.3.578s.024ad60`
**Assembly**: `QuasimorphBigStack` · **Folder**: `Quasimorph_BigStack`
**Complexity**: Small — one patch; most of the work is the scaffolding a standalone mod needs
**Status**: PLANNED — awaiting confirmation

## Summary

A standalone mod raising the maximum stack size of every stackable item to a configurable
ceiling, default 9999. A **sibling** of Big Pack and Floor Loot, not part of either: same
build tooling and conventions, independently installable, independently removable.

## Scope decisions (owner, 2026-09-01)

| Question | Answer |
|---|---|
| Packaging | **Separate mod.** Revised from the first draft, which folded it into Big Pack. |
| Item scope | All 11 categories implementing `IStackableRecord`. |
| Safety | Configurable ceiling plus a documented wind-down, not a fixed 9999. |

### What separating costs, and why it is still right

`ModLog`, `ModConfig`, `build.ps1` and `tools/` get copied a third time. That duplication
is real, but it is the correct trade for a Quasimorph mod: each mod is its own
`LocalUserPresets` folder with its own DLL, so sharing code would mean either a shared
assembly the game has no way to load or a build step that stitches sources together. Three
small independent mods that can each be deleted on their own beats one bundle where
removing the stack change means removing the backpack change too.

The two do interact, and the README says so: winding stacks back down needs somewhere to
put the split items, and Big Pack's 50-row grid is exactly that. Neither requires the
other.

## How stacking actually works (verified from IL)

`ItemFactory.GetMaxStackSize(IStackableRecord)` is the **single funnel** from config data to
an item's real limit. Decompiled, it is:

```csharp
public short GetMaxStackSize(IStackableRecord stackable)
{
    if (stackable == null) return 1;
    switch (_state.Get<Difficulty>().Preset.ItemsStackSize)   // enum X1..X4
    {
        case X2: return (short)(stackable.MaxStack * 2);
        case X3: return (short)(stackable.MaxStack * 3);
        case X4: return (short)(stackable.MaxStack * 4);
        default: return stackable.MaxStack;
    }
}
```

Seven callers: `ItemFactory.CreateComponent`, `CreatureSystem.SpawnAdditionalAmmo`,
`ItemInteractionSystem.Disassemble`, `FixStacksCount`'s inner `FixItemCount`,
`MercenarySystem.CloneInventoryForPhantom`, and the two production windows.

The item side is `PickupItem.MaxStack => _stackable?.Max ?? 1`, a
`StackableItemComponent` whose `Max` is public and settable through
`BasePickupItem.SetMaxStack(short)`.

### Existing saves fix themselves

`ItemInteractionSystem.FixStacksCount(ItemStorage)` walks a storage, calls
`GetMaxStackSize` per item and `SetMaxStack` on each. It runs from
`SpaceGameMode.VisitStation`, `AfterRaidScreen`, `ArsenalScreen`, `AugmentationScreen`,
`FastTradeScreen` and `TradeShuttleScreen`. So **no migration sweep is needed** — items
already in a save get the new ceiling the next time the player is on the ship.

### Shrinking is safe, and that is what makes a wind-down possible

`FixStacksCount` is not a clamp. When an item exceeds the current maximum it banks the
excess in `_consumablesCache` keyed by item id, then afterwards it:

1. tops up other stacks of the same id, up to their max;
2. for whatever is still left, calls `ItemFactory.CreateForInventory(id, 0, 0)` in a loop,
   filling each new stack to max;
3. places each one with `ItemStorage.AddItemAndReshuffleOptional`.

So lowering the ceiling **redistributes rather than deletes** — the basis of the wind-down
procedure. The catch is step 3: `AddItemAndReshuffleOptional` is the non-forcing variant,
so anything that will not fit in the grid is dropped.

## The finding that decides the design

The obvious approach — set `MaxStack = 9999` on all 11 record types at
`AfterConfigsLoaded`, no Harmony at all — **is unsafe**, and the IL says why.

`GetMaxStackSize` multiplies by the difficulty preset's stack setting and converts with
`conv.i2`. On a preset of X4 that is `9999 * 4 = 39996`, which does not fit in a `short`
and wraps to **-25540**. A negative maximum stack, on every stackable item, for any player
using that difficulty option.

Patching the funnel instead returns the final value directly and never enters the
multiply. That is the design.

Same reasoning caps the config: `FixItemCount` computes
`GetMaxStackSize(record) + consumablesBonus` and converts with `conv.i2` again, so the
ceiling is clamped to **30000**, leaving headroom under `short.MaxValue` (32767) for that
perk bonus.

## Patterns to mirror (from `Quasimorph_BigPack`, commit `79f7050`)

| Category | Source | Pattern |
|---|---|---|
| Entry point | `BigPackMod.cs:44` | `[Hook(ModHookType.AfterConfigsLoaded)]`, `Guard`, Harmony `PatchAll` |
| Patch style | `SpacePatch.cs` | Postfix, `try`/`catch` inside, parameters matched by the game's real names |
| Config | `ModConfig.cs` | `config.txt` key=value, commented template on first run, clamped ints |
| Logging | `ModLog.cs` | Log beside the DLL, mirrored to `Player.log` |
| Uninstall safety | `UninstallRisk.cs` | Record what vanilla would have done, warn when the player exceeds it |
| Build | `build.ps1` | netstandard2.1, `<Private>false</Private>`, `apicheck.py` gate |

## Design

One postfix, in the same style as Big Pack's two.

```
[HarmonyPatch(typeof(ItemFactory), nameof(ItemFactory.GetMaxStackSize))]
postfix(IStackableRecord stackable, ref short __result)
    if not enabled                -> return
    if stackable == null          -> return    // preserve the vanilla 1
    UninstallRisk.RecordVanillaMax(stackable, __result)
    __result = (short)ModConfig.MaxStack
```

Deliberately an override, not a multiplier: the player should get the number in the config
regardless of which difficulty stack option their campaign was started on.

**The vanilla maximum is free here.** `__result` on entry to the postfix is exactly what
the game would have used, so the wind-down warning gets its baseline without the
bookkeeping Big Pack needed — no re-assert pass, no weak table keyed on live objects.

Unlike Big Pack's patches this one **cannot be confined to the player**.
`GetMaxStackSize` takes a record, not an inventory — there is no owner in scope. Shop
stock, floor loot and enemy inventories get the same ceiling. That is inherent, not an
oversight, and the README will say so.

## Files to change

All under `Quasimorph_BigStack/Quasimorph_BigStack_src/`.

| File | Action | Why |
|---|---|---|
| `mod_src/QuasimorphBigStack/QuasimorphBigStack.csproj` | CREATE | Big Pack's csproj, renamed |
| `mod_src/QuasimorphBigStack/modmanifest.json` | CREATE | `UniqueModName: QuasimorphBigStack` |
| `mod_src/QuasimorphBigStack/BigStackMod.cs` | CREATE | Hook, Harmony bootstrap, `Guard` |
| `mod_src/QuasimorphBigStack/ModLog.cs` | CREATE | Port |
| `mod_src/QuasimorphBigStack/ModConfig.cs` | CREATE | `enabled`, `max_stack` (1–30000) |
| `mod_src/QuasimorphBigStack/StackPatch.cs` | CREATE | The `GetMaxStackSize` postfix |
| `mod_src/QuasimorphBigStack/UninstallRisk.cs` | CREATE | Vanilla max per record id; wind-down warning |
| `build.ps1`, `tools/apicheck.py`, `tools/cli_meta.py`, `.gitignore` | CREATE | Vendored |
| `README.md`, `PROJECT_STATE.md` | CREATE | Docs and resumable state |

### config.txt

```
enabled=true
max_stack=9999          # 1-30000. Read "winding down" in the README before lowering.
```

## Phases

- [x] **Phase 1 — Skeleton.** Project, manifest, `ModLog`, `ModConfig`, `BigStackMod`,
      `build.ps1`, vendored tools. Builds clean with 0 warnings; `apicheck` resolves all
      10 member references.
- [x] **Phase 2 — The patch.** `StackPatch.cs`. Logs the vanilla ceiling alongside the
      override the first time it fires. UNTESTED in game.
- [x] **Phase 3 — Wind-down safety.** `UninstallRisk.cs` + README procedure. **Deviates
      from this plan**: see Decisions — the vanilla-maximum capture was dropped because a
      `BasePickupItem` cannot be mapped back to its `IStackableRecord`, so the warning
      reports slot arithmetic against a configurable `wind_down_stack` instead.
- [ ] **Phase 4 — Live test.** BLOCKED: needs a game launch. The parts only a running
      game can settle:
      (a) do stacks actually reach 9999 and display sanely in grid and tooltips?
      (b) **does station stock or trade pricing distort?** `TradeSystem` reads `MaxStack`
      in six places when generating and valuing station inventory. This is the one
      genuinely unknown consequence and the main reason Phase 4 is not optional.
      (c) does the wind-down work: set `max_stack=50`, visit a station, confirm stacks
      split and nothing is lost, then uninstall cleanly.

## Validation

```powershell
cd Quasimorph_BigStack\Quasimorph_BigStack_src
.\build.ps1              # compile + apicheck must report 0 unresolved
.\build.ps1 -Install
```

Then read `QuasimorphBigStack.log` for the ceiling line and check a stack of ammo in the
arsenal.

## Risks

| Risk | Likelihood / Impact | Mitigation |
|---|---|---|
| `short` overflow producing a negative maximum | **Certain** if records are patched instead of the funnel | Patch `GetMaxStackSize`, bypassing the difficulty multiply; clamp config to 30000 for the `consumablesBonus` headroom |
| **Trade economy distortion.** Six `TradeSystem` methods size and value station stock from `MaxStack`. | Medium / unknown | Phase 4 (b) observes it directly. `max_stack` is configurable, so a bad result is a number change, not a rebuild |
| Winding down or uninstalling drops items the grid cannot hold — 9999 of an item that vanilla-stacks at 50 needs 200 slots | Medium / severe | Documented stepwise wind-down while the mod is still installed; log warning; Big Pack's 50-row grid absorbs most of it if installed |
| Stacks do not auto-consolidate — `FixStacksCount` only redistributes items *over* the max | High / cosmetic | Expectation-setting in the README; sort button and normal pickup merging do the rest |
| Global reach: shops, loot and enemies also get 9999 | Certain / low | Inherent to a record-level limit. Documented, not hidden |
| Third copy of the shared scaffolding drifts from its siblings | Medium / low | Ported verbatim from Big Pack `79f7050`; the plan records the source commit |
| A game update renames `GetMaxStackSize` or its parameter | Medium | `apicheck` in the build; `Guard` degrades the mod to vanilla stacks |

## Decisions

- 2026-09-01 — **Separate sibling mod**, revised from the first draft's "fold into Big
  Pack". Each mod stays independently installable and removable. (Owner.)
- 2026-09-01 — All 11 `IStackableRecord` categories. (Owner.)
- 2026-09-01 — Patch `ItemFactory.GetMaxStackSize` rather than mutating the 11 record
  types, because the record route overflows `short` on a X4 difficulty preset.
- 2026-09-01 — Override rather than multiply, so the configured number is what the player
  gets on any difficulty.
- 2026-09-01 — Ceiling clamped to 30000, not 32767, to leave room for
  `ConsumablesStackBonus` in `FixItemCount`'s `conv.i2`.
- 2026-09-01 — No save-migration sweep: `FixStacksCount` already re-derives every item's
  max on the ship.
- 2026-09-01 — ~~Vanilla maximum captured from `__result` in the postfix~~ **Reversed
  during Phase 3.** Capturing it is easy; *using* it is not. The warning needs the vanilla
  maximum for a carried item, and the game exposes no way to get from a
  `BasePickupItem` to its `IStackableRecord` — `Record<T>()` is generic over a concrete
  record type the caller has to know. The warning now reports how many extra stacks a
  split at `wind_down_stack` would create, which needs no mapping and is the more
  actionable number anyway.

## Next action

Phase 4 — install and launch. Everything is written and builds; nothing has been run.

```powershell
cd Quasimorph_BigStack\Quasimorph_BigStack_src
.\build.ps1 -Install
```
