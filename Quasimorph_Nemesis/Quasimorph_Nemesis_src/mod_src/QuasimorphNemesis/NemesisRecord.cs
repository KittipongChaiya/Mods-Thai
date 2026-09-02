using System;
using System.Collections.Generic;
using SimpleJSON;

namespace QuasimorphNemesis
{
    /// <summary>
    /// One remembered enemy.
    ///
    /// <b>This is the whole mod's state.</b> Everything else - the injected mob class,
    /// the name on screen, the stats it fights with, the raid it turns up in - is
    /// derived from these fields and can be thrown away and rebuilt at any time. That
    /// is deliberate, and it is what keeps the mod idempotent: a nemesis is never a
    /// buffed creature that has to be tracked, it is a row that a creature is built
    /// from.
    /// </summary>
    internal sealed class NemesisRecord
    {
        /// <summary>Stable across the campaign. Also forms the injected mob class id.</summary>
        internal int Id;

        /// <summary>The generated name, already localised at birth - see <see cref="NameForge"/>.</summary>
        internal string Name = string.Empty;

        /// <summary>The mob class it was promoted from. Its loadout and body come from here.</summary>
        internal string BaseMobClassId = string.Empty;

        internal string FactionId = string.Empty;

        /// <summary>
        /// How many times it has killed one of your mercenaries. Drives the title, the
        /// stat scaling and the equipment tech level - so rank is the single dial the
        /// rest of the mod reads.
        /// </summary>
        internal int Rank;

        /// <summary>Game day it was first promoted, for the log and the record screen.</summary>
        internal int FirstSeenDay;

        /// <summary>Set when the player finally kills it. Retired rows are kept for the tally, never spawned.</summary>
        internal bool Retired;

        /// <summary>The mob class id this record injects into <c>Data.MobClasses</c>.</summary>
        internal string MobClassId => "nemesis_" + Id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The localization key the game will ask for when it draws the name.</summary>
        internal string NameLocKey => "monster." + MobClassId + ".name";

        internal string DescLocKey => "monster." + MobClassId + ".desc";

        internal JSONNode ToJson()
        {
            var node = new JSONObject();
            node["id"] = Id;
            node["name"] = Name ?? string.Empty;
            node["base"] = BaseMobClassId ?? string.Empty;
            node["faction"] = FactionId ?? string.Empty;
            node["rank"] = Rank;
            node["day"] = FirstSeenDay;
            node["retired"] = Retired;
            return node;
        }

        internal static NemesisRecord FromJson(JSONNode node)
        {
            if (node == null)
            {
                return null;
            }

            var record = new NemesisRecord
            {
                Id = node["id"].AsInt,
                Name = node["name"].Value ?? string.Empty,
                BaseMobClassId = node["base"].Value ?? string.Empty,
                FactionId = node["faction"].Value ?? string.Empty,
                Rank = node["rank"].AsInt,
                FirstSeenDay = node["day"].AsInt,
                Retired = node["retired"].AsBool,
            };

            // A row with no base class cannot be rebuilt into anything, and a row with
            // no id cannot be addressed. Both are corruption rather than absence, so
            // they are dropped rather than repaired into something plausible.
            if (record.Id <= 0 || record.BaseMobClassId.Length == 0)
            {
                return null;
            }
            return record;
        }
    }
}
