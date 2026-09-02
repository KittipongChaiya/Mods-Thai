using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// The single definition of "is an ally", and the only place this mod decides
    /// whether a creature is on your side.
    ///
    /// <b>Every patch in this mod asks this question first.</b> That is the whole
    /// safety argument: the line-of-sight layer relaxes what you can see and order,
    /// and relaxing it for a hostile creature would be a cheat and a lie. A creature
    /// that fails this test leaves every patch untouched, by the vanilla path.
    ///
    /// <b>Why this is duplicated from the sibling Retinue mod.</b> Retinue has a
    /// richer <c>AllyIdentity</c> that also knows about roster mercenaries and squad
    /// counting. Sharing it would mean this mod referencing that assembly - a hard
    /// dependency between two mods that are deliberately independent, so that either
    /// can be uninstalled without the other. Fifteen duplicated lines is the cheaper
    /// side of that trade, and this comment is the price of admitting it.
    /// </summary>
    internal static class AllyTest
    {
        /// <summary>
        /// True for a creature that fights on the player's side and is not the player.
        ///
        /// The player's own mercenary is <c>PlayerAlliance</c> too and is excluded:
        /// you do not give yourself orders through the ally panel.
        /// </summary>
        internal static bool IsAlly(Creature creature)
        {
            return creature is Monster monster &&
                   monster.CreatureData != null &&
                   monster.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance;
        }

        /// <summary>Stable identity for an ally, valid across save, load and floors.</summary>
        internal static int IdOf(Creature creature)
        {
            return creature?.CreatureData?.UniqueId ?? 0;
        }
    }
}
