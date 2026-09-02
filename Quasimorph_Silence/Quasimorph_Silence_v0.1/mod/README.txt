# Quasimorph Silence

**Noise and stealth made real.**

The game already simulates noise in full. Every step, door, gunshot and death raises an
event with a radius, and enemies that are awake enough to care go and investigate it.
**None of it has ever been on your screen.**

This mod shows it to you, and lets you do something about it.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

| Layer | What you get |
|---|---|
| **See it** | A readout beside the movement panel: what you just made, how far it carried, and how many enemies were close enough and awake enough to hear it. |
| **Move quietly** | The game has had a Slow movement mode all along. This makes it worth using — footsteps only, yours only. |
| **Distract** | Press a key, click a cell, and the world hears something there. It is the game's own noise event, so anything that investigates noise investigates that. |
| **Know the numbers** | The three global noise radii are written to the log at the start of every raid. Nothing in the game has ever told anybody what a footstep costs. |

## What it does not do

**Enemies keep exactly the noise the game gave them.** The quiet-movement layer scales
footsteps that came from your own mercenary's cell, checked on every single call — not a
mode, not a flag set earlier, nothing that can be left switched on by accident.

**Only footsteps scale with how you move.** A door bangs the same however carefully you
walked up to it, and a gunshot is a gunshot. Otherwise tiptoeing would be a silencer you
did not earn.

**Your squad is not quietened.** If you run the sibling Retinue mod, three allies behind
you are three sets of footsteps the floor can hear. That is a real cost of bringing them.

## One patch

Almost everything this needs is already public. `CreatureSystem.PropagateNoise` is the
single funnel every noise event passes through, so one patch both sees them all and
scales the ones you made. The three radii are ordinary public properties on
`GlobalSettings`; the movement state, the cursor cell and the noise event itself are all
public too. `PropagateNoise` being callable is also what makes distractions a two-line
mechanic rather than an AI project.

## Installing

Copy the `mod` folder's contents to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphSilence\
```

so that `modmanifest.json` and `QuasimorphSilence.dll` sit directly inside
`QuasimorphSilence\`. Start the game; `config.txt` is written next to the DLL with every
setting explained.

## config.txt

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | Master switch. `false` applies no patches at all. |
| `quiet_movement` | `true` | Scale footstep noise by movement mode. Yours only. |
| `slow_noise_scale` | `0.4` | How much noise a footstep keeps while walking slowly. |
| `normal_noise_scale` | `1.0` | Vanilla. |
| `run_noise_scale` | `1.5` | Running is louder than vanilla by default. |
| `minimum_radius` | `1` | Floor under the scaled radius, so nobody is ever truly silent. |
| `readout` | `true` | The on-screen line. |
| `log_every_noise` | `false` | Write every event in the raid to the log, and a `probe.txt`. Diagnostic. |
| `distraction` | `true` | The throw-a-noise mechanic. |
| `distraction_key` | `T` | Any Unity `KeyCode` name. |
| `distraction_radius` | `10` | How far it carries. |
| `distraction_ap_cost` | `2` | Free would make it strictly better than moving. |
| `step_radius_override` | `-1` | Global. `-1` leaves the game's own value alone. |
| `door_radius_override` | `-1` | Global. |
| `death_radius_override` | `-1` | Global. |

## This can be a cheat, and it can be the opposite

Making yourself quieter is an advantage, and the mod says so rather than pretending
otherwise. But the scales go **both ways**, and the interesting direction is up.

Set `run_noise_scale=2.5` and running wakes the floor. Raise `step_radius_override` and
sound carries everywhere. Paired with the sibling **Hardcore Tactical Ruthless** mod —
whose enemies keep hunting long after they lose you — a loud floor is a considerably
harder game than vanilla, and one where the Slow mode the game shipped finally has a
reason to exist.

## If the game updates

The mod checks itself at startup and writes the result to `QuasimorphSilence.log`. It has
exactly one patch, so the failure mode is simple: either it attached and everything works,
or it did not and the log says so plainly while the rest of the game carries on.
