using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Putting things on the floor.
    ///
    /// <b>No item id is hardcoded anywhere in this mod.</b> The cache is assembled by
    /// asking the game what it has and filtering by item class, the same way the sibling
    /// Retinue mod finds a medkit. A hardcoded id is a bug waiting for the next game
    /// update, and it would also quietly ignore every item any other mod added.
    /// </summary>
    internal static class Loot
    {
        private static readonly System.Random Roll = new System.Random();

        /// <summary>
        /// Item classes a supply cache may contain.
        ///
        /// Ammunition and medical supplies, because those are what a crew about to be
        /// shot at actually needs, and what a defending faction would plausibly have
        /// been carrying. Deliberately no weapons or armour: a cache that can roll a
        /// better gun than the one you brought stops being compensation and starts being
        /// the reason to want the complication.
        /// </summary>
        private static readonly ItemClass[] Wanted =
        {
            ItemClass.Ammo,
            ItemClass.Medpack,
            ItemClass.Dressing,
            ItemClass.Syringe,
            ItemClass.Pills,
        };

        private static List<string> _pool;

        internal static int DropCache(ItemsOnFloor itemsOnFloor, MapGrid mapGrid,
                                      CellPosition cell, int count)
        {
            var pool = Pool();
            if (pool.Count == 0)
            {
                return 0;
            }

            var dropped = 0;
            for (var i = 0; i < count; i++)
            {
                try
                {
                    var id = pool[Roll.Next(pool.Count)];

                    // rndConditionAndCapacity: a cache found on a corpse-strewn station
                    // should be a compromise rather than a shop delivery.
                    var item = ItemOnFloorSystem.SpawnItem(itemsOnFloor, mapGrid, id, cell,
                                                           visualDelay: 0f,
                                                           rndConditionAndCapacity: true,
                                                           updateExamined: false);
                    if (item != null)
                    {
                        dropped++;
                    }
                }
                catch (Exception error)
                {
                    ModLog.Error("could not drop a cache item; the rest of the cache is " +
                                 "unaffected", error);
                }
            }
            return dropped;
        }

        /// <summary>
        /// Every item in the game of a class worth finding in a cache, resolved once.
        /// </summary>
        private static List<string> Pool()
        {
            if (_pool != null)
            {
                return _pool;
            }

            _pool = new List<string>();
            try
            {
                var items = Data.Items;
                if (items == null)
                {
                    ModLog.Warn("Data.Items is null; supply caches will be empty");
                    return _pool;
                }

                foreach (var id in items.Ids)
                {
                    var record = RecordOf(items, id);
                    if (record == null)
                    {
                        continue;
                    }

                    foreach (var wanted in Wanted)
                    {
                        if (record.ItemClass == wanted)
                        {
                            _pool.Add(id);
                            break;
                        }
                    }
                }

                ModLog.Info("supply cache pool: " + _pool.Count + " item type(s)");
            }
            catch (Exception error)
            {
                ModLog.Error("could not build the supply cache pool; caches will be empty",
                             error);
            }
            return _pool;
        }

        /// <summary>
        /// <c>ItemsCollection.GetSimpleRecord</c> assumes every entry is composite and
        /// throws on the ones that are not, so this asks the question defensively - the
        /// same guard the sibling Retinue mod uses.
        /// </summary>
        private static ItemRecord RecordOf(ItemsCollection items, string id)
        {
            return items.GetRecord(id) is CompositeItemRecord composite
                ? composite.GetRecord<ItemRecord>()
                : null;
        }
    }
}
