# Quasimorph — Hardcore Tactical Ruthless

Adds a fourth difficulty, **Hardcore Tactical Ruthless** (ยุทธวิธีไร้ปรานี), to the
difficulty screen. Pick any other difficulty and the game is exactly the game you
already had.

Target game: `1.0.3.578s.024ad60`.

## What it is

The game already ships forty-five difficulty sliders and a Custom mode, so a mod that
only moves sliders would not be worth installing. This one is built around the thing
sliders cannot express: **how well the enemy plays.**

> Difficulty should cost you *decisions*, not *time*.

Every change had to pass three tests before it shipped:

| Test | Question |
|---|---|
| Not a cheat | Does it help the player in any way? |
| Not a sponge | Does it just make the same fight take longer? |
| Answerable | Can good play beat it? |

**Enemy health is left at the vanilla value on purpose.** It is the one knob that
reliably fails the second test — it adds shots per kill and nothing else. Instead the
enemy sees further, acts more often, keeps hunting after it loses you, throws grenades
at your cover, picks the right firemode, and opens the door you closed behind you.

## The three layers

**1 — The difficulty preset.** Derived from vanilla Unfair by multiplier, never by
absolute numbers, so it always sits above whatever the game's own baseline is.

Enemies see 30% further, act 15% more, hit 20% harder, dodge and resist a little more,
and there are 30% more of them. Loot drops to 70%, corpse salvage to 60% condition,
selling to 80%, mission pay to 85%. Perks cost 25% more XP. Contracts expire sooner,
weight burns more calories, and the quasimorphosis clock runs faster.

Death closes the mission and drops your backpack. Revival costs time. Evacuation needs
the chip. No emergency box. **You keep your perks and your rank** — a bad half hour
should cost you a mission, not a campaign. The difficulty is locked for the run.

**2 — Tactical AI.** Longer hunt and investigate memory, more grenades, better firemode
choice, and your mines and hazards are avoided more often. Panic and surrender are
reduced only slightly, because both are good mechanics that end fights early; gutting
them would just make every fight longer.

Two rules keep this sane. A multiplier is never applied to a behaviour the designers
switched off, so a mindless creature does not learn to cook grenades. And only enemies
that already reason about equipment — item users, grenade throwers, firemode pickers —
get the door and item-use flags. Everything else keeps its vanilla relationship with
doors.

Sleeping enemies were left alone deliberately. Removing them would delete a stealth
option, which would make the game *less* tactical, not more.

**3 — Enemy loadouts.** Enemies come one tech level better equipped — and that gear is
also your salvage, so a harder fight is a fight worth taking. Condition drops to 70%
and spare ammo with it, so what you recover is a compromise rather than a resupply.

**Your own mercenaries are never touched.** They are built from a different table
(`MercenaryClasses`) that this mod contains no code path to. That is the structural
guarantee behind "not a cheat", not a promise.

## Install

Copy the `mod` folder's contents into:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphRuthless
```

Start a new game and the fourth panel is on the difficulty screen.

## config.txt

Written with defaults and comments on first run, next to the DLL.

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | `false` leaves the game completely untouched |
| `tactical_ai` | `true` | Layer 2 — enemy behaviour |
| `mob_loadouts` | `true` | Layer 3 — enemy gear and salvage |
| `intensity` | `1.0` | Scales layers 2 and 3 toward vanilla. `0.5` is halfway, `0.0` is off |
| `probe` | `false` | Writes `probe.txt` listing every preset, AI preset and mob class the game loaded |

`intensity` does not scale the difficulty preset itself — the preset is the mode's
identity, the layers are its tuning.

## Language

The panel reads Thai when the sibling Thai translation mod is installed and English
otherwise. The mod carries both strings itself and asks the game which language is
actually in the slot, so the two mods can be installed in either order and neither
depends on the other.

## Uninstalling — read this first

A save started on this difficulty records `HardcoreTacticalRuthless` as its difficulty
id. **Removing the mod leaves that save pointing at a difficulty the game no longer
knows about.** What the game does about that has not been tested yet (see
`PROJECT_STATE.md`); it may fall back to a default or it may fail to load the save.

Finish a Ruthless run, or keep a backup, before uninstalling.

Everything else is clean: the mod writes no files into the save, and the AI and loadout
changes are restored from a snapshot whenever a run starts on a vanilla difficulty.

## Not designed to run with Big Pack

The sibling **Quasimorph Big Pack** grants unlimited backpack space and removes carry
weight — a direct contradiction of this mode's not-a-cheat rule, and it defeats the
scarcity the whole design rests on. Both will load and run; the log says so plainly if
it spots it. Which mods to run is your call, not the mod's.

## Building

Needs the .NET 8 SDK and Python 3.

```powershell
.\build.ps1            # compile, stage, verify references
.\build.ps1 -Install   # and copy into LocalUserPresets
```

Step 3 resolves every game member the DLL references against the real game assemblies.
A sibling mod once shipped a call to an overload the game had already dropped; that
check exists so this one cannot.
