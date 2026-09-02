# Project State — Quasimorph Big Stack

**Mod version**: 0.1.1 — item stacks to 9999
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Phase**: 4 of 4 — Live test | **Status**: PARTIALLY TESTED IN GAME — one bug found and fixed
**Updated**: 2026-09-01

## 0.1.1 — bought and reward ammunition arrived at 9999

Reported from play. Root cause is one line of vanilla code:

```csharp
public StackableItemComponent(short max) { Max = max; Count = max; }
```

Every stackable item is created **full**. Vanilla survives that because "full" is a sane
number. `ItemFactory.CreateComponent` overwrites `Count` with a random amount only for
`AmmoRecord`, and only when `randomizeConditionAndCapacity` is set — and both reported
routes call `CreateForInventory(id, false, false)`:

| Route | Path |
|---|---|
| Mission rewards | `MissionFactory.GenerateReward` → `MissionSystem.AddReward` → `CreateForInventory(id, false, false)` |
| Station stock | `TradeSystem.GetRandomItemsFromStation` → `CreateForInventory(id, 0, 0)` |

Fixed by `InitialCountPatch`: a postfix on the `StackableItemComponent(short)` constructor
clamps `Count` back to the vanilla maximum, captured from the `GetMaxStackSize` call that
happens on the instruction immediately before. `Max` is left at the raised ceiling, so
capacity is unchanged — an item now holds a normal amount in a 9999-capacity container.

Ruled out along the way: the three dictionaries `GetRandomItemsFromStation` builds
(`StackCount`, `InventoryWidthSize`, `MaxStack` by item id) are UI and valuation metadata,
not stock quantities, so they needed no patch.
**Branch**: `feat/stack-size-9999` (off `Quasimorph`)

Plan: `.claude/plans/stack-size-9999.plan.md`

## Phases

| Phase | What | Status |
|---|---|---|
| 1 | Skeleton, config, logging, build | **COMPLETE** — builds clean, apicheck 0 unresolved |
| 2 | `ItemFactory.GetMaxStackSize` postfix | **COMPLETE** — confirmed working in game |
| 2b | `InitialCountPatch` — initial count back to vanilla | **IMPLEMENTED, UNTESTED** (0.1.1) |
| 3 | Wind-down safety (warning + README) | **IMPLEMENTED, UNTESTED** |
| 4 | Live test | **PARTIAL** — stacks confirmed raised; 0.1.1 fix and trade effects still unverified |

## What "UNTESTED" means here

The mod compiles, all 10 game references resolve against the shipped assemblies, and every
API claim below was read out of the game's IL. **None of it has been run.** Harmony binds
at runtime, so hook firing and parameter matching are unproven, and the trade-economy
effect is genuinely unknown.

## Findings — read from IL, not assumed

| Question | Answer | Evidence |
|---|---|---|
| Where does a stack limit come from? | `ItemFactory.GetMaxStackSize(IStackableRecord)`, the single funnel; 7 callers | caller scan |
| What does it do? | `record.MaxStack × difficulty.Preset.ItemsStackSize` (X1–X4), via `conv.i2` | `GetMaxStackSize`, 88 bytes IL |
| Can records just be edited instead? | **No.** `9999 × 4 = 39996` overflows `short` and wraps to **-25540** | same IL |
| Do existing saves need migration? | **No.** `FixStacksCount` re-derives every item's max on the ship | caller scan: `VisitStation`, `AfterRaidScreen`, `ArsenalScreen`, `AugmentationScreen`, `FastTradeScreen`, `TradeShuttleScreen` |
| Does shrinking destroy items? | **Not directly.** Excess is banked, other stacks topped up, remainder spawned via `CreateForInventory` | `FixStacksCount` IL_00a2–IL_01cb |
| So what is the risk? | The spawned stacks are placed with `AddItemAndReshuffleOptional` — non-forcing, so overflow is dropped | `FixStacksCount` IL_01cb |
| Can it be confined to the player? | **No.** The method takes a record, not an inventory | signature |
| Why cap at 30000? | `FixItemCount` computes `GetMaxStackSize + ConsumablesStackBonus` then `conv.i2` | `FixItemCount` IL_0000–IL_0013 |

Still open, and only a real game can answer:

- Do stacks reach 9999 and display sanely in the grid and tooltips?
- **Does station stock or trade pricing distort?** `TradeSystem` reads `MaxStack` in six
  places when generating and valuing station inventory. This is the main unknown.
- Does the wind-down actually work end to end?

## Architecture

One Harmony postfix on an ordinary item-factory method. Nothing touches `State`,
`GameLoop`, `Data` or the bootstrap path.

| Piece | How |
|---|---|
| Stack limit | Postfix `ItemFactory.GetMaxStackSize` → `ModConfig.MaxStack`. Override, not multiply, so the configured number holds on any difficulty. Null record left at vanilla 1 — that is "does not stack", not a limit to raise. |
| Existing saves | Nothing. The game's own `FixStacksCount` does it. |
| Wind-down warning | On `AfterSaveLoaded` and `SpaceStarted`, reports how many extra stacks a split at `wind_down_stack` would create, and therefore how many free slots are needed. |

## Decisions

- 2026-09-01 — Separate sibling mod, not folded into Big Pack. (Owner, revised from the
  first draft.)
- 2026-09-01 — Patch `GetMaxStackSize` rather than the 11 record types, because the record
  route overflows `short` on an X4 difficulty preset.
- 2026-09-01 — Ceiling clamped to 30000 for `ConsumablesStackBonus` headroom.
- 2026-09-01 — **Deviation from the plan.** The plan proposed capturing each record's
  vanilla maximum from `__result` and warning against it. Dropped: the game gives no way
  to get from a `BasePickupItem` back to its `IStackableRecord`, so the captured value
  could never be matched to a carried item. The warning instead reports slot arithmetic
  against a configurable `wind_down_stack`, which needs no mapping and answers the more
  useful question — how many free slots the split will need.

## Known risks

- **Winding down or uninstalling can drop items** the grid has no room for. Mitigated by
  the stepwise procedure in the README and the log warning; not preventable from a mod.
- **Trade economy** is the untested unknown; `max_stack` is configurable so a bad result
  is a number change, not a rebuild.
- Third copy of the shared scaffolding (`ModLog`, `ModConfig`, `build.ps1`, `tools/`),
  ported from Big Pack `79f7050`. Accepted cost of independent mods.

## Next action

Install and launch:

```powershell
cd Quasimorph_BigStack\Quasimorph_BigStack_src
.\build.ps1 -Install
```

Then read `QuasimorphBigStack.log`. Expected: the config echo, `harmony patch applied:
ItemFactory.GetMaxStackSize`, and a `stack ceiling:` line naming the vanilla value it
overrode. Then check an ammo stack in the arsenal and look hard at station stock.
