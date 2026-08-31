#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Shared loaders for the v1.4 review pipeline.

The older tools each carry their own copy of `load_base()`, which is fine when a
script only reads. The review pipeline also *writes*, and `make_reviews.py` and
`apply_reviews.py` must group cells into (English, Thai) pairs **identically** -
one emits a batch keyed by a representative cell, the other fans the revision
back out to every cell in the same pair. If those two groupings ever disagreed,
a revision would land on cells it was never reviewed against. So the grouping
lives here once rather than being duplicated and trusted to stay in sync.

Not imported by the pre-1.4 tools; they are left as they are.
"""
from __future__ import annotations

import json
from collections import defaultdict
from pathlib import Path

ENGLISH_COLUMN = 1
PROJECT = Path(__file__).resolve().parent.parent
BASE = PROJECT / "work" / "localization_base.tsv"
TRANSLATIONS = PROJECT / "translations"
REVIEWS = PROJECT / "work" / "reviews"

#: Revision tiers, in the order v1.4 works through them. A prefix not listed
#: here falls in "b" - the small mechanical label prefixes all behave that way.
TIERS: dict[str, set[str]] = {
    "a": {"ui", "tooltip", "tutorial", "gamekey", "notification", "strategy"},
    "c": {"monster", "station", "faction", "spaceobject", "terminal", "bramfatura"},
    "d": {"mission", "story"},
}


def prefix_of(key: str) -> str:
    head = key.split(".", 1)[0]
    return head or "(blank)"


def tier_of(key: str) -> str:
    prefix = prefix_of(key)
    for tier, prefixes in TIERS.items():
        if prefix in prefixes:
            return tier
    return "b"


def load_base() -> dict[str, str]:
    """key -> English cell, from the extracted game table."""
    text = BASE.read_bytes().decode("utf-8", "surrogateescape")
    rows = [line.split("\t") for line in text.split("\r\n") if line]
    return {row[0]: row[ENGLISH_COLUMN] for row in rows[1:]}


def load_translations() -> tuple[dict[str, str], dict[str, Path]]:
    """key -> Thai, and key -> the translations file that owns it.

    The owner map is what lets a revision be written back in place instead of
    into a parallel overlay, which keeps translations/*.json the single source
    of truth and makes `git diff` the revision record.
    """
    thai: dict[str, str] = {}
    owner: dict[str, Path] = {}
    for path in sorted(TRANSLATIONS.glob("*.json")):
        for key, value in json.loads(path.read_text(encoding="utf-8-sig")).items():
            thai[key] = value
            owner[key] = path
    return thai, owner


def group_pairs(base: dict[str, str], thai: dict[str, str]) -> dict[tuple[str, str], list[str]]:
    """(English, Thai) -> every key carrying exactly that pair.

    Reviewing per pair rather than per cell cuts 11,352 cells to ~7,977 units of
    work, and makes it impossible for two cells with identical source and
    identical translation to drift apart during the pass.
    """
    pairs: dict[tuple[str, str], list[str]] = defaultdict(list)
    for key, value in thai.items():
        pairs[(base.get(key, ""), value)].append(key)
    return {pair: sorted(keys) for pair, keys in pairs.items()}


def write_json(path: Path, data) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
