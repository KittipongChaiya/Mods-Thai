using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Big Pack and Floor Loot mods: a
    /// Workshop mod black-screened this game on startup by applying a Harmony postfix
    /// to <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely to
    /// cache the <c>State</c> in a static field. That is never necessary - every hook
    /// is handed an <see cref="IModContext"/> that already carries it. Nothing in this
    /// mod may patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on
    /// the bootstrap path. The one patch target here is <c>Localization.Get</c>, an
    /// ordinary lookup that answers for two keys of our own and passes everything
    /// else straight through.
    /// </summary>
    public static class RuthlessMod
    {
        private static string _modDirectory;

        private static string ModDirectory
        {
            get
            {
                if (_modDirectory == null)
                {
                    _modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    ModLog.Start(_modDirectory);
                }
                return _modDirectory;
            }
        }

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void OnAfterConfigsLoaded(IModContext context)
        {
            Guard("AfterConfigsLoaded", () =>
            {
                _ = ModDirectory;              // forces the log banner
                ModConfig.Load(_modDirectory);

                if (!ModConfig.Enabled)
                {
                    ModLog.Info("disabled by config; no difficulty added, no patches applied");
                    return;
                }

                ConflictCheck.Run();

                if (ModConfig.Probe)
                {
                    DataProbe.Dump(_modDirectory);
                }

                // Capture vanilla before anything is written, so the behaviour layers
                // always have somewhere honest to restore to.
                TacticalAi.CaptureVanilla();
                MobLoadouts.CaptureVanilla();

                DifficultyRegistration.Register();

                VerifyPatchTargets();

                // PatchAll is allowed to throw here: UserModSystem.InvokeHook catches
                // it, logs it, and the game carries on without us - the correct
                // failure mode for a mod.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                ModLog.Info("harmony patches applied: Localization.Get (both overloads)");
            });
        }

        /// <summary>
        /// Every moment a run's difficulty is freshly knowable. The behaviour layers
        /// are switched on here and, just as importantly, switched off again when the
        /// run in front of us is a vanilla one.
        /// </summary>
        [Hook(ModHookType.AfterSaveLoaded)]
        public static void OnAfterSaveLoaded(IModContext context) => Sync(context, "save loaded");

        [Hook(ModHookType.SpaceStarted)]
        public static void OnSpaceStarted(IModContext context) => Sync(context, "space");

        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context) => Sync(context, "dungeon");

        private static void Sync(IModContext context, string reason)
        {
            Guard("Sync(" + reason + ")", () =>
            {
                if (!ModConfig.Enabled)
                {
                    return;
                }

                var active = DifficultyRegistration.IsActive(context?.State);
                if (active)
                {
                    TacticalAi.Apply();
                    MobLoadouts.Apply();
                }
                else
                {
                    // Config records are shared global state. A vanilla run started in
                    // the same session must get vanilla values back.
                    TacticalAi.Restore();
                    MobLoadouts.Restore();
                }
            });
        }

        /// <summary>
        /// The build-time reference check resolves every game member the mod calls,
        /// but a Harmony target named in an attribute is a string and slips past it.
        /// Resolving the two overloads here turns "the difficulty panel is showing a
        /// raw key" into one explicit log line instead of a puzzle.
        /// </summary>
        private static void VerifyPatchTargets()
        {
            var byFlag = AccessTools.Method(typeof(Localization), nameof(Localization.Get),
                                            new[] { typeof(string), typeof(bool) });
            var byLang = AccessTools.Method(typeof(Localization), nameof(Localization.Get),
                                            new[] { typeof(string), typeof(Localization.Lang) });

            if (byFlag == null || byLang == null)
            {
                ModLog.Error("Localization.Get overloads not found (bool=" + (byFlag != null) +
                             " lang=" + (byLang != null) + "). The difficulty will still be " +
                             "added, but its panel will show the raw localization key.");
                return;
            }
            ModLog.Info("patch targets resolved: both Localization.Get overloads");
        }

        private static void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                try
                {
                    ModLog.Error("Hook " + what + " failed", error);
                }
                catch (Exception)
                {
                    // Logging must never be the thing that breaks the game either.
                }
            }
        }
    }
}
