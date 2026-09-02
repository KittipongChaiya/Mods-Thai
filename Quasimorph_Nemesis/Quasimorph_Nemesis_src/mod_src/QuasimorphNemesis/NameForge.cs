using System;
using System.Text;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Builds a nemesis a name and a title.
    ///
    /// <b>Generated from syllables rather than picked from a list</b>, so the mod does
    /// not ship a few dozen names that a long campaign runs out of and starts repeating.
    /// The seed is the record's id, so a given nemesis has the same name every time it
    /// is rebuilt - on reload, on a later floor, in the log - without the name having to
    /// be trusted from the save.
    ///
    /// The titles escalate with rank, which is the only place in the mod where the
    /// player is told, in words, that this thing has beaten them before.
    /// </summary>
    internal static class NameForge
    {
        private static readonly string[] Heads =
        {
            "Var", "Kess", "Dro", "Mal", "Tor", "Vex", "Ras", "Gorn", "Sil", "Hade",
            "Ork", "Vand", "Zel", "Bru", "Cass", "Dek", "Fenn", "Grim", "Hoss", "Ivar",
        };

        private static readonly string[] Tails =
        {
            "ok", "ara", "ich", "ent", "us", "ai", "orn", "eth", "ika", "ov",
            "an", "esk", "ur", "ya", "ol", "ram", "ez", "in", "ath", "ko",
        };

        /// <summary>
        /// Rank 0 is unreachable - a record is created at rank 1 - so the first entry is
        /// only a guard against a corrupt save handing us a zero.
        /// </summary>
        private static readonly string[] Titles =
        {
            "the Unproven",
            "the Survivor",
            "the Hunter",
            "the Executioner",
            "the Butcher",
            "the Undying",
        };

        internal static string Name(NemesisRecord record)
        {
            if (record == null)
            {
                return "Nemesis";
            }

            // A fixed multiplier rather than Random: the same id must always give the
            // same name, in this session and in every later one.
            var seed = record.Id * 2654435761L;
            var head = Heads[(int)(Math.Abs(seed / 7) % Heads.Length)];
            var tail = Tails[(int)(Math.Abs(seed / 13) % Tails.Length)];

            var text = new StringBuilder(head.Length + tail.Length);
            text.Append(head).Append(tail);
            return text.ToString();
        }

        internal static string Title(int rank)
        {
            if (rank < 0)
            {
                rank = 0;
            }
            return rank >= Titles.Length ? Titles[Titles.Length - 1] : Titles[rank];
        }

        /// <summary>What the player actually reads on screen.</summary>
        internal static string FullName(NemesisRecord record)
        {
            if (record == null)
            {
                return "Nemesis";
            }

            var name = string.IsNullOrEmpty(record.Name) ? Name(record) : record.Name;
            return name + " " + Title(record.Rank);
        }
    }
}
