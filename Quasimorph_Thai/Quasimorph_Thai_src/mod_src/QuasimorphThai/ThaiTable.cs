using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

namespace QuasimorphThai
{
    /// <summary>
    /// Supplies the Thai localization table to the game.
    ///
    /// The game reads its table through <c>MGSC.CustomResources.Load("localization")</c>,
    /// which gives registered mod hooks first refusal before falling back to
    /// <c>Resources.Load</c>. We take that call, load the game's *own* table, lay the
    /// Thai translations over its English column, and hand back the result. No game
    /// file is ever modified.
    ///
    /// Merging rather than replacing is what makes this survive a game update:
    ///   * a key the game added and we have not translated keeps the game's English,
    ///     instead of rendering as a raw key like "ui.some.new.thing";
    ///   * the other ten language columns always come from the installed game version;
    ///   * a key the game removed simply goes unused.
    /// The translations are addressed by key, so they carry across versions untouched.
    /// </summary>
    internal static class ThaiTable
    {
        internal const string ResourcePath = "localization";

        private const string PlainFileName = "thai_overrides.tsv";
        private const string GzipFileName = "thai_overrides.tsv.gz";

        /// <summary>Column the mod writes Thai into (the game's English slot).</summary>
        private const int ThaiColumn = 1;

        /// <summary>Shown in the language dropdown where "English" used to be.</summary>
        private const string LanguageName = "ไทย";

        private static TextAsset _cached;
        private static bool _attempted;

        internal static int RowCount { get; private set; }
        internal static int AppliedCount { get; private set; }
        internal static int UntranslatedCount { get; private set; }
        internal static int StaleCount { get; private set; }
        internal static int RequestCount { get; private set; }

        internal static bool WasRequested => RequestCount > 0;

        internal static TextAsset Provide(string modDirectory)
        {
            RequestCount++;
            if (_cached != null)
            {
                return _cached;
            }
            if (_attempted)
            {
                // Already failed once; do not retry on every resource load.
                return null;
            }
            _attempted = true;

            var overrides = ReadOverrides(modDirectory);
            if (overrides == null || overrides.Count == 0)
            {
                return null;
            }

            var original = LoadGameTable();
            if (original == null)
            {
                return null;
            }

            var merged = Merge(original, overrides);
            if (RowCount < 2)
            {
                ModLog.Error("Merged table has " + RowCount + " rows; refusing to install it.");
                return null;
            }

            _cached = new TextAsset(merged)
            {
                name = ResourcePath,
                // Keep it alive across scene loads; the game holds no reference itself.
                hideFlags = HideFlags.HideAndDontSave,
            };

            ModLog.Info("Thai table ready: " + RowCount + " rows, " + AppliedCount
                        + " translated, " + UntranslatedCount + " left in English.");
            if (StaleCount > 0)
            {
                ModLog.Info(StaleCount + " translation(s) match no row in this game version "
                            + "(the game probably removed or renamed those keys).");
            }
            if (UntranslatedCount > 0)
            {
                ModLog.Info("Untranslated rows fall back to the game's own English, so a game "
                            + "update never shows raw keys.");
            }
            return _cached;
        }

        /// <summary>
        /// Reads the game's built-in table. Deliberately calls Unity's Resources.Load
        /// and not CustomResources.Load - the latter would re-enter this very hook.
        /// </summary>
        private static string LoadGameTable()
        {
            try
            {
                var asset = Resources.Load(ResourcePath) as TextAsset;
                if (asset == null)
                {
                    ModLog.Error("The game's own '" + ResourcePath + "' asset could not be "
                                 + "loaded, so there is nothing to merge into. Leaving the "
                                 + "game in English.");
                    return null;
                }
                return asset.text;
            }
            catch (Exception error)
            {
                ModLog.Error("Failed to read the game's localization table", error);
                return null;
            }
        }

        private static string Merge(string original, Dictionary<string, string> overrides)
        {
            // Split on '\n' only, so each line keeps its trailing '\r'. That '\r' sits at
            // the end of the last (empty) column, exactly where the game already expects
            // it, and the file's CRLF endings survive the round trip untouched.
            var lines = original.Split('\n');
            var applied = new HashSet<string>();
            var untranslated = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.Length == 0)
                {
                    continue;
                }

                var columns = line.Split('\t');
                if (columns.Length <= ThaiColumn)
                {
                    continue;
                }

                if (i == 0)
                {
                    // Header: its column-1 cell is the name shown in the language dropdown.
                    columns[ThaiColumn] = LanguageName;
                    lines[i] = string.Join("\t", columns);
                    continue;
                }

                var key = columns[0];
                if (overrides.TryGetValue(key, out var thai))
                {
                    columns[ThaiColumn] = thai;
                    lines[i] = string.Join("\t", columns);
                    applied.Add(key);
                }
                else if (columns[ThaiColumn].Length > 0)
                {
                    untranslated++;
                }
            }

            RowCount = lines.Length;
            AppliedCount = applied.Count;
            UntranslatedCount = untranslated;
            StaleCount = overrides.Count - applied.Count;
            return string.Join("\n", lines);
        }

        private static Dictionary<string, string> ReadOverrides(string modDirectory)
        {
            var gzip = Path.Combine(modDirectory, GzipFileName);
            var plain = Path.Combine(modDirectory, PlainFileName);

            try
            {
                if (File.Exists(gzip))
                {
                    return Parse(ReadGzip(gzip));
                }
                if (File.Exists(plain))
                {
                    return Parse(File.ReadAllText(plain, new UTF8Encoding(false)));
                }
            }
            catch (Exception error)
            {
                ModLog.Error("Could not read the Thai translations", error);
                return null;
            }

            ModLog.Error("No Thai translations found. Expected " + PlainFileName + " or "
                         + GzipFileName + " in " + modDirectory);
            return null;
        }

        /// <summary>Parses the shipped "key TAB thai" table, one entry per line.</summary>
        private static Dictionary<string, string> Parse(string text)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }
                var tab = line.IndexOf('\t');
                if (tab <= 0)
                {
                    continue;
                }
                result[line.Substring(0, tab)] = line.Substring(tab + 1);
            }
            return result;
        }

        private static string ReadGzip(string path)
        {
            using (var file = File.OpenRead(path))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip, new UTF8Encoding(false)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
