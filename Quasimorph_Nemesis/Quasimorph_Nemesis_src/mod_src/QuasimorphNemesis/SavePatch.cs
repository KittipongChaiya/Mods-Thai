using System;
using HarmonyLib;
using MGSC;
using SimpleJSON;

namespace QuasimorphNemesis
{
    /// <summary>
    /// Puts the roster in the save file, and takes it out again.
    ///
    /// <b>Why a patch and not a file of our own.</b> A JSON file next to the DLL would
    /// need no patch at all, and would be wrong the first time the player loads an
    /// earlier save - which in a game built around permadeath and save management is
    /// not an edge case but the normal way it is played. Your nemeses have to belong to
    /// the campaign they were made in. <c>ModHookType</c> offers
    /// <c>BeforeSaveLoaded</c> and <c>AfterSaveLoaded</c> but nothing for writing, so
    /// there is no hook-only route to a save-bound roster.
    ///
    /// <b>Additive, and namespaced.</b> Four other mods in a typical load order write to
    /// these same two methods. We read and write exactly one key -
    /// <see cref="NemesisRoster.SaveKey"/> - and never look at, move or remove anything
    /// else in the node. A save written with this mod loads perfectly well without it;
    /// the key is simply ignored.
    /// </summary>
    [HarmonyPatch(typeof(ComponentsLayout), "SerializeGlobalComponents")]
    internal static class SerializeGlobalComponentsPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(JSONNode rootNode)
        {
            if (!ModConfig.Enabled || rootNode == null)
            {
                return;
            }

            try
            {
                rootNode[NemesisRoster.SaveKey] = NemesisRoster.ToJson();
            }
            catch (Exception error)
            {
                // Failing to write the roster loses nemeses. Throwing here would lose
                // the save, so this is caught and reported, never propagated.
                ModLog.Error("could not write the nemesis roster into this save; the " +
                             "save itself is unaffected", error);
            }
        }
    }

    /// <summary>
    /// Reads the roster back. Runs before <c>AfterSaveLoaded</c>, so by the time the
    /// mod syncs its mob classes the rows are already in place.
    /// </summary>
    [HarmonyPatch(typeof(ComponentsLayout), "DeserializeGlobalComponents")]
    internal static class DeserializeGlobalComponentsPatch
    {
        [HarmonyPostfix]
        internal static void Postfix(JSONNode jsonNode)
        {
            if (!ModConfig.Enabled)
            {
                return;
            }

            try
            {
                // Clear first, and unconditionally. Loading a save with no roster must
                // leave no roster - otherwise the previous campaign's enemies follow the
                // player into the new one.
                NemesisRoster.Clear();

                var node = jsonNode?[NemesisRoster.SaveKey];
                if (node == null || node.IsNull)
                {
                    ModLog.Info("this save has no nemesis roster yet");
                    return;
                }

                NemesisRoster.FromJson(node);
            }
            catch (Exception error)
            {
                NemesisRoster.Clear();
                ModLog.Error("could not read the nemesis roster from this save; starting " +
                             "the campaign with an empty one", error);
            }
        }
    }
}
