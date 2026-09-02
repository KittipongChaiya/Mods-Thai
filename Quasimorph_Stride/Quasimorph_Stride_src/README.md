# Quasimorph Stride

**Act while running.**

In the vanilla game the Run stance forbids every interaction. Doors will not open, loot
cannot be picked up, corpses cannot be searched. Worse, a run order that meets a shut
door does not pause — it throws the rest of the move away and leaves you standing in the
doorway. This lifts that, one category at a time.

Built for Quasimorph `1.0.3`.

## What it does

| Layer | What you get |
|---|---|
| **Doors** | Open and close doors while running — including doors your path crosses on the way somewhere else, so a long run no longer dies at the first closed door. |
| **Loot** | Pick things up off the floor and search corpses while running. |
| **Containers** | Crates, lockers, terminals and other interactive scenery. |
| **Elevators** | Ladders, elevators and dislocators. **Off by default.** |
| **Vest** | Medkits, stimulants and grenades from the vest. **Off by default.** |
| **Ally inventory** | An ally's pack and wound fixation. **Off by default.** |

## What it does *not* do

**It does not make anything free.** Interacting while running still ends your turn,
exactly as it does at walking pace. Free actions remain what they have always been — a
property of the Slow stance alone, through `Player.FreeInteractObstacles` and
`Player.FreeInventoryUse`, neither of which this mod touches.

So the trade the Run stance offers is unchanged: three action points, a large accuracy
penalty, and more noise. All this mod removes is the part where a sprinting soldier
cannot work a door handle.

It also touches no enemy and no ally. All three patches read `creatures.Player` and
nothing else.

## Why the restriction exists, and why lifting it is not a hack

The Run stance does not fail to interact by accident. It is forbidden by three checks in
`PlayerInteractionSystem`, all written the same way:

```csharp
if (player.MovementState == CreatureMovementState.Run
    && !PerkSystem.GetPerkParameterBool(player.CreatureData, "BRunActions"))
    return false;
```

| Gate | What it blocks |
|---|---|
| `CanInteractObstacles` | Doors, containers, terminals, elevators, ladders |
| `CanUseInventory` | Floor pickup, corpse looting, vest slots, inventory screen, healing screen |
| `CanOpenAllyInventory` | Ally inventory and wound fixation |

`BRunActions` is a real vanilla perk parameter — the game's own `ParameterNames` calls it
`PARAM_RUN_ACTIONS`. **"Can act while running" is a state the game already models and
already hands out as a perk reward.** This mod grants that state, per config. There is
nothing here the game cannot already produce on its own.

Both ways of opening a door run through the same gate, which is why one patch covers
both: clicking a door directly reaches `InteractObstacle`, and running to somewhere with
a door in the way reaches `MovePlayer`'s `ClosedDoor` branch. The second is the one that
hurts — the refusal there sets `clearCmdQueue`, so the rest of your move is discarded.

## One thing done carefully

`CanUseInventory` takes no argument saying what is asking. One check stands in front of
five different things, and you asked for two of them.

So rather than lift it wholesale, the three call paths that were actually wanted — floor
pickup, corpse looting, and optionally vest use — open a **scope** around themselves, and
the grant only applies inside it. The HUD's inventory button, the healing screen and the
ally wound panel reach the same check with no scope open and get vanilla's answer.

If you would rather have the blunt version, `run_full_inventory=true` overrides all of
it.

The two obstacle gates also re-run the checks vanilla never reached. Vanilla
short-circuits on the Run test and never evaluates the ones below it, so lifting the
first means taking responsibility for the rest — otherwise this mod would quietly hand a
Baron mutation the elevator, or a tutorial the object it is deliberately withholding.
Both are re-tested before anything is granted.

## Settings

`config.txt` appears next to the assembly on first run, with every key documented.

| Key | Default | What it unlocks while running |
|---|---|---|
| `enabled` | `true` | Master switch. `false` applies no patches at all. |
| `run_open_doors` | `true` | Doors, open and close |
| `run_take_items` | `true` | Floor loot and corpses |
| `run_use_containers` | `true` | Crates, lockers, terminals |
| `run_use_elevators` | `false` | Elevators, ladders, dislocators |
| `run_use_vest` | `false` | Vest slots |
| `run_ally_inventory` | `false` | Ally pack and wound fixation |
| `run_full_inventory` | `false` | Everything, unscoped — overrides the keys above |
| `fix_tooltip` | `true` | Correct the Run tooltip (see below) |
| `probe` | `false` | Write `probe.txt` with everything resolved at startup |

Elevators and the vest are off by default on purpose. Sprinting straight into an
extraction changes how a raid *ends*, and throwing a grenade mid-sprint is a combat
capability — both are decisions rather than fixes, so they are yours to make.

## The tooltip

Vanilla's Run tooltip ends with a red line stating that inventory and actions are
forbidden. With this mod installed that line is a lie, and a tooltip that lies is worse
than no tooltip — you read it, believe it, and never try the door. So a green correction
is appended underneath naming exactly what your config permits. It is generated from the
same booleans the patches read, so it cannot drift out of step with them.

Set `fix_tooltip=false` to leave the tooltip exactly as the game wrote it.

## Other mods

Checked against all 104 Workshop mods installed on this machine. One touches the same
decision:

- **Speed Toggle** prefixes `PlayerInteractionSystem.OpenTheDoor` to strip the door
  animation pause, and its replacement calls `CanInteractObstacles` itself. It asks the
  question this mod answers, gets the wider yes, and opens the door without the pause.
  The two compose; neither needs to know about the other.
- **Vanilla Set Bonuses** also patches `OpenTheDoor`. This mod does not patch that method
  at all — it patches the permission check in front of it.
- **Red's Opt-in Mod Pack** and **Ally Roam/Patrol** reference `ProcessCmd`, which this
  mod prefixes. Ours reads one field of one argument and never returns `false`, so it
  cannot change what that method does, and Harmony runs both regardless of order.
- **Stealth Auto-Walk** drops you out of the Run stance when an enemy appears. That is
  the opposite end of the same subject and the two agree.

Nothing installed patches any of the three gates. Whatever is loaded, `QuasimorphStride.log`
names it and says what it means.

## Building

```powershell
.\build.ps1            # compile and stage the release folder
.\build.ps1 -Install   # also install into LocalUserPresets for testing
```

The build has two gates beyond compiling.

`tools/apicheck.py` resolves every reference the mod makes against the real game
assemblies and fails if one is missing. Because this mod reaches for **no private members
at all**, that covers every member it calls — the only things it cannot see are the patch
targets themselves, which are addressed by name. Those are covered twice over: `nameof`
makes a rename fail the build here, and `PatchVerify` makes it a line in the log on
anyone else's install.

`tools/cfgcheck.py` confirms the config still describes itself. Every setting is written
down four times — the template `config.txt` is generated from, the `Load()` call that
reads it back, the field default it falls back to, and the README table above — and
nothing in the compiler connects them. A key renamed in one place gives you a config file
whose settings are silently ignored, which looks exactly like the mod working.

## Siblings

**Signals** (ally orders), **Retinue** (an ally squad), **Ruthless** (difficulty),
**Big Pack** and **Big Stack** (inventory), **Thai** (translation). Each installs and
uninstalls on its own; none requires another.
