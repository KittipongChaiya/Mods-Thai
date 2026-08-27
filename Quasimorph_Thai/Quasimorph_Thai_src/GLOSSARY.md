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
2. Never introduce a tab or a line break into a cell — it would shift every later column.
3. Keep stat abbreviations that the UI aligns in columns (HP, AP, XP) in Latin.
4. Prefer the author's existing wording above over a fresh choice, even where another
   translation would be defensible.
