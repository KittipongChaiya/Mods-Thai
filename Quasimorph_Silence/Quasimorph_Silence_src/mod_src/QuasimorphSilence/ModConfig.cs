using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphSilence
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

        internal static bool QuietMovement = true;
        internal static float SlowNoiseScale = 0.4f;
        internal static float NormalNoiseScale = 1.0f;
        internal static float RunNoiseScale = 1.5f;
        internal static int MinimumRadius = 1;

        internal static bool Readout = true;
        internal static bool LogEveryNoise = false;

        internal static bool Distraction = true;
        internal static string DistractionKey = "T";
        internal static int DistractionRadius = 10;
        internal static int DistractionApCost = 2;

        internal static int StepRadiusOverride = -1;
        internal static int DoorRadiusOverride = -1;
        internal static int DeathRadiusOverride = -1;

        private const string Template =
            "# Quasimorph - Silence\n" +
            "#\n" +
            "# The game already simulates noise in full. Every step, door, shot and\n" +
            "# death raises an event with a radius, and enemies that are awake enough to\n" +
            "# care go and investigate it. None of that has ever been on your screen.\n" +
            "#\n" +
            "# This mod shows it to you, and lets you do something about it.\n" +
            "\n" +
            "# Master switch. false applies no patches at all.\n" +
            "enabled=true\n" +
            "\n" +
            "# --------------------------------------------------------- moving quietly\n" +
            "#\n" +
            "# The game has had a Slow movement mode all along. These settings decide\n" +
            "# what it is worth. They apply to FOOTSTEPS ONLY and to YOUR mercenary\n" +
            "# only - a door bangs the same however carefully you walked up to it, a\n" +
            "# gunshot is a gunshot, and every enemy keeps exactly the noise the game\n" +
            "# gave it.\n" +
            "quiet_movement=true\n" +
            "\n" +
            "# How much of its noise a footstep keeps in each movement mode. 1.0 is\n" +
            "# vanilla. Below 1.0 is quieter, above is louder - so if you want the\n" +
            "# harder game rather than the easier one, raise these instead of lowering\n" +
            "# them and let running get you caught.\n" +
            "slow_noise_scale=0.4\n" +
            "normal_noise_scale=1.0\n" +
            "run_noise_scale=1.5\n" +
            "\n" +
            "# A floor under the scaled radius. Zero would mean an enemy standing next to\n" +
            "# you does not hear you at all, which reads as a bug rather than as stealth.\n" +
            "minimum_radius=1\n" +
            "\n" +
            "# ------------------------------------------------------------- the readout\n" +
            "\n" +
            "# Show what you just made, and how many enemies were close enough and awake\n" +
            "# enough to hear it, beside the movement mode panel.\n" +
            "readout=true\n" +
            "\n" +
            "# Also write every single noise event in the raid to the log. Useful once,\n" +
            "# noisy forever - this is a diagnostic, not a feature.\n" +
            "log_every_noise=false\n" +
            "\n" +
            "# ------------------------------------------------------------- distraction\n" +
            "\n" +
            "# Make a noise somewhere you are not. Press the key, click a cell, and the\n" +
            "# world hears something there - the game's own noise event, so anything that\n" +
            "# investigates noise will investigate that.\n" +
            "distraction=true\n" +
            "distraction_key=T\n" +
            "distraction_radius=10\n" +
            "\n" +
            "# Action points it costs. Free would make it strictly better than moving.\n" +
            "distraction_ap_cost=2\n" +
            "\n" +
            "# --------------------------------------------------------- world overrides\n" +
            "#\n" +
            "# The game's three global noise radii. These are GLOBAL - they apply to\n" +
            "# every creature including you, because that is how the game stores them.\n" +
            "# -1 leaves the game's own value alone, which is the default and the\n" +
            "# recommendation. Raise them for a world where sound carries.\n" +
            "step_radius_override=-1\n" +
            "door_radius_override=-1\n" +
            "death_radius_override=-1\n";

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
                QuietMovement = Bool(values, "quiet_movement", QuietMovement);
                SlowNoiseScale = Float(values, "slow_noise_scale", SlowNoiseScale, 0f, 4f);
                NormalNoiseScale = Float(values, "normal_noise_scale", NormalNoiseScale, 0f, 4f);
                RunNoiseScale = Float(values, "run_noise_scale", RunNoiseScale, 0f, 4f);
                MinimumRadius = Int(values, "minimum_radius", MinimumRadius, 0, 20);
                Readout = Bool(values, "readout", Readout);
                LogEveryNoise = Bool(values, "log_every_noise", LogEveryNoise);
                Distraction = Bool(values, "distraction", Distraction);
                DistractionKey = Text(values, "distraction_key", DistractionKey);
                DistractionRadius = Int(values, "distraction_radius", DistractionRadius, 1, 60);
                DistractionApCost = Int(values, "distraction_ap_cost", DistractionApCost, 0, 20);
                StepRadiusOverride = Int(values, "step_radius_override", StepRadiusOverride, -1, 60);
                DoorRadiusOverride = Int(values, "door_radius_override", DoorRadiusOverride, -1, 60);
                DeathRadiusOverride = Int(values, "death_radius_override", DeathRadiusOverride, -1, 60);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " quiet_movement=" + QuietMovement +
                        " slow=" + SlowNoiseScale.ToString("0.##", CultureInfo.InvariantCulture) +
                        " normal=" + NormalNoiseScale.ToString("0.##", CultureInfo.InvariantCulture) +
                        " run=" + RunNoiseScale.ToString("0.##", CultureInfo.InvariantCulture) +
                        " minimum_radius=" + MinimumRadius +
                        " readout=" + Readout +
                        " distraction=" + Distraction + "(" + DistractionKey + ")" +
                        " overrides=" + StepRadiusOverride + "/" + DoorRadiusOverride +
                        "/" + DeathRadiusOverride);
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

        private static string Text(Dictionary<string, string> values, string key, string fallback)
        {
            return values.TryGetValue(key, out var text) && text.Length > 0 ? text : fallback;
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
                ModLog.Warn(key + "=" + text + " is outside " + min + "-" + max + ", clamping");
                return parsed < min ? min : max;
            }
            return parsed;
        }
    }
}
