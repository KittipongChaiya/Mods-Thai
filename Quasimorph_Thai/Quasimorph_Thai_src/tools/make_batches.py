#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Split the untranslated part of the table into work batches.

Batches are grouped by key prefix so a translator (human or model) sees related
strings together and keeps terminology consistent, and are capped by character
budget rather than row count because cell sizes vary by three orders of
magnitude.

    python make_batches.py --stats
    python make_batches.py --emit --budget 20000
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path

ENGLISH_COLUMN = 1
PROJECT = Path(__file__).resolve().parent.parent
BASE = PROJECT / "work" / "localization_base.tsv"
TRANSLATIONS = PROJECT / "translations"
BATCHES = PROJECT / "work" / "batches"


def load_base() -> list[tuple[str, str]]:
    text = BASE.read_bytes().decode("utf-8", "surrogateescape")
    rows = [line.split("\t") for line in text.split("\r\n") if line]
    return [(row[0], row[ENGLISH_COLUMN]) for row in rows[1:]]


def load_done() -> set[str]:
    done: set[str] = set()
    if TRANSLATIONS.is_dir():
        for path in sorted(TRANSLATIONS.glob("*.json")):
            done.update(json.loads(path.read_text(encoding="utf-8")))
    return done


def prefix_of(key: str) -> str:
    head = key.split(".", 1)[0]
    return head or "(blank)"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stats", action="store_true")
    parser.add_argument("--emit", action="store_true")
    parser.add_argument("--budget", type=int, default=20000,
                        help="max English characters per batch")
    parser.add_argument("--only", help="only this key prefix")
    args = parser.parse_args()

    rows = load_base()
    done = load_done()
    pending = [(k, v) for k, v in rows if v and k not in done]

    groups: dict[str, list[tuple[str, str]]] = defaultdict(list)
    for key, english in pending:
        groups[prefix_of(key)].append((key, english))

    if args.stats or not args.emit:
        total_cells = sum(1 for _, v in rows if v)
        total_chars = sum(len(v) for _, v in rows if v)
        pending_chars = sum(len(v) for _, v in pending)
        print(f"cells with text : {total_cells}")
        print(f"already done    : {total_cells - len(pending)} "
              f"({(total_cells - len(pending)) / total_cells * 100:.1f}%)")
        print(f"pending         : {len(pending)} cells, {pending_chars} chars "
              f"of {total_chars}")
        print()
        print(f"{'prefix':<24}{'cells':>8}{'chars':>12}{'avg':>8}")
        ordered = sorted(groups.items(), key=lambda kv: -sum(len(v) for _, v in kv[1]))
        for name, items in ordered:
            chars = sum(len(v) for _, v in items)
            print(f"{name:<24}{len(items):>8}{chars:>12}{chars // len(items):>8}")

    if not args.emit:
        return 0

    BATCHES.mkdir(parents=True, exist_ok=True)
    for stale in BATCHES.glob("*.json"):
        stale.unlink()

    index = 0
    manifest = []
    for name in sorted(groups):
        if args.only and name != args.only:
            continue
        batch: dict[str, str] = {}
        used = 0
        for key, english in groups[name]:
            if batch and used + len(english) > args.budget:
                index += 1
                manifest.append(write_batch(index, name, batch))
                batch, used = {}, 0
            batch[key] = english
            used += len(english)
        if batch:
            index += 1
            manifest.append(write_batch(index, name, batch))

    (BATCHES / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\nwrote {len(manifest)} batches to {BATCHES}")
    return 0


def write_batch(index: int, prefix: str, batch: dict[str, str]) -> dict:
    name = f"{index:03d}_{prefix}.json"
    (BATCHES / name).write_text(
        json.dumps(batch, ensure_ascii=False, indent=1), encoding="utf-8")
    return {
        "file": name,
        "prefix": prefix,
        "cells": len(batch),
        "chars": sum(len(v) for v in batch.values()),
    }


if __name__ == "__main__":
    raise SystemExit(main())
