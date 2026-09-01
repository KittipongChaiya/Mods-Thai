using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphBigPack
{
    /// <summary>
    /// Plain key=value settings read from config.txt next to the assembly. Written
    /// with defaults on first run so the file is self-documenting.
    ///
    /// Deliberately not an MCM dependency: a mod that only needs four settings should
    /// not be removed from the load order because another mod failed.
    /// </summary>
    internal static class ModConfig
    {
        private const string FileName = "config.txt";

        /// <summary>Rows beyond which the grid is more nuisance than convenience.</summary>
        private const int MaxHeight = 200;

        internal static bool Enabled = true;
        internal static int BackpackHeight = 50;
        internal static bool ResizeVest = false;
        internal static int VestHeight = 4;
        internal static bool RemoveWeight = true;

        private const string Template =
            "# Quasimorph Big Pack\n" +
            "# Unlimited inventory space for your own mercenaries.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Backpack height in rows. Width is never touched: the inventory panel\n" +
            "# scrolls vertically but has no horizontal scrollbar, so a wider grid\n" +
            "# would render off-panel. 1-200.\n" +
            "backpack_height=50\n" +
            "\n" +
            "# The vest is a short horizontal strip and may not scroll at all. Leave\n" +
            "# this off unless you have checked that extra rows are reachable.\n" +
            "resize_vest=false\n" +
            "vest_height=4\n" +
            "\n" +
            "# Zero the weight the penalty formulas see, so a full pack costs you no\n" +
            "# dodge, satiety, or movement. Note this also gives up the weight BONUS\n" +
            "# to melee damage and physical resist. false restores vanilla weight.\n" +
            "remove_weight=true\n";

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
                BackpackHeight = Int(values, "backpack_height", BackpackHeight, 1, MaxHeight);
                ResizeVest = Bool(values, "resize_vest", ResizeVest);
                VestHeight = Int(values, "vest_height", VestHeight, 1, MaxHeight);
                RemoveWeight = Bool(values, "remove_weight", RemoveWeight);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " backpack_height=" + BackpackHeight +
                        " resize_vest=" + ResizeVest +
                        " vest_height=" + VestHeight +
                        " remove_weight=" + RemoveWeight);
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

        private static int Int(Dictionary<string, string> values, string key, int fallback,
                               int min, int max)
        {
            if (!values.TryGetValue(key, out var text) ||
                !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                return fallback;
            }

            if (parsed < min || parsed > max)
            {
                ModLog.Warn(key + "=" + parsed + " is outside " + min + "-" + max + ", clamping");
                return parsed < min ? min : max;
            }
            return parsed;
        }
    }
}
