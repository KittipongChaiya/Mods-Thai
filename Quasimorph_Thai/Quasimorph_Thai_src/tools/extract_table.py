#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Extract the `localization` TextAsset out of a Quasimorph resources.assets.

This is a build-time tool. The shipped mod never touches resources.assets; it
only needs the table as a starting point for the Thai translation.

    python extract_table.py <resources.assets> <out.tsv>
"""
from __future__ import annotations

import sys
from pathlib import Path

ASSET_NAME = "localization"
EXPECTED_COLUMNS = 18


def main() -> int:
    try:
        import UnityPy
    except ImportError:
        print("UnityPy is required: pip install UnityPy", file=sys.stderr)
        return 2

    source = Path(sys.argv[1])
    destination = Path(sys.argv[2])

    env = UnityPy.load(str(source))
    for obj in env.objects:
        if obj.type.name != "TextAsset":
            continue
        data = obj.read()
        if getattr(data, "m_Name", "") != ASSET_NAME:
            continue

        raw = obj.read_typetree()["m_Script"]
        text = raw if isinstance(raw, str) else raw.decode("utf-8", "surrogateescape")
        payload = text.encode("utf-8", "surrogateescape")

        destination.parent.mkdir(parents=True, exist_ok=True)
        destination.write_bytes(payload)

        lines = [ln for ln in text.split("\r\n") if ln]
        widths = {len(ln.split("\t")) for ln in lines}
        print(f"asset      : {ASSET_NAME} (path_id={obj.path_id})")
        print(f"bytes      : {len(payload)}")
        print(f"rows       : {len(lines)} (1 header + {len(lines) - 1} data)")
        print(f"columns    : {sorted(widths)}")
        print(f"header     : {lines[0].split(chr(9))[:12]}")
        if widths != {EXPECTED_COLUMNS}:
            print(f"WARNING: expected every row to have {EXPECTED_COLUMNS} columns",
                  file=sys.stderr)
        print(f"wrote      : {destination}")
        return 0

    print(f"TextAsset {ASSET_NAME!r} not found in {source}", file=sys.stderr)
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
