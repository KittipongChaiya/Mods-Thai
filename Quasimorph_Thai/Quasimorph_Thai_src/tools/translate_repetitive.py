#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Translate a highly repetitive batch from an exact-match phrase dictionary.

Some prefixes (notably `woundslot`) are hundreds of mechanical strings built from
a small vocabulary - "Arm amputated", "Servo torn off". Hand-typing those invites
inconsistency and typos; a dictionary applies one decision everywhere.

Anything the dictionary does not cover is reported, never guessed, so the
remainder can be translated by hand.

Several batches can be passed at once: sibling batches of the same prefix share
most of their vocabulary, so deduplicating across all of them at once shrinks
the dictionary a lot.

    python translate_repetitive.py <phrases.json> <out.json> <batch.json>...
"""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path


def main() -> int:
    # The dictionary may be split across several files (a glob is accepted), so a
    # long prefix can be translated in sittings without merging by hand.
    spec = sys.argv[1]
    if any(ch in spec for ch in "*?"):
        pattern = Path(spec)
        dict_paths = sorted(pattern.parent.glob(pattern.name))
    else:
        dict_paths = [Path(spec)]

    phrases: dict[str, str] = {}
    for path in dict_paths:
        phrases.update(json.loads(path.read_text(encoding="utf-8-sig")))

    out_path = Path(sys.argv[2])

    batch: dict[str, str] = {}
    for path in sys.argv[3:]:
        batch.update(json.loads(Path(path).read_text(encoding="utf-8-sig")))

    translated: dict[str, str] = {}
    unknown: Counter[str] = Counter()

    for key, english in batch.items():
        thai = phrases.get(english)
        if thai is None:
            unknown[english] += 1
            continue
        translated[key] = thai

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        json.dumps(translated, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")

    print(f"covered   : {len(translated)}/{len(batch)} "
          f"({len(translated) / len(batch) * 100:.1f}%)")
    print(f"wrote     : {out_path}")
    if unknown:
        # Write a fill-in skeleton rather than printing hundreds of lines: the
        # translator fills the empty values and re-runs with it as the dictionary.
        # Keep the skeleton beside the dictionary, never beside the output: a
        # half-filled file in translations/ would be merged as empty strings.
        todo_dir = dict_paths[0].parent if dict_paths else out_path.parent
        todo = todo_dir / (out_path.stem + ".todo.json")
        todo.write_text(
            json.dumps({english: "" for english, _ in unknown.most_common()},
                       ensure_ascii=False, indent=1) + "\n",
            encoding="utf-8")
        print(f"\nNOT COVERED: {len(unknown)} distinct phrase(s)")
        print(f"skeleton  : {todo}  (fill in the values, then re-run)")
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
