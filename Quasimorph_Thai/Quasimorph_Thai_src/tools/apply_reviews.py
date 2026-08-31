#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Write a revised batch back into translations/*.json.

Reads work/revisions/NNN_tier_prefix.json as {key: new_thai} and its matching
work/reviews/NNN_tier_prefix.json for the text that was reviewed. Each revision
is written into whichever translations file already owns that key, so
translations/*.json stays the single source of truth and `git diff` is the
revision record.

Three things it refuses to do, because each would ship a broken table:

  * write a key it has never seen, or whose current Thai no longer matches what
    the review batch was generated from (someone edited underneath it)
  * change a placeholder, %TOKEN% or rich-text tag away from the English
  * introduce a tab, a line break or stray edge whitespace

A revision is fanned out to every cell sharing the reviewed (English, Thai)
pair, which is what stops two identical cells drifting apart mid-pass.

    python apply_reviews.py work/revisions/001_a_ui.json
    python apply_reviews.py work/revisions/001_a_ui.json --dry-run
"""
from __future__ import annotations

import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path

from _corpus import REVIEWS, group_pairs, load_base, load_translations, write_json
from make_reviews import LEDGER, signature

BRACE = re.compile(r"\{\d+\}")
PERCENT = re.compile(r"%[A-Za-z_][A-Za-z0-9_.]*(?::[A-Za-z0-9_]+)?%")
TAG = re.compile(r"</?[a-zA-Z][^>]*>")


def rewrite_in_place(path: Path, updates: dict[str, str]) -> int:
    """Replace just the value on each key's own line, touching nothing else.

    Re-serialising the file with json.dumps would be simpler, but these files
    carry blank lines that group them by key prefix, and a round-trip silently
    deletes every one of them - eleven junk diff lines for a one-line change,
    multiplied across 38 files and 45 batches. Values are guaranteed free of
    tabs and line breaks, so every entry is exactly one line and a line-level
    substitution is both safe and diff-minimal.
    """
    lines = path.read_text(encoding="utf-8-sig").split("\n")
    remaining = dict(updates)
    for index, line in enumerate(lines):
        stripped = line.lstrip()
        for key in list(remaining):
            prefix = json.dumps(key, ensure_ascii=False) + ": "
            if not stripped.startswith(prefix):
                continue
            indent = line[:len(line) - len(stripped)]
            comma = "," if stripped.rstrip().endswith(",") else ""
            value = json.dumps(remaining.pop(key), ensure_ascii=False)
            lines[index] = f"{indent}{prefix}{value}{comma}"
            break
    if remaining:
        raise SystemExit(f"{path.name}: no line found for {sorted(remaining)}")
    path.write_text("\n".join(lines), encoding="utf-8")
    return len(updates)


def verify(key: str, english: str, new: str) -> list[str]:
    """The same invariants check_translations.py enforces, applied before writing."""
    problems = []
    if "\t" in new:
        problems.append(f"{key}: contains a TAB")
    if "\n" in new or "\r" in new:
        problems.append(f"{key}: contains a line break")
    if new != new.strip():
        problems.append(f"{key}: leading or trailing whitespace")
    if not new.strip():
        problems.append(f"{key}: empty")
    for label, pattern in (("placeholder", BRACE), ("token", PERCENT), ("tag", TAG)):
        want, got = Counter(pattern.findall(english)), Counter(pattern.findall(new))
        if want != got:
            missing, extra = want - got, got - want
            detail = []
            if missing:
                detail.append("missing " + ", ".join(sorted(missing)))
            if extra:
                detail.append("unexpected " + ", ".join(sorted(extra)))
            problems.append(f"{key}: {label} mismatch ({'; '.join(detail)})")
    return problems


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("revisions", help="work/revisions/NNN_tier_prefix.json")
    parser.add_argument("--dry-run", action="store_true", help="report, write nothing")
    parser.add_argument("--partial", action="store_true",
                        help="mark only the revised pairs reviewed, not the whole batch")
    args = parser.parse_args()

    revisions_path = Path(args.revisions)
    review_path = REVIEWS / revisions_path.name
    if not review_path.exists():
        print(f"no matching review batch: {review_path}")
        return 2

    review = json.loads(review_path.read_text(encoding="utf-8"))
    revisions = json.loads(revisions_path.read_text(encoding="utf-8"))
    base = load_base()
    thai, owner = load_translations()
    pairs = group_pairs(base, thai)

    problems: list[str] = []
    edits: dict[str, str] = {}          # key -> new Thai, after fan-out
    changed_pairs = 0

    for key, new in revisions.items():
        if key not in review:
            problems.append(f"{key}: not in the review batch {review_path.name}")
            continue
        english, was = review[key]["en"], review[key]["th"]
        if thai.get(key) != was:
            problems.append(f"{key}: current Thai differs from the reviewed text "
                            f"- regenerate the batch before applying")
            continue
        problems.extend(verify(key, english, new))
        if new == was:
            continue
        changed_pairs += 1
        for sibling in pairs.get((english, was), [key]):
            edits[sibling] = new

    if problems:
        print(f"REFUSING TO APPLY - {len(problems)} problem(s):")
        for line in problems[:25]:
            print("  " + line)
        if len(problems) > 25:
            print(f"  ... and {len(problems) - 25} more")
        return 1

    # Applying a batch normally means "this whole batch has been read", so every
    # pair in it is marked reviewed - including the ones deliberately left alone,
    # which is the only way to tell those from pairs nobody has looked at yet.
    # --partial is for landing a fix noticed while reading somewhere else: it
    # would otherwise mark hundreds of unread pairs as done and skip them for good.
    scope = revisions if args.partial else review
    reviewed_now = {signature(review[k]["en"], edits.get(k, review[k]["th"]))
                    for k in scope}
    fanned = len(edits) - changed_pairs

    print(f"review batch : {review_path.name} ({len(review)} pairs)")
    print(f"revised      : {changed_pairs} pair(s) changed, "
          f"{(len(scope) - changed_pairs) if args.partial else len(review) - changed_pairs}"
          f" left as they were")
    if args.partial:
        print(f"partial      : marking only {len(scope)} pair(s) reviewed, "
              f"not the batch's {len(review)}")
    print(f"cells written: {len(edits)} ({fanned} by fan-out to identical cells)")

    if args.dry_run:
        print("\ndry run - nothing written")
        return 0

    by_file: dict[Path, dict[str, str]] = defaultdict(dict)
    for key, value in edits.items():
        by_file[owner[key]][key] = value
    for path, updates in sorted(by_file.items()):
        rewritten = rewrite_in_place(path, updates)
        print(f"  {path.name}: {rewritten} cell(s)")

    ledger = set()
    if LEDGER.exists():
        ledger = set(json.loads(LEDGER.read_text(encoding="utf-8"))["reviewed"])
    ledger |= reviewed_now
    write_json(LEDGER, {"reviewed": sorted(ledger)})
    print(f"\nledger       : {len(ledger)} pair(s) marked reviewed")
    print("verify with  : python check_translations.py")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
