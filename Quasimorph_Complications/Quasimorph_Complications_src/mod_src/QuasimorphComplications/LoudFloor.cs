using System;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Sound carries here.
    ///
    /// Raises the game's three global noise radii for the duration of one raid. Every
    /// footstep, every door and every death reaches further, so a floor that heard you
    /// once keeps knowing roughly where you are.
    ///
    /// <b>It cuts both ways, which is why it is a complication and not a punishment.</b>
    /// The radii are global: the enemy's death across the corridor is now audible to
    /// you as well, and the sibling Silence mod - which shows you exactly what you just
    /// made and how many heard it - turns this from an invisible tax into the most
    /// readable complication in the mod.
    ///
    /// <b>Restore is mandatory.</b> These are shared global settings, and the next raid
    /// did not agree to them. The vanilla values are snapshotted on the way in and put
    /// back in <see cref="OnFloorEnd"/> - the same discipline the sibling Ruthless mod
    /// uses for the AI presets it writes to.
    /// </summary>
    internal sealed class LoudFloor : Complication
    {
        internal override string Id => "loud";

        internal override string Announcement =>
            "The hull carries sound. Everything here is listening.";

        private bool _applied;
        private int _step;
        private int _door;
        private int _death;

        internal override bool CanRun(State state)
        {
            return Data.Global != null;
        }

        internal override void OnFloorStart(State state)
        {
            Guard("floor start", () =>
            {
                var settings = Data.Global;
                if (settings == null || _applied)
                {
                    return;
                }

                _step = settings.NoiseStepRadius;
                _door = settings.NoiseDoorRadius;
                _death = settings.NoiseDeathRadius;
                _applied = true;

                settings.NoiseStepRadius = Scale(_step);
                settings.NoiseDoorRadius = Scale(_door);
                settings.NoiseDeathRadius = Scale(_death);

                ModLog.Info("loud floor: step " + _step + "->" + settings.NoiseStepRadius +
                            ", door " + _door + "->" + settings.NoiseDoorRadius +
                            ", death " + _death + "->" + settings.NoiseDeathRadius);
            });
        }

        internal override void OnFloorEnd(State state)
        {
            Guard("floor end", () => Restore());
        }

        /// <summary>
        /// Puts the world back. Called from the floor-end hook and again from the
        /// scheduler when a raid ends any other way, because leaving the whole game
        /// permanently louder would be the worst bug this mod could have.
        /// </summary>
        internal void Restore()
        {
            if (!_applied)
            {
                return;
            }

            var settings = Data.Global;
            if (settings == null)
            {
                return;
            }

            settings.NoiseStepRadius = _step;
            settings.NoiseDoorRadius = _door;
            settings.NoiseDeathRadius = _death;
            _applied = false;

            ModLog.Info("loud floor: noise radii restored to " + _step + "/" + _door +
                        "/" + _death);
        }

        private static int Scale(int value)
        {
            return (int)Math.Round(Math.Max(1, value) * ModConfig.LoudFloorScale);
        }
    }
}
