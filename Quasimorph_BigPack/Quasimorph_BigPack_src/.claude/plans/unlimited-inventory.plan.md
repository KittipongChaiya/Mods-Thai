# Plan: Big Pack — unlimited inventory space

**Target game**: Quasimorph `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Assembly**: `QuasimorphBigPack` · **Folder**: `Quasimorph_BigPack`
**Complexity**: Medium
**Status**: PLANNED — awaiting confirmation

## Summary

A standalone mod that makes the player mercenary's backpack effectively unlimited and
removes the weight penalties that would otherwise punish carrying a full one. Scope was
set by the owner on 2026-09-01: **raid backpack + vest only** (ship and station cargo
already grow themselves), and **weight penalties removed** as well as space.

## Scope decisions (owner, 2026-09-01)

| Question | Answer |
|---|---|
| Which storages | Player mercenary's backpack + vest. Not ship/station cargo, not containers, not corpses. |
| Weight | Removed too. Space alone would let you carry everything and then be crushed by dodge/satiety penalties. |

## Phase 0 findings (from static analysis of `Assembly-CSharp.dll`)

### Weight is a penalty, not a cap

Nothing blocks a pickup on weight. `CreatureData.GetItemsWeight()` is a **single funnel**
feeding eight consumers, all of them penalties or display:

```
CreatureData.GetWeightMeleeDamageModifier    CreatureData.GetDodge
CreatureData.GetWeightPhysicalResistBonus    Player.OnMoved
CreatureData.GetItemsWeightSatietyDrain      Player/Monster.ProcessMeleeAttackOnEnemy
ProcessKnockback                             InventoryWeightPanel / TooltipFactory /
                                             MercenaryBackpackIcon / PrepareRaidScreen
```

One postfix on `GetItemsWeight` therefore neutralises the whole system. See the risk
table for what that costs.

### `Inventory.ResizeStorage` is the single resize funnel

Every path that changes a backpack or vest size in the game reaches this one public
method — 9 call sites in 8 methods:

```
Player.ProcessBrokenBackpackOrVest      Inventory.ResizeBackpack
Inventory.EnableAdditionalSlot          Inventory.ResizeVest
Inventory.BackpackSlotOnItemAdded       ItemInteractionSystem.Repair  (x2)
Inventory.BackpackSlotOnItemRemoved
Inventory.BackpackSlotOnOnItemSwitched
```

This matters because **anything we set gets reset**: equipping, unequipping, breaking or
repairing a backpack all re-derive the size from the equipped item's `BackpackRecord`.
A one-shot resize would silently revert mid-raid.

### The UI already scrolls vertically, but only vertically

```
InventoryScreen._backpackGridView   MGSC.ItemGrid
InventoryScreen._backpackScrollbar  MGSC.CommonScrollBar      + type MGSC.ItemGridScroll
ItemsStorageView                    _itemGrid + _scrollbar
```

and the game itself grows storages at runtime through `ItemStorage.ExpandHeightAndPutItem`
(used by `InventoryScreen`, `FastTradeScreen` and `StationExchangeWindow`). So a tall grid
is a shape the game already renders. **Width is fixed by the panel layout — grow height
only, never width.**

### Confining to the player needs no reflection

```
State.Get<Mercenaries>().Values : List<Mercenary>   ->  Mercenary.CreatureData.Inventory
State.Get<Mercenaries>().MercenaryInRaid            ->  same, for the raid case
Player.Mercenary                                    ->  public property
```

All public. An `Inventory` is the player's **iff** it is `merc.CreatureData.Inventory` for
some merc in that list. Monsters carry `Inventory` too, so this predicate is what stops
the mod from handing every enemy an infinite backpack.

### Verified API shapes (1.0.3)

```
Inventory..ctor(int bpWidth, int bpHeight, int vestWidth, BackpackMode, bool canHaveVest)
Inventory.ResizeStorage(ItemStorage, int width, int height, ItemStorage slot, bool)  public
Inventory.ResizeBackpack(int, int, ItemStorage)                                      private
Inventory.ResizeVest(int, int, ItemStorage)                                          private
Inventory.BackpackStore / VestStore                     public fields -> ItemStorage
ItemStorage.Width / Height                              public get, PRIVATE set
ItemStorage.Resize(int, int)                            public
ItemStorage.ExpandHeight(int)                           public
ItemStorage.ResizeAndReshuffle(int, int, ref List<BasePickupItem>)  public
ItemStorage.Source                                      public -> ItemStorageSource
CreatureData.GetItemsWeight()                           public
enum BackpackMode { Normal, Endless }                   Inventory._backpackMode (private)
enum ItemStorageSource { Floor, Backpack, Vest, ... ShipCargo, StationCargo, ShuttleCargo }
```

### The `BackpackMode.Endless` question — deliberately not the design

The game ships an `Endless` backpack mode, read by `BackpackSlotOnItem*`, `VestSlotOn*`
and `TryAddItemToAnyStorage`. It is tempting, but `_backpackMode` is private and set only
in the `Inventory` constructor, so using it means patching a constructor that runs for
every creature before we can tell whose it is. Phase 0 tests it as a possible
simplification; the plan does not depend on it.

## Patterns to mirror (from `Quasimorph_FloorLoot`, commit `11249c6`)

| Category | Source | Pattern |
|---|---|---|
| Entry point | `FloorLootMod.cs:41` | `[Hook(ModHookType.AfterConfigsLoaded)]`, public static, `IModContext` |
| State | `FloorLootMod.cs:44` | Capture `State` from `context.State`. **Never** patch `State`/`GameLoop`/`Data` |
| Harmony | `FloorLootMod.cs:52` | `new Harmony(ModInfo.HarmonyId).PatchAll(...)` inside the hook; let it throw |
| Error containment | `FloorLootMod.cs:171` | `Guard(what, action)` around every hook and patch body |
| Reflection audit | `LootButton.LogFieldResolution` | Resolve string-named members once, log whether each was found |
| Config | `ModConfig.cs` | `config.txt` key=value, written commented on first run, bad file falls back to defaults |
| Logging | `ModLog.cs` | Dedicated log beside the DLL, mirrored into `Player.log` |
| Build | `build.ps1`, `.csproj` | netstandard2.1, `<Private>false</Private>`, `apicheck.py` gate |

## Design

Two patches, both off the bootstrap path.

**1. Space — postfix `Inventory.ResizeStorage`.**
Postfix, not prefix: the game computes item overflow against the size it passed, so let it
finish at the real size and then grow. Growing can never orphan an item; shrinking can.

```
postfix(Inventory __instance, ItemStorage storage)
    if not enabled                              -> return
    if storage.Source not in {Backpack, Vest}   -> return
    if __instance is not a player mercenary's   -> return
    if storage.Height >= target                 -> return
    storage.ExpandHeight(...)                   -> NOT __instance.ResizeStorage(...)
```

The last line is load-bearing: calling `ResizeStorage` from its own postfix re-enters the
patch and recurses forever. Grow through `ItemStorage` directly.

**2. A re-assert pass.** Storages created in the `Inventory` constructor never pass through
`ResizeStorage`, so the patch alone misses a mercenary who has no backpack equipped. Re-assert
on `AfterConfigsLoaded` and `DungeonStarted` over `State.Get<Mercenaries>().Values`.

**3. Weight — postfix `CreatureData.GetItemsWeight()`** returning `0f` for player mercenaries
only, gated on a config key.

## Files to change

| File | Action | Why |
|---|---|---|
| `mod_src/QuasimorphBigPack/QuasimorphBigPack.csproj` | CREATE | Copy of the FloorLoot csproj, renamed |
| `mod_src/QuasimorphBigPack/modmanifest.json` | CREATE | `UniqueModName: QuasimorphBigPack` |
| `mod_src/QuasimorphBigPack/BigPackMod.cs` | CREATE | Hooks, Harmony bootstrap, `Guard` |
| `mod_src/QuasimorphBigPack/ModLog.cs` | CREATE | Port of the FloorLoot logger |
| `mod_src/QuasimorphBigPack/ModConfig.cs` | CREATE | Port; keys below |
| `mod_src/QuasimorphBigPack/PlayerInventories.cs` | CREATE | The "is this the player's?" predicate, one place |
| `mod_src/QuasimorphBigPack/SpacePatch.cs` | CREATE | `ResizeStorage` postfix + re-assert pass |
| `mod_src/QuasimorphBigPack/WeightPatch.cs` | CREATE | `GetItemsWeight` postfix |
| `build.ps1`, `tools/apicheck.py`, `tools/cli_meta.py` | CREATE | Vendored from FloorLoot |

### config.txt

```
enabled=true
backpack_height=50      # rows. Width is never touched - the panel cannot scroll sideways.
resize_vest=false       # the vest is a horizontal strip; see Risks
remove_weight=true      # zero the weight the penalty formulas see
```

## Phases

- [x] **Phase 0 — Spike.** **Resolved by IL, no spike build needed.**
      (a) `ExpandHeight(int)` is a **delta**: `Height = Height + arg`, then `_positions` is
      regrown to `MaxCapacity`. Exactly the primitive wanted.
      Also settled: `BackpackStore.Source == Backpack (1)`, `VestStore.Source == Vest (2)`,
      the vest is built at `height = 1`, and shrinking really does `Remove` excess items.
      (b) and (c) — whether `ItemGrid` scrolls to row 50 and whether the vest strip scrolls
      at all — **still open, and only a running game can answer them.**
- [x] **Phase 1 — Skeleton.** csproj, manifest, `ModLog`, `ModConfig`, `BigPackMod`,
      `PlayerInventories`, `build.ps1`, vendored tools. Builds clean with 0 warnings;
      `apicheck` resolves all 13 member references. Mod load itself is UNTESTED.
- [x] **Phase 2 — Space.** `ResizeStorage` postfix + re-assert on `AfterSaveLoaded`,
      `SpaceStarted`, `DungeonStarted`. UNTESTED in game.
- [x] **Phase 3 — Weight.** `GetItemsWeight` postfix, gated on `remove_weight`. UNTESTED.
- [x] **Phase 4 — Uninstall safety.** `UninstallRisk` records the vanilla height the game
      asked for and warns when the pack holds more than that. README leads with the
      procedure. UNTESTED.
- [ ] **Phase 5 — Package and live test.** BLOCKED: needs a game launch. Raid, fill the
      pack, save, reload, exit to ship, then the uninstall drill on a **copied** save.

## Validation

```powershell
cd Quasimorph_BigPack\Quasimorph_BigPack_src
.\build.ps1              # compile + apicheck.py must report 0 unresolved
.\build.ps1 -Install     # copies into LocalUserPresets\QuasimorphBigPack
```

Then launch, run one raid, and read `QuasimorphBigPack.log` beside the DLL.

## Risks

| Risk | Likelihood / Impact | Mitigation |
|---|---|---|
| **Uninstalling with an over-full pack loses items.** Sizes are serialised. Remove the mod and the next `ResizeBackpack` calls `ResizeAndReshuffle`, which hands back the items that no longer fit — and the game decides their fate, not us. | Medium / **severe** | Phase 4: documented "empty your pack into ship cargo before uninstalling", plus an in-game warning. Phase 5 drills it on a copied save. |
| Zeroing weight also removes weight **bonuses** — `GetWeightMeleeDamageModifier` and `GetWeightPhysicalResistBonus` suggest a heavy load helps melee and physical resist. | High / low | Accepted and documented; `remove_weight=false` keeps vanilla. Revisit only if it feels wrong in play. |
| Vest is a horizontal strip; extra rows may render off-panel. | Medium / low | `resize_vest=false` until Phase 0 (c) says otherwise. |
| Recursion via `ResizeStorage` postfix calling `ResizeStorage`. | Certain if written naively | Grow through `ItemStorage.ExpandHeight`; never re-enter the patched method. |
| Another mod patches `Inventory.ResizeStorage` or `GetItemsWeight`. | Low | Postfix only, no `__result` on the resize path; re-check the installed 90 before release. |
| Game update moves a patch target. | Medium | `apicheck.py` in the build; `Guard` degrades the mod to a no-op rather than a crash. |
| Huge grids slow `Reshuffle` / sort. | Low | `backpack_height` is config; 50 rows is ~2 orders below anything pathological. |

## Decisions

- 2026-09-01 — Scope: player backpack + vest only; weight penalties removed. (Owner.)
- 2026-09-01 — Grow **height only**. Width is fixed by panel layout; height is what the
  game's own cargo screens already grow and what `_backpackScrollbar` exists to serve.
- 2026-09-01 — Postfix `Inventory.ResizeStorage` rather than prefix-rewriting its width and
  height arguments, so the game's overflow handling always runs against the size it chose.
- 2026-09-01 — `BackpackMode.Endless` rejected as the primary design: reaching it means
  patching a constructor that runs for every creature before ownership is knowable.
- 2026-09-01 — Separate mod, not a feature of `Quasimorph_FloorLoot`. Different concern,
  different risk profile, and this one can lose items on uninstall while that one cannot.

## Next action

Phase 5. Everything is written and builds; nothing has been run. Install and launch:

```powershell
cd Quasimorph_BigPack\Quasimorph_BigPack_src
.\build.ps1 -Install
```

Then read `QuasimorphBigPack.log` for `harmony patches applied`, a `re-assert` line and a
`backpack: NxM -> Nx50` line, and check on screen that the grid scrolls to row 50.
