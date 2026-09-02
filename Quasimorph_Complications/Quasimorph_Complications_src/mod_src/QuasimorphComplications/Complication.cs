using System;
using MGSC;

namespace QuasimorphComplications
{
    /// <summary>
    /// One thing that can go wrong with a raid.
    ///
    /// A complication has three moments and no other surface: the floor starts, a turn
    /// passes, the floor ends. Everything a complication does happens inside those, and
    /// <see cref="Scheduler"/> is the only thing that calls them. Keeping the contract
    /// this small is what makes a complication readable on its own and safe to add to -
    /// a new one is a single file that cannot reach into the rest of the mod.
    ///
    /// <b>Cleanup is not optional.</b> Anything a complication changes about the world -
    /// and one of them writes to shared global settings - has to be given back in
    /// <see cref="OnFloorEnd"/>, because the next raid did not agree to it.
    /// </summary>
    internal abstract class Complication
    {
        /// <summary>Stable key used in config and the log. Never shown to the player.</summary>
        internal abstract string Id { get; }

        /// <summary>What the player is told when the raid begins.</summary>
        internal abstract string Announcement { get; }

        /// <summary>
        /// Relative likelihood against the other enabled complications. Read from config
        /// so a player can turn one off by setting it to zero without editing anything
        /// else.
        /// </summary>
        internal virtual int Weight => ModConfig.WeightOf(Id);

        /// <summary>
        /// Whether this complication can run on the floor in front of us. Default is
        /// yes; a complication that needs something specific says so here rather than
        /// failing quietly later.
        /// </summary>
        internal virtual bool CanRun(State state) => true;

        internal virtual void OnFloorStart(State state)
        {
        }

        /// <summary>
        /// Called once per turn, never per frame. <paramref name="turn"/> is the raid's
        /// own turn counter, so a complication can pace itself without keeping time.
        /// </summary>
        internal virtual void OnTurn(State state, int turn)
        {
        }

        internal virtual void OnFloorEnd(State state)
        {
        }

        /// <summary>
        /// Shared failure handling. A complication that throws must cost the player a
        /// complication, never the raid.
        /// </summary>
        protected void Guard(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception error)
            {
                ModLog.Error("complication '" + Id + "' failed during " + what +
                             "; the raid continues without it", error);
            }
        }
    }
}
