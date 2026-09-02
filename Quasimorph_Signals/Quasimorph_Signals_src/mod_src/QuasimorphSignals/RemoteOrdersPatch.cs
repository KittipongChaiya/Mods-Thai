using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 3 - reaching an ally you cannot currently see.
    ///
    /// <b>The safety rule, and it is absolute.</b> Every patch in this file asks
    /// <see cref="AllyTest.IsAlly"/> before it changes anything, and returns the
    /// vanilla answer untouched for everything else. Relaxing visibility for a hostile
    /// creature would be a wallhack wearing a squad-command costume. The check is on
    /// the creature in hand, on every single call - not a mode, not a flag set
    /// earlier, nothing that can be left switched on by accident.
    ///
    /// <b>What this covers, and what it may not.</b> Two gates are patched here and
    /// both are well understood: the ally's own signal marker, and the inspect
    /// window's follower test. The game has a third candidate,
    /// <c>PlayerInteractionSystem.EvaluateSecondaryCursorAction</c>, which is a private
    /// static taking eight systems and returning a bool. It is deliberately <i>not</i>
    /// patched: without being able to read its body there is no honest way to write a
    /// prefix for it that is safe, and shipping a guess into the cursor path of a
    /// turn-based game is a worse outcome than a feature that covers less. If in-game
    /// testing shows right-click orders are still refused at range, that method is the
    /// next target and PROJECT_STATE.md records it as such.
    /// </summary>
    internal static class RemoteOrdersPatch
    {
    }

    /// <summary>
    /// Keeps an ally's signal marker drawn when the ally is out of sight, so there is
    /// something on screen to click.
    ///
    /// This is the same property the 'Ally Roam/Patrol', 'Continue on Monster
    /// Detection' and 'Stealth Auto-Walk' mods all postfix. We are careful to be
    /// additive: we only ever turn the answer from false to true, and only for an
    /// ally, so a mod that wants a signal shown still gets one.
    /// </summary>
    [HarmonyPatch(typeof(Monster), "get_ShowSignal")]
    internal static class ShowSignalPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Monster __instance, ref bool __result)
        {
            if (!ModConfig.Enabled || !ModConfig.RemoteOrders || __result)
            {
                return;
            }

            try
            {
                if (AllyTest.IsAlly(__instance))
                {
                    __result = true;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("ShowSignal postfix failed; the vanilla answer stands", error);
            }
        }
    }

    /// <summary>
    /// The inspect window's own test for "is this one of mine, and may I command it".
    /// Answering yes for an ally that happens to be out of sight is what puts the
    /// command controls on the panel at range.
    /// </summary>
    [HarmonyPatch(typeof(MonsterInspectWindow), "IsFollowerAlly")]
    internal static class IsFollowerAllyPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Creature creature, ref bool __result)
        {
            if (!ModConfig.Enabled || !ModConfig.RemoteOrders || __result)
            {
                return;
            }

            try
            {
                if (AllyTest.IsAlly(creature))
                {
                    __result = true;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("IsFollowerAlly postfix failed; the vanilla answer stands", error);
            }
        }
    }
}
