using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 1 - what an ally has been told to do, and making sure it keeps doing it.
    ///
    /// <b>This layer applies no Harmony patches.</b> Every stance is reachable through
    /// public game API:
    ///
    /// <code>
    /// roam   : Behaviour.SetEndlessHunt(true, force: true)
    /// escort : Behaviour.StartFollowing(player)
    /// </code>
    ///
    /// That is deliberate, and it is why this file is testable on its own: set
    /// <c>default_roam=true</c> in config.txt and the whole roaming behaviour can be
    /// verified in game before a single patch exists. The patches in this mod exist
    /// only to give you a <i>button</i> and to let you reach an ally you cannot see -
    /// never to make the behaviour itself work.
    ///
    /// <b>Roam does not replace the vanilla follow control.</b> The game's own
    /// follow/wait button is a <c>ToggleAllyStateButton</c>, whose state is a
    /// <c>Side</c> - an enum of exactly Left and Right. It structurally cannot hold a
    /// third value, which is why the Workshop mod that tries to cycle three states
    /// through it is unreliable. Roam is therefore its own separate two-state control,
    /// and vanilla follow/wait keeps working exactly as it always did.
    /// </summary>
    internal static class AllyOrders
    {
        /// <summary>
        /// Keyed by <c>CreatureData.UniqueId</c>, which is stable across saves, loads
        /// and floors - unlike the creature object, which is not.
        /// </summary>
        private static readonly Dictionary<int, bool> Roaming = new Dictionary<int, bool>();

        /// <summary>
        /// A leak guard, not a correctness rule. Ids are never reused within a run and
        /// a stale entry costs one int, so pruning precisely would be more risk than it
        /// removes: an ally riding an elevator is briefly absent from the creature list,
        /// and a keen pruner would forget its orders exactly then.
        /// </summary>
        private const int MaxRemembered = 512;

        internal static bool IsRoaming(Creature ally)
        {
            var id = AllyTest.IdOf(ally);
            return id != 0 && Roaming.TryGetValue(id, out var roaming)
                ? roaming
                : ModConfig.DefaultRoam;
        }

        /// <summary>
        /// Record an order and carry it out now. Returns the stance actually in force,
        /// which is the old one if the creature was not something we may command.
        /// </summary>
        internal static bool Set(Creature creature, bool roam, Creatures creatures)
        {
            if (!AllyTest.IsAlly(creature))
            {
                return false;
            }

            var id = AllyTest.IdOf(creature);
            if (id != 0)
            {
                if (Roaming.Count >= MaxRemembered && !Roaming.ContainsKey(id))
                {
                    Roaming.Clear();
                    ModLog.Info("order memory reached " + MaxRemembered + " allies and was reset");
                }
                Roaming[id] = roam;
            }

            Apply((Monster)creature, roam, creatures);
            return roam;
        }

        internal static bool Toggle(Creature creature, Creatures creatures)
        {
            return Set(creature, !IsRoaming(creature), creatures);
        }

        /// <summary>
        /// Re-assert every standing order once per turn.
        ///
        /// <b>Only where the ally has actually drifted.</b> <c>IsEndlessHunt</c> is a
        /// public getter, so disagreement is cheap to detect, and acting only on
        /// disagreement matters: calling <c>StartFollowing</c> unconditionally every
        /// turn would reset the follow state under an ally that was already following
        /// and make it stutter in a doorway.
        /// </summary>
        internal static void Sweep(State state)
        {
            var creatures = state?.Get<Creatures>();
            if (creatures?.Monsters == null || creatures.Player == null)
            {
                return;
            }

            foreach (var creature in creatures.Monsters)
            {
                if (!AllyTest.IsAlly(creature))
                {
                    continue;
                }

                var health = creature.CreatureData?.Health;
                if (health == null || !health.Alive)
                {
                    continue;
                }

                var ally = (Monster)creature;
                var wanted = IsRoaming(ally);
                try
                {
                    if (ally.Behaviour != null && ally.Behaviour.IsEndlessHunt != wanted)
                    {
                        Apply(ally, wanted, creatures);
                    }
                }
                catch (Exception error)
                {
                    ModLog.Error("could not re-assert the order for ally " +
                                 AllyTest.IdOf(ally), error);
                }
            }
        }

        private static void Apply(Monster ally, bool roam, Creatures creatures)
        {
            try
            {
                if (ally?.Behaviour == null)
                {
                    return;
                }

                if (roam)
                {
                    // The same call the sibling Retinue mod makes for stance=hunter,
                    // and the same one vanilla makes for a hunting group.
                    ally.Behaviour.SetEndlessHunt(value: true, force: true);
                }
                else
                {
                    ally.Behaviour.SetEndlessHunt(value: false, force: true);
                    if (creatures?.Player != null)
                    {
                        // FollowTarget is a FightState: it shoots what it sees and
                        // follows when there is nothing to shoot. It is also the state
                        // the game's own follow/wait and shoot/hold-fire buttons read
                        // and write, so those keep working on this ally afterwards.
                        ally.Behaviour.StartFollowing(creatures.Player);
                    }
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not set the stance on ally " + AllyTest.IdOf(ally) +
                             "; it keeps whatever the game gave it", error);
            }
        }
    }
}
