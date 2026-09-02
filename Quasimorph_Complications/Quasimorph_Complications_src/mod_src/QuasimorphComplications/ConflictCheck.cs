using System;

namespace QuasimorphComplications
{
    /// <summary>
    /// Notices which other mods are loaded and records, once, what that means for this
    /// one. It does not block, disable or fight anything - which mods to run is the
    /// player's decision. The point is only that "why was that floor like that?" should
    /// have a visible answer in a log file.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "QuasimorphSilence",
                "the best pairing in the set. The loud floor complication raises the " +
                "game's global noise radii for one raid; that mod is the only thing " +
                "that will show you what it did. Without it, a loud floor is an " +
                "invisible tax - with it, it is the most readable complication here."
            },
            {
                "QuasimorphRuthless",
                "harder enemies and longer hunt memory. Reinforcements and rival crews " +
                "are drawn from whoever is already on the floor, so they inherit that " +
                "difficulty's scaling automatically. If floors start feeling unfair " +
                "rather than tense, lower 'chance' before turning anything off."
            },
            {
                "QuasimorphNemesis",
                "a named enemy may be on a floor that also has a complication. Nothing " +
                "coordinates them, which is the intent - the campaign should be able to " +
                "produce a bad day on its own."
            },
            {
                "QuasimorphRetinue",
                "a squad changes the arithmetic of reinforcement waves considerably. " +
                "Consider raising reinforcement_size or reinforcement_waves if fights " +
                "stop being a problem."
            },
            {
                "LoC_PreventFloorItemDestruction",
                "'Prevent Floor item destruction from fire/acid'. Directly relevant: it " +
                "stops the fire complication destroying the loot it spreads over, which " +
                "removes the main cost of a floor on fire. Not a conflict - a choice."
            },
            {
                "LoC_CapEnemySpawn",
                "'Cap Enemy Spawn Tech'. Reinforcements are modelled on creatures " +
                "already present, so they are capped along with everything else."
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
