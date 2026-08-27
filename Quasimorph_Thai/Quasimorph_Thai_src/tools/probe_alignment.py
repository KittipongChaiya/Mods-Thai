#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Test whether the recovered Thai cells can be matched back to 1.0.3 keys.

Layout of one line in the localization TSV:

    key \t english <tail>            where <tail> = \t ru \t de ... \t\t\t\t\t\t

The mod replaced only the `english` cell, so the opaque span the patch leaves
between two Thai cells is exactly:

    <tail_i> CRLF key_{i+1} \t       => observed_tail = len(tail_i) + len(key_{i+1}) + 3

That equation is the only handle we have on the keys.
"""
from __future__ import annotations

import json
import sys
from collections import Counter
from pathlib import Path


def blen(text: str) -> int:
    return len(text.encode("utf-8", "surrogateescape"))


def main() -> int:
    loc = Path(sys.argv[1]).read_bytes().decode("utf-8", "surrogateescape")
    cells = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))

    lines = [ln for ln in loc.split("\r\n") if ln]
    rows = []
    for ln in lines:
        cols = ln.split("\t")
        key, english = cols[0], cols[1]
        tail = "\t" + "\t".join(cols[2:])
        rows.append((key, english, blen(key), blen(tail)))

    header = rows[0]
    print(f"header tail bytes    : {header[3]}  (expect 98)")
    print(f"observed cell0 tail  : {cells[0]['tail_len']}")
    implied = cells[0]["tail_len"] - header[3] - 3
    print(f"=> implied len(key_1): {implied}")

    matches = [r for r in rows if r[2] == implied]
    print(f"\n1.0.3 keys of length {implied}: {len(matches)}")
    for key, english, _kl, _tl in matches[:25]:
        print(f"    {key!r:45s} -> {english!r}")

    # How discriminating is the equation overall?
    tail_hist = Counter(r[3] for r in rows)
    key_hist = Counter(r[2] for r in rows)
    print(f"\ndistinct tail lengths: {len(tail_hist)}   distinct key lengths: {len(key_hist)}")

    sample = cells[:300]
    counts = []
    for cell in sample:
        observed = cell["tail_len"]
        total = 0
        for tail_len, n_tail in tail_hist.items():
            need = observed - tail_len - 3
            if need in key_hist:
                total += n_tail * key_hist[need]
        counts.append(total)

    counts.sort()
    print(f"\ncandidate (row,next-key) pairs per cell, first 300 cells:")
    print(f"    min={counts[0]}  p25={counts[len(counts)//4]}  p50={counts[len(counts)//2]}  "
          f"p90={counts[int(len(counts)*0.9)]}  max={counts[-1]}")
    print(f"    cells with a UNIQUE pair: {sum(1 for c in counts if c == 1)}")
    print(f"    cells with <=5 pairs    : {sum(1 for c in counts if c <= 5)}")

    # Does 1.0.3's own row order reproduce the observed sequence at any offset?
    predicted = [rows[j][3] + rows[j + 1][2] + 3 for j in range(len(rows) - 1)]
    observed = [c["tail_len"] for c in cells]
    best, best_at = 0, -1
    for start in range(min(400, len(predicted))):
        run = 0
        while (start + run < len(predicted) and run < len(observed)
               and predicted[start + run] == observed[run]):
            run += 1
        if run > best:
            best, best_at = run, start
    print(f"\nlongest run where 1.0.3's own order reproduces the observed sequence: "
          f"{best} (starting at row {best_at})")
    print("  -> a long run would mean the row order never changed; 0-2 means it did.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
