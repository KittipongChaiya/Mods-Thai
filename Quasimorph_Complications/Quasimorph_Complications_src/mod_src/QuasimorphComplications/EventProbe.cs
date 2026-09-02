using System;
using System.IO;
using System.Reflection;
using System.Text;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// The spike, shipped rather than thrown away.
    ///
    /// <b>The game has an unused in-raid event system, and this mod does not use it.</b>
    /// <c>IngameEventSystem.RandomizeDungeonEvent</c> is public and static;
    /// <c>Data.Events</c> holds the records; <c>MobSpawnEventRecord</c> has
    /// <c>PointsRange</c>, <c>AllianceType</c>, <c>FactionId</c>, <c>QmorphosLevel</c>
    /// and - remarkably - <c>BlockAllDoors</c>. A reinforcement wave that locks the
    /// doors behind it is already a data type in this game, and nothing in a typical
    /// hundred-mod load order touches any of it.
    ///
    /// This mod drives its complications itself instead, because one question could not
    /// be answered without running the game: <c>EventCollection</c> declares only
    /// <c>TryGet</c>, so unlike <c>Data.MobClasses</c> - a
    /// <c>ConfigRecordCollection&lt;T&gt;</c> with a public <c>AddRecord</c>, which is
    /// what makes the sibling Nemesis mod possible - there may be no way to add an event
    /// at all.
    ///
    /// So this class answers it, at runtime, and writes the answer down. If
    /// <c>Data.Events</c> turns out to be addable, the whole complication catalogue
    /// could be re-expressed as real game events, which would be a better mod than this
    /// one. That is the reason to keep the probe rather than delete it.
    /// </summary>
    internal static class EventProbe
    {
        internal static void Dump(string modDirectory)
        {
            try
            {
                var text = new StringBuilder();
                text.AppendLine("Quasimorph Complications probe " + ModInfo.Version);
                text.AppendLine("captured " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                text.AppendLine();

                text.AppendLine("== Complication catalogue");
                foreach (var complication in Scheduler.All)
                {
                    text.AppendLine("   " + complication.Id.PadRight(16) +
                                    " weight " + complication.Weight);
                }

                text.AppendLine();
                text.AppendLine("== The game's own dungeon event system (unused by this mod)");
                DescribeEvents(text);

                File.WriteAllText(Path.Combine(modDirectory, "probe.txt"), text.ToString(),
                                  new UTF8Encoding(false));
                ModLog.Info("wrote probe.txt");
            }
            catch (Exception error)
            {
                ModLog.Error("could not write probe.txt", error);
            }
        }

        private static void DescribeEvents(StringBuilder text)
        {
            try
            {
                var events = Data.Events;
                if (events == null)
                {
                    text.AppendLine("   Data.Events is null at this moment");
                    return;
                }

                var type = events.GetType();
                text.AppendLine("   Data.Events type : " + type.FullName);
                text.AppendLine("   base type        : " +
                                (type.BaseType?.FullName ?? "(none)"));

                text.AppendLine();
                text.AppendLine("   -- public methods, including inherited --");
                foreach (var method in type.GetMethods(BindingFlags.Instance |
                                                       BindingFlags.Public))
                {
                    if (method.DeclaringType == typeof(object))
                    {
                        continue;
                    }
                    text.AppendLine("      " + method.Name);
                }

                // The question the whole of Route A turns on.
                var add = type.GetMethod("AddRecord");
                var remove = type.GetMethod("RemoveRecord");
                text.AppendLine();
                text.AppendLine("   AddRecord    : " + (add == null ? "NOT FOUND" : "FOUND"));
                text.AppendLine("   RemoveRecord : " + (remove == null ? "NOT FOUND" : "FOUND"));
                text.AppendLine();
                text.AppendLine(add == null
                    ? "   -> events cannot be injected; driving complications ourselves was right."
                    : "   -> events CAN be injected. Worth re-expressing the catalogue as real "
                      + "game events in a later version.");
            }
            catch (Exception error)
            {
                text.AppendLine("   could not inspect Data.Events: " + error.GetType().Name +
                                " " + error.Message);
            }
        }
    }
}
