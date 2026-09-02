using System;
using System.IO;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Entry point. Every method tagged with <see cref="Hook"/> is discovered by
    /// <c>MGSC.UserModSystem.GrabMethods</c> and must be public and static.
    ///
    /// Design note, inherited from the sibling Retinue, Big Pack and Ruthless mods: a
    /// Workshop mod black-screened this game on startup by applying a Harmony postfix
    /// to <c>MGSC.State.Resolve</c>, the dependency injection resolver, purely to
    /// cache the <c>State</c> in a static field. That is never necessary - every hook
    /// is handed an <see cref="IModContext"/> that already carries it. Nothing here
    /// may patch <c>State</c>, <c>GameLoop</c>, <c>Data</c>, or anything else on the
    /// bootstrap path. Our targets are one inspect window and one cursor evaluation.
    ///
    /// <b>Why this mod patches at all, when Retinue does not.</b> Retinue guarantees
    /// it applies no Harmony patches, and keeping that guarantee true is exactly why
    /// this is a separate mod rather than a Retinue feature. The behaviour half of
    /// roaming needs no patches - see <see cref="AllyOrders"/>. The button and the
    /// out-of-sight layer do, because every member involved is private. Splitting them
    /// means you can uninstall this and keep a patch-free Retinue, in the same way Big
    /// Stack and Big Pack are separable.
    /// </summary>
    public static class SignalsMod
    {
        private static string _modDirectory;
        private static int _lastSweptTurn = -1;
        private static Creatures _creatures;

        /// <summary>
        /// The current creature list, captured from a hook context - never from a
        /// patch, and never by patching the resolver. The UI layer needs the player
        /// object to hand to <c>StartFollowing</c>, and a click arrives with no state
        /// of its own.
        /// </summary>
        internal static Creatures Creatures => _creatures;

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

                // PatchAll is allowed to throw here: UserModSystem.InvokeHook catches
                // it, logs it, and the game carries on without us - the correct
                // failure mode for a mod that only adds a convenience.
                new Harmony(ModInfo.HarmonyId).PatchAll(Assembly.GetExecutingAssembly());

                // Retinue needs no verification step because it patches nothing. This
                // mod resolves private members by name, which tools/apicheck.py
                // structurally cannot check - a string is not a member reference - so
                // the check has to happen here, at runtime, and be loud.
                PatchVerify.Run(_modDirectory);
            });
        }

        /// <summary>
        /// A floor has been entered - a new raid, the next floor down, or a save
        /// reloaded mid-raid. Standing orders survive all three, because they are keyed
        /// by <c>CreatureData.UniqueId</c> rather than by the creature object.
        /// </summary>
        [Hook(ModHookType.DungeonStarted)]
        public static void OnDungeonStarted(IModContext context)
        {
            Guard("DungeonStarted", () =>
            {
                _lastSweptTurn = -1;
                if (!ModConfig.Enabled)
                {
                    return;
                }
                _creatures = context?.State?.Get<Creatures>() ?? _creatures;
                AllyOrders.Sweep(context?.State);
            });
        }

        /// <summary>
        /// Re-asserts standing orders once per turn, so an ally that the AI pulled back
        /// into following goes back to roaming.
        ///
        /// This hook runs every frame, so the first thing it does is decide not to. A
        /// creature can only change state on a turn boundary, so the turn number is
        /// both the cheapest possible guard and the correct one.
        /// </summary>
        [Hook(ModHookType.DungeonUpdateAfterGameLoop)]
        public static void OnDungeonUpdate(IModContext context)
        {
            // Cheapest possible early-out first: this runs on every frame, and a
            // static bool read must not become a try/catch on every frame.
            if (!ModConfig.Enabled)
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

                _creatures = state?.Get<Creatures>() ?? _creatures;
                AllyOrders.Sweep(state);
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
