using System.Collections.Generic;
using MGSC;

namespace QuasimorphBigStack
{
    /// <summary>
    /// The one way this mod can cost you something.
    ///
    /// Lowering the ceiling — or removing the mod — does not delete anything by itself.
    /// <c>ItemInteractionSystem.FixStacksCount</c> banks whatever is over the new maximum,
    /// tops up other stacks of the same item, and spawns fresh stacks through
    /// <c>ItemFactory.CreateForInventory</c> for the remainder. That is genuinely safe
    /// behaviour, and it is what makes a wind-down possible at all.
    ///
    /// The catch is the last step: each new stack is placed with
    /// <c>ItemStorage.AddItemAndReshuffleOptional</c>, the non-forcing variant. If the
    /// grid has no room, the item is simply not placed. One stack of 9999 rounds becomes
    /// 200 stacks at a vanilla maximum of 50, and no ordinary backpack has 200 free slots.
    ///
    /// So rather than guess at vanilla maximums — which the game never hands us alongside
    /// an item — this reports the thing the player can actually act on: how many extra
    /// stacks a wind-down would create right now.
    /// </summary>
    internal static class UninstallRisk
    {
        internal static void Check(State state, string when)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            var windDown = ModConfig.WindDownStack;
            if (windDown <= 0)
            {
                return;
            }

            foreach (var inventory in PlayerInventories.All(state))
            {
                var storages = inventory.Storages;
                if (storages == null)
                {
                    continue;
                }

                for (var i = 0; i < storages.Count; i++)
                {
                    Report(storages[i], windDown, when);
                }
            }
        }

        private static void Report(ItemStorage storage, int windDown, string when)
        {
            var items = storage?.Items;
            if (items == null || items.Count == 0)
            {
                return;
            }

            var extraStacks = 0;
            var oversized = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var count = items[i]?.StackCount ?? 0;
                if (count <= windDown)
                {
                    continue;
                }
                oversized++;
                // Integer ceiling: how many stacks this becomes, minus the one it is now.
                extraStacks += ((count + windDown - 1) / windDown) - 1;
            }

            if (extraStacks <= 0)
            {
                return;
            }

            ModLog.Warn(when + ": " + storage.Source + " holds " + oversized +
                        " stack(s) above " + windDown + ", which would split into " +
                        extraStacks + " extra stack(s) needing " + extraStacks +
                        " more slots. Free up room before lowering max_stack or " +
                        "uninstalling, or the overflow is dropped.");
        }
    }
}
