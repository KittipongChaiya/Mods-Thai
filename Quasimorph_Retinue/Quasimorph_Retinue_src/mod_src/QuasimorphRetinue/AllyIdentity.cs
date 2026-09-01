using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphRetinue
{
    /// <summary>
    /// The single definition of "is an ally", and the only place this mod decides
    /// whether a creature is on your side.
    ///
    /// This is the guarantee the whole mod rests on. The sibling Hardcore Tactical
    /// Ruthless mod works on <c>Data.AiPresets</c> and <c>Data.MobClasses</c> - shared
    /// config records - and can therefore only change the world as a whole. Its notes
    /// record that allies could not be told apart from enemies, and at that level
    /// they cannot: an ally and the enemy it just defected from are the same mob class
    /// reading the same AI preset.
    ///
    /// One level down they are trivially distinguishable. <c>CreatureAlliance</c> is a
    /// field on every individual <c>CreatureData</c>, so this mod works on creature
    /// *instances* and never writes to a shared record - except in
    /// <see cref="Recruiting"/>, which is explicit about it and restores what it
    /// changed. That is why nothing here can make an enemy stronger: there is no code
    /// path from an ally instance to a hostile one.
    /// </summary>
    internal static class AllyIdentity
    {
        /// <summary>
        /// True for a creature that fights on the player's side and is not the player.
        ///
        /// The player's own mercenary is <c>PlayerAlliance</c> too, and is deliberately
        /// excluded everywhere: this mod makes your squad strong, not you.
        /// </summary>
        internal static bool IsAlly(Creature creature)
        {
            return creature is Monster monster &&
                   monster.CreatureData != null &&
                   monster.CreatureData.CreatureAlliance == CreatureAlliance.PlayerAlliance;
        }

        /// <summary>
        /// True for an ally that is really one of the player's own roster mercenaries
        /// wearing a monster's clothes.
        ///
        /// Nothing in vanilla does this - the game fields exactly one mercenary and
        /// every other ally is a monster. Mods do: the Workshop mod <i>Squad: More
        /// operatives</i> deploys your other operatives as player-alliance monsters so
        /// the AI can drive them, and they are indistinguishable from a recruited thug
        /// by alliance alone.
        ///
        /// They must be, though, because a mercenary is a persistent character. Writing
        /// strength onto one is not buffing a minion, it is permanently buffing the
        /// player's own roster - and those stats would follow the character back to the
        /// ship, into the next raid, and into the save forever. So this mod counts them
        /// as bodies in the squad and refuses to touch their stats.
        ///
        /// The test is the one that mod uses on itself: does this creature's
        /// <c>CreatureData</c> belong, by reference, to a mercenary on the roster.
        /// </summary>
        internal static bool IsPlayerMercenary(State state, Creature creature)
        {
            var data = creature?.CreatureData;
            if (data == null)
            {
                return false;
            }

            List<Mercenary> roster;
            try
            {
                roster = state?.Get<Mercenaries>()?.Values;
            }
            catch (Exception)
            {
                // Not knowing is not a licence to write. Treat it as "yes, leave alone".
                return true;
            }

            if (roster == null)
            {
                return false;
            }

            foreach (var mercenary in roster)
            {
                // Reference identity on purpose: two mercenaries can hold equal-looking
                // data, but only one object is this creature.
                if (mercenary != null && ReferenceEquals(mercenary.CreatureData, data))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Every living ally on the current floor, however it was acquired - spawned by
        /// this mod, recruited with a gift, converted by a perk, summoned, or handed
        /// over by a quest. The layers above deliberately do not care which.
        /// </summary>
        internal static List<Monster> Living(Creatures creatures)
        {
            var allies = new List<Monster>();
            if (creatures?.Monsters == null)
            {
                return allies;
            }

            foreach (var creature in creatures.Monsters)
            {
                if (IsAlly(creature) && creature.CreatureData.Health != null &&
                    creature.CreatureData.Health.Alive)
                {
                    allies.Add((Monster)creature);
                }
            }
            return allies;
        }

        /// <summary>
        /// Whether the mod should be doing anything at all right now.
        ///
        /// <b>Fails closed.</b> If the difficulty cannot be read and the player asked
        /// for a difficulty restriction, the answer is no and the game stays vanilla -
        /// the same rule the sibling mod uses, for the same reason.
        /// </summary>
        internal static bool ShouldRun(State state)
        {
            if (!ModConfig.Enabled)
            {
                return false;
            }

            if (ModConfig.OnlyOnDifficulty.Length == 0)
            {
                return true;
            }

            if (state == null)
            {
                return false;
            }

            try
            {
                var preset = state.Get<Difficulty>()?.Preset;
                if (preset == null)
                {
                    return false;
                }
                return string.Equals(preset.Id, ModConfig.OnlyOnDifficulty, StringComparison.Ordinal);
            }
            catch (Exception error)
            {
                if (!_difficultyReadFailureLogged)
                {
                    _difficultyReadFailureLogged = true;
                    ModLog.Error("could not read the active difficulty from State; " +
                                 "only_on_difficulty is set, so every layer stays off " +
                                 "and the game stays vanilla", error);
                }
                return false;
            }
        }

        private static bool _difficultyReadFailureLogged;
    }
}
