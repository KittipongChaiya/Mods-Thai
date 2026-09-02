using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using MGSC;

namespace QuasimorphStride
{
    /// <summary>
    /// Confirms, at startup, that every patch this mod believes it applied is really
    /// attached to a real game method - and says so loudly when one is not.
    ///
    /// <b>What the build already guarantees, and what it cannot.</b>
    /// <c>tools/apicheck.py</c> walks the built assembly's TypeRef and MemberRef tables
    /// and resolves each one against the shipped game assemblies. Every member this mod
    /// <i>calls</i> - <c>PerkSystem.GetPerkParameterBool</c>, <c>MapObstacle.Door</c>,
    /// <c>TooltipFactory.AddPanelToTooltip</c> and the rest - is covered by that, which
    /// is a stronger position than the sibling Signals mod is in, because this one
    /// reaches for no private members at all.
    ///
    /// The patch targets themselves are the exception, and they are the exception in
    /// every Harmony mod: <c>[HarmonyPatch(typeof(X), nameof(X.Y))]</c> compiles to a
    /// type token and a <i>string</i>, and a string is not a member reference. Two
    /// things close that gap. <c>nameof</c> rather than a literal means a renamed method
    /// fails the build on this machine's game version; this class means it is a line in
    /// the log on anyone else's.
    /// </summary>
    internal static class PatchVerify
    {
        /// <summary>Each patch we expect, as declaring type and method name.</summary>
        private static readonly string[,] Expected =
        {
            { "PlayerInteractionSystem", "CanInteractObstacles" },
            { "PlayerInteractionSystem", "CanUseInventory" },
            { "PlayerInteractionSystem", "CanOpenAllyInventory" },
            { "PlayerInteractionSystem", "TakeItemOrLootCorpse" },
            { "PlayerInteractionSystem", "ProcessCmd" },
            { "PlayerInteractionSystem", "InteractVestSlot" },
            { "TooltipFactory", "BuildMovementStateTooltip" },
        };

        internal static void Run(string modDirectory)
        {
            var problems = new List<string>();

            var patched = new HashSet<string>();
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
                ModLog.Warn("If the game has updated, this mod needs rebuilding against it.");
            }

            if (ModConfig.Probe)
            {
                WriteProbe(modDirectory, patched, problems);
            }
        }

        /// <summary>
        /// Records what was actually resolved, so a bug report can be a single file
        /// rather than a conversation.
        /// </summary>
        private static void WriteProbe(string modDirectory, HashSet<string> patched,
                                       List<string> problems)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Stride probe " + ModInfo.Version);
                text.AppendLine("captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                text.AppendLine("== Patches attached");
                foreach (var name in patched)
                {
                    text.AppendLine("   " + name);
                }

                text.AppendLine();
                text.AppendLine("== The three gates, as this game build declares them");
                Describe(text, typeof(PlayerInteractionSystem), "CanInteractObstacles");
                Describe(text, typeof(PlayerInteractionSystem), "CanUseInventory");
                Describe(text, typeof(PlayerInteractionSystem), "CanOpenAllyInventory");

                text.AppendLine();
                text.AppendLine("== Scope sources");
                Describe(text, typeof(PlayerInteractionSystem), "TakeItemOrLootCorpse");
                Describe(text, typeof(PlayerInteractionSystem), "ProcessCmd");
                Describe(text, typeof(PlayerInteractionSystem), "InteractVestSlot");
                text.AppendLine("   scope currently open : " + PickupScope.IsOpen);

                text.AppendLine();
                text.AppendLine("== Tooltip");
                Describe(text, typeof(TooltipFactory), "BuildMovementStateTooltip");
                Describe(text, typeof(TooltipFactory), "AddPanelToTooltip");

                text.AppendLine();
                text.AppendLine("== The perk parameter this mod emulates");
                text.AppendLine("   ParameterNames.PARAM_RUN_ACTIONS = " +
                                ParameterNames.PARAM_RUN_ACTIONS);

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

        private static void Describe(StringBuilder text, Type type, string name)
        {
            try
            {
                var method = AccessTools.Method(type, name);
                text.AppendLine("   " + type.Name + "." + name + " : " +
                                (method == null ? "MISSING" : "found"));
            }
            catch (Exception error)
            {
                text.AppendLine("   " + type.Name + "." + name + " : error " +
                                error.GetType().Name);
            }
        }
    }
}
