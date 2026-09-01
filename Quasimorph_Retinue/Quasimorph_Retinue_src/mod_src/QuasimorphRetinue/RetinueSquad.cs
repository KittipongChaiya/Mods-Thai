using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Layer 1 - the squad itself, and the reason this mod exists.
    ///
    /// The game can already do all of this. <c>DungeonGenerator.SpawnAllySquads</c>
    /// builds a unit of faction guards, spawns it beside the player and marks it so it
    /// rides the elevator down with you. It is gated behind a Proxy Corp department
    /// unlock, procedural missions only, beneficiary faction only, first floor only -
    /// which is why most players have never seen it. This class makes the same four
    /// calls, in the same order, without the gate:
    ///
    /// <code>
    /// CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
    /// Behaviour.SetEndlessHunt(true, force: true);   // or StartFollowing, per stance
    /// Behaviour.WaitTransferAtElevator = true;
    /// IsTransferable = true;
    /// </code>
    ///
    /// <b>It tops up rather than spawns.</b> Counting living allies first and spawning
    /// only the shortfall makes four separate situations correct with one rule: a
    /// squad that followed you downstairs is not duplicated, a mid-floor save reload
    /// spawns nothing at all, casualties are replaced on the next floor, and allies you
    /// recruited yourself count toward the total instead of being ignored.
    /// </summary>
    internal static class RetinueSquad
    {
        /// <summary>Vanilla's own spawn geometry for an ally squad, copied unchanged.</summary>
        private const int MinRadiusFromPlayer = 2;
        private const int MinRadiusInGroup = 2;
        private const int MaxRadiusSpiral = 6;
        private const int MaxRadiusFromPlayer = 6;

        internal static void TopUp(State state)
        {
            if (ModConfig.SquadSize <= 0)
            {
                return;
            }

            var creatures = state?.Get<Creatures>();
            var raidMetadata = state?.Get<RaidMetadata>();
            if (creatures?.Player == null || raidMetadata == null)
            {
                return;
            }

            if (!IsEligibleRaid(raidMetadata))
            {
                return;
            }

            var living = AllyIdentity.Living(creatures);
            var shortfall = ModConfig.SquadSize - living.Count;
            if (shortfall <= 0)
            {
                ModLog.Info("retinue at strength: " + living.Count + "/" + ModConfig.SquadSize +
                            " already here, nothing spawned");
                return;
            }

            var mobClassId = ChooseEscortClass(state, raidMetadata, out var factionId, out var techLevel);
            if (mobClassId == null)
            {
                ModLog.Warn("no usable escort mob class found in the game data; " +
                            "no retinue this floor. The squad will be retried next floor.");
                return;
            }

            var spawned = Spawn(state, creatures, raidMetadata, mobClassId, factionId, techLevel, shortfall);
            ModLog.Info("retinue: " + living.Count + " already here, " + spawned + " of " +
                        shortfall + " spawned as '" + mobClassId + "'" +
                        (factionId.Length == 0 ? "" : " (" + factionId + ")") +
                        ", stance " + ModConfig.Stance);
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

        private static int Spawn(State state, Creatures creatures, RaidMetadata raidMetadata,
                                 string mobClassId, string factionId, int techLevel, int count)
        {
            var mercenaries = state.Get<Mercenaries>();
            var perkFactory = state.Get<PerkFactory>();
            var difficulty = state.Get<Difficulty>();
            var turnController = state.Get<TurnController>();
            var debugData = state.Get<DungeonGeneratedDebugData>();
            var mapGrid = state.Get<MapGrid>();

            if (mercenaries == null || perkFactory == null || difficulty == null ||
                turnController == null || debugData == null || mapGrid == null)
            {
                // SpawnFixedGroup dereferences every one of these. Missing any of them
                // means we are being called at a moment the game is not ready for a
                // spawn, and the honest answer is to do nothing.
                ModLog.Warn("the dungeon systems are not all available yet; no retinue this floor");
                return 0;
            }

            var unit = new Unit
            {
                FactionId = factionId,
                TechLevelLimit = techLevel,
                Alliance = CreatureAlliance.PlayerAlliance,
                DebugNote = "QuasimorphRetinue squad",
            };
            for (var i = 0; i < count; i++)
            {
                unit.Members.Add(mobClassId);
            }

            // setEndlessHunt is false here because the stance is applied below - an
            // escort that starts by hunting would walk away from you on turn one.
            var spawned = SpawnSystem.SpawnFixedGroup(
                mercenaries, perkFactory, difficulty, turnController, debugData, mapGrid,
                creatures, raidMetadata, creatures.Player.CreatureData.Position, unit,
                MinRadiusFromPlayer, MinRadiusInGroup, MaxRadiusSpiral, MaxRadiusFromPlayer,
                autoSetGroupIndex: true, strictStartingPoint: false, setEndlessHunt: false,
                debugLabel: "QuasimorphRetinue squad");

            if (spawned == null || spawned.Count == 0)
            {
                // SpawnFixedGroup logs its own reason and returns an empty list when it
                // cannot find room. That is a "not this floor", never an exception.
                return 0;
            }

            foreach (var ally in spawned)
            {
                Configure(ally, creatures);
            }
            return spawned.Count;
        }

        /// <summary>
        /// Turns a freshly spawned monster into a member of your squad. These are the
        /// same four writes vanilla makes for the Proxy Corp squad, plus the stance and
        /// the kit.
        /// </summary>
        private static void Configure(Monster ally, Creatures creatures)
        {
            try
            {
                ally.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
                ally.RefreshAllianceSignal();

                if (string.Equals(ModConfig.Stance, ModConfig.StanceHunter, StringComparison.Ordinal))
                {
                    ally.Behaviour.SetEndlessHunt(value: true, force: true);
                }
                else
                {
                    // FollowTarget is a FightState: it shoots what it sees and follows
                    // when there is nothing to shoot. It is also the state the game's
                    // own ally orders read and write, so the follow/wait and
                    // shoot/hold-fire buttons work on these allies immediately.
                    ally.Behaviour.StartFollowing(creatures.Player);
                }

                ally.Behaviour.WaitTransferAtElevator = true;
                ally.IsTransferable = true;

                if (ModConfig.StartingKit)
                {
                    GiveKit(ally);
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not configure a spawned ally; it will behave as an " +
                             "ordinary friendly creature", error);
            }
        }

        /// <summary>
        /// A medkit so they can patch themselves and each other up, and a second helping
        /// of the ammunition their own mob class would have carried.
        ///
        /// The ammunition is handed over by calling the game's own
        /// <c>SpawnAdditionalAmmo</c> a second time, so it is always the right calibre
        /// for whatever weapon the spawn rolled. No item id is hardcoded anywhere.
        /// </summary>
        private static void GiveKit(Monster ally)
        {
            CreatureSystem.SpawnAdditionalAmmo(ally);

            var medkitId = MedkitId();
            if (medkitId == null)
            {
                return;
            }

            var medkit = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(medkitId);
            if (medkit != null)
            {
                ally.AddItem(medkit);
            }
        }

        private static string _medkitId;
        private static bool _medkitSearched;

        /// <summary>
        /// The plainest medkit in the game, found by class rather than by name: lowest
        /// tech level wins, ties broken by id so the answer is the same every run.
        /// </summary>
        private static string MedkitId()
        {
            if (_medkitSearched)
            {
                return _medkitId;
            }
            _medkitSearched = true;

            try
            {
                var items = Data.Items;
                if (items == null)
                {
                    return null;
                }

                var bestTech = int.MaxValue;
                foreach (var id in items.Ids)
                {
                    var record = ItemRecordOf(items, id);
                    if (record == null || record.ItemClass != ItemClass.Medpack)
                    {
                        continue;
                    }

                    if (record.TechLevel < bestTech ||
                        (record.TechLevel == bestTech &&
                         string.CompareOrdinal(id, _medkitId) < 0))
                    {
                        bestTech = record.TechLevel;
                        _medkitId = id;
                    }
                }

                ModLog.Info(_medkitId == null
                    ? "no Medpack item found; allies spawn without a medkit"
                    : "squad medkit: '" + _medkitId + "' (tech level " + bestTech + ")");
            }
            catch (Exception error)
            {
                ModLog.Error("could not choose a medkit; allies spawn without one", error);
                _medkitId = null;
            }

            return _medkitId;
        }

        /// <summary>
        /// <c>ItemsCollection.GetSimpleRecord</c> assumes every entry is composite and
        /// throws on the ones that are not, so this asks the question defensively.
        /// </summary>
        private static ItemRecord ItemRecordOf(ItemsCollection items, string id)
        {
            return items.GetRecord(id) is CompositeItemRecord composite
                ? composite.GetRecord<ItemRecord>()
                : null;
        }

        /// <summary>
        /// Which creature the squad is made of, decided from the game's own data.
        ///
        /// Factions already nominate their bodyguards - <c>CEOGuardCreatureId</c> is a
        /// faction's best, <c>GuardCreatureId</c> its ordinary one - so there is no need
        /// to hardcode a mob class id and no way for a game update to leave this mod
        /// pointing at something that no longer exists.
        ///
        /// The mission's beneficiary faction is preferred, because those are the people
        /// who hired you and it reads as the squad they sent.
        /// </summary>
        private static string ChooseEscortClass(State state, RaidMetadata raidMetadata,
                                                out string factionId, out int techLevel)
        {
            factionId = string.Empty;
            techLevel = -1;

            if (Data.Factions == null || Data.MobClasses == null)
            {
                return null;
            }

            var forbidden = ObjectiveMobClass(raidMetadata);

            // Preferred: whoever hired us.
            var mission = SafeMission(state, raidMetadata);
            if (mission != null && !string.IsNullOrEmpty(mission.BeneficiaryFactionId))
            {
                var chosen = FromFaction(Data.Factions.GetRecord(mission.BeneficiaryFactionId), forbidden);
                if (chosen != null)
                {
                    var faction = SafeFaction(state, mission.BeneficiaryFactionId);
                    factionId = mission.BeneficiaryFactionId;
                    techLevel = Mathf.Max(mission.MinTechLevel, faction?.CurrentTechLevel ?? 1);
                    return chosen;
                }
            }

            // Otherwise any faction that ships a guard at all.
            foreach (var record in Data.Factions.Records)
            {
                if (record == null || !record.Enabled)
                {
                    continue;
                }
                var chosen = FromFaction(record, forbidden);
                if (chosen != null)
                {
                    factionId = record.Id;
                    var faction = SafeFaction(state, record.Id);
                    techLevel = faction?.CurrentTechLevel ?? record.InitialTechLevel;
                    return chosen;
                }
            }

            return null;
        }

        private static string FromFaction(FactionRecord record, string forbidden)
        {
            if (record == null)
            {
                return null;
            }
            return Usable(record.CEOGuardCreatureId, forbidden) ??
                   Usable(record.GuardCreatureId, forbidden);
        }

        private static string Usable(string mobClassId, string forbidden)
        {
            if (string.IsNullOrEmpty(mobClassId) ||
                string.Equals(mobClassId, forbidden, StringComparison.Ordinal))
            {
                return null;
            }
            return Data.MobClasses.GetRecord(mobClassId) == null ? null : mobClassId;
        }

        /// <summary>
        /// A "kill N of this creature" objective counts by mob class and does not care
        /// who did the killing, so a squad made of the target would let the enemy
        /// complete your mission for you by shooting your own escorts. Cheap to avoid.
        /// </summary>
        private static string ObjectiveMobClass(RaidMetadata raidMetadata)
        {
            var win = raidMetadata.WinCondition;
            if (win == null || win.WinCondition != WinCondition.KillMonster ||
                win.WinConditionParameters == null || win.WinConditionParameters.Count == 0)
            {
                return null;
            }
            return win.WinConditionParameters[0];
        }

        private static Mission SafeMission(State state, RaidMetadata raidMetadata)
        {
            try
            {
                return state.Get<Missions>()?.Get(raidMetadata);
            }
            catch (Exception)
            {
                // Story raids and unusual raid types do not always have a mission
                // record. That is a reason to fall back, not to fail.
                return null;
            }
        }

        private static Faction SafeFaction(State state, string factionId)
        {
            try
            {
                return state.Get<Factions>()?.Get(factionId, logMissing: false);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
