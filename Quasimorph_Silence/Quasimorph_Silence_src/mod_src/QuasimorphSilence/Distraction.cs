using System;
using MGSC;
using UnityEngine;

namespace QuasimorphSilence
{
    /// <summary>
    /// Make a noise somewhere you are not.
    ///
    /// <b>The cheapest thing in this mod, and the only genuinely new tactical option.</b>
    /// <c>CreatureSystem.PropagateNoise</c> is public and static, so raising a noise
    /// event at an arbitrary cell is one call - and because it is the game's own event,
    /// every enemy that already investigates noise investigates this one, with no AI
    /// work on our side at all.
    ///
    /// It costs action points, because a free distraction would be strictly better than
    /// moving and would turn every floor into the same puzzle.
    /// </summary>
    internal static class Distraction
    {
        private static KeyCode _key = KeyCode.T;
        private static bool _keyResolved;

        /// <summary>
        /// Called once per frame from the dungeon hook. Cheap early-outs first: this
        /// runs constantly and must not become real work on a frame where nothing
        /// happened.
        /// </summary>
        internal static void Update(State state)
        {
            if (!ModConfig.Enabled || !ModConfig.Distraction)
            {
                return;
            }

            if (!Input.GetKeyDown(Key()))
            {
                return;
            }

            try
            {
                Throw(state);
            }
            catch (Exception error)
            {
                ModLog.Error("could not make a distraction", error);
            }
        }

        private static void Throw(State state)
        {
            var creatures = state?.Get<Creatures>();
            var player = creatures?.Player;
            if (player?.CreatureData == null)
            {
                return;
            }

            var mapRenderer = state.Get<MapRenderer>();
            if (mapRenderer == null)
            {
                return;
            }

            var target = mapRenderer.GetCellUnderCursor();

            // Making a noise on your own cell is just standing there loudly. It is
            // almost certainly a misclick, and doing it would spend the action points
            // for nothing.
            if (target.X == player.CreatureData.Position.X &&
                target.Y == player.CreatureData.Position.Y)
            {
                ModLog.Info("distraction cancelled: that is where you are standing");
                return;
            }

            // CreatureData carries the points spent this turn and the base maximum, not
            // a "remaining" figure, so the remainder is derived - and the maximum itself
            // depends on how the mercenary is moving, which is why MoveSystem is asked
            // rather than the base value being used directly.
            var data = player.CreatureData;
            var maximum = MoveSystem.GetMaxActionPoints(data.MovementState, data);
            var available = maximum - data.APUsedThisTurn;

            if (available < ModConfig.DistractionApCost)
            {
                ModLog.Info("distraction needs " + ModConfig.DistractionApCost +
                            " action points, you have " + available);
                return;
            }

            data.APUsedThisTurn += ModConfig.DistractionApCost;

            // NoiseType.Step rather than something louder: this is a pebble, not a
            // gunshot, and an enemy investigating it should be curious rather than
            // alarmed.
            CreatureSystem.PropagateNoise(creatures, target, (int)NoiseType.Step,
                                          ModConfig.DistractionRadius);

            ModLog.Info("distraction at " + target.X + "," + target.Y + ", radius " +
                        ModConfig.DistractionRadius + ", heard by " + NoiseWatch.LastHeardBy);
        }

        /// <summary>
        /// Resolves the configured key once. An unparseable key falls back to T and says
        /// so, rather than silently doing nothing every time the player presses it.
        /// </summary>
        private static KeyCode Key()
        {
            if (_keyResolved)
            {
                return _key;
            }
            _keyResolved = true;

            try
            {
                _key = (KeyCode)Enum.Parse(typeof(KeyCode), ModConfig.DistractionKey, true);
            }
            catch (Exception)
            {
                _key = KeyCode.T;
                ModLog.Warn("distraction_key='" + ModConfig.DistractionKey +
                            "' is not a Unity KeyCode name; using T");
            }
            return _key;
        }
    }
}
