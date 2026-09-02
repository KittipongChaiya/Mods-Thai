using System;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Notices which other mods are loaded and records, once, what that means for this
    /// one. It does not block, disable or fight anything - which mods to run is the
    /// player's decision. The point is only that "why did that thing hit so hard?"
    /// should have a visible answer in a log file.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "QuasimorphRuthless",
                "the intended pairing, and the one to watch. That mod already raises " +
                "every enemy's stats and equipment tech level through the shared mob " +
                "class and AI preset records. A nemesis is built by cloning one of " +
                "those records after that mod has written to it, so rank scaling lands " +
                "on top of an already harder enemy. If a rank 3 nemesis feels " +
                "unreasonable on Hardcore Tactical Ruthless, lower health_per_rank and " +
                "max_tech_level_bonus rather than turning the mod off."
            },
            {
                "QuasimorphRetinue",
                "a squad to lose. Retinue's allies are what a nemesis will mostly be " +
                "killing, and an ally death is not a rank - only your own mercenary " +
                "dying counts. The two mods do not interact otherwise."
            },
            {
                "QuasimorphSignals",
                "no interaction. It commands allies; this mod builds enemies."
            },
            {
                "quasimorph.loottracker",
                "'ItemTracker'. It writes to the same two save methods this mod uses. " +
                "Both are additive postfixes under their own keys, so the two rosters " +
                "sit side by side in the save and neither reads the other's node."
            },
            {
                "QM_ItemTracker",
                "writes to the same two save methods this mod uses. Both are additive " +
                "postfixes under their own keys and neither reads the other's node."
            },
            {
                "io.candleeconomy.mod",
                "'Realistic Stock Market'. Writes to the same two save methods. Same " +
                "story: separate keys, additive postfixes, no interaction."
            },
            {
                "LoC_CapEnemySpawn",
                "'Cap Enemy Spawn Tech'. It caps the tech level enemies spawn at. A " +
                "returning nemesis asks for a raised equipment tech level, so that cap " +
                "may quietly flatten the main thing a rank buys. Not a conflict, but " +
                "worth knowing if ranks stop feeling like they matter."
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
