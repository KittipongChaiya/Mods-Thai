# Quasimorph Cheat Table — 0.9.87 → 1.0.3

**Table**: `Quasimorph-v1.0.3-1.CT` (Cheat Engine 7.x, table version 46)
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Predecessor**: `Quasimorph-v0.9.87-1.CT` — kept unchanged as a fallback
**Status**: offsets and hook logic VERIFIED statically · in-game behaviour **UNTESTED**
**Updated**: 2026-08-28

Original table by **Dickincorp**, backpack script by **Pekar**, both of
fearlessrevolution.com. This is a port of their work to 1.0.3, not new content.

---

## Why the 0.9.87 table cannot simply be re-pointed

It broke in two independent ways, and only one of them is a matter of new numbers.

1. **Hardcoded JIT offsets.** Every script hooked a fixed byte offset inside a
   JIT-compiled method — `MGSC.TradeSystem:SellItems+364`,
   `MGSC.ScreenWithShipCargo:DragControllerShowContextMenuCallback+8bb`, and nine
   more. Those offsets move whenever the method's IL changes.
2. **The stolen instructions no longer exist.** Each script also hardcoded the
   bytes it displaced, e.g. `db 41 89 46 3C 49 63 46 40` for `SellItems`. Those
   bytes encode both the field offset *and* the register the JIT happened to pick.
   In 1.0.3 several of the hooked methods have different bodies, so scanning for
   the old byte patterns would not have found them either.

So the port replaces the hooking strategy rather than patching numbers.

## What the new table does instead

Every hook now attaches to the **entry point** of a Mono method and captures the
object from the **calling convention**, which is fixed by the Windows x64 ABI and
does not depend on what the JIT emitted:

| Argument slot | Location at the first instruction |
|---|---|
| 1st (`this` for instance methods) | `RCX` |
| 2nd / 3rd / 4th | `RDX` / `R8` / `R9` |
| 5th and later | `[rsp+28]`, `[rsp+30]`, `[rsp+38]`, … |

The displaced prologue bytes are no longer hardcoded. A small Lua runtime in the
"Activate me" script measures them at hook time:

```
qmHook(id, method, capture)   -- resolve -> measure prologue -> build the cave
qmUnhook(id)                  -- write the saved bytes back, free the cave
```

`qmHook` walks instruction boundaries with `getInstructionSize` (falling back to
parsing `disassemble` output) until it has at least 5 bytes, copies those exact
bytes into the code cave, and pads the entry with `nop`. It refuses to hook a
method that begins with a relative branch, and raises a readable error instead of
silently doing nothing when a symbol will not resolve.

The consequence: **the table should survive future patches that only move code**.
It needs revisiting only when a *field layout* or a *method signature* changes.

## Hook map

| Group | Method | Captured |
|---|---|---|
| Weapon – Mission | `MGSC.WeaponComponent:SpendAmmo` | `RCX` = WeaponComponent |
| Weapon – Ship | `MGSC.ScreenWithShipCargo:DragControllerShowContextMenuCallback` | `RDX` = ItemSlot |
| Items | `MGSC.PickupItem:get_IsStackable` | `RCX` = PickupItem |
| Backpack | `MGSC.Inventory:ResizeBackpack` | `RCX` = Inventory |
| Durability | `MGSC.BreakableItemComponent:get_Durability` | `RCX` = BreakableItemComponent |
| Player Stats | `MGSC.StarvationEffect:set_CurrentLevel` | `RCX` = StarvationEffect |
| Perks / XP | `MGSC.Perk:AddExp` | `RCX` = Perk |
| QuasiLvL | `MGSC.QmorphosController:ProcessActionPoint` | `RCX` = QmorphosController |
| Travel | `MGSC.TravelSystem:ProcessSpaceshipTravel` | `[rsp+38]` = TravelMetadata |
| Faction Stats | `MGSC.TradeSystem:BuyStationItems` | `RDX` = Factions |
| Faction Stats (sell) | `MGSC.TradeSystem:SellItems` | `RDX` = Factions |

All 11 target classes and methods still exist in 1.0.3 under the same names.

## Offset changes, 0.9.87 → 1.0.3

Offsets are Mono runtime field offsets, computed from `Assembly-CSharp.dll`
metadata by `trainer_tools/` and cross-checked against the offsets the 0.9.87
table is known to have used.

| Class | Field | 0.9.87 | 1.0.3 |
|---|---|---:|---:|
| `BreakableItemComponent` | CurrentPercent | `18` | **`10`** |
| | MaxDurability | `20` | **`18`** |
| | Unbreakable | `28` | **`20`** |
| `Perk` | CurrentExp / ExpPerAction / MaxExp | `2C`/`30`/`34` | **`34`/`38`/`3C`** |
| `StarvationEffect` | _currentLevel / MaxLevel | `40`/`44` | **`48`/`4C`** |
| `Inventory` | _backpackMode | `D0` | **`B4`** |
| `Faction` | Power … AllTimeTradingPoints | `28`…`40` | **`18`…`30`** |
| `WeaponRecord` | Range / ReloadDuration / MagazineCapacity | `AC`/`B0`/`B8` | **`B0`/`B8`/`BC`** |
| `QmorphosController` | _raidMetadata | `28` | **`30`** |
| `RaidMetadata` | QMorphosLevel | `40` | **`2C`** |
| | QMorphosMinLevel | `48` | **`34`** |
| | IsBaronAllowed / IsGlobalJammed | `4D`/`4E` | **`50`/`51`** |
| | WinCondition | `20` | **`40`** |
| `TravelMetadata` | TravelHoursDuration | `80` | **`90`** |
| | InitialTravelDistance | `70` | **`78`** |

Unchanged, and therefore evidence the layout model is right:

| Class | Field | Offset |
|---|---|---:|
| `WeaponComponent` | CurrentAmmo / _weaponRecord | `48` / `28` |
| `PickupItem` | _stackable / _usable | `40` / `48` |
| `StackableItemComponent` | Count / Max | `10` / `12` |
| `UsableItemComponent` | MaxUsageValue / UsageCost / CurrentUsageValue | `10` / `14` / `18` |
| `MissionWinCondition` | EvacuationBlocked / ByItem / Flee | `32` / `33` / `34` |
| `TravelMetadata` | FlightTime | `48` |

## Behaviour changes

- **Faction Stats is now indexed.** 0.9.87 captured the single `Faction` local
  that the trade code happened to be holding. That local cannot be located
  statically, so the table now captures the `Factions` collection passed in `RDX`
  and walks `Factions+18 → List<Faction> → +10 → array → +20+8*i`. Eight faction
  slots are exposed, plus the live faction count. Buying **or** selling populates
  the same tree — the old table had two duplicate copies.
- **Weapon – Ship** reads the item's shared `WeaponRecord` through
  `ItemSlot+128 → item → _records[0]`, so Range / ReloadDuration /
  MagazineCapacity are editable from the ship. `CurrentAmmo` is per-instance and
  stays on the mission hook. Right-clicking a non-weapon shows meaningless
  numbers — that hazard existed in 0.9.87 too.
- **Travel** hooks `ProcessSpaceshipTravel` rather than `StartSpaceshipTravel`.
  It runs every tick of a flight, so the values populate and stay live.
  0.9.87 also silently dropped the game's own write to `TravelHoursDuration`
  (its trampoline never re-executed the stolen `movsd`); the new hook preserves
  the original instruction, so nothing is skipped.
- **`IsInvisible` was removed.** No such field exists on `Creature` or
  `CreatureData` in 1.0.3. The entry is gone rather than pointing at garbage.
- Extra fields exposed where they were free: `Falloff`, `ThrowRange`,
  `BonusAccuracy`, `MaxPenaltyPercent`, `MinDurabilityAfterRepair`,
  `CurrentMaxUsageValue`, `ItemsWeight`, `BaseHealth`, `BaseActionPoints`,
  `Health Min`, `TurnNumber`, `EvacuationInProgress`, `EvacuationCompleted`,
  `IsInfiniteAmmo`, `BramfaturaCounter`, `CanTravel`, faction count.

## Bugs carried over from 0.9.87, now fixed

- Both weapon scripts declared the **same** symbols `weapon3` / `iweapon3`;
  enabling ship and mission weapons together collided. Pointer symbols are now
  allocated once in "Activate me" and each hook has its own cave name.
- `[DISABLE]` of the Buy, Sell and Perk scripts freed `ihunger3` — a copy-paste
  from the hunger script — leaking their own symbols and double-freeing another's.
- The header still said `Activate me ! Quasimorph 0.9.87`.

## Verification

Static — run from `trainer_tools/`, all passing:

```bash
python gen_ct.py        # regenerate the table from the offset map
python validate_ct.py   # XML, unique IDs, symbol balance, offsets, hook pairing
python test_lua.py      # executes the table's Lua against a stubbed CE API
```

`test_lua.py` runs `qmHook`/`qmUnhook` for real and asserts on the assembler they
emit: cave allocated near the target, capture line first, all stolen bytes copied
verbatim, entry padded to the measured length, `dealloc` on unhook, and both
error paths.

**In-game: UNTESTED.** Cheat Engine is not installed on the build machine, so
nothing here has been attached to a running `Quasimorph.exe`. That pass is still
required:

1. Load the table, run the game, enable **1) Activate me ! Quasimorph 1.0.3**.
2. Load a save, then enable each group and confirm its values populate on the
   documented trigger:

   | Group | Trigger |
   |---|---|
   | Weapon – Mission | fire a weapon |
   | Weapon – Ship | right-click a weapon in ship cargo |
   | Items | right-click any item |
   | Backpack | open a backpack / resize |
   | Durability | hover a damaged item |
   | Player Stats | move in a mission |
   | Perks / XP | gain any XP |
   | QuasiLvL | move in a mission |
   | Travel | start a flight |
   | Faction Stats | buy or sell at a station |

3. Toggle every script **off** and confirm the game keeps running — that
   exercises `qmUnhook`'s byte restore.
4. If a script errors, the message names the method that failed to resolve.
   Re-run `python inspect_types.py method <Class>:<Method>` to see whether the
   signature moved.

## When the game updates again

The hooks should survive code movement on their own. Field offsets will not.
See `trainer_tools/README.md` — the loop is: re-run `inspect_types.py layout` for
the classes in the table above, edit the offset map at the bottom of `gen_ct.py`,
regenerate, revalidate.
