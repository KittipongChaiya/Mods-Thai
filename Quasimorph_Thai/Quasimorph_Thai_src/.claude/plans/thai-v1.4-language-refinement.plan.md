# Plan: Thai translation v1.4 — language refinement pass

**Source**: free-form request — "refine the language to make it better, easier to
understand, and more friendly, while still maintaining the game's style."
**Project**: `Quasimorph_Thai/Quasimorph_Thai_src`
**Baseline**: v1.3 — 11,352 / 11,352 cells translated, all validation passing
**Complexity**: Large (edit surface is 7,977 distinct EN→TH pairs / 1.81 M chars)
**Status**: AWAITING CONFIRMATION

---

## 1. Requirements restatement

v1.3 is complete and correct: every cell is translated, placeholders and tags validate,
and the mod loads. This is **not** a translation task — it is an **editing** task on
finished text. The goal is a v1.4 release in which the Thai:

1. **Reads better** — natural Thai sentence shape instead of English word order.
2. **Is easier to understand** — shorter clauses, plainer vocabulary, unambiguous
   game-mechanic wording.
3. **Feels friendlier** — the UI talks *to* the player rather than at them.
4. **Keeps the game's style** — Quasimorph is grim, cynical, corporate-dystopian
   sci-fi. Friendly must not become cheerful. Lore voice stays literary and dark;
   only the *interface* voice becomes warmer.

Non-goals: no new coverage, no game-version port, no mechanic renames that would break
the player↔wiki↔community vocabulary.

---

## 2. Grounding — measured, not assumed

Measured from the current `translations/*.json` against `work/localization_base.tsv`:

| Metric | Value |
|---|---|
| Cells | 11,352 |
| Thai characters | 1,035,889 |
| Distinct Thai strings | 7,767 |
| **Distinct (English, Thai) pairs — the real edit surface** | **7,977** |
| Prose cells (>120 chars) | 2,300 → only 1,843 distinct |

Where the volume actually lives:

| Tier | Prefixes | Distinct pairs | TH chars | EN chars |
|---|---|---|---|---|
| **A — interface** | `ui` `tooltip` `tutorial` `gamekey` `notification` `strategy` | 1,812 | 90,998 | 103,664 |
| **B — gameplay text** | `item` `perk` `mgperk` `trait` `wound*` `curse` `firemode` `weapon` `armor` `class` `spec` `pact` `name` `alliance` `missiontype` `factiontype` | 3,078 | 70,429 | 73,067 |
| **C — world text** | `monster` `station` `faction` `spaceobject` `terminal` `bramfatura` | 1,564 | 219,880 | 256,001 |
| **D — lore bulk** | `mission` `story` | 1,644 | 461,339 | 541,878 |

**A + B is 61 % of the pairs but only 9 % of the characters** — and it is what a player
reads on every screen, every run. That asymmetry drives the phasing below.

### Defects already found by scanning

| Signal | Cells | Verdict |
|---|---|---|
| Same English → **different** Thai (80 source strings) | 194 | **Real bugs.** Includes `Duggur` → ดักกูร์ / ดุกกูร์, `armor` → เกราะ / เสื้อเกราะ, `Reload` → บรรจุอาวุธ / การชาร์จใหม่, `Sting` → เหล็กใน / เหล็กไน (misspelling), `Volcano` → ภูเขาไฟ / **`Vulcan`** (wrong word left in) |
| `ของมัน` used of people / organisations | 199 | Style defect — unnatural and faintly rude in Thai |
| `การ`-nominalisation ×2+ in one cell | 908 | Translationese; the main cause of "reads stiff" |
| Unbroken run >90 chars (excluding `;`-lists) | ~156 | Hurts readability **and** TMP line-wrapping in narrow panels |
| `...` instead of `…` | 128 | Cosmetic consistency |
| Leading/trailing whitespace | 1 | `monster.corporation_dog.desc` |

### Representative before / after

```
perk.levelUpPassive_AnyThrowKill
  EN  Each kill with a thrown weapon grants {0} XP.
  NOW การสังหารด้วยอาวุธขว้างแต่ละครั้งให้ {0} XP        <- noun-stacked, no subject
  ->  สังหารศัตรูด้วยอาวุธขว้าง รับ {0} XP ต่อครั้ง        <- verb-first, player-facing

faction.ChurchRevelation.desc
  NOW ...มันให้บริการทางศาสนาคุณภาพสูงบนพื้นฐานเชิงพาณิชย์   <- "มัน" for the Church
  ->  ...ศาสนจักรให้บริการทางศาสนาคุณภาพสูงในเชิงพาณิชย์

perk.flauros_spiked_vines.desc
  EN  Fills a target area with a radius of %IRadius% with thorn vines.
  NOW เติมเถาหนามลงในพื้นที่เป้าหมายรัศมี %IRadius%        <- "fills...with" translated literally
  ->  ปกคลุมพื้นที่รัศมี %IRadius% ด้วยเถาหนาม
```

---

## 3. The style contract (the crux)

**Decided 2026-08-31 by the project owner: the target voice is Warhammer 40,000 —
grimdark, gothic, ceremonial.**

That steer pulls against "friendlier" for part of the table, so the contract resolves it
with a hard boundary rather than a compromise:

> **The chrome is plain and clear. The world is grimdark.**
>
> Anything that is *the game talking to the player as software* — buttons, labels,
> tooltips, settings, key bindings, error and system messages — is plain, short and
> friendly. Clarity wins; 40K diction there would just make the UI harder to use.
>
> Anything that is *the world talking* — factions, stations, monsters, terminals,
> bramfaturas, story, missions, perk and item flavour, demon dialogue — takes the
> elevated gothic register.
>
> "Friendlier" therefore means **friendlier interface**, not friendlier fiction. The
> fiction gets *darker and more ceremonial*, and easier to read at the same time,
> because elevated ≠ convoluted.

This is not a new invention — it extends the voice the translation **already has** in
`story` and `bramfatura` (`จงตามมาเถิด สิ่งประดิษฐ์ของมนุษย์ … ข้าคือเจ้านายหลังประตูสีเขียว`)
outward into `faction`, `station`, `monster`, `terminal` and flavour text, which currently
read like neutral encyclopedia entries.

Phase 1 produces **`STYLE.md`**, a sibling to `GLOSSARY.md`, binding for the whole pass.

### Layer 1 — Chrome (plain, clear, friendly)

| Class | Addressee | Register | Rule |
|---|---|---|---|
| Buttons / labels / tooltips | — | Terse, no pronoun | Verb or noun alone. Never a full sentence. |
| Settings, key bindings, system + error messages | `คุณ` | Plain-polite, **no** ครับ/ค่ะ | Speak to the player directly; short clauses |
| Tutorial + instructional dialog | `คุณ`, `บอส` for the narrator | Plain-polite, conversational | Teaching text stays warm and easy — this is a new player's first hour |
| Perk / item / wound **mechanics** | implicit player | Plain, mechanical | Verb-first, one clause per effect, numbers late |

### Layer 2 — World (grimdark gothic)

| Class | Register | Rule |
|---|---|---|
| Faction / corporation dossiers | Formal-bureaucratic, pompous | Official-dossier diction. Name the subject; **never** `มัน` for an organisation |
| Church of Revelation, pacts, curses | Liturgical | Ceremonial cadence — `จง…`, `แห่ง…`, `ผู้…`; Pali/Sanskrit-derived vocabulary |
| Bramfaturas, demons, quasimorphs | Archaic, imperious | `ข้า` / `เจ้า` (already established); `ท่าน` for the exalted |
| Station / spaceobject / monster prose | Neutral-literary, bleak | Elevated but readable; short sentences, dark imagery kept |
| Story / mission prose | Dark-literary | **Voice preserved** — defect sweep only (Phase 6) |
| Perk / item / pact **names** | Ceremonial compound nouns | Extend the existing `…แห่ง…` pattern (บัลลังก์แดงและทอง, ราคาแห่งบัรซัค) |
| Combat log / newsline | Terse military vox-report | Factual and clipped, never casual |

### Grimdark lexicon (Phase 1 fixes the full list into `STYLE.md`)

Where Thai offers both a plain and an elevated word, Layer 2 takes the elevated one:

| Plain | Layer 2 |
|---|---|
| ตาย | มรณะ / ดับสูญ |
| ทำลาย | ทำลายล้าง / ล้างผลาญ |
| ความเชื่อ | ศรัทธา |
| กำจัด (of heretics) | ชำระล้าง |
| คำสั่ง (of Church / Hexarchy) | พระบัญชา / อาชญา |
| คนบาป | ผู้ต้องสาป / ผู้นอกรีต |
| จิตใจ | ดวงจิต / วิญญาณ |
| ศักดิ์สิทธิ์ | ศักดิ์สิทธิ์ (kept) |

Hard limits: **no** invented Thai-Buddhist royal register (ราชาศัพท์) — it reads as Thai
monarchy, not gothic sci-fi. **No** archaic register in Layer 1. Elevated vocabulary must
never cost comprehension: if an elevated word makes a mechanic ambiguous, the plain word wins.

### Rules applied mechanically (both layers)

1. Prefer a plain verb over `การ`+verb when the cell is an effect description.
2. At most one `ซึ่ง` per sentence; prefer splitting the sentence.
3. `ถูก` only for genuine adversity-passive; otherwise reorder to active voice.
   (Layer 2 exception: `ถูก` is welcome where the doom is the point.)
4. `มัน` never refers to a person, a faction, a corporation, or the Church.
5. Insert a space at phrase boundaries so no unbroken run exceeds ~60 characters —
   this is also what lets TMP wrap Thai in narrow panels.
6. `…` not `...`. No leading/trailing space. No tab, no newline (already enforced).
7. Placeholders, `%TOKEN%`, `<color=…>`, `<br>` stay byte-identical — unchanged from v1.3.
8. Terminology comes from `GLOSSARY.md`. Changing a glossary term is a glossary edit
   first, then a global re-apply — never a one-off.

---

## 4. Tooling to add (`tools/`)

The v1.3 pipeline only runs English → Thai. A revision pass needs a review loop, mirroring
the existing patterns (`make_batches.py` for emit, `check_translations.py` for validation,
`translate_repetitive.py` for the distinct-phrase indirection).

| File | Action | Why |
|---|---|---|
| `tools/make_reviews.py` | CREATE | Mirrors `make_batches.py`. Emits `work/reviews/NNN_<tier>.json` as `{key: {en, th}}`, **deduplicated by (EN, TH) pair**, character-budgeted, filterable by `--prefix` / `--smell` / `--only-inconsistent`. `--stats` prints what remains. |
| `tools/apply_reviews.py` | CREATE | Reads `reviews/NNN.json` (`{key: new_th}`), writes each key back into whichever `translations/*.json` owns it, and **fans the edit out to every key sharing the same (EN, TH) pair**. Refuses to write if a key is unknown or a placeholder/tag would change. |
| `tools/check_style.py` | CREATE | Advisory (non-blocking) lint: the §3 rules, `ของมัน`-for-organisation, long runs, `...`, glossary-term drift. Reports, never rewrites. |
| `tools/consistency.py` | CREATE | Reports same-EN→different-TH **and** same-TH→different-EN. The first becomes build-blocking once Phase 2 clears it. |
| `tools/check_translations.py` | UPDATE | Add the two cheap invariants it lacks: leading/trailing whitespace, and `...` where `…` is the convention. Remains the hard gate. |
| `STYLE.md` | CREATE | The §3 contract. |
| `GLOSSARY.md` | UPDATE | Absorb every term resolved during Phase 2. |

Source of truth stays `translations/*.json` — edits land **in place**, and `git diff` is
the revision record. No parallel overlay directory, no second source of truth.

---

## 5. Phases

### Phase 0 — Safety net
- Tag the v1.3 tree (`git tag v1.3-translation`) so the whole pass is revertible.
- Keep the current `thai_overrides.tsv.gz` for A/B comparison in game.
- **Gate**: `check_translations.py` green on the untouched tree.

### Phase 1 — Style contract + tooling
- Write `STYLE.md` (§3), reconciled against `GLOSSARY.md`.
- Build the four new tools; update `check_translations.py`.
- **Gate**: tools run clean on the v1.3 tree and reproduce the numbers in §2.

### Phase 2 — Mechanical defect sweep (whole table, all tiers)
Cheap, high-certainty, no judgement calls:
- Resolve all 80 inconsistent English→Thai renderings to one form each (194 cells).
- Fix the `Volcano`→`Vulcan`, `เหล็กไน`→`เหล็กใน`, `ดุกกูร์`→`ดักกูร์` class of errors.
- Normalise `...`→`…`; strip the stray-whitespace cell.
- Every resolution is recorded in `GLOSSARY.md`.
- **Gate**: `consistency.py` reports zero same-EN→different-TH; `check_translations.py` green.

### Phase 3 — Tier A, the interface (1,812 pairs, ~195 k chars EN+TH ≈ 11 review batches)
`ui` `tooltip` `tutorial` `gamekey` `notification` `strategy`. Highest read-count text in
the game and where "friendly" is actually felt. The tutorial gets its own batch — it is a
new player's first hour.
- **Gate**: `check_translations.py` green, `check_style.py` clean for the tier, in-game
  spot-check of main menu / settings / inventory / a tooltip / the first tutorial screen.

### Phase 4 — Tier B, gameplay text (3,078 pairs, ~143 k chars ≈ 8 review batches)
`item` `perk` `mgperk` `trait` `wound*` `curse` plus the small label prefixes. Verb-first
effect descriptions; numbers and tokens late; one clause per effect.
- **Gate**: as Phase 3, plus a perk-tooltip and wound-panel screenshot check for overflow.

### Phase 5 — Tier C, world text (1,564 pairs, ~476 k chars ≈ 26 review batches)
`monster` `station` `faction` `spaceobject` `terminal` `bramfatura`. Where the 199 `ของมัน`
defects and the long unbroken runs concentrate.
- **Gate**: as above, plus a station-description and faction-panel wrap check.

### Phase 6 — Tier D, lore bulk (mission + story) — **targeted, not a rewrite**
1,644 pairs / ~1 M chars EN+TH. Recommendation: **do not re-edit this prose wholesale.**
It is already in good literary shape, it is read once per playthrough, and a full pass here
costs more than tiers A–C combined. Instead revise only cells flagged by `check_style.py` /
`consistency.py` (est. 400–600 cells): pronoun defects, glossary drift, long unbroken runs,
ellipses.
- **Gate**: the flagged set clears; a sampled read of 30 mission briefings and 20 story
  entries confirms the voice is unchanged.

### Phase 7 — Release v1.4
- Bump `1.3.0` → `1.4.0` in `mod_src/QuasimorphThai/ModLog.cs:74` and
  `QuasimorphThai.csproj:13`; retarget `build.ps1 -OutDir` to `Quasimorph_Thai_v1.4`.
- `.\build.ps1`, deploy to `LocalUserPresets\QuasimorphThai\`, verify the log line
  `11577 rows, 11352 translated, 0 left in English`.
- Update `PROJECT_STATE.md` and `README.md`; add a v1.4 changelog entry.
- **Gate**: clean load, no exceptions, spot-checks pass.

---

## 6. Validation

```powershell
$P = "C:\Users\Administrator\Desktop\Mods-Thai\Quasimorph_Thai\Quasimorph_Thai_src"
python "$P\tools\check_translations.py"   # HARD gate: placeholders, tags, tabs, dupes
python "$P\tools\consistency.py"          # HARD gate from Phase 2: one Thai per English
python "$P\tools\check_style.py"          # ADVISORY: style-contract lint
git diff --stat translations\             # the revision record
.\build.ps1                               # validates, packs, compiles, zips
```

In-game evidence is required before any phase is called done: launch with the mod
deployed, read `QuasimorphThai.log`, screenshot the screens that phase touched.
"Compiles" is not "verified".

---

## 7. Risks

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| An edit breaks a `%TOKEN%` / `<color=…>` / `{0}` | Medium | Game prints a raw token | `apply_reviews.py` refuses the write; `check_translations.py` blocks the build |
| "Friendlier" drifts into cheerful and breaks tone | Medium | Ruins the game's voice | `STYLE.md` fixes register *per text class*; lore prose explicitly excluded |
| Terminology churn confuses returning v1.3 players | Medium | Wiki / community vocabulary mismatch | Glossary terms change only via an explicit decision + global re-apply |
| Longer Thai overflows narrow panels | Medium | Clipped labels | Phase-gate screenshot checks; the ~60-char space rule aids wrapping |
| Scope: a full A–D pass is ~100 review batches | High | Stalls before release | Phase 6 is deliberately targeted; tiers ship independently |
| Thai combining-mark clipping (open since v1.3) | Unknown | Unreadable marks | Inherited v1.3 Phase 6 item — check during phase-gate screenshots; port `AdjustThaiMarkGlyphMetrics` only if it shows |

---

## 8. Acceptance

- [ ] `STYLE.md` exists; `GLOSSARY.md` absorbs every term resolved in Phase 2
- [ ] `consistency.py`: zero same-English→different-Thai
- [ ] `check_translations.py`: zero problems, coverage still 11,352 / 11,352
- [ ] Tiers A, B, C revised; Tier D defect-swept
- [ ] Placeholder / tag byte-equality preserved on every edited cell
- [ ] v1.4 builds, deploys, loads clean; log confirms 11,352 translated
- [ ] Screens touched by each phase visually verified in game

---

## 9. Decisions taken

| # | Decision | Date | Rationale |
|---|---|---|---|
| 1 | **Depth: tiers A + B + C fully revised; tier D (mission/story) defect-swept only.** | 2026-08-31 | Owner's call. Polishes everything the player reads constantly (~45 review batches) without re-litigating 1 M chars of lore prose that is already in good shape. |
| 2 | **Target voice: Warhammer 40,000 — grimdark, gothic, ceremonial.** | 2026-08-31 | Owner's stated preference. Resolved as the two-layer contract in §3: chrome stays plain, clear and friendly; the world takes the elevated gothic register. Quasimorph's setting (Church of Revelation, Hexarchy, pacts, corporate dystopia) already supports it, and `story`/`bramfatura` are written in that voice today. |
| 3 | **Edits land in place in `translations/*.json`; `git diff` is the revision record.** | 2026-08-31 | One source of truth. An overlay directory would fork the translation and complicate the next game-update merge. |
| 4 | **Revision is keyed by distinct (EN, TH) pair, not by cell.** | 2026-08-31 | 11,352 cells collapse to 7,977 pairs — ~30 % less work, and identical source strings cannot drift apart again. |
