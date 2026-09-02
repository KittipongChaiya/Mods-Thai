using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Promotion, arrival and departure - the loop that makes the roster mean something.
    ///
    /// Nothing in this file writes stats onto a creature. Promotion creates a row and a
    /// template; arrival asks the game to spawn that template. The creature that walks
    /// out is an ordinary monster built the ordinary way, which is what keeps the whole
    /// mod reload-safe.
    /// </summary>
    internal static class Encounters
    {
        /// <summary>Vanilla's own spawn geometry for a fixed group, as the sibling Retinue mod uses.</summary>
        private const int MinRadiusFromPlayer = 6;
        private const int MinRadiusInGroup = 2;
        private const int MaxRadiusSpiral = 12;
        private const int MaxRadiusFromPlayer = 20;

        private static readonly System.Random Roll = new System.Random();

        /// <summary>Nemesis ids already spawned into the floor we are on.</summary>
        private static readonly HashSet<int> PresentThisFloor = new HashSet<int>();

        internal static void OnFloorStarted(State state)
        {
            PresentThisFloor.Clear();

            var raidMetadata = state?.Get<RaidMetadata>();
            if (raidMetadata == null || !IsEligibleRaid(raidMetadata))
            {
                return;
            }

            TryPromote(state, raidMetadata);
            TryReturn(state, raidMetadata);
        }

        /// <summary>
        /// Station visits have no combat and the editor raids are not a game. Vanilla's
        /// own ally-squad code declines the same raid types, for the same reason.
        /// </summary>
        private static bool IsEligibleRaid(RaidMetadata raidMetadata)
        {
            switch (raidMetadata.RaidType)
            {
                case RaidType.Station:
                case RaidType.EditorTestGeneration:
                case RaidType.EditorProcMission:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Promotes one of the floor's existing enemies into a new nemesis.
        ///
        /// <b>The promoted enemy is not modified.</b> It is read - its mob class and
        /// faction - and a row is created from that. The creature standing in front of
        /// the player is left exactly as the game made it, and the nemesis itself
        /// arrives on a later floor. That keeps this mod out of the middle of a fight
        /// that has already started, and means a promotion can never make the current
        /// floor harder than the player signed up for.
        /// </summary>
        private static void TryPromote(State state, RaidMetadata raidMetadata)
        {
            if (ModConfig.PromoteChance <= 0f ||
                NemesisRoster.LivingCount >= ModConfig.MaxLiving)
            {
                return;
            }

            if (Roll.NextDouble() > ModConfig.PromoteChance)
            {
                return;
            }

            var candidate = PickHostile(state);
            if (candidate == null)
            {
                return;
            }

            var record = NemesisRoster.Create(candidate.CreatureData.MobClassId,
                                              candidate.CreatureData.FactionId, DayOf(state));
            MobClassInjector.SyncAll();

            ModLog.Info("promoted: " + NameForge.FullName(record) + " (id " + record.Id +
                        ", from '" + record.BaseMobClassId + "'). It will find you later.");
        }

        /// <summary>
        /// An ordinary hostile to base a new nemesis on. Reservoir-sampled so the choice
        /// is uniform without building a list of the whole floor.
        /// </summary>
        private static Monster PickHostile(State state)
        {
            var creatures = state?.Get<Creatures>();
            if (creatures?.Monsters == null)
            {
                return null;
            }

            Monster chosen = null;
            var seen = 0;

            foreach (var creature in creatures.Monsters)
            {
                if (!(creature is Monster monster) || monster.CreatureData == null)
                {
                    continue;
                }

                // Allies are not candidates, and neither is anything already ours.
                if (monster.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance ||
                    NemesisRoster.ByMobClass(monster.CreatureData.MobClassId) != null ||
                    string.IsNullOrEmpty(monster.CreatureData.MobClassId))
                {
                    continue;
                }

                seen++;
                if (Roll.Next(seen) == 0)
                {
                    chosen = monster;
                }
            }
            return chosen;
        }

        /// <summary>Brings living nemeses back, one roll each, at most one per floor.</summary>
        private static void TryReturn(State state, RaidMetadata raidMetadata)
        {
            if (ModConfig.ReturnChance <= 0f)
            {
                return;
            }

            foreach (var record in NemesisRoster.All)
            {
                if (record.Retired || PresentThisFloor.Contains(record.Id))
                {
                    continue;
                }

                if (Roll.NextDouble() > ModConfig.ReturnChance)
                {
                    continue;
                }

                if (Spawn(state, raidMetadata, record))
                {
                    PresentThisFloor.Add(record.Id);
                    ModLog.Info("arrived: " + NameForge.FullName(record) + " is on this floor");
                }
                break;   // one reunion per floor is a threat; several is a parade
            }
        }

        private static bool Spawn(State state, RaidMetadata raidMetadata, NemesisRecord record)
        {
            try
            {
                var creatures = state.Get<Creatures>();
                var mercenaries = state.Get<Mercenaries>();
                var perkFactory = state.Get<PerkFactory>();
                var difficulty = state.Get<Difficulty>();
                var turnController = state.Get<TurnController>();
                var debugData = state.Get<DungeonGeneratedDebugData>();
                var mapGrid = state.Get<MapGrid>();

                if (creatures?.Player == null || mercenaries == null || perkFactory == null ||
                    difficulty == null || turnController == null || debugData == null ||
                    mapGrid == null)
                {
                    // SpawnFixedGroup dereferences every one of these. Missing any means
                    // we are being called at a moment the game is not ready for a spawn,
                    // and the honest answer is to do nothing.
                    ModLog.Warn("the dungeon systems are not all available yet; " +
                                NameForge.FullName(record) + " waits for another floor");
                    return false;
                }

                var unit = new Unit
                {
                    FactionId = record.FactionId ?? string.Empty,
                    TechLevelLimit = TechLevelOf(state, record),
                    // The alliance the raid's defenders belong to - the same side the
                    // enemy it was promoted from was fighting on.
                    Alliance = CreatureAlliance.VictimFaction,
                    DebugNote = "QuasimorphNemesis " + record.MobClassId,
                };
                unit.Members.Add(record.MobClassId);

                var spawned = SpawnSystem.SpawnFixedGroup(
                    mercenaries, perkFactory, difficulty, turnController, debugData, mapGrid,
                    creatures, raidMetadata, creatures.Player.CreatureData.Position, unit,
                    MinRadiusFromPlayer, MinRadiusInGroup, MaxRadiusSpiral, MaxRadiusFromPlayer,
                    autoSetGroupIndex: true, strictStartingPoint: false, setEndlessHunt: true,
                    debugLabel: "QuasimorphNemesis " + record.MobClassId);

                // SpawnFixedGroup logs its own reason and returns an empty list when it
                // cannot find room. That is a "not this floor", never an exception.
                return spawned != null && spawned.Count > 0;
            }
            catch (Exception error)
            {
                ModLog.Error("could not bring " + NameForge.FullName(record) +
                             " into this raid; it stays in the roster", error);
                return false;
            }
        }

        /// <summary>
        /// Days since the campaign began. <c>SpaceTime</c> carries two DateTimes rather
        /// than a day counter, so the number is derived rather than read.
        /// </summary>
        private static int DayOf(State state)
        {
            try
            {
                var spaceTime = state?.Get<SpaceTime>();
                if (spaceTime == null)
                {
                    return 0;
                }
                return Math.Max(0, (int)(spaceTime.Time - spaceTime.StartGameDate).TotalDays);
            }
            catch (Exception)
            {
                // The day is a label in the log, never a decision. Not knowing it is fine.
                return 0;
            }
        }

        /// <summary>
        /// The equipment tier the nemesis spawns at, resolved the way the sibling Retinue
        /// mod resolves it for a guard: the live faction's current tech level, falling
        /// back to the faction record's starting one.
        ///
        /// Note this is only the <i>ceiling</i>. The escalation a rank actually buys
        /// comes from <c>EquipmentTechLevelBonus</c> on the injected mob class, which is
        /// why a nemesis can outgrow the faction that spawned it.
        /// </summary>
        private static int TechLevelOf(State state, NemesisRecord record)
        {
            try
            {
                if (string.IsNullOrEmpty(record.FactionId))
                {
                    return -1;   // vanilla's "no limit"
                }

                var faction = state?.Get<Factions>()?.Get(record.FactionId, logMissing: false);
                if (faction != null)
                {
                    return faction.CurrentTechLevel;
                }

                return Data.Factions?.GetRecord(record.FactionId)?.InitialTechLevel ?? -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>
        /// The player killed something. If it was a nemesis, that is the end of it - the
        /// row is retired rather than deleted, so the campaign keeps the tally and the
        /// name cannot be handed to somebody else.
        /// </summary>
        internal static void OnMonsterKilled(Creature creature)
        {
            var mobClassId = creature?.CreatureData?.MobClassId;
            var record = NemesisRoster.ByMobClass(mobClassId);
            if (record == null || record.Retired)
            {
                return;
            }

            record.Retired = true;
            PresentThisFloor.Remove(record.Id);
            MobClassInjector.SyncAll();

            ModLog.Info("killed: " + NameForge.FullName(record) + " is finished. It had " +
                        record.Rank + " of yours.");
        }

        /// <summary>
        /// One of the player's mercenaries died. If a nemesis dealt the killing blow it
        /// gains a rank, which is the only way rank is ever earned.
        /// </summary>
        internal static void OnPlayerKilledBy(Creature killer)
        {
            var mobClassId = killer?.CreatureData?.MobClassId;
            var record = NemesisRoster.ByMobClass(mobClassId);
            if (record == null || record.Retired)
            {
                return;
            }

            record.Rank++;
            MobClassInjector.SyncAll();

            ModLog.Info("promoted by your death: " + NameForge.FullName(record) +
                        " is now rank " + record.Rank);
        }
    }
}
