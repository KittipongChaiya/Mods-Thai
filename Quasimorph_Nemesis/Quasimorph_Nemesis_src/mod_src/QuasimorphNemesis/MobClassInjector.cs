using System;
using System.Collections.Generic;
using System.Reflection;
using MGSC;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Turns roster rows into real mob classes the game can spawn.
    ///
    /// <b>This is the design decision the whole mod rests on.</b> The obvious way to
    /// make a nemesis stronger is to find the creature and write bigger numbers onto
    /// it. The sibling Retinue mod documents at length why that is a trap: buffed stats
    /// are saved with the creature, the bookkeeping that says "already buffed" is not,
    /// and after a reload the buff lands again on top of itself.
    ///
    /// A nemesis is worse exposed than an ally, because it is buffed <i>and</i>
    /// persisted <i>and</i> deliberately re-encountered. So it is not buffed at all.
    /// Instead each rank produces a <c>MobClassRecord</c> - a template - and the game
    /// builds an ordinary creature from it the way it builds every other creature.
    /// Rebuilding the record a thousand times produces the same record, because it is
    /// always cloned fresh from the untouched base class and never from itself.
    ///
    /// <b>Shallow clone, scalar writes only.</b> A <c>MobClassRecord</c> holds lists -
    /// weapons, armour, body types. The clone shares those references with the base
    /// record, so this class assigns value-typed fields and never mutates a list or
    /// replaces one. Mutating a shared list would edit the base class every enemy of
    /// that type is built from, which is exactly the leak this mod must not have.
    /// </summary>
    internal static class MobClassInjector
    {
        /// <summary>Ids we have added, so uninstall and difficulty changes can undo them.</summary>
        private static readonly HashSet<string> Injected = new HashSet<string>(StringComparer.Ordinal);

        private static FieldInfo[] _fields;

        /// <summary>
        /// Rebuilds every living nemesis's mob class from the roster. Safe to call as
        /// often as you like; that is the point.
        /// </summary>
        internal static void SyncAll()
        {
            var classes = Data.MobClasses;
            if (classes == null)
            {
                ModLog.Error("Data.MobClasses is null; no nemesis can be built this session");
                return;
            }

            var wanted = new HashSet<string>(StringComparer.Ordinal);
            var built = 0;

            foreach (var record in NemesisRoster.All)
            {
                if (record.Retired)
                {
                    continue;
                }

                wanted.Add(record.MobClassId);
                if (Build(classes, record))
                {
                    built++;
                }
            }

            // Anything we injected that the roster no longer wants - retired, trimmed,
            // or belonging to a save we have since left - goes away again.
            var stale = new List<string>();
            foreach (var id in Injected)
            {
                if (!wanted.Contains(id))
                {
                    stale.Add(id);
                }
            }
            foreach (var id in stale)
            {
                Remove(classes, id);
            }

            if (built > 0 || stale.Count > 0)
            {
                ModLog.Info("mob classes synced: " + built + " nemesis template(s) built, " +
                            stale.Count + " removed");
            }
        }

        /// <summary>Removes every record this mod added. Used when a run is excluded by config.</summary>
        internal static void RemoveAll()
        {
            var classes = Data.MobClasses;
            if (classes == null)
            {
                return;
            }

            foreach (var id in new List<string>(Injected))
            {
                Remove(classes, id);
            }
        }

        private static void Remove(ConfigRecordCollection<MobClassRecord> classes, string id)
        {
            try
            {
                classes.RemoveRecord(id);
            }
            catch (Exception error)
            {
                ModLog.Warn("could not remove mob class '" + id + "' (" +
                            error.GetType().Name + ")");
            }
            Injected.Remove(id);
        }

        private static bool Build(ConfigRecordCollection<MobClassRecord> classes,
                                  NemesisRecord record)
        {
            try
            {
                var baseRecord = classes.GetRecord(record.BaseMobClassId, false);
                if (baseRecord == null)
                {
                    ModLog.Warn("nemesis " + record.Id + " was promoted from mob class '" +
                                record.BaseMobClassId + "', which this game build does " +
                                "not have. It stays in the roster but cannot appear.");
                    return false;
                }

                var clone = Clone(baseRecord);
                if (clone == null)
                {
                    return false;
                }

                Scale(clone, record.Rank);

                // AddRecord on an id already present would double up, so replace.
                if (Injected.Contains(record.MobClassId))
                {
                    classes.RemoveRecord(record.MobClassId);
                }
                classes.AddRecord(record.MobClassId, clone);
                Injected.Add(record.MobClassId);
                return true;
            }
            catch (Exception error)
            {
                ModLog.Error("could not build the mob class for nemesis " + record.Id +
                             "; it will not appear this session", error);
                return false;
            }
        }

        /// <summary>
        /// Field-by-field shallow copy. Reflection rather than a hand-written copy
        /// constructor on purpose: a game update that adds a field to
        /// <c>MobClassRecord</c> would silently drop it from a hand-written copy, and a
        /// nemesis built from a half-copied template is a very confusing bug.
        /// </summary>
        private static MobClassRecord Clone(MobClassRecord source)
        {
            var type = typeof(MobClassRecord);
            _fields = _fields ?? type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                BindingFlags.NonPublic);

            var clone = Activator.CreateInstance(type) as MobClassRecord;
            if (clone == null)
            {
                ModLog.Error("MobClassRecord could not be constructed; no nemesis templates");
                return null;
            }

            foreach (var field in _fields)
            {
                if (!field.IsLiteral && !field.IsInitOnly)
                {
                    field.SetValue(clone, field.GetValue(source));
                }
            }
            return clone;
        }

        /// <summary>
        /// What a rank actually buys. Every value is assigned from the base record's
        /// value, never read back from the clone and multiplied again.
        ///
        /// Only scalars are touched. Nothing here reaches into a list, because those
        /// lists are shared with the base class by <see cref="Clone"/>.
        /// </summary>
        private static void Scale(MobClassRecord clone, int rank)
        {
            var steps = Math.Max(0, rank);

            // A rung on a ladder, not a magnitude - the same reasoning the sibling
            // Ruthless mod uses for its own equipment bonus.
            clone.EquipmentTechLevelBonus += Math.Min(steps, ModConfig.MaxTechLevelBonus);

            // Health is the stat that most reliably turns a fight into a slog rather
            // than a threat, so it is the most restrained multiplier here.
            clone.HealthMod = Scaled(clone.HealthMod, ModConfig.HealthPerRank, steps);

            clone.Los += Math.Min(steps, 2);
            clone.ActionPointsMod += steps >= ModConfig.RankForExtraTurn ? 1 : 0;
            clone.DodgeMod += ModConfig.DodgePerRank * steps;
        }

        /// <summary>
        /// A mob class modifier is a percentage offset, so a base of zero still has to
        /// scale from something. 100 is the game's own neutral value for these fields.
        /// </summary>
        private static int Scaled(int baseValue, float perRank, int steps)
        {
            var reference = baseValue == 0 ? 100f : Math.Abs(baseValue);
            return (int)Math.Round(baseValue + reference * perRank * steps);
        }
    }
}
