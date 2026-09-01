using MGSC;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Layer 4 - your own place in the fight.
    ///
    /// <c>CreatureData.IgnoreByMonsters</c> is read by <c>AIVision.GetVisibleEnemies</c>:
    /// a creature carrying it is never added to any monster's enemy list, so nothing
    /// ever chooses it as a target. The game uses it on quest captives you are escorting.
    /// Setting it on your own mercenary is a one-field spectator mode, supported by the
    /// engine rather than bolted onto it.
    ///
    /// It is a cheat and the config file says so. It is also not immortality: fire, gas,
    /// explosions and hazardous terrain do not consult a target list, so standing in
    /// them still kills you.
    ///
    /// The flag is written on every floor, in both directions, so turning the switch off
    /// takes effect on the next floor rather than leaving a permanently untargetable
    /// mercenary baked into a save.
    /// </summary>
    internal static class PlayerRole
    {
        private static bool _lastApplied;
        private static bool _everApplied;

        internal static void Sync(State state)
        {
            var player = state?.Get<Creatures>()?.Player;
            if (player?.CreatureData == null)
            {
                return;
            }

            var wanted = ModConfig.Spectator;
            player.CreatureData.IgnoreByMonsters = wanted;

            if (_everApplied && wanted == _lastApplied)
            {
                return;
            }

            _everApplied = true;
            _lastApplied = wanted;
            ModLog.Info(wanted
                ? "spectator ON: enemies will not target your mercenary. Fire, gas and " +
                  "explosions still will."
                : "spectator OFF: your mercenary is targetable, as in vanilla");
        }
    }
}
