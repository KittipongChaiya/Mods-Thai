using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigStack
{
    /// <summary>
    /// Keeps a raised stack ceiling from also handing out full stacks.
    ///
    /// The bug this fixes: purchased ammunition and mission-reward ammunition arrived at
    /// 9999. The cause is one line of vanilla code —
    ///
    /// <code>
    ///     public StackableItemComponent(short max) { Max = max; Count = max; }
    /// </code>
    ///
    /// — every stackable item is created *full*. Vanilla gets away with it because "full"
    /// is a sane number; with the ceiling raised, "full" is 9999. `ItemFactory.CreateComponent`
    /// then overwrites `Count` with a sensible random amount **only** for `AmmoRecord`,
    /// and **only** when its `randomizeConditionAndCapacity` flag is set. Both routes the
    /// player complained about call `CreateForInventory(id, false, false)` — mission
    /// rewards through `MissionSystem.AddReward`, station stock through
    /// `TradeSystem.GetRandomItemsFromStation` — so the randomisation never runs and the
    /// 9999 survives.
    ///
    /// The fix restores vanilla's *quantity* while keeping the raised *capacity*: clamp
    /// the freshly constructed count back to what the game would have used, and leave
    /// `Max` alone. An item created this way holds a normal amount of ammunition in a
    /// container that can now accept 9999.
    ///
    /// This is the constructor of a plain data component, not a bootstrap-path type, and
    /// the postfix only ever lowers a number that was set a few instructions earlier.
    /// </summary>
    [HarmonyPatch(typeof(StackableItemComponent), MethodType.Constructor, new[] { typeof(short) })]
    internal static class InitialCountPatch
    {
        private static bool _loggedFirst;

        [HarmonyPostfix]
        internal static void Postfix(StackableItemComponent __instance)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                // Only meaningful directly after our own GetMaxStackSize override. The
                // flag is single-use: a component built without that call ahead of it
                // (deserialisation, or game code we have not seen) is left untouched.
                if (!GetMaxStackSizePatch.HasLastVanillaMax)
                {
                    return;
                }

                var vanilla = GetMaxStackSizePatch.LastVanillaMax;
                GetMaxStackSizePatch.HasLastVanillaMax = false;

                if (__instance == null || vanilla <= 0 || __instance.Count <= vanilla)
                {
                    return;
                }

                if (!_loggedFirst)
                {
                    _loggedFirst = true;
                    ModLog.Info("initial stack count clamped from " + __instance.Count +
                                " to the vanilla " + vanilla + "; capacity stays " +
                                __instance.Max);
                }

                __instance.Count = vanilla;
            }
            catch (Exception error)
            {
                ModLog.Error("StackableItemComponent constructor postfix failed", error);
            }
        }
    }
}
