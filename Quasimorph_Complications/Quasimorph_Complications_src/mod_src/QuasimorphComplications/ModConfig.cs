using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace QuasimorphComplications
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
        internal static float Chance = 0.35f;

        internal static bool Banner = true;
        internal static float BannerSeconds = 9f;
        internal static float FlashSeconds = 5f;

        internal static int WeightReinforcements = 10;
        internal static int WeightFire = 8;
        internal static int WeightRivals = 8;
        internal static int WeightLoud = 10;

        internal static int ReinforcementWaves = 3;
        internal static int ReinforcementInterval = 12;
        internal static int ReinforcementSize = 2;
        internal static int ReinforcementDistance = 14;

        internal static int CacheSize = 4;
        internal static int CacheDistance = 8;

        internal static int FireSeedCells = 3;
        internal static int FireMaxCells = 14;
        internal static int FireSpreadInterval = 6;
        internal static int FireSafeDistance = 12;

        internal static int RivalSize = 3;
        internal static int RivalArrivalTurn = 8;
        internal static int RivalDistance = 16;

        internal static float LoudFloorScale = 2.0f;

        internal static bool Probe = false;

        internal static int WeightOf(string id)
        {
            switch (id)
            {
                case "reinforcements": return WeightReinforcements;
                case "fire": return WeightFire;
                case "rivals": return WeightRivals;
                case "loud": return WeightLoud;
                default: return 0;
            }
        }

        private const string Template =
            "# Quasimorph - Complications\n" +
            "#\n" +
            "# Raids stop being the same raid. Each one may roll a single named\n" +
            "# complication, announced when you arrive, that changes how the floor has\n" +
            "# to be played - not how much health anything has.\n" +
            "#\n" +
            "# Every complication is a trade rather than a tax. Reinforcements arrive,\n" +
            "# and so does the cache somebody was carrying. A rival crew is a threat and\n" +
            "# also the best gear on the floor. A loud hull hears you - and lets you\n" +
            "# hear them.\n" +
            "\n" +
            "# Master switch. false leaves the game completely untouched.\n" +
            "enabled=true\n" +
            "\n" +
            "# Restrict the whole mod to one difficulty preset id. Empty means every\n" +
            "# difficulty. Set it to HardcoreTacticalRuthless to run this only as a\n" +
            "# companion to the sibling Hardcore Tactical Ruthless mod.\n" +
            "only_on_difficulty=\n" +
            "\n" +
            "# Chance that a raid gets a complication at all. One per raid, never two -\n" +
            "# two at once is not twice as interesting, it is noise, and it makes it\n" +
            "# impossible to learn what any single one does to a floor.\n" +
            "chance=0.35\n" +
            "\n" +
            "# ------------------------------------------------------------ the notice\n" +
            "\n" +
            "# Show the complication on screen when the raid starts. A complication\n" +
            "# nobody is told about is just an unfair floor, so leaving this on is\n" +
            "# strongly recommended - the log always says either way.\n" +
            "banner=true\n" +
            "banner_seconds=9\n" +
            "flash_seconds=5\n" +
            "\n" +
            "# ---------------------------------------------------------------- weights\n" +
            "#\n" +
            "# Relative likelihood of each complication. 0 switches one off entirely.\n" +
            "weight_reinforcements=10\n" +
            "weight_fire=8\n" +
            "weight_rivals=8\n" +
            "weight_loud=10\n" +
            "\n" +
            "# --------------------------------------------------------- reinforcements\n" +
            "#\n" +
            "# Waves of the defending faction's own troops, modelled on whoever is\n" +
            "# already on the floor, arriving away from you and hunting.\n" +
            "reinforcement_waves=3\n" +
            "reinforcement_interval=12\n" +
            "reinforcement_size=2\n" +
            "reinforcement_distance=14\n" +
            "\n" +
            "# The compensation, dropped on turn one where you can reach it.\n" +
            "cache_size=4\n" +
            "cache_distance=8\n" +
            "\n" +
            "# ------------------------------------------------------------------- fire\n" +
            "#\n" +
            "# The most dangerous complication here, and the most capped. A fire that\n" +
            "# got away could make a floor genuinely unwinnable, which is a broken raid\n" +
            "# rather than a hard one - so it seeds a few cells, never near you, and\n" +
            "# stops spreading once fire_max_cells are alight.\n" +
            "fire_seed_cells=3\n" +
            "fire_max_cells=14\n" +
            "fire_spread_interval=6\n" +
            "fire_safe_distance=12\n" +
            "\n" +
            "# ------------------------------------------------------------ rival crew\n" +
            "#\n" +
            "# Another crew, hostile to the station and to you. They spawn on the\n" +
            "# alliance the game already uses for creatures that fight everybody, so the\n" +
            "# three-way fight is the game's own AI and not something this mod wrote.\n" +
            "rival_size=3\n" +
            "rival_arrival_turn=8\n" +
            "rival_distance=16\n" +
            "\n" +
            "# ------------------------------------------------------------ loud floor\n" +
            "#\n" +
            "# Multiplier on the game's three global noise radii, for this raid only.\n" +
            "# They are restored when the floor ends. Pairs with the sibling Silence\n" +
            "# mod, which shows you exactly what you just made and who heard it.\n" +
            "loud_floor_scale=2.0\n" +
            "\n" +
            "# ------------------------------------------------------------ diagnostics\n" +
            "\n" +
            "# Writes probe.txt recording the complication catalogue and what this mod\n" +
            "# could learn about the game's own unused dungeon event system.\n" +
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
                Chance = Float(values, "chance", Chance, 0f, 1f);
                Banner = Bool(values, "banner", Banner);
                BannerSeconds = Float(values, "banner_seconds", BannerSeconds, 1f, 60f);
                FlashSeconds = Float(values, "flash_seconds", FlashSeconds, 1f, 60f);
                WeightReinforcements = Int(values, "weight_reinforcements", WeightReinforcements, 0, 100);
                WeightFire = Int(values, "weight_fire", WeightFire, 0, 100);
                WeightRivals = Int(values, "weight_rivals", WeightRivals, 0, 100);
                WeightLoud = Int(values, "weight_loud", WeightLoud, 0, 100);
                ReinforcementWaves = Int(values, "reinforcement_waves", ReinforcementWaves, 1, 20);
                ReinforcementInterval = Int(values, "reinforcement_interval", ReinforcementInterval, 2, 100);
                ReinforcementSize = Int(values, "reinforcement_size", ReinforcementSize, 1, 10);
                ReinforcementDistance = Int(values, "reinforcement_distance", ReinforcementDistance, 4, 60);
                CacheSize = Int(values, "cache_size", CacheSize, 0, 20);
                CacheDistance = Int(values, "cache_distance", CacheDistance, 0, 60);
                FireSeedCells = Int(values, "fire_seed_cells", FireSeedCells, 1, 20);
                FireMaxCells = Int(values, "fire_max_cells", FireMaxCells, 1, 200);
                FireSpreadInterval = Int(values, "fire_spread_interval", FireSpreadInterval, 1, 100);
                FireSafeDistance = Int(values, "fire_safe_distance", FireSafeDistance, 4, 60);
                RivalSize = Int(values, "rival_size", RivalSize, 1, 10);
                RivalArrivalTurn = Int(values, "rival_arrival_turn", RivalArrivalTurn, 1, 100);
                RivalDistance = Int(values, "rival_distance", RivalDistance, 4, 60);
                LoudFloorScale = Float(values, "loud_floor_scale", LoudFloorScale, 1f, 6f);
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
                        " chance=" + Chance.ToString("0.##", CultureInfo.InvariantCulture) +
                        " banner=" + Banner +
                        " weights=" + WeightReinforcements + "/" + WeightFire + "/" +
                        WeightRivals + "/" + WeightLoud +
                        " loud_scale=" + LoudFloorScale.ToString("0.##", CultureInfo.InvariantCulture));
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
