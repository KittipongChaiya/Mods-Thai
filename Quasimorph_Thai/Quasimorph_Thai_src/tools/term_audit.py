#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Find terms whose Thai rendering drifts between cells.

`consistency.py` compares whole cells, so it only sees a term used inconsistently
when the term *is* the entire cell. Most of this translation's real defects were
not like that: the vest slot was ช่องเสื้อเกราะ in one label and เสื้อกั๊ก in
twenty others; the tutorial told the player to press a button whose actual label
read differently; `shuttle` was กระสวย, ยานขนส่ง and ยานการค้า in different
places. Every one of those hid behind a different English string.

The trick that found them by hand, automated: a term that is the whole English
of some short cell has a **canonical** Thai - that cell's translation, the words
the player literally sees on the button. Any longer cell whose English contains
the term should normally contain that canonical Thai too. Where it does not,
either the term drifted or the sentence legitimately rephrased it, so this
reports rather than rewrites.

    python term_audit.py                    # terms with the most drift first
    python term_audit.py --term shuttle     # one term, every cell
    python term_audit.py --min-cells 4 --max-term-len 24
"""
from __future__ import annotations

import argparse
import re
from collections import defaultdict

from _corpus import load_base, load_translations, prefix_of

WORD = re.compile(r"[A-Za-z][A-Za-z0-9'’.-]*")
TAG = re.compile(r"<[^>]+>")
#: %VICTIM_NAME% contains the word "victim", {0} contains nothing useful, and
#: neither is text the player reads. Both must go before any term matching, or
#: every mission cell looks like a drifted "victim".
TOKEN = re.compile(r"%[A-Za-z_][A-Za-z0-9_.]*(?::[A-Za-z0-9_]+)?%|\{\d+\}")


def strip_tags(value: str) -> str:
    return TOKEN.sub(" ", TAG.sub(" ", value))


def is_distinctive(term: str) -> bool:
    """Worth auditing at all? Only multi-word terms are.

    Capitalisation is no help: UI labels are Title Case, so "Place", "Reward"
    and "Still" all look like named terms. In running prose they are ordinary
    English, and demanding that every sentence containing "place" echo the
    button's วาง buries the real findings under hundreds of non-defects. The
    worst of them was `still` -> น้ำเปล่า, which is *still water* - a drinks
    item, matched against every sentence using "still" as an adverb.

    A multi-word term ("Vest slot", "Trade shuttle", "Mind Chip") is a name, and
    a name is what carries one fixed rendering across the table.
    """
    return " " in term


def canonical_terms(base: dict[str, str], thai: dict[str, str],
                    max_len: int) -> tuple[dict[str, dict[str, list[str]]], dict[str, str]]:
    """English term -> {its Thai in a cell that is *only* that term: [keys]}.

    A term can have more than one canonical rendering (Reload is a module
    recharge in one place and a weapon action in another); all are kept, and a
    longer cell matching any of them counts as consistent.
    """
    canon: dict[str, dict[str, list[str]]] = defaultdict(lambda: defaultdict(list))
    original_case: dict[str, str] = {}
    for key, english in base.items():
        if key not in thai:
            continue
        term = strip_tags(english).strip().strip(".:!?")
        if not term or len(term) > max_len or not WORD.fullmatch(term.replace(" ", "")):
            # Allow multi-word terms; the fullmatch above is on the spaceless form.
            if not (term and len(term) <= max_len and all(WORD.fullmatch(w) for w in term.split())):
                continue
        value = thai[key].strip()
        if not value or value == term:
            continue
        original_case.setdefault(term.lower(), term)
        canon[term.lower()][value].append(key)
    return canon, original_case


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--term", help="audit one term")
    parser.add_argument("--min-cells", type=int, default=3,
                        help="ignore terms appearing in fewer cells than this")
    parser.add_argument("--max-term-len", type=int, default=22)
    parser.add_argument("--limit", type=int, default=25)
    args = parser.parse_args()

    base = load_base()
    thai, _ = load_translations()
    canon, original_case = canonical_terms(base, thai, args.max_term_len)

    findings = []
    for term, renderings in canon.items():
        if args.term and term != args.term.lower():
            continue
        if len(term) < 4 or not is_distinctive(original_case[term]):
            continue
        pattern = re.compile(rf"(?<![A-Za-z]){re.escape(term)}(?![A-Za-z])", re.IGNORECASE)
        users = [k for k, e in base.items()
                 if k in thai and k not in {x for v in renderings.values() for x in v}
                 and pattern.search(strip_tags(e))]
        if len(users) + len(renderings) < args.min_cells:
            continue
        accepted = set(renderings)
        missing = [k for k in users if not any(r in thai[k] for r in accepted)]
        if not missing:
            continue
        findings.append((len(missing), term, renderings, users, missing))

    findings.sort(reverse=True)
    print(f"terms audited : {len(canon)}")
    print(f"with drift    : {len(findings)}\n")

    for count, term, renderings, users, missing in findings[:args.limit]:
        canon_list = " | ".join(sorted(renderings))
        print(f"--- {term!r}  canonical: {canon_list}")
        print(f"    {len(users)} other cell(s) use the term; {count} carry none of those renderings")
        for key in missing[:6]:
            print(f"      {key} [{prefix_of(key)}]")
            print(f"        {thai[key][:110]}")
        if len(missing) > 6:
            print(f"      ... and {len(missing) - 6} more")
        print()

    print("advisory - a longer sentence may legitimately rephrase a term")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
