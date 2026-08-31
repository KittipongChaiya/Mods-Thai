#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Emit review batches for the v1.4 refinement pass.

`make_batches.py` emits work for text that has no translation. This emits work
for text that *has* one and needs editing, so a batch carries both sides:

    { "<key>": { "en": "...", "th": "<current>", "cells": 3 } }

Batches are one unit of work per distinct (English, Thai) pair, not per cell.
11,352 cells collapse to ~7,977 pairs, and two cells that share a source string
and a translation can no longer drift apart during the pass - the revision is
fanned back out to all of them by `apply_reviews.py`.

Pairs already reviewed are skipped, tracked in work/reviews/reviewed.json. That
ledger is needed because "reviewed and left alone" is otherwise indistinguishable
from "never looked at".

    python make_reviews.py --stats
    python make_reviews.py --emit --tier a --budget 18000
    python make_reviews.py --emit --smell inanimate_pronoun     # defect sweep
    python make_reviews.py --emit --smell any --tier d          # tier D, Phase 6
"""
from __future__ import annotations

import argparse
import hashlib
import json
from collections import defaultdict
from pathlib import Path

from _corpus import (REVIEWS, group_pairs, load_base, load_translations,
                     prefix_of, tier_of, write_json)

LEDGER = REVIEWS / "reviewed.json"
STYLE_JSON = REVIEWS.parent / "style.json"


def signature(english: str, thai: str) -> str:
    return hashlib.sha1(f"{english}\x00{thai}".encode("utf-8")).hexdigest()[:16]


def load_ledger() -> set[str]:
    if not LEDGER.exists():
        return set()
    return set(json.loads(LEDGER.read_text(encoding="utf-8"))["reviewed"])


def load_smell(rule: str) -> set[str]:
    if not STYLE_JSON.exists():
        raise SystemExit(f"{STYLE_JSON} not found - run: python check_style.py --json {STYLE_JSON}")
    findings = json.loads(STYLE_JSON.read_text(encoding="utf-8"))
    if rule == "any":
        return {k for keys in findings.values() for k in keys}
    if rule not in findings:
        raise SystemExit(f"unknown rule {rule!r}; known: {', '.join(findings)}")
    return set(findings[rule])


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--stats", action="store_true")
    parser.add_argument("--emit", action="store_true")
    parser.add_argument("--budget", type=int, default=18000,
                        help="max English+Thai characters per batch")
    parser.add_argument("--tier", help="only this revision tier (a/b/c/d)")
    parser.add_argument("--prefix", help="only this key prefix")
    parser.add_argument("--smell", help="only pairs flagged by this check_style rule, or 'any'")
    parser.add_argument("--all", action="store_true",
                        help="include pairs already in the reviewed ledger")
    args = parser.parse_args()

    base = load_base()
    thai, _ = load_translations()
    pairs = group_pairs(base, thai)
    ledger = set() if args.all else load_ledger()
    smelly = load_smell(args.smell) if args.smell else None

    selected: dict[tuple[str, str], list[str]] = {}
    for pair, keys in pairs.items():
        english, current = pair
        if not english.strip():
            continue
        if signature(english, current) in ledger:
            continue
        if args.tier and not any(tier_of(k) == args.tier for k in keys):
            continue
        if args.prefix and not any(prefix_of(k) == args.prefix for k in keys):
            continue
        if smelly is not None and not any(k in smelly for k in keys):
            continue
        selected[pair] = keys

    done = len(pairs) - len([p for p in pairs if signature(*p) not in ledger])
    if args.stats or not args.emit:
        print(f"pairs total    : {len(pairs)}")
        print(f"already reviewed: {done}")
        print(f"selected       : {len(selected)} pairs, "
              f"{sum(len(p[0]) + len(p[1]) for p in selected)} chars (EN+TH), "
              f"{sum(len(k) for k in selected.values())} cells")
        print()
        groups: dict[str, list] = defaultdict(list)
        for pair, keys in selected.items():
            groups[f"{tier_of(keys[0])}/{prefix_of(keys[0])}"].append(pair)
        print(f"{'tier/prefix':<24}{'pairs':>8}{'chars':>10}")
        for name in sorted(groups):
            chars = sum(len(a) + len(b) for a, b in groups[name])
            print(f"{name:<24}{len(groups[name]):>8}{chars:>10}")

    if not args.emit:
        return 0

    REVIEWS.mkdir(parents=True, exist_ok=True)
    for stale in REVIEWS.glob("[0-9][0-9][0-9]_*.json"):
        stale.unlink()

    # Group by prefix so related strings are reviewed together and terminology
    # stays consistent within a batch - same reasoning as make_batches.py.
    groups = defaultdict(list)
    for pair, keys in selected.items():
        groups[(tier_of(keys[0]), prefix_of(keys[0]))].append((pair, keys))

    index = 0
    manifest = []
    for (tier, prefix) in sorted(groups):
        batch: dict[str, dict] = {}
        used = 0
        for (english, current), keys in sorted(groups[(tier, prefix)], key=lambda x: x[1][0]):
            size = len(english) + len(current)
            if batch and used + size > args.budget:
                index += 1
                manifest.append(write_batch(index, tier, prefix, batch))
                batch, used = {}, 0
            entry = {"en": english, "th": current}
            if len(keys) > 1:
                entry["cells"] = len(keys)
            batch[keys[0]] = entry
            used += size
        if batch:
            index += 1
            manifest.append(write_batch(index, tier, prefix, batch))

    write_json(REVIEWS / "manifest.json", manifest)
    total = sum(m["chars"] for m in manifest)
    print(f"\nwrote {len(manifest)} review batches to {REVIEWS} ({total} chars EN+TH)")
    print("translate into work/revisions/<same name>.json as {key: new_thai}, then:")
    print("  python apply_reviews.py work/revisions/<name>.json")
    return 0


def write_batch(index: int, tier: str, prefix: str, batch: dict) -> dict:
    name = f"{index:03d}_{tier}_{prefix}.json"
    write_json(REVIEWS / name, batch)
    return {
        "file": name,
        "tier": tier,
        "prefix": prefix,
        "pairs": len(batch),
        "cells": sum(e.get("cells", 1) for e in batch.values()),
        "chars": sum(len(e["en"]) + len(e["th"]) for e in batch.values()),
    }


if __name__ == "__main__":
    raise SystemExit(main())
