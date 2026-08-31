#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Report translation inconsistency across the whole table.

Two directions, and they are not equally serious:

  * one English string rendered as several different Thai strings - almost
    always a defect. The player sees the same game concept under two names, and
    it is how `Duggur` ended up as both ดักกูร์ and ดุกกูร์, and how one
    `Volcano` cell kept the untranslated word `Vulcan`.

  * one Thai string used for several different English strings - usually fine
    (English distinguishes "Close" the verb from "Close" the adjective where
    Thai does not), occasionally a real collision worth a look.

Not every divergence is a defect. `hit` is deliberately ตะปบ for a claw, ต่อย
for a punch and ทุบ for a bludgeon; `Max` is the UI's สูงสุด in one cell and the
name of Maximilian Rohr in another. Those are good translation, not drift. So
reviewed exceptions are recorded in `consistency_allow.json` with the exact set
of renderings they may take and the reason - and --strict fails only on
divergence nobody has signed off, including a *new* rendering appearing under an
already-allowed English string.

    python consistency.py                # report both, exit 0
    python consistency.py --strict       # exit 1 on unreviewed divergence
    python consistency.py --json out.json
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

from _corpus import PROJECT, load_base, load_translations, prefix_of

ALLOW = PROJECT / "consistency_allow.json"


def load_allow() -> dict[str, dict]:
    if not ALLOW.exists():
        return {}
    return json.loads(ALLOW.read_text(encoding="utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--strict", action="store_true",
                        help="fail on divergence that consistency_allow.json does not cover")
    parser.add_argument("--json", help="also write the findings as JSON")
    parser.add_argument("--limit", type=int, default=40, help="how many groups to print")
    args = parser.parse_args()

    base = load_base()
    thai, _ = load_translations()

    by_english: dict[str, dict[str, list[str]]] = defaultdict(lambda: defaultdict(list))
    by_thai: dict[str, set[str]] = defaultdict(set)
    for key, value in thai.items():
        english = base.get(key, "")
        if not english.strip():
            continue
        by_english[english][value].append(key)
        by_thai[value].add(english)

    split = {en: v for en, v in by_english.items() if len(v) > 1}
    merged = {th: v for th, v in by_thai.items() if len(v) > 1}
    affected = sum(len(k) for v in split.values() for k in v.values())

    allow = load_allow()
    unreviewed: dict[str, dict[str, list[str]]] = {}
    drifted: dict[str, set[str]] = {}
    for english, variants in split.items():
        entry = allow.get(english)
        if entry is None:
            unreviewed[english] = variants
            continue
        # An allowed English string may still grow a rendering nobody approved.
        extra = set(variants) - set(entry.get("thai", []))
        if extra:
            drifted[english] = extra

    print(f"distinct English strings : {len(by_english)}")
    print(f"one English -> many Thai : {len(split)} strings, {affected} cells")
    print(f"  reviewed + allowed     : {len(split) - len(unreviewed)}")
    print(f"  unreviewed             : {len(unreviewed)}")
    print(f"  allowed but drifted    : {len(drifted)}")
    print(f"one Thai -> many English : {len(merged)} strings (usually fine)")

    if unreviewed:
        print("\n--- one English rendered several ways, not yet reviewed ---")
        ordered = sorted(unreviewed.items(), key=lambda kv: (len(kv[0]), kv[0]))
        for english, variants in ordered[:args.limit]:
            shown = english if len(english) <= 90 else english[:87] + "..."
            print(f"\n  EN  {shown}")
            for value, keys in sorted(variants.items()):
                where = f"{prefix_of(keys[0])} +{len(keys) - 1}" if len(keys) > 1 else keys[0]
                print(f"      -> {value}    [{len(keys)} cell(s): {where}]")
        if len(ordered) > args.limit:
            print(f"\n  ... and {len(ordered) - args.limit} more")

    if drifted:
        print("\n--- allowed, but with a rendering that is not in the allowlist ---")
        for english, extra in sorted(drifted.items()):
            print(f"  {english!r}: unexpected {sorted(extra)}")

    if args.json:
        payload = {
            "split": {en: dict(v) for en, v in split.items()},
            "unreviewed": {en: dict(v) for en, v in unreviewed.items()},
            "merged": {th: sorted(v) for th, v in merged.items()},
        }
        Path(args.json).write_text(
            json.dumps(payload, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"\nwrote {args.json}")

    if args.strict and (unreviewed or drifted):
        print(f"\nFAIL: {len(unreviewed)} unreviewed and {len(drifted)} drifted divergence(s).")
        print(f"      Resolve them, or record the deliberate ones in {ALLOW.name}.")
        return 1
    if args.strict:
        print("\nconsistent: every divergence is reviewed and allowed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
