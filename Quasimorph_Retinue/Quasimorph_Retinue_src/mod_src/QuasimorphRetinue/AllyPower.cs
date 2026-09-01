using System;
using System.Collections.Generic;
using MGSC;
using UnityEngine;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Layer 2 - what makes an ally worth having.
    ///
    /// <b>Every value here is computed and assigned, never multiplied in place.</b>
    /// That single rule is what makes this layer correct, and it is worth the space
    /// to explain because the obvious implementation is wrong.
    ///
    /// The obvious implementation reads a stat, multiplies it and writes it back,
    /// then keeps a set of creature ids so it never does that twice. It works until
    /// the player saves and reloads: the buffed stats are saved with the creature,
    /// the set is not, and the ally is buffed again on top of itself. A few floors of
    /// that and an ally has a five-figure health pool.
    ///
    /// So each stat is instead derived from a base the mod never writes to:
    ///
    /// <list type="bullet">
    /// <item>damage and resistance from the difficulty preset, which is exactly what
    ///       the game itself wrote there at spawn</item>
    /// <item>sight from the mob class record plus the difficulty's own sight bonus</item>
    /// <item>health from <c>BaseHealth</c>, which already has the difficulty's health
    ///       multiplier folded in and which nothing here touches</item>
    /// <item>turns from <c>BaseActionPoints</c>, likewise</item>
    /// </list>
    ///
    /// Running this a thousand times on the same ally produces the same ally. Reloads,
    /// floor transfers and duplicate hook calls are all therefore free, and the mod
    /// needs no persistent bookkeeping of its own.
    /// </summary>
    internal static class AllyPower
    {
        // ---- staying alive ----------------------------------------------------
        //
        // An ally that dies in the first exchange is a cutscene, not a squad. This is
        // the largest single number in the mod and the one most likely to need cutting
        // if fights start feeling like they are being won by attrition.
        private const float Health = 1.60f;

        /// <summary>Wrong-damage-type hits stop deleting them.</summary>
        private const float Resist = 1.25f;

        /// <summary>Long-range potshots stop landing every time.</summary>
        private const float Dodge = 1.20f;

        // ---- winning the fight ------------------------------------------------
        //
        // They have to actually kill things. An ally that only chips is the sponge
        // failure with the sides swapped: the fight still ends, it just takes longer.
        private const float Damage = 1.50f;

        /// <summary>They spot first, which is the whole point of a screen.</summary>
        private const int LosBonus = 1;

        /// <summary>
        /// The single biggest competence knob, and the one that costs real seconds per
        /// turn. Held to +1 deliberately - the game's own ally-support perks
        /// (<c>IAllyAddedAP</c>) work in the same units and at the same scale.
        /// </summary>
        private const int ActionPointBonus = 1;

        // ---- held at vanilla, on purpose ---------------------------------------
        //
        // The omissions are as much a part of the design as the multipliers, so they
        // are listed rather than left to be inferred from silence:
        //
        //   BaseRangeAccuracy / BaseMeleeAccuracy - derived from the body type, which
        //     cannot be recomputed from saved data alone, so buffing them could not be
        //     made idempotent. Damage and action points cover the same ground without
        //     the risk of compounding across a reload.
        //
        //   HasSecondChance - owned by PerkSystem, which recomputes it. It would also
        //     revive an ally mid-fight, which reads as a bug rather than as strength.
        //
        //   Health.SetInvulnerability - an immortal squad is not a squad.

        /// <summary>
        /// Purely to keep the log readable. Correctness does not depend on it: the
        /// arithmetic above is idempotent, so a missed or repeated entry changes
        /// nothing but how many lines get written.
        /// </summary>
        private static readonly HashSet<int> Logged = new HashSet<int>();

        /// <summary>Same purpose, for the allies deliberately left alone.</summary>
        private static readonly HashSet<int> Skipped = new HashSet<int>();

        internal static void Sweep(State state)
        {
            if (!ModConfig.AllyPower || ModConfig.Power <= 0f)
            {
                return;
            }

            var creatures = state?.Get<Creatures>();
            if (creatures == null)
            {
                return;
            }

            var difficulty = state.Get<Difficulty>();
            if (difficulty?.Preset == null)
            {
                // Without the preset there is no honest baseline to compute from, and
                // guessing one would be the compounding bug this class exists to avoid.
                ModLog.Warn("no difficulty preset available; ally strength left vanilla this pass");
                return;
            }

            var turnController = state.Get<TurnController>();
            foreach (var ally in AllyIdentity.Living(creatures))
            {
                // A mod may have fielded one of the player's own roster mercenaries as
                // an ally. Those are persistent characters, and strength written onto
                // one would follow it back to the ship and stay in the save. They count
                // as squad members everywhere else in this mod; here they are skipped.
                if (AllyIdentity.IsPlayerMercenary(state, ally))
                {
                    if (Skipped.Add(ally.CreatureData.UniqueId))
                    {
                        ModLog.Info("ally #" + ally.CreatureData.UniqueId + " is one of your own " +
                                    "mercenaries; left exactly as the game made it");
                    }
                    continue;
                }

                Apply(ally, difficulty.Preset, turnController);
            }
        }

        /// <summary>
        /// Brings one ally up to strength. Safe to call at any time, any number of
        /// times, on an ally acquired by any means.
        /// </summary>
        internal static void Apply(Monster ally, DifficultyPreset preset, TurnController turnController)
        {
            var data = ally?.CreatureData;
            if (data == null || preset == null)
            {
                return;
            }

            try
            {
                var before = Describe(ally);

                // Damage dealt. The game writes exactly preset.EnemyDamageMult here at
                // spawn (CreatureFactory.CreateMonsterFromMobClass), so the vanilla
                // value is knowable without having recorded it.
                data.BaseOverallDmgMult = Tuning.Scale(preset.EnemyDamageMult, Damage);

                // Damage resisted. Likewise written from preset.EnemyResistance by
                // CreatureSystem.GenerateMonster.
                data.OverallResistMult = Tuning.Scale(preset.EnemyResistance, Resist);

                // Evasion. This multiplier defaults to 1 and only this mod writes it,
                // so the vanilla baseline is 1 by definition.
                data.BaseOverallDodgeMult = Tuning.Effective(Dodge);

                ApplySight(data, preset);
                ApplyHealth(data);
                ApplyActionPoints(ally, data, turnController);

                // They do not fold at the wrong moment. Monsters never pass through
                // PerkSystem.RefreshPerkPassives - that takes a Mercenary - so unlike
                // HasSecondChance this assignment is not fighting anything.
                data.IgnorePain = true;

                if (Logged.Add(data.UniqueId))
                {
                    ModLog.Info("ally strengthened: " + before + "  ->  " + Describe(ally));
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not strengthen ally " + data.UniqueId + " (" +
                             data.MobClassId + "); it stays vanilla", error);
            }
        }

        /// <summary>
        /// Sight, rebuilt the way <c>CreatureSystem.GenerateMonster</c> builds it:
        /// the mob class's own range plus the difficulty's sight bonus. Skipped
        /// entirely when the mob class cannot be resolved, because a guess here would
        /// be a permanent, saved wrong answer.
        /// </summary>
        private static void ApplySight(CreatureData data, DifficultyPreset preset)
        {
            if (string.IsNullOrEmpty(data.MobClassId) || Data.MobClasses == null)
            {
                return;
            }

            var record = Data.MobClasses.GetRecord(data.MobClassId);
            if (record == null)
            {
                return;
            }

            data.BaseLosLevel = record.Los + Mathf.RoundToInt(preset.EnemyLos) + Tuning.Bonus(LosBonus);
        }

        /// <summary>
        /// Raises the health ceiling. <c>BaseHealth</c> is the game's own
        /// already-difficulty-scaled figure and is never written here, so the target is
        /// the same on every pass.
        ///
        /// A wounded ally keeps its wound - the ceiling moves, the current value does
        /// not - so this cannot be used as a free heal by reloading.
        /// </summary>
        private static void ApplyHealth(CreatureData data)
        {
            var health = data.Health;
            if (health == null || data.BaseHealth <= 0)
            {
                return;
            }

            var target = Mathf.RoundToInt(Tuning.Scale(data.BaseHealth, Health));
            if (target <= 0 || target == health.MaxValue)
            {
                return;
            }

            if (health.IsFull)
            {
                // First pass, at spawn: a fresh ally should arrive at full strength
                // rather than looking like it walked in already hurt.
                health.Reinitialize(target);
            }
            else
            {
                health.ReinitializePreservingCurrent(target);
            }
        }

        /// <summary>
        /// Extra turns per round.
        ///
        /// Two things have to move together: <c>ActionPoints</c>, which decides how
        /// many points the creature is willing to spend, and the turn controller's
        /// contender list, which decides how often it is asked. Spawning code sets
        /// both; so does this.
        ///
        /// The target never lowers an existing value, so a curse that granted bonus
        /// points is not quietly taken back, and re-running still lands on the same
        /// number.
        /// </summary>
        private static void ApplyActionPoints(Monster ally, CreatureData data, TurnController turnController)
        {
            var bonus = Tuning.Bonus(ActionPointBonus);
            if (bonus <= 0 || turnController == null || data.BaseActionPoints <= 0)
            {
                return;
            }

            var target = Math.Max(ally.ActionPoints, data.BaseActionPoints + bonus);
            if (target == ally.ActionPoints)
            {
                return;
            }

            ally.ActionPoints = target;

            // Re-seat rather than top up: RemoveContender clears every slot this
            // creature holds, so rebuilding from zero is correct however many it had.
            // It touches only the standing contender list, not the turn already in
            // progress, so doing this mid-round is safe.
            turnController.RemoveContender(ally);
            for (var i = 0; i < target; i++)
            {
                turnController.AddContender(ally);
            }
        }

        private static string Describe(Monster ally)
        {
            var data = ally.CreatureData;
            return data.MobClassId + "#" + data.UniqueId +
                   " hp=" + (data.Health == null ? "?" : data.Health.MaxValue.ToString()) +
                   " dmg=" + data.BaseOverallDmgMult.ToString("0.##") +
                   " resist=" + data.OverallResistMult.ToString("0.##") +
                   " dodge=" + data.BaseOverallDodgeMult.ToString("0.##") +
                   " los=" + data.BaseLosLevel +
                   " ap=" + ally.ActionPoints;
        }
    }
}
