using System;

namespace QuasimorphStride
{
    /// <summary>
    /// Notices which other mods are loaded and records, once, what that means for this
    /// one. It does not block, yield to, or fight anything - which mods to run is the
    /// player's decision. The point is that "why did my character stop at that door?"
    /// should have a visible answer in a log file.
    ///
    /// Unlike the sibling Signals mod there is no case here where this one stands down.
    /// Signals yields the ally panel because two mods writing to one button produce
    /// whichever ran last. This mod adds no control and writes no UI state; it answers
    /// three permission questions with a wider yes. Two mods both saying yes is still
    /// yes.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "QM_SpeedToggle",
                "'Speed Toggle'. The one mod on this machine that touches the same " +
                "decision. It prefixes PlayerInteractionSystem.OpenTheDoor to strip the " +
                "door animation pause, and its replacement calls CanInteractObstacles " +
                "itself - so it asks the question this mod answers, gets the wider yes, " +
                "and opens the door without the pause. The two compose; neither needs " +
                "to know about the other."
            },
            {
                "VanillaSetBonuses",
                "it also patches PlayerInteractionSystem.OpenTheDoor. This mod does " +
                "not patch that method at all - it patches the permission check in " +
                "front of it - so there is nothing to contend for."
            },
            {
                "RedsOptionalTweaks",
                "'Red's Opt-in Mod Pack'. It references PlayerInteractionSystem.ProcessCmd, " +
                "which this mod prefixes. Ours reads one field of one argument and never " +
                "returns false, so it cannot change what that method does and Harmony " +
                "runs both regardless of order."
            },
            {
                "AllyRoamPatrol",
                "'Ally Roam/Patrol'. It references PlayerInteractionSystem.ProcessCmd, " +
                "as this mod does. Same reasoning: our prefix only opens a scope."
            },
            {
                "TravelAdvanced",
                "it references PlayerInteractionSystem.MovePlayer, which this mod does " +
                "not patch. This mod changes the answer MovePlayer gets when it asks " +
                "whether a door may be opened, not MovePlayer itself."
            },
            {
                "WalkAndReload",
                "'Walk and Auto Reload'. It patches Player.OnMoved and Player.SkipTurn - " +
                "the movement itself, not permission to interact. Complementary."
            },
            {
                "StealthAutoWalk",
                "'Stealth Auto-Walk'. It drops the player out of the Run stance when an " +
                "enemy appears. That is the opposite end of the same subject and the two " +
                "agree: this mod makes running less punishing, that one stops you " +
                "running into a room you should have walked into."
            },
            {
                "QuasimorphSignals",
                "the sibling mod. It commands allies; this one changes what the player " +
                "may do while running. No shared target."
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
