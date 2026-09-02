using HarmonyLib;
using MGSC;

namespace QuasimorphStride
{
    /// <summary>
    /// A window during which <see cref="CanUseInventoryPatch"/> is allowed to say yes.
    ///
    /// <b>Why this exists.</b> <c>PlayerInteractionSystem.CanUseInventory</c> takes no
    /// argument describing what is asking. One check stands in front of five different
    /// things: picking loot off the floor, searching a corpse, using a vest slot,
    /// opening the inventory screen, and opening the healing screen. Lifting it
    /// unconditionally would hand over all five in exchange for a request about two.
    ///
    /// So the three call paths that were actually asked for open a scope around
    /// themselves, and the grant lives inside it. Everything else - the inventory
    /// button on the HUD, the healing screen, an ally's wound panel - reaches the check
    /// with no scope open and gets vanilla's answer.
    ///
    /// <b>Depth, not a flag.</b> A counter rather than a bool, so that a nested call
    /// closing its own scope cannot close an outer one that is still open.
    ///
    /// <b>Finalizers, not postfixes.</b> A postfix does not run when the original
    /// throws, and a scope stuck open would silently become the unconditional grant
    /// this class exists to avoid. A finalizer runs either way.
    /// </summary>
    internal static class PickupScope
    {
        private static int _depth;

        /// <summary>
        /// Whether any scope is open - deliberately not <i>which</i> one.
        ///
        /// The three scope sources answer to two different config keys
        /// (<c>run_take_items</c> and <c>run_use_vest</c>), so it is fair to ask why the
        /// grant does not check the key belonging to the scope that is actually open. A
        /// reason token was considered and rejected, because it would not buy what it
        /// looks like it buys: <c>CanUseInventory</c> is told nothing about what is
        /// asking, so even with a token the answer would still be "whichever scope
        /// opened last", not "the thing being requested".
        ///
        /// The assumption this rests on, stated plainly so a future game build can be
        /// checked against it: <b>a scope authorises the call it wraps, and the calls
        /// wrapped here only ask about themselves.</b> Every <c>CanUseInventory</c> call
        /// inside <c>InteractVestSlot</c> is about the vest; every one inside
        /// <c>TakeItemOrLootCorpse</c> and the <c>TakeItem</c> command is about loot.
        /// That holds in 1.0.3, verified by reading all three. If a later build routes
        /// one through the other, this is the line that stops being true, and the fix
        /// then is to narrow the scopes rather than to label them.
        /// </summary>
        internal static bool IsOpen => _depth > 0;

        internal static void Open() => _depth++;

        internal static void Close()
        {
            if (_depth > 0)
            {
                _depth--;
            }
        }

        /// <summary>
        /// Dropped on a floor change. Nothing should be able to leave a scope open
        /// across a raid boundary, but if a game update ever introduces a path that
        /// does, this makes it a bug that heals itself at the next elevator rather than
        /// one that quietly widens the mod for the rest of the session.
        /// </summary>
        internal static void Reset() => _depth = 0;
    }

    /// <summary>
    /// Picking loot up off the floor you are standing on, and searching a corpse.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.TakeItemOrLootCorpse))]
    internal static class TakeItemOrLootCorpsePatch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state)
        {
            __state = ModConfig.TakeItems;
            if (__state)
            {
                PickupScope.Open();
            }
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                PickupScope.Close();
            }
        }
    }

    /// <summary>
    /// The queued pickup - the second half of a click on a distant pile of loot, which
    /// the game turns into a run of move commands followed by a
    /// <c>TakeItemCommand</c>.
    ///
    /// <c>ProcessCmd</c> is a long method that dispatches every player command there
    /// is, and two other installed mods reference it. This prefix reads one field of
    /// one argument and never returns false, so it cannot alter what the method does or
    /// interfere with anyone else patching it; Harmony runs every prefix regardless of
    /// order.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.ProcessCmd))]
    internal static class ProcessCmdPatch
    {
        [HarmonyPrefix]
        public static void Prefix(ICommand cmd, out bool __state)
        {
            __state = ModConfig.TakeItems && cmd != null && cmd.Type == CmdType.TakeItem;
            if (__state)
            {
                PickupScope.Open();
            }
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                PickupScope.Close();
            }
        }
    }

    /// <summary>
    /// Using a vest slot - a medkit, a stimulant, a grenade. Off by default: this is a
    /// combat capability rather than a convenience, and the Run stance carries an
    /// accuracy penalty precisely because it is the stance you are not meant to fight
    /// from.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.InteractVestSlot))]
    internal static class InteractVestSlotPatch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state)
        {
            __state = ModConfig.UseVest;
            if (__state)
            {
                PickupScope.Open();
            }
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            if (__state)
            {
                PickupScope.Close();
            }
        }
    }
}
