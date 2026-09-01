using System;
using System.Globalization;
using System.IO;
using System.Text;
using MGSC;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Writes every value this mod reasons about to probe.txt, on demand.
    ///
    /// The game's config tables live inside Unity assets rather than loose files, so
    /// they cannot be read by static analysis of the assemblies. That leaves exactly one
    /// honest way to know what the vanilla numbers are: ask the running game. The tuning
    /// in <see cref="AllyPower"/>, the escort choice in <see cref="RetinueSquad"/> and
    /// the bribe list in <see cref="Recruiting"/> are all meant to be checked against
    /// this output rather than trusted.
    ///
    /// Off by default; <c>probe=true</c> in config.txt turns it on. It runs at
    /// <c>AfterConfigsLoaded</c>, so reaching the main menu is enough - no raid needed.
    /// </summary>
    internal static class DataProbe
    {
        private const string FileName = "probe.txt";

        internal static void Dump(string modDirectory)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Retinue - data probe");
                text.AppendLine("written " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                DumpDifficulties(text);
                DumpFactions(text);
                DumpAiPresets(text);
                DumpMedkits(text);

                var path = Path.Combine(modDirectory, FileName);
                File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
                ModLog.Info("probe written to " + path);
            }
            catch (Exception error)
            {
                ModLog.Error("probe failed", error);
            }
        }

        /// <summary>
        /// The four preset fields every ally stat is computed from. Reading them across
        /// the whole ladder is what proves the arithmetic in <see cref="AllyPower"/> is
        /// starting from the right baseline.
        /// </summary>
        private static void DumpDifficulties(StringBuilder text)
        {
            text.AppendLine("=== Data.DifficultyPresets - the ally baseline ===");
            text.AppendLine("(allies are monsters, so every one of these applies to them too)");
            text.AppendLine();

            var presets = Data.DifficultyPresets;
            if (presets == null)
            {
                text.AppendLine("  (null)");
                text.AppendLine();
                return;
            }

            foreach (var entry in presets)
            {
                var p = entry.Value;
                if (p == null)
                {
                    text.AppendLine("--- " + entry.Key + "  (null) ---");
                    continue;
                }
                text.AppendLine("--- " + entry.Key + " ---");
                text.AppendLine("  EnemyHealth          " + F(p.EnemyHealth) + "   (already folded into BaseHealth)");
                text.AppendLine("  EnemyDamageMult      " + F(p.EnemyDamageMult) + "   (the vanilla BaseOverallDmgMult)");
                text.AppendLine("  EnemyResistance      " + F(p.EnemyResistance) + "   (the vanilla OverallResistMult)");
                text.AppendLine("  EnemyActionPoint     " + F(p.EnemyActionPoint) + "   (already folded into BaseActionPoints)");
                text.AppendLine("  EnemyLos             " + F(p.EnemyLos) + "   (added to MobClassRecord.Los)");
                text.AppendLine("  EnemyDodgeMult       " + F(p.EnemyDodgeMult));
            }
            text.AppendLine();
        }

        /// <summary>
        /// Where the squad comes from. A faction with no guard ids cannot supply a
        /// retinue; a run where every line here is empty is the one case where the
        /// squad silently does not appear, and this is how to see it.
        /// </summary>
        private static void DumpFactions(StringBuilder text)
        {
            text.AppendLine("=== Data.Factions - escort candidates ===");
            var factions = Data.Factions;
            if (factions == null)
            {
                text.AppendLine("  (null)");
                text.AppendLine();
                return;
            }

            text.AppendLine("  count " + factions.Count);
            text.AppendLine();
            foreach (var r in factions.Records)
            {
                if (r == null)
                {
                    continue;
                }
                text.AppendLine("--- " + r.Id + (r.Enabled ? "" : "   [disabled]") + " ---");
                text.AppendLine("  InitialTechLevel     " + r.InitialTechLevel);
                text.AppendLine("  CEOGuardCreatureId   " + Describe(r.CEOGuardCreatureId));
                text.AppendLine("  GuardCreatureId      " + Describe(r.GuardCreatureId));
            }
            text.AppendLine();
        }

        /// <summary>
        /// A guard mob class, with the stats an escort is judged on. An empty or
        /// unarmoured entry here explains a squad that dies instantly.
        /// </summary>
        private static string Describe(string mobClassId)
        {
            if (string.IsNullOrEmpty(mobClassId))
            {
                return "(none)";
            }

            var record = Data.MobClasses?.GetRecord(mobClassId);
            if (record == null)
            {
                return mobClassId + "   [MISSING from Data.MobClasses - unusable]";
            }

            return mobClassId +
                   "   ai=" + record.AiPresetId +
                   " los=" + record.Los +
                   " hp+" + record.HealthMod +
                   " ap+" + record.ActionPointsMod +
                   " dodge+" + F(record.DodgeMod) +
                   " tech+" + record.EquipmentTechLevelBonus;
        }

        /// <summary>
        /// Who can be bribed, and who this mod would open up. Counting the
        /// <c>[thinker]</c> tags is the check on <see cref="Recruiting"/>: none means
        /// the layer is inert, all of them means the heuristic is too loose.
        /// </summary>
        private static void DumpAiPresets(StringBuilder text)
        {
            text.AppendLine("=== Data.AiPresets - recruitability ===");
            var presets = Data.AiPresets;
            if (presets == null)
            {
                text.AppendLine("  (null)");
                text.AppendLine();
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
                var classCount = r.ItemsClassesAsGifts == null ? -1 : r.ItemsClassesAsGifts.Count;
                var idCount = r.ItemsIdsAsGifts == null ? -1 : r.ItemsIdsAsGifts.Count;
                var willing = classCount > 0 || idCount > 0;

                text.AppendLine("--- " + r.Id +
                                (thinker ? "   [thinker]" : "") +
                                (willing ? "   [already bribable]" : "") + " ---");
                text.AppendLine("  CanUseItems          " + r.CanUseItems);
                text.AppendLine("  GrenadeChance        " + F(r.GrenadeChance));
                text.AppendLine("  BestFiremodeChance   " + F(r.BestFiremodeChance));
                text.AppendLine("  CanPanic             " + r.CanPanic);
                text.AppendLine("  SurrenderByDamage    " + F(r.SurrenderChanceByDamage));
                text.AppendLine("  SurrendersByOutnumbr " + r.SurrendersByOutnumber);
                text.AppendLine("  ItemsClassesAsGifts  " +
                                (classCount < 0 ? "(null)" : classCount.ToString()) +
                                (classCount > 0 ? "  " + string.Join(", ", r.ItemsClassesAsGifts) : ""));
                text.AppendLine("  ItemsIdsAsGifts      " +
                                (idCount < 0 ? "(null)" : idCount.ToString()) +
                                (idCount > 0 ? "  " + string.Join(", ", r.ItemsIdsAsGifts) : ""));
            }
            text.AppendLine();
        }

        /// <summary>
        /// Every Medpack in the game, so the one the squad is issued can be checked
        /// against what a player would actually consider a basic medkit.
        /// </summary>
        private static void DumpMedkits(StringBuilder text)
        {
            text.AppendLine("=== Data.Items - Medpack class (squad kit candidates) ===");
            var items = Data.Items;
            if (items == null)
            {
                text.AppendLine("  (null)");
                text.AppendLine();
                return;
            }

            var found = 0;
            foreach (var id in items.Ids)
            {
                var record = items.GetRecord(id) is CompositeItemRecord composite
                    ? composite.GetRecord<ItemRecord>()
                    : null;
                if (record == null || record.ItemClass != ItemClass.Medpack)
                {
                    continue;
                }
                found++;
                text.AppendLine("  " + id +
                                "   tech=" + record.TechLevel +
                                " price=" + F(record.Price) +
                                " weight=" + F(record.Weight));
            }

            if (found == 0)
            {
                text.AppendLine("  (none - allies will spawn without a medkit)");
            }
            text.AppendLine();
        }

        private static string F(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
