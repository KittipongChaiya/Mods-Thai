using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasimorphStride
{
    /// <summary>
    /// Keeps the movement-stance tooltip honest.
    ///
    /// Vanilla's Run tooltip ends with a red line reading
    /// <c>tooltip.InventoryAndActionsForbidden</c>. Once this mod is installed that
    /// line is false, and a tooltip that lies about the rules is worse than no tooltip
    /// at all - the player reads it, believes it, and never tries the door.
    ///
    /// The vanilla line cannot be removed from a postfix without reaching into the
    /// factory's panel pool, so a green correction is appended underneath it instead,
    /// naming exactly what the current config permits. It is generated from the same
    /// booleans the patches read, so it cannot drift out of step with them.
    ///
    /// <b>Written directly, not localized.</b> <c>Localization.Get</c> is the single
    /// most contested method across the installed mod set - seven mods on this machine
    /// reach for it - and there is no vanilla key that says this anyway. The sibling
    /// Signals mod took the same decision for the same reason.
    /// </summary>
    [HarmonyPatch(typeof(TooltipFactory), nameof(TooltipFactory.BuildMovementStateTooltip))]
    internal static class MovementStateTooltipPatch
    {
        private const string Icon = "common_hand";

        [HarmonyPostfix]
        public static void Postfix(TooltipFactory __instance, CreatureMovementState movementState)
        {
            if (!ModConfig.FixTooltip || movementState != CreatureMovementState.Run)
            {
                return;
            }

            var permitted = Permitted();
            if (permitted.Count == 0)
            {
                return;
            }

            // Everything below reaches into the tooltip factory's panel pool and the
            // text layout underneath it - real UI work, none of it ours. This whole
            // patch is cosmetic: a line of explanatory text is never worth interrupting
            // the game for, so a failure here costs the player the line and nothing
            // else.
            try
            {
                __instance.AddPanelToTooltip()
                          .SetIcon(Icon)
                          .SetName("While running: " + string.Join(", ", permitted))
                          .SetNameColor(Colors.Green);
            }
            catch (Exception error)
            {
                Safety.Report("TooltipFactory.AddPanelToTooltip", error);
            }
        }

        /// <summary>
        /// What the config actually allows, in the player's words rather than the
        /// config file's. Ordered so the two things this mod exists for come first.
        /// </summary>
        private static List<string> Permitted()
        {
            var permitted = new List<string>();

            if (ModConfig.OpenDoors)
            {
                permitted.Add("doors");
            }
            if (ModConfig.TakeItems)
            {
                permitted.Add("loot and corpses");
            }
            if (ModConfig.UseContainers)
            {
                permitted.Add("containers");
            }
            if (ModConfig.UseElevators)
            {
                permitted.Add("elevators");
            }
            if (ModConfig.UseVest)
            {
                permitted.Add("vest");
            }
            if (ModConfig.AllyInventory)
            {
                permitted.Add("ally inventory");
            }
            if (ModConfig.FullInventory)
            {
                permitted.Add("full inventory");
            }

            return permitted;
        }
    }
}
