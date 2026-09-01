using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigPack
{
    /// <summary>
    /// Removes the cost of carrying a full pack.
    ///
    /// Weight in Quasimorph never blocks a pickup - there is no capacity check anywhere
    /// on this path. It is purely a penalty, and every consumer of it reads the same
    /// method, <c>CreatureData.GetItemsWeight</c>:
    ///
    /// <list type="bullet">
    /// <item>GetDodge / GetWeightAffectDodgeMult</item>
    /// <item>GetItemsWeightSatietyDrain</item>
    /// <item>Player.OnMoved and ProcessKnockback</item>
    /// <item>Player and Monster ProcessMeleeAttackOnEnemy</item>
    /// <item>GetWeightMeleeDamageModifier, GetWeightPhysicalResistBonus</item>
    /// <item>the weight panel, backpack icon, tooltip and prepare-raid screen</item>
    /// </list>
    ///
    /// So one postfix covers the whole system. Note the last two entries in that list
    /// are BONUSES - a heavy load helps melee damage and physical resist - and zeroing
    /// the weight gives those up along with the penalties. That trade is why this is a
    /// config key rather than unconditional.
    ///
    /// The weight readout in the UI will show 0 for your own mercenaries. That is the
    /// same number the game's own formulas are now using, so it is honest rather than
    /// cosmetic.
    /// </summary>
    [HarmonyPatch(typeof(CreatureData), nameof(CreatureData.GetItemsWeight))]
    internal static class ItemsWeightPatch
    {
        private static bool _loggedFirstZero;

        [HarmonyPostfix]
        internal static void Postfix(CreatureData __instance, ref float __result)
        {
            if (!ModConfig.Enabled || !ModConfig.RemoveWeight || __result == 0f)
            {
                return;
            }

            try
            {
                if (!PlayerInventories.Owns(BigPackMod.GameState, __instance))
                {
                    return;
                }

                // Deliberately no logging on the success path: this runs on movement,
                // dodge and every melee swing, and a line per call would bury the log.
                if (!_loggedFirstZero)
                {
                    _loggedFirstZero = true;
                    ModLog.Info("weight neutralised (first was " + __result + ")");
                }

                __result = 0f;
            }
            catch (Exception error)
            {
                ModLog.Error("GetItemsWeight postfix failed", error);
            }
        }
    }
}
