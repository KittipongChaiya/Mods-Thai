using System;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// Somebody else took the same contract.
    ///
    /// A small crew arrives on a faction of their own - hostile to the station and
    /// hostile to you. They are a threat and they are also loot on legs, which is the
    /// trade: the floor is more dangerous, and the best gear on it is now walking
    /// around wearing itself.
    ///
    /// They spawn on <c>Traitors</c>, an alliance the game already uses for creatures
    /// that fight everybody, so the existing AI handles the three-way fight without this
    /// mod writing a line of behaviour.
    /// </summary>
    internal sealed class RivalCrew : Complication
    {
        internal override string Id => "rivals";

        internal override string Announcement =>
            "Another crew is working this station. They were not told about you either.";

        private bool _arrived;

        internal override void OnFloorStart(State state)
        {
            _arrived = false;
        }

        internal override void OnTurn(State state, int turn)
        {
            if (_arrived || turn < ModConfig.RivalArrivalTurn)
            {
                return;
            }

            Guard("turn " + turn, () =>
            {
                if (Spawns.Wave(state, ModConfig.RivalSize, hostile: false,
                                minDistance: ModConfig.RivalDistance, label: "rival crew"))
                {
                    _arrived = true;
                    ModLog.Info("rival crew arrived on turn " + turn);
                    Announce.Flash("Another crew is on this floor.");
                }
            });
        }
    }
}
