using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 5 - picking the destination.
    ///
    /// Press <b>Move</b> on an ally's panel, then right-click anywhere on the map. The
    /// panel closes, the mod waits for exactly one right-click, turns it into a cell,
    /// and hands that cell to <see cref="MoveOrders"/>.
    ///
    /// <b>Why a mode rather than a modifier key.</b> A held modifier would have to be
    /// read through Rewired, whose bindings the player can remap and three of the
    /// installed Workshop mods already reach into. One click consumed after an
    /// explicit button press needs none of that, and it cannot fire by accident: there
    /// is no state to be left switched on, because the very next right-click clears it
    /// whatever it hits.
    ///
    /// <b>Why the click is consumed rather than shared.</b> Right-click in this game
    /// already means turn to face, open a door, or open a creature's options menu. A
    /// destination click that also did one of those would be a bug the player could
    /// not explain, so the prefix returns false and vanilla never sees it. That
    /// suppression lasts exactly one click and only while armed.
    /// </summary>
    internal static class MoveTargeting
    {
        private static int _awaitingAllyId;

        internal static bool IsArmed => _awaitingAllyId != 0;

        /// <summary>
        /// Begins waiting for a destination click for one ally, and says so on screen -
        /// there is no cursor change available to a mod, so the notification line is
        /// the only thing telling the player the game is waiting for them.
        /// </summary>
        internal static void Arm(Creature ally)
        {
            if (!AllyTest.IsAlly(ally))
            {
                return;
            }

            _awaitingAllyId = AllyTest.IdOf(ally);
            Notify("Right-click a destination for this ally. Anywhere on the floor, seen or not.");
            ModLog.Info("awaiting a destination click for ally " + _awaitingAllyId);

            // Close the panel so the map is visible and the click is not swallowed by
            // the UI - IsSecondaryCursorActionDown ignores clicks over a UI object.
            try
            {
                UI.Back();
            }
            catch (Exception error)
            {
                ModLog.Warn("could not close the ally panel (" + error.GetType().Name +
                            "); close it yourself before clicking a destination");
            }
        }

        internal static void Disarm()
        {
            _awaitingAllyId = 0;
        }

        /// <summary>
        /// Turns the consumed click into an order. Always disarms, whether or not the
        /// order was accepted: a player who clicked a wall gets a message and their
        /// cursor back, rather than being trapped in a mode they cannot see.
        /// </summary>
        private static void Place(CellPosition cell, MapGrid mapGrid, Creatures creatures)
        {
            var id = _awaitingAllyId;
            Disarm();

            var ally = FindAlly(id, creatures);
            if (ally == null)
            {
                Notify("That ally is no longer here. Order cancelled.");
                ModLog.Info("destination click discarded: ally " + id + " is gone");
                return;
            }

            if (MoveOrders.Give(ally, cell, mapGrid))
            {
                Notify("Ally moving to " + cell.X + ", " + cell.Y + ".");
            }
            else
            {
                Notify("Nothing can stand there. Order cancelled.");
            }
        }

        private static Creature FindAlly(int id, Creatures creatures)
        {
            if (id == 0 || creatures?.Monsters == null)
            {
                return null;
            }

            foreach (var creature in creatures.Monsters)
            {
                if (AllyTest.IdOf(creature) != id || !AllyTest.IsAlly(creature))
                {
                    continue;
                }
                var health = creature.CreatureData?.Health;
                return health != null && health.Alive ? creature : null;
            }
            return null;
        }

        internal static void Notify(string message)
        {
            try
            {
                UI.Staff?.NotificationPanel?.AddNotification(message);
            }
            catch (Exception)
            {
                // Cosmetic. A missing notification panel must never stop an order.
            }
        }

        /// <summary>
        /// Consumes one right-click on the map while a destination is being awaited.
        ///
        /// The patched method is private, so its name is a string and
        /// <c>tools/apicheck.py</c> cannot see it - <see cref="PatchVerify"/> is what
        /// checks it at runtime instead. Harmony binds the three parameters below by
        /// name out of the eight the real method takes.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInteractionSystem), "EvaluateSecondaryCursorAction")]
        internal static class SecondaryCursorPatch
        {
            [HarmonyPrefix]
            internal static bool Prefix(MapRenderer mapRenderer, MapGrid mapGrid,
                                        Creatures creatures, ref bool __result)
            {
                if (!ModConfig.Enabled || !ModConfig.MoveOrders || !IsArmed)
                {
                    return true;     // not our click; vanilla runs untouched
                }

                try
                {
                    var cell = mapRenderer.GetCellUnderCursor();
                    Place(cell, mapGrid, creatures);

                    // Tell the game the click was handled, so it does not also turn the
                    // player to face it or open a door.
                    __result = true;
                    return false;
                }
                catch (Exception error)
                {
                    // Never leave the cursor broken. Disarm, let vanilla have the
                    // click, and record why.
                    Disarm();
                    ModLog.Error("destination click failed; the click was passed to the " +
                                 "game unchanged", error);
                    return true;
                }
            }
        }
    }
}
