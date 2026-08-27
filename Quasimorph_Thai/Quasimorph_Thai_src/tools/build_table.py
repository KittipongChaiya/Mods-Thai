#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Merge Thai translations into the base localization table.

Thai goes into the English column (index 1). The game shows the language named
by the header's column-1 cell, so that cell becomes "ไทย" and the player picks
it where "English" used to be.

    python build_table.py <base.tsv> <out.tsv> [--translations f.json]... [--marker]

`--marker` is the smoke-test mode: every untranslated cell is prefixed with a
Thai marker so a launch immediately proves the resource hook, the TSV parse and
the Thai font are all working end to end.
"""
from __future__ import annotations

import argparse
import gzip
import json
import sys
from pathlib import Path

ENGLISH_COLUMN = 1
EXPECTED_COLUMNS = 18
LANGUAGE_NAME = "ไทย"
SMOKE_MARKER = "ไทย│"


def load_rows(path: Path) -> list[list[str]]:
    text = path.read_bytes().decode("utf-8", "surrogateescape")
    rows = [line.split("\t") for line in text.split("\r\n") if line]
    bad = [i for i, row in enumerate(rows) if len(row) != EXPECTED_COLUMNS]
    if bad:
        raise SystemExit(f"{path}: {len(bad)} row(s) do not have {EXPECTED_COLUMNS} columns "
                         f"(first at line {bad[0] + 1})")
    return rows


def load_translations(paths: list[Path]) -> dict[str, str]:
    merged: dict[str, str] = {}
    for path in paths:
        data = json.loads(path.read_text(encoding="utf-8"))
        for key, value in data.items():
            if not isinstance(value, str):
                raise SystemExit(f"{path}: value for {key!r} is not a string")
            merged[key] = value
    return merged


def validate(key: str, value: str) -> str | None:
    """A tab or newline inside a cell would silently shift every later column."""
    if "\t" in value:
        return "contains a tab"
    if "\n" in value or "\r" in value:
        return "contains a line break"
    return None


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("base")
    parser.add_argument("out")
    parser.add_argument("--translations", action="append", default=[])
    parser.add_argument("--marker", action="store_true",
                        help="prefix untranslated cells with a Thai marker (smoke test)")
    parser.add_argument("--gzip", action="store_true", help="also write <out>.gz")
    args = parser.parse_args()

    rows = load_rows(Path(args.base))
    translations = load_translations([Path(p) for p in args.translations])

    header, data_rows = rows[0], rows[1:]
    header[ENGLISH_COLUMN] = LANGUAGE_NAME

    translated = 0
    untranslated = 0
    rejected: list[str] = []
    unused = set(translations)

    for row in data_rows:
        key = row[0]
        english = row[ENGLISH_COLUMN]
        unused.discard(key)

        thai = translations.get(key)
        if thai is not None:
            problem = validate(key, thai)
            if problem:
                rejected.append(f"{key}: {problem}")
                continue
            row[ENGLISH_COLUMN] = thai
            translated += 1
            continue

        untranslated += 1
        if args.marker and english:
            row[ENGLISH_COLUMN] = SMOKE_MARKER + english

    if rejected:
        print(f"REJECTED {len(rejected)} translation(s):", file=sys.stderr)
        for line in rejected[:20]:
            print("  " + line, file=sys.stderr)
        return 1

    payload = "\r\n".join("\t".join(row) for row in rows).encode("utf-8", "surrogateescape")
    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_bytes(payload)

    if args.gzip:
        gz = out.with_suffix(out.suffix + ".gz")
        gz.write_bytes(gzip.compress(payload, 9))
        print(f"gzip       : {gz} ({gz.stat().st_size} bytes)")

    total = translated + untranslated
    print(f"rows       : {len(rows)} (1 header + {len(data_rows)} data)")
    print(f"translated : {translated}/{total} ({translated / total * 100:.1f}%)")
    print(f"remaining  : {untranslated}")
    if unused:
        print(f"WARNING    : {len(unused)} translation key(s) match no row, e.g. "
              f"{sorted(unused)[:3]}", file=sys.stderr)
    print(f"wrote      : {out} ({len(payload)} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
