using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Retinue, Signals, Big Pack and Ruthless
    /// mods: a Workshop mod black-screened this game on startup by applying a Harmony
    /// postfix to <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely
    /// to cache the <c>State</c> in a static field. That is never necessary - every hook
    /// is handed an <see cref="IModContext"/> that already carries it. Nothing here may
    /// patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on the
    /// bootstrap path.
    /// </summary>
    public static class NemesisMod
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

                ConflictCheck.Run();

                // PatchAll is allowed to throw here: UserModSystem.InvokeHook catches it,
                // logs it, and the game carries on without us - the correct failure mode.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());
                PatchVerify.Run(_modDirectory);
            });
        }

        /// <summary>
        /// The roster has just been read out of the save by
        /// <see cref="DeserializeGlobalComponentsPatch"/>. This is the first moment the
        /// difficulty is knowable, so it is also where a run the player excluded gets
        /// its mob classes taken back off it.
        /// </summary>
        [Hook(ModHookType.AfterSaveLoaded)]
        public static void OnAfterSaveLoaded(IModContext context) => Sync(context, "save loaded");

        [Hook(ModHookType.SpaceStarted)]
        public static void OnSpaceStarted(IModContext context) => Sync(context, "space");

        /// <summary>
        /// A floor has been entered - a new raid, the next floor down, or a save reloaded
        /// mid-raid. Promotion and arrival are both decided here.
        /// </summary>
        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            Guard("DungeonStarted", () =>
            {
                if (!ShouldRun(context?.State))
                {
                    return;
                }

                MobClassInjector.SyncAll();
                Encounters.OnFloorStarted(context?.State);
            });
        }

        private static void Sync(IModContext context, string reason)
        {
            Guard("Sync(" + reason + ")", () =>
            {
                if (!ModConfig.Enabled)
                {
                    return;
                }

                if (ShouldRun(context?.State))
                {
                    MobClassInjector.SyncAll();
                }
                else
                {
                    // Mob classes are shared global state. A run the player excluded must
                    // get a clean table back without restarting the game.
                    MobClassInjector.RemoveAll();
                }
            });
        }

        /// <summary>
        /// Whether the mod should be doing anything at all right now.
        ///
        /// <b>Fails closed.</b> If the difficulty cannot be read and the player asked for
        /// a difficulty restriction, the answer is no and the game stays vanilla - the
        /// same rule the sibling mods use, for the same reason.
        /// </summary>
        private static bool ShouldRun(State state)
        {
            if (!ModConfig.Enabled)
            {
                return false;
            }

            if (ModConfig.OnlyOnDifficulty.Length == 0)
            {
                return true;
            }

            if (state == null)
            {
                return false;
            }

            try
            {
                var preset = state.Get<Difficulty>()?.Preset;
                return preset != null &&
                       string.Equals(preset.Id, ModConfig.OnlyOnDifficulty, StringComparison.Ordinal);
            }
            catch (Exception error)
            {
                if (!_difficultyReadFailureLogged)
                {
                    _difficultyReadFailureLogged = true;
                    ModLog.Error("could not read the active difficulty from State; " +
                                 "only_on_difficulty is set, so the mod stays off and the " +
                                 "game stays vanilla", error);
                }
                return false;
            }
        }

        private static bool _difficultyReadFailureLogged;

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
