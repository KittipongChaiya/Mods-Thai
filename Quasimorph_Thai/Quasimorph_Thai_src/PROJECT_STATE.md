# Project State — Quasimorph Thai Mod

**Mod version**: 1.2 (old, game 0.9.9) → **1.3 in progress** (game 1.0.3)
**Target game**: `1.0.3.578s.024ad60`, Unity `2022.3.62f2`
**Phase**: 3 of 5 — Translate the table | **Status**: COMPLETE
**Updated**: 2026-08-28

## Architecture (validated end to end on 2026-08-27)

The mod no longer patches any game file. It uses Quasimorph's **official mod API**:

| Piece | How |
|---|---|
| Mod discovery | Folder under `%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphThai\` with a `modmanifest.json` |
| Load | `MGSC.UserModSystem.LoadCustomPresets` → `Assembly.LoadFrom` → `GrabMethods` finds `[Hook]` methods |
| Thai text | `[Hook(ModHookType.ResourcesLoad)]` → registered into `MGSC.CustomResources`. We load the game's **own** table via `Resources.Load("localization")` and lay Thai over its English column **by key**, then return the merged `TextAsset`. Merging (not replacing) is what makes this survive a game update: text the game adds stays English instead of rendering as a raw key, and the other ten languages always come from the installed version. Ships only `thai_overrides.tsv.gz` (142 KB), not a full 6 MB table. |
| Thai font | `[Hook(ModHookType.MainMenuStarted)]` → sets `FontPreset._font` (the preset serving `Lang.EnglishUS`) to the bundled Tahoma TMP asset |
| Uninstall | Delete the folder |

Thai is written into the **English column**; the header's column-1 cell becomes `ไทย`, so the
player picks it where "English" used to be. Unchanged from the 1.2 design.

**Proven on 2026-08-27**: title screen rendered `ไทย │ PRESS ANY KEY TO CONTINUE` with correct
Thai glyphs. Log clean, no exceptions, one table load, `Mod QuasimorphThai loaded.` in Player.log.

## Environment

| Item | Value |
|---|---|
| Game root | `C:\Users\Administrator\Desktop\Quasimorph.v1.0.3\game` (Goldberg repack — **Steam "Verify integrity" rollback unavailable**, but irrelevant now: no game file is touched) |
| Localization source | TextAsset `localization`, path_id 7646, 15,691,408 B, TSV/CRLF, **18 columns**, 11,577 lines (1 header + 11,576 rows), 11,352 non-empty English cells |
| Header | `['', 'English', 'Russian', 'German', 'Frenсh', ...]` — **`Frenсh` has a Cyrillic `с` (U+0441); preserve byte-exact** |
| Build tools | Python 3.13 + UnityPy 1.25.3; .NET SDK 8.0.424 at `~\.dotnet` (needs `DOTNET_ROOT`); ilspycmd 8.2.0.7535 (needs `DOTNET_ROLL_FORWARD=Major`) |

## Decisions

- 2026-08-27 — **Dropped the bsdiff delivery entirely.** It was byte-locked to the 0.9.9
  `resources.assets` and was the direct cause of the failed install. Replaced with the game's
  own `CustomResources` override hook.
- 2026-08-27 — **Dropped BepInEx / winhttp.dll / doorstop.** The game has a first-class mod
  loader; the whole BepInEx payload is unnecessary.
- 2026-08-27 — Self-repair check is **outcome-based** (`Localization.Get("ui.lang", Lang.EnglishUS)`
  contains Thai?) rather than tracking our own "did the hook fire" flag. The flag version produced
  a false-positive reload warning; asking the game the actual question is simpler and correct
  regardless of startup ordering.
- 2026-08-27 — Reused the existing `quasimorph_tahoma_tmp.bundle` (built with Unity 2020.3.9f1).
  **Verified it loads fine under 2022.3.62f2**, so no Unity Editor is needed.
- 2026-08-27 — Font field is located **by type** (`TMP_FontAsset`) with `_font` as the fast path,
  so a future rename does not silently disable Thai.

## Completed

- [x] Phase 0 — Root-caused the failed install (bsdiff base-hash mismatch; fails safe, game
      never modified). Mapped the localization asset and table shape.
- [x] Phase 1 — Found and validated the official mod API. Decompiled `Localization`,
      `CustomResources`, `UserModSystem`, `FontPreset`, `LocalizationFontKeeper`.
- [x] Phase 2 — Recovered the old Thai corpus from the 1.2 bsdiff: **10,348 ordered Thai cells**
      (73.3% fully literal, only 42 unrecoverable). **Keys are NOT recoverable** — 0.9.9's row
      order differs from 1.0.3's and the tail-length equation is hopelessly ambiguous (median
      335,785 candidate matches per cell, zero unique). Corpus is therefore a
      **glossary / translation memory**, not a key→value map. Saved to `work/corpus_cells.json`.
- [x] Phase 4 — Mod assembly written, builds clean, loads, serves the table, applies the font.
- [x] **Phase 3 — Translate the 1.0.3 table into Thai. 11,352 / 11,352 cells (100%).**
      Finished 2026-08-28. Every prefix is done, `check_translations.py` reports zero
      problems, and the in-game log confirms `11577 rows, 11352 translated, 0 left in
      English`.

## Active

_Nothing in progress._

### Translation loop (resumable — repeat until coverage is 100%)

```powershell
$P="C:\Users\Administrator\Desktop\Quasimorph_Thai_src"
# 1. See what is left and regenerate work batches (skips anything already translated)
python "$P\tools\make_batches.py" --stats
python "$P\tools\make_batches.py" --emit --budget 20000
# 2. Read work\batches\NNN_prefix.json, translate, write translations\NNN_prefix.json
#    (key -> Thai). Large batches may be split into NNNa / NNNb files.
# 3. Validate - placeholders, %TOKENS%, rich-text tags, tabs, duplicate keys
python "$P\tools\check_translations.py"
# 4. Rebuild + deploy + verify in game (NOTE: never name the splat variable $args)
$cmdArgs=@("$P\tools\build_table.py","$P\work\localization_base.tsv","$P\build\mod\localization_th.tsv","--gzip")
Get-ChildItem "$P\translations\*.json" | ForEach-Object { $cmdArgs+="--translations"; $cmdArgs+=$_.FullName }
& python @cmdArgs
```

**Progress: 11,352 / 11,352 cells (100%) — all validation passing.**
Every prefix is translated, including the `mission` (648 distinct phrases) and `story`
(999 distinct phrases) lore bulk. The loop above is kept for the next game update, when
`make_batches.py --stats` will list whatever new keys the update introduced.

### Index-keyed workflow for the lore bulk (`mission`, `story`)

`mission` and `story` are far too repetitive to translate cell by cell (1,406 cells from
only 648 distinct strings) and far too large to re-type the English keys for
`translate_repetitive.py`. So they use an extra indirection:

```powershell
# 1. freeze the distinct-phrase list (an ordered JSON array)
#    -> work/mission_distinct.json, work/story_distinct.json
# 2. read a character-budgeted slice
python tools\dump_phrases.py work\story_distinct.json --start 0 --budget 11000
# 3. write work\story_th_NNN.json as { "<index>": "<Thai>" }
# 4. join index -> English -> Thai and apply
python tools\build_phrases.py work\story_distinct.json "work\story_th_*.json" -o work\phrases_story.json
python tools\translate_repetitive.py work\phrases_story.json translations\111_story.json work\batches\001_story.json
python tools\check_translations.py
```

`build_phrases.py` reports any duplicate/out-of-range/empty index, so a slice cannot be
silently half-applied. `translate_repetitive.py` writes a `.todo.json` skeleton while
coverage is partial — delete it, it is not part of the translation.

Done: **everything**. For the record, by prefix: **all `ui`**, **all `item`**, **all `monster`**, **all `station`**, **all `perk`**,
**all `mgperk`**, **all `faction`**, **all `bramfatura`**, **all `terminal`**,
**all `tutorial`**, **all `spaceobject`**, `alliance`, `armor`, `class`, `factiontype`,
`missiontype`, `name`, `notification`, `pact`, `spec`, `weapon`, `woundtype`, `firemode`,
`gamekey`, `strategy`, `wound`, `trait`, `woundeffect`, `curse`, `woundslot`, `tooltip`,
**all `mission`** (1,406 cells / 648 distinct), **all `story`** (1,047 cells / 999 distinct).
Nothing remains.

**2026-08-27 session — verified the premise first.** Re-extracted the live `localization`
TextAsset from the installed game and diffed it against `work/localization_base.tsv`:
byte-identical (15,691,408 B), 0 keys added, 0 English cells changed. So there are **no
"newly added" keys from a game update** — the English text still visible in game is simply
the not-yet-translated remainder, and the deployed mod was already in sync with the last
build.

**Tip for mechanical batches** (`woundslot` was 488 cells from only 149 distinct phrases):
use `tools/translate_repetitive.py <batch> <phrases.json> <out>`. Run it once with an empty
`{}` dictionary to list every distinct phrase, write the dictionary, then re-run. It never
guesses — uncovered phrases are reported, not invented.

**v1.3 is packaged and delivered** to `Quasimorph_Thai\Quasimorph_Thai_v1.3\` (+ .zip),
rebuilt 2026-08-28 with the complete translation (`thai_overrides.tsv.gz`, 477,854 B) and
deployed to `LocalUserPresets\QuasimorphThai\`. `build.ps1 -OutDir` now defaults to that
in-repo folder instead of the old Desktop path, which no longer exists.

**Translation rules**: follow `GLOSSARY.md`. Keep `{0}`, `%TOKEN%`, `<color=…>`, `<br>`
byte-identical. Keep alphanumeric model designations (`RPG-77`, `Starlock MD-6`, `A.R.C.`)
in Latin; translate descriptive names and every `.shortdesc`. Never introduce a tab or
newline into a cell.

## Pending

- [ ] Phase 5 — Package + installer rewrite (plain copy into `LocalUserPresets`; no admin, no
      backup, no hash gating) and rewrite `วิธีติดตั้ง.txt`.
- [ ] Phase 6 — Full playthrough validation pass; check long descriptions and Thai mark
      rendering (the 1.2 plugin had `AdjustThaiMarkGlyphMetrics`, not yet ported — re-check
      whether 1.0.3 still needs it).

## Known risks / open items

- Thai combining-mark vertical positioning not yet visually verified beyond the title screen.
  The old plugin adjusted glyph metrics; port only if screenshots show clipping.
- Steam Workshop distribution untested (this copy cannot use it). Local folder install works.

- 2026-08-27 — **Merge at runtime instead of replacing the table.** Replacing it meant a
  game update would leave newly-added keys absent, and `Localization.Get` returns the key
  itself when missing — players would have seen `ui.some.new.thing` on screen. Merging over
  the game's live table removes that failure mode entirely and cut the shipped payload from
  6 MB to 142 KB.
- 2026-08-27 — Project restructured: font bundle vendored to `assets/`, obsolete 1.2
  delivery moved to `archive/v1.2_payload/` (kept — `resources.assets.bsdiff` is the only
  original copy of the 1.2 Thai translation). `build.ps1` builds + packages in one command.
  `README.md` documents the layout and the game-update procedure.

## Repo layout

Project folder: `Desktop\Quasimorph_Thai_src` (renamed from `Quasimorph_Thai_v1.2` on
2026-08-27 — the old name described the delivery it once held, not the project it became).
All tools resolve paths relative to their own location, so the folder can be renamed or
moved freely. See `README.md` for what must never be deleted.

```
README.md                 layout + what to do when the game updates
build.ps1                 validate -> pack overrides -> compile -> stage -> zip
mod_src/QuasimorphThai/   C# mod (ThaiMod, ThaiTable, ThaiFont, ModLog, Diagnostics)
assets/                   Thai TMP font bundle (+ the tahoma.ttf it was built from)
tools/                    build-time Python (extract_table, build_overrides,
                          make_batches, check_translations, translate_repetitive, ...)
translations/             key → Thai JSON — THE TRANSLATION WORK, never delete
work/                     regenerable intermediates; corpus_cells.json is the recovered
                          1.2 translation, keep it
build/                    compiler + staging output, rebuilt by build.ps1
archive/v1.2_payload/     the old 1.2 delivery (BepInEx, doorstop, resources.assets.bsdiff).
                          Unused by the build; the bsdiff is the only original copy of the
                          1.2 Thai translation, so it is archived rather than deleted.
```

## Next Action

Phase 6 — playthrough validation. The translation is complete and the mod is deployed, but
only the startup path has been verified programmatically (log: 11,352 translated, font
loaded and applied, no exceptions). What still needs a human pass:

1. Play far enough to see long `story.*.details` briefings and check Thai combining marks
   (สระ / วรรณยุกต์) are not clipped — the 1.2 plugin had `AdjustThaiMarkGlyphMetrics`,
   not yet ported. Port it only if screenshots show clipping.
2. Spot-check a station screen, a perk tooltip, and a mission briefing for line-wrapping
   and overflow in the narrower UI panels.

Then Phase 5 (installer + `วิธีติดตั้ง.txt` rewrite), which is still pending.
