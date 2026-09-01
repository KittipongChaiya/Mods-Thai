using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Layer 2 - enemy behaviour, and the reason this mod exists.
    ///
    /// The difficulty sliders can make an enemy tougher. They cannot make it
    /// *better*. These records can: how long it keeps hunting you after losing
    /// sight, whether it throws a grenade into your cover instead of walking into
    /// your kill zone, whether the door you closed behind you means anything.
    ///
    /// Two rules keep this from turning into nonsense:
    ///
    /// 1. <b>Never enable a behaviour the designers switched off.</b> A multiplier is
    ///    only ever applied to a value that is already above zero, so a mindless
    ///    creature does not suddenly start cooking grenades.
    /// 2. <b>Only thinking enemies get the tool-use flags.</b> A preset already
    ///    showing item use, grenades or firemode choice is a humanoid that reasons;
    ///    everything else keeps its vanilla relationship with doors.
    ///
    /// Config records are shared global state, so the vanilla values are snapshotted
    /// before anything is written and restored the moment a run starts on a different
    /// difficulty. That is what keeps a vanilla save vanilla in the same session.
    /// </summary>
    internal static class TacticalAi
    {
        // ---- persistence: enemies stop losing interest -----------------------
        private const float HuntMemory = 1.75f;
        private const float InvestigateMemory = 1.75f;

        // ---- competence: they use what they are carrying ---------------------
        private const float GrenadeChance = 1.60f;
        private const float BestFiremodeChance = 1.50f;
        private const float UltimateChance = 1.25f;

        // ---- your traps stop being free --------------------------------------
        private const float AvoidMineChance = 1.50f;
        private const float AvoidDangerTerrain = 1.50f;

        // ---- they hold the line a little longer ------------------------------
        //
        // Modest on purpose. Panic and surrender are good mechanics: they end fights
        // early and reward pressure. Suppressing them hard would just make every
        // engagement longer, which is the sponge failure wearing a different hat.
        private const float PanicChance = 0.75f;
        private const float SurrenderChance = 0.75f;

        private static readonly List<Snapshot> Vanilla = new List<Snapshot>();
        private static bool _applied;
        private static bool _snapshotTaken;

        /// <summary>
        /// Everything this layer is capable of writing, captured before it writes
        /// anything. Restoring is then a straight copy back rather than an attempt
        /// to invert the arithmetic.
        /// </summary>
        private sealed class Snapshot
        {
            internal AiPresetRecord Record;
            internal int HuntMemory;
            internal int InvestigateMemory;
            internal float GrenadeChance;
            internal float BestFiremodeChance;
            internal float UltimateChance;
            internal float AvoidMineChance;
            internal Dictionary<HazardType, float> AvoidDangerTerrainChances;
            internal bool CanOpenDoor;
            internal bool CanMeleeAttackDoor;
            internal bool CanUseItems;
            internal float PanicByTeammateDeath;
            internal float PanicByLeaderDeath;
            internal float PanicByDamage;
            internal float PanicByEccolapse;
            internal float SurrenderByDamage;
        }

        /// <summary>Captures vanilla values. Safe to call more than once; only the first counts.</summary>
        internal static void CaptureVanilla()
        {
            if (_snapshotTaken)
            {
                return;
            }

            var presets = Data.AiPresets;
            if (presets == null)
            {
                ModLog.Error("Data.AiPresets is null; the tactical layer is unavailable");
                return;
            }

            foreach (var record in presets.Records)
            {
                if (record == null)
                {
                    continue;
                }
                Vanilla.Add(new Snapshot
                {
                    Record = record,
                    HuntMemory = record.HuntMemory,
                    InvestigateMemory = record.InvestigateMemory,
                    GrenadeChance = record.GrenadeChance,
                    BestFiremodeChance = record.BestFiremodeChance,
                    UltimateChance = record.UltimateChance,
                    AvoidMineChance = record.AvoidMineChance,
                    AvoidDangerTerrainChances = CopyOf(record.AvoidDangerTerrainChances),
                    CanOpenDoor = record.CanOpenDoor,
                    CanMeleeAttackDoor = record.CanMeleeAttackDoor,
                    CanUseItems = record.CanUseItems,
                    PanicByTeammateDeath = record.PanicChanceByTeammateDeath,
                    PanicByLeaderDeath = record.PanicChanceByLeaderDeath,
                    PanicByDamage = record.PanicChanceByDamage,
                    PanicByEccolapse = record.PanicChanceByEccolapse,
                    SurrenderByDamage = record.SurrenderChanceByDamage,
                });
            }

            _snapshotTaken = true;
            ModLog.Info("tactical layer: snapshotted " + Vanilla.Count + " ai presets");
        }

        internal static void Apply()
        {
            if (_applied || !_snapshotTaken)
            {
                return;
            }
            if (!ModConfig.TacticalAi || ModConfig.Intensity <= 0f)
            {
                return;
            }

            var thinkers = 0;
            foreach (var vanilla in Vanilla)
            {
                var record = vanilla.Record;

                record.HuntMemory = Tuning.ScaleInt(vanilla.HuntMemory, HuntMemory);
                record.InvestigateMemory = Tuning.ScaleInt(vanilla.InvestigateMemory, InvestigateMemory);

                record.GrenadeChance = Tuning.ScaleChance(vanilla.GrenadeChance, GrenadeChance);
                record.BestFiremodeChance = Tuning.ScaleChance(vanilla.BestFiremodeChance, BestFiremodeChance);
                record.UltimateChance = Tuning.ScaleChance(vanilla.UltimateChance, UltimateChance);

                record.AvoidMineChance = Tuning.ScaleChance(vanilla.AvoidMineChance, AvoidMineChance);
                ScaleHazards(record, vanilla);

                record.PanicChanceByTeammateDeath = Tuning.ScaleChance(vanilla.PanicByTeammateDeath, PanicChance);
                record.PanicChanceByLeaderDeath = Tuning.ScaleChance(vanilla.PanicByLeaderDeath, PanicChance);
                record.PanicChanceByDamage = Tuning.ScaleChance(vanilla.PanicByDamage, PanicChance);
                record.PanicChanceByEccolapse = Tuning.ScaleChance(vanilla.PanicByEccolapse, PanicChance);
                record.SurrenderChanceByDamage = Tuning.ScaleChance(vanilla.SurrenderByDamage, SurrenderChance);

                // Tool use is qualitative, not a magnitude, so it is not scaled by
                // intensity - it is part of what the mode *is*. It is still gated on
                // the creature already being a thinking one.
                if (IsThinker(vanilla))
                {
                    record.CanOpenDoor = true;
                    record.CanMeleeAttackDoor = true;
                    record.CanUseItems = true;
                    thinkers++;
                }
            }

            _applied = true;
            ModLog.Info("tactical layer ON: " + Vanilla.Count + " ai presets tuned, " +
                        thinkers + " of them given doors and item use " +
                        "(intensity " + ModConfig.Intensity + ")");
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
                record.HuntMemory = vanilla.HuntMemory;
                record.InvestigateMemory = vanilla.InvestigateMemory;
                record.GrenadeChance = vanilla.GrenadeChance;
                record.BestFiremodeChance = vanilla.BestFiremodeChance;
                record.UltimateChance = vanilla.UltimateChance;
                record.AvoidMineChance = vanilla.AvoidMineChance;
                RestoreHazards(record, vanilla);
                record.CanOpenDoor = vanilla.CanOpenDoor;
                record.CanMeleeAttackDoor = vanilla.CanMeleeAttackDoor;
                record.CanUseItems = vanilla.CanUseItems;
                record.PanicChanceByTeammateDeath = vanilla.PanicByTeammateDeath;
                record.PanicChanceByLeaderDeath = vanilla.PanicByLeaderDeath;
                record.PanicChanceByDamage = vanilla.PanicByDamage;
                record.PanicChanceByEccolapse = vanilla.PanicByEccolapse;
                record.SurrenderChanceByDamage = vanilla.SurrenderByDamage;
            }

            _applied = false;
            ModLog.Info("tactical layer OFF: " + Vanilla.Count + " ai presets restored to vanilla");
        }

        /// <summary>
        /// A preset that already uses items, throws grenades or picks a firemode is a
        /// creature that reasons about its equipment. Those are the ones that should
        /// also reason about a door. Everything else keeps vanilla behaviour, which is
        /// how a mindless horror avoids politely turning a handle.
        /// </summary>
        private static bool IsThinker(Snapshot vanilla)
        {
            return vanilla.CanUseItems ||
                   vanilla.GrenadeChance > 0f ||
                   vanilla.BestFiremodeChance > 0f;
        }

        private static void ScaleHazards(AiPresetRecord record, Snapshot vanilla)
        {
            var source = vanilla.AvoidDangerTerrainChances;
            var live = record.AvoidDangerTerrainChances;
            if (source == null || live == null)
            {
                return;
            }

            // Collect first: writing to a dictionary while enumerating it throws.
            var keys = new List<HazardType>(source.Keys);
            foreach (var key in keys)
            {
                live[key] = Tuning.ScaleChance(source[key], AvoidDangerTerrain);
            }
        }

        private static void RestoreHazards(AiPresetRecord record, Snapshot vanilla)
        {
            var source = vanilla.AvoidDangerTerrainChances;
            var live = record.AvoidDangerTerrainChances;
            if (source == null || live == null)
            {
                return;
            }
            var keys = new List<HazardType>(source.Keys);
            foreach (var key in keys)
            {
                live[key] = source[key];
            }
        }

        private static Dictionary<HazardType, float> CopyOf(Dictionary<HazardType, float> source)
        {
            return source == null ? null : new Dictionary<HazardType, float>(source);
        }
    }
}
