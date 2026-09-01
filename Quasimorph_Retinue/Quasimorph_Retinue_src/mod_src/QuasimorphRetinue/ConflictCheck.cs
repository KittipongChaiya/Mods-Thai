using System;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Notices which sibling mods are loaded and records, once, what that means for
    /// this one. It does not block, disable or fight anything - which mods to run is
    /// the player's decision. The point is only that "why does my squad feel like
    /// that?" should have a visible answer in a log file.
    /// </summary>
    internal static class ConflictCheck
    {
        /// <summary>Assembly name, and what its presence changes here.</summary>
        private static readonly string[,] Known =
        {
            {
                "QuasimorphRuthless",
                "the intended pairing. Your allies are monsters, so they receive that " +
                "difficulty's enemy multipliers too - more health, damage, sight and " +
                "turns - and this mod's own multipliers stack on top of that. Expect a " +
                "stronger squad on Hardcore Tactical Ruthless than on Normal, facing " +
                "correspondingly stronger enemies."
            },
            {
                "QuasimorphBigPack",
                "unlimited backpack space. Unrelated to the squad, but it makes the " +
                "supplies that recruiting and healing cost effectively free."
            },
            {
                "Squad_Leader",
                "the 'Squad: More operatives' mod. Your own mercenaries are fielded as " +
                "player-alliance allies by it. They count toward squad_size, so the " +
                "retinue spawns fewer guards to make room, but their stats are left " +
                "exactly as the game made them - they are persistent characters, and " +
                "strength written onto one would follow it back to the ship."
            },
            {
                "AllyRoamPatrol",
                "extra ally stances. It patches FollowTarget and IdleFollow; this mod " +
                "only sets the initial stance at spawn, so its orders win afterwards, " +
                "which is the right way round."
            },
            {
                "QM_FollowerOrders",
                "richer ally commands. Complementary - it changes how you give orders, " +
                "this mod changes who is there to receive them."
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
                        if (!string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase))
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
                // Purely informational. It must never be the reason a load fails.
                ModLog.Error("companion check failed; continuing", error);
            }
        }
    }
}
