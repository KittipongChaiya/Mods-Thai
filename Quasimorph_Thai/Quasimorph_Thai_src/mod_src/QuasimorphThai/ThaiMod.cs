using System;
using System.IO;
using System.Reflection;
using MGSC;
using UnityEngine;

namespace QuasimorphThai
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Nothing here modifies a game file. The Thai table is served through the game's
    /// own <c>CustomResources</c> override API and the font is swapped in memory.
    /// </summary>
    public static class ThaiMod
    {
        /// <summary>A key that exists in every table version and is short to compare.</summary>
        private const string ProbeKey = "ui.lang";

        private static string _modDirectory;

        private static string ModDirectory
        {
            get
            {
                if (_modDirectory == null)
                {
                    // The ResourcesLoad hook gets no ModContext, so we locate ourselves.
                    _modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    ModLog.Start(_modDirectory);
                }
                return _modDirectory;
            }
        }

        /// <summary>
        /// Called by <c>CustomResources.Load</c> for every resource the game loads, so it
        /// must stay cheap and must never throw: the caller has no exception handling and
        /// a throw here would take the game down.
        /// </summary>
        [Hook(ModHookType.ResourcesLoad)]
        public static UnityEngine.Object OnResourcesLoad(string path)
        {
            try
            {
                if (!string.Equals(path, ThaiTable.ResourcePath, StringComparison.Ordinal))
                {
                    return null;
                }
                return ThaiTable.Provide(ModDirectory);
            }
            catch (Exception error)
            {
                try
                {
                    ModLog.Error("Resource hook failed for '" + path + "'", error);
                }
                catch (Exception)
                {
                    // Logging must never be the thing that breaks the game either.
                }
                return null;
            }
        }

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void OnAfterConfigsLoaded(IModContext context)
        {
            Guard("AfterConfigsLoaded", () => EnsureThaiTableInstalled("AfterConfigsLoaded"));
        }

        [Hook(ModHookType.MainMenuStarted)]
        public static void OnMainMenuStarted(IModContext context)
        {
            Guard("MainMenuStarted", () =>
            {
                EnsureThaiTableInstalled("MainMenuStarted");
                ThaiFont.Apply(ModDirectory);
                ReportState();
                Diagnostics.CaptureAfterDelay(ModDirectory, 4f);
            });
        }

        /// <summary>
        /// If the localization singleton was built before our resource hook was
        /// registered, the game is holding the untranslated table. Re-running the game's
        /// own loader picks our table up, so the mod repairs itself instead of silently
        /// showing English.
        /// </summary>
        private static void EnsureThaiTableInstalled(string caller)
        {
            if (IsThaiActive())
            {
                return;
            }

            var localization = Singleton<Localization>.Instance;
            if (localization == null)
            {
                return;
            }

            var loadDb = typeof(Localization).GetMethod(
                "LoadDB", BindingFlags.Instance | BindingFlags.NonPublic);
            if (loadDb == null)
            {
                ModLog.Error("Localization.LoadDB is gone; cannot reload the table.");
                return;
            }

            ModLog.Warn(caller + ": the English slot is still English, so the table was built "
                        + "before our hook was registered. Reloading it.");
            loadDb.Invoke(localization, null);
            localization.ChangeLang(localization.CurrentLang);
            ModLog.Info(caller + ": reload done, thai now active = " + IsThaiActive());
        }

        /// <summary>
        /// Asks the game itself whether the slot we translate into actually holds Thai.
        /// Checking the outcome rather than our own bookkeeping means the repair still
        /// fires correctly no matter how the game orders its startup.
        /// </summary>
        private static bool IsThaiActive()
        {
            try
            {
                if (Singleton<Localization>.Instance == null)
                {
                    return false;
                }
                // The two-argument overload reads a specific language slot, so this stays
                // correct even when the player currently has Russian or Korean selected.
                return ContainsThai(Localization.Get(ProbeKey, Localization.Lang.EnglishUS));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void ReportState()
        {
            ModLog.Info("table requests=" + ThaiTable.RequestCount
                        + " rows=" + ThaiTable.RowCount);
            foreach (var line in ThaiFont.Describe())
            {
                ModLog.Info("font " + line);
            }

            var sample = Localization.Get(ProbeKey, Localization.Lang.EnglishUS);
            ModLog.Info("probe " + ProbeKey + " = '" + sample + "' (thai=" + ContainsThai(sample) + ")");
        }

        private static bool ContainsThai(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (var c in value)
            {
                if (c >= '฀' && c <= '๿')
                {
                    return true;
                }
            }
            return false;
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                ModLog.Error("Hook " + what + " failed", error);
            }
        }
    }
}
