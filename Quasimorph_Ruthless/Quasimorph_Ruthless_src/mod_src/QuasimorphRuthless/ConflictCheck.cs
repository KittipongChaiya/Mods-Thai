using System;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Notices when a mod that hands the player a permanent advantage is loaded
    /// alongside this one, and says so once, in the log.
    ///
    /// It does not block, disable or fight anything. Which mods to run is the
    /// player's decision, and a difficulty mod quietly overriding that decision
    /// would be worse behaviour than the conflict it is warning about. The point is
    /// only that "Ruthless felt easy" should have a visible answer.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>
        /// Assembly name, and what it gives the player that this mode is built to
        /// take away.
        /// </summary>
        private static readonly string[,] Known =
        {
            { "QuasimorphBigPack", "unlimited backpack space and no carry weight" },
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
                        if (!string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        ModLog.Warn("'" + name + "' is loaded. It grants " + Known[i, 1] + ", " +
                                    "which this difficulty is designed to deny. Both will work, " +
                                    "but the mode will play considerably easier than intended.");
                        break;
                    }
                }
            }
            catch (Exception error)
            {
                // Purely informational. It must never be the reason a load fails.
                ModLog.Error("conflict check failed; continuing", error);
            }
        }
    }
}
