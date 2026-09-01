using System;

namespace QuasimorphRetinue
{
    /// <summary>
    /// The arithmetic shared by the layers.
    ///
    /// Every number in this mod is a multiplier against a value the game computed,
    /// never an absolute, so a game update that retunes a baseline carries the mod
    /// along with it instead of silently overriding it.
    /// </summary>
    internal static class Tuning
    {
        /// <summary>
        /// Folds <c>power</c> into a multiplier. At 1.0 the factor is used as written;
        /// at 0.5 it lands halfway back toward vanilla; at 0.0 it is vanilla exactly,
        /// which is what makes <c>power=0</c> a true off switch rather than an
        /// approximation of one.
        /// </summary>
        internal static float Effective(float factor)
        {
            return 1f + (factor - 1f) * ModConfig.Power;
        }

        internal static float Scale(float vanilla, float factor)
        {
            return vanilla * Effective(factor);
        }

        /// <summary>Scales a count, never rounding a live value down to nothing.</summary>
        internal static int ScaleInt(int vanilla, float factor)
        {
            if (vanilla == 0)
            {
                return 0;
            }
            var scaled = (int)Math.Round(vanilla * Effective(factor), MidpointRounding.AwayFromZero);
            return vanilla > 0 && scaled < 1 ? 1 : scaled;
        }

        /// <summary>
        /// Scales a flat bonus. Unlike a multiplier a bonus of +1 at <c>power=0</c>
        /// must become +0, so this is a straight product rather than a ratio.
        /// </summary>
        internal static int Bonus(int amount)
        {
            return (int)Math.Round(amount * ModConfig.Power, MidpointRounding.AwayFromZero);
        }
    }
}
