using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphRuthless
{
    /// <summary>
    /// The name and description shown on the new difficulty panel, in Thai and English.
    ///
    /// The game has no public way to add a localization key with new text.
    /// <c>Localization.DuplicateKey</c> is public but only copies an existing string,
    /// and both <c>Localization.Get</c> overloads read private dictionaries directly
    /// rather than funnelling through one another. So the mod carries its own two
    /// strings and answers for its own two keys - no reflection into the game's
    /// dictionaries, and no dependency on the sibling Thai translation mod.
    /// </summary>
    internal static class ModStrings
    {
        private const string KeyPrefix = "ui.difficulty." + ModInfo.PresetId;

        internal const string NameKey = KeyPrefix + ".name";
        internal const string DescKey = KeyPrefix + ".desc";

        /// <summary>
        /// A vanilla key that always exists, used to ask the game which language is
        /// really in the slot we are about to answer for. See <see cref="IsThaiActive"/>.
        /// </summary>
        private const string ProbeKey = "ui.difficulty.Hard.name";

        // The vanilla ladder reads ปกติ -> ยาก -> ไม่ยุติธรรม (Normal, Hard, Unfair),
        // so the Thai name has to sit above "unfair" without becoming a sentence.
        // ยุทธวิธีไร้ปรานี - "tactical, without mercy" - carries both halves of the
        // English name and still fits the panel.
        private const string ThaiName = "ยุทธวิธีไร้ปรานี";
        private const string EnglishName = "Hardcore Tactical Ruthless";

        // Same shape as the vanilla descriptions: short lines, <br><br> between them,
        // the headline wrapped in the game's own highlight colour. The first line is
        // the design statement, because it is also the thing a player most needs to
        // know before choosing this mode.
        private const string ThaiDesc =
            "<color=#FFFEC1><b>ศัตรูฉลาดขึ้น ไม่ใช่เลือดหนาขึ้น</b></color>" +
            "<br><br>ไล่ล่าต่อเนื่อง ใช้ระเบิดมือ และเปิดประตูตามเข้ามา" +
            "<br><br>HP ศัตรูเท่าเดิม" +
            "<br><br>ของน้อยลง ของที่ปล้นจากศพสภาพแย่ ค่าจ้างน้อยลง" +
            "<br><br>เมื่อตาย ภารกิจปิดและเป้ตกทิ้งไว้" +
            "<br><br>ล็อกความยากตลอดการเล่น";

        private const string EnglishDesc =
            "<color=#FFFEC1><b>Enemies are smarter, not spongier</b></color>" +
            "<br><br>They keep hunting, use grenades, and open doors after you" +
            "<br><br>Enemy HP is untouched" +
            "<br><br>Less loot, worn salvage, tighter pay" +
            "<br><br>Death closes the mission and drops your backpack" +
            "<br><br>Difficulty is locked for the run";

        /// <summary>
        /// Answers for our own two keys, and nothing else.
        /// Returns false for every other key so the game's own lookup runs untouched.
        /// </summary>
        internal static bool TryResolve(string key, out string value)
        {
            value = null;

            // Ordinal prefix test first: this runs for every localized string the game
            // draws, so it must stay cheap and must never allocate.
            if (string.IsNullOrEmpty(key) || !key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var thai = IsThaiActive();
            if (key == NameKey)
            {
                value = thai ? ThaiName : EnglishName;
                return true;
            }
            if (key == DescKey)
            {
                value = thai ? ThaiDesc : EnglishDesc;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Asks the game itself whether the English slot currently holds Thai.
        ///
        /// The sibling Thai mod does not add a Thai language - it replaces the strings
        /// in the EnglishUS slot and repoints the font. So the only honest way to know
        /// which language to serve is to read a vanilla string back and look at it.
        /// Checking the outcome rather than our own bookkeeping keeps this correct no
        /// matter which order the two mods load in.
        ///
        /// Not cached on purpose: the player can change language at runtime, and this
        /// is only ever called for the two keys of one panel.
        /// </summary>
        private static bool IsThaiActive()
        {
            try
            {
                if (Singleton<Localization>.Instance == null)
                {
                    return false;
                }
                // The two-argument overload reads a specific language slot, so this
                // stays correct even when the player currently has Russian selected.
                // ProbeKey is not one of ours, so our own patch passes it straight
                // through to the game - no recursion.
                return ContainsThai(Localization.Get(ProbeKey, Localization.Lang.EnglishUS));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ContainsThai(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            foreach (var c in value)
            {
                // Thai block, U+0E00..U+0E7F.
                if (c >= '฀' && c <= '๿')
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// <c>Localization.Get(string, bool)</c> - the overload the UI labels use.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(bool))]
    internal static class LocalizationGetByFlagPatch
    {
        /// <summary>Returning false skips the original, which is what serves our string.</summary>
        private static bool Prefix(string key, ref string __result)
        {
            return !ModStrings.TryResolve(key, out __result);
        }
    }

    /// <summary>
    /// <c>Localization.Get(string, Lang)</c> - the overload that reads a named language
    /// slot. It does not delegate to the other one, so it needs its own patch.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(Localization.Lang))]
    internal static class LocalizationGetByLangPatch
    {
        private static bool Prefix(string key, ref string __result)
        {
            return !ModStrings.TryResolve(key, out __result);
        }
    }
}
