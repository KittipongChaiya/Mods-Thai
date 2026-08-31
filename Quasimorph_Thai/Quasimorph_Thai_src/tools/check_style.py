#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Advisory lint against STYLE.md. Reports, never rewrites.

`check_translations.py` is the hard gate - it catches what would break the game.
This is the soft one: it catches what makes the Thai read like a translation.
Every finding here is a judgement call, so a flagged cell may legitimately keep
its wording. Treat the output as a reading list, not a rewrite list.

    python check_style.py                      # summary + samples
    python check_style.py --rule long_run -v   # every hit for one rule
    python check_style.py --tier a             # restrict to a revision tier
    python check_style.py --json work/style.json
    python check_style.py --glossary           # terminology drift report
"""
from __future__ import annotations

import argparse
import json
import re
from collections import defaultdict
from pathlib import Path

from _corpus import PROJECT, load_base, load_translations, prefix_of, tier_of

TAG = re.compile(r"<[^>]+>")
BR = re.compile(r"<br\s*/?>", re.IGNORECASE)
GLOSSARY = PROJECT / "GLOSSARY.md"

#: Where "มัน" is likely to stand for a person or an organisation rather than a
#: thing. Deliberately NOT the whole of Layer 2: a bramfatura is a realm, a
#: station is a place and a monster is a beast, and "มัน" for those is correct
#: Thai - `เมฆหนาทึบห้อยต่ำอยู่เหนือมัน` ("over it", the forest) is not a defect.
#: Corporations, the Church and named people dominate these four prefixes.
PERSONIFIED = {"faction", "terminal", "story", "mission"}

#: Layer 2 vocabulary. In a Layer 1 cell this is a register leak - a settings
#: toggle should never be ceremonial.
CEREMONIAL = ["จง", "เถิด", "ผองชน", "ดวงจิต", "พระบัญชา", "อาชญา", "ปรนนิบัติ",
              "ประกอบศาสนกิจ", "ผู้ต้องสาป", "สรีระ", "น่าสะพรึง", "อัปมงคล", "ล้างผลาญ"]

#: STYLE.md asks authors to aim for runs under SOFT_RUN. Only past HARD_RUN does
#: TMP actually fail to wrap the line in a narrow panel, so the two are reported
#: separately: the hard band is a bug list, the soft band is a polish list.
SOFT_RUN = 60
HARD_RUN = 90


def plain(value: str) -> str:
    """Text as the player reads it: tags removed, <br> treated as a break."""
    return TAG.sub(" ", BR.sub(" ", value))


def longest_run(value: str) -> int:
    # Semicolon-delimited list cells are split by the game itself, so their
    # length is not a wrapping problem.
    if value.count(";") >= 3:
        return 0
    return max((len(part) for part in plain(value).split(" ")), default=0)


def build_rules(base: dict[str, str]):
    """name -> (predicate(key, thai) -> bool, description)."""

    def is_mechanic(key: str) -> bool:
        return key.endswith((".desc", ".shortdesc")) and tier_of(key) == "b"

    return {
        "trailing_space": (
            lambda k, t: t != t.strip(),
            "leading or trailing whitespace"),
        # Deliberately this direction. Both render - the shipped TMP asset is
        # dynamic and tahoma.ttf has U+2026 - but '.' is in the static atlas and
        # '…' is not, and 128 of the 131 affected cells already use '...'.
        # Normalising down is cheaper and keeps the common case off the runtime
        # rasterization path. See STYLE.md §5.
        "ellipsis": (
            lambda k, t: "…" in t,
            "'…' where STYLE.md wants ASCII '...'"),
        # Only in chrome. A character saying "ใช่ครับ" in mission dialogue is
        # characterisation, not a register error - Layer 2 is left alone.
        "polite_particle": (
            lambda k, t: tier_of(k) in {"a", "b"} and
            bool(re.search(r"(ครับ|ค่ะ|คะ)(\s|$)", t)),
            "polite particle in a Layer 1 cell"),
        "inanimate_pronoun": (
            lambda k, t: prefix_of(k) in PERSONIFIED and
            bool(re.search(r"ของมัน|มัน(คือ|เป็น|ให้|มี|ได้|จะ|ยัง)", t)),
            "'มัน' where the referent is likely a person or an organisation"),
        "long_run_hard": (
            lambda k, t: longest_run(t) > HARD_RUN,
            f"unbroken run over {HARD_RUN} chars - TMP cannot wrap it"),
        "long_run_soft": (
            lambda k, t: SOFT_RUN < longest_run(t) <= HARD_RUN,
            f"unbroken run of {SOFT_RUN + 1}-{HARD_RUN} chars - readable, could breathe"),
        "stacked_nominalisation": (
            lambda k, t: t.count("การ") >= 2,
            "two or more 'การ'+verb nominalisations in one cell"),
        "nominalised_mechanic": (
            lambda k, t: is_mechanic(k) and t.lstrip().startswith("การ"),
            "mechanic description opens with 'การ' instead of a verb"),
        "relative_clause": (
            lambda k, t: t.count("ซึ่ง") > 1 or "ที่ซึ่ง" in t,
            "more than one 'ซึ่ง', or a 'ที่ซึ่ง'"),
        "passive_mechanic": (
            lambda k, t: is_mechanic(k) and "ถูก" in t,
            "'ถูก' passive in a mechanic description"),
        # Bare "โดย" is ordinary Thai (โดยตรง, โดยไม่, โดยชอบธรรม). What carries
        # over from English is the full passive-with-agent frame, ถูก X โดย Y.
        "passive_with_agent": (
            lambda k, t: "ถูก" in t and re.search(r"ถูก.{0,40}โดย", t) is not None,
            "'ถูก … โดย' - an English passive carried over whole"),
        "register_leak": (
            lambda k, t: tier_of(k) in {"a", "b"} and
            not k.endswith(".name") and
            any(w in t for w in CEREMONIAL),
            "Layer 2 ceremonial vocabulary in a Layer 1 cell"),
    }


def glossary_terms() -> list[tuple[str, str]]:
    """(English, Thai) pairs parsed out of the GLOSSARY.md markdown tables."""
    terms: list[tuple[str, str]] = []
    for line in GLOSSARY.read_text(encoding="utf-8").splitlines():
        if not line.startswith("|"):
            continue
        cells = [c.strip() for c in line.strip().strip("|").split("|")]
        if len(cells) < 2 or cells[0] in {"English", "---", "Kind", "Plain"}:
            continue
        if set(cells[0]) <= set("-: ") or not cells[1]:
            continue
        terms.append((cells[0], cells[1]))
    return terms


def report_glossary(base: dict[str, str], thai: dict[str, str]) -> None:
    """For each glossary term, what Thai do cells containing it actually use?

    Not pass/fail - many hits are incidental. It is a drift report: a term with
    four different renderings in the wild is worth a look before the tier that
    owns it is revised.
    """
    print("term drift (cells whose English contains the term, by Thai rendering)\n")
    for english, expected in glossary_terms():
        if len(english) < 3 or not re.match(r"^[A-Za-z][A-Za-z ./()'-]*$", english):
            continue
        word = re.compile(rf"\b{re.escape(english)}\b", re.IGNORECASE)
        hits = [k for k, v in base.items() if k in thai and word.search(v)]
        if len(hits) < 2:
            continue
        expected_core = expected.split(" (")[0].split(" / ")[0].strip("`")
        missing = [k for k in hits if expected_core and expected_core not in thai[k]]
        if not missing:
            continue
        print(f"  {english:28s} -> {expected_core}")
        print(f"      {len(hits)} cell(s) contain it; {len(missing)} do not use that rendering")
        for key in missing[:3]:
            print(f"        {key}: {thai[key][:70]}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--rule", help="only this rule")
    parser.add_argument("--tier", help="only this revision tier (a/b/c/d)")
    parser.add_argument("--prefix", help="only this key prefix")
    parser.add_argument("--json", help="write {rule: [keys]} for make_reviews.py --smell")
    parser.add_argument("--glossary", action="store_true", help="terminology drift report")
    parser.add_argument("-v", "--verbose", action="store_true", help="list every hit")
    args = parser.parse_args()

    base = load_base()
    thai, _ = load_translations()

    if args.tier:
        thai = {k: v for k, v in thai.items() if tier_of(k) == args.tier}
    if args.prefix:
        thai = {k: v for k, v in thai.items() if prefix_of(k) == args.prefix}

    if args.glossary:
        report_glossary(base, thai)
        return 0

    rules = build_rules(base)
    if args.rule:
        if args.rule not in rules:
            print(f"unknown rule {args.rule!r}; known: {', '.join(rules)}")
            return 2
        rules = {args.rule: rules[args.rule]}

    findings: dict[str, list[str]] = {}
    for name, (predicate, _) in rules.items():
        findings[name] = sorted(k for k, v in thai.items() if predicate(k, v))

    flagged = {k for keys in findings.values() for k in keys}
    print(f"scanned  : {len(thai)} cells")
    print(f"flagged  : {len(flagged)} cells by at least one rule\n")
    print(f"{'rule':26s}{'cells':>7s}  what it means")
    for name, (_, description) in rules.items():
        print(f"{name:26s}{len(findings[name]):>7d}  {description}")

    for name in rules:
        keys = findings[name]
        if not keys:
            continue
        by_tier = defaultdict(int)
        for key in keys:
            by_tier[tier_of(key)] += 1
        print(f"\n--- {name} ({len(keys)}) tiers: "
              f"{', '.join(f'{t}={n}' for t, n in sorted(by_tier.items()))}")
        for key in (keys if args.verbose else keys[:4]):
            print(f"  {key}")
            print(f"    {thai[key][:150]}")
        if not args.verbose and len(keys) > 4:
            print(f"  ... and {len(keys) - 4} more")

    if args.json:
        Path(args.json).write_text(
            json.dumps(findings, ensure_ascii=False, indent=1), encoding="utf-8")
        print(f"\nwrote {args.json}")

    print("\nadvisory only - nothing here blocks a build")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
