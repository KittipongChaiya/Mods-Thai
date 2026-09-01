using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigStack
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling mods: a Workshop mod black-screened this
    /// game on startup by applying a Harmony postfix to <c>MGSC.State.Resolve</c>, the
    /// dependency injection resolver, purely to cache the <c>State</c> in a static field.
    /// That is never necessary — every hook is handed an <see cref="IModContext"/> that
    /// already carries it. Nothing here may patch <c>State</c>, <c>GameLoop</c>,
    /// <c>Data</c>, or anything else on the bootstrap path. Our single patch target is an
    /// ordinary item-factory method.
    /// </summary>
    public static class BigStackMod
    {
        private static string _modDirectory;
        private static State _state;

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

        /// <summary>The game state, captured from the hook context - never from a patch.</summary>
        internal static State GameState => _state;

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void OnAfterConfigsLoaded(IModContext context)
        {
            Guard("AfterConfigsLoaded", () =>
            {
                _ = ModDirectory;              // forces the log banner
                _state = context.State;
                ModConfig.Load(_modDirectory);

                if (!ModConfig.Enabled)
                {
                    ModLog.Info("disabled by config; no patches applied");
                    return;
                }

                // One gameplay patch, nothing on the bootstrap path. PatchAll is allowed
                // to throw here: UserModSystem.InvokeHook catches it, logs it, and the
                // game carries on without us - the correct failure mode.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                ModLog.Info("harmony patch applied: ItemFactory.GetMaxStackSize");
            });
        }

        /// <summary>
        /// Items already in a save need no migration: the game's own
        /// <c>ItemInteractionSystem.FixStacksCount</c> re-derives every item's maximum
        /// from its record whenever the player reaches a station, the after-raid screen,
        /// the arsenal, augmentation, or either trade screen.
        ///
        /// These hooks exist only to check the wind-down risk at the moments the player
        /// is on the ship and might be about to change the config or uninstall.
        /// </summary>
        [Hook(ModHookType.AfterSaveLoaded)]
        public static void OnAfterSaveLoaded(IModContext context) => CheckRisk(context, "save loaded");

        [Hook(ModHookType.SpaceStarted)]
        public static void OnSpaceStarted(IModContext context) => CheckRisk(context, "space");

        private static void CheckRisk(IModContext context, string reason)
        {
            Guard("CheckRisk(" + reason + ")", () =>
            {
                _state = context?.State ?? _state;
                UninstallRisk.Check(_state, reason);
            });
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
