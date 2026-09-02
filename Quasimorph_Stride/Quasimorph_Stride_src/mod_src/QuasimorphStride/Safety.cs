using System;
using System.Collections.Generic;

namespace QuasimorphStride
{
    /// <summary>
    /// Containment for the few places a runtime patch calls code it does not own.
    ///
    /// <b>Why the startup <c>Guard</c> is not enough.</b> <see cref="StrideMod"/> wraps
    /// every hook so a failure at load costs the player nothing. The gameplay patches
    /// had no equivalent, and they are the ones that run inside the turn loop and the
    /// UI: an exception escaping a postfix does not fail politely, it interrupts
    /// whatever the game was doing. A mod whose entire purpose is a convenience must
    /// never be able to do that.
    ///
    /// <b>Reported once per site.</b> These paths run on every interaction. A patch that
    /// throws would otherwise write a line per click for the rest of the session, which
    /// buries the first occurrence - the only one that carries useful context - under
    /// thousands of copies.
    /// </summary>
    internal static class Safety
    {
        private static readonly HashSet<string> Reported = new HashSet<string>();

        /// <summary>
        /// Records a failure the first time a given site produces one, and says nothing
        /// on every occasion after that.
        /// </summary>
        internal static void Report(string site, Exception error)
        {
            try
            {
                if (Reported.Add(site))
                {
                    ModLog.Error("'" + site + "' threw; this mod is standing down there " +
                                 "and leaving the game's own answer in place. Reported " +
                                 "once per session", error);
                }
            }
            catch (Exception)
            {
                // Logging must never be the thing that breaks the game either.
            }
        }
    }
}
