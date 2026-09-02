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
        internal const string CloseButtonField = "_closeButton";

        /// <summary>
        /// The compiler-generated backing field of <c>CommonButton.OnClick</c>. An
        /// event cannot be assigned from outside its declaring class, and a cloned
        /// button whose inherited handlers cannot be cleared is a button that might do
        /// two things at once - so this is required, not cosmetic.
        /// </summary>
        internal const string CommonButtonOnClickField = "OnClick";

        /// <summary>
        /// The creature an AI state belongs to. Declared on <c>HasTargetState</c>, not on
        /// <c>FightState</c>, and private - so a patch on a fight state has no supported
        /// way to ask whose turn it is running. The Workshop mod 'Squad: More operatives'
        /// reaches the same field the same way, for the same reason.
        /// </summary>
        internal const string HasTargetStateOwnerField = "_owner";

        internal static FieldInfo InspectedCreature;
        internal static FieldInfo FollowButton;
        internal static FieldInfo LeftCaption;
        internal static FieldInfo RightCaption;
        internal static FieldInfo CloseButton;
        internal static FieldInfo CommonButtonOnClick;
        internal static FieldInfo HasTargetStateOwner;

        /// <summary>Name of each member that could not be found, for the log.</summary>
        internal static readonly List<string> Missing = new List<string>();

        internal static bool UiUsable { get; private set; }

        internal static bool MoveButtonUsable { get; private set; }

        internal static bool FireDisciplineUsable { get; private set; }

        internal static void Resolve()
        {
            Missing.Clear();

            InspectedCreature = Field(typeof(MonsterInspectWindow), InspectedCreatureField);
            FollowButton = Field(typeof(MonsterInspectWindow), FollowButtonField);
            LeftCaption = Field(typeof(ToggleAllyStateButton), LeftCaptionField);
            RightCaption = Field(typeof(ToggleAllyStateButton), RightCaptionField);
            CloseButton = Field(typeof(MonsterInspectWindow), CloseButtonField);
            CommonButtonOnClick = Field(typeof(CommonButton), CommonButtonOnClickField);
            HasTargetStateOwner = Field(typeof(HasTargetState), HasTargetStateOwnerField);

            // The captions are cosmetic: a button with the wrong label still works. The
            // creature and the button it sits beside are not - without them there is
            // nothing to read the stance from and nowhere to put the control.
            UiUsable = InspectedCreature != null && FollowButton != null;

            // The Move control needs two more members, and degrades on its own without
            // taking the stance control with it - so it has its own flag rather than
            // widening UiUsable.
            MoveButtonUsable = UiUsable && CloseButton != null && CommonButtonOnClick != null;

            // Fire discipline needs nothing from the UI at all - it is a decision made
            // inside the AI - so it stands or falls on its own single field.
            FireDisciplineUsable = HasTargetStateOwner != null;
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
