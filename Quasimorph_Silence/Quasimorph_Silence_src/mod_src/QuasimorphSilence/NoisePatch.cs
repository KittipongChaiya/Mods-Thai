using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphSilence
{
    /// <summary>
    /// The one patch point the whole mod needs.
    ///
    /// <c>CreatureSystem.PropagateNoise</c> is public, static, and the single funnel
    /// every noise event in the game passes through - steps, doors, gunshots,
    /// explosions, deaths. Prefixing it changes what the world hears; postfixing it
    /// tells the player what just happened. Nothing else in this mod touches the noise
    /// system at all.
    ///
    /// <b>The player is the only creature this patch will quieten.</b> An enemy's noise
    /// goes through the vanilla path with its vanilla radius, checked on every single
    /// call. That check is a cell comparison rather than a mode or a flag set earlier,
    /// so there is no state that can be left switched on by accident.
    /// </summary>
    [HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.PropagateNoise))]
    internal static class PropagateNoisePatch
    {
        /// <summary>
        /// Carries the untouched radius from the prefix to the postfix, so the readout
        /// can say what the step <i>would</i> have cost. Game logic is single-threaded,
        /// so "the call in progress" is unambiguous.
        /// </summary>
        [ThreadStatic] private static int _vanillaRadius;

        [ThreadStatic] private static bool _fromPlayer;

        [HarmonyPrefix]
        internal static void Prefix(Creatures creatures, CellPosition noiseSource,
                                    int noiseType, ref int radius)
        {
            _vanillaRadius = radius;
            _fromPlayer = false;

            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                _fromPlayer = NoiseWatch.IsPlayerSource(creatures, noiseSource);
                if (!_fromPlayer || !ModConfig.QuietMovement)
                {
                    return;
                }

                var scale = ScaleFor(creatures, noiseType);
                if (scale >= 1f)
                {
                    return;
                }

                var quietened = (int)Math.Round(radius * scale);

                // Never silence completely unless the player asked for exactly that. A
                // noise radius of zero means an enemy standing next to you does not hear
                // you open the door in their face, which reads as a bug rather than as
                // stealth.
                var floor = ModConfig.MinimumRadius;
                radius = Math.Max(floor, quietened);
            }
            catch (Exception error)
            {
                ModLog.Error("could not scale the noise; the vanilla radius stands", error);
            }
        }

        [HarmonyPostfix]
        internal static void Postfix(Creatures creatures, CellPosition noiseSource,
                                     int noiseType, int radius)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                NoiseWatch.Record(creatures, noiseSource, noiseType, radius,
                                  _vanillaRadius, _fromPlayer);

                if (_fromPlayer)
                {
                    NoiseWatch.LastPlayerTurn = SilenceMod.CurrentTurn;
                }

                if (ModConfig.LogEveryNoise)
                {
                    ModLog.Info((_fromPlayer ? "you: " : "world: ") +
                                NoiseWatch.Describe(NoiseWatch.LastType) +
                                " at " + noiseSource.X + "," + noiseSource.Y +
                                " radius " + radius +
                                (_vanillaRadius != radius ? " (vanilla " + _vanillaRadius + ")" : "") +
                                ", heard by " + NoiseWatch.LastHeardBy);
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not record the noise", error);
            }
        }

        /// <summary>
        /// How much of its noise this event keeps.
        ///
        /// Only footsteps scale with how you are moving - a door bangs the same however
        /// carefully you were walking towards it, and a gunshot is a gunshot. Applying
        /// the movement multiplier to everything would let the player fire a rifle
        /// quietly by tiptoeing, which is not stealth, it is a silencer you did not earn.
        /// </summary>
        private static float ScaleFor(Creatures creatures, int noiseType)
        {
            if (noiseType != (int)NoiseType.Step)
            {
                return 1f;
            }

            var state = creatures?.Player?.CreatureData?.MovementState
                        ?? CreatureMovementState.Normal;

            switch (state)
            {
                case CreatureMovementState.Slow:
                    return ModConfig.SlowNoiseScale;
                case CreatureMovementState.Run:
                    return ModConfig.RunNoiseScale;
                default:
                    return ModConfig.NormalNoiseScale;
            }
        }
    }
}
