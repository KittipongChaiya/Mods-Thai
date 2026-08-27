#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Print a character-budgeted slice of a distinct-phrase list.

The phrase lists for `mission` and `story` are far too large to read in one go,
so translation proceeds in slices. Printing by *character* budget rather than by
count keeps each slice a similar amount of work, because phrase lengths span two
orders of magnitude.

    python dump_phrases.py work/mission_distinct.json --start 0 --budget 15000
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source")
    parser.add_argument("--start", type=int, default=0)
    parser.add_argument("--budget", type=int, default=15000)
    args = parser.parse_args()

    phrases = json.loads(Path(args.source).read_text(encoding="utf-8"))
    used = 0
    index = args.start
    while index < len(phrases) and (used == 0 or used + len(phrases[index]) <= args.budget):
        print(f"[{index}] {phrases[index]}")
        used += len(phrases[index])
        index += 1
    print(f"\n--- printed {index - args.start} phrases ({used} chars); "
          f"next --start {index} of {len(phrases)} ---")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
