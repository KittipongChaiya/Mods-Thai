using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphRetinue
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

        internal const string StanceEscort = "escort";
        internal const string StanceHunter = "hunter";

        internal static bool Enabled = true;
        internal static string OnlyOnDifficulty = string.Empty;

        internal static int SquadSize = 3;
        internal static string Stance = StanceEscort;
        internal static bool StartingKit = true;

        internal static bool AllyPower = true;
        internal static float Power = 1.0f;

        internal static bool Recruiting = true;
        internal static bool Spectator = false;

        internal static bool Probe = false;

        private const string Template =
            "# Quasimorph - Retinue\n" +
            "#\n" +
            "# A squad that fights so you do not have to. Allies spawn with you, are\n" +
            "# topped up every floor, and are strong enough to matter. Enemies are\n" +
            "# never touched by anything in this file.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Restrict the whole mod to one difficulty preset id. Empty means every\n" +
            "# difficulty. Set it to HardcoreTacticalRuthless to run this only as a\n" +
            "# companion to the sibling Hardcore Tactical Ruthless mod.\n" +
            "only_on_difficulty=\n" +
            "\n" +
            "# --------------------------------------------------------------- the squad\n" +
            "\n" +
            "# How many allies to keep alive around you. Counted at the start of every\n" +
            "# floor and topped up, so survivors are never duplicated and casualties are\n" +
            "# replaced. 0 disables the squad and leaves you whatever allies the game\n" +
            "# itself gives you - which the rest of this file still makes stronger.\n" +
            "#\n" +
            "# Every extra body is extra time per turn. 3 is the tuned value; above 5\n" +
            "# a floor starts to feel slow rather than safe.\n" +
            "squad_size=3\n" +
            "\n" +
            "# escort = they follow you, screen you and shoot what they see.\n" +
            "# hunter = they leave you behind and go looking for the enemy.\n" +
            "# Either way you can re-order any of them individually in game: click an\n" +
            "# ally to get follow/wait and shoot-at-will/hold-fire buttons.\n" +
            "stance=escort\n" +
            "\n" +
            "# Give each newly spawned ally a medkit and a spare magazine, so they can\n" +
            "# patch themselves up and keep firing without you playing quartermaster.\n" +
            "starting_kit=true\n" +
            "\n" +
            "# ------------------------------------------------------------ ally strength\n" +
            "\n" +
            "# Make every ally stronger - the retinue, anyone you recruit with a gift,\n" +
            "# anything you summon, and any ally a quest hands you.\n" +
            "ally_power=true\n" +
            "\n" +
            "# Scales how far above vanilla an ally lands. 1.0 is the tuned value and is\n" +
            "# balanced against Hardcore Tactical Ruthless. 0.0 leaves allies exactly as\n" +
            "# the game made them. Values above 1.0 are allowed and are on you.\n" +
            "power=1.0\n" +
            "\n" +
            "# ------------------------------------------------------------- recruitment\n" +
            "\n" +
            "# Widen the game's own gift mechanic. Drop an item a thinking enemy wants\n" +
            "# where it can see it, and it will walk over, pick it up and join you\n" +
            "# permanently. Vanilla already does this for a few creatures; this opens it\n" +
            "# to every enemy that reasons about its equipment. Mindless ones never learn.\n" +
            "recruiting=true\n" +
            "\n" +
            "# ------------------------------------------------------------- your own role\n" +
            "\n" +
            "# Enemies stop targeting your mercenary entirely - you can stand in the open\n" +
            "# and watch. This is a cheat and is labelled as one. Fire, gas and explosions\n" +
            "# do not care about it, so you can still die if you stand in them.\n" +
            "spectator=false\n" +
            "\n" +
            "# ------------------------------------------------------------- diagnostics\n" +
            "\n" +
            "# Writes probe.txt next to this file listing every faction, guard mob class\n" +
            "# and AI preset the game loaded, with the values this mod reasons about.\n" +
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
                OnlyOnDifficulty = Text(values, "only_on_difficulty", OnlyOnDifficulty);
                SquadSize = Int(values, "squad_size", SquadSize, 0, 8);
                Stance = Choice(values, "stance", Stance, StanceEscort, StanceHunter);
                StartingKit = Bool(values, "starting_kit", StartingKit);
                AllyPower = Bool(values, "ally_power", AllyPower);
                Power = Float(values, "power", Power, 0f, 4f);
                Recruiting = Bool(values, "recruiting", Recruiting);
                Spectator = Bool(values, "spectator", Spectator);
                Probe = Bool(values, "probe", Probe);
            }
            catch (Exception error)
            {
                // A bad or unreadable config must leave the mod on its defaults,
                // never stop it loading.
                ModLog.Error("could not read " + FileName + ", using defaults", error);
            }

            ModLog.Info("config: enabled=" + Enabled +
                        " only_on_difficulty=" + (OnlyOnDifficulty.Length == 0 ? "(any)" : OnlyOnDifficulty) +
                        " squad_size=" + SquadSize +
                        " stance=" + Stance +
                        " starting_kit=" + StartingKit +
                        " ally_power=" + AllyPower +
                        " power=" + Power.ToString("0.##", CultureInfo.InvariantCulture) +
                        " recruiting=" + Recruiting +
                        " spectator=" + Spectator +
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

        private static string Text(Dictionary<string, string> values, string key, string fallback)
        {
            return values.TryGetValue(key, out var text) ? text : fallback;
        }

        /// <summary>
        /// A key whose value must be one of a fixed set. A typo falls back to the
        /// default and says so, rather than silently disabling a layer.
        /// </summary>
        private static string Choice(Dictionary<string, string> values, string key, string fallback,
                                     params string[] allowed)
        {
            if (!values.TryGetValue(key, out var text))
            {
                return fallback;
            }

            foreach (var option in allowed)
            {
                if (string.Equals(text, option, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            ModLog.Warn(key + "=" + text + " is not one of [" + string.Join(", ", allowed) +
                        "], using " + fallback);
            return fallback;
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
                ModLog.Warn(key + "=" + parsed + " is outside " + min + "-" + max + ", clamping");
                return parsed < min ? min : max;
            }
            return parsed;
        }
    }
}
