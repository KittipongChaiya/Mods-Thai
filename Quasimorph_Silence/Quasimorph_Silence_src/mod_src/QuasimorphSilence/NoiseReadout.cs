using System;
using System.Reflection;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;

namespace QuasimorphSilence
{
    /// <summary>
    /// Puts the noise on screen, beside the movement mode panel where the decision is
    /// actually made.
    ///
    /// <b>No patch.</b> The panel is found in the scene and a copy of its own action
    /// point label is parented next to it, so the readout inherits the game's font,
    /// colour and layout for free - the same trick the sibling Signals mod uses to add
    /// a control without fighting the UI it sits in. If the panel or the label cannot be
    /// found, the mod loses its readout and keeps everything else.
    /// </summary>
    internal static class NoiseReadout
    {
        private const string LabelName = "QuasimorphSilenceNoiseLabel";

        private static TextMeshProUGUI _label;
        private static MoveStatePanel _panel;
        private static FieldInfo _sourceField;
        private static bool _warned;
        private static string _lastText;

        internal static void Reset()
        {
            _label = null;
            _panel = null;
            _lastText = null;
        }

        internal static void Refresh(State state)
        {
            if (!ModConfig.Readout)
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

                var text = Compose(state);
                if (string.Equals(text, _lastText, StringComparison.Ordinal))
                {
                    return;   // TMP rebuilds its mesh on assignment; only write on change
                }

                _lastText = text;
                label.text = text;
            }
            catch (Exception error)
            {
                if (!_warned)
                {
                    _warned = true;
                    ModLog.Error("the noise readout failed and has been left blank; the " +
                                 "rest of the mod is unaffected", error);
                }
            }
        }

        /// <summary>
        /// What the player reads. Two facts, both of which the game has always known and
        /// never said: how far the last noise you made carried, and how many enemies
        /// were awake and close enough to react to it.
        /// </summary>
        private static string Compose(State state)
        {
            var movement = state?.Get<Creatures>()?.Player?.CreatureData?.MovementState
                           ?? CreatureMovementState.Normal;

            var mode = movement == CreatureMovementState.Slow ? "quiet"
                     : movement == CreatureMovementState.Run ? "loud"
                     : "normal";

            // Before the first step of a raid there is nothing true to report, so the
            // readout says what the mode means rather than inventing a number.
            if (NoiseWatch.LastPlayerTurn < 0)
            {
                return "moving " + mode;
            }

            var text = NoiseWatch.Describe(NoiseWatch.LastType) + " " +
                       NoiseWatch.LastRadius + "m";

            if (NoiseWatch.LastVanillaRadius != NoiseWatch.LastRadius)
            {
                text += " (was " + NoiseWatch.LastVanillaRadius + ")";
            }

            text += NoiseWatch.LastHeardBy > 0
                ? "  ·  " + NoiseWatch.LastHeardBy + " heard you"
                : "  ·  unheard";

            return text;
        }

        private static TextMeshProUGUI Ensure()
        {
            if (_label != null)
            {
                return _label;
            }

            // The panel is pooled and rebuilt between raids, so it is re-found rather
            // than cached across them.
            if (_panel == null)
            {
                _panel = UnityEngine.Object.FindObjectOfType<MoveStatePanel>();
                if (_panel == null)
                {
                    return null;
                }
            }

            _sourceField = _sourceField ?? AccessTools.Field(typeof(MoveStatePanel),
                                                             "_actionPointsLeft");
            if (_sourceField == null)
            {
                if (!_warned)
                {
                    _warned = true;
                    ModLog.Warn("MoveStatePanel._actionPointsLeft was not found on this " +
                                "game build; no readout. Everything else still works.");
                }
                return null;
            }

            if (!(_sourceField.GetValue(_panel) is TextMeshProUGUI source))
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

            // Sit under the borrowed label rather than on top of it.
            var rect = _label.rectTransform;
            rect.anchoredPosition = source.rectTransform.anchoredPosition +
                                    new Vector2(0f, -18f);
            rect.sizeDelta = new Vector2(Math.Max(220f, rect.sizeDelta.x), rect.sizeDelta.y);

            _label.text = string.Empty;
            _label.enableWordWrapping = false;
            _label.alignment = TextAlignmentOptions.Left;

            ModLog.Info("noise readout attached to the movement panel");
            return _label;
        }
    }
}
