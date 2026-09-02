# Quasimorph — source code

The shared root of this repository. Every piece of work lives on its own branch
cut from **this** branch, so each mod can be returned to and developed on its
own without dragging the others along.

---

## Branch map

```
Quasimorph_SourceCode          <- you are here: the shared base
│                                 (main points at this same commit)
├── translate-v1.4              Thai localisation
├── trainer                     Cheat Engine tables and trainer tooling
├── bigpack-v0.1                Unlimited inventory space
├── bigstack-v0.1               Item stacks to 9999
├── complications-v0.1          One named complication per raid
├── nemesis-v0.1                Enemies that remember you
├── retinue-v0.1                An ally squad that fights for you
├── ruthless-v0.1               Hardcore Tactical Ruthless difficulty
├── signals-v0.3                Ally orders that carry out of sight
├── silence-v0.1                The noise simulation, on screen
└── stride-v0.1                 Act while running
```

Each branch carries **only its own work**, plus whatever this base provides, and
keeps its real commit history. Branches are never merged into each other — a mod
branch only ever merges *from* `Quasimorph_SourceCode`.

---

## What lives here

| Path | Why it is shared |
|---|---|
| `tools/apicheck.py` | Byte-identical in all nine C# mods |
| `tools/cli_meta.py` | Byte-identical in all nine C# mods |
| `.gitignore` | Build output rules every mod needs |
| `.gitattributes` | Line-ending normalisation |

Fix a shared tool **once here**, then merge this branch into any mod branch that
needs the fix:

```bash
git checkout ruthless-v0.1
git merge Quasimorph_SourceCode
```

Decompiled game source, if you add it, belongs here too — it is reference
material shared by every mod.

> Note: each mod also still carries its own historical copy of these tools under
> `Quasimorph_<Name>/Quasimorph_<Name>_src/tools/`, because `build.ps1` resolves
> them relative to itself. The copies here are the canonical ones; unifying the
> two would mean changing every `build.ps1` and is a separate change.

---

## Working on a mod

```bash
git checkout ruthless-v0.1
cd Quasimorph_Ruthless/Quasimorph_Ruthless_src
.\build.ps1
```

Each mod keeps the `Quasimorph_<Name>_src/` (development) and
`Quasimorph_<Name>_v0.1/` (packaged release) pair side by side; `build.ps1`
depends on that layout.

Mods target Quasimorph `1.0.3.578s.024ad60`. The game install itself is not in
version control.

---

## History

The combined monorepo that preceded this split is preserved at the tag
`archive/monorepo`, and each release milestone at `thai/*` and `mod/*`.
