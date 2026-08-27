#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Dump the recovered Thai corpus as reference material.

The corpus came out of the 1.2 patch without its keys, so it cannot be merged
automatically. It is still the author's own wording, so it is worth reading as a
glossary before translating the same game again.

    python dump_corpus.py <max_chars> [skip] [take]
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

PROJECT = Path(__file__).resolve().parent.parent
CORPUS = PROJECT / "work" / "corpus_cells.json"


def main() -> int:
    max_chars = int(sys.argv[1]) if len(sys.argv) > 1 else 40
    skip = int(sys.argv[2]) if len(sys.argv) > 2 else 0
    take = int(sys.argv[3]) if len(sys.argv) > 3 else 400

    cells = json.loads(CORPUS.read_text(encoding="utf-8"))
    short = [c for c in cells if len(c["text"]) <= max_chars and c["gap_bytes"] == 0]

    print(f"# corpus cells total={len(cells)} short(<= {max_chars} chars, clean)={len(short)}")
    for cell in short[skip:skip + take]:
        print(f"{cell['index']}\t{cell['text']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
