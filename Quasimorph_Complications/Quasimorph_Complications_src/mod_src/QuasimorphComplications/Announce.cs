using System;
using System.Reflection;
using MGSC;
using TMPro;
using UnityEngine;

namespace QuasimorphComplications
{
    /// <summary>
    /// Telling the player what they have walked into.
    ///
    /// <b>A complication nobody is told about is just an unfair floor.</b> The whole
    /// design rests on the player knowing at the start what kind of raid this is, so
    /// they can decide to be quick, or careful, or to leave. That makes this class load
    /// bearing rather than decorative.
    ///
    /// <b>No patch.</b> The movement panel is found in the scene and a copy of its own
    /// label is parented next to it, so the banner inherits the game's font and layout -
    /// the same technique the sibling Signals and Silence mods use. If the panel cannot
    /// be found the mod loses its banner and keeps its complications, and the log still
    /// says what is happening.
    /// </summary>
    internal static class Announce
    {
        private const string LabelName = "QuasimorphComplicationsBanner";

        private static TextMeshProUGUI _label;
        private static FieldInfo _sourceField;
        private static bool _warned;
        private static float _clearAt;

        internal static void Reset()
        {
            _label = null;
            _clearAt = 0f;
        }

        /// <summary>The complication's own line, shown for longer because it matters more.</summary>
        internal static void Banner(string text) => Show(text, ModConfig.BannerSeconds);

        /// <summary>A shorter note when something actually happens mid-raid.</summary>
        internal static void Flash(string text) => Show(text, ModConfig.FlashSeconds);

        private static void Show(string text, float seconds)
        {
            if (!ModConfig.Banner)
            {
                return;
            }

            try
            {
                var label = Ensure();
                if (label == null)
                {
                    return;
                }

                label.text = text;
                _clearAt = Time.unscaledTime + seconds;
            }
            catch (Exception error)
            {
                if (!_warned)
                {
                    _warned = true;
                    ModLog.Error("the on-screen banner failed; complications still run and " +
                                 "the log still reports them", error);
                }
            }
        }

        /// <summary>Called every frame to time the banner out. Cheap when there is nothing to do.</summary>
        internal static void Tick()
        {
            if (_label == null || _clearAt <= 0f || Time.unscaledTime < _clearAt)
            {
                return;
            }

            _clearAt = 0f;
            try
            {
                _label.text = string.Empty;
            }
            catch (Exception)
            {
                _label = null;
            }
        }

        private static TextMeshProUGUI Ensure()
        {
            if (_label != null)
            {
                return _label;
            }

            // The panel is pooled and rebuilt between raids, so it is re-found rather
            // than cached across them.
            var panel = UnityEngine.Object.FindObjectOfType<MoveStatePanel>();
            if (panel == null)
            {
                return null;
            }

            _sourceField = _sourceField ?? typeof(MoveStatePanel).GetField(
                "_actionPointsLeft", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_sourceField == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    ModLog.Warn("MoveStatePanel._actionPointsLeft was not found on this " +
                                "game build; no banner. Complications still run and the " +
                                "log still reports them.");
                }
                return null;
            }

            if (!(_sourceField.GetValue(panel) is TextMeshProUGUI source))
            {
                return null;
            }

            var parent = source.transform.parent;
            if (parent == null)
            {
                return null;
            }

            var existing = parent.Find(LabelName);
            if (existing != null)
            {
                _label = existing.GetComponent<TextMeshProUGUI>();
                return _label;
            }

            var clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            clone.name = LabelName;

            _label = clone.GetComponent<TextMeshProUGUI>();
            if (_label == null)
            {
                UnityEngine.Object.Destroy(clone);
                return null;
            }

            // Sit clear of the panel's own numbers, and of the sibling Silence mod's
            // noise readout if that is installed too.
            var rect = _label.rectTransform;
            rect.anchoredPosition = source.rectTransform.anchoredPosition +
                                    new Vector2(0f, -36f);
            rect.sizeDelta = new Vector2(Math.Max(420f, rect.sizeDelta.x), rect.sizeDelta.y);

            _label.text = string.Empty;
            _label.enableWordWrapping = false;
            _label.alignment = TextAlignmentOptions.Left;

            ModLog.Info("complication banner attached to the movement panel");
            return _label;
        }
    }
}
