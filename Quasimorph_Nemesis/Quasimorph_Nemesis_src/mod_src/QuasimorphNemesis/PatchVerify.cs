using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Confirms, at startup, that every patch this mod believes it applied is really
    /// attached to a real game method - and says so loudly when one is not.
    ///
    /// <c>tools/apicheck.py</c> resolves every reference in the built assembly against
    /// the real game assemblies, which is what stops this project shipping a call to a
    /// method the game has dropped. It works by walking the TypeRef and MemberRef
    /// metadata tables, and three of the five methods below are reached by <i>name</i> -
    /// a string, which appears in neither table. This is the step that turns a silent
    /// failure into a line a player can read.
    /// </summary>
    internal static class PatchVerify
    {
        /// <summary>Each patch we expect, as declaring type and method name.</summary>
        private static readonly string[,] Expected =
        {
            { "ComponentsLayout", "SerializeGlobalComponents" },
            { "ComponentsLayout", "DeserializeGlobalComponents" },
            { "Localization", "Get" },
            { "Player", "ProcessDamage" },
            { "DungeonGameMode", "OnPlayerDied" },
            { "CreatureSystem", "KillMonster" },
        };

        internal static void Run(string modDirectory)
        {
            var problems = new List<string>();
            var patched = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                foreach (var method in Harmony.GetAllPatchedMethods())
                {
                    var info = Harmony.GetPatchInfo(method);
                    if (info?.Owners == null || !info.Owners.Contains(ModInfo.HarmonyId))
                    {
                        continue;
                    }
                    patched.Add((method.DeclaringType?.Name ?? "?") + "." + method.Name);
                }
            }
            catch (Exception error)
            {
                problems.Add("could not read the Harmony patch table (" +
                             error.GetType().Name + ")");
            }

            for (var i = 0; i < Expected.GetLength(0); i++)
            {
                var key = Expected[i, 0] + "." + Expected[i, 1];
                if (!patched.Contains(key))
                {
                    problems.Add("patch did not attach: " + key);
                }
            }

            if (problems.Count == 0)
            {
                ModLog.Info("all " + Expected.GetLength(0) + " patches attached");
            }
            else
            {
                ModLog.Warn("this mod is running with reduced function on this game build. " +
                            "The rest of the game is unaffected. Details:");
                foreach (var problem in problems)
                {
                    ModLog.Warn("  - " + problem);
                }

                // Worth naming, because these two failing is not a degraded mod but a
                // silently forgetful one, which is far more confusing to play.
                foreach (var problem in problems)
                {
                    if (problem.Contains("GlobalComponents"))
                    {
                        ModLog.Warn("  !! the roster cannot be saved or loaded on this " +
                                    "build. Nemeses will not survive a reload.");
                        break;
                    }
                }
                ModLog.Warn("If the game has updated, this mod needs rebuilding against it.");
            }

            if (ModConfig.Probe)
            {
                WriteProbe(modDirectory, patched, problems);
            }
        }

        private static void WriteProbe(string modDirectory, HashSet<string> patched,
                                       List<string> problems)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Nemesis probe " + ModInfo.Version);
                text.AppendLine("captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                text.AppendLine("== Patches attached");
                foreach (var name in patched)
                {
                    text.AppendLine("   " + name);
                }

                text.AppendLine();
                text.AppendLine("== Roster");
                if (NemesisRoster.All.Count == 0)
                {
                    text.AppendLine("   empty");
                }
                foreach (var record in NemesisRoster.All)
                {
                    text.AppendLine("   " + (record.Retired ? "[dead] " : "[alive] ") +
                                    NameForge.FullName(record) +
                                    "  id=" + record.Id +
                                    " rank=" + record.Rank +
                                    " base=" + record.BaseMobClassId +
                                    " faction=" + record.FactionId +
                                    " mobclass=" + record.MobClassId);
                }

                text.AppendLine();
                text.AppendLine("== Problems");
                if (problems.Count == 0)
                {
                    text.AppendLine("   none");
                }
                foreach (var problem in problems)
                {
                    text.AppendLine("   " + problem);
                }

                File.WriteAllText(Path.Combine(modDirectory, "probe.txt"), text.ToString(),
                                  new UTF8Encoding(false));
                ModLog.Info("wrote probe.txt");
            }
            catch (Exception error)
            {
                ModLog.Error("could not write probe.txt", error);
            }
        }
    }
}
