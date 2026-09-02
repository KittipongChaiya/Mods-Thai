using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QuasimorphSignals
{
    /// <summary>
    /// Every private game member this mod reaches for, resolved once and in one place.
    ///
    /// <b>Why this file exists.</b> <c>tools/apicheck.py</c> resolves every reference in
    /// the built assembly against the real game assemblies, which is what stops this
    /// project shipping a call to a method the game has dropped. It works by walking the
    /// TypeRef and MemberRef metadata tables - and a member fetched by <i>name</i> never
    /// appears in those tables. A string is not a reference. So everything below is
    /// invisible to the build-time check by construction, and has to be verified at
    /// runtime instead. <see cref="PatchVerify"/> is that verification, and this class
    /// is the single list it checks.
    /// </summary>
    internal static class Targets
    {
        internal const string InspectedCreatureField = "_inspectedCreature";
        internal const string FollowButtonField = "_followButton";
        internal const string LeftCaptionField = "_leftCaption";
        internal const string RightCaptionField = "_rightCaption";

        internal static FieldInfo InspectedCreature;
        internal static FieldInfo FollowButton;
        internal static FieldInfo LeftCaption;
        internal static FieldInfo RightCaption;

        /// <summary>Name of each member that could not be found, for the log.</summary>
        internal static readonly List<string> Missing = new List<string>();

        internal static bool UiUsable { get; private set; }

        internal static void Resolve()
        {
            Missing.Clear();

            InspectedCreature = Field(typeof(MonsterInspectWindow), InspectedCreatureField);
            FollowButton = Field(typeof(MonsterInspectWindow), FollowButtonField);
            LeftCaption = Field(typeof(ToggleAllyStateButton), LeftCaptionField);
            RightCaption = Field(typeof(ToggleAllyStateButton), RightCaptionField);

            // The captions are cosmetic: a button with the wrong label still works. The
            // creature and the button it sits beside are not - without them there is
            // nothing to read the stance from and nowhere to put the control.
            UiUsable = InspectedCreature != null && FollowButton != null;
        }

        private static FieldInfo Field(Type type, string name)
        {
            try
            {
                var field = AccessTools.Field(type, name);
                if (field == null)
                {
                    Missing.Add(type.Name + "." + name);
                }
                return field;
            }
            catch (Exception error)
            {
                Missing.Add(type.Name + "." + name + " (" + error.GetType().Name + ")");
                return null;
            }
        }
    }
}
