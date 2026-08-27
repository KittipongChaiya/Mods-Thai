using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using MGSC;
using TMPro;
using UnityEngine;

namespace QuasimorphThai
{
    /// <summary>
    /// Points the font preset that serves <see cref="Localization.Lang.EnglishUS"/> at a
    /// Thai-capable TMP font.
    ///
    /// The game re-reads <c>FontPreset.FontAsset</c> on every
    /// <c>Localization.ActualizeFontAndSize</c> call, so swapping the preset's font once
    /// propagates to every label without touching individual text components.
    /// </summary>
    internal static class ThaiFont
    {
        private const string BundleFileName = "quasimorph_tahoma_tmp.bundle";
        private const string FontAssetName = "ProjectModThai_Tahoma_TMP";

        /// <summary>A character we can probe for to confirm Thai coverage.</summary>
        private const char ThaiProbe = 'ก';

        private static AssetBundle _bundle;
        private static TMP_FontAsset _thaiFont;

        internal static bool IsApplied { get; private set; }

        internal static void Apply(string modDirectory)
        {
            if (IsApplied)
            {
                return;
            }

            var font = LoadFont(modDirectory);
            if (font == null)
            {
                return;
            }

            var preset = FindPresetFor(Localization.Lang.EnglishUS);
            if (preset == null)
            {
                ModLog.Warn("No FontPreset serves EnglishUS yet; will retry on the next hook.");
                return;
            }

            var field = FindFontField(preset);
            if (field == null)
            {
                ModLog.Error("FontPreset exposes no TMP_FontAsset field; cannot install the Thai font.");
                return;
            }

            var previous = field.GetValue(preset) as TMP_FontAsset;
            field.SetValue(preset, font);
            RegisterFallback(previous, font);
            Prewarm(font);

            IsApplied = true;
            ModLog.Info("Installed Thai font into FontPreset '" + preset.name + "' (field '"
                        + field.Name + "', was '" + (previous == null ? "<null>" : previous.name) + "').");
        }

        private static TMP_FontAsset LoadFont(string modDirectory)
        {
            if (_thaiFont != null)
            {
                return _thaiFont;
            }

            var path = Path.Combine(modDirectory, BundleFileName);
            if (!File.Exists(path))
            {
                ModLog.Error("Font bundle not found: " + path);
                return null;
            }

            try
            {
                _bundle = _bundle ?? AssetBundle.LoadFromFile(path);
            }
            catch (Exception error)
            {
                ModLog.Error("Font bundle failed to load", error);
                return null;
            }

            if (_bundle == null)
            {
                ModLog.Error("Font bundle returned null. It was built with an older Unity and may "
                             + "be incompatible with this game build.");
                return null;
            }

            _thaiFont = _bundle.LoadAsset<TMP_FontAsset>(FontAssetName)
                        ?? FirstFontIn(_bundle);

            if (_thaiFont == null)
            {
                ModLog.Error("Font bundle loaded but contains no TMP_FontAsset.");
                return null;
            }

            _thaiFont.hideFlags = HideFlags.HideAndDontSave;
            _thaiFont.isMultiAtlasTexturesEnabled = true;
            ModLog.Info("Loaded Thai font asset '" + _thaiFont.name + "'.");
            return _thaiFont;
        }

        private static TMP_FontAsset FirstFontIn(AssetBundle bundle)
        {
            var all = bundle.LoadAllAssets<TMP_FontAsset>();
            return all != null && all.Length > 0 ? all[0] : null;
        }

        private static FontPreset FindPresetFor(Localization.Lang lang)
        {
            var keeper = SingletonMonoBehaviour<LocalizationFontKeeper>.Instance;
            if (keeper == null || keeper.FontPresets == null)
            {
                return null;
            }

            foreach (var preset in keeper.FontPresets)
            {
                if (preset != null && preset.AvaialableLangs != null
                    && preset.AvaialableLangs.IndexOf(lang) != -1)
                {
                    return preset;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the backing field for <c>FontPreset.FontAsset</c> by type rather than by
        /// name, so a future rename in the game does not silently disable the Thai font.
        /// </summary>
        private static FieldInfo FindFontField(FontPreset preset)
        {
            var type = preset.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

            var byName = type.GetField("_font", flags);
            if (byName != null && byName.FieldType == typeof(TMP_FontAsset))
            {
                return byName;
            }

            foreach (var candidate in type.GetFields(flags))
            {
                if (candidate.FieldType == typeof(TMP_FontAsset))
                {
                    ModLog.Warn("FontPreset._font is gone; falling back to field '" + candidate.Name + "'.");
                    return candidate;
                }
            }
            return null;
        }

        /// <summary>
        /// Keeps the original font as a fallback so any glyph Tahoma lacks (and the other
        /// languages' presets, which we do not touch) still render.
        /// </summary>
        private static void RegisterFallback(TMP_FontAsset previous, TMP_FontAsset thai)
        {
            try
            {
                if (previous != null && thai.fallbackFontAssetTable != null
                    && !thai.fallbackFontAssetTable.Contains(previous))
                {
                    thai.fallbackFontAssetTable.Add(previous);
                }

                var global = TMP_Settings.fallbackFontAssets;
                if (global != null && !global.Contains(thai))
                {
                    global.Insert(0, thai);
                }
            }
            catch (Exception error)
            {
                ModLog.Warn("Fallback registration skipped: " + error.Message);
            }
        }

        /// <summary>
        /// Forces the Thai block into the font atlas up front. Without this the first
        /// screen that shows Thai can flash missing glyphs while the atlas fills in.
        /// </summary>
        private static void Prewarm(TMP_FontAsset font)
        {
            try
            {
                var thai = new System.Text.StringBuilder(96);
                for (var c = 0x0E01; c <= 0x0E5B; c++)
                {
                    // U+0E3B..U+0E3E are unassigned in Unicode; asking for them would
                    // always be reported as "missing" and hide a real gap.
                    if (c >= 0x0E3B && c <= 0x0E3E)
                    {
                        continue;
                    }
                    thai.Append((char)c);
                }

                font.TryAddCharacters(thai.ToString(), out string missing);
                if (!string.IsNullOrEmpty(missing))
                {
                    ModLog.Warn("Font is missing " + missing.Length + " Thai code points.");
                }
                if (!font.HasCharacter(ThaiProbe))
                {
                    ModLog.Warn("Font does not report the Thai probe glyph after prewarm.");
                }
            }
            catch (Exception error)
            {
                ModLog.Warn("Thai glyph prewarm skipped: " + error.Message);
            }
        }

        internal static IEnumerable<string> Describe()
        {
            yield return "fontLoaded=" + (_thaiFont != null);
            yield return "applied=" + IsApplied;
        }
    }
}
