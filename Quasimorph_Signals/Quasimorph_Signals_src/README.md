# Quasimorph Signals

**Roam, and orders that carry out of sight.**

Adds an **Escort / Roam** control to the ally panel, and lets you command an ally you
cannot currently see. Works on every ally the game can give you — a vanilla escort, an
enemy you bribed, a summon, a quest ally, or a squad from the sibling **Retinue** mod.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

| Layer | What you get |
|---|---|
| **Roam** | Tell one ally to stop shadowing you and go hunting. Per ally, reversible, remembered across floors and saves. |
| **The control** | A second two-state toggle on the ally panel, beside the vanilla follow button. |
| **Out of sight** | Your allies' markers stay on screen when they are out of view, so you can select and order one at range. |

**Enemies are never touched.** Every patch asks "is this creature on my side?" first and
returns the vanilla answer for everything else. That check happens on the creature in
hand, on every call — it is not a mode that can be left switched on.

## Why it does not reuse the vanilla follow button

The game's follow/wait control is a `ToggleAllyStateButton`, and its state is a `Side` —
an enum of exactly `Left` and `Right`. It **structurally cannot hold a third value.**

That is worth knowing because the Workshop mod *Ally Roam/Patrol* adds its Roam state by
relabelling that button, which is very likely why its Roam option often does not appear.
This mod clones the button into a separate Escort/Roam toggle instead, so vanilla
follow/wait and shoot/hold-fire keep working exactly as they always did.

## Roaming needs no patches. The button does.

Worth being precise, because it is the reason this is its own mod:

```
roam    Behaviour.SetEndlessHunt(true, force: true)   public API
escort  Behaviour.StartFollowing(player)              public API
```

The behaviour is plain public game API. Only the **button** and the **out-of-sight
layer** need Harmony, because every member involved is private.

The sibling **Retinue** mod guarantees it applies no Harmony patches at all. Keeping that
guarantee true is exactly why this is a separate mod rather than a Retinue feature —
uninstall this and Retinue is still patch-free. Neither mod requires the other.

## Installing

Copy the `mod` folder's contents to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphSignals\
```

so that `modmanifest.json` and `QuasimorphSignals.dll` sit directly inside
`QuasimorphSignals\`. Start the game; `config.txt` is written next to the DLL with every
setting explained.

## Using it

Click an ally to open its inspect window. Beside the vanilla **Follow / Wait** control
there is now **Escort / Roam**:

- **Escort** — the ally behaves as the game made it, and vanilla follow/wait governs.
- **Roam** — the ally leaves you and goes looking for the enemy.

The order sticks. It is re-asserted once per turn, so an ally the AI pulls back into
following goes back to roaming, and it survives elevators, saves and reloads because it
is keyed to the creature's own id rather than to the object.

## config.txt

| Key | Default | What it does |
|---|---|---|
| `enabled` | `true` | Master switch. `false` applies no patches at all. |
| `command_ui` | `true` | Add the Escort/Roam toggle to the ally panel. |
| `default_roam` | `false` | Whether a newly seen ally starts out roaming. |
| `remote_orders` | `true` | Keep ally markers on screen when out of view. Allies only. |
| `yield_to_ally_roam_patrol` | `true` | If *Ally Roam/Patrol* is loaded, leave the panel to it. |
| `probe` | `false` | Write `probe.txt` recording every member this mod resolved. |

## If the game updates

This mod reaches several private game members by name, and a name in a string cannot be
checked at build time the way an ordinary call can. So it checks itself at startup
instead: `QuasimorphSignals.log` lists every patch that attached and every member that
resolved. If a game update renames something, that file says so in plain words and the
rest of the game carries on unaffected.

## Other mods

- **Ally Roam/Patrol** — same idea, incompatible approach. By default this mod detects it
  and withdraws its own control, keeping roaming and out-of-sight orders. Uninstalling
  that mod, or setting `yield_to_ally_roam_patrol=false`, gives you this one's toggle.
- **Continue on Monster Detection**, **Stealth Auto-Walk** — both postfix the same
  `Monster.ShowSignal` property. This mod only ever turns that answer from false to true,
  and only for allies, so their behaviour on enemies is unchanged.
- **Direct Follower Orders** — complementary. It adds its own order panel rather than
  touching the follow button.
- **Squad: More operatives** — its operatives get the roam control too. Giving an order
  writes nothing to a character sheet, so unlike a stat change it is safe on a persistent
  mercenary.
