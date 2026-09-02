using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Putting creatures on a floor that has already started.
    ///
    /// This is the same call the sibling Retinue and Nemesis mods make, in the same
    /// order, with the same failure contract: <c>SpawnSystem.SpawnFixedGroup</c> logs
    /// its own reason and returns an empty list when it cannot find room, and that is a
    /// "not this floor" rather than an error. Nothing here forces a spawn.
    /// </summary>
    internal static class Spawns
    {
        private const int MinRadiusInGroup = 2;
        private const int MaxRadiusSpiral = 14;

        private static readonly System.Random Roll = new System.Random();

        internal static bool Wave(State state, int size, bool hostile, int minDistance,
                                  string label)
        {
            if (size <= 0)
            {
                return false;
            }

            var creatures = state?.Get<Creatures>();
            var raidMetadata = state?.Get<RaidMetadata>();
            var mercenaries = state?.Get<Mercenaries>();
            var perkFactory = state?.Get<PerkFactory>();
            var difficulty = state?.Get<Difficulty>();
            var turnController = state?.Get<TurnController>();
            var debugData = state?.Get<DungeonGeneratedDebugData>();
            var mapGrid = state?.Get<MapGrid>();

            if (creatures?.Player == null || raidMetadata == null || mercenaries == null ||
                perkFactory == null || difficulty == null || turnController == null ||
                debugData == null || mapGrid == null)
            {
                // SpawnFixedGroup dereferences every one of these. Missing any means we
                // are being called at a moment the game is not ready for a spawn.
                ModLog.Warn("the dungeon systems are not all available; no " + label);
                return false;
            }

            var mobClassId = PickMobClass(creatures, hostile, out var factionId);
            if (mobClassId == null)
            {
                ModLog.Warn("nothing on this floor to model a " + label + " on");
                return false;
            }

            if (!MapPick.FarCell(state, minDistance, out var origin))
            {
                return false;
            }

            var unit = new Unit
            {
                FactionId = factionId ?? string.Empty,
                TechLevelLimit = -1,
                Alliance = hostile ? CreatureAlliance.VictimFaction : CreatureAlliance.Traitors,
                DebugNote = "QuasimorphComplications " + label,
            };
            for (var i = 0; i < size; i++)
            {
                unit.Members.Add(mobClassId);
            }

            var spawned = SpawnSystem.SpawnFixedGroup(
                mercenaries, perkFactory, difficulty, turnController, debugData, mapGrid,
                creatures, raidMetadata, origin, unit,
                minRadiusFromObject: 0, minRadiusGroup: MinRadiusInGroup,
                maxRadiusSpiral: MaxRadiusSpiral, maxRadiusFromObject: MaxRadiusSpiral,
                autoSetGroupIndex: true, strictStartingPoint: false, setEndlessHunt: true,
                debugLabel: "QuasimorphComplications " + label);

            return spawned != null && spawned.Count > 0;
        }

        /// <summary>
        /// Models the newcomers on somebody already here.
        ///
        /// Reading the floor rather than choosing from the whole game means a
        /// reinforcement wave is always the kind of thing that would plausibly be
        /// stationed on this station, at this tech level, for this faction - without the
        /// mod holding any table of its own that a game update could invalidate.
        /// </summary>
        private static string PickMobClass(Creatures creatures, bool hostile,
                                           out string factionId)
        {
            factionId = string.Empty;
            if (creatures?.Monsters == null)
            {
                return null;
            }

            string chosen = null;
            var seen = 0;

            foreach (var creature in creatures.Monsters)
            {
                if (!(creature is Monster monster) || monster.CreatureData == null)
                {
                    continue;
                }

                var isAlly = monster.CreatureData.CreatureAlliance ==
                             CreatureAlliance.PlayerAlliance;
                if (isAlly)
                {
                    continue;
                }

                var id = monster.CreatureData.MobClassId;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                // Reservoir sampling: uniform over the floor without building a list.
                seen++;
                if (Roll.Next(seen) == 0)
                {
                    chosen = id;
                    factionId = monster.CreatureData.FactionId ?? string.Empty;
                }
            }
            return chosen;
        }
    }
}
