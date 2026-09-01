using System;
using System.Globalization;
using System.IO;
using System.Text;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Writes every value this mod reasons about to probe.txt, on demand.
    ///
    /// The game's config tables live inside Unity assets rather than loose JSON, so
    /// they cannot be read by static analysis of the assemblies. That leaves exactly
    /// one honest way to know what the vanilla numbers are: ask the running game.
    /// Every multiplier in <see cref="PresetTuning"/>, <see cref="TacticalAi"/> and
    /// <see cref="MobLoadouts"/> is meant to be checked against this output rather
    /// than trusted.
    ///
    /// Off by default; <c>probe=true</c> in config.txt turns it on.
    /// </summary>
    internal static class DataProbe
    {
        private const string FileName = "probe.txt";

        internal static void Dump(string modDirectory)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Hardcore Tactical Ruthless - data probe");
                text.AppendLine("written " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                DumpPresets(text);
                DumpAiPresets(text);
                DumpMobClasses(text);

                var path = Path.Combine(modDirectory, FileName);
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
                ModLog.Info("probe written to " + path);
            }
            catch (Exception error)
            {
                ModLog.Error("probe failed", error);
            }
        }

        private static void DumpPresets(StringBuilder text)
        {
            text.AppendLine("=== Data.DifficultyPresets ===");
            var presets = Data.DifficultyPresets;
            if (presets == null)
            {
                text.AppendLine("  (null)");
                return;
            }

            foreach (var entry in presets)
            {
                var p = entry.Value;
                text.AppendLine("--- " + entry.Key + (p == null ? "  (null)" : "") + " ---");
                if (p == null)
                {
                    continue;
                }

                text.AppendLine("  icon descriptor      " + (p.ContentDescriptor == null ? "NULL" : "present"));
                text.AppendLine("  EnemyHealth          " + F(p.EnemyHealth));
                text.AppendLine("  EnemyDamageMult      " + F(p.EnemyDamageMult));
                text.AppendLine("  EnemyResistance      " + F(p.EnemyResistance));
                text.AppendLine("  EnemyActionPoint     " + F(p.EnemyActionPoint));
                text.AppendLine("  EnemyLos             " + F(p.EnemyLos));
                text.AppendLine("  EnemyDodgeMult       " + F(p.EnemyDodgeMult));
                text.AppendLine("  MonsterPoints        " + F(p.MonsterPoints));
                text.AppendLine("  ItemPoints           " + F(p.ItemPoints));
                text.AppendLine("  KilledMobsItemsCond  " + F(p.KilledMobsItemsCondition));
                text.AppendLine("  BarterValue          " + F(p.BarterValue));
                text.AppendLine("  MissionRewardPoints  " + F(p.MissionRewardPoints));
                text.AppendLine("  ExpMult              " + F(p.ExpMult));
                text.AppendLine("  FactionReputation    " + F(p.FactionReputation));
                text.AppendLine("  FactionGrowthSpeed   " + F(p.FactionGrowthSpeed));
                text.AppendLine("  ProcMissionLifetime  " + F(p.ProcMissionLifetime));
                text.AppendLine("  WeightSatietyDrain   " + F(p.WeightSatietyDrainMult));
                text.AppendLine("  QmorphLevelGrowth    " + F(p.QmorphLevelGrowth));
                text.AppendLine("  QmorphStatsAffect    " + F(p.QmorphStatsAffect));
                // Named "Time" but labelled "crafting speed" in the UI. The two imply
                // opposite directions, which is why nothing tunes it yet.
                text.AppendLine("  MagnumCraftingTime   " + F(p.MagnumCraftingTime));
                text.AppendLine("  MissionStageCountMod " + F(p.MissionStageCountMod));
                text.AppendLine("  RndEventsChance      " + F(p.RndEventsChance));
                text.AppendLine("  StartingMercCount    " + F(p.StartingMercCount));
                text.AppendLine("  StartingClassesCount " + F(p.StartingClassesCount));
                text.AppendLine("  EvacRules            " + p.EvacRules);
                text.AppendLine("  DeathPenalty         " + p.DeathPenalty);
                text.AppendLine("  RevivePenalty        " + p.RevivePenalty);
                text.AppendLine("  DropPenalty          " + p.DropPenalty);
                text.AppendLine("  StartingEquip        " + p.StartingEquip);
                text.AppendLine("  BackpacksSize        " + p.BackpacksSize);
                text.AppendLine("  ItemsStackSize       " + p.ItemsStackSize);
                text.AppendLine("  DeathGift            " + p.DeathGift);
                text.AppendLine("  LosePerks            " + p.LosePerks);
                text.AppendLine("  LoseRank             " + p.LoseRank);
                text.AppendLine("  Tutorial             " + p.Tutorial);
                text.AppendLine("  SmoothProgression    " + p.SmoothProgression);
                text.AppendLine("  SpendAPAtElevator    " + p.SpendAPAtElevator);
                text.AppendLine("  ImmutableDifficulty  " + p.ImmutableDifficulty);
                text.AppendLine("  EquipRepairAfterMis  " + p.EquipRepairAfterMission);
                text.AppendLine("  LoseMissionOnEvac    " + p.LoseMissionOnEvacuation);
                text.AppendLine("  ForbidKillFaction    " + p.ForbidKillFaction);
                text.AppendLine("  RndEventsEnabled     " + p.RndEventsEnabled);
            }
            text.AppendLine();
        }

        private static void DumpAiPresets(StringBuilder text)
        {
            text.AppendLine("=== Data.AiPresets ===");
            var presets = Data.AiPresets;
            if (presets == null)
            {
                text.AppendLine("  (null)");
                return;
            }

            text.AppendLine("  count " + presets.Count);
            text.AppendLine();
            foreach (var r in presets.Records)
            {
                if (r == null)
                {
                    continue;
                }
                var thinker = r.CanUseItems || r.GrenadeChance > 0f || r.BestFiremodeChance > 0f;
                text.AppendLine("--- " + r.Id + (thinker ? "   [thinker]" : "") + " ---");
                text.AppendLine("  HuntMemory           " + r.HuntMemory);
                text.AppendLine("  InvestigateMemory    " + r.InvestigateMemory);
                text.AppendLine("  GrenadeChance        " + F(r.GrenadeChance));
                text.AppendLine("  BestFiremodeChance   " + F(r.BestFiremodeChance));
                text.AppendLine("  UltimateChance       " + F(r.UltimateChance));
                text.AppendLine("  AvoidMineChance      " + F(r.AvoidMineChance));
                text.AppendLine("  MaxAttacksPercent    " + F(r.MaxAttacksPercent));
                text.AppendLine("  CanOpenDoor          " + r.CanOpenDoor);
                text.AppendLine("  CanMeleeAttackDoor   " + r.CanMeleeAttackDoor);
                text.AppendLine("  CanUseItems          " + r.CanUseItems);
                text.AppendLine("  CanPanic             " + r.CanPanic);
                text.AppendLine("  CanRage              " + r.CanRage);
                text.AppendLine("  PeriodicallySleeps   " + r.PeriodicallySleeps);
                text.AppendLine("  PanicByTeammateDeath " + F(r.PanicChanceByTeammateDeath));
                text.AppendLine("  PanicByLeaderDeath   " + F(r.PanicChanceByLeaderDeath));
                text.AppendLine("  PanicByDamage        " + F(r.PanicChanceByDamage));
                text.AppendLine("  PanicByEccolapse     " + F(r.PanicChanceByEccolapse));
                text.AppendLine("  SurrenderByDamage    " + F(r.SurrenderChanceByDamage));
                text.AppendLine("  SurrendersByOutnumbr " + r.SurrendersByOutnumber);
                text.AppendLine("  EndlessHuntBehave    " + r.EndlessHuntBehave);
                text.AppendLine("  SomniaBehave         " + r.SomniaBehave);
                if (r.AvoidDangerTerrainChances != null)
                {
                    foreach (var hazard in r.AvoidDangerTerrainChances)
                    {
                        text.AppendLine("  avoid " + hazard.Key + " = " + F(hazard.Value));
                    }
                }
            }
            text.AppendLine();
        }

        private static void DumpMobClasses(StringBuilder text)
        {
            text.AppendLine("=== Data.MobClasses ===");
            var classes = Data.MobClasses;
            if (classes == null)
            {
                text.AppendLine("  (null)");
                return;
            }

            text.AppendLine("  count " + classes.Count);
            text.AppendLine();
            foreach (var r in classes.Records)
            {
                if (r == null)
                {
                    continue;
                }
                text.AppendLine("--- " + r.Id + " ---");
                text.AppendLine("  AiPresetId           " + r.AiPresetId);
                text.AppendLine("  EquipTechLevelBonus  " + r.EquipmentTechLevelBonus);
                text.AppendLine("  ItemConditionPercent " + r.ItemConditionPercent.Min + ".." + r.ItemConditionPercent.Max);
                text.AppendLine("  AdditAmmo            " + r.AdditAmmo.Min + ".." + r.AdditAmmo.Max);
                text.AppendLine("  AdditItemCount       " + r.AdditItemCount.Min + ".." + r.AdditItemCount.Max);
                text.AppendLine("  Los                  " + r.Los);
                text.AppendLine("  HealthMod            " + r.HealthMod);
                text.AppendLine("  ActionPointsMod      " + r.ActionPointsMod);
                text.AppendLine("  DodgeMod             " + F(r.DodgeMod));
            }
            text.AppendLine();
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
