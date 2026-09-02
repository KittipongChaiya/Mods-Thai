using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace QuasimorphStride
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
        internal static bool OpenDoors = true;
        internal static bool TakeItems = true;
        internal static bool UseContainers = true;
        internal static bool UseElevators = false;
        internal static bool UseVest = false;
        internal static bool AllyInventory = false;
        internal static bool FullInventory = false;
        internal static bool FixTooltip = true;
        internal static bool Probe = false;

        private const string Template =
            "# Quasimorph - Stride\n" +
            "#\n" +
            "# Act while running. In the vanilla game the Run stance forbids every\n" +
            "# interaction: doors will not open, loot cannot be picked up, corpses\n" +
            "# cannot be searched. This lifts that, one category at a time.\n" +
            "#\n" +
            "# It does NOT make those actions free. Interacting still ends your turn\n" +
            "# exactly as it does at walking pace - free actions remain what they have\n" +
            "# always been, a property of the Slow stance alone. This mod buys you\n" +
            "# convenience, never action economy.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched - no patches\n" +
            "# are applied at all.\n" +
            "enabled=true\n" +
            "\n" +
            "# ------------------------------------------------------------------ doors\n" +
            "\n" +
            "# Open and close doors while running, including doors your path crosses on\n" +
            "# the way to somewhere else. Without this, a run order that meets a shut\n" +
            "# door stops dead and throws the rest of the move away.\n" +
            "run_open_doors=true\n" +
            "\n" +
            "# ------------------------------------------------------------------ loot\n" +
            "\n" +
            "# Pick things up off the floor and search corpses while running.\n" +
            "#\n" +
            "# The game gates floor pickup, corpse looting, vest use, the inventory\n" +
            "# screen and the healing screen behind one single check. This key opens\n" +
            "# that check only for the two actions named above and closes it again\n" +
            "# immediately afterwards, so sprinting does not quietly become a free pass\n" +
            "# to the whole inventory. The two keys below cover the rest, separately.\n" +
            "run_take_items=true\n" +
            "\n" +
            "# Use crates, lockers, terminals and other interactive scenery while\n" +
            "# running. Same spirit as picking things up, so it shares its default.\n" +
            "run_use_containers=true\n" +
            "\n" +
            "# Use elevators, ladders and dislocators while running. Off by default,\n" +
            "# deliberately: sprinting straight into an extraction is a change to how a\n" +
            "# raid ends, not a convenience, and that is a decision rather than a fix.\n" +
            "run_use_elevators=false\n" +
            "\n" +
            "# ----------------------------------------------------------------- combat\n" +
            "\n" +
            "# Use vest slots - medkits, stimulants, grenades - while running. Off by\n" +
            "# default: this one is a combat capability rather than a convenience, and\n" +
            "# the Run stance already carries an accuracy penalty precisely because it\n" +
            "# is meant to be the stance you cannot fight from.\n" +
            "run_use_vest=false\n" +
            "\n" +
            "# Open an ally's inventory and fixate its wounds while running. Off by\n" +
            "# default for the same reason.\n" +
            "run_ally_inventory=false\n" +
            "\n" +
            "# ---------------------------------------------------------- everything else\n" +
            "\n" +
            "# Open the full inventory and healing screens while running, by hotkey or\n" +
            "# button, with no restriction at all. This is the blunt version of\n" +
            "# run_take_items and it overrides every key above it. Off by default.\n" +
            "run_full_inventory=false\n" +
            "\n" +
            "# ------------------------------------------------------------- the tooltip\n" +
            "\n" +
            "# The vanilla Run tooltip states in red that inventory and actions are\n" +
            "# forbidden. With this mod running that line is no longer true, so a\n" +
            "# correction is appended below it saying what is actually permitted.\n" +
            "# Set false to leave the tooltip exactly as the game wrote it.\n" +
            "fix_tooltip=true\n" +
            "\n" +
            "# ------------------------------------------------------------- diagnostics\n" +
            "\n" +
            "# Writes probe.txt next to this file recording every patch that attached\n" +
            "# and every game member this mod resolved. Costs one file write at\n" +
            "# startup. Off by default.\n" +
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
                OpenDoors = Bool(values, "run_open_doors", OpenDoors);
                TakeItems = Bool(values, "run_take_items", TakeItems);
                UseContainers = Bool(values, "run_use_containers", UseContainers);
                UseElevators = Bool(values, "run_use_elevators", UseElevators);
                UseVest = Bool(values, "run_use_vest", UseVest);
                AllyInventory = Bool(values, "run_ally_inventory", AllyInventory);
                FullInventory = Bool(values, "run_full_inventory", FullInventory);
                FixTooltip = Bool(values, "fix_tooltip", FixTooltip);
                Probe = Bool(values, "probe", Probe);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " run_open_doors=" + OpenDoors +
                        " run_take_items=" + TakeItems +
                        " run_use_containers=" + UseContainers +
                        " run_use_elevators=" + UseElevators +
                        " run_use_vest=" + UseVest +
                        " run_ally_inventory=" + AllyInventory +
                        " run_full_inventory=" + FullInventory +
                        " fix_tooltip=" + FixTooltip +
                        " probe=" + Probe);
        }

        /// <summary>
        /// True when nothing at all has been unlocked, in which case the mod has no
        /// work to do and says so rather than attaching patches that can only ever
        /// return the answer the game already gave.
        /// </summary>
        internal static bool AnythingUnlocked =>
            OpenDoors || TakeItems || UseContainers || UseElevators ||
            UseVest || AllyInventory || FullInventory;

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

        /// <summary>
        /// A key that is absent falls back silently - that is what a default is for.
        /// A key that is <i>present and unreadable</i> says so, because the two cases
        /// are not alike: someone typed that line meaning something by it.
        ///
        /// This matters most in the direction that is easiest to miss. Four of these
        /// keys default to true, so a mistyped `run_open_doors=flase` intended to switch
        /// a permission off leaves it on instead - the mod ends up granting more than
        /// the player asked for, and the summary line below would report the wrong value
        /// with no hint that anything had gone wrong.
        /// </summary>
        private static bool Bool(Dictionary<string, string> values, string key, bool fallback)
        {
            if (!values.TryGetValue(key, out var text))
            {
                return fallback;
            }

            if (bool.TryParse(text, out var parsed))
            {
                return parsed;
            }

            ModLog.Warn("could not read '" + key + "=" + text + "' as true or false; " +
                        "using " + fallback + ". Check that line for a typo - if you " +
                        "meant to switch something off, it is still on.");
            return fallback;
        }
    }
}
