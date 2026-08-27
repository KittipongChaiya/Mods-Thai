# Quasimorph Thai — source project

> **Do not delete this folder.** The shipped `.zip` contains only compiled output — the
> translations, tooling and mod source live *here* and nowhere else. Losing this folder
> means losing the translation work.
>
> Renaming it is safe. Every tool resolves paths relative to its own location, so the
> folder can be called anything and moved anywhere.
>
> *(Formerly `Quasimorph_Thai_v1.2` — the old name described the delivery it once held,
> not the project it grew into.)*

## What is in here

| Path | What it is | Safe to delete? |
|---|---|---|
| `translations/` | **The translation work.** key → Thai, one JSON per batch. | **Never** |
| `mod_src/QuasimorphThai/` | C# source of the mod (+ `modmanifest.json`) | **Never** |
| `assets/` | Thai TMP font bundle + `tahoma.ttf` it was built from | **Never** |
| `tools/` | Build + validation scripts | **Never** |
| `build.ps1` | One-command build → release folder + zip | **Never** |
| `GLOSSARY.md` | Agreed Thai renderings; keeps terminology consistent | **Never** |
| `PROJECT_STATE.md` | Progress, decisions, how to resume | **Never** |
| `work/` | Regenerable intermediates (base table, batches, phrase dicts) | Regenerable, but `corpus_cells.json` is the recovered 1.2 translation — keep it |
| `build/` | Compiler/staging output | Yes, rebuilt by `build.ps1` |
| `archive/v1.2_payload/` | The **old** 1.2 delivery: BepInEx, doorstop, `resources.assets.bsdiff` | Not used by the build. `resources.assets.bsdiff` is the only original copy of your 1.2 Thai translation — archive it somewhere before deleting. |

## Building

```powershell
.\build.ps1
# or, for a game installed elsewhere:
.\build.ps1 -GameManaged "D:\Quasimorph\Quasimorph_Data\Managed"
```

Produces `Desktop\Quasimorph_Thai_v1.3\` and a matching `.zip`. It validates the
translations first and refuses to build if any are malformed.

Requires Python 3.8+ and the .NET 8 SDK (installed at `%USERPROFILE%\.dotnet`).

## Translating more

```powershell
$P = $PSScriptRoot   # or this folder
python "$P\tools\make_batches.py" --stats          # what is left
python "$P\tools\make_batches.py" --emit --budget 22000
# translate work\batches\NNN_*.json  ->  translations\NNN_*.json
python "$P\tools\check_translations.py"
.\build.ps1
```

For repetitive prefixes, translate a *phrase dictionary* instead of every cell —
`woundslot` was 488 cells from only 149 distinct phrases:

```powershell
# 1. list every distinct phrase (empty dictionary)
'{}' | Set-Content "$P\work\empty.json"
python "$P\tools\translate_repetitive.py" "$P\work\empty.json" "$P\translations\NNN.json" $batches
# 2. fill in work\NNN.todo.json, save as work\phrases_NNN.json, re-run with it
```

It never guesses: anything the dictionary does not cover is reported, not invented.

---

# When the game updates

**The mod is built to survive this.** It does not modify any game file, and it does not
replace the game's localization table — at startup it loads the game's *own* table and
lays the Thai translations over the English column, matched **by key**.

So after a game update:

* Text the devs **added** shows in **English**, not as a raw key. Nothing breaks.
* Text the devs **changed** keeps your Thai (it is still keyed the same) — see the
  caveat below.
* Text the devs **removed** is simply unused.
* All ten other languages always come from the installed game version.

### Step 1 — just try it

Launch the game with the mod installed. Then read
`…\LocalUserPresets\QuasimorphThai\QuasimorphThai.log`:

```
Thai table ready: 11577 rows, 7046 translated, 4306 left in English.
```

If that line appears and the count is sane, **you are done — no rebuild needed.**
`N translation(s) match no row in this game version` tells you how many keys the game
dropped or renamed.

### Step 2 — only if you want to translate the new text

```powershell
# Re-extract the base table from the NEW game version
python "$P\tools\extract_table.py" "<new game>\Quasimorph_Data\resources.assets" "$P\work\localization_base.tsv"

# Everything already translated is skipped; only new/changed rows are batched
python "$P\tools\make_batches.py" --stats
python "$P\tools\make_batches.py" --emit --budget 22000
# ...translate, then:
.\build.ps1 -GameManaged "<new game>\Quasimorph_Data\Managed"
```

`translations/*.json` carries across versions untouched — it is keyed by string key,
never by row position or byte offset. That is the whole reason this survives updates,
and it is exactly what v1.2's binary-diff approach could not do.

### Caveat worth knowing

If the developers **reword an English string but keep its key**, the mod will keep
showing your older Thai for it, because a key match is all it can see. That is usually
what you want, but it means a reworded line can quietly drift out of date. To catch
those, diff the old and new base tables:

```powershell
python "$P\tools\find_keys.py" "$P\work\localization_base.tsv" "<some pattern>" key
```
…or keep the previous `work/localization_base.tsv` and compare the English columns.

### What would actually break

Only a change to the game's **mod API** — `MGSC.CustomResources.RegisterHook`,
the `[Hook(ModHookType.ResourcesLoad)]` attribute, or `UserModSystem` loading mods from
`LocalUserPresets`. These are first-class, documented-by-use extension points, so this
is unlikely; if it happened, the symptom is `Mod QuasimorphThai loaded.` missing from
the game's `Player.log`, and the fix is recompiling against the new assemblies.

The font patch is defensive already: it finds the `TMP_FontAsset` field on `FontPreset`
**by type**, so a rename of `_font` does not silently disable Thai.
