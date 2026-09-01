using System;

namespace QuasimorphRuthless
{
    /// <summary>
    /// The arithmetic shared by the behaviour layers.
    ///
    /// Everything here is expressed as a multiplier against a captured vanilla value
    /// rather than an absolute number, so a game update that retunes the baseline
    /// carries the mod along with it instead of silently overriding it.
    /// </summary>
    internal static class Tuning
    {
        /// <summary>
        /// Folds <c>intensity</c> into a multiplier. At 1.0 the factor is used as
        /// written; at 0.5 it lands halfway back toward vanilla; at 0.0 it is vanilla.
        /// </summary>
        internal static float Effective(float factor)
        {
            return 1f + (factor - 1f) * ModConfig.Intensity;
        }

        internal static float Scale(float vanilla, float factor)
        {
            return vanilla * Effective(factor);
        }

        /// <summary>
        /// Scales an integer, refusing to round a live value down to nothing. A
        /// hunt memory of 1 turn scaled by 0.6 should stay 1 turn, not become an
        /// enemy that forgets you instantly.
        /// </summary>
        internal static int ScaleInt(int vanilla, float factor)
        {
            if (vanilla == 0)
            {
                return 0;
            }
            var scaled = (int)Math.Round(vanilla * Effective(factor), MidpointRounding.AwayFromZero);
            if (vanilla > 0 && scaled < 1)
            {
                return 1;
            }
            return scaled;
        }

        /// <summary>
        /// Scales a probability, with two guarantees that matter more than the number.
        ///
        /// <b>Zero stays zero.</b> A behaviour the designers switched off for a creature
        /// stays off. This is what stops a mindless horror from learning to throw
        /// grenades because a multiplier was applied to every record in the table.
        ///
        /// <b>The ceiling calibrates itself.</b> The game mixes 0-1 chances and 0-100
        /// percentages across its config tables and the difference is not visible from
        /// the field type. Reading the scale off the vanilla value is correct for both
        /// and cannot clamp a percentage down to 1.
        /// </summary>
        internal static float ScaleChance(float vanilla, float factor)
        {
            if (vanilla <= 0f)
            {
                return vanilla;
            }

            var ceiling = vanilla > 1f ? 100f : 1f;
            var scaled = vanilla * Effective(factor);

            if (scaled < 0f)
            {
                return 0f;
            }
            return scaled > ceiling ? ceiling : scaled;
        }
    }
}
