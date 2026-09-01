using System;
using System.IO;
using System.Reflection;
using MGSC;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Big Pack, Floor Loot and Ruthless mods:
    /// a Workshop mod black-screened this game on startup by applying a Harmony postfix
    /// to <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely to cache
    /// the <c>State</c> in a static field. That is never necessary - every hook is
    /// handed an <see cref="IModContext"/> that already carries it. Nothing in this mod
    /// may patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on the
    /// bootstrap path.
    ///
    /// This mod goes further than its siblings and applies <b>no Harmony patches at
    /// all</b>. Every layer is a plain write through a public API from inside a hook,
    /// which is why there is no patch-verification step here: there is nothing to
    /// verify that the build-time reference check has not already resolved.
    /// </summary>
    public static class RetinueMod
    {
        private static string _modDirectory;
        private static int _lastSweptTurn = -1;

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
                    ModLog.Info("disabled by config; no squad, no ally changes, nothing touched");
                    return;
                }

                ConflictCheck.Run();

                if (ModConfig.Probe)
                {
                    DataProbe.Dump(_modDirectory);
                }

                // Capture the vanilla gift lists before anything is written, so the
                // recruiting layer always has somewhere honest to restore to. The
                // writes themselves wait for Sync, which is the first moment the
                // active difficulty is knowable.
                Recruiting.CaptureVanilla();
            });
        }

        /// <summary>
        /// Every moment a run's difficulty is freshly knowable. The one layer that
        /// touches shared config records is switched on here and, just as importantly,
        /// switched off again when the run in front of us is one the player excluded.
        /// </summary>
        [Hook(ModHookType.AfterSaveLoaded)]
        public static void OnAfterSaveLoaded(IModContext context) => Sync(context, "save loaded");

        [Hook(ModHookType.SpaceStarted)]
        public static void OnSpaceStarted(IModContext context) => Sync(context, "space");

        /// <summary>
        /// A floor has been entered - a new raid, the next floor down, or a save
        /// reloaded mid-raid. All three land here, and all three are handled by the
        /// same top-up-and-strengthen pass.
        /// </summary>
        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            Sync(context, "dungeon");

            Guard("DungeonStarted", () =>
            {
                var state = context?.State;
                if (!AllyIdentity.ShouldRun(state))
                {
                    return;
                }

                _lastSweptTurn = -1;
                PlayerRole.Sync(state);
                RetinueSquad.TopUp(state);
                AllyPower.Sweep(state);
            });
        }

        /// <summary>
        /// Catches allies that appear part-way through a floor: one bribed with a gift,
        /// one converted by a perk, a summon, or one a quest handed over. They are as
        /// much your squad as the retinue is, and get the same strength.
        ///
        /// This hook runs every frame, so the first thing it does is decide not to. A
        /// creature can only change sides on a turn boundary, so the turn number is
        /// both the cheapest possible guard and the correct one.
        /// </summary>
        [Hook(ModHookType.DungeonUpdateAfterGameLoop)]
        public static void OnDungeonUpdate(IModContext context)
        {
            // Cheapest possible early-out first: this runs on every frame, and two
            // static bool reads must not become a try/catch on every frame.
            if (!ModConfig.Enabled || !ModConfig.AllyPower)
            {
                return;
            }

            Guard("DungeonUpdate", () =>
            {
                var state = context?.State;
                var raidMetadata = state?.Get<RaidMetadata>();
                if (raidMetadata == null || raidMetadata.TurnNumber == _lastSweptTurn)
                {
                    return;
                }
                _lastSweptTurn = raidMetadata.TurnNumber;

                if (AllyIdentity.ShouldRun(state))
                {
                    AllyPower.Sweep(state);
                }
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

                if (AllyIdentity.ShouldRun(context?.State))
                {
                    Recruiting.Apply();
                }
                else
                {
                    // AI presets are shared global state. A run the player excluded
                    // must get the vanilla lists back without restarting the game.
                    Recruiting.Restore();
                }
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
