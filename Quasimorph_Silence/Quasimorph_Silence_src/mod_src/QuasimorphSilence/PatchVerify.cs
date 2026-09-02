using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using MGSC;

namespace QuasimorphSilence
{
    /// <summary>
    /// Confirms, at startup, that the patch this mod depends on is really attached.
    ///
    /// This mod has an unusually small patch surface - one method - because almost
    /// everything it needs is public. That single patch is load-bearing for the whole
    /// mod, though, so its absence is worth naming rather than leaving the player to
    /// discover that the readout never updates.
    /// </summary>
    internal static class PatchVerify
    {
        private static readonly string[,] Expected =
        {
            { "CreatureSystem", "PropagateNoise" },
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
                ModLog.Info("noise patch attached; every other member this mod uses is public");
            }
            else
            {
                ModLog.Warn("this mod cannot see or change noise on this game build. The " +
                            "rest of the game is unaffected. Details:");
                foreach (var problem in problems)
                {
                    ModLog.Warn("  - " + problem);
                }
                ModLog.Warn("If the game has updated, this mod needs rebuilding against it.");
            }

            if (ModConfig.LogEveryNoise)
            {
                WriteProbe(modDirectory, patched, problems);
            }
        }

        /// <summary>
        /// Records the game's own noise settings, which is the thing this project most
        /// wanted written down: nothing in the game or its documentation says what a
        /// footstep actually costs you.
        /// </summary>
        private static void WriteProbe(string modDirectory, HashSet<string> patched,
                                       List<string> problems)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Silence probe " + ModInfo.Version);
                text.AppendLine("captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                text.AppendLine("== Patches attached");
                foreach (var name in patched)
                {
                    text.AppendLine("   " + name);
                }

                text.AppendLine();
                text.AppendLine("== Vanilla noise radii");
                try
                {
                    var settings = Data.Global;
                    if (settings == null)
                    {
                        text.AppendLine("   GlobalSettings unavailable at this moment");
                    }
                    else
                    {
                        text.AppendLine("   step  : " + settings.NoiseStepRadius);
                        text.AppendLine("   door  : " + settings.NoiseDoorRadius);
                        text.AppendLine("   death : " + settings.NoiseDeathRadius);
                    }
                }
                catch (Exception error)
                {
                    text.AppendLine("   unreadable: " + error.GetType().Name);
                }

                text.AppendLine();
                text.AppendLine("== Noise types the game defines");
                foreach (var value in Enum.GetValues(typeof(NoiseType)))
                {
                    text.AppendLine("   " + (int)value + "  " + value);
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
