using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphNemesis
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
        internal static string OnlyOnDifficulty = string.Empty;

        internal static float PromoteChance = 0.25f;
        internal static int MaxLiving = 3;
        internal static int RosterCap = 32;
        internal static float ReturnChance = 0.5f;

        internal static float HealthPerRank = 0.15f;
        internal static float DodgePerRank = 0.05f;
        internal static int MaxTechLevelBonus = 3;
        internal static int RankForExtraTurn = 3;

        internal static bool Probe = false;

        private const string Template =
            "# Quasimorph - Nemesis\n" +
            "#\n" +
            "# Enemies that remember you. Now and then a hostile is promoted to a named\n" +
            "# elite. If it kills one of your mercenaries it survives, is remembered for\n" +
            "# the rest of the campaign, gains a rank and comes back better equipped.\n" +
            "# Kill it and it is gone for good, and the tally is yours.\n" +
            "#\n" +
            "# Ordinary enemies are never touched. A nemesis is built from its own\n" +
            "# generated mob class, so nothing here can leak onto the rest of the world.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Restrict the whole mod to one difficulty preset id. Empty means every\n" +
            "# difficulty. Set it to HardcoreTacticalRuthless to run this only as a\n" +
            "# companion to the sibling Hardcore Tactical Ruthless mod.\n" +
            "only_on_difficulty=\n" +
            "\n" +
            "# ------------------------------------------------------------- promotion\n" +
            "\n" +
            "# Chance that a raid promotes one of its enemies to a new nemesis, when\n" +
            "# there is room under max_living. 0.0 stops new ones appearing; the ones\n" +
            "# you already have keep coming back.\n" +
            "promote_chance=0.25\n" +
            "\n" +
            "# How many nemeses may be alive at once. This is the pacing dial: a low\n" +
            "# number keeps each one memorable, a high one turns them into a faction.\n" +
            "max_living=3\n" +
            "\n" +
            "# Chance that a living nemesis turns up in any given eligible raid. Below\n" +
            "# 1.0 they are a threat you cannot plan around, which is the intent.\n" +
            "return_chance=0.5\n" +
            "\n" +
            "# How many rows the campaign remembers in total, killed ones included.\n" +
            "# Retired rows are dropped first when this is reached.\n" +
            "roster_cap=32\n" +
            "\n" +
            "# --------------------------------------------------------------- escalation\n" +
            "#\n" +
            "# What one rank buys. A rank is earned by killing one of your mercenaries.\n" +
            "\n" +
            "# Extra health per rank, as a fraction of the mob class's own modifier.\n" +
            "# Restrained on purpose: health is the stat that turns a threat into a slog.\n" +
            "health_per_rank=0.15\n" +
            "\n" +
            "# Extra evasion per rank.\n" +
            "dodge_per_rank=0.05\n" +
            "\n" +
            "# Ceiling on the equipment tech level a nemesis can climb to above its base\n" +
            "# class. This is the main reason a returning nemesis feels different - and\n" +
            "# that gear is also your salvage when you finally win.\n" +
            "max_tech_level_bonus=3\n" +
            "\n" +
            "# The rank at which a nemesis starts acting twice per round. The single\n" +
            "# biggest step up in the mod; 3 means it has already killed three of yours.\n" +
            "rank_for_extra_turn=3\n" +
            "\n" +
            "# ------------------------------------------------------------- diagnostics\n" +
            "\n" +
            "# Writes probe.txt next to this file listing the roster and every member\n" +
            "# this mod resolved. Costs one file write at startup. Off by default.\n" +
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
                PromoteChance = Float(values, "promote_chance", PromoteChance, 0f, 1f);
                MaxLiving = Int(values, "max_living", MaxLiving, 0, 16);
                ReturnChance = Float(values, "return_chance", ReturnChance, 0f, 1f);
                RosterCap = Int(values, "roster_cap", RosterCap, 1, 512);
                HealthPerRank = Float(values, "health_per_rank", HealthPerRank, 0f, 2f);
                DodgePerRank = Float(values, "dodge_per_rank", DodgePerRank, 0f, 1f);
                MaxTechLevelBonus = Int(values, "max_tech_level_bonus", MaxTechLevelBonus, 0, 6);
                RankForExtraTurn = Int(values, "rank_for_extra_turn", RankForExtraTurn, 1, 20);
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
                        " promote_chance=" + PromoteChance.ToString("0.##", CultureInfo.InvariantCulture) +
                        " max_living=" + MaxLiving +
                        " return_chance=" + ReturnChance.ToString("0.##", CultureInfo.InvariantCulture) +
                        " roster_cap=" + RosterCap +
                        " health_per_rank=" + HealthPerRank.ToString("0.##", CultureInfo.InvariantCulture) +
                        " dodge_per_rank=" + DodgePerRank.ToString("0.##", CultureInfo.InvariantCulture) +
                        " max_tech_level_bonus=" + MaxTechLevelBonus +
                        " rank_for_extra_turn=" + RankForExtraTurn +
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
