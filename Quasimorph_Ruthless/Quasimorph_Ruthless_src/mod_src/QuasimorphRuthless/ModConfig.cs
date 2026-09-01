using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphRuthless
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
        internal static bool TacticalAi = true;
        internal static bool MobLoadouts = true;
        internal static float Intensity = 1.0f;
        internal static bool Probe = false;

        private const string Template =
            "# Quasimorph - Hardcore Tactical Ruthless\n" +
            "#\n" +
            "# Adds a difficulty called Hardcore Tactical Ruthless to the difficulty\n" +
            "# screen. Nothing below changes any other difficulty: pick a vanilla one\n" +
            "# and the game behaves exactly as it always did.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Layer 2 - enemy behaviour. Longer hunts, grenades, better firemode\n" +
            "# choice, doors that no longer stop anyone, fewer sleeping free kills.\n" +
            "# This is the layer that makes the mode tactical rather than spongy.\n" +
            "tactical_ai=true\n" +
            "\n" +
            "# Layer 3 - enemy loadouts. Better gear on enemies, which is also better\n" +
            "# for you to salvage, against worse condition and less spare ammo. Your\n" +
            "# own mercenaries are built from a different table and are never touched.\n" +
            "mob_loadouts=true\n" +
            "\n" +
            "# Scales every layer-2 and layer-3 delta. 1.0 is the tuned mode.\n" +
            "# 0.5 is half as far from vanilla; 0.0 disables both layers entirely.\n" +
            "# The difficulty preset's own sliders are not scaled by this.\n" +
            "intensity=1.0\n" +
            "\n" +
            "# Diagnostics. Writes probe.txt next to this file listing every difficulty\n" +
            "# preset, AI preset and mob class the game loaded, with their values.\n" +
            "# Costs one file write at startup. Off by default.\n" +
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
                TacticalAi = Bool(values, "tactical_ai", TacticalAi);
                MobLoadouts = Bool(values, "mob_loadouts", MobLoadouts);
                Intensity = Float(values, "intensity", Intensity, 0f, 1f);
                Probe = Bool(values, "probe", Probe);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " tactical_ai=" + TacticalAi +
                        " mob_loadouts=" + MobLoadouts +
                        " intensity=" + Intensity.ToString("0.##", CultureInfo.InvariantCulture) +
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

        private static float Float(Dictionary<string, string> values, string key, float fallback,
                                   float min, float max)
        {
            if (!values.TryGetValue(key, out var text) ||
                !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
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
