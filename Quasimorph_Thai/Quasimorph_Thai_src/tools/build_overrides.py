#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Build the shipped Thai override table from translations/*.json.

The mod merges these over the game's own localization table at runtime, so this
file carries only "key TAB thai" - never the other ten language columns, and
never rows we have not translated. That is what lets one build keep working
after a game update: anything the game adds simply stays English.

    python build_overrides.py <out.tsv> [--base work/localization_base.tsv] [--gzip]

Passing --base cross-checks the keys against a known table and reports which of
them that table does not contain. That is advisory only: a key missing from the
base is still shipped, because the player's game version may well have it.
"""
from __future__ import annotations

import argparse
import gzip
import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
TRANSLATIONS = PROJECT / "translations"


def load_translations() -> dict[str, str]:
    merged: dict[str, str] = {}
    collisions: list[str] = []
    for path in sorted(TRANSLATIONS.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8-sig"))
        for key, value in data.items():
            if key in merged and merged[key] != value:
                collisions.append(f"{key} ({path.name})")
            merged[key] = value
    if collisions:
        print(f"WARNING: {len(collisions)} key(s) translated twice with different text, "
              f"e.g. {collisions[:3]}", file=sys.stderr)
    return merged


def validate(entries: dict[str, str]) -> list[str]:
    """A tab or newline in a cell would corrupt the merged table at runtime."""
    problems = []
    for key, value in entries.items():
        if "\t" in key or "\n" in key or "\r" in key:
            problems.append(f"{key!r}: key contains a tab or line break")
        if "\t" in value:
            problems.append(f"{key}: value contains a TAB")
        if "\n" in value or "\r" in value:
            problems.append(f"{key}: value contains a line break")
    return problems


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("out")
    parser.add_argument("--base", help="localization table to cross-check keys against")
    parser.add_argument("--gzip", action="store_true", help="also write <out>.gz")
    args = parser.parse_args()

    entries = load_translations()
    if not entries:
        print("no translations found", file=sys.stderr)
        return 1

    problems = validate(entries)
    if problems:
        print(f"REFUSING TO BUILD - {len(problems)} malformed entr(ies):", file=sys.stderr)
        for line in problems[:20]:
            print("  " + line, file=sys.stderr)
        return 1

    payload = "\n".join(f"{key}\t{value}" for key, value in sorted(entries.items()))
    payload = (payload + "\n").encode("utf-8")

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(payload)
    print(f"entries    : {len(entries)}")
    print(f"wrote      : {out} ({len(payload)} bytes)")

    if args.gzip:
        gz = out.with_suffix(out.suffix + ".gz")
        gz.write_bytes(gzip.compress(payload, 9))
        print(f"gzip       : {gz} ({gz.stat().st_size} bytes)")

    if args.base:
        base_text = Path(args.base).read_bytes().decode("utf-8", "surrogateescape")
        base_keys = {line.split("\t", 1)[0] for line in base_text.split("\r\n") if line}
        unknown = sorted(set(entries) - base_keys)
        covered = len(base_keys & set(entries))
        print(f"coverage   : {covered}/{len(base_keys) - 1} rows of {Path(args.base).name}")
        if unknown:
            print(f"NOTE       : {len(unknown)} key(s) are not in that table, e.g. "
                  f"{unknown[:3]} - shipped anyway, the player's version may have them")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
