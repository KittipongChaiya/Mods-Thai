# Quasimorph Big Stack

Every stackable item stacks to 9999.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

Raises the maximum stack size for all eleven stackable item categories — ammo,
consumables, devices, grenades, throwing weapons, repair kits, trash, exp gainers,
fixation medicine, pact components and placeable devices.

The number is an override, not a multiplier, so you get exactly what is in `config.txt`
whichever stack-size option your campaign's difficulty was started on.

**Items already in your save need nothing.** The game re-derives every item's maximum
whenever you reach a station, the after-raid screen, the arsenal, augmentation or either
trade screen.

## This affects the whole game, not just you

Stack limits are defined per item type, with no notion of who is holding the item. There
is no way for a mod to raise your stacks alone. Shop stock, floor loot and enemy
inventories get the same ceiling.

## Winding down

**Do this before you uninstall, or before you lower `max_stack`.**

Shrinking a stack limit does not delete anything on its own. The game banks whatever is
over the new maximum, tops up your other stacks of the same item, and creates fresh stacks
for the remainder. The problem is where those new stacks go: they are placed only if there
is room, and dropped silently if there is not. A single stack of 9999 rounds becomes 200
stacks at a limit of 50, and no ordinary backpack has 200 free slots.

So wind down in steps rather than all at once:

1. Move what you can into ship cargo, which is far larger than a backpack.
2. Lower `max_stack` part of the way — say 9999 to 1000 — and restart the game.
3. Visit a station, or open the arsenal. The game splits the stacks there.
4. Repeat until `max_stack` is back near vanilla (around 50 for most things).
5. Only then delete the mod folder.

The mod helps you judge this: every time you load a save or arrive in space it writes a
line to `QuasimorphBigStack.log` saying how many extra stacks a wind-down would create at
your `wind_down_stack` setting, and therefore how many free slots you need.

The sibling **Big Pack** mod, which makes backpacks 50 rows tall, gives you a great deal
more room to absorb this. Neither mod requires the other.

## Installing

Copy the `mod` folder to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphBigStack\
```

so that `modmanifest.json` and `QuasimorphBigStack.dll` sit directly inside
`QuasimorphBigStack\`. Start the game.

## Configuration

`config.txt` is written next to the DLL on first run.

| Key | Default | Meaning |
|---|---|---|
| `enabled` | `true` | `false` leaves the game completely untouched. |
| `max_stack` | `9999` | Items per stack, 1–30000. |
| `wind_down_stack` | `50` | Only used for the log warning — the size you plan to wind down to. |

### Why the limit is 30000 and not 32767

Stack counts are 16-bit signed integers. The game adds a perk bonus to the maximum before
converting back to that type, so a value close to the top wraps around to a *negative*
stack size. 30000 leaves room for the bonus.

This is also why the mod overrides the game's stack calculation rather than editing the
item definitions: the vanilla routine multiplies by the difficulty's stack setting, and
`9999 × 4` already overflows.

## Capacity, not free ammunition

Raising the ceiling raises how much a stack can *hold*. It does not change how much you
are given.

This needed fixing in 0.1.1. The game creates every stackable item full — a quirk that is
harmless when "full" is 40 rounds and absurd when it is 9999 — so bought and mission-reward
ammunition arrived at 9999 apiece. Newly created items now start at the amount vanilla
would have given, in a container that happens to hold 9999.

## What to expect

- **Stacks do not merge themselves.** Raising the limit does not combine stacks you
  already have — the game only redistributes stacks that are *over* the limit. Picking
  items up merges them normally, and the sort button helps.
- **Trade may still look different.** Station stock is priced and laid out partly from
  stack limits. If station inventories look wrong, lower `max_stack` and restart.

## Troubleshooting

`QuasimorphBigStack.log` sits next to the DLL and is rewritten each launch. It records the
config, the patch, the first stack limit it overrode (with the vanilla value for
comparison), and the wind-down warning.

If the mod fails to patch — most likely after a game update moved something — it logs the
error and does nothing else. The game keeps running with vanilla stack sizes.
