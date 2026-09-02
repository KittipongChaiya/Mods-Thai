using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Finding somewhere on the floor to put something.
    ///
    /// Every complication that spawns, drops or ignites needs a cell that is real, not
    /// a wall, and not on top of the player. This is the one place that decision is
    /// made, so a complication never has to reason about map geometry itself.
    /// </summary>
    internal static class MapPick
    {
        private static readonly System.Random Roll = new System.Random();

        /// <summary>
        /// A walkable cell at least <paramref name="minDistance"/> from the player.
        ///
        /// Returns false rather than a bad cell when the floor cannot offer one - a
        /// small or crowded map is a real answer, and a complication that cannot be
        /// placed simply does not happen. That is the same contract
        /// <c>SpawnSystem.SpawnFixedGroup</c> uses when it cannot find room, and the
        /// sibling Retinue and Nemesis mods both treat it as "not this floor" rather
        /// than as an error.
        /// </summary>
        internal static bool FarCell(State state, int minDistance, out CellPosition cell)
        {
            cell = CellPosition.Zero;

            var mapGrid = state?.Get<MapGrid>();
            var player = state?.Get<Creatures>()?.Player?.CreatureData;
            if (mapGrid == null || player == null)
            {
                return false;
            }

            var width = mapGrid.MaxWidth;
            var height = mapGrid.MaxHeight;
            if (width <= 2 || height <= 2)
            {
                return false;
            }

            // Bounded rather than exhaustive: a floor with room will answer quickly, and
            // one without should not cost a full grid scan every turn.
            for (var attempt = 0; attempt < 200; attempt++)
            {
                var candidate = new CellPosition(Roll.Next(1, width - 1),
                                                 Roll.Next(1, height - 1));

                if (mapGrid.IsWall(candidate))
                {
                    continue;
                }

                if (Chebyshev(candidate, player.Position) < minDistance)
                {
                    continue;
                }

                cell = candidate;
                return true;
            }
            return false;
        }

        /// <summary>Several distinct cells, for a complication that seeds more than one.</summary>
        internal static List<CellPosition> FarCells(State state, int minDistance, int count)
        {
            var cells = new List<CellPosition>();
            for (var i = 0; i < count; i++)
            {
                if (!FarCell(state, minDistance, out var cell))
                {
                    break;
                }

                var duplicate = false;
                foreach (var existing in cells)
                {
                    if (existing.X == cell.X && existing.Y == cell.Y)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    cells.Add(cell);
                }
            }
            return cells;
        }

        internal static int Chebyshev(CellPosition a, CellPosition b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        }
    }
}
