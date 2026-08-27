#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Verify translations against their English source.

Catches the mistakes that actually break a localized build:
  * a dropped or renamed placeholder ({0}, %CREATURE%) - the game prints a raw
    token or throws on a format call
  * unbalanced rich-text tags - TMP swallows the rest of the label
  * a tab or newline in a cell - shifts every later column in the TSV
  * a key that is not in the table at all - silent no-op

    python check_translations.py
"""
from __future__ import annotations

import json
import re
import sys
from collections import Counter
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
BASE = PROJECT / "work" / "localization_base.tsv"
TRANSLATIONS = PROJECT / "translations"

BRACE = re.compile(r"\{\d+\}")
# Two token styles appear in the table: plain %CREATURE% and the resolver form
# %SFaction:LocalizeFaction% used by strategy/mission text.
PERCENT = re.compile(r"%[A-Za-z_][A-Za-z0-9_]*(?::[A-Za-z0-9_]+)?%")
TAG = re.compile(r"</?[a-zA-Z][^>]*>")
THAI = re.compile(r"[฀-๿]")


def load_base() -> dict[str, str]:
    text = BASE.read_bytes().decode("utf-8", "surrogateescape")
    rows = [line.split("\t") for line in text.split("\r\n") if line]
    return {row[0]: row[1] for row in rows[1:]}


def main() -> int:
    base = load_base()
    problems: list[str] = []
    seen: Counter[str] = Counter()
    checked = 0

    for path in sorted(TRANSLATIONS.glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        for key, thai in data.items():
            checked += 1
            seen[key] += 1
            where = f"{path.name}:{key}"

            if key not in base:
                problems.append(f"{where}: key not in the table")
                continue
            english = base[key]

            if "\t" in thai:
                problems.append(f"{where}: contains a TAB")
            if "\n" in thai or "\r" in thai:
                problems.append(f"{where}: contains a line break")

            for label, pattern in (("placeholder", BRACE), ("token", PERCENT)):
                want, got = Counter(pattern.findall(english)), Counter(pattern.findall(thai))
                if want != got:
                    missing = want - got
                    extra = got - want
                    detail = []
                    if missing:
                        detail.append("missing " + ", ".join(sorted(missing)))
                    if extra:
                        detail.append("unexpected " + ", ".join(sorted(extra)))
                    problems.append(f"{where}: {label} mismatch ({'; '.join(detail)})")

            want_tags, got_tags = Counter(TAG.findall(english)), Counter(TAG.findall(thai))
            if want_tags != got_tags:
                problems.append(
                    f"{where}: rich-text tags differ "
                    f"({sum(want_tags.values())} in English vs {sum(got_tags.values())} in Thai)")

            if english.strip() and not THAI.search(thai) and thai == english:
                # Fine for names, numbers and Latin-only labels; only worth noting.
                pass

    for key, count in seen.items():
        if count > 1:
            problems.append(f"{key}: translated in {count} files")

    print(f"checked   : {checked} translations")
    print(f"coverage  : {len(seen)}/{sum(1 for v in base.values() if v)} non-empty cells "
          f"({len(seen) / sum(1 for v in base.values() if v) * 100:.1f}%)")
    if problems:
        print(f"\nPROBLEMS  : {len(problems)}")
        for line in problems[:40]:
            print("  " + line)
        if len(problems) > 40:
            print(f"  ... and {len(problems) - 40} more")
        return 1
    print("\nall checks passed")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
