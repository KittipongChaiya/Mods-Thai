using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphBigStack
{
    /// <summary>
    /// Answers one question, in one place: does this inventory belong to a mercenary
    /// the player owns?
    ///
    /// Monsters carry a full <see cref="Inventory"/> too, and every resize and every
    /// weight lookup in the game runs through the same methods for both sides. This
    /// predicate is the only thing standing between the mod and handing every enemy
    /// on the map an infinite backpack, so it is deliberately conservative: anything
    /// it cannot positively identify as the player's is treated as not ours.
    ///
    /// The whole path is public - <c>Mercenaries.Values</c> -&gt;
    /// <c>Mercenary.CreatureData</c> -&gt; <c>CreatureData.Inventory</c> - so there is
    /// no reflection here and nothing for a game update to silently rename.
    /// </summary>
    internal static class PlayerInventories
    {
        /// <summary>
        /// The roster, or null before a campaign is loaded. <c>State.Get</c> is called
        /// on paths that run during menus too, so a miss here is normal, not an error.
        /// </summary>
        private static Mercenaries Roster(State state)
        {
            if (state == null)
            {
                return null;
            }
            try
            {
                return state.Get<Mercenaries>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static bool Owns(State state, Inventory inventory)
        {
            if (inventory == null)
            {
                return false;
            }

            var values = Roster(state)?.Values;
            if (values == null)
            {
                return false;
            }

            // Indexed rather than foreach: this runs from inside game callbacks, and a
            // roster that gains a mercenary mid-iteration must not throw out of a patch.
            for (var i = 0; i < values.Count; i++)
            {
                Mercenary merc;
                try
                {
                    merc = values[i];
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }

                if (ReferenceEquals(merc?.CreatureData?.Inventory, inventory))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool Owns(State state, CreatureData creature)
        {
            return creature != null && Owns(state, creature.Inventory);
        }

        /// <summary>Every inventory the player owns, for the re-assert pass.</summary>
        internal static IEnumerable<Inventory> All(State state)
        {
            var values = Roster(state)?.Values;
            if (values == null)
            {
                yield break;
            }

            for (var i = 0; i < values.Count; i++)
            {
                Mercenary merc;
                try
                {
                    merc = values[i];
                }
                catch (ArgumentOutOfRangeException)
                {
                    break;
                }

                var inventory = merc?.CreatureData?.Inventory;
                if (inventory != null)
                {
                    yield return inventory;
                }
            }
        }
    }
}
