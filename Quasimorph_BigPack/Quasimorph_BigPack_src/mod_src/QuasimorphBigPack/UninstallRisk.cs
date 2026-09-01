using System.Runtime.CompilerServices;
using MGSC;

namespace QuasimorphBigPack
{
    /// <summary>
    /// The one way this mod can cost you something.
    ///
    /// Grid sizes are serialised into the save. Remove the mod with a pack fuller than
    /// a vanilla one holds and the next <c>Inventory.ResizeStorage</c> shrinks it, which
    /// is not a no-op: it calls <c>ResizeAndReshuffle</c>, collects everything that no
    /// longer fits, and then either pushes it into the floor storage it was handed or -
    /// if there is no floor, which is the case on the ship - calls <c>Remove</c> on it.
    /// Those items are gone.
    ///
    /// We cannot stop that from outside the game, so we do the next best thing: remember
    /// how big the pack would be without us, and say plainly when the player is carrying
    /// more than that.
    /// </summary>
    internal static class UninstallRisk
    {
        /// <summary>
        /// Vanilla height per storage, as the game last asked for it. Weak keys so a
        /// storage belonging to a discarded save is not kept alive by this table.
        /// </summary>
        private static readonly ConditionalWeakTable<ItemStorage, object> VanillaHeight =
            new ConditionalWeakTable<ItemStorage, object>();

        /// <summary>
        /// Called from the resize postfix with the height the game itself chose, before
        /// we grow past it. This is the only place that number is observable.
        /// </summary>
        internal static void RecordVanillaHeight(ItemStorage storage, int height)
        {
            if (storage == null || height <= 0)
            {
                return;
            }
            VanillaHeight.Remove(storage);
            VanillaHeight.Add(storage, height);
        }

        private static int VanillaCapacity(ItemStorage storage)
        {
            if (storage == null || !VanillaHeight.TryGetValue(storage, out var boxed))
            {
                return 0;
            }
            return storage.Width * (int)boxed;
        }

        /// <summary>
        /// Warn when a pack holds more than it would without the mod. Cheap, and it runs
        /// at the two moments a player might be about to close the game.
        /// </summary>
        internal static void Check(State state, string when)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            foreach (var inventory in PlayerInventories.All(state))
            {
                Report(inventory.BackpackStore, "backpack", when);
                if (ModConfig.ResizeVest)
                {
                    Report(inventory.VestStore, "vest", when);
                }
            }
        }

        private static void Report(ItemStorage storage, string what, string when)
        {
            var vanilla = VanillaCapacity(storage);
            var carried = storage?.Items?.Count ?? 0;
            if (vanilla <= 0 || carried <= vanilla)
            {
                return;
            }

            ModLog.Warn(when + ": " + what + " holds " + carried + " items but only " +
                        vanilla + " would fit without Big Pack. Uninstalling now would " +
                        "destroy the excess - move it to ship cargo first.");
        }
    }
}
