#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Turn index-keyed translations into an English->Thai phrase dictionary.

`translate_repetitive.py` needs a dictionary keyed by the exact English string.
For the 648 mission and 999 story phrases, re-emitting those English keys by
hand would double the work and invite typos - a single altered character makes
the entry silently miss. So translations are written index-keyed against the
frozen distinct-phrase list instead, and this tool joins the two.

    python build_phrases.py work/mission_distinct.json work/mission_th_*.json \
                            -o work/phrases_mission.json
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("phrases", help="JSON array of distinct English strings")
    parser.add_argument("translations", nargs="+",
                        help="JSON objects mapping index -> Thai (globs allowed)")
    parser.add_argument("-o", "--out", required=True)
    args = parser.parse_args()

    english = json.loads(Path(args.phrases).read_text(encoding="utf-8"))

    paths: list[Path] = []
    for spec in args.translations:
        if any(ch in spec for ch in "*?"):
            pattern = Path(spec)
            paths.extend(sorted(pattern.parent.glob(pattern.name)))
        else:
            paths.append(Path(spec))

    thai: dict[int, str] = {}
    clashes = []
    for path in paths:
        for key, value in json.loads(path.read_text(encoding="utf-8")).items():
            index = int(key)
            if index in thai and thai[index] != value:
                clashes.append(f"index {index} translated twice with different text")
            thai[index] = value

    out_of_range = [i for i in thai if not 0 <= i < len(english)]
    empty = [i for i, v in thai.items() if not v.strip()]

    mapping = {english[i]: thai[i] for i in sorted(thai) if 0 <= i < len(english)}
    Path(args.out).write_text(
        json.dumps(mapping, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")

    print(f"phrases   : {len(english)}")
    print(f"translated: {len(thai)} ({len(thai) / len(english) * 100:.1f}%)")
    print(f"wrote     : {args.out} ({len(mapping)} entries)")
    problems = clashes + [f"index {i} out of range" for i in out_of_range] \
                       + [f"index {i} is empty" for i in empty]
    if problems:
        print("\nPROBLEMS:")
        for line in problems[:20]:
            print("  " + line)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
