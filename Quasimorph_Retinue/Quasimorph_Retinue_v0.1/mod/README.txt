# Quasimorph — Retinue

**A squad that fights so you don't have to.**

Allies spawn with you, follow you down through the floors, and are strong enough to
hold a room on the hardest difficulty. You walk behind them, heal them, hand them
ammunition and tell them where to go. Enemies are never touched by anything in this
mod.

Built for Quasimorph `1.0.3.578s.024ad60`. Pairs with — but does not require — the
sibling **Hardcore Tactical Ruthless** mod.

---

## What it actually does

| Layer | What you get |
|---|---|
| **The squad** | Three allies spawn beside you at the start of every floor, follow you to the elevator and ride it down. Casualties are replaced on the next floor; survivors are never duplicated. |
| **Ally strength** | Every ally — the squad, anyone you bribe, anything you summon, anyone a quest gives you — gets more health, more damage, better resistance and evasion, longer sight and an extra turn per round. |
| **Recruiting** | Drop food, drink, pills, a medkit or something valuable where a thinking enemy can see it. It walks over, picks it up, and joins you permanently. |
| **Your role** | Optional: enemies stop targeting your own mercenary entirely, so you can stand in the open and watch. Off by default. |

## Install

Copy the `mod` folder into

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphRetinue\
```

so that `modmanifest.json` and `QuasimorphRetinue.dll` sit directly inside it. Launch
the game once; `config.txt` is written next to the DLL with every setting explained.

---

## How to actually play support

**This part is vanilla Quasimorph and most players never find it.** The mod gives you
a squad; the game already gives you everything you need to command one.

**Click an ally** to open its inspect window. There you get:

- **Follow / Wait** — hold one at a doorway while the rest push on.
- **Shoot at will / Hold fire** — stop them opening up before you are in position, or
  before they wake the floor.
- **Their backpack** — take what they picked up, or give them a better gun, a helmet,
  more ammunition. They will use what you give them.
- **Medkits** — your medkits, applied to their wounds, the same way you heal yourself.
  Allies who can use items will also patch themselves and each other up, which is why
  the squad spawns carrying one.

**Push past them** when they block a corridor — allies are pushable, they will not
trap you in a doorway.

That loop is the game this mod is for: you carry the supplies, they carry the fight.

---

## Settings

All in `config.txt`, written on first launch with the same explanations.

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | Master switch. `false` leaves the game completely untouched. |
| `only_on_difficulty` | *(empty)* | Restrict the whole mod to one difficulty preset id. Empty means every difficulty. Set it to `HardcoreTacticalRuthless` to run this only as a Ruthless companion. |
| `squad_size` | `3` | How many allies to keep around you. `0` disables the squad but still strengthens allies you find yourself. |
| `stance` | `escort` | `escort` — they follow and screen you. `hunter` — they go looking for the enemy without you. Either way you can re-order each one in game. |
| `starting_kit` | `true` | Each new ally spawns with a medkit and a second helping of its own ammunition. |
| `ally_power` | `true` | The strength layer. |
| `power` | `1.0` | How far above vanilla an ally lands. `0.0` is vanilla exactly. |
| `recruiting` | `true` | Widen the game's gift mechanic so most thinking enemies can be bribed. |
| `spectator` | `false` | Enemies stop targeting your mercenary. A cheat, and labelled as one. |
| `probe` | `false` | Writes `probe.txt` with every value the mod reasons about. |

### The strength table

Each value is a multiplier on what the game already gave that creature, scaled by
`power`. An ally on Hardcore Tactical Ruthless is therefore stronger than one on
Normal, because it inherits that difficulty's enemy scaling first.

| Stat | Multiplier | Why |
|---|---|---|
| Health | ×1.60 | An ally that dies in the first exchange is a cutscene, not a squad. |
| Damage | ×1.50 | They have to actually kill things. An ally that only chips makes fights longer, not safer. |
| Resistance | ×1.25 | Wrong-damage-type hits stop deleting them. |
| Evasion | ×1.20 | Long-range potshots stop landing every time. |
| Sight | +1 | They spot first, which is the point of a screen. |
| Turns per round | +1 | The biggest competence knob, and the one that costs real seconds. |
| Pain | ignored | They do not fold at the wrong moment. |

**Held at vanilla on purpose:** accuracy (it is derived from the body type and could
not be changed safely across a save/reload), second chance (owned by the perk system,
and an ally reviving mid-fight reads as a bug), and invulnerability (an immortal squad
is not a squad).

---

## Honest notes

**Ally strength is saved with the ally.** The mod writes stats onto each creature, the
same way the game writes difficulty multipliers onto every monster it spawns. Those
values are part of the save. **If you uninstall the mod, allies already in a save keep
the strength they were given** — there is no code left running to take it back. New
allies after that point are ordinary.

**`spectator` is likewise saved.** If you have it on and plan to uninstall, set it to
`false` and play one more floor first — the flag is rewritten on every floor, so that
clears it. Uninstalling with it on leaves a permanently untargetable mercenary in that
save.

**Recruiting is the one thing that touches shared data.** It writes to the game's AI
presets, which are global for the session. The vanilla lists are snapshotted before
anything is written and restored the moment a run starts that you excluded with
`only_on_difficulty`, so a run you meant to keep vanilla stays vanilla.

**More bodies means longer turns.** Every ally acts on its own initiative. Three is
tuned; above five a floor starts to feel slow rather than safe. If turns start to drag,
`squad_size` is the first thing to cut and the `+1` turn bonus is the second.

**Creatures the designers already made bribable are left alone.** What those specific
creatures want is part of their design, and flattening it into a generic list would
throw away something the game got right.

**Mindless enemies never learn.** Recruiting only opens up for creatures whose AI
already reasons about equipment — the ones that use items, throw grenades or pick a
firemode. A horror does not accept a sandwich.

---

## Building from source

Requires the .NET 8 SDK and a copy of the game.

```powershell
cd Quasimorph_Retinue_src
.\build.ps1                 # compile, stage, verify every game reference resolves
.\build.ps1 -Install        # ...and copy into LocalUserPresets for testing
```

The build refuses to finish unless `tools/apicheck.py` can resolve every game member
the mod calls against the real shipped assemblies. A sibling mod once shipped a call to
an overload the game had already dropped; this is what makes that class of bug
impossible to ship.

## Uninstall

Delete the `QuasimorphRetinue` folder from `LocalUserPresets`. Read the honest notes
above first — the two things that persist in a save are ally strength and the
`spectator` flag.
