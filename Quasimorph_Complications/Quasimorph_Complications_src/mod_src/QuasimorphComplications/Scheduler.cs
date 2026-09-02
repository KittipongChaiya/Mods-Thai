using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Decides what goes wrong, tells the player, and ticks it.
    ///
    /// <b>One complication per raid, chosen at the start.</b> Two at once is not twice
    /// as interesting - it is noise, and it makes it impossible for a player to learn
    /// what any single complication actually does to a floor. The mod would rather a
    /// raid be plain than muddled.
    /// </summary>
    internal static class Scheduler
    {
        private static readonly List<Complication> Catalogue = new List<Complication>
        {
            new Reinforcements(),
            new FireAboard(),
            new RivalCrew(),
            new LoudFloor(),
        };

        private static readonly System.Random Roll = new System.Random();

        private static Complication _active;
        private static int _lastTickedTurn = -1;

        internal static Complication Active => _active;

        internal static IEnumerable<Complication> All => Catalogue;

        internal static void OnFloorStart(State state)
        {
            // A floor ending is not always announced to us - a reload, a quit, a death -
            // so anything the previous complication changed is given back here as well
            // as at floor end. Restoring twice is harmless; restoring never is not.
            EndActive(state);

            _lastTickedTurn = -1;
            _active = null;

            if (!IsEligibleRaid(state))
            {
                return;
            }

            if (Roll.NextDouble() > ModConfig.Chance)
            {
                ModLog.Info("no complication this raid");
                return;
            }

            _active = Choose(state);
            if (_active == null)
            {
                ModLog.Info("no complication is able to run on this floor");
                return;
            }

            ModLog.Info("complication: " + _active.Id + " - " + _active.Announcement);
            Announce.Banner(_active.Announcement);

            try
            {
                _active.OnFloorStart(state);
            }
            catch (Exception error)
            {
                ModLog.Error("complication '" + _active.Id + "' failed at floor start and " +
                             "has been dropped", error);
                _active = null;
            }
        }

        internal static void OnTurn(State state, int turn)
        {
            if (_active == null || turn == _lastTickedTurn)
            {
                return;
            }
            _lastTickedTurn = turn;

            try
            {
                _active.OnTurn(state, turn);
            }
            catch (Exception error)
            {
                ModLog.Error("complication '" + _active.Id + "' failed on turn " + turn +
                             " and has been dropped", error);
                EndActive(state);
            }
        }

        internal static void OnFloorEnd(State state) => EndActive(state);

        private static void EndActive(State state)
        {
            if (_active == null)
            {
                return;
            }

            var finished = _active;
            _active = null;

            try
            {
                finished.OnFloorEnd(state);
            }
            catch (Exception error)
            {
                ModLog.Error("complication '" + finished.Id + "' failed while cleaning up",
                             error);
            }
        }

        /// <summary>Weighted choice among the complications that can run right now.</summary>
        private static Complication Choose(State state)
        {
            var eligible = new List<Complication>();
            var total = 0;

            foreach (var complication in Catalogue)
            {
                var weight = complication.Weight;
                if (weight <= 0)
                {
                    continue;   // switched off in config
                }

                try
                {
                    if (!complication.CanRun(state))
                    {
                        continue;
                    }
                }
                catch (Exception)
                {
                    continue;   // a complication that cannot answer does not run
                }

                eligible.Add(complication);
                total += weight;
            }

            if (total <= 0)
            {
                return null;
            }

            var pick = Roll.Next(total);
            foreach (var complication in eligible)
            {
                pick -= complication.Weight;
                if (pick < 0)
                {
                    return complication;
                }
            }
            return eligible.Count > 0 ? eligible[eligible.Count - 1] : null;
        }

        /// <summary>
        /// Station visits have no combat and the editor raids are not a game. Vanilla's
        /// own ally-squad code declines the same raid types, for the same reason.
        /// </summary>
        private static bool IsEligibleRaid(State state)
        {
            var raidMetadata = state?.Get<RaidMetadata>();
            if (raidMetadata == null)
            {
                return false;
            }

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
    }
}
