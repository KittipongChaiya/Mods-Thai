using System;
using MGSC;

namespace QuasimorphSilence
{
    /// <summary>
    /// What the game just heard, and who heard it.
    ///
    /// <b>The game already simulates all of this and shows the player none of it.</b>
    /// Every step, every door, every shot and every death raises a noise event with a
    /// position and a radius, and enemies whose current AI state reacts to noise go and
    /// investigate. None of it has ever been on screen. This class is the observation
    /// half of the mod - it changes nothing, it only remembers the last event so the
    /// readout has something true to show.
    ///
    /// <c>CreatureSystem.PropagateNoise</c> is the single funnel every one of those
    /// events passes through, which is why one patch point is enough to see all of them.
    /// </summary>
    internal static class NoiseWatch
    {
        /// <summary>The most recent event, whoever made it.</summary>
        internal static NoiseType LastType { get; private set; }

        internal static int LastRadius { get; private set; }

        internal static CellPosition LastPosition { get; private set; }

        /// <summary>True when the most recent event was one the player made.</summary>
        internal static bool LastWasPlayer { get; private set; }

        /// <summary>How many enemies were close enough and awake enough to react.</summary>
        internal static int LastHeardBy { get; private set; }

        /// <summary>The radius the player's last step would have had before this mod scaled it.</summary>
        internal static int LastVanillaRadius { get; private set; }

        /// <summary>Turn number the last player-made event happened on, so the readout can go quiet.</summary>
        internal static int LastPlayerTurn = -1;

        internal static void Record(Creatures creatures, CellPosition source, int noiseType,
                                    int radius, int vanillaRadius, bool fromPlayer)
        {
            LastType = ToNoiseType(noiseType);
            LastRadius = radius;
            LastVanillaRadius = vanillaRadius;
            LastPosition = source;
            LastWasPlayer = fromPlayer;
            LastHeardBy = CountListeners(creatures, source, radius);
        }

        /// <summary>
        /// Whether this noise came from the player's own mercenary.
        ///
        /// <c>PropagateNoise</c> is handed a position rather than a creature, so the
        /// source has to be inferred - and a cell is a sound way to do it, because no
        /// two creatures occupy one. A step, a door and a shot all raise their event at
        /// the acting creature's own cell.
        /// </summary>
        internal static bool IsPlayerSource(Creatures creatures, CellPosition source)
        {
            try
            {
                var player = creatures?.Player?.CreatureData;
                if (player == null)
                {
                    return false;
                }
                return player.Position.X == source.X && player.Position.Y == source.Y;
            }
            catch (Exception)
            {
                // Not knowing means "not the player", which leaves the vanilla path alone.
                return false;
            }
        }

        /// <summary>
        /// Enemies within the radius whose current AI state actually listens.
        ///
        /// A sleeping or mindless creature has a state that returns false from
        /// <c>ReactsToNoise</c>, and counting it would make the readout lie in the most
        /// annoying possible direction - telling the player they were heard when they
        /// were not.
        /// </summary>
        private static int CountListeners(Creatures creatures, CellPosition source, int radius)
        {
            if (creatures?.Monsters == null || radius <= 0)
            {
                return 0;
            }

            var count = 0;
            try
            {
                foreach (var creature in creatures.Monsters)
                {
                    if (!(creature is Monster monster) || monster.CreatureData == null)
                    {
                        continue;
                    }

                    // Your own squad hearing you is not a threat and should not be
                    // reported as one.
                    if (monster.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance)
                    {
                        continue;
                    }

                    var health = monster.CreatureData.Health;
                    if (health == null || !health.Alive)
                    {
                        continue;
                    }

                    if (!InRange(monster.CreatureData.Position, source, radius))
                    {
                        continue;
                    }

                    if (monster.Behaviour?.CurrentState?.ReactsToNoise == true)
                    {
                        count++;
                    }
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not count who heard that", error);
            }
            return count;
        }

        /// <summary>
        /// Chebyshev distance - a radius of N reaches N cells in every direction
        /// including diagonally, which is how movement works on this grid.
        /// </summary>
        private static bool InRange(CellPosition a, CellPosition b, int radius)
        {
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            return Math.Max(dx, dy) <= radius;
        }

        private static NoiseType ToNoiseType(int value)
        {
            return Enum.IsDefined(typeof(NoiseType), value)
                ? (NoiseType)value
                : NoiseType.None;
        }

        internal static string Describe(NoiseType type)
        {
            switch (type)
            {
                case NoiseType.Step: return "step";
                case NoiseType.Door: return "door";
                case NoiseType.Gunshot: return "gunshot";
                case NoiseType.Explosion: return "explosion";
                case NoiseType.Death: return "death";
                default: return "noise";
            }
        }
    }
}
