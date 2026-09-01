using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphBigPack
{
    /// <summary>
    /// Makes the player's backpack (and optionally vest) as tall as the config asks.
    ///
    /// Height only, never width. <c>InventoryScreen</c> pairs its backpack grid with a
    /// <c>CommonScrollBar</c> and the game grows its own cargo storages vertically at
    /// runtime through <c>ItemStorage.ExpandHeightAndPutItem</c>, so a tall grid is a
    /// shape the UI already renders. There is no horizontal scrollbar anywhere, so a
    /// wider grid would simply render off-panel.
    /// </summary>
    internal static class InventorySpace
    {
        /// <summary>
        /// How tall this storage should be, or 0 if it is none of our business.
        /// Filtering on <see cref="ItemStorage.Source"/> keeps us off weapon slots,
        /// armour slots, floor piles, containers and every cargo hold.
        /// </summary>
        private static int TargetHeight(ItemStorage storage)
        {
            if (storage == null)
            {
                return 0;
            }

            switch (storage.Source)
            {
                case ItemStorageSource.Backpack:
                    return ModConfig.BackpackHeight;
                case ItemStorageSource.Vest:
                    return ModConfig.ResizeVest ? ModConfig.VestHeight : 0;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Grow one storage to <paramref name="target"/> rows. Returns true if it
        /// actually changed.
        ///
        /// <c>ExpandHeight</c> is a delta, not an absolute: its body is
        /// <c>Height = Height + height</c> followed by a resize of the internal
        /// <c>_positions</c> array to the new <c>MaxCapacity</c>. That second half is
        /// the reason to prefer it over setting <c>Height</c> by reflection - the
        /// backing array has to grow with the grid or every write past the old
        /// capacity is out of bounds.
        ///
        /// We only ever grow. Shrinking is what destroys items, and this mod never
        /// does it.
        /// </summary>
        internal static bool Grow(ItemStorage storage, int target, string what)
        {
            if (storage == null || target <= 0 || storage.Width <= 0)
            {
                return false;
            }

            var before = storage.Height;
            if (before >= target)
            {
                return false;
            }

            storage.ExpandHeight(target - before);

            if (storage.Height < target)
            {
                // Never seen; if a game update changes ExpandHeight semantics this is
                // how we find out, rather than by a grid that quietly stays small.
                ModLog.Warn(what + ": asked for " + target + " rows, got " + storage.Height +
                            " (was " + before + ") - ExpandHeight may have changed");
                return false;
            }

            ModLog.Info(what + ": " + storage.Width + "x" + before + " -> " +
                        storage.Width + "x" + storage.Height);
            return true;
        }

        /// <summary>
        /// Grow a storage only if the inventory holding it is the player's.
        ///
        /// <paramref name="vanillaHeight"/> is the height the game just asked for. It is
        /// recorded before growing even when no growth is needed, because this call is
        /// the only place that number is ever visible, and
        /// <see cref="UninstallRisk"/> needs the current one - a bigger backpack changes
        /// it mid-game.
        /// </summary>
        internal static void GrowIfOurs(State state, Inventory inventory, ItemStorage storage,
                                        int vanillaHeight)
        {
            var target = TargetHeight(storage);
            if (target <= 0)
            {
                return;
            }

            if (!PlayerInventories.Owns(state, inventory))
            {
                return;
            }

            UninstallRisk.RecordVanillaHeight(storage, vanillaHeight);
            Grow(storage, target, storage.Source.ToString());
        }

        /// <summary>
        /// Storages built in the <see cref="Inventory"/> constructor never pass through
        /// <c>ResizeStorage</c>, so the patch alone would miss a mercenary who has no
        /// backpack equipped. Run this whenever we get a fresh look at the roster.
        /// </summary>
        internal static void ReassertAll(State state, string reason)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            int grown = 0, seen = 0;
            foreach (var inventory in PlayerInventories.All(state))
            {
                seen++;
                if (Grow(inventory.BackpackStore, ModConfig.BackpackHeight, "backpack"))
                {
                    grown++;
                }
                if (ModConfig.ResizeVest && Grow(inventory.VestStore, ModConfig.VestHeight, "vest"))
                {
                    grown++;
                }
            }
            ModLog.Info("re-assert (" + reason + "): " + seen + " mercenaries, " +
                        grown + " storages grown");
        }
    }

    /// <summary>
    /// Postfix, not prefix. <c>ResizeStorage</c> hands whatever no longer fits to
    /// <c>ResizeAndReshuffle</c> and then re-homes it or calls <c>Remove</c> on it, all
    /// computed against the size it was given. Rewriting that size on the way in would
    /// mean lying to the game's own overflow handling; growing afterwards cannot
    /// displace anything, because the grid only ever gets bigger.
    ///
    /// Growing is done through <see cref="ItemStorage.ExpandHeight"/> and never by
    /// calling <c>ResizeStorage</c> again - that would re-enter this patch and recurse
    /// until the stack ran out.
    /// </summary>
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.ResizeStorage))]
    internal static class ResizeStoragePatch
    {
        /// <summary>
        /// <paramref name="storage"/> and <paramref name="height"/> are matched by name
        /// against the game's own parameter list (storage, width, height, itemsOnFloor,
        /// forceFloor). A rename in a game update makes Harmony throw at patch time,
        /// which the caller logs - a loud failure, not a silent one.
        /// </summary>
        [HarmonyPostfix]
        internal static void Postfix(Inventory __instance, ItemStorage storage, int height)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                InventorySpace.GrowIfOurs(BigPackMod.GameState, __instance, storage, height);
            }
            catch (Exception error)
            {
                // A patch that throws into game code is how mods break saves. Swallow
                // it here and leave the player with a vanilla-sized grid.
                ModLog.Error("ResizeStorage postfix failed", error);
            }
        }
    }
}
