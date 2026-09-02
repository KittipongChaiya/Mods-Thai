using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphSilence
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Retinue, Signals, Nemesis, Big Pack and
    /// Ruthless mods: a Workshop mod black-screened this game on startup by applying a
    /// Harmony postfix to <c>MGSC.State.Resolve</c>, the dependency injection resolver,
    /// purely to cache the <c>State</c> in a static field. That is never necessary -
    /// every hook is handed an <see cref="IModContext"/> that already carries it.
    /// Nothing here may patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything
    /// else on the bootstrap path.
    /// </summary>
    public static class SilenceMod
    {
        private static string _modDirectory;

        /// <summary>The turn number, so the readout knows when to go quiet again.</summary>
        internal static int CurrentTurn { get; private set; }

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
        /// Applies the global radius overrides, and reports what the game's own values
        /// are - which nothing else in the game or its mods has ever told anybody.
        /// </summary>
        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            Guard("DungeonStarted", () =>
            {
                if (!ModConfig.Enabled)
                {
                    return;
                }

                CurrentTurn = 0;
                NoiseReadout.Reset();
                ApplyGlobalRadii();
            });
        }

        /// <summary>
        /// Runs every frame, so the first thing it does is decide not to.
        /// </summary>
        [Hook(ModHookType.DungeonUpdateAfterGameLoop)]
        public static void OnDungeonUpdate(IModContext context)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            Guard("DungeonUpdate", () =>
            {
                var state = context?.State;
                var raidMetadata = state?.Get<RaidMetadata>();
                if (raidMetadata != null)
                {
                    CurrentTurn = raidMetadata.TurnNumber;
                }

                Distraction.Update(state);
                NoiseReadout.Refresh(state);
            });
        }

        /// <summary>
        /// The three global noise radii are ordinary public properties on
        /// <c>GlobalSettings</c>, so this needs no patch at all.
        ///
        /// <b>They are global in the true sense</b> - every creature reads them,
        /// including every enemy. That is why the default for all three is "leave the
        /// game's own value alone", and why the config says so in as many words rather
        /// than presenting them as a stealth setting.
        /// </summary>
        private static void ApplyGlobalRadii()
        {
            try
            {
                var settings = Data.Global;
                if (settings == null)
                {
                    ModLog.Warn("GlobalSettings is not available; radii left as they are");
                    return;
                }

                ModLog.Info("vanilla noise radii - step " + settings.NoiseStepRadius +
                            ", door " + settings.NoiseDoorRadius +
                            ", death " + settings.NoiseDeathRadius);

                if (ModConfig.StepRadiusOverride >= 0)
                {
                    settings.NoiseStepRadius = ModConfig.StepRadiusOverride;
                }
                if (ModConfig.DoorRadiusOverride >= 0)
                {
                    settings.NoiseDoorRadius = ModConfig.DoorRadiusOverride;
                }
                if (ModConfig.DeathRadiusOverride >= 0)
                {
                    settings.NoiseDeathRadius = ModConfig.DeathRadiusOverride;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not read or set the global noise radii", error);
            }
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
