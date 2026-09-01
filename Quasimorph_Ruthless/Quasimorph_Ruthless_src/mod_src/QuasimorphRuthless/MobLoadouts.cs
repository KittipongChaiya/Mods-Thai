using System.Collections.Generic;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Layer 3 - what enemies carry, and what is left of it afterwards.
    ///
    /// This layer is deliberately a trade rather than a tax. Enemies come a tech
    /// level better equipped, which makes the fight harder - and that same gear is
    /// your salvage, which makes the fight worth taking. Condition and spare ammo
    /// both drop, so what you recover is a compromise you have to plan around
    /// instead of a resupply you can count on.
    ///
    /// <b>Player mercenaries are structurally out of reach here.</b> They are built
    /// from <c>Data.MercenaryClasses</c>, a different collection this layer never
    /// touches. That is the guarantee behind the not-a-cheat rule: there is no code
    /// path from this file to the player's squad.
    ///
    /// Non-player NPCs who fight alongside you are drawn from the same mob table as
    /// your enemies and are tuned with them. The world holds one standard.
    /// </summary>
    internal static class MobLoadouts
    {
        /// <summary>
        /// One tech level better. Additive rather than multiplied, because a tech
        /// level is a rung on a ladder and not a magnitude.
        /// </summary>
        private const int EquipmentTechLevelBonus = 1;

        /// <summary>What you strip off a corpse is worn.</summary>
        private const float ItemCondition = 0.70f;

        /// <summary>And it comes with less spare ammunition.</summary>
        private const float AdditAmmo = 0.70f;

        private static readonly List<Snapshot> Vanilla = new List<Snapshot>();
        private static bool _applied;
        private static bool _snapshotTaken;

        private sealed class Snapshot
        {
            internal MobClassRecord Record;
            internal int EquipmentTechLevelBonus;
            internal IntRange ItemConditionPercent;
            internal IntRange AdditAmmo;
        }

        internal static void CaptureVanilla()
        {
            if (_snapshotTaken)
            {
                return;
            }

            var classes = Data.MobClasses;
            if (classes == null)
            {
                ModLog.Error("Data.MobClasses is null; the loadout layer is unavailable");
                return;
            }

            foreach (var record in classes.Records)
            {
                if (record == null)
                {
                    continue;
                }
                Vanilla.Add(new Snapshot
                {
                    Record = record,
                    EquipmentTechLevelBonus = record.EquipmentTechLevelBonus,
                    ItemConditionPercent = record.ItemConditionPercent,
                    AdditAmmo = record.AdditAmmo,
                });
            }

            _snapshotTaken = true;
            ModLog.Info("loadout layer: snapshotted " + Vanilla.Count + " mob classes");
        }

        internal static void Apply()
        {
            if (_applied || !_snapshotTaken)
            {
                return;
            }
            if (!ModConfig.MobLoadouts || ModConfig.Intensity <= 0f)
            {
                return;
            }

            // An additive rung on a ladder, so intensity is applied directly rather
            // than through Tuning.Effective - which, being a multiplier fold, would
            // have left this at a flat +1 no matter how far intensity was turned down.
            var techBonus = (int)System.Math.Round(EquipmentTechLevelBonus * ModConfig.Intensity,
                                                   System.MidpointRounding.AwayFromZero);
            foreach (var vanilla in Vanilla)
            {
                var record = vanilla.Record;
                record.EquipmentTechLevelBonus = vanilla.EquipmentTechLevelBonus + techBonus;
                record.ItemConditionPercent = ScaleRange(vanilla.ItemConditionPercent, ItemCondition);
                record.AdditAmmo = ScaleRange(vanilla.AdditAmmo, AdditAmmo);
            }

            _applied = true;
            ModLog.Info("loadout layer ON: " + Vanilla.Count + " mob classes tuned, tech level +" +
                        techBonus + ", condition x" + ItemCondition + ", spare ammo x" + AdditAmmo);
        }

        internal static void Restore()
        {
            if (!_applied)
            {
                return;
            }

            foreach (var vanilla in Vanilla)
            {
                var record = vanilla.Record;
                record.EquipmentTechLevelBonus = vanilla.EquipmentTechLevelBonus;
                record.ItemConditionPercent = vanilla.ItemConditionPercent;
                record.AdditAmmo = vanilla.AdditAmmo;
            }

            _applied = false;
            ModLog.Info("loadout layer OFF: " + Vanilla.Count + " mob classes restored to vanilla");
        }

        /// <summary>
        /// Scales both ends of a range. <c>IntRange</c> is a struct, so this returns a
        /// new one rather than mutating a copy that would be silently discarded.
        /// </summary>
        private static IntRange ScaleRange(IntRange vanilla, float factor)
        {
            return new IntRange
            {
                Min = Tuning.ScaleInt(vanilla.Min, factor),
                Max = Tuning.ScaleInt(vanilla.Max, factor),
            };
        }
    }
}
