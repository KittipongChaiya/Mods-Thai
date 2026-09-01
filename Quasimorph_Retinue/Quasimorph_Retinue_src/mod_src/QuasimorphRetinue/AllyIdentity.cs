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
