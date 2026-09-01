# Quasimorph Big Pack

Unlimited inventory space for your own mercenaries, and no weight penalty for using it.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

- Your mercenaries' backpacks become **50 rows tall** (configurable). The width is left
  alone on purpose — see below.
- Carrying a full pack costs you **nothing**: no dodge loss, no extra satiety drain, no
  movement penalty.
- Enemies are unaffected. Ship cargo, station cargo, containers and corpses are
  untouched.

## Read this before you uninstall

**Empty your backpack into ship cargo before removing this mod.**

Grid sizes are saved with your game. If you remove the mod while carrying more than a
normal backpack holds, the game shrinks the grid on the next equip change, collects
everything that no longer fits, and — when there is no floor to drop it on, which is the
case on the ship — **deletes it**. That is vanilla behaviour, and no mod can intercept it
once it is gone.

The mod watches for this and writes a warning to `QuasimorphBigPack.log` whenever you are
carrying more than a vanilla pack would hold, so you get told before it matters.

## Installing

Copy the `mod` folder to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphBigPack\
```

so that `modmanifest.json` and `QuasimorphBigPack.dll` sit directly inside
`QuasimorphBigPack\`. Start the game.

To uninstall, read the warning above, then delete that folder.

## Configuration

`config.txt` is written next to the DLL on first run.

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | `false` leaves the game completely untouched. |
| `backpack_height` | `50` | Backpack rows, 1–200. |
| `resize_vest` | `false` | See "Why the vest is off by default". |
| `vest_height` | `4` | Only used when `resize_vest=true`. |
| `remove_weight` | `true` | `false` restores vanilla weight penalties. |

## Why only the height changes

The inventory panel scrolls vertically and has no horizontal scrollbar, so a taller grid
is a shape the game already knows how to draw — it grows its own cargo holds the same way.
A *wider* grid would simply render off the edge of the panel where you could not reach it.

## Why the vest is off by default

The vest is built as a single row (`height = 1`) and sits in a short horizontal strip in
the UI. Nothing suggests it scrolls. Turn `resize_vest` on if you want to try it, but
check that you can actually reach the extra rows before you put anything in them.

## What removing the weight penalty gives up

Weight in Quasimorph never blocks a pickup — it is purely a modifier. Every consumer
reads one method, so switching it off is clean, but it is worth knowing that two of those
consumers are *bonuses*: a heavy load slightly increases melee damage and physical
resistance. `remove_weight=true` gives those up along with the penalties. Set it to
`false` if you would rather keep vanilla weight entirely.

## Troubleshooting

`QuasimorphBigPack.log` sits next to the DLL and is rewritten each launch. It records the
config it read, the patches it applied, every grid it grew, and any warning about carrying
more than vanilla capacity. Everything is mirrored into the game's own `Player.log` too.

If the mod fails to patch — most likely after a game update moved something — it logs the
error and does nothing else. The game keeps running with vanilla inventories.
