using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Layer 1 - puts the preset into the game.
    ///
    /// <c>Data.DifficultyPresets</c> is a plain <c>Dictionary&lt;string, DifficultyPreset&gt;</c>
    /// and <c>DifficultyScreen.OnEnable</c> enumerates all of it, calling
    /// <c>AddPanel(key, value)</c> once per entry. So adding one dictionary entry adds
    /// one panel - no UI patching, no prefab work, nothing to keep in sync.
    /// </summary>
    internal static class DifficultyRegistration
    {
        internal static bool Registered { get; private set; }

        /// <summary>
        /// Set once we have failed to read the active difficulty, so the warning is
        /// logged a single time instead of on every run boundary.
        /// </summary>
        private static bool _resolveFailureLogged;

        internal static void Register()
        {
            var presets = Data.DifficultyPresets;
            if (presets == null)
            {
                ModLog.Error("Data.DifficultyPresets is null; the difficulty was not added");
                return;
            }

            if (presets.ContainsKey(ModInfo.PresetId))
            {
                // Two copies of the mod in the load order, or a hook that ran twice.
                // Either way, the first registration stands.
                ModLog.Warn("preset " + ModInfo.PresetId + " is already registered; leaving it alone");
                Registered = true;
                return;
            }

            var source = FindBasePreset(presets);
            if (source == null)
            {
                ModLog.Error("no usable base preset found among " + presets.Count +
                             "; the difficulty was not added");
                return;
            }

            var preset = PresetTuning.Derive(source, ModInfo.PresetId);
            presets[ModInfo.PresetId] = preset;
            Registered = true;

            ModLog.Info("registered difficulty '" + ModInfo.PresetId + "' derived from '" +
                        source.Id + "'");
            ModLog.Info("  enemy   los x" + PresetTuning.EnemyLos +
                        " ap x" + PresetTuning.EnemyActionPoint +
                        " dmg x" + PresetTuning.EnemyDamageMult +
                        " health x1 (held, by design)");
            ModLog.Info("  loot    items x" + PresetTuning.ItemPoints +
                        " salvage condition x" + PresetTuning.KilledMobsItemsCondition +
                        " monsters x" + PresetTuning.MonsterPoints);
        }

        /// <summary>
        /// Vanilla <c>Hard</c> is the intended baseline. If a future version renames or
        /// drops it, fall back to any preset that carries a content descriptor - the
        /// icon is the one field <c>AddPanel</c> dereferences without a null check, so
        /// a preset without one would break the whole difficulty screen.
        /// </summary>
        private static DifficultyPreset FindBasePreset(Dictionary<string, DifficultyPreset> presets)
        {
            if (presets.TryGetValue(ModInfo.BasePresetId, out var preferred) &&
                preferred != null && preferred.ContentDescriptor != null)
            {
                return preferred;
            }

            ModLog.Warn("preset '" + ModInfo.BasePresetId +
                        "' is missing or has no icon; falling back to the first usable preset");

            foreach (var entry in presets)
            {
                if (entry.Value != null && entry.Value.ContentDescriptor != null)
                {
                    return entry.Value;
                }
            }
            return null;
        }

        /// <summary>
        /// True when the run currently in progress is on our difficulty.
        ///
        /// Fails closed: if the active preset cannot be read for any reason, this
        /// returns false and the behaviour layers stay off. An unexpected vanilla game
        /// is a much better failure than an unexpected modded one.
        /// </summary>
        internal static bool IsActive(State state)
        {
            if (state == null)
            {
                return false;
            }

            try
            {
                var difficulty = state.Get<Difficulty>();
                var preset = difficulty?.Preset;
                if (preset == null)
                {
                    return false;
                }
                return string.Equals(preset.Id, ModInfo.PresetId, StringComparison.Ordinal);
            }
            catch (Exception error)
            {
                if (!_resolveFailureLogged)
                {
                    _resolveFailureLogged = true;
                    ModLog.Error("could not read the active difficulty from State; the " +
                                 "behaviour layers will stay off and the game stays vanilla",
                                 error);
                }
                return false;
            }
        }
    }
}
