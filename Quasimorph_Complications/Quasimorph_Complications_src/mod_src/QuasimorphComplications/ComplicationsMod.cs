using System;
using System.IO;
using System.Reflection;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling mods: a Workshop mod black-screened this
    /// game on startup by applying a Harmony postfix to <c>MGSC.State.Resolve</c>, the
    /// dependency injection resolver, purely to cache the <c>State</c> in a static
    /// field. That is never necessary - every hook is handed an <see cref="IModContext"/>
    /// that already carries it.
    ///
    /// <b>This mod applies no Harmony patches at all</b>, like the sibling Retinue mod
    /// and unlike Signals, Nemesis and Silence. That is not restraint for its own sake -
    /// it turned out that every effect a complication needs is already public API:
    /// <c>SpawnSystem.SpawnFixedGroup</c> for arrivals, <c>FireController.AddFire</c>
    /// for the fire, <c>ItemOnFloorSystem.SpawnItem</c> for the cache, and
    /// <c>Data.Global</c> for the noise radii. There is therefore no patch-verification
    /// step here: there is nothing to verify that the build-time reference check has not
    /// already resolved.
    /// </summary>
    public static class ComplicationsMod
    {
        private static string _modDirectory;
        private static int _lastTurn = -1;
        private static bool _difficultyReadFailureLogged;

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
                    ModLog.Info("disabled by config; nothing touched");
                    return;
                }

                ConflictCheck.Run();

                if (ModConfig.Probe)
                {
                    EventProbe.Dump(_modDirectory);
                }
            });
        }

        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            Guard("DungeonStarted", () =>
            {
                _lastTurn = -1;
                Announce.Reset();

                if (!ShouldRun(context?.State))
                {
                    return;
                }

                Scheduler.OnFloorStart(context?.State);
            });
        }

        /// <summary>
        /// The floor is over - the elevator, the exit, or a death. Anything a
        /// complication changed about shared state is given back here.
        /// </summary>
        [Hook(ModHookType.DungeonFinished)]
        public static void OnDungeonFinished(IModContext context)
        {
            Guard("DungeonFinished", () => Scheduler.OnFloorEnd(context?.State));
        }

        /// <summary>
        /// Runs every frame, so the first thing it does is decide not to. A complication
        /// only ever acts on a turn boundary, so the turn number is both the cheapest
        /// guard and the correct one.
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
                Announce.Tick();

                if (Scheduler.Active == null)
                {
                    return;
                }

                var state = context?.State;
                var raidMetadata = state?.Get<RaidMetadata>();
                if (raidMetadata == null || raidMetadata.TurnNumber == _lastTurn)
                {
                    return;
                }
                _lastTurn = raidMetadata.TurnNumber;

                Scheduler.OnTurn(state, raidMetadata.TurnNumber);
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
