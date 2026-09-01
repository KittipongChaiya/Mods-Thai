using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// Layer 1 - the difficulty preset itself.
    ///
    /// Every value here is a <b>delta from the vanilla Hard preset</b>, never an
    /// absolute number. That is deliberate: the mod inherits whatever baseline the
    /// game ships, so a patch that retunes Hard can never leave this mode sitting
    /// below the difficulty it is supposed to sit above.
    ///
    /// Three tests govern what is allowed in this table. A knob ships only if it
    /// passes all three:
    ///
    ///   NOT A CHEAT   does it help the player in any way?
    ///   NOT A SPONGE  does it just make the same fight take longer?
    ///   ANSWERABLE    can good play beat it?
    ///
    /// <see cref="Held"/> documents the knobs deliberately left alone, and why.
    /// That list is as much a part of the design as the multipliers are.
    /// </summary>
    internal static class PresetTuning
    {
        // -------------------------------------------------------- enemy competence
        //
        // Enemies get better at fighting, not harder to kill. An enemy that sees you
        // first, acts more often and shrugs off the wrong ammo is harder to out-think.
        // An enemy with more hit points is only harder to out-click - which is why
        // EnemyHealth is in Held below rather than here.

        /// <summary>Mistakes cost more, so cover and range discipline matter.</summary>
        internal const float EnemyDamageMult = 1.20f;

        /// <summary>Ammo and damage-type choice becomes a real decision.</summary>
        internal const float EnemyResistance = 1.10f;

        /// <summary>More enemy turns per round of yours. Tempo pressure, not padding.</summary>
        internal const float EnemyActionPoint = 1.15f;

        /// <summary>They see you first. This is the single most tactical knob here:
        /// it rewards approach, positioning and patience instead of reflexes.</summary>
        internal const float EnemyLos = 1.30f;

        /// <summary>Long-range potshots stop being free.</summary>
        internal const float EnemyDodgeMult = 1.10f;

        // ------------------------------------------------------------- pressure
        /// <summary>More bodies per map, so fights are engagements rather than duels.</summary>
        internal const float MonsterPoints = 1.30f;

        /// <summary>The corruption clock runs faster - a timer you must plan against.</summary>
        internal const float QmorphLevelGrowth = 1.25f;

        /// <summary>And it bites harder when it does.</summary>
        internal const float QmorphStatsAffect = 1.20f;

        /// <summary>A less predictable galaxy. Events are content, not punishment.</summary>
        internal const float RndEventsChance = 1.25f;

        // ------------------------------------------------------------- scarcity
        //
        // Scarcity is what turns a shooting gallery into a series of decisions:
        // what to carry, what to leave, what a fight is actually worth.

        /// <summary>Fewer items lying around the map.</summary>
        internal const float ItemPoints = 0.70f;

        /// <summary>What you strip off a corpse is worn. Salvage is a compromise.</summary>
        internal const float KilledMobsItemsCondition = 0.60f;

        /// <summary>Selling is worth less, so hoarding is not a strategy.</summary>
        internal const float BarterValue = 0.80f;

        /// <summary>Mission pay is tighter.</summary>
        internal const float MissionRewardPoints = 0.85f;

        /// <summary>More XP is required per perk, so progression is earned.</summary>
        internal const float ExpMult = 1.25f;

        /// <summary>Standing with factions is slower to build.</summary>
        internal const float FactionReputation = 0.85f;

        /// <summary>Contracts expire sooner. Deciding is part of the game.</summary>
        internal const float ProcMissionLifetime = 0.85f;

        /// <summary>Weight burns more calories, so your loadout is a real choice.</summary>
        internal const float WeightSatietyDrainMult = 1.20f;

        /// <summary>
        /// Knobs deliberately NOT touched, and the reason. Kept as documentation
        /// rather than deleted, because "why is this not tuned?" is the question a
        /// future reader will actually have.
        ///
        /// <list type="bullet">
        /// <item><b>EnemyHealth</b> - the sponge knob. Fails the not-a-sponge test by
        /// definition: it adds shots per kill and nothing else. Held at Hard's value
        /// on purpose, and this is the single clearest statement of the mod's design.</item>
        ///
        /// <item><b>MissionStageCountMod</b> - more floors is more minutes, not more
        /// difficulty. Fails not-a-sponge.</item>
        ///
        /// <item><b>MagnumCraftingTime</b> - the field is named "Time" but the game's
        /// own UI label reads "crafting speed". Those imply opposite directions and
        /// the difference is unverified, so tuning it would be a coin flip on whether
        /// the mode gets harder or easier. Left alone until the probe answers it.</item>
        ///
        /// <item><b>FactionGrowthSpeed</b> - it is genuinely unclear whether faster
        /// faction growth is harder or easier for the player. Unverified, so untouched.</item>
        ///
        /// <item><b>StartingMercCount / StartingClassesCount</b> - changing the size of
        /// the starting squad is a product decision about what the mode is, not a
        /// difficulty tuning knob. Inherited from Hard.</item>
        ///
        /// <item><b>LoseMissionOnEvacuation / ForbidKillFaction</b> - inherited, so the
        /// mode stays recognisable as Unfair-plus rather than a different game.</item>
        /// </list>
        /// </summary>
        internal static class Held
        {
        }

        /// <summary>
        /// Copies every field of <paramref name="source"/> into a new preset, then
        /// applies the deltas above and the absolute settings.
        ///
        /// The copy is written out property by property rather than by reflection.
        /// It is longer, but it means a field the game adds in a future version fails
        /// visibly at compile time instead of silently defaulting to zero on a preset
        /// the player selected.
        /// </summary>
        internal static DifficultyPreset Derive(DifficultyPreset source, string newId)
        {
            var preset = new DifficultyPreset
            {
                Id = newId,

                // The icon. AddPanel dereferences this without a null check, so a
                // preset without a descriptor throws and takes the whole difficulty
                // screen with it. Sharing Hard's descriptor is what keeps us safe
                // and costs nothing - the mode simply wears the Unfair icon.
                ContentDescriptor = source.ContentDescriptor,

                // ---- inherited verbatim -------------------------------------
                EnemyHealth = source.EnemyHealth,
                MagnumCraftingTime = source.MagnumCraftingTime,
                MissionStageCountMod = source.MissionStageCountMod,
                FactionGrowthSpeed = source.FactionGrowthSpeed,
                StartingMercCount = source.StartingMercCount,
                StartingClassesCount = source.StartingClassesCount,
                LoseMissionOnEvacuation = source.LoseMissionOnEvacuation,
                ForbidKillFaction = source.ForbidKillFaction,
                RndMercsAtStart = source.RndMercsAtStart,
                RndClassesAtStart = source.RndClassesAtStart,
                RndStartingEquip = source.RndStartingEquip,
                RndStartLocation = source.RndStartLocation,

                // ---- enemy competence ---------------------------------------
                EnemyDamageMult = source.EnemyDamageMult * EnemyDamageMult,
                EnemyResistance = source.EnemyResistance * EnemyResistance,
                EnemyActionPoint = source.EnemyActionPoint * EnemyActionPoint,
                EnemyLos = source.EnemyLos * EnemyLos,
                EnemyDodgeMult = source.EnemyDodgeMult * EnemyDodgeMult,

                // ---- pressure -----------------------------------------------
                MonsterPoints = source.MonsterPoints * MonsterPoints,
                QmorphLevelGrowth = source.QmorphLevelGrowth * QmorphLevelGrowth,
                QmorphStatsAffect = source.QmorphStatsAffect * QmorphStatsAffect,
                RndEventsChance = source.RndEventsChance * RndEventsChance,

                // ---- scarcity -----------------------------------------------
                ItemPoints = source.ItemPoints * ItemPoints,
                KilledMobsItemsCondition = source.KilledMobsItemsCondition * KilledMobsItemsCondition,
                BarterValue = source.BarterValue * BarterValue,
                MissionRewardPoints = source.MissionRewardPoints * MissionRewardPoints,
                ExpMult = source.ExpMult * ExpMult,
                FactionReputation = source.FactionReputation * FactionReputation,
                ProcMissionLifetime = source.ProcMissionLifetime * ProcMissionLifetime,
                WeightSatietyDrainMult = source.WeightSatietyDrainMult * WeightSatietyDrainMult,

                // ---- consequences -------------------------------------------
                //
                // Harsh but recoverable, by design. A bad half hour should cost you a
                // mission and a backpack, not a campaign - the run-to-run loop is
                // where this game is fun, and permadeath quietly deletes it.
                DeathPenalty = DeathPenalty.DieButMissionGone,
                RevivePenalty = RevivePenalty.TimePenalty,
                DropPenalty = DropPenalty.Bag,
                EvacRules = EvacRules.ByChip,
                DeathGift = false,
                LosePerks = false,
                LoseRank = false,

                // ---- starting state and quality of life ---------------------
                StartingEquip = StartingEquip.Low,
                EquipRepairAfterMission = false,
                BackpacksSize = BackpackSize.X1,
                ItemsStackSize = ItemStacksSize.X1,
                SmoothProgression = false,
                Tutorial = false,

                // Moving between floors costs a turn, so a retreat upstairs is a
                // decision with a price rather than a free reset.
                SpendAPAtElevator = true,

                RndEventsEnabled = true,

                // You chose hardcore. The run holds you to it.
                ImmutableDifficulty = true,
            };

            return preset;
        }
    }
}
