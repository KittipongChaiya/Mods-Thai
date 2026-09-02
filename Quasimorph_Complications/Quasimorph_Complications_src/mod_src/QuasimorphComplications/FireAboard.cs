using System;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Something is burning, and it is spreading.
    ///
    /// A few cells catch on the first turn and the fire creeps outward. It closes
    /// routes, it makes a corridor a decision rather than a corridor, and it burns the
    /// faction's cargo - so a floor on fire is a floor you have to loot in a hurry.
    ///
    /// <b>This is the most dangerous complication in the mod and it is capped in three
    /// ways.</b> A fire that gets away could make a floor genuinely unwinnable, which is
    /// not difficulty but a broken raid. So: it seeds a fixed small number of cells,
    /// never within <c>fire_safe_distance</c> of the player, and stops spreading
    /// entirely once <c>fire_max_cells</c> are alight. The player can also switch it off
    /// on its own without touching the rest of the mod.
    /// </summary>
    internal sealed class FireAboard : Complication
    {
        internal override string Id => "fire";

        internal override string Announcement =>
            "There is a fire aboard, and nobody is coming to put it out.";

        private int _lit;

        internal override bool CanRun(State state)
        {
            // Without a fire controller there is no fire to have.
            return state?.Get<FireController>() != null;
        }

        internal override void OnFloorStart(State state)
        {
            _lit = 0;
            Guard("floor start", () => Seed(state, ModConfig.FireSeedCells));
        }

        internal override void OnTurn(State state, int turn)
        {
            if (turn <= 0 || turn % ModConfig.FireSpreadInterval != 0)
            {
                return;
            }

            if (_lit >= ModConfig.FireMaxCells)
            {
                return;   // the cap, and the reason a floor stays winnable
            }

            Guard("turn " + turn, () => Seed(state, 1));
        }

        private void Seed(State state, int count)
        {
            var fire = state?.Get<FireController>();
            if (fire == null)
            {
                return;
            }

            var cells = MapPick.FarCells(state, ModConfig.FireSafeDistance, count);
            foreach (var cell in cells)
            {
                if (_lit >= ModConfig.FireMaxCells)
                {
                    return;
                }

                if (fire.HasFire(cell))
                {
                    continue;
                }

                // isPlayerFire: false - this is the station burning, not something the
                // player did, so nothing about it should be credited to them.
                fire.AddFire(cell, EFireType.WeakFire, null, propagate: true,
                             isPlayerFire: false, visualDelay: 0f);
                _lit++;
            }

            if (cells.Count > 0)
            {
                ModLog.Info("fire: " + _lit + " of at most " + ModConfig.FireMaxCells +
                            " cell(s) alight");
            }
        }
    }
}
