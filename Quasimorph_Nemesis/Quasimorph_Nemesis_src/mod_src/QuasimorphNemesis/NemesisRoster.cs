using System;
using System.Collections.Generic;
using SimpleJSON;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Every enemy this campaign remembers, and the only place that list is edited.
    ///
    /// <b>Why this lives in the save and not in a file of our own.</b> A file next to
    /// the DLL would be simpler and would need no patch at all - but it would be wrong
    /// the moment the player loads an earlier save, which in a game built around
    /// permadeath and save management is not an edge case. The roster has to move with
    /// the save or it is lying. <c>ModHookType</c> has <c>BeforeSaveLoaded</c> and
    /// <c>AfterSaveLoaded</c> but nothing for writing, so there is no hook-only route
    /// to that, which is why <see cref="SavePatch"/> exists.
    /// </summary>
    internal static class NemesisRoster
    {
        /// <summary>
        /// The key our node hangs off in the save's global components. Namespaced so
        /// that the four other mods writing to the same JSON object cannot collide with
        /// us and we cannot collide with them.
        /// </summary>
        internal const string SaveKey = "quasimorph.nemesis.roster";

        private static readonly List<NemesisRecord> Records = new List<NemesisRecord>();
        private static int _nextId = 1;

        internal static IReadOnlyList<NemesisRecord> All => Records;

        internal static int LivingCount
        {
            get
            {
                var count = 0;
                foreach (var record in Records)
                {
                    if (!record.Retired)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        internal static NemesisRecord ById(int id)
        {
            foreach (var record in Records)
            {
                if (record.Id == id)
                {
                    return record;
                }
            }
            return null;
        }

        /// <summary>Finds the record a live creature was built from, by its mob class id.</summary>
        internal static NemesisRecord ByMobClass(string mobClassId)
        {
            if (string.IsNullOrEmpty(mobClassId))
            {
                return null;
            }

            foreach (var record in Records)
            {
                if (string.Equals(record.MobClassId, mobClassId, StringComparison.Ordinal))
                {
                    return record;
                }
            }
            return null;
        }

        internal static NemesisRecord Create(string baseMobClassId, string factionId, int day)
        {
            var record = new NemesisRecord
            {
                Id = _nextId++,
                BaseMobClassId = baseMobClassId ?? string.Empty,
                FactionId = factionId ?? string.Empty,
                Rank = 1,
                FirstSeenDay = day,
            };
            record.Name = NameForge.Name(record);
            Records.Add(record);
            Trim();
            return record;
        }

        /// <summary>
        /// Keeps the roster bounded. Retired rows go first and oldest first, because a
        /// nemesis you already killed is a memento and a living one is a threat.
        /// </summary>
        private static void Trim()
        {
            if (Records.Count <= ModConfig.RosterCap)
            {
                return;
            }

            for (var i = 0; i < Records.Count && Records.Count > ModConfig.RosterCap; i++)
            {
                if (Records[i].Retired)
                {
                    Records.RemoveAt(i);
                    i--;
                }
            }

            while (Records.Count > ModConfig.RosterCap)
            {
                Records.RemoveAt(0);
            }
        }

        /// <summary>
        /// Wipes the in-memory roster. Called before a save's roster is read in, so
        /// that loading a second save never inherits the first one's enemies.
        /// </summary>
        internal static void Clear()
        {
            Records.Clear();
            _nextId = 1;
        }

        internal static JSONNode ToJson()
        {
            var root = new JSONObject();
            root["nextId"] = _nextId;
            var array = new JSONArray();
            foreach (var record in Records)
            {
                array.Add(record.ToJson());
            }
            root["records"] = array;
            return root;
        }

        internal static void FromJson(JSONNode node)
        {
            Clear();
            if (node == null)
            {
                return;
            }

            try
            {
                var array = node["records"].AsArray;
                if (array != null)
                {
                    for (var i = 0; i < array.Count; i++)
                    {
                        var record = NemesisRecord.FromJson(array[i]);
                        if (record != null)
                        {
                            Records.Add(record);
                        }
                    }
                }

                _nextId = Math.Max(1, node["nextId"].AsInt);
                foreach (var record in Records)
                {
                    // Never hand out an id a live row already owns, whatever the save said.
                    if (record.Id >= _nextId)
                    {
                        _nextId = record.Id + 1;
                    }
                }

                ModLog.Info("roster loaded: " + Records.Count + " remembered, " +
                            LivingCount + " still out there");
            }
            catch (Exception error)
            {
                // A corrupt roster costs the player their nemeses, which is sad. It must
                // not cost them the save.
                Clear();
                ModLog.Error("the roster in this save could not be read and has been " +
                             "reset; the campaign is otherwise untouched", error);
            }
        }
    }
}
