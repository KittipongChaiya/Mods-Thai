using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Confirms, at startup, that every patch this mod believes it applied is really
    /// attached to a real game method - and says so loudly when one is not.
    ///
    /// The sibling Retinue mod has no step like this and does not need one: it applies
    /// no patches, so there is nothing to verify that the build-time reference check
    /// has not already resolved. This mod is different in exactly one way that matters:
    /// it reaches private members by name. <c>tools/apicheck.py</c> walks the built
    /// assembly's TypeRef and MemberRef tables, and a name in a string literal is in
    /// neither. A game update that renames <c>_followButton</c> would sail through the
    /// build and fail silently in play. This is the step that turns that into a line
    /// in the log.
    /// </summary>
    internal static class PatchVerify
    {
        /// <summary>Each patch we expect, as declaring type and method name.</summary>
        private static readonly string[,] Expected =
        {
            { "MonsterInspectWindow", "RefreshFollowButton" },
            { "Monster", "get_ShowSignal" },
            { "MonsterInspectWindow", "IsFollowerAlly" },
            { "PlayerInteractionSystem", "EvaluateSecondaryCursorAction" },
        };

        internal static void Run(string modDirectory)
        {
            Targets.Resolve();

            var problems = new List<string>();

            foreach (var name in Targets.Missing)
            {
                problems.Add("field not found: " + name);
            }

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
                ModLog.Info("all " + Expected.GetLength(0) + " patches attached and all " +
                            "private members resolved");
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
                text.AppendLine("Quasimorph Signals probe " + ModInfo.Version);
                text.AppendLine("captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                text.AppendLine("== Patches attached");
                foreach (var name in patched)
                {
                    text.AppendLine("   " + name);
                }

                text.AppendLine();
                text.AppendLine("== Private members");
                Describe(text, typeof(MonsterInspectWindow), Targets.InspectedCreatureField);
                Describe(text, typeof(MonsterInspectWindow), Targets.FollowButtonField);
                Describe(text, typeof(ToggleAllyStateButton), Targets.LeftCaptionField);
                Describe(text, typeof(ToggleAllyStateButton), Targets.RightCaptionField);
                Describe(text, typeof(MonsterInspectWindow), Targets.CloseButtonField);
                Describe(text, typeof(CommonButton), Targets.CommonButtonOnClickField);
                text.AppendLine("   move control usable : " + Targets.MoveButtonUsable);

                text.AppendLine();
                text.AppendLine("== Line-of-sight candidates");
                Describe(text, typeof(Monster), "get_ShowSignal", method: true);
                Describe(text, typeof(Creature), "get_IsSeenByPlayer", method: true);
                Describe(text, typeof(PlayerInteractionSystem),
                         "EvaluateSecondaryCursorAction", method: true);

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

        private static void Describe(StringBuilder text, Type type, string name,
                                     bool method = false)
        {
            try
            {
                MemberInfo member = method
                    ? (MemberInfo)AccessTools.Method(type, name)
                    : AccessTools.Field(type, name);
                text.AppendLine("   " + type.Name + "." + name + " : " +
                                (member == null ? "MISSING" : "found"));
            }
            catch (Exception error)
            {
                text.AppendLine("   " + type.Name + "." + name + " : error " +
                                error.GetType().Name);
            }
        }
    }
}
