# Thai Glossary — Quasimorph

Terminology recovered from the author's own 1.2 translation (`work/corpus_cells.json`,
extracted from the old bsdiff patch). **Use these renderings** so 1.3 stays consistent with
the previously released mod. Style: concise; translate where Thai has a natural word,
transliterate game-specific proper nouns.

## Core nouns

| English | Thai |
|---|---|
| Operator | ผู้ปฏิบัติการ |
| Class | คลาส |
| Mission | ภารกิจ |
| Faction | ฝ่าย |
| Station | สถานี |
| Sector | เซกเตอร์ |
| Reputation | ชื่อเสียง |
| Contract | สัญญาจ้าง |
| Strategy | ยุทธศาสตร์ |
| Clone / cloning | โคลน / การโคลน |
| Corpse | ซากศพ |
| Turn | เทิร์น |
| Floor, deck level | ชั้น |
| Points | แต้ม |
| Resources | ทรัพยากร |
| Stash / cargo hold | คลังสัมภาระ |
| Chip | ชิป |

## Character systems

| English | Thai |
|---|---|
| Perk | เพิร์ก |
| Passive perk | เพิร์กติดตัว |
| Active perk | เพิร์กกระตุ้น |
| Trait | พรสวรรค์ |
| Wound | บาดแผล |
| Status effect | สถานะผิดปกติ |
| Addiction | ภาวะเสพติด |
| Implant | อวัยวะฝัง (also อิมพลันต์) |
| Augmentation | อวัยวะเสริม / ส่วนเสริมร่างกาย |
| Health / HP | พลังชีวิต / HP |
| Satiety | ระดับความอิ่ม |
| Pain threshold | ขีดทนความเจ็บ |

## Equipment

| English | Thai |
|---|---|
| Item | ไอเทม |
| Equipment / gear | อุปกรณ์ |
| Ordnance | ยุทโธปกรณ์ |
| Weapon | อาวุธ |
| Armor | เสื้อเกราะ |
| Armor slot | ช่องเสื้อเกราะ |
| Grenade | ระเบิดมือ |
| Reload | บรรจุอาวุธ |
| Slot | ช่อง |
| Backpack | เป้ |
| Craft(ing) | คราฟต์ |
| Disassemble | แยกชิ้นส่วน |
| Upgrade | อัปเกรด |

## Setting / proper nouns

| English | Thai |
|---|---|
| Quasimorphosis | ควอซิมอร์โฟซิส |
| Quasimorphic (storm) | (พายุ)ควอซิมอร์ฟิก |
| Hexarch | เฮกซาร์ค |
| The Church | ศาสนจักร |
| NCE | NCE (keep Latin) |

## Common UI phrases (author's wording)

| English | Thai |
|---|---|
| Continue | เล่นต่อ |
| Back | ย้อนกลับ |
| Pause | หยุดชั่วคราว |
| Close | ปิด |
| Exit | ออก |
| Settings | ตั้งค่า |
| None | ไม่มี |
| All | ทั้งหมด |
| Unavailable | ใช้ไม่ได้ |
| Not available yet | ยังใช้ไม่ได้ |
| Locked / blocked | ถูกบล็อก |
| Installed | ติดตั้งแล้ว |
| Press any key to continue | กดปุ่มใดก็ได้เพื่อดำเนินการต่อ |
| Hold to skip | กดค้างเพื่อข้าม |
| Safe zone | เขตปลอดภัย |
| Evacuate | อพยพ |
| Buy / Sell | ซื้อ / ขาย |
| Difficulty | ความยาก |
| Easy / Normal / Unfair / Custom | ง่าย / ปกติ / ไม่ยุติธรรม / กำหนดเอง |
| Tutorial | บทฝึกสอน |
| Random event | เหตุการณ์สุ่ม |

## Units / abbreviations

| English | Thai |
|---|---|
| wk. | สัป. |
| day | วัน |
| hr. | ชม. |
| min. | น. |
| kg | กก. |

## Rules

1. Keep placeholders (`{0}`, `{1}`, `%`) and rich-text tags (`<color=…>`, `<b>`) **exactly** as-is.
   This includes the dotted sub-object tokens `%Explosion.Damage%`, `%Grenade.Radius%`.
2. Never introduce a tab or a line break into a cell — it would shift every later column.
3. Keep stat abbreviations that the UI aligns in columns (HP, AP, XP) in Latin.
4. Prefer the author's existing wording above over a fresh choice, even where another
   translation would be defensible.

## Named entities — Latin vs. Thai

Decided 2026-08-27 while translating `station`, and applied from there on.

| Kind | Rule | Examples |
|---|---|---|
| Station / place proper names | **Keep Latin**, in the `.name` cell *and* in prose that mentions it | `Abyss-2199`, `Aynrand`, `Carcosa`, `Connelly-4` |
| Station short codes (`.shortname`) | **Keep Latin** — the map aligns them in columns | `ABS`, `AKK` |
| Corporation / PMC / squad names | **Keep Latin** (matches `class.*.name` from the 1.2 corpus) | `RealWare`, `Dilthey`, `Eclipse Blades`, `Tezctlan` |
| Latin mottos and taxon-style ability names | **Keep Latin** | `Solve et Coagula`, `Carnifex`, `Castigor Tenebrarum` |
| Bramfaturas, spirits, demons, deities | **Transliterate** | บรามฟาตูรา, คัตตารัม, ฟลาวรอส, ดักกูร์, สคริฟนัส, แกนนิกซ์, โอลีร์นา, รอน, ชาร์ตามาคุม, อูร์, กักตุงกร์, โมแรกซ์, ไพมอน, เชดู |
| Planets | Standard Thai astronomical names | ดาวพุธ, ดาวศุกร์, โลก, ดาวอังคาร, ดาวพฤหัสบดี, ดาวเสาร์, ดวงจันทร์ |
| Moons / asteroids | **Transliterate** | ไททัน, ยูโรปา, แกนีมีด, คัลลิสโต, ไอโอ, โฟบอส, ซีรีส, เวสตา, พัลลัส, ไฮเจีย, ไฮเปอเรียน, ฟีบี, เอนเซลาดัส |

Rationale for keeping station names Latin: the strategy map already prints the Latin
short code beside the name, and 271 missions plus 1,047 story cells refer to stations by
name in running prose — a Latin name is guaranteed to match its map label without
maintaining a 163-entry name map.

## Setting terms (recovered from the 1.2 corpus — do not re-coin)

| English | Thai |
|---|---|
| dignitas | `dignitas` (kept Latin) |
| gavvakh | กัฟวัค |
| bramfatura | บรามฟาตูรา |
| quasimorph / quasimorphic | ควอซิมอร์ฟ / ควอซิมอร์ฟิก |
| eccollapse / eccolapse | เอคโคแลปส์ |
| metamatter | เมทาแมตเทอร์ |
| Outer System / Inner System | ระบบสุริยะชั้นนอก / ระบบสุริยะชั้นใน |
| Main Belt / the Belt | แถบเข็มขัดหลัก / แถบเข็มขัด |
| Civil Resistance | ขบวนการต่อต้านพลเรือน |
| Church of Revelation | ศาสนจักรแห่งวิวรณ์ |
| Hexarchy / Hexarch | เฮกซาร์คี / เฮกซาร์ค |
| ancap (system, market) | อนาธิปไตยทุนนิยม |
| Great Anarchist Revolution / GAR | การปฏิวัติอนาธิปไตยครั้งใหญ่ / `GAR` |
| Solar Empire | จักรวรรดิสุริยะ |
| PMC / SBN / NCE | kept Latin |
| corporation | บรรษัท |

## Combat / ability wording (fixed while translating `perk`)

| English | Thai |
|---|---|
| Pact wielder | ผู้ถือสนธิสัญญา |
| pact | สนธิสัญญา |
| turn(s) | เทิร์น |
| trigger | ทริกเกอร์ |
| panic | ตื่นตระหนก |
| knockback | ผลักถอย |
| stun | มึนงง |
| rate of fire | อัตราการยิง |
| immobilize / root | ตรึงไว้กับที่ |
| pierce through | ทะลุทะลวง |
| resistances | ค่าต้านทาน |
| stabilize a wound | ห้ามเลือดบาดแผล |
| radius / area | รัศมี / พื้นที่ |
| lesser / greater / baron quasimorph | ควอซิมอร์ฟชั้นล่าง / ชั้นสูง / บารอน |
| Talent (perk type) | ความสามารถพิเศษ — *not* พรสวรรค์, which is already `trait` |

## Story-arc terms (fixed while translating `story` — do not re-coin)

Bramfaturas and their lords keep the transliterated Thai they already had in `faction` and
`perk`: อูร์ (Ur) / อูร์ปาร์ป (Urparp), แกนนิกซ์ (Gannix), โอลีร์นา (Olirna), โฟเคอร์มา
(Fokerma), ดักกูร์ (Duggur), กักตุงกร์ (Gagtungr). `Skrivnus` stays Latin — the only
existing use (`item.skrivnus_knife`) never transliterated it.

| English | Thai | Note |
|---|---|---|
| Hellstrom Network | เครือข่าย Hellstrom | person's name stays Latin |
| Hellstrom Index | ดัชนี Hellstrom | |
| Red Chip | ชิปแดง | |
| colnode | คอลโนด | |
| Jamming Node | โหนดก่อกวนสัญญาณ | |
| Engram Control Server | เซิร์ฟเวอร์ควบคุมเอ็นแกรม | |
| the Hive | Hive | AnCom's post-merge form — kept Latin, as in `104a_spaceobject` |
| Inner Rite | คณะพิธีวงใน | |
| AnCom Sanctuary / Sanctum | สถานศักดิ์สิทธิ์ AnCom | |
| the Ark | นาวา | the Hive's salvation project |
| Andreev Emitter | ตัวปล่อยคลื่นอันดรีเยฟ | |
| geofront / Geofront Mast | จีโอฟรอนต์ / เสาจีโอฟรอนต์ | |
| Earth Interdict | ข้อห้ามแห่งโลก | |
| phasonuclear | เฟโซนิวเคลียร์ | cf. ระเบิดเฟส = Phase Bomb |
| Telephaturic Dislocator | เครื่องเคลื่อนย้ายเทเลฟาตูริก | matches `item.quest_dislocator_part` |
| Monolith | โมโนลิธ | matches `item.quest_secret_data` |
| Skull of Destiny | กะโหลกแห่งโชคชะตา | |
| Casket of Silence | หีบแห่งความเงียบ | |
| Nakal | นาคัล | Tezctlan's three: นานาอัวตซิน / โตนาติอู / โตปิลต์ซิน |
| gavpan | กัฟปาน | vault of gavvakh |
| Pleroma / Kenoma | เพลโรมา / เคโนมา | |
| Precentor | พรีเซนเตอร์ | RealWare's head-of-corporation title |
| lugal | ลูกัล | Sumerian king title, used of Urparp |
| Red and Gold Throne | บัลลังก์แดงและทอง | |
| Blood Warriors | นักรบเลือด | Shedu's Thousand's self-name |
| rokosz | การกบฏขุนนาง | mission title `Rokosz` |

Mission titles are translated unless the title is itself a proper name: `Malleus
Maleficarum` and `Odoacer` stay Latin; `Treasure Island` → เกาะมหาสมบัติ, `The Glass Bead
Game` → เกมลูกแก้ว, `Reap the Whirlwind` → เก็บเกี่ยวลมบ้าหมู, `Flower War` → สงครามดอกไม้.

Station and ship names always stay Latin (`Hargeysa`, `Carcosa`, `Flat Obsidian`,
`Sinkhole Oasis`, `HIS Ares`, `Feathered Temple`, `Humankind's Hope Citadel`), but the
bodies they orbit are transliterated (เวสตา, พัลลัส, ไททัน, ไฮเปอเรียน, โฟบอส) —
`Interamnia` and `Hecate` are the exceptions, kept Latin by `097*_station_desc`.

## Resolved during the v1.4 consistency sweep (2026-08-31)

An ability that exists both as a skull **item** and as the **perk** it grants had drifted
into two Thai names in ~45 cases, so the player met one ability under two names. Resolved
systematically:

| Case | Rule applied | Winner |
|---|---|---|
| Pact-ability name pairs (`item.skull_*` ↔ `perk.*`) | `STYLE.md` §3: ceremonial naming uses `…แห่ง…` | the **perk** form |
| Implant / device pairs (`item.*` ↔ the perk it grants) | The item is a physical device; name both after it | the **item** form |
| Transliteration conflicts | This glossary decides | see below |
| Latin-vs-Thai conflicts | Latin ability names and station names stay Latin | Latin |

Transliterations fixed to one form:

| English | Thai | Was also |
|---|---|---|
| Urparp | อูร์ปาร์ป | อูร์พาร์ป |
| Duggur | ดักกูร์ | ดุกกูร์ |
| Gnyann | กนีอันน์ | ญัน |
| Vincar | วินคาร์ | วินการ์ |
| Fukabirna | ฟูคาบีร์นา | ฟูกาบีร์นา |
| Hematite | ฮีมาไทต์ | เฮมาไทต์ |
| Tlenamacac | ตเลนามากัก | ตเลนามาคัก |
| Khatm as-Sihr | คัตม์ อัส-ซิห์ร | คอตัม อัซซิฮ์ร |
| Ash-Shakin | อัช-ชากิน | อัชชะกิน |
| al-Abarsa | อัล-อาบาร์ซา | อัลอาบาร์ซา |
| Chöd | เชอด | เชอ |
| Mujtahid | มุจญ์ตะฮิด | มุจตะฮิด |
| Tonal | โตนัล | โทนัล |
| Tonatiuh | โตนาติอู | โทนาติอุห์ |
| Topiltzin | โตปิลต์ซิน | โทปิลต์ซิน |
| Tezcatlipoca | เตซกัตลีโปกา | เทซคัตลิโปกา |
| Nagual / Nawal | นาวาล | นากูอัล |
| Quauhxicalli | กวาวชิกัลลี | เกาชิกัลลี |
| Cathbad | แคธแบด | คาธบาด |
| Kangling | คังลิง | กังลิง |
| Hafizun | ฮาฟีซุน | ฮาฟิซุน |
| Voglea | วอกเลีย | โวเกลอา |

A second pass found **32 more** of these pairs that the first had missed: the source
table writes the same name with a curly apostrophe in the skull item (`Tonal’s Wound
Binding`) and a straight one in the perk (`Tonal's`), so a whole-cell comparison never put
them side by side. `tools/consistency.py` now folds typographic variants together before
grouping, and `_corpus.canonical()` is shared so `make_reviews.py` groups identically.

Outright errors corrected: `spaceobject.volcano.name` said **`Vulcan`** (a different word
entirely — now `Volcano`, Latin, as station and space-object names are); `Sting` was
เหล็กไน, a misspelling of เหล็กใน; `Castigor Tenebrarum` and `Carnifex` were transliterated
in the item cell but Latin in the perk cell, against the rule above.

`Addiction` now uses this glossary's **ภาวะเสพติด** everywhere (ภาวะเสพติดยา /
ภาวะเสพติดนิโคติน / ภาวะเสพติดกัฟวัค), replacing the ad-hoc การติด… and ติด… pair.
`tooltip.Power` said อำนาจ — political authority — where the stat means พลัง.

**Divergences that are correct and must stay** are recorded in `consistency_allow.json`
with a reason each, and `tools/consistency.py --strict` fails on anything not listed there.
`hit` is deliberately ตะปบ for a claw, ต่อย for a punch and ทุบ for a bludgeon; `Max` is
สูงสุด in the UI and Maximilian Rohr's name elsewhere.
