using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphStride
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Signals, Retinue, Big Pack and Ruthless
    /// mods: a Workshop mod black-screened this game on startup by applying a Harmony
    /// postfix to <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely
    /// to cache the <c>State</c> in a static field. That is never necessary - every
    /// hook is handed an <see cref="IModContext"/> that already carries it. Nothing
    /// here may patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on
    /// the bootstrap path.
    ///
    /// This mod is unusually well behaved even by that standard. It holds no state
    /// about the world, keys nothing to a creature, writes nothing to a save, and needs
    /// no per-turn sweep - so it has no <c>DungeonUpdate</c> hook at all. It answers
    /// three permission questions and goes back to sleep.
    /// </summary>
    public static class StrideMod
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
                    ModLog.Info("disabled by config; no patches applied, nothing touched");
                    return;
                }

                if (!ModConfig.AnythingUnlocked)
                {
                    ModLog.Warn("every unlock is switched off, so this mod would answer " +
                                "every question exactly as the game already does. No " +
                                "patches applied. Set at least one run_* key to true, or " +
                                "enabled=false to say so deliberately.");
                    return;
                }

                ConflictCheck.Run();

                // PatchAll is allowed to throw here: UserModSystem.InvokeHook catches
                // it, logs it, and the game carries on without us - the correct
                // failure mode for a mod that only adds a convenience.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());

                PatchVerify.Run(_modDirectory);
            });
        }

        /// <summary>
        /// A floor has been entered - a new raid, the next floor down, or a save
        /// reloaded mid-raid.
        ///
        /// There is nothing to restore, because this mod remembers nothing about the
        /// world. The one thing it does hold is the pickup scope depth, and dropping it
        /// here means a scope somehow left open by a future game build heals at the
        /// next elevator instead of quietly widening the mod for the session.
        /// </summary>
        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            Guard("DungeonStarted", PickupScope.Reset);
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
