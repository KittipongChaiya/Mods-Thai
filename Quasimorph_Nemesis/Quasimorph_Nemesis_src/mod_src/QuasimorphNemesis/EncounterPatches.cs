using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Watching for the two moments that matter: you killed it, or it killed you.
    ///
    /// Both are observations. Neither patch changes a return value or skips an original
    /// - they read what happened and write a number into the roster. That is the whole
    /// reason this mod can sit alongside the other mods that patch the same two places.
    /// </summary>
    internal static class EncounterPatches
    {
    }

    /// <summary>
    /// Remembers who last hurt the player's mercenary.
    ///
    /// <c>DamageHitInfo.damageDealer</c> is a public field carrying the creature that
    /// dealt the blow, which is the only reliable way to attribute a death - by the time
    /// <c>OnPlayerDied</c> runs, the fight is over and the attacker is no longer in
    /// scope. Held rather than acted on, because a hit is not yet a death.
    /// </summary>
    [HarmonyPatch(typeof(Player), "ProcessDamage")]
    internal static class PlayerProcessDamagePatch
    {
        internal static Creature LastAttacker;

        [HarmonyPostfix]
        internal static void Postfix(DamageHitInfo hitInfo)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                if (hitInfo.damageDealer != null)
                {
                    LastAttacker = hitInfo.damageDealer;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not record the attacker", error);
            }
        }
    }

    /// <summary>
    /// The player's mercenary died. Whoever last hit it earns a rank, if it is one of
    /// ours.
    /// </summary>
    [HarmonyPatch(typeof(DungeonGameMode), "OnPlayerDied")]
    internal static class OnPlayerDiedPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                Encounters.OnPlayerKilledBy(PlayerProcessDamagePatch.LastAttacker);
            }
            catch (Exception error)
            {
                ModLog.Error("could not credit the kill", error);
            }
            finally
            {
                // Never let one death be credited twice, whatever happens above.
                PlayerProcessDamagePatch.LastAttacker = null;
            }
        }
    }

    /// <summary>
    /// Something died. If it was a nemesis, the player has finally won and the row is
    /// retired.
    /// </summary>
    [HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.KillMonster))]
    internal static class KillMonsterPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(Creature creature)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                Encounters.OnMonsterKilled(creature);
            }
            catch (Exception error)
            {
                ModLog.Error("could not retire the killed nemesis", error);
            }
        }
    }
}
