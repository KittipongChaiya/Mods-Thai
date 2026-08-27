#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Search the localization table by key pattern or English value."""
from __future__ import annotations

import re
import sys
from pathlib import Path


def main() -> int:
    loc = Path(sys.argv[1]).read_bytes().decode("utf-8", "surrogateescape")
    pattern = re.compile(sys.argv[2], re.IGNORECASE)
    field = sys.argv[3] if len(sys.argv) > 3 else "both"
    limit = int(sys.argv[4]) if len(sys.argv) > 4 else 60

    shown = 0
    for line in loc.split("\r\n"):
        if not line:
            continue
        cols = line.split("\t")
        key, english = cols[0], cols[1]
        hit = (
            (field in ("key", "both") and pattern.search(key))
            or (field in ("value", "both") and pattern.search(english))
        )
        if hit:
            print(f"{key!r:52s} -> {english!r}")
            shown += 1
            if shown >= limit:
                break
    print(f"\n({shown} shown)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
