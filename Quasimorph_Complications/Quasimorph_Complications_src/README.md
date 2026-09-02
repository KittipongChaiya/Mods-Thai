# Quasimorph Complications

**Raids stop being the same raid.**

Each raid may roll a single named complication, announced when you arrive, that changes
how the floor has to be played — not how much health anything has.

Built for Quasimorph `1.0.3.578s.024ad60`.

## The four

| | What happens | The trade |
|---|---|---|
| **Reinforcements** | The defending faction sends waves, away from you, hunting. A clock you cannot ignore. | A supply cache drops on turn one. Somebody had to carry the ammunition that is about to be shot at you. |
| **Fire aboard** | Cells catch and spread. Routes close. The station's cargo burns. | The floor has to be looted in a hurry, and everything you take was taken from a fire. |
| **Rival crew** | Another crew is working the same station, hostile to it and to you. | They are a threat and they are also the best gear on the floor, walking around wearing itself. |
| **Loud floor** | The hull carries sound. Every footstep, door and death reaches twice as far. | It cuts both ways — you hear them too. |

**One per raid, never two.** Two at once is not twice as interesting; it is noise, and it
makes it impossible to learn what any single complication does to a floor.

## Not a tax, a trade

The sibling **Hardcore Tactical Ruthless** mod's rule was that difficulty should cost you
decisions, not time. Complications follow it: **every one of them carries its own
compensation in fiction rather than in money.** This mod does not touch mission rewards at
all — a botched reward patch is a save-affecting bug, and the trades above are better
design than a percentage anyway.

## No Harmony patches

This mod applies **none**, like the sibling Retinue mod. That is not restraint for its own
sake — it turned out every effect a complication needs is already public API:
`SpawnSystem.SpawnFixedGroup` for arrivals, `FireController.AddFire` for the fire,
`ItemOnFloorSystem.SpawnItem` for the cache, `Data.Global` for the noise radii.

No item id is hardcoded anywhere. The cache is assembled by asking the game what it has and
filtering by item class, so it works with whatever any other mod added too.

## Installing

Copy the `mod` folder's contents to:

```
%USERPROFILE%\AppData\LocalLow\Magnum Scriptum LTD\Quasimorph\LocalUserPresets\QuasimorphComplications\
```

so that `modmanifest.json` and `QuasimorphComplications.dll` sit directly inside
`QuasimorphComplications\`. Start the game; `config.txt` is written next to the DLL with
every setting explained.

## config.txt highlights

| Key | Default | What it does |
|---|---|---|
| `chance` | `0.35` | Chance a raid gets a complication at all. |
| `banner` | `true` | Show it on arrival. **A complication nobody is told about is just an unfair floor.** |
| `weight_*` | `10/8/8/10` | Relative likelihood. `0` switches one off. |
| `fire_max_cells` | `14` | The cap that keeps a burning floor winnable. |
| `loud_floor_scale` | `2.0` | Multiplier on the global noise radii, restored when the floor ends. |
| `only_on_difficulty` | *(empty)* | Restrict to one difficulty preset id. |
| `probe` | `false` | Write `probe.txt`, including what the game's own unused event system turns out to allow. |

## Pairs with Silence

The **loud floor** complication raises the game's global noise radii, and the sibling
**Silence** mod is the only thing that will show you what that did. Without it, a loud
floor is an invisible tax. With it, it is the most readable complication here.

## The road not taken

The game has an entire **unused in-raid event system**. `IngameEventSystem
.RandomizeDungeonEvent` is public and static, `Data.Events` holds the records, and
`MobSpawnEventRecord` carries `PointsRange`, `AllianceType`, `FactionId`, `QmorphosLevel`
and — remarkably — **`BlockAllDoors`**. A reinforcement wave that locks the doors behind it
is already a data type in this game, and nothing in a typical hundred-mod load order
touches any of it.

This mod drives its complications itself instead, because one question could not be
answered without running the game: `EventCollection` declares only `TryGet`, so unlike
`Data.MobClasses` there may be no way to add an event at all.

**Set `probe=true` and the mod answers that question and writes it down.** If events turn
out to be injectable, the whole catalogue could be re-expressed as real game events — which
would be a better mod than this one.
