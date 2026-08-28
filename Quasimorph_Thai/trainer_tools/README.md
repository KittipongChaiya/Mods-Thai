# trainer_tools

Static analysis of Quasimorph's `Assembly-CSharp.dll`, and the generator for
`../Quasimorph-v1.0.3-1.CT`. Pure Python 3 + stdlib; `test_lua.py` also needs
`lupa`. Nothing here launches or attaches to the game.

The point of these tools is that the cheat table's offsets are **derived and
checked**, not copied from a forum post and hoped for.

## Files

| File | What it is |
|---|---|
| `cli_meta.py` | ECMA-335 metadata reader — PE → CLI header → `#~`/`#Strings`/`#Blob` streams → all 45 metadata tables |
| `monolayout.py` | Mono x64 object-layout model on top of it: resolves base classes across assemblies and computes runtime field offsets |
| `il.py` | Minimal CIL reader — method bodies, opcode walk, field/method token resolution |
| `inspect_types.py` | CLI over the above: `layout`, `method`, `il`, `grep` |
| `gen_ct.py` | Builds the `.CT`. **The offset map lives at the bottom of this file** |
| `validate_ct.py` | Structural checks on the generated table |
| `test_lua.py` | Runs the table's own Lua against a stubbed Cheat Engine API |

## Usage

```bash
python inspect_types.py layout Faction WeaponRecord RaidMetadata
python inspect_types.py method TradeSystem:BuyStationItems
python inspect_types.py il BreakableItemComponent:get_Durability
python inspect_types.py grep Evacuation
```

`--game <path>` points at another install; it defaults to
`C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game`.

`layout` prints the offset every Cheat Engine pointer chain needs:

```
MGSC.Faction   instance_size=0x90   base=System.Object
   0x010  Faction::Id                      string
   0x018  Faction::Power                   int
   0x01C  Faction::CurrentTechLevel        int
```

`method` prints the entry-point register map, which is what the hooks capture:

```
   entry-point register map (Windows x64):
        RCX        = stations       : Stations
        RDX        = factions       : Factions
        [rsp+38]   = travelData     : TravelMetadata
```

## Rebuilding the table

```bash
python gen_ct.py && python validate_ct.py && python test_lua.py
```

## Porting to a newer game version

1. Point `--game` at the new install and re-run `inspect_types.py layout` for
   every class listed in the offset table in `../TRAINER_NOTES.md`.
2. Diff against that table. Only classes whose fields actually moved need edits.
3. Update the offsets in the group definitions at the bottom of `gen_ct.py`.
4. Regenerate and re-run both checks.
5. Verify in-game — the static checks cannot prove the hooks fire.

The hooks themselves attach to method entry points and read arguments from the
Windows x64 calling convention, so code movement alone should not break them.
What does break them: a hooked method being renamed, removed, or having a
parameter inserted before the one being captured. `inspect_types.py method`
shows all three.

## How the layout model was validated

Mono lays out `auto`-layout classes in declaration order after a 16-byte
`MonoObject` header. The model reproduces, from 1.0.3 metadata alone, the exact
field *spacing* the 0.9.87 table depended on, across six independent classes —
including `WeaponComponent` (`CurrentAmmo` at `48`, `_weaponRecord` at `28`) and
`MissionWinCondition` (evacuation flags at `32`/`33`/`34`), which match the old
table byte for byte. Where offsets differ, the whole run shifts by a constant
because a field was added or removed, which is what a real version change looks
like.

`monolayout.py` also implements Mono's GC-aware two-pass layout as `model='gc'`.
It produced identical results for every class in the table, so `decl` is used.
