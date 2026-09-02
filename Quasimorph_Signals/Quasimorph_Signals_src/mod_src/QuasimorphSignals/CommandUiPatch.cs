using System;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;

namespace QuasimorphSignals
{
    /// <summary>
    /// Layer 2 - the Escort/Roam control on the ally inspect panel.
    ///
    /// <b>This does not touch the vanilla follow button.</b> That button is a
    /// <c>ToggleAllyStateButton</c> whose state is a <c>Side</c>, an enum of exactly
    /// <c>Left</c> and <c>Right</c>. It cannot hold a third value, so the Workshop mod
    /// that tries to cycle Follow / Roam / Stand through it is fighting the type
    /// system, which is the most likely reason its Roam option often fails to appear.
    ///
    /// Instead we clone that button into a second two-state control beside it, meaning
    /// Escort on the left and Roam on the right. Vanilla follow/wait and
    /// shoot/hold-fire keep working untouched, and the clone inherits the game's own
    /// styling, layout and input handling for free.
    ///
    /// The click needs no patch at all: <c>OnValueChanged</c> is a public event.
    /// </summary>
    [HarmonyPatch(typeof(MonsterInspectWindow), "RefreshFollowButton")]
    internal static class RefreshFollowButtonPatch
    {
        private const string CloneName = "QuasimorphSignalsRoamToggle";

        /// <summary>
        /// Set while we drive the toggle ourselves. <c>ToggleLeft</c> and
        /// <c>ToggleRight</c> raise <c>OnValueChanged</c>, so without this the act of
        /// displaying the current stance would be read back as an order to change it.
        /// </summary>
        [ThreadStatic] private static bool _settingState;

        private static bool _loggedCreated;

        [HarmonyPostfix]
        internal static void Postfix(MonsterInspectWindow __instance)
        {
            if (!ModConfig.Enabled || !ModConfig.CommandUi || !Targets.UiUsable ||
                __instance == null)
            {
                return;
            }

            try
            {
                var vanilla = Targets.FollowButton.GetValue(__instance) as ToggleAllyStateButton;
                if (vanilla == null)
                {
                    return;
                }

                var creature = Targets.InspectedCreature.GetValue(__instance) as Creature;

                // The move control is refreshed first, and outside the yield below.
                //
                // Yielding exists because 'Ally Roam/Patrol' and this mod both relabel
                // the *vanilla* follow button, and two mods writing one control on the
                // same callback is a coin toss. The move control is a new button of our
                // own with its own name; nothing else writes to it, so there is nothing
                // to yield and no reason to withhold it from a player who happens to
                // run that mod. It is also refreshed before the ally check below, so
                // that it can hide itself when an enemy is inspected - the window is
                // pooled, and a control left visible from the last ally would otherwise
                // appear on a monster.
                MoveButton.Refresh(__instance, vanilla, creature);

                if (ConflictCheck.YieldUi)
                {
                    return;
                }

                var toggle = Ensure(vanilla);
                if (toggle == null)
                {
                    return;
                }

                // Only allies get the control. An enemy inspect window must look
                // exactly as it always did.
                var isAlly = AllyTest.IsAlly(creature);
                toggle.gameObject.SetActive(isAlly);
                if (!isAlly)
                {
                    return;
                }

                _settingState = true;
                try
                {
                    if (AllyOrders.IsRoaming(creature))
                    {
                        toggle.ToggleRight();
                    }
                    else
                    {
                        toggle.ToggleLeft();
                    }
                }
                finally
                {
                    _settingState = false;
                }
            }
            catch (Exception error)
            {
                ModLog.Error("could not refresh the roam control; the vanilla panel is " +
                             "unaffected", error);
            }
        }

        /// <summary>
        /// Finds our clone under the same parent as the vanilla button, creating it on
        /// first use. Looked up by name rather than cached, because the inspect window
        /// is pooled and a cached reference would outlive the object it points at.
        /// </summary>
        private static ToggleAllyStateButton Ensure(ToggleAllyStateButton vanilla)
        {
            var parent = vanilla.transform.parent;
            if (parent == null)
            {
                return null;
            }

            var existing = parent.Find(CloneName);
            if (existing != null)
            {
                return existing.GetComponent<ToggleAllyStateButton>();
            }

            var clone = UnityEngine.Object.Instantiate(vanilla.gameObject, parent);
            clone.name = CloneName;
            clone.transform.SetSiblingIndex(vanilla.transform.GetSiblingIndex() + 1);

            var toggle = clone.GetComponent<ToggleAllyStateButton>();
            if (toggle == null)
            {
                UnityEngine.Object.Destroy(clone);
                ModLog.Warn("the cloned follow button carried no ToggleAllyStateButton; " +
                            "no roam control this session");
                return null;
            }

            Caption(toggle, Targets.LeftCaption, "Escort");
            Caption(toggle, Targets.RightCaption, "Roam");

            toggle.OnValueChanged += side => OnToggled(toggle, side);

            if (!_loggedCreated)
            {
                _loggedCreated = true;
                ModLog.Info("roam control added to the ally panel beside the vanilla " +
                            "follow button");
            }
            return toggle;
        }

        /// <summary>
        /// Writes the label straight onto the text component.
        ///
        /// <c>ToggleAllyStateButton.Initialize</c> takes localization <i>tags</i>, not
        /// literal text, so using it would mean registering keys - and the game's
        /// <c>Localization.Get</c> is already the single most contested method in a
        /// loaded mod set. Setting the text is the smaller footprint.
        /// </summary>
        private static void Caption(ToggleAllyStateButton toggle, System.Reflection.FieldInfo field,
                                    string text)
        {
            if (field == null)
            {
                return;   // cosmetic only; the control still works unlabelled
            }

            try
            {
                if (field.GetValue(toggle) is TextMeshProUGUI label)
                {
                    label.text = text;
                }
            }
            catch (Exception error)
            {
                ModLog.Warn("could not label the roam control (" + error.GetType().Name +
                            "); it works but reads as the vanilla button");
            }
        }

        private static void OnToggled(ToggleAllyStateButton toggle, ToggleAllyStateButton.Side side)
        {
            if (_settingState || !ModConfig.Enabled)
            {
                return;   // we are only displaying the current stance, not changing it
            }

            try
            {
                var window = toggle.GetComponentInParent<MonsterInspectWindow>();
                if (window == null || !Targets.UiUsable)
                {
                    return;
                }

                var creature = Targets.InspectedCreature.GetValue(window) as Creature;
                if (!AllyTest.IsAlly(creature))
                {
                    return;
                }

                var creatures = SignalsMod.Creatures;
                var roam = side == ToggleAllyStateButton.Side.Right;

                // An explicit stance order supersedes a standing destination. Leaving
                // both in force would have the two layers pulling the same ally in
                // different directions every turn.
                MoveOrders.Clear(creature);
                AllyOrders.Set(creature, roam, creatures);
                ModLog.Info("ally " + AllyTest.IdOf(creature) + " ordered to " +
                            (roam ? "roam" : "escort"));
            }
            catch (Exception error)
            {
                ModLog.Error("could not apply the roam order", error);
            }
        }
    }
}
