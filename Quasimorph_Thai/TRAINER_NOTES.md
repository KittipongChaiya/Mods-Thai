# Quasimorph Cheat Table — 0.9.87 → 1.0.3

**Table**: `Quasimorph-v1.0.3-1.CT` (Cheat Engine 7.x, table version 46)
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Predecessor**: `Quasimorph-v0.9.87-1.CT` — kept unchanged as a fallback
**Status**: hooks and offsets verified against the running game · in-game use confirmed by the maintainer
**Updated**: 2026-08-29

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
   bytes encode both a field offset *and* the register the JIT happened to pick.
   In 1.0.3 several hooked methods have different bodies, so scanning for the old
   byte patterns would not have found them either.

So the port replaces the hooking strategy rather than patching numbers.

## What the new table does instead

Every hook attaches to the **entry point** of a Mono method and captures the
object from the **calling convention**, which is fixed by the Windows x64 ABI and
does not depend on what the JIT emitted:

| Argument slot | Location at the first instruction |
|---|---|
| 1st (`this` for instance methods) | `RCX` |
| 2nd / 3rd / 4th | `RDX` / `R8` / `R9` |
| 5th and later | `[rsp+28]`, `[rsp+30]`, `[rsp+38]`, … |

The displaced prologue bytes are not hardcoded. A small Lua runtime in the
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

Measured prologues vary from 8 to 11 bytes across the eleven targets — e.g.
`48 83 EC 18 48 89 3C 24` for `SpendAmmo`, `55 48 8B EC 48 81 EC E0 00 00 00`
for `DragControllerShowContextMenuCallback` — which is exactly why they are not
hardcoded.

**Captures go through RAX.** x86-64 encodes a store to an absolute 64-bit
address only for the accumulator (`mov [moffs64],rax`). `mov [pWeapon],rcx` has
no encoding at the distance CE places the pointer block, and CE's assembler does
not fall back to RIP-relative. Every capture is therefore
`push rax / mov rax,<src> / mov [pX],rax / pop rax`.

The consequence: **the table should survive future patches that only move code**.
It needs revisiting when a *field layout* or a *method signature* changes.

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

All eleven resolve in the running game.

## Field offsets

**Mono does not lay classes out in declaration order.** For `auto`-layout
classes it makes two passes — reference-bearing fields first, then value types —
so any class that interleaves the two has a layout that field order alone does
not predict. This is the single biggest trap in this table: an offset map built
from declaration order was wrong for 38 of 69 checked fields, including every
offset in `Faction`, `CreatureData`, `Inventory`, `RaidMetadata`,
`TravelMetadata`, `WeaponRecord` and `ItemSlot`.

Offsets now come from `mono_class_enumFields` in the **live process**
(`trainer_tools/offsets.json`), which is what the JIT actually uses.
`monolayout.py`'s `gc` model reproduces that dump exactly — 486 of 486 instance
fields across 24 classes — and `gen_ct.py` refuses to build if the two disagree.
No offset in the generator is typed by hand; each is `OFF('Faction', 'Power')`.

Selected changes, 0.9.87 → 1.0.3:

| Class | Field | 0.9.87 | 1.0.3 |
|---|---|---:|---:|
| `WeaponComponent` | CurrentAmmo | `48` | **`5C`** |
| | _weaponRecord | `28` | `28` |
| `WeaponRecord` | Range / ReloadDuration / MagazineCapacity | `AC`/`B0`/`B8` | **`C4`/`CC`/`D0`** |
| `ItemSlot` | Item | — | `F8` |
| `PickupItem` | _stackable / _usable | `40`/`48` | `40`/`48` |
| `StackableItemComponent` | Count / Max | `10`/`12` | `10`/`12` |
| `UsableItemComponent` | MaxUsageValue / UsageCost / CurrentUsageValue | `10`/`14`/`18` | `10`/`14`/`18` |
| `Inventory` | _backpackMode | `D0` | **`E8`** |
| `BreakableItemComponent` | CurrentPercent / MaxDurability / Unbreakable | `18`/`20`/`28` | **`10`/`18`/`20`** |
| `StarvationEffect` | _currentLevel / MaxLevel | `40`/`44` | **`48`/`4C`** |
| `Creature` | CreatureData | — | `28` |
| `CreatureData` | Health / Inventory | — | `48` / `58` |
| | BaseLosLevel … BaseDodge | `F8`…`104` | **`104`…`110`** |
| `HealthInfo` | MaxValue / _value / _invulnerability | `20`/`28`/`2C` | **`24`/`2C`/`30`** |
| `Perk` | CurrentExp / ExpPerAction / MaxExp | `2C`/`30`/`34` | **`34`/`38`/`3C`** |
| `QmorphosController` | _raidMetadata | `28` | **`30`** |
| `RaidMetadata` | QMorphosLevel / QMorphosMinLevel | `40`/`48` | **`60`/`68`** |
| | WinCondition | `20` | `20` |
| | IsBaronAllowed / IsGlobalJammed | `4D`/`4E` | **`6D`/`6E`** |
| `MissionWinCondition` | EvacuationBlocked / ByItem / Flee | `32`/`33`/`34` | `32`/`33`/`34` |
| `TravelMetadata` | TravelHoursDuration / FlightTime | `80`/`48` | **`98`/`60`** |
| `Faction` | Power … AllTimeTradingPoints | `28`…`40` | **`48`…`60`** |
| `Factions` | Values | — | `18` |

## Behaviour changes

- **Faction Stats is now indexed.** 0.9.87 captured the single `Faction` local
  the trade code happened to be holding. That local cannot be located
  statically, so the table captures the `Factions` collection from `RDX` and
  walks `Values → List._items → array[i]`. Eight faction slots are exposed, plus
  the live faction count. Buying **or** selling fills the same tree — the old
  table had two duplicate copies.
- **Weapon – Ship** reads the item's shared `WeaponRecord` through
  `ItemSlot → Item → _records[0]`, so Range / ReloadDuration / MagazineCapacity
  are editable from the ship. `CurrentAmmo` is per-instance and stays on the
  mission hook. Right-clicking a non-weapon shows meaningless numbers — that
  hazard existed in 0.9.87 too.
- **Travel** hooks `ProcessSpaceshipTravel` rather than `StartSpaceshipTravel`.
  It runs every tick of a flight, so the values populate and stay live. 0.9.87
  also silently dropped the game's own write to `TravelHoursDuration` (its
  trampoline never re-executed the stolen `movsd`); the new hook preserves the
  original instruction.
- **`IsInvisible` was removed** — no such field exists on `Creature` or
  `CreatureData` in 1.0.3. `IsInfiniteAmmo` is exposed instead.
- Extra fields where they were free: `Falloff`, `ThrowRange`, `BonusAccuracy`,
  `MaxPenaltyPercent`, `MinDurabilityAfterRepair`, `CurrentMaxUsageValue`,
  `ItemsWeight`, `BaseHealth`, `BaseActionPoints`, `Health Min`, `TurnNumber`,
  `EvacuationInProgress`, `EvacuationCompleted`, `BramfaturaCounter`,
  `CanTravel`, `Price`, `Weight`, faction count.

## Bugs carried over from 0.9.87, now fixed

- Both weapon scripts declared the **same** symbols `weapon3` / `iweapon3`;
  enabling ship and mission weapons together collided. Pointer symbols are now
  allocated once in "Activate me" and each hook has its own cave name.
- `[DISABLE]` of the Buy, Sell and Perk scripts freed `ihunger3` — a copy-paste
  from the hunger script — leaking their own symbols and double-freeing another's.
- The header still said `Activate me ! Quasimorph 0.9.87`.

## Verification

**Against the running game** (`trainer_tools/ce_selftest.py`, CE 7.7, game
`1.0.3.578s.024ad60`):

- all 10 pointer symbols allocate and resolve
- all 11 Mono method symbols resolve
- all 11 hooks measure their prologue, assemble, install (`E9` at the entry),
  unhook, and restore the first 24 bytes **byte-identically** — 36/36 checks
- the game stays alive and responding with all 11 hooks installed
- 69 offsets checked against `mono_class_enumFields`; the shipped map matches

**Static** (`trainer_tools/`): `gen_ct.py` cross-checks 486 fields against the
runtime dump, `validate_ct.py` runs 48 structural checks, and `test_lua.py`
executes the table's own Lua against a stubbed CE API and asserts on the
assembler it emits (16 checks).

**Normal play**: confirmed working by the maintainer. The automated
`ce_selftest.py monitor` run recorded 0 of 119 entries, because its watch window
elapsed before the game was played — it is not evidence either way. To capture
live numbers, run `python ce_selftest.py monitor --run` and play during the
window.

## When the game updates again

The hooks should survive code movement on their own. Field offsets will not.

```bash
python ce_selftest.py dump --run     # with the new build running
# refresh offsets.json from the dump, then
python gen_ct.py && python validate_ct.py && python test_lua.py
python ce_selftest.py hooks --run
```

Do not skip the in-game step. Both defects found after the "statically verified"
build — ten hooks that could not assemble, and 38 wrong offsets — were invisible
to static analysis. See `trainer_tools/README.md`.
