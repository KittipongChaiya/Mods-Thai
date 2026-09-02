using System;

namespace QuasimorphSilence
{
    /// <summary>
    /// Notices which other mods are loaded and records, once, what that means for this
    /// one. It does not block, disable or fight anything - which mods to run is the
    /// player's decision. The point is only that "why did they hear that?" should have a
    /// visible answer in a log file.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "VanillaSetBonuses",
                "the only other mod known to patch CreatureSystem.PropagateNoise. Both " +
                "patches are additive and ours only scales footsteps the player made, " +
                "so the two do not contend."
            },
            {
                "QuasimorphRuthless",
                "the intended pairing. That mod gives enemies longer hunt and " +
                "investigate memory, which is precisely what makes noise worth managing " +
                "- a floor that heard you once keeps looking. If you want the harder " +
                "game rather than the easier one, raise run_noise_scale rather than " +
                "lowering slow_noise_scale."
            },
            {
                "com.user.quasimorph.stealthautowalk",
                "'Stealth Auto-Walk'. Complementary and a good pairing: it stops your " +
                "movement when you are seen, this tells you when you are heard. The two " +
                "cover the two halves of being noticed and share no patch targets."
            },
            {
                "NBK_RedSpy_QM_StopOnDetected",
                "'Continue on Monster Detection'. Complementary - it reacts to being " +
                "seen, this mod is about being heard."
            },
            {
                "QuasimorphNemesis",
                "no interaction, but worth knowing: a nemesis investigating a noise is " +
                "the same AI as any other enemy, so a distraction works on it too."
            },
            {
                "QuasimorphRetinue",
                "your squad makes noise too, and this mod does not quieten them - only " +
                "your own mercenary. Three allies walking behind you are three sets of " +
                "footsteps the floor can hear, which is a real cost of bringing them."
            },
        };

        internal static void Run()
        {
            try
            {
                var loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < Known.GetLength(0); i++)
                {
                    var name = Known[i, 0];
                    foreach (var assembly in loaded)
                    {
                        if (!string.Equals(assembly.GetName().Name, name,
                                           StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ModLog.Info("'" + name + "' is loaded: " + Known[i, 1]);
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not inspect the loaded mod set; assuming no conflicts",
                             error);
            }
        }
    }
}
