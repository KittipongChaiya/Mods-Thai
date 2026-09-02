using System;
using HarmonyLib;
using MGSC;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Gives a nemesis its name on screen.
    ///
    /// The game names a creature through a localization key shaped
    /// <c>monster.&lt;mobClassId&gt;.name</c> - confirmed against the game's own
    /// translation tables. Because every nemesis is spawned from its own injected mob
    /// class (see <see cref="MobClassInjector"/>), it already asks for a key no
    /// translation file has: <c>monster.nemesis_7.name</c>. All this patch does is
    /// answer.
    ///
    /// <b>That is why naming needed no per-creature plumbing.</b> The alternative was
    /// to carry a name on the creature instance and patch every place a name is drawn.
    /// Spawning from a real mob class means the name arrives through the game's own
    /// naming path instead, so it is correct everywhere at once - inspect window,
    /// combat log, damage popups, corpse - without this mod knowing where any of those
    /// are.
    ///
    /// <b>On patching Localization.Get.</b> It is the single most contested method in a
    /// typical load order. This postfix touches only keys beginning with
    /// <c>monster.nemesis_</c> and returns instantly for everything else, so it adds no
    /// behaviour any other mod could notice.
    /// </summary>
    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), new[] { typeof(string), typeof(bool) })]
    internal static class LocalizationGetPatch
    {
        private const string Prefix = "monster.nemesis_";

        [HarmonyPostfix]
        internal static void Postfix(string key, ref string __result)
        {
            if (!ModConfig.Enabled || key == null || !key.StartsWith(Prefix, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var resolved = Resolve(key);
                if (resolved != null)
                {
                    __result = resolved;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not resolve the name for '" + key + "'", error);
            }
        }

        /// <summary>
        /// Turns <c>monster.nemesis_7.name</c> back into the row it belongs to. Returns
        /// null for anything that does not match a live record, which leaves the game's
        /// own answer in place.
        /// </summary>
        private static string Resolve(string key)
        {
            var lastDot = key.LastIndexOf('.');
            if (lastDot <= 0)
            {
                return null;
            }

            var suffix = key.Substring(lastDot + 1);
            var mobClassId = key.Substring("monster.".Length, lastDot - "monster.".Length);

            var record = NemesisRoster.ByMobClass(mobClassId);
            if (record == null)
            {
                return null;
            }

            if (string.Equals(suffix, "name", StringComparison.Ordinal))
            {
                return NameForge.FullName(record);
            }

            if (string.Equals(suffix, "desc", StringComparison.Ordinal))
            {
                return record.Rank <= 1
                    ? "Marked. It has seen your face and lived."
                    : "It has killed " + record.Rank + " of your operatives and come back for more.";
            }

            return null;
        }
    }
}
