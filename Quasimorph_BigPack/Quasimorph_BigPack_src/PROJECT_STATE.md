# Project State — Quasimorph Big Pack

**Mod version**: 0.1.0 — unlimited inventory space
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Phase**: 5 of 5 — Live test | **Status**: BUILT, NOT YET RUN IN GAME
**Updated**: 2026-09-01
**Branch**: `feat/unlimited-inventory`

Plan: `.claude/plans/unlimited-inventory.plan.md`

## Phases

| Phase | What | Status |
|---|---|---|
| 0 | API spike | **COMPLETE** — resolved entirely by static analysis, no spike build needed |
| 1 | Skeleton, config, logging, build | **COMPLETE** — builds clean, apicheck 0 unresolved |
| 2 | Space (`Inventory.ResizeStorage` postfix + re-assert) | **IMPLEMENTED, UNTESTED** |
| 3 | Weight (`CreatureData.GetItemsWeight` postfix) | **IMPLEMENTED, UNTESTED** |
| 4 | Uninstall safety (warning + README) | **IMPLEMENTED, UNTESTED** |
| 5 | Package and live test | **BLOCKED** — needs a game launch |

## What "UNTESTED" means here

The mod compiles, every one of its 13 game references resolves against the shipped
assemblies, and every API assumption below was checked against the game's own IL rather
than guessed. **None of it has been run.** Harmony applies patches at runtime, so
parameter-name matching, hook firing and the UI's behaviour with a 50-row grid are all
still unproven.

## Phase 0 — answered from IL, not from a spike

| Question | Answer | Evidence |
|---|---|---|
| Is `ExpandHeight(int)` a delta or absolute? | **Delta.** `Height = Height + arg`, then `_positions` regrown to `MaxCapacity`. | `ItemStorage::ExpandHeight`, 32 bytes IL |
| Is `BackpackStore.Source` really `Backpack`? | **Yes**, `source=1`. `VestStore` is `source=2`. | `Inventory::.ctor` IL_024e, IL_025e |
| How tall is a vest? | **`height = 1`** — a literal single row. Confirms `resize_vest=false` default. | `Inventory::.ctor` IL_0260 |
| What are `ResizeStorage`'s parameter names? | `storage, width, height, itemsOnFloor, forceFloor` | Param table |
| Does shrinking destroy items? | **Yes.** Excess goes to `itemsOnFloor`, or `Remove` when there is none. | `Inventory::ResizeStorage` IL_007b |
| Does weight cap anything? | **No.** Purely a modifier; 8 consumers, all through `GetItemsWeight`. | caller scan |

Still open, and only a real game can answer:

- (b) Does `ItemGrid` scroll to row 50, and does drag-and-drop hit the right cell there?
- (c) Does the vest strip scroll at all? Decides whether `resize_vest` is ever usable.

## Architecture

Two Harmony patches, both on ordinary gameplay methods. Nothing touches `State`,
`GameLoop`, `Data` or the bootstrap path — the rule inherited from the sibling Floor Loot
mod, where a Workshop mod black-screened the game by patching `State.Resolve`.

| Piece | How |
|---|---|
| Space | Postfix `Inventory.ResizeStorage`. It is the single funnel — 9 call sites — so equipping, unequipping, breaking and repairing a backpack all pass through it. Postfix, not prefix, so the game's overflow handling runs against the size it chose. Growth via `ItemStorage.ExpandHeight`, never by re-calling `ResizeStorage` (that would recurse). |
| Initial size | Storages built in the `Inventory` constructor never hit `ResizeStorage`, so a re-assert pass runs on `AfterSaveLoaded`, `SpaceStarted` and `DungeonStarted`. |
| Ownership | `State.Get<Mercenaries>().Values` → `Mercenary.CreatureData.Inventory`, reference equality. Entirely public, no reflection. This is what keeps monsters out. |
| Weight | Postfix `CreatureData.GetItemsWeight` → `0f` for player mercenaries only. |
| Uninstall safety | The resize postfix records the vanilla height the game asked for; a check warns when the pack holds more than that would fit. |

## Decisions

- 2026-09-01 — Scope: player backpack + vest only, weight penalties removed. (Owner.)
- 2026-09-01 — Height only, never width: no horizontal scrollbar exists anywhere.
- 2026-09-01 — `BackpackMode.Endless` rejected: `_backpackMode` is set only in the
  `Inventory` constructor, which runs for every creature before ownership is knowable.
- 2026-09-01 — Harmony parameters matched by the game's real names (`storage`, `height`)
  rather than positional `__0`/`__2`, so a rename fails loudly at patch time.

## Known risks

- **Uninstalling with an over-full pack destroys the excess.** Confirmed in IL, not
  speculation. Mitigated by a log warning and the README, not preventable from a mod.
- Zeroing weight also gives up the weight *bonuses* to melee damage and physical resist.
  `remove_weight=false` restores vanilla.

## Next action

Install and launch:

```powershell
cd Quasimorph_BigPack\Quasimorph_BigPack_src
.\build.ps1 -Install
```

Then start a game and read `QuasimorphBigPack.log` beside the DLL. Expected lines: the
config echo, `harmony patches applied`, a `re-assert` line naming a mercenary count, and a
`backpack: NxM -> Nx50` line. Then check the inventory screen actually scrolls to row 50.
