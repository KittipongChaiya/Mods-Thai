# Quasimorph Nemesis

**Enemies that remember you.**

Now and then a raid marks one of its enemies. It gets a name, and it comes looking for
you later. If it kills one of your mercenaries it survives, gains a rank and returns
better armed. Kill it and it is finished — and the gear it climbed to is your salvage.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

| | |
|---|---|
| **Promotion** | A raid may mark one enemy. The marked enemy is *not* changed — a record is made from it, and the nemesis itself arrives on a later floor. |
| **Return** | Living nemeses turn up in later raids, at most one per floor. |
| **Rank** | Earned only by killing one of *your* mercenaries. Rank buys equipment tech level, health, evasion, sight, and eventually a second action per round. |
| **Retirement** | Kill it and the row is retired. The name is never reused and the tally is kept. |
| **Memory** | The roster lives inside your save, so it belongs to that campaign and travels with it. |

## Why nothing here can leak onto ordinary enemies

A nemesis is never a buffed creature. It is a **row that a creature is built from.**

Each rank produces its own `MobClassRecord` — a template cloned fresh from the untouched
base class — and the game then builds an ordinary monster from it exactly as it builds
every other monster. Only nemesis spawns use nemesis templates, so there is no path from
this mod to the rest of the world's enemies.

That also makes the mod reload-safe by construction. The sibling **Retinue** mod
documents the trap this avoids:

> *The obvious implementation reads a stat, multiplies it and writes it back... It works
> until the player saves and reloads: the buffed stats are saved with the creature, the
> set is not, and the ally is buffed again on top of itself.*

A nemesis is buffed *and* persisted *and* deliberately re-encountered, so it is the worst
possible candidate for that approach. Rebuilding a template a thousand times produces the
same template, because it is always cloned from the base and never from itself.

## Where the name comes from

The game names a creature through a localization key shaped `monster.<mobClassId>.name`.
Because each nemesis is spawned from its own injected mob class, it already asks for a key
no translation file has — `monster.nemesis_7.name` — so the mod simply answers.

The pleasant consequence: the name arrives through the game's own naming path, so it is
correct everywhere at once — inspect window, combat log, damage popups, corpse — without
this mod knowing where any of those are.

## Installing

Copy the `mod` folder's contents to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphNemesis\
```

so that `modmanifest.json` and `QuasimorphNemesis.dll` sit directly inside
`QuasimorphNemesis\`. Start the game; `config.txt` is written next to the DLL with every
setting explained.

## config.txt

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | Master switch. `false` applies no patches at all. |
| `only_on_difficulty` | *(empty)* | Restrict to one difficulty preset id. |
| `promote_chance` | `0.25` | Chance a raid marks a new nemesis. |
| `max_living` | `3` | How many may be alive at once. The pacing dial. |
| `return_chance` | `0.5` | Chance a living nemesis appears in an eligible raid. |
| `roster_cap` | `32` | Rows remembered in total, killed ones included. |
| `health_per_rank` | `0.15` | Extra health per rank. Restrained on purpose. |
| `dodge_per_rank` | `0.05` | Extra evasion per rank. |
| `max_tech_level_bonus` | `3` | Ceiling on equipment tech level above the base class. |
| `rank_for_extra_turn` | `3` | Rank at which it acts twice per round. |
| `probe` | `false` | Write `probe.txt` with the roster and every patch resolved. |

## Honest notes

**The roster lives in your save.** There is no hook for writing a save, so the mod patches
the game's global-component serialisation to add one key of its own. It reads and writes
only that key. **A save made with this mod loads perfectly well without it** — the key is
simply ignored.

**Uninstalling mid-campaign is clean, with one caveat.** Nemesis mob classes exist only in
memory and are rebuilt from the roster each session, so removing the mod removes them.
Any nemesis creature *standing on a floor in a mid-raid save* was already built and will
still be there, unnamed, until that raid ends.

**Rank comes only from your own mercenary dying.** An ally death is not a rank. If you run
Retinue, your squad can lose fights to a nemesis all day without feeding it.

**It stacks with Ruthless, deliberately.** That mod raises every enemy through the shared
records; a nemesis is cloned from those records *after* it has written to them, so rank
sits on top of an already harder enemy. If a rank 3 nemesis feels unreasonable there,
lower `health_per_rank` and `max_tech_level_bonus` rather than turning the mod off.

**If `Cap Enemy Spawn Tech` is installed**, it may flatten the equipment tech level that
is the main thing a rank buys. Not a conflict, but worth knowing if ranks stop mattering.

## If the game updates

The mod reaches several private game members by name, and a name in a string cannot be
checked at build time the way an ordinary call can. So it checks itself at startup:
`QuasimorphNemesis.log` lists every patch that attached. If the two save patches fail it
says so explicitly, because a mod that silently forgets your nemeses is far more confusing
to play than one that is plainly broken.
