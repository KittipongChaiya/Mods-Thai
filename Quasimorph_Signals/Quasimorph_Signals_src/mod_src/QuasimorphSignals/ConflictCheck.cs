using System;

namespace QuasimorphSignals
{
    /// <summary>
    /// Notices which other mods are loaded and records, once, what that means for this
    /// one. It does not block or fight anything - which mods to run is the player's
    /// decision. The point is that "why does my ally panel look like that?" should
    /// have a visible answer in a log file.
    ///
    /// One exception, and it is a narrow one: see <see cref="YieldUi"/>.
    /// </summary>
    internal static class ConflictCheck
    {
        private const string AllyRoamPatrol = "AllyRoamPatrol";

        /// <summary>
        /// True when the 'Ally Roam/Patrol' mod is loaded and this mod has agreed to
        /// leave the ally panel to it.
        ///
        /// This is the one place this mod stands down rather than merely reporting,
        /// because the alternative is not a preference but a defect. That mod adds its
        /// roam state by relabelling the vanilla follow button on every refresh; this
        /// mod adds a second button and refreshes it in the same postfix. Two mods
        /// writing to one panel on the same callback produce whichever result ran last,
        /// which is not a thing a player can debug. Our behaviour and out-of-sight
        /// layers keep running either way - only the control is withdrawn.
        /// </summary>
        internal static bool YieldUi { get; private set; }

        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "QuasimorphRetinue",
                "the intended pairing. That mod spawns and strengthens a squad and sets " +
                "each ally's stance at spawn; this one lets you change that stance per " +
                "ally afterwards, and reach an ally you cannot see. Neither requires " +
                "the other, and that mod applies no Harmony patches - which is exactly " +
                "why this is a separate mod rather than a feature of it."
            },
            {
                AllyRoamPatrol,
                "the same idea, done by relabelling the vanilla follow button. That " +
                "button is a two-state control, so the two approaches cannot share it."
            },
            {
                "QM_FollowerOrders",
                "the 'Direct Follower Orders' mod. Complementary: it adds its own order " +
                "panel and target picker rather than touching the follow button, so it " +
                "and this mod do not contend for the same control."
            },
            {
                "NBK_RedSpy_QM_StopOnDetected",
                "'Continue on Monster Detection'. It postfixes Monster.ShowSignal, as " +
                "this mod does. Ours only ever turns the answer from false to true and " +
                "only for allies, so its behaviour on enemies is unchanged."
            },
            {
                "StealthAutoWalk",
                "'Stealth Auto-Walk'. It postfixes Monster.ShowSignal, as this mod " +
                "does. Ours only ever turns the answer from false to true and only for " +
                "allies, so its behaviour on enemies is unchanged."
            },
            {
                "Squad_Leader",
                "the 'Squad: More operatives' mod. Its operatives are player-alliance " +
                "creatures, so they get the roam control too. Giving an order does not " +
                "write to a character sheet, so unlike a stat change this is safe on a " +
                "persistent mercenary."
            },
        };

        internal static void Run()
        {
            YieldUi = false;

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

                        if (string.Equals(name, AllyRoamPatrol, StringComparison.OrdinalIgnoreCase) &&
                            ModConfig.YieldToAllyRoamPatrol)
                        {
                            YieldUi = true;
                            ModLog.Info("  -> leaving the ally panel to it. Roaming and " +
                                        "out-of-sight orders still work here. Uninstall " +
                                        "that mod, or set yield_to_ally_roam_patrol=false, " +
                                        "to get this mod's Escort/Roam control instead.");
                        }
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
