using System;
using System.Collections.Generic;
using MGSC;

namespace QuasimorphRetinue
{
    /// <summary>
    /// Layer 3 - turning enemies into allies with the mechanic the game already has.
    ///
    /// Drop an item where a creature can see it. If that creature's AI preset lists the
    /// item's class in <c>ItemsClassesAsGifts</c>, it breaks off whatever it was doing -
    /// idling, investigating, panicking, surrendering, or attacking you - walks to the
    /// item, picks it up, and:
    ///
    /// <code>
    /// _owner.Behaviour.StartFollowing(_creatures.Player);
    /// _owner.CreatureData.CreatureAlliance = CreatureAlliance.PlayerAlliance;
    /// </code>
    ///
    /// That is vanilla, wired into nine different AI state transitions, and it needs no
    /// new code from a mod at all. It is simply switched off for almost every creature,
    /// because almost every gift list ships empty. This layer fills them in.
    ///
    /// <b>This is the one place the mod writes to a shared config record</b>, so it
    /// carries the same discipline the sibling Ruthless mod uses: snapshot before
    /// writing anything, and restore on demand. A record is global state for the whole
    /// session, and a player who turns this off mid-session must get the vanilla
    /// behaviour back without restarting the game.
    ///
    /// Two rules keep it honest:
    ///
    /// 1. <b>Only creatures that already reason.</b> A preset that uses items, throws
    ///    grenades or picks a firemode is a thinking humanoid that can weigh a bribe.
    ///    A mindless horror does not learn to accept a gift because a list grew.
    /// 2. <b>Only classes a bribe is plausibly made of.</b> Food, drink, medicine and
    ///    valuables. Not weapons, not ammunition, not quest items - an enemy that
    ///    defects for a dropped rifle turns every firefight into an auction.
    /// </summary>
    internal static class Recruiting
    {
        /// <summary>
        /// What an enemy will change sides for. Deliberately things you carry spares of
        /// and can afford to lose, so recruiting costs a real decision about supplies
        /// rather than a spare click.
        /// </summary>
        private static readonly ItemClass[] Bribes =
        {
            ItemClass.Food,
            ItemClass.Drink,
            ItemClass.Alcohol,
            ItemClass.Pills,
            ItemClass.Medpack,
            ItemClass.ValuableBarter,
        };

        private static readonly List<Snapshot> Vanilla = new List<Snapshot>();
        private static bool _applied;
        private static bool _snapshotTaken;

        /// <summary>
        /// The gift lists as the game shipped them. A null list is recorded as null and
        /// restored as null, because "no list" and "empty list" are not the same thing
        /// to code that may dereference it.
        /// </summary>
        private sealed class Snapshot
        {
            internal AiPresetRecord Record;
            internal List<ItemClass> Classes;
            internal List<string> Ids;
        }

        internal static void CaptureVanilla()
        {
            if (_snapshotTaken)
            {
                return;
            }

            var presets = Data.AiPresets;
            if (presets == null)
            {
                ModLog.Error("Data.AiPresets is null; recruiting is unavailable");
                return;
            }

            foreach (var record in presets.Records)
            {
                if (record == null)
                {
                    continue;
                }
                Vanilla.Add(new Snapshot
                {
                    Record = record,
                    Classes = record.ItemsClassesAsGifts == null
                        ? null
                        : new List<ItemClass>(record.ItemsClassesAsGifts),
                    Ids = record.ItemsIdsAsGifts == null
                        ? null
                        : new List<string>(record.ItemsIdsAsGifts),
                });
            }

            _snapshotTaken = true;
            ModLog.Info("recruiting: snapshotted " + Vanilla.Count + " ai presets");
        }

        internal static void Apply()
        {
            if (_applied || !_snapshotTaken || !ModConfig.Recruiting)
            {
                return;
            }

            var widened = 0;
            var alreadyWilling = 0;
            foreach (var vanilla in Vanilla)
            {
                if (WasAlreadyWilling(vanilla))
                {
                    alreadyWilling++;
                    continue;
                }

                if (!IsThinker(vanilla.Record))
                {
                    continue;
                }

                var classes = vanilla.Record.ItemsClassesAsGifts;
                if (classes == null)
                {
                    classes = new List<ItemClass>();
                    vanilla.Record.ItemsClassesAsGifts = classes;
                }

                foreach (var bribe in Bribes)
                {
                    if (!classes.Contains(bribe))
                    {
                        classes.Add(bribe);
                    }
                }
                widened++;
            }

            _applied = true;
            ModLog.Info("recruiting ON: " + widened + " ai presets can now be bribed, " +
                        alreadyWilling + " already could in vanilla, " +
                        (Vanilla.Count - widened - alreadyWilling) + " left mindless");
        }

        internal static void Restore()
        {
            if (!_applied)
            {
                return;
            }

            foreach (var vanilla in Vanilla)
            {
                vanilla.Record.ItemsClassesAsGifts = vanilla.Classes == null
                    ? null
                    : new List<ItemClass>(vanilla.Classes);
                vanilla.Record.ItemsIdsAsGifts = vanilla.Ids == null
                    ? null
                    : new List<string>(vanilla.Ids);
            }

            _applied = false;
            ModLog.Info("recruiting OFF: " + Vanilla.Count + " ai presets restored to vanilla");
        }

        /// <summary>
        /// A creature the designers already made bribable. Left exactly as it was: the
        /// specific thing it wants is a piece of that creature's design, and replacing
        /// it with a generic list would flatten something the game got right.
        /// </summary>
        private static bool WasAlreadyWilling(Snapshot vanilla)
        {
            return (vanilla.Classes != null && vanilla.Classes.Count > 0) ||
                   (vanilla.Ids != null && vanilla.Ids.Count > 0);
        }

        /// <summary>
        /// The same test the sibling Ruthless mod uses to decide who gets to open a
        /// door: a preset that already reasons about its equipment is a creature that
        /// can reason about a bribe.
        /// </summary>
        private static bool IsThinker(AiPresetRecord record)
        {
            return record.CanUseItems ||
                   record.GrenadeChance > 0f ||
                   record.BestFiremodeChance > 0f;
        }
    }
}
