# Thai Style Guide — Quasimorph

Binding for the **v1.4 language refinement pass** and everything after it.

`GLOSSARY.md` decides **which word**. This file decides **how the sentence is built and
how it sounds**. When the two disagree, `GLOSSARY.md` wins on terminology and this file
wins on register and syntax.

---

## 0. The one rule everything else follows

> **The chrome is plain and clear. The world is grimdark.**

Two layers, with a hard boundary between them:

| | Layer 1 — Chrome | Layer 2 — World |
|---|---|---|
| What it is | The game talking to the player **as software** | The game talking to the player **as fiction** |
| Goal | Understand it in one glance | Feel the setting |
| Register | Plain, short, friendly | Elevated, gothic, ceremonial |
| Target voice | A well-made Thai app | Warhammer 40,000 rendered in Thai |

The v1.4 brief was "better, easier to understand, more friendly" **and** "a 40K feel".
Those only conflict if one register is applied to everything. Split by layer and both are
satisfied: the interface gets friendlier, the fiction gets darker, and both get clearer.

**Elevated is not the same as convoluted.** Layer 2 raises the *vocabulary* and the
*cadence*. It never raises the sentence length or the clause depth. A grimdark line that
has to be read twice has failed.

---

## 1. Which layer is a cell in?

Decide by **what the text does**, not by its key prefix — several prefixes contain both.

### Layer 1 — Chrome

| Class | Examples of keys |
|---|---|
| Buttons, labels, menu items | `ui.label.*`, `ui.caption.*`, `ui.context.*`, `ui.button.*` |
| Tooltips and stat names | `tooltip.*`, `ui.tooltip.*`, `ui.resist.*` |
| Settings, key bindings, controls | `ui.settings.*`, `ui.controls.*`, `gamekey.*` |
| System, error and confirmation messages | `ui.dialog.*` that asks a question, `notification.*` |
| Tutorial and instructional text | `ui.dialog.spacemodetutorial_*`, `tutorial.*` |
| **Mechanic descriptions** — what a thing *does* | `perk.*.desc`, `item.*.shortdesc`, `wound*.*`, `trait.*.desc`, `mgperk.*`, `firemode.*` |
| Item / equipment class labels | `item.class.*`, `armor.*`, `spec.*` |

### Layer 2 — World

| Class | Examples of keys |
|---|---|
| Faction and corporation dossiers | `faction.*.desc`, `factiontype.*`, `alliance.*` |
| Church, pacts, curses, rites | `pact.*`, `curse.*`, `perk.*` belonging to a pact |
| Bramfaturas, demons, quasimorphs | `bramfatura.*`, `ui.dialog.bramfatura_orbit.*`, `ui.dialog.event_bram_*` |
| Station, space-object, monster prose | `station.*.desc`, `spaceobject.*.desc`, `monster.*.desc` |
| Terminals, in-world documents, news | `terminal.*`, `ui.newsline.*`, `ui.dialog.event_*` |
| Story and mission prose | `story.*`, `mission.*` |
| **Names** of perks, items, missions, pacts | `perk.*.name`, `item.*.name`, `mission.*.names` |
| Outro / intro narration | `ui.outro.*`, `ui.intro.*` |

### The split inside one object

An item or perk is usually **both**. Its *name* is Layer 2; its *mechanic line* is Layer 1.

```
perk.mars_idol_of_ur.name    Red and Gold Idol
  -> เทวรูปแดงและทอง                         Layer 2 — ceremonial

perk.levelUpPassive_AnyThrowKill    Each kill with a thrown weapon grants {0} XP.
  -> สังหารศัตรูด้วยอาวุธขว้าง รับ {0} XP ต่อครั้ง   Layer 1 — plain, verb-first
```

---

## 2. Layer 1 — Chrome

### Voice

- Address the player as **`คุณ`**, and only when the sentence needs a subject.
- **No `ครับ` / `ค่ะ`.** Polite particles make a system message sound like a shop
  assistant and cost horizontal space in narrow panels.
- No archaic pronouns, no `ท่าน`, no ceremonial vocabulary. Ever.
- Friendly means *direct and unfussy*, not chatty.

### Form

| Class | Form | Example |
|---|---|---|
| Button / label | Bare verb or bare noun. **Never a sentence.** | `เปิด` · `ยกเลิก` · `เซฟใหม่` |
| Tooltip / stat name | Bare noun phrase, no article, no verb | `ความแม่นยำโดยรวม` · `ต้านทานธาตุ` |
| Toggle / setting | Describe the state being switched on | `ปิดเสียงเกมเมื่อสลับออกจากหน้าต่าง` |
| Confirmation question | Full sentence, `คุณ`, ends in `หรือไม่?` | `คุณต้องการฝึกต่อหรือไม่?` |
| Refusal / error | State the fact, not the blame | `เปลี่ยนปุ่มนี้ไม่ได้` — not `คุณไม่สามารถ…` |
| Mechanic description | **Verb first. One clause per effect. Numbers last.** | `สังหารศัตรูด้วยอาวุธขว้าง รับ {0} XP ต่อครั้ง` |

### Mechanic descriptions — the highest-traffic text in the game

1. **Lead with the verb the player performs, or the effect that happens.**
   Not `การสังหาร…ให้ …` but `สังหาร… รับ …`.
2. **One clause per effect.** Two effects, two clauses separated by a space.
3. **Numbers and tokens go late**, after the reader knows what they modify.
4. **Say the trigger before the result**, matching the order of play:
   `ยิงกระสุนนัดสุดท้าย จะกระตุ้นทริกเกอร์`
5. Keep `HP`, `AP`, `XP` in Latin (`GLOSSARY.md` rule 3) — the UI aligns them in columns.

```
perk.flauros_spiked_vines.desc
  EN   Fills a target area with a radius of %IRadius% with thorn vines.
  WAS  เติมเถาหนามลงในพื้นที่เป้าหมายรัศมี %IRadius%     "fills…with" carried over literally
  NOW  ปกคลุมพื้นที่รัศมี %IRadius% ด้วยเถาหนาม

perk.venus_sacrifice.desc
  WAS  …แปลง HP ปัจจุบันของมันแต่ละแต้มเป็นแต้มโล่…      "มัน" for an ally; one long clause
  NOW  สังหารพันธมิตรเป้าหมาย แล้วเปลี่ยน HP ที่เหลือของเขาทุก 1 แต้ม
       เป็นโล่ %FShieldCapacityModifier% แต้มให้ผู้ถือสนธิสัญญา
```

### Tutorial

The tutorial is Layer 1 and stays **the warmest text in the game** — it is a new player's
first hour. Keep the existing conversational narrator (`บอส` for the player, first person
for the speaker). Short sentences, one idea each. Do not raise its register.

---

## 3. Layer 2 — World

### Voice by speaker

| Speaker | Pronouns | Cadence |
|---|---|---|
| Bramfaturas, demons, quasimorphs | `ข้า` / `เจ้า` | Imperious, archaic, imperative: `จง…เถิด` |
| Church of Revelation, rites, pacts | `เรา` / `ท่าน` | Liturgical: `แห่ง…`, `ผู้…`, `จง…` |
| Corporations, PMCs, official dossiers | none / `บรรษัท` by name | Formal-bureaucratic and faintly pompous |
| News feeds, vox reports, combat log | none | Terse, factual, clipped — never casual |
| Station / monster / object prose | 3rd person | Neutral-literary and bleak |

### Rules

1. **`มัน` never refers to a person, a faction, a corporation, or the Church.**
   Repeat the name, use `บรรษัท` / `ศาสนจักร` / `องค์กร`, or drop the subject entirely.
   Thai tolerates a dropped subject far better than an inanimate pronoun for an institution.
2. **Corporate irony is preserved, not flattened.** Quasimorph's corporations describe
   atrocities in marketing language. Render that as bureaucratic Thai, not as neutral Thai.
3. **Ceremonial naming uses the existing `…แห่ง…` pattern** already established in the
   translation: `บัลลังก์แดงและทอง`, `ราคาแห่งบัรซัค`, `กะโหลกแห่งโชคชะตา`.
4. **Keep the darkness.** Do not soften violence, blasphemy or despair. "Friendlier" is a
   Layer 1 instruction and has no force here.

### Grimdark lexicon

Where Thai offers both a plain and an elevated word, **Layer 2 takes the elevated one** —
provided it does not cost comprehension.

| Plain | Layer 2 |
|---|---|
| ตาย | มรณะ · ดับสูญ · สิ้นชีพ |
| ฆ่า | สังหาร · ประหาร |
| ทำลาย | ทำลายล้าง · ล้างผลาญ |
| กำจัด (of heretics) | ชำระล้าง |
| ความเชื่อ | ศรัทธา |
| คำสั่ง (of Church / Hexarchy) | พระบัญชา · อาชญา |
| คนบาป | ผู้ต้องสาป · ผู้นอกรีต |
| จิตใจ | ดวงจิต · วิญญาณ |
| เริ่มต้น (of a rite) | ประกอบ · ก่อกำเนิด |
| ให้บริการ (of the Church) | ประกอบศาสนกิจ · ปรนนิบัติ |
| ทุกคน | ทุกดวงจิต · ผองชน |
| ร่างกาย | สรีระ · เรือนร่าง |
| น่ากลัว | น่าสะพรึง · อัปมงคล |

```
faction.ChurchRevelation.desc
  WAS  …มันให้บริการทางศาสนาคุณภาพสูงบนพื้นฐานเชิงพาณิชย์
       เป้าหมายหลักของศาสนจักรคือการตอบสนองความต้องการทางจิตวิญญาณของทุกคน…
  NOW  …ศาสนจักรประกอบศาสนกิจชั้นเลิศบนรากฐานแห่งการค้า
       พันธกิจของศาสนจักรคือปรนนิบัติดวงจิตทุกดวงที่แสวงหาวิวรณ์…
```

`มัน` is gone, the corporate-religion irony lands harder in liturgical diction, and the
sentence is shorter than it was.

### Hard limits

- **No ราชาศัพท์** (Thai royal register). It reads as the Thai monarchy, not as gothic
  science fiction. `พระบัญชา` is as far as it goes, and only for the Church and the Hexarchy.
- **No Layer 2 vocabulary in Layer 1.** A settings toggle is never ceremonial.
- **Comprehension outranks atmosphere.** If an elevated word makes a mechanic ambiguous or
  a proper noun unrecognisable, the plain word wins. Every time.
- **No invented lore.** Elevating the register must not add imagery the English does not have.

---

## 4. Syntax rules (both layers)

Applied everywhere. These are what make the translation stop reading like a translation.

1. **Prefer a verb to `การ`+verb.** `การสังหาร…ให้…` → `สังหาร… รับ…`
   Nominalisation is correct Thai but stacking it is the single biggest source of
   stiffness in the v1.3 text (908 cells carry two or more).
2. **At most one `ซึ่ง` per sentence.** Prefer splitting into two sentences.
   `ที่ซึ่ง` is almost always avoidable.
3. **`ถูก` only for genuine adversity-passive.** Otherwise reorder to active voice.
   *Layer 2 exception*: `ถูก` is welcome where the doom is the point (`ผู้ถูกสาป`).
4. **`โดย` is not an all-purpose "by".** For an instrument use `ด้วย`; for an agent
   prefer restructuring to active voice.
5. **Break long runs.** Insert a space at phrase boundaries so no unbroken run exceeds
   **~60 characters**. This is not cosmetic: Unity TMP wraps Thai on spaces, so an
   unbroken run is what overflows a narrow panel. Semicolon-delimited list cells
   (`mission.*.names`) are exempt — the game splits them itself.
   A real space is the only lever available: **U+200B (ZWSP) is not in the shipped
   atlas** and must never be used for this.
6. **One idea per sentence.** If a Thai sentence needs a comma to survive, split it.

---

## 5. What the font can render

`assets/quasimorph_tahoma_tmp.bundle` ships a **static atlas of 179 glyphs** — ASCII plus
84 Thai characters — but it is a **dynamic** TMP asset (`m_AtlasPopulationMode = 1`) with
the `tahoma` `Font` object embedded beside it, so anything outside the atlas is rasterized
at runtime from `tahoma.ttf` (3,772 codepoints, full Thai + Latin-1 + General Punctuation).

Verified 2026-08-31 by static analysis: every character the translation currently uses and
the atlas lacks — `“` `”` (91× each), **`ฤ` (89×, e.g. ฤทธิ์, พฤหัสบดี)**, `ฯ`, `…`, `’`,
`ö`, `ü`, `é`, `ì`, Cyrillic `С` — is present in `tahoma.ttf`. Nothing renders as tofu.

Two rules follow:

- **`tahoma.ttf` is the hard boundary.** A character it does not have cannot render at all.
  `tools/check_font.py` enforces this, so an editing pass cannot silently introduce one.
- **Prefer ASCII `...` to `…`.** Both render, but `.` is in the static atlas while `…`
  depends on runtime rasterization, and 128 of the 131 affected cells already use `...`.
  Normalise the three outliers **down** to `...`, not the other way round.

> This reverses an earlier draft of this guide, which called for `…`. That would have
> pushed 128 cells onto the runtime path for no benefit. Where the atlas already covers a
> character, use it.

`ฤ` is the item to confirm on screen first: it is the most-used character that is not in
the static atlas, so it is the single best test that dynamic population is working.

---

## 6. Mechanical invariants

Enforced by `tools/check_translations.py` (hard, blocks the build) and reported by
`tools/check_style.py` (advisory).

| Invariant | Enforced by |
|---|---|
| `{0}`, `%TOKEN%`, `%Obj.Field:Resolver%` byte-identical to English | hard |
| `<color=…>`, `<b>`, `<br>` byte-identical to English | hard |
| No tab, no line break in a cell | hard |
| Every character exists in `tahoma.ttf` | hard |
| No leading or trailing whitespace | hard |
| One Thai rendering per English string | hard (from Phase 2 on) |
| `...` not `…` (see §5) | hard |
| No `ของมัน` / `มัน` for a person or organisation | advisory |
| No unbroken run > 60 chars | advisory |
| ≤ 1 `ซึ่ง`; no stacked `การ` | advisory |
| Glossary terms used as written | advisory |

Advisory findings are judgement calls — a flagged cell may legitimately keep its wording.
They are a reading list, not a rewrite list.

---

## 7. What must not change

- **Terminology already in `GLOSSARY.md`.** Changing a term is a glossary edit first, then
  a global re-apply — never a one-off in a single cell. Returning v1.3 players and the
  community wiki depend on stable vocabulary.
- **Latin-script proper nouns.** Station names, corporations, PMCs, Latin mottos and
  alphanumeric model designations stay Latin, per `GLOSSARY.md`.
- **Story and mission prose voice.** In v1.4 these get a defect sweep only — pronoun
  misuse, glossary drift, long runs, ellipses. Their literary voice is already correct and
  is not to be rewritten.
- **Meaning.** This is an editing pass on a finished translation. If a rewrite changes what
  a line means, it is a bug, not an improvement.
