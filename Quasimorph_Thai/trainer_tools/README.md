# trainer_tools

Static analysis of Quasimorph's `Assembly-CSharp.dll`, the generator for
`../Quasimorph-v1.0.3-1.CT`, and the harness that tests it against the running
game. Python 3 + stdlib; `test_lua.py` needs `lupa`, `ce_selftest.py` needs
Cheat Engine installed.

The point of these tools is that the table's offsets are **derived and
verified**, not copied from a forum post and hoped for.

## Files

| File | What it is |
|---|---|
| `cli_meta.py` | ECMA-335 metadata reader — PE → CLI header → `#~`/`#Strings`/`#Blob` streams → all 45 metadata tables |
| `monolayout.py` | Mono x64 object-layout model: resolves base classes across assemblies and computes runtime field offsets |
| `il.py` | Minimal CIL reader — method bodies, opcode walk, field/method token resolution |
| `inspect_types.py` | CLI over the above: `layout`, `method`, `il`, `grep` |
| `offsets.json` | **Ground truth.** Field offsets dumped from the live process with `mono_class_enumFields` |
| `gen_ct.py` | Builds the `.CT`. Offsets resolve through `OFF(class, field)` against `offsets.json` |
| `validate_ct.py` | Structural checks on the generated table |
| `test_lua.py` | Runs the table's own Lua against a stubbed Cheat Engine API |
| `ce_selftest.py` | Drives Cheat Engine against the running game: `hooks`, `dump`, `monitor` |

## Usage

```bash
python inspect_types.py layout Faction WeaponRecord RaidMetadata
python inspect_types.py method TradeSystem:BuyStationItems
python inspect_types.py il BreakableItemComponent:get_Durability
python inspect_types.py grep Evacuation
```

`--game <path>` points at another install; it defaults to
`C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game`.

`method` prints the entry-point register map, which is what the hooks capture:

```
   entry-point register map (Windows x64):
        RCX        = stations       : Stations
        RDX        = factions       : Factions
        [rsp+38]   = travelData     : TravelMetadata
```

### Rebuilding and checking

```bash
python gen_ct.py && python validate_ct.py && python test_lua.py
python ce_selftest.py hooks --run          # needs the game running
```

`ce_selftest.py` writes a temporary script into Cheat Engine's `autorun/`
folder, launches CE, waits for it to finish, prints the report and removes the
script again. Modes:

- `hooks` — resolve every Mono symbol, measure each prologue, install, confirm
  the `E9`, unhook, and compare the restored bytes
- `dump` — dump `mono_class_enumFields` for every class the table touches;
  this is what `offsets.json` is built from
- `monitor` — leave all hooks installed and evaluate every value entry as its
  pointer becomes non-zero. Play the game during the window (`--seconds`)

## Two traps, both found only in-game

**1. Mono does not use declaration order.** For `auto`-layout classes it lays
out reference-bearing fields first, then value types. Any class that interleaves
the two — `Faction`, `CreatureData`, `Inventory`, `RaidMetadata`,
`TravelMetadata`, `WeaponRecord`, `ItemSlot` — has a layout that field order
alone does not predict. A declaration-order map was wrong for 38 of 69 checked
fields.

`monolayout.py` implements both: `layout(..., 'gc')` is the correct one and
reproduces the runtime dump exactly (486/486 instance fields over 24 classes).
`'decl'` is kept only to make the difference visible. `gen_ct.py` cross-checks
the model against `offsets.json` on every build and refuses to emit a table if
they disagree.

**2. `mov [symbol],rcx` does not assemble.** x86-64 encodes a store to an
absolute 64-bit address only for the accumulator (`mov [moffs64],rax`), and CE
does not fall back to RIP-relative at the distance it places allocations. Ten of
eleven hooks silently failed to install. Every capture now routes through RAX.

Neither was visible to static analysis, and the table had already passed a full
static suite when both were shipped. **Always finish with `ce_selftest.py`.**

## Porting to a newer game version

1. Run the new build, then `python ce_selftest.py dump --run`.
2. Rebuild `offsets.json` from that dump.
3. `python gen_ct.py` — it fails loudly if a field the table needs is gone.
4. `python validate_ct.py && python test_lua.py`
5. `python ce_selftest.py hooks --run`
6. `python ce_selftest.py monitor --run` and play during the window.

The hooks attach to method entry points and read arguments from the Windows x64
calling convention, so code movement alone does not break them. What does: a
hooked method being renamed, removed, or gaining a parameter before the one
being captured. `inspect_types.py method` shows all three.
