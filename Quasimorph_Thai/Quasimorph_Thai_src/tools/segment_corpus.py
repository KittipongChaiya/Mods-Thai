#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Segment the v1.2 bsdiff patch into the ordered Thai cells it inserted.

The shipped patch encodes old(0.9.9 resources.assets) -> new(0.9.9 patched).
We do not have `old`, so add-runs are opaque. But copy-runs are literal bytes
lifted straight from the patch's `extra` block, and those literals are the Thai
translation. Walking the control stream therefore yields the Thai text in
0.9.9 table order, split by the opaque spans that carried the keys and the ten
other language columns.

Long opaque spans are row boundaries (a row tail is ten language cells, so
hundreds of bytes). Short opaque spans sit *inside* a Thai cell, where bsdiff
happened to match a few ASCII bytes (markup, placeholders, digits, spaces).
"""
from __future__ import annotations

import bz2
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

# An opaque span at least this long is treated as a row boundary rather than a
# gap inside a single translated cell. Row tails carry ten language columns;
# intra-cell matches are markup fragments a few bytes wide.
ROW_BOUNDARY_MIN_BYTES = 40

GAP_PLACEHOLDER = "�"


def decode_int64(block: bytes) -> int:
    value = block[7] & 0x7F
    for index in range(6, -1, -1):
        value = value * 256 + block[index]
    return -value if block[7] & 0x80 else value


@dataclass
class Patch:
    control: list[tuple[int, int, int]]
    diff: bytes
    extra: bytes
    new_size: int


def load_patch(path: Path) -> Patch:
    raw = path.read_bytes()
    if raw[:8] != b"BSDIFF40":
        raise SystemExit(f"not a BSDIFF40 patch: {path}")
    control_len = decode_int64(raw[8:16])
    diff_len = decode_int64(raw[16:24])
    new_size = decode_int64(raw[24:32])

    control_start = 32
    diff_start = control_start + control_len
    extra_start = diff_start + diff_len

    control_raw = bz2.decompress(raw[control_start:diff_start])
    diff = bz2.decompress(raw[diff_start:extra_start])
    extra = bz2.decompress(raw[extra_start:])

    control = [
        (
            decode_int64(control_raw[off:off + 8]),
            decode_int64(control_raw[off + 8:off + 16]),
            decode_int64(control_raw[off + 16:off + 24]),
        )
        for off in range(0, len(control_raw), 24)
    ]
    return Patch(control=control, diff=diff, extra=extra, new_size=new_size)


@dataclass
class Cell:
    """One translated cell, as recovered from the patch."""

    index: int
    new_offset: int
    text: str
    gap_bytes: int = 0
    dirty_gap: bool = False
    tail_len: int = 0
    parts: list[str] = field(default_factory=list)

    @property
    def complete(self) -> bool:
        return self.gap_bytes == 0


def segment(patch: Patch) -> tuple[list[Cell], int]:
    """Walk the control stream and group literal runs into cells."""
    cells: list[Cell] = []
    literal_parts: list[bytes] = []
    gap_bytes = 0
    dirty_gap = False
    cell_offset = -1
    region_start = -1

    new_pos = 0
    diff_pos = 0
    extra_pos = 0

    def flush(tail_len: int) -> None:
        nonlocal literal_parts, gap_bytes, dirty_gap, cell_offset
        if literal_parts:
            joined = (GAP_PLACEHOLDER.encode("utf-8")).join(literal_parts)
            cells.append(
                Cell(
                    index=len(cells),
                    new_offset=cell_offset,
                    text=joined.decode("utf-8", "replace"),
                    gap_bytes=gap_bytes,
                    dirty_gap=dirty_gap,
                    tail_len=tail_len,
                    parts=[p.decode("utf-8", "replace") for p in literal_parts],
                )
            )
        literal_parts = []
        gap_bytes = 0
        dirty_gap = False
        cell_offset = -1

    for add_len, copy_len, _seek in patch.control:
        if add_len:
            # Opaque span: old bytes plus a delta we cannot invert without `old`.
            if literal_parts:
                if add_len >= ROW_BOUNDARY_MIN_BYTES:
                    flush(add_len)
                else:
                    gap_bytes += add_len
                    if any(patch.diff[diff_pos:diff_pos + add_len]):
                        dirty_gap = True
            new_pos += add_len
            diff_pos += add_len

        if copy_len:
            if region_start < 0:
                region_start = new_pos
            if cell_offset < 0:
                cell_offset = new_pos
            literal_parts.append(patch.extra[extra_pos:extra_pos + copy_len])
            new_pos += copy_len
            extra_pos += copy_len

    flush(0)
    return cells, region_start


def main() -> int:
    patch_path = Path(sys.argv[1])
    out_path = Path(sys.argv[2]) if len(sys.argv) > 2 else None

    patch = load_patch(patch_path)
    cells, region_start = segment(patch)

    total_gap = sum(c.gap_bytes for c in cells)
    dirty = [c for c in cells if c.dirty_gap]
    clean = [c for c in cells if c.complete]

    print(f"control triples      : {len(patch.control)}")
    print(f"extra (literal) bytes: {len(patch.extra)}")
    print(f"region starts at new : {region_start}")
    print()
    print(f"cells recovered      : {len(cells)}")
    print(f"  fully literal      : {len(clean)}  ({len(clean)/len(cells)*100:.1f}%)")
    print(f"  with opaque gaps   : {len(cells)-len(clean)}")
    print(f"  gaps carrying delta: {len(dirty)}   <- unrecoverable without `old`")
    print(f"  total gap bytes    : {total_gap}")

    lengths = sorted(len(c.text) for c in cells)
    print(f"\ncell length (chars)  : min={lengths[0]} p50={lengths[len(lengths)//2]} "
          f"p90={lengths[int(len(lengths)*0.9)]} max={lengths[-1]}")

    tails = [c.tail_len for c in cells if c.tail_len]
    tails.sort()
    if tails:
        print(f"row-tail span (bytes): min={tails[0]} p50={tails[len(tails)//2]} "
              f"p90={tails[int(len(tails)*0.9)]} max={tails[-1]}")

    print("\n--- first 40 recovered cells ---")
    for cell in cells[:40]:
        flag = "" if cell.complete else f"  [gap {cell.gap_bytes}B{' DELTA' if cell.dirty_gap else ''}]"
        print(f"{cell.index:6d}  tail={cell.tail_len:<5d} {cell.text!r}{flag}")

    if out_path:
        out_path.write_text(
            json.dumps(
                [
                    {
                        "index": c.index,
                        "new_offset": c.new_offset,
                        "text": c.text,
                        "gap_bytes": c.gap_bytes,
                        "dirty_gap": c.dirty_gap,
                        "tail_len": c.tail_len,
                    }
                    for c in cells
                ],
                ensure_ascii=False,
            ),
            encoding="utf-8",
        )
        print(f"\nwrote {len(cells)} cells to {out_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
