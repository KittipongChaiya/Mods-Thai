using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 6 - allies stop emptying a shotgun down a corridor.
    ///
    /// <b>The bug is a missing check, not a tuning problem.</b> Reading every path from
    /// an AI deciding to attack to the shot leaving the barrel, the distance gate exists
    /// in exactly one of five:
    ///
    /// <list type="table">
    /// <item><c>Attack.ProcessTacticMode</c> - <b>gated</b>: <c>weaponRecord.Range >= distance</c>,
    ///       else it logs "Target out of eff. range. Approaching"</item>
    /// <item><c>Attack.ProcessDesperateMode</c> - not gated</item>
    /// <item><c>Defense.TryAttack</c> - not gated</item>
    /// <item><c>Rage</c> - not gated</item>
    /// <item><c>FollowTarget.TryAttack</c> - <b>not gated</b>, and this is where every
    ///       escorting ally lives, all of the time</item>
    /// </list>
    ///
    /// Neither function underneath them filters by distance either.
    /// <c>FightState.TryRangeAttack</c> asks only whether the weapon is unbroken and can
    /// fire; <c>AiBehaviour.TryShoot</c> adds a <c>ShootTargetReachable</c> call that
    /// sounds like a range check and is not - it is a raycast for line of fire.
    ///
    /// <b>This is not a new idea.</b> The Workshop mod 'Squad: More operatives' ships
    /// the same prefix on the same method, which is independent confirmation of both the
    /// diagnosis and the remedy. It does not help here because of one line: it gates on
    /// its own <c>IsSquadAlly</c>, so it covers the operatives that mod deploys and
    /// nothing else - not a Retinue guard, not an ally bribed with a gift, not a summon,
    /// not a quest ally. This layer is that patch with its coverage fixed, in the mod
    /// that already knows what an ally is.
    ///
    /// <b>Two things that patch and vanilla both get wrong.</b> Both read
    /// <c>weaponRecord.Range</c>, the raw config value. The real firing path uses
    /// <c>weaponComponent.Range + CreatureData.GetFirearmRangeBonus(record)</c>, and
    /// <c>WeaponComponent.Range</c> is itself the record value plus the ammunition's
    /// range bonus, the weapon's effective-range start and an <c>IAddedEffectiveRange</c>
    /// item trait. Reading the record alone silently ignores ammunition type, item traits
    /// and the creature's own range perks - so an ally carrying long-range rounds would
    /// be told to walk closer for no reason. Every member involved is public.
    ///
    /// <b>Why effective range is the honest boundary.</b> It is not an invented number.
    /// <c>DamageSystem.FalloffDamage</c> begins taking damage away the moment
    /// <c>distance > range</c>. Below it, full damage; above it, the game is already
    /// punishing the shot. Holding fire there is doing what the designers documented in
    /// the one state they gated.
    ///
    /// <b>Enemies are never touched.</b> Every call asks <see cref="AllyTest.IsAlly"/>
    /// first, on the creature in hand. Teaching enemies not to waste ammunition would be
    /// a difficulty change wearing a bug fix's clothes.
    /// </summary>
    internal static class FireDiscipline
    {
        /// <summary>
        /// How many times in a row an ally may decline a shot without the range to its
        /// target shrinking before it is allowed to fire anyway.
        ///
        /// This is the pathological case, and vanilla's own gate has it too: a target
        /// that is visible but unreachable - across a chasm, behind glass, on the far
        /// side of a locked door - would otherwise be circled forever by an ally that
        /// refuses to shoot and cannot get closer. A weak shot beats a frozen bodyguard.
        /// </summary>
        private const int GiveUpAfterDeclines = 6;

        private sealed class Progress
        {
            internal int LastDistance = int.MaxValue;
            internal int Declines;
        }

        /// <summary>Keyed by <c>CreatureData.UniqueId</c>, like every order in this mod.</summary>
        private static readonly Dictionary<int, Progress> Tracked = new Dictionary<int, Progress>();

        /// <summary>Log the first decision per ally only; this runs several times a turn.</summary>
        private static readonly HashSet<int> Logged = new HashSet<int>();

        /// <summary>
        /// Decides whether a shot is worth taking. Returns true to let the game shoot.
        ///
        /// Ordered so that everything which is not our business returns as early and as
        /// cheaply as possible, and so that the ally check happens before any weapon
        /// maths rather than after it.
        /// </summary>
        internal static bool ShouldShoot(FightState state, CellPosition targetPos)
        {
            if (!ModConfig.Enabled || !ModConfig.FireDiscipline || !Targets.FireDisciplineUsable)
            {
                return true;
            }

            var owner = Targets.HasTargetStateOwner.GetValue(state) as Creature;
            if (!AllyTest.IsAlly(owner))
            {
                return true;      // enemies keep vanilla behaviour, exactly
            }

            var data = owner.CreatureData;
            var weapon = data.Inventory?.CurrentWeapon;
            var record = weapon?.Record<WeaponRecord>();
            var component = weapon?.Comp<WeaponComponent>();
            if (record == null || component == null || record.IsMelee)
            {
                return true;      // nothing here has a firing range to reason about
            }

            var distance = CellPosition.Distance(data.Position, targetPos);
            var effectiveRange = component.Range + data.GetFirearmRangeBonus(record);

            if (distance <= effectiveRange)
            {
                Clear(data.UniqueId);
                return true;
            }

            // Out of effective range. Declining is only useful if declining leads to
            // closing the distance - otherwise it just means standing there.
            if (!CanClose(state, owner))
            {
                LogOnce(data, record, distance, effectiveRange,
                        "cannot close, taking the shot anyway");
                return true;
            }

            if (!MakingProgress(data.UniqueId, distance))
            {
                LogOnce(data, record, distance, effectiveRange,
                        "closed no distance in " + GiveUpAfterDeclines +
                        " attempts, taking the shot anyway");
                return true;
            }

            LogOnce(data, record, distance, effectiveRange, "holding fire and closing in");
            return false;
        }

        /// <summary>
        /// Whether declining will actually turn into movement.
        ///
        /// Every caller of <c>TryRangeAttack</c> falls through to a move when the shot is
        /// declined - <c>FollowTarget</c> to <c>TryMoveToTarget</c>, <c>Attack</c> and
        /// <c>Rage</c> to <c>MoveToTarget</c>, <c>Defense</c> to <c>TryWalk</c> - so
        /// returning false is already the whole "get closer" behaviour. It only works
        /// while the ally is able and willing to move, and this is where that is checked.
        ///
        /// <c>FollowTarget.Wait</c> is the player's own hold-position order, given
        /// through the vanilla follow/wait button. An ally told to hold must not silently
        /// refuse to shoot as well.
        /// </summary>
        private static bool CanClose(FightState state, Creature owner)
        {
            if (owner.CreatureData.Immobile || !owner.CanMove())
            {
                return false;
            }

            return !(state is FollowTarget follow) || !follow.Wait;
        }

        /// <summary>
        /// True while the ally is still getting closer. Counts consecutive declines that
        /// did not shorten the range, and gives up after
        /// <see cref="GiveUpAfterDeclines"/> of them.
        /// </summary>
        private static bool MakingProgress(int id, int distance)
        {
            if (id == 0)
            {
                return true;      // unidentifiable: never let the counter trap it
            }

            if (!Tracked.TryGetValue(id, out var progress))
            {
                progress = new Progress();
                Tracked[id] = progress;
            }

            if (distance < progress.LastDistance)
            {
                progress.LastDistance = distance;
                progress.Declines = 0;
                return true;
            }

            progress.LastDistance = distance;
            progress.Declines++;
            return progress.Declines < GiveUpAfterDeclines;
        }

        private static void Clear(int id)
        {
            if (id != 0)
            {
                Tracked.Remove(id);
                Logged.Remove(id);
            }
        }

        /// <summary>
        /// Forgets every ally's approach history. Called on a floor change, where
        /// distances mean nothing any more.
        /// </summary>
        internal static void Reset()
        {
            Tracked.Clear();
            Logged.Clear();
        }

        private static void LogOnce(CreatureData data, WeaponRecord record, int distance,
                                    int effectiveRange, string decision)
        {
            if (!Logged.Add(data.UniqueId))
            {
                return;
            }

            ModLog.Info("ally " + data.UniqueId + " with " + record.Id + ": target at " +
                        distance + ", effective range " + effectiveRange + " - " + decision);
        }
    }

    /// <summary>
    /// The gate itself.
    ///
    /// <c>TryRangeAttack</c> has two overloads; this is the <c>CellPosition</c> one,
    /// which the other delegates to, so patching it covers every fight state at once.
    /// Its name reaches Harmony as a string, which <c>tools/apicheck.py</c> structurally
    /// cannot verify - <see cref="PatchVerify"/> is what checks it at runtime.
    /// </summary>
    [HarmonyPatch(typeof(FightState), "TryRangeAttack", new[] { typeof(CellPosition) })]
    internal static class TryRangeAttackPatch
    {
        [HarmonyPrefix]
        internal static bool Prefix(FightState __instance, CellPosition targetPos, ref bool __result)
        {
            try
            {
                if (FireDiscipline.ShouldShoot(__instance, targetPos))
                {
                    return true;
                }

                // False here is not "the shot failed" so much as "not from here". Every
                // caller reads it as a cue to close the distance instead.
                __result = false;
                return false;
            }
            catch (Exception error)
            {
                ModLog.Error("fire discipline failed; the shot is left entirely to the " +
                             "game to decide", error);
                return true;
            }
        }
    }
}
