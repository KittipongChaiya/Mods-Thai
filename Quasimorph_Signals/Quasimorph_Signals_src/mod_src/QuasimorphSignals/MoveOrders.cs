using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 4 - "go there", to any cell on the floor, seen or unseen.
    ///
    /// <b>This needs no new AI state and no patch.</b> The game already has a state
    /// whose entire job is walking to a cell you cannot see: <c>Investigate</c>, the
    /// one a creature enters when it hears a noise through a wall. Every piece of it
    /// is public:
    ///
    /// <code>
    /// Behaviour.TryGetState&lt;Investigate&gt;(out var go);   // public
    /// go.SetInvestigateCell(cell);                            // public
    /// Behaviour.SetState&lt;Investigate&gt;("...");            // public, forced
    /// </code>
    ///
    /// It paths with <c>GetNextPosToTarget</c> + <c>MoveToPosition</c>, the AI's own
    /// pathfinder, which has never consulted the player's line of sight - it cannot,
    /// because the creatures using it are not the player. So "even out of sight" is
    /// not a feature that had to be built here. It is what this state already is.
    ///
    /// <b>Why the order does not time out on a long walk.</b> There is an
    /// <c>InvestigationAITimer</c>, and at first glance it looks like it would expire
    /// half way across a map. It does not: its <c>ProcessAfterState</c> only
    /// decrements while <c>IsAtInterestPosition || CantMove</c>. The clock runs when
    /// the creature has arrived and is looking around, or when it is stuck - never
    /// while it is walking. A move order therefore survives any distance, and ends
    /// shortly after arrival, which is exactly the shape an order should have.
    ///
    /// <b>Endless hunt has to be off.</b> <c>Investigate</c> carries a transition to
    /// <c>Attack</c> named "Endless Hunt" that fires immediately when the creature is
    /// hunting. An ally sent somewhere while roaming would leave for the nearest enemy
    /// on the same turn, so issuing a move order clears the roam flag first.
    /// </summary>
    internal static class MoveOrders
    {
        /// <summary>
        /// How many consecutive turns an ally may report itself unable to move before
        /// the order is abandoned. Without this a destination behind a locked door
        /// would be re-issued forever and the ally would stutter against it for the
        /// rest of the raid.
        /// </summary>
        private const int GiveUpAfterStuckTurns = 4;

        private sealed class Order
        {
            internal CellPosition Cell;
            internal bool Arrived;
            internal int StuckTurns;
        }

        /// <summary>
        /// Keyed by <c>CreatureData.UniqueId</c>, like every other order in this mod:
        /// stable across saves, loads and floors, unlike the creature object.
        /// </summary>
        private static readonly Dictionary<int, Order> Standing = new Dictionary<int, Order>();

        internal static bool Has(Creature ally)
        {
            var id = AllyTest.IdOf(ally);
            return id != 0 && Standing.ContainsKey(id);
        }

        internal static bool TryGetDestination(Creature ally, out CellPosition cell)
        {
            cell = default;
            var id = AllyTest.IdOf(ally);
            if (id == 0 || !Standing.TryGetValue(id, out var order))
            {
                return false;
            }
            cell = order.Cell;
            return true;
        }

        /// <summary>
        /// Drops a standing move order. Called when the player gives the ally a
        /// different order instead - an explicit new instruction always wins over an
        /// old one, rather than the two fighting each other every turn.
        /// </summary>
        internal static void Clear(Creature ally)
        {
            var id = AllyTest.IdOf(ally);
            if (id != 0)
            {
                Standing.Remove(id);
            }
        }

        /// <summary>
        /// Sends an ally to a cell. Returns false, having changed nothing, when the
        /// destination is not somewhere a creature could stand.
        /// </summary>
        internal static bool Give(Creature creature, CellPosition cell, MapGrid mapGrid)
        {
            if (!AllyTest.IsAlly(creature))
            {
                return false;
            }

            if (!IsReachableLooking(cell, mapGrid))
            {
                ModLog.Info("move order refused: " + cell + " is not a cell a creature can stand on");
                return false;
            }

            var id = AllyTest.IdOf(creature);
            if (id == 0)
            {
                return false;
            }

            Standing[id] = new Order { Cell = cell };
            Send((Monster)creature, cell);
            ModLog.Info("ally " + id + " ordered to " + cell);
            return true;
        }

        /// <summary>
        /// Keeps a standing order in force, once per turn.
        ///
        /// The rules are a whitelist rather than a blacklist, deliberately. Re-issuing
        /// is only ever done from a state where the ally is plainly idling or
        /// following; anything else - a fight, a panic, a surrender, picking up a
        /// weapon - is left completely alone, and the order resumes when that finishes.
        /// An ally that is shot at on the way to a destination should shoot back, not
        /// walk on obediently.
        /// </summary>
        internal static void Enforce(Monster ally, Creatures creatures)
        {
            var id = AllyTest.IdOf(ally);
            if (id == 0 || !Standing.TryGetValue(id, out var order) || ally.Behaviour == null)
            {
                return;
            }

            var state = ally.Behaviour.CurrentState;
            var atDestination = ally.CreatureData.Position.Equals(order.Cell);

            if (atDestination)
            {
                if (!order.Arrived)
                {
                    order.Arrived = true;
                    ModLog.Info("ally " + id + " reached " + order.Cell + " and is holding there");
                }
                order.StuckTurns = 0;

                // Holding is not a state of its own: the ally simply stops being told
                // to walk. It will still fight, and the game may eventually try to
                // wander it off with IdleMigrate - the drift check below is what
                // brings it back.
                return;
            }

            if (state is Investigate investigate)
            {
                // Already walking. The one thing worth watching is whether it has
                // given up: CantMove means the pathfinder found nothing this turn.
                if (investigate.CantMove)
                {
                    order.StuckTurns++;
                    if (order.StuckTurns >= GiveUpAfterStuckTurns)
                    {
                        Standing.Remove(id);
                        ModLog.Warn("ally " + id + " could not reach " + order.Cell +
                                    " after " + GiveUpAfterStuckTurns + " turns; order dropped");
                    }
                }
                else
                {
                    order.StuckTurns = 0;
                }
                return;
            }

            // Not walking, and not where it was sent. Either it drifted after arriving,
            // or a noise pulled its investigate cell somewhere else, or it finished a
            // fight. Re-issue - but only from a state where doing so is safe.
            if (IsInterruptible(state))
            {
                Send(ally, order.Cell);
            }
        }

        /// <summary>
        /// States an ally may be pulled out of to resume a move order. Everything not
        /// listed here - fighting, panicking, surrendering, fetching a weapon, asleep,
        /// eccolapsing - keeps the ally, and the order simply waits.
        /// </summary>
        private static bool IsInterruptible(AIState state)
        {
            return state is Idle || state is IdleFollow || state is IdleMigrate ||
                   state is FollowTarget || state is Stay;
        }

        private static void Send(Monster ally, CellPosition cell)
        {
            try
            {
                var behaviour = ally?.Behaviour;
                if (behaviour == null)
                {
                    return;
                }

                // Investigate has a transition to Attack called "Endless Hunt" that
                // fires the moment a hunting creature enters it. A roaming ally sent
                // across the map would leave for the nearest enemy on the same turn.
                behaviour.SetEndlessHunt(value: false, force: true);

                if (!behaviour.TryGetState<Investigate>(out var investigate))
                {
                    ModLog.Warn("ally " + AllyTest.IdOf(ally) + " has no Investigate state; " +
                                "it cannot be given a destination");
                    return;
                }

                investigate.SetInvestigateCell(cell);
                behaviour.SetState<Investigate>("QuasimorphSignals move order");
            }
            catch (Exception error)
            {
                ModLog.Error("could not send ally " + AllyTest.IdOf(ally) + " to " + cell, error);
            }
        }

        /// <summary>
        /// A cheap sanity check on the destination, not a pathfinding query. Whether a
        /// route exists is the pathfinder's business and is answered every turn by
        /// <c>CantMove</c>; this only rejects the obviously impossible - a wall, a
        /// closed door, the void outside the map - so that a misclick fails loudly at
        /// the moment it happens rather than silently four turns later.
        /// </summary>
        private static bool IsReachableLooking(CellPosition cell, MapGrid mapGrid)
        {
            if (mapGrid == null)
            {
                return false;
            }

            var target = mapGrid.GetCell(cell, checkBorders: false);
            return target != null && target.IsFloor && !target.IsObjBlockPass;
        }

        /// <summary>
        /// Forgets orders for allies that are no longer around. Called on floor change,
        /// where the whole creature list is replaced - unlike a turn boundary, where an
        /// ally riding an elevator is briefly absent and forgetting would be wrong.
        /// </summary>
        internal static void Prune(Creatures creatures)
        {
            if (creatures?.Monsters == null || Standing.Count == 0)
            {
                return;
            }

            var alive = new HashSet<int>();
            foreach (var creature in creatures.Monsters)
            {
                var id = AllyTest.IdOf(creature);
                if (id != 0)
                {
                    alive.Add(id);
                }
            }

            var stale = new List<int>();
            foreach (var entry in Standing)
            {
                if (!alive.Contains(entry.Key))
                {
                    stale.Add(entry.Key);
                }
            }

            foreach (var id in stale)
            {
                Standing.Remove(id);
            }

            if (stale.Count > 0)
            {
                ModLog.Info("cleared " + stale.Count + " move order(s) for allies no longer on this floor");
            }
        }
    }
}
