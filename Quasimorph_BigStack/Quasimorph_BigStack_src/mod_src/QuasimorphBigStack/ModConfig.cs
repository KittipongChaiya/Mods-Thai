using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphBigStack
{
    /// <summary>
    /// Plain key=value settings read from config.txt next to the assembly. Written
    /// with defaults on first run so the file is self-documenting.
    /// </summary>
    internal static class ModConfig
    {
        /// <summary>
        /// Hard ceiling, deliberately below <c>short.MaxValue</c> (32767).
        ///
        /// <c>ItemInteractionSystem.FixItemCount</c> computes
        /// <c>GetMaxStackSize(record) + ConsumablesStackBonus</c> and converts the result
        /// with <c>conv.i2</c>. Anything that lands over 32767 wraps negative, so the
        /// headroom is not optional.
        /// </summary>
        private const int MaxAllowed = 30000;

        internal static bool Enabled = true;
        internal static int MaxStack = 9999;
        internal static int WindDownStack = 50;

        private const string Template =
            "# Quasimorph Big Stack\n" +
            "# Raises the maximum stack size of every stackable item.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Maximum items per stack, 1-30000. The upper limit is not arbitrary: the\n" +
            "# game stores stack counts in a 16-bit signed integer and adds a perk bonus\n" +
            "# before converting, so values near 32767 wrap to a negative stack size.\n" +
            "max_stack=9999\n" +
            "\n" +
            "# The stack size you intend to wind down to before uninstalling. Only used\n" +
            "# for the warning in the log: it reports how many inventory slots a stack\n" +
            "# would need once it is split back down to this size. See the README section\n" +
            "# \"Winding down\" - this is the number you would put in max_stack, visit a\n" +
            "# station with, and only then uninstall.\n" +
            "wind_down_stack=50\n";

        internal static void Load(string modDirectory)
        {
            var path = Path.Combine(modDirectory, "config.txt");
            try
            {
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, Template, new UTF8Encoding(false));
                    ModLog.Info("wrote default config.txt");
                    return;
                }

                var values = Parse(File.ReadAllLines(path));
                Enabled = Bool(values, "enabled", Enabled);
                MaxStack = Int(values, "max_stack", MaxStack, 1, MaxAllowed);
                WindDownStack = Int(values, "wind_down_stack", WindDownStack, 1, MaxAllowed);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read config.txt, using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " max_stack=" + MaxStack +
                        " wind_down_stack=" + WindDownStack);
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
