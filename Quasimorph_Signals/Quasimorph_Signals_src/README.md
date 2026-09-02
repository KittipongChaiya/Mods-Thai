# Quasimorph Signals

**Send an ally anywhere on the floor, seen or not.**

Adds a **Move to…** button and an **Escort / Roam** control to the ally panel, and lets
you command an ally you cannot currently see. Works on every ally the game can give you
— a vanilla escort, an enemy you bribed, a summon, a quest ally, or a squad from the
sibling **Retinue** mod.

Built for Quasimorph `1.0.3.578s.024ad60`.

## What it does

| Layer | What you get |
|---|---|
| **Move to…** | Press the button on an ally's panel, then right-click anywhere on the floor. That ally walks there and holds position — through walls, into rooms you have never entered, across the whole map. |
| **Roam** | Tell one ally to stop shadowing you and go hunting. Per ally, reversible, remembered across floors and saves. |
| **The control** | New buttons on the ally panel, beside the vanilla follow and shoot toggles. |
| **Out of sight** | Your allies' markers stay on screen when they are out of view, so you can select and order one at range. |
| **Fire discipline** | Allies stop firing from beyond their weapon's effective range and close in instead — no more emptying a shotgun down a corridor for one pellet. |

## Sending an ally somewhere

1. Click the ally to open its panel.
2. Press **Move to…**. The panel closes and a line appears telling you the game is
   waiting.
3. **Right-click** the destination. Anywhere on the floor — a room you have never seen
   is fine.

The ally paths there on its own and **holds position** when it arrives. It still fights
anything it meets on the way, and resumes walking afterwards. To call it back, give it
any other order: **Escort** puts it back at your side, and pressing the button again
(now reading **Cancel move**) drops the order where it stands.

If you point at a wall, or a room with no way in, the order is refused or abandoned
after a few turns and says so rather than leaving the ally grinding against a door.

### Why out of sight comes free here

The order is carried by the game's own `Investigate` state — the one a creature enters
when it hears a noise through a wall. It paths with the AI's own pathfinder, which has
never consulted the player's line of sight, because the creatures using it are not the
player. Sending an ally somewhere you cannot see is not a restriction that had to be
lifted; it is what that state already does.

There is an investigation timer, and at a glance it looks like it would expire half way
across a map. It does not: it only counts down once the creature has **arrived** or is
**stuck**, never while it is walking. So an order survives any distance and ends shortly
after arrival — which is the right shape for an order.

**Enemies are never touched.** Every patch asks "is this creature on my side?" first and
returns the vanilla answer for everything else. That check happens on the creature in
hand, on every call — it is not a mode that can be left switched on.

## Fire discipline

An escorting ally with a shotgun used to open fire the instant it saw anything, at any
distance, for almost no damage. That is a **missing check**, not a tuning problem — and
the game already has the check somewhere else.

There are five states an ally can fight from. Exactly one of them tests the range:

| State | Where an ally ends up | Range check? |
|---|---|---|
| `Attack.ProcessTacticMode` | a hunting ally that picked a target | **Yes** — else *"Target out of eff. range. Approaching"* |
| `Attack.ProcessDesperateMode` | low health, cornered | No |
| `Defense.TryAttack` | holding a position | No |
| `Rage` | enraged | No |
| **`FollowTarget.TryAttack`** | **every escorting ally, all the time** | **No** |

`FollowTarget` is where your squad lives whenever it is escorting you. The one state
they are always in is the one that never checked. This layer adds the check to all five.

**Out of range, they close the distance.** That falls out for free: every caller already
treats a failed shot as a cue to move — `FollowTarget` and `Attack` walk at the enemy,
`Defense` repositions. And an ally that *cannot* close — told to hold position, immobile,
or stuck in a blocked corridor — takes the weak shot rather than standing uselessly.

**Range is read properly.** The check uses `WeaponComponent.Range` plus the creature's
own `GetFirearmRangeBonus`, which together account for the **ammunition loaded**, item
traits, and range perks. Both vanilla's own check and the *Squad: More operatives* mod
read the raw record value and miss all three, so an ally carrying long-range rounds gets
told to walk closer for no reason. This one does not.

**Why effective range is the right line.** It is not an invented threshold:
`DamageSystem.FalloffDamage` starts taking damage away the moment `distance > range`.
Below it, full damage. Above it, the game is already punishing the shot.

**Enemies are untouched.** Teaching them not to waste ammunition would be a difficulty
change wearing a bug fix's clothes.

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
there is now **Move to…** (see above) and **Escort / Roam**:

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
| `move_orders` | `true` | Add the **Move to…** button and the right-click destination picker. |
| `fire_discipline` | `true` | Hold fire beyond the weapon's effective range and close in instead. Allies only. |
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

- **Ally Roam/Patrol** — same idea, incompatible approach, but only for the *stance*
  toggle: both mods relabel the vanilla follow button, and two mods writing one control
  on the same callback is a coin toss. By default this mod withdraws its Escort/Roam
  toggle when that mod is present. **Move to… is unaffected** — it is a new button of
  this mod's own that nothing else writes to, so you keep it either way. Uninstalling
  that mod, or setting `yield_to_ally_roam_patrol=false`, gives you the toggle back.
- **Continue on Monster Detection**, **Stealth Auto-Walk** — both postfix the same
  `Monster.ShowSignal` property. This mod only ever turns that answer from false to true,
  and only for allies, so their behaviour on enemies is unchanged.
- **Direct Follower Orders** — complementary. It adds its own order panel rather than
  touching the follow button.
- **Squad: More operatives** — its operatives get the roam control too. Giving an order
  writes nothing to a character sheet, so unlike a stat change it is safe on a persistent
  mercenary. It also patches the same shooting method with the same effective-range rule,
  but only for the operatives *it* deploys — not Retinue guards, bribed allies, summons or
  quest allies. Both compute the same answer from the same weapon, so they agree and there
  is nothing to reconcile; this mod simply covers every ally.
