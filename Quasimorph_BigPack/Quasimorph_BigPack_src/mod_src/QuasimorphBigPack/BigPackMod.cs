using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigPack
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Floor Loot mod: a Workshop mod
    /// black-screened this game on startup by applying a Harmony postfix to
    /// <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely to cache the
    /// <c>State</c> in a static field. That is never necessary - every hook is handed an
    /// <see cref="IModContext"/> that already carries it. Nothing in this mod may patch
    /// <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on the bootstrap
    /// path. Both of our patch targets are ordinary gameplay methods.
    /// </summary>
    public static class BigPackMod
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

                // Two gameplay patches, nothing on the bootstrap path. PatchAll is
                // allowed to throw here: UserModSystem.InvokeHook catches it, logs it,
                // and the game carries on without us - the correct failure mode.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                ModLog.Info("harmony patches applied: Inventory.ResizeStorage, " +
                            "CreatureData.GetItemsWeight");
            });
        }

        /// <summary>
        /// Storages built in the <c>Inventory</c> constructor never pass through
        /// <c>ResizeStorage</c>, so the patch alone would miss a mercenary carrying no
        /// backpack. These three hooks are every moment the roster is freshly available.
        /// </summary>
        [Hook(ModHookType.AfterSaveLoaded)]
        public static void OnAfterSaveLoaded(IModContext context) => Reassert(context, "save loaded");

        [Hook(ModHookType.SpaceStarted)]
        public static void OnSpaceStarted(IModContext context) => Reassert(context, "space");

        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context) => Reassert(context, "dungeon");

        private static void Reassert(IModContext context, string reason)
        {
            Guard("Reassert(" + reason + ")", () =>
            {
                _state = context?.State ?? _state;
                InventorySpace.ReassertAll(_state, reason);
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
