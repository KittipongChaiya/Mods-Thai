using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphStride
{
    /// <summary>
    /// The whole mod, in three postfixes.
    ///
    /// <b>What the game actually does.</b> The Run stance does not fail to interact by
    /// accident; it is forbidden from interacting by three checks in
    /// <c>PlayerInteractionSystem</c>, all written the same way:
    ///
    /// <code>
    /// if (player.MovementState == CreatureMovementState.Run
    ///     &amp;&amp; !PerkSystem.GetPerkParameterBool(player.CreatureData, "BRunActions"))
    ///     return false;
    /// </code>
    ///
    /// <c>BRunActions</c> is a real vanilla perk parameter - <c>ParameterNames</c> names
    /// it <c>PARAM_RUN_ACTIONS</c> - so "can act while running" is a state the game
    /// already models and already hands out as a perk reward. This mod grants that
    /// state, per config. It is not inventing a behaviour, and there is nothing here
    /// the game cannot already produce on its own.
    ///
    /// <b>Why postfixes and not a patch on the perk lookup.</b> A single postfix on
    /// <c>PerkSystem.GetPerkParameterBool</c> returning true for <c>BRunActions</c>
    /// would cover all three gates in one line. It is rejected for two reasons: that
    /// method takes a <c>CreatureData</c>, so it would silently grant the same thing to
    /// every ally and enemy the game asks about, and it is called for every perk
    /// parameter in the game rather than only these three. Three narrow postfixes on
    /// the exact decision points are larger to read and smaller in effect, which is the
    /// right trade for a mod that only wants to change a rule about doors.
    ///
    /// <b>What none of this touches.</b> Action points. Interacting still ends the turn
    /// through <c>EndPlayerTurn(MapObstacleInteraction)</c> exactly as it does at
    /// walking pace, because <c>Player.FreeInteractObstacles</c> and
    /// <c>Player.FreeInventoryUse</c> are both <c>MovementState == Slow</c> and neither
    /// is patched. Free actions remain the Slow stance's alone.
    /// </summary>
    internal static class RunGate
    {
        /// <summary>
        /// True when the Run stance is the reason the player was just refused - that
        /// is, when the player is running and does not already hold the perk that
        /// lifts the restriction. If the perk is held the gate never fired, so there
        /// is nothing here to grant.
        /// </summary>
        internal static bool WasTheReason(Player player)
        {
            if (player == null || player.MovementState != CreatureMovementState.Run)
            {
                return false;
            }

            // Walks the creature's perk list, which belongs to the game. On any failure
            // the answer is no: this mod only ever widens a refusal, so denying is
            // identical to not being installed.
            try
            {
                return !PerkSystem.GetPerkParameterBool(player.CreatureData,
                                                        ParameterNames.PARAM_RUN_ACTIONS);
            }
            catch (Exception error)
            {
                Safety.Report("PerkSystem.GetPerkParameterBool", error);
                return false;
            }
        }
    }

    /// <summary>
    /// Doors, containers, elevators, ladders and every other interactive object.
    ///
    /// Both ways of opening a door run through here, which is why one patch covers
    /// them both: clicking a door directly reaches <c>InteractObstacle</c>, and running
    /// to somewhere that happens to have a door in the way reaches
    /// <c>MovePlayer</c>'s <c>ClosedDoor</c> branch. In vanilla the second case is the
    /// worse one - the refusal clears the whole command queue, so the rest of the move
    /// is thrown away and the character stops in the doorway.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.CanInteractObstacles))]
    internal static class CanInteractObstaclesPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Creatures creatures, Scenarios scenarios,
                                   MapObstacle contextObstacle,
                                   ref LimitsTooltip.LimitType limitType,
                                   ref bool __result)
        {
            if (__result)
            {
                return;
            }

            // Vanilla sets RunNoObstacles before its first check and overwrites it
            // before either of the others returns, so on a refusal this is an exact
            // test for "the Run stance was the reason" and not a guess.
            if (limitType != LimitsTooltip.LimitType.RunNoObstacles)
            {
                return;
            }

            // Reached with a null obstacle from MovePlayer, when a cell is flagged as a
            // closed door but no door object is found there. That is a state we cannot
            // classify, so vanilla's answer stands.
            if (contextObstacle == null)
            {
                return;
            }

            var player = creatures?.Player;
            if (!RunGate.WasTheReason(player) || !Allows(contextObstacle))
            {
                return;
            }

            // Vanilla short-circuited on the Run check and never evaluated the two
            // refusals below it. Lifting the first one means taking responsibility for
            // running the rest ourselves - otherwise this mod would quietly grant a
            // Baron mutation the elevator, or a tutorial the object it is withholding.
            if (player.MutatedQuasimorph &&
                (contextObstacle.Elevator != null || contextObstacle.Ladder != null))
            {
                limitType = LimitsTooltip.LimitType.MutatedToBaron;
                return;
            }

            // A virtual call into whichever scenario is running - vanilla's own code,
            // a tutorial, or a scenario shipped by another mod. It is the one thing
            // this postfix invokes that it does not own, so it is the one thing
            // contained. Refusing on failure is the safe direction: it leaves the
            // player exactly where the unmodded game left them.
            try
            {
                var scenario = scenarios?.First<BaseDungeonScenario>();
                if (scenario != null && !scenario.CanInteractObstacles(contextObstacle))
                {
                    limitType = LimitsTooltip.LimitType.TutorialNotAllowed;
                    return;
                }
            }
            catch (Exception error)
            {
                Safety.Report("BaseDungeonScenario.CanInteractObstacles", error);
                return;
            }

            __result = true;
        }

        /// <summary>
        /// Which config key governs this object. The order matters: a corpse is a
        /// container as far as the game is concerned, but searching one is how you pick
        /// things up off a body, so it answers to the loot key rather than the scenery
        /// key.
        /// </summary>
        private static bool Allows(MapObstacle obstacle)
        {
            if (obstacle.Door != null)
            {
                return ModConfig.OpenDoors;
            }
            if (obstacle.Elevator != null || obstacle.Ladder != null ||
                obstacle.Dislocator != null)
            {
                return ModConfig.UseElevators;
            }
            if (obstacle.CorpseStorage != null)
            {
                return ModConfig.TakeItems;
            }
            return ModConfig.UseContainers;
        }
    }

    /// <summary>
    /// Floor loot, corpses, vest slots, the inventory screen and the healing screen -
    /// all five behind one check with no argument saying which of them is asking.
    ///
    /// So the grant is scoped instead of unconditional: <see cref="PickupScope"/> opens
    /// a window around the specific actions that were asked for and closes it again
    /// immediately, and this postfix only grants inside that window. Sprinting
    /// therefore does not become a free pass to the whole inventory unless
    /// <c>run_full_inventory</c> asks for exactly that.
    ///
    /// Unlike the obstacle gate this one needs no follow-up checks: the Run stance is
    /// the only thing vanilla refuses for here, so there is nothing underneath it that
    /// lifting it could skip.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.CanUseInventory))]
    internal static class CanUseInventoryPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Creatures creatures, ref bool __result)
        {
            if (__result || !(ModConfig.FullInventory || PickupScope.IsOpen))
            {
                return;
            }

            if (RunGate.WasTheReason(creatures?.Player))
            {
                __result = true;
            }
        }
    }

    /// <summary>
    /// An ally's inventory and wound fixation.
    ///
    /// Vanilla refuses here for three separate reasons and only one of them is ours.
    /// The limit type separates them: the adjacency and follower checks leave it at
    /// <c>NoInventory</c>, and only the Run check writes <c>RunNoObstacles</c>. An ally
    /// across the room stays out of reach.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.CanOpenAllyInventory))]
    internal static class CanOpenAllyInventoryPatch
    {
        [HarmonyPostfix]
        public static void Postfix(Creatures creatures,
                                   ref LimitsTooltip.LimitType limitType,
                                   ref bool __result)
        {
            if (__result || !ModConfig.AllyInventory)
            {
                return;
            }

            if (limitType != LimitsTooltip.LimitType.RunNoObstacles)
            {
                return;
            }

            var player = creatures?.Player;
            if (!RunGate.WasTheReason(player))
            {
                return;
            }

            // As above: vanilla never reached its own last check, so we run it.
            if (player.ChangedMercenary)
            {
                limitType = LimitsTooltip.LimitType.ChangedMercenary;
                return;
            }

            __result = true;
        }
    }
}
