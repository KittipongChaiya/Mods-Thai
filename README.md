# Mods-Thai

Quasimorph mods and the Thai localisation, developed against
Quasimorph `1.0.3.578s.024ad60`.

---

## Repository layout

Every mod follows the same two-folder convention:

```
Quasimorph_<Name>/
├── Quasimorph_<Name>_src/     # development project — source of truth
│   ├── mod_src/               # C# sources + modmanifest.json
│   ├── tools/                 # helper scripts
│   ├── build.ps1              # produces build/mod/
│   ├── README.md              # what the mod does
│   └── PROJECT_STATE.md       # phase status, decisions, next action
└── Quasimorph_<Name>_v0.1/    # packaged, installable mod
    └── mod/                   # what a player copies into the game
```

**`_src/` is the source of truth.** The `_v0.1/` folder is generated output that
happens to be committed so a release can be downloaded without a build step.
Build intermediates (`build/`, `bin/`, `obj/`, `__pycache__/`) are not tracked —
run the mod's `build.ps1` to regenerate them.

---

## The mods

| Mod | What it does |
|---|---|
| **Big Pack** | Unlimited inventory space for your own mercenaries, no weight penalty. |
| **Big Stack** | Every stackable item stacks to 9999. |
| **Complications** | Each raid may roll one named complication that changes how the floor has to be played — not how much health things have. |
| **Nemesis** | Enemies that remember you. A marked enemy gains a name, hunts you, and returns with a higher rank if it survives. |
| **Retinue** | A squad that fights so you don't have to. Allies follow you down through the floors. |
| **Ruthless** | Adds a fourth difficulty, Hardcore Tactical Ruthless (ยุทธวิธีไร้ปรานี), built on enemy competence. |
| **Signals** | Send an ally anywhere on the floor, seen or not. Adds *Move to…* and an *Escort / Roam* control. |
| **Silence** | Noise and stealth made real — puts the game's existing noise simulation on screen. |
| **Stride** | Act while running: open doors, pick up loot and search corpses without leaving the Run stance. |
| **Thai** | Thai localisation of the game, plus the translation tooling and Cheat Engine tables. |

---

## Branches

This repository has exactly two branches, and they never merge into each other.

| Branch | Contents |
|---|---|
| **`main`** | All mod development — every project above, source and packaged releases. This is the trunk; all work happens here. |
| **`Quasimorph_SourceCode`** | Reference only. Decompiled Quasimorph game source, kept as an orphan branch with no shared history. Never merged into `main`. |

## Tags

Each mod milestone is tagged, so any release point stays reachable by name:

```
thai/v1.3   thai/v1.4
mod/bigpack-v0.1        mod/bigstack-v0.1     mod/complications-v0.1
mod/nemesis-v0.1        mod/retinue-v0.1      mod/ruthless-v0.1
mod/signals-v0.1        mod/signals-v0.2      mod/signals-v0.3
mod/silence-v0.1        mod/stride-v0.1
```

---

## Building a mod

Each mod builds independently. From its `_src/` folder:

```powershell
.\build.ps1
```

Builds reference the game's managed assemblies. The default path is set per
project and can be overridden:

```powershell
dotnet build -p:GameManaged="<path>\Quasimorph_Data\Managed"
```
