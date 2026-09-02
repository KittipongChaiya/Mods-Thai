using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuasimorphSignals
{
    /// <summary>
    /// Plain key=value settings read from config.txt next to the assembly. Written
    /// with defaults on first run so the file is self-documenting.
    ///
    /// Deliberately not an MCM dependency: a mod that only needs a handful of
    /// settings should not be removed from the load order because another mod failed.
    /// </summary>
    internal static class ModConfig
    {
        private const string FileName = "config.txt";

        internal static bool Enabled = true;
        internal static bool CommandUi = true;
        internal static bool RemoteOrders = true;
        internal static bool MoveOrders = true;
        internal static bool DefaultRoam = false;
        internal static bool YieldToAllyRoamPatrol = true;
        internal static bool Probe = false;

        private const string Template =
            "# Quasimorph - Signals\n" +
            "#\n" +
            "# Roam, and orders that carry out of sight. Adds an Escort/Roam control to\n" +
            "# the ally panel and lets you command an ally you cannot currently see.\n" +
            "# Works on any ally: a vanilla escort, one you bribed, a summon, a quest\n" +
            "# ally, or a squad from the sibling Retinue mod. Enemies are never touched.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched - no patches\n" +
            "# are applied at all.\n" +
            "enabled=true\n" +
            "\n" +
            "# ------------------------------------------------------------- the control\n" +
            "\n" +
            "# Add the Escort/Roam toggle to the ally inspect panel, next to the vanilla\n" +
            "# follow and shoot controls. false keeps the roaming behaviour available to\n" +
            "# the rest of the mod but gives you no way to switch it in game.\n" +
            "command_ui=true\n" +
            "\n" +
            "# Whether a newly seen ally starts out roaming. false means every ally\n" +
            "# behaves exactly as the game made it until you say otherwise.\n" +
            "default_roam=false\n" +
            "\n" +
            "# ------------------------------------------------------------ go there\n" +
            "\n" +
            "# Adds a 'Move to...' button to the ally panel. Press it, then right-click\n" +
            "# anywhere on the floor and that ally walks there and holds position -\n" +
            "# through walls, into rooms you have never seen, across the whole map. It\n" +
            "# will still fight anything it meets on the way and carry on afterwards.\n" +
            "#\n" +
            "# Press the button again to cancel, or give the ally any other order.\n" +
            "# Ordering into a sealed room simply fails after a few turns and says so.\n" +
            "move_orders=true\n" +
            "\n" +
            "# --------------------------------------------------------- out of sight\n" +
            "\n" +
            "# Let you select and order an ally that is not currently visible - through\n" +
            "# a wall, in an unexplored room, or across the floor. This applies to\n" +
            "# allies only. Enemies keep exactly the visibility the game gives them,\n" +
            "# which is checked on every single call and is the reason this is not a\n" +
            "# wallhack.\n" +
            "remote_orders=true\n" +
            "\n" +
            "# ------------------------------------------------------------- other mods\n" +
            "\n" +
            "# The 'Ally Roam/Patrol' Workshop mod adds its own roam state by relabelling\n" +
            "# the vanilla follow button. That button is a two-state control, so the two\n" +
            "# mods cannot both drive it. When true and that mod is loaded, this one\n" +
            "# leaves the panel alone and only its out-of-sight layer runs.\n" +
            "yield_to_ally_roam_patrol=true\n" +
            "\n" +
            "# ------------------------------------------------------------- diagnostics\n" +
            "\n" +
            "# Writes probe.txt next to this file recording the exact UI and\n" +
            "# line-of-sight members this mod resolved, with their accessibility. Costs\n" +
            "# one file write at startup. Off by default.\n" +
            "probe=false\n";

        internal static void Load(string modDirectory)
        {
            var path = Path.Combine(modDirectory, FileName);
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, Template, new UTF8Encoding(false));
                    ModLog.Info("wrote default " + FileName);
                    return;
                }

                var values = Parse(File.ReadAllLines(path));
                Enabled = Bool(values, "enabled", Enabled);
                CommandUi = Bool(values, "command_ui", CommandUi);
                RemoteOrders = Bool(values, "remote_orders", RemoteOrders);
                MoveOrders = Bool(values, "move_orders", MoveOrders);
                DefaultRoam = Bool(values, "default_roam", DefaultRoam);
                YieldToAllyRoamPatrol = Bool(values, "yield_to_ally_roam_patrol", YieldToAllyRoamPatrol);
                Probe = Bool(values, "probe", Probe);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " command_ui=" + CommandUi +
                        " remote_orders=" + RemoteOrders +
                        " move_orders=" + MoveOrders +
                        " default_roam=" + DefaultRoam +
                        " yield_to_ally_roam_patrol=" + YieldToAllyRoamPatrol +
                        " probe=" + Probe);
        }

        private static Dictionary<string, string> Parse(string[] lines)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines)
            {
                var line = raw?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    continue;
                }
                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    continue;
                }
                values[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
            }
            return values;
        }

        private static bool Bool(Dictionary<string, string> values, string key, bool fallback)
        {
            return values.TryGetValue(key, out var text) && bool.TryParse(text, out var parsed)
                ? parsed
                : fallback;
        }
    }
}
