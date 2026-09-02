using System;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// The floor called for help, and help is on its way.
    ///
    /// A wave of the defending faction's own troops arrives every few turns, away from
    /// the player, and hunts. The clock is the point: a floor you could clear at leisure
    /// becomes a floor you have to leave.
    ///
    /// <b>The trade.</b> Ruthless's rule is that difficulty should be a trade and not a
    /// tax, so this complication drops a supply cache on the first turn. Someone had to
    /// carry the ammunition that is about to be shot at you, and taking it off them
    /// early is the reward for recognising the situation quickly.
    /// </summary>
    internal sealed class Reinforcements : Complication
    {
        internal override string Id => "reinforcements";

        internal override string Announcement =>
            "The floor has called for reinforcements. Expect company, and keep moving.";

        private int _wavesSent;

        internal override void OnFloorStart(State state)
        {
            _wavesSent = 0;
            Guard("floor start", () => DropCache(state));
        }

        internal override void OnTurn(State state, int turn)
        {
            if (turn <= 0 || turn % ModConfig.ReinforcementInterval != 0)
            {
                return;
            }

            if (_wavesSent >= ModConfig.ReinforcementWaves)
            {
                return;
            }

            Guard("turn " + turn, () =>
            {
                if (Spawns.Wave(state, ModConfig.ReinforcementSize, hostile: true,
                                minDistance: ModConfig.ReinforcementDistance,
                                label: "reinforcement wave"))
                {
                    _wavesSent++;
                    ModLog.Info("reinforcement wave " + _wavesSent + " of " +
                                ModConfig.ReinforcementWaves + " arrived on turn " + turn);
                    Announce.Flash("Reinforcements have landed.");
                }
            });
        }

        /// <summary>
        /// The compensation, dropped where the player can reach it rather than where the
        /// enemy will. A complication that only takes is a tax.
        /// </summary>
        private void DropCache(State state)
        {
            var itemsOnFloor = state?.Get<ItemsOnFloor>();
            var mapGrid = state?.Get<MapGrid>();
            if (itemsOnFloor == null || mapGrid == null)
            {
                return;
            }

            if (!MapPick.FarCell(state, ModConfig.CacheDistance, out var cell))
            {
                ModLog.Info("no room on this floor for the supply cache");
                return;
            }

            var dropped = Loot.DropCache(itemsOnFloor, mapGrid, cell, ModConfig.CacheSize);
            ModLog.Info("supply cache: " + dropped + " item(s) at " + cell.X + "," + cell.Y);
        }
    }
}
