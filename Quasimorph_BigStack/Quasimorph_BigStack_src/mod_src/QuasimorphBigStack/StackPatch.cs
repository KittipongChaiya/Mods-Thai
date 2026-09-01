using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigStack
{
    /// <summary>
    /// Raises the maximum stack size of every stackable item.
    ///
    /// <c>ItemFactory.GetMaxStackSize</c> is the single funnel from config data to an
    /// item's real limit — seven callers reach it, covering item creation, ammo spawning,
    /// disassembly, phantom cloning, both production windows, and the stack-fixing pass
    /// the game runs on the ship. Overriding it therefore covers all eleven record types
    /// implementing <c>IStackableRecord</c> without touching any of them.
    ///
    /// Why not simply set <c>MaxStack = 9999</c> on the records at AfterConfigsLoaded,
    /// which would need no Harmony at all? Because the vanilla method multiplies by the
    /// difficulty preset's stack option before returning:
    ///
    /// <code>
    ///     case X4: return (short)(stackable.MaxStack * 4);   // conv.i2
    /// </code>
    ///
    /// 9999 * 4 is 39996, which does not fit in a short and wraps to -25540 — a negative
    /// maximum stack on every item, for anyone playing that difficulty option. Returning
    /// the final value from a postfix never enters the multiply.
    ///
    /// This patch cannot be confined to the player. The method takes a record, not an
    /// inventory, so there is no owner in scope: shop stock, floor loot and enemy
    /// inventories get the same ceiling. That is inherent to a record-level limit.
    /// </summary>
    [HarmonyPatch(typeof(ItemFactory), nameof(ItemFactory.GetMaxStackSize))]
    internal static class GetMaxStackSizePatch
    {
        private static bool _loggedFirst;

        /// <summary>
        /// <paramref name="stackable"/> is matched by name against the game's own
        /// parameter list. A rename in a game update makes Harmony throw at patch time,
        /// which the caller logs — a loud failure, not a silent one.
        /// </summary>
        [HarmonyPostfix]
        internal static void Postfix(IStackableRecord stackable, ref short __result)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                // Vanilla returns 1 for a null record and never consults the difficulty.
                // Leave that alone: it is the "this thing does not stack" answer, not a
                // limit to raise.
                if (stackable == null)
                {
                    return;
                }

                if (!_loggedFirst)
                {
                    _loggedFirst = true;
                    ModLog.Info("stack ceiling: vanilla would have said " + __result +
                                " for the first record asked; overriding to " +
                                ModConfig.MaxStack);
                }

                __result = (short)ModConfig.MaxStack;
            }
            catch (Exception error)
            {
                ModLog.Error("GetMaxStackSize postfix failed", error);
            }
        }
    }
}
