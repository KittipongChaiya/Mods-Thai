#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verify every character in the translation can actually be drawn.

The shipped TMP asset carries a *static* atlas of 179 glyphs (ASCII + 84 Thai
characters), but it is a dynamic asset with the `tahoma` Font object embedded
beside it, so TMP rasterizes anything else on demand from tahoma.ttf. That makes
the TTF - not the atlas - the real boundary: a character tahoma.ttf does not
have cannot be drawn at all, and TMP renders nothing rather than complaining.

The translation already relies on this for `ฤ` (89 cells - ฤทธิ์, พฤหัสบดี),
`“` `”` (91 each), `ฯ`, `…`, `’`, `ö`, `ü`, `é`, `ì` and a Cyrillic `С`. All are
in tahoma.ttf, so all are fine. This check exists so that an editing pass cannot
quietly introduce one that is not - an en dash or a typographic prime picked up
from an English source would vanish on screen with no error anywhere.

    python check_font.py            # fail if any character is unrenderable
    python check_font.py --atlas    # also list what needs runtime rasterization

Needs fontTools. Without it the check reports that it was skipped and exits 0,
so it can sit in the build without becoming a hard dependency.
"""
from __future__ import annotations

import argparse
import sys
import unicodedata
from collections import Counter
from pathlib import Path

from _corpus import PROJECT, load_translations

TTF = PROJECT / "assets" / "tahoma.ttf"

#: The 179 glyphs baked into the shipped atlas: printable ASCII, plus the Thai
#: block minus ฤ ฦ ฯ. Anything outside this set is drawn only if runtime
#: rasterization works, which is why --atlas reports it.
STATIC_ATLAS = (set(range(0x20, 0x7F))
                | (set(range(0x0E01, 0x0E5C)) - {0x0E24, 0x0E26, 0x0E2F,
                                                 0x0E3B, 0x0E3C, 0x0E3D, 0x0E3E}))


def font_codepoints() -> set[int] | None:
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        return None
    with TTFont(TTF, lazy=True) as font:
        return set(font.getBestCmap())


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--atlas", action="store_true",
                        help="also list characters that need runtime rasterization")
    args = parser.parse_args()

    if not TTF.exists():
        print(f"SKIPPED: {TTF} not found")
        return 0

    available = font_codepoints()
    if available is None:
        print("SKIPPED: fontTools is not installed (pip install fonttools)")
        return 0

    thai, _ = load_translations()
    used: Counter[str] = Counter()
    for value in thai.values():
        used.update(value)

    missing = {ch: n for ch, n in used.items() if ord(ch) not in available}
    dynamic = {ch: n for ch, n in used.items()
               if ord(ch) in available and ord(ch) not in STATIC_ATLAS}

    print(f"tahoma.ttf   : {len(available)} codepoints")
    print(f"characters   : {len(used)} distinct, used across {len(thai)} cells")
    print(f"unrenderable : {len(missing)}")

    if args.atlas:
        print(f"\nnot in the static atlas - rasterized at runtime from tahoma.ttf "
              f"({len(dynamic)} characters):")
        for ch, count in sorted(dynamic.items(), key=lambda kv: -kv[1]):
            print(f"  U+{ord(ch):04X} {ch!r:6s} x{count:<6d} {name_of(ch)}")

    if missing:
        print(f"\nFAIL - {len(missing)} character(s) exist in no font we ship. "
              f"These draw as nothing in game:")
        for ch, count in sorted(missing.items(), key=lambda kv: -kv[1]):
            where = [k for k, v in thai.items() if ch in v][:3]
            print(f"  U+{ord(ch):04X} {ch!r:6s} x{count:<5d} {name_of(ch)}")
            for key in where:
                print(f"      {key}")
        return 1

    print("\nall characters are renderable")
    return 0


def name_of(ch: str) -> str:
    try:
        return unicodedata.name(ch)
    except ValueError:
        return "<unnamed>"


if __name__ == "__main__":
    raise SystemExit(main())
