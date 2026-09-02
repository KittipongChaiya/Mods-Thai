using System;
using MGSC;
using UnityEngine;

namespace QuasimorphSignals
{
    /// <summary>
    /// The <b>Move</b> control on the ally panel, beside the vanilla follow and shoot
    /// toggles and this mod's Escort/Roam toggle.
    ///
    /// <b>Why this is a button and not another toggle.</b> The other three controls are
    /// <c>ToggleAllyStateButton</c>s, whose state is a <c>Side</c> - an enum of exactly
    /// Left and Right. "Go to a place I am about to point at" has no second side; it is
    /// an action, not a stance. So this clones the panel's own close button, a
    /// <c>CommonButton</c>, which is the game's plain push-button and brings its
    /// styling, hover states and input handling with it.
    ///
    /// <c>SetRawCaption</c> takes literal text rather than a localization tag, so no
    /// key has to be registered - the same reasoning that keeps the toggle captions out
    /// of <c>Localization.Get</c>, already the most contested method in a loaded mod set.
    /// </summary>
    internal static class MoveButton
    {
        private const string CloneName = "QuasimorphSignalsMoveButton";

        private static bool _loggedCreated;
        private static bool _loggedUnavailable;

        /// <summary>
        /// Places the button, shows it only for allies, and labels it with what it will
        /// do next - so an ally already under a move order offers to cancel it rather
        /// than silently re-arming.
        /// </summary>
        internal static void Refresh(MonsterInspectWindow window, ToggleAllyStateButton anchor,
                                     Creature creature)
        {
            if (!ModConfig.MoveOrders || !Targets.MoveButtonUsable || window == null || anchor == null)
            {
                return;
            }

            try
            {
                var button = Ensure(window, anchor);
                if (button == null)
                {
                    return;
                }

                var isAlly = AllyTest.IsAlly(creature);
                button.gameObject.SetActive(isAlly);
                if (!isAlly)
                {
                    return;
                }

                button.SetRawCaption(MoveOrders.Has(creature) ? "Cancel move" : "Move to...");
            }
            catch (Exception error)
            {
                ModLog.Error("could not refresh the move button; the rest of the panel is " +
                             "unaffected", error);
            }
        }

        /// <summary>
        /// Finds our clone under the same parent as the stance controls, creating it on
        /// first use. Looked up by name rather than cached, because the inspect window
        /// is pooled and a cached reference would outlive the object it points at.
        /// </summary>
        private static CommonButton Ensure(MonsterInspectWindow window, ToggleAllyStateButton anchor)
        {
            var parent = anchor.transform.parent;
            if (parent == null)
            {
                return null;
            }

            var existing = parent.Find(CloneName);
            if (existing != null)
            {
                return existing.GetComponent<CommonButton>();
            }

            var source = Targets.CloseButton?.GetValue(window) as CommonButton;
            if (source == null)
            {
                if (!_loggedUnavailable)
                {
                    _loggedUnavailable = true;
                    ModLog.Warn("no CommonButton to clone on the inspect window; there will " +
                                "be no Move control. Every other part of this mod still works.");
                }
                return null;
            }

            var clone = UnityEngine.Object.Instantiate(source.gameObject, parent);
            clone.name = CloneName;
            clone.transform.SetAsLastSibling();

            var button = clone.GetComponent<CommonButton>();
            if (button == null)
            {
                UnityEngine.Object.Destroy(clone);
                ModLog.Warn("the cloned button carried no CommonButton; no Move control " +
                            "this session");
                return null;
            }

            // The button we cloned closes the window. Ours must not, because Arm
            // closes it already and two closes would pop two screens - the panel and
            // whatever was behind it.
            //
            // An event cannot be assigned from outside the class that declares it, so
            // the subscriber list is cleared through its backing field. If that field
            // cannot be resolved on this game build there is no way to be sure what
            // the clone inherited, and a Move button that might close two screens is
            // worse than no Move button: we destroy it and say so.
            if (!ClearSubscribers(button))
            {
                UnityEngine.Object.Destroy(clone);
                if (!_loggedUnavailable)
                {
                    _loggedUnavailable = true;
                    ModLog.Warn("could not clear the cloned button's click handler, so no " +
                                "Move control is offered. Every other part of this mod " +
                                "still works. If the game has updated, this mod needs " +
                                "rebuilding against it.");
                }
                return null;
            }

            button.OnClick += (_, __) => OnPressed(window);

            if (!_loggedCreated)
            {
                _loggedCreated = true;
                ModLog.Info("move control added to the ally panel");
            }
            return button;
        }

        /// <summary>
        /// Empties a <c>CommonButton</c>'s click event through its backing field.
        /// Returns false if the field could not be reached, which is the caller's
        /// signal to abandon the button rather than ship one with unknown behaviour.
        /// </summary>
        private static bool ClearSubscribers(CommonButton button)
        {
            var field = Targets.CommonButtonOnClick;
            if (field == null)
            {
                return false;
            }

            try
            {
                field.SetValue(button, null);
                return true;
            }
            catch (Exception error)
            {
                ModLog.Warn("could not clear the cloned button's handlers (" +
                            error.GetType().Name + ")");
                return false;
            }
        }

        private static void OnPressed(MonsterInspectWindow window)
        {
            if (!ModConfig.Enabled || !ModConfig.MoveOrders || !Targets.UiUsable)
            {
                return;
            }

            try
            {
                var creature = Targets.InspectedCreature.GetValue(window) as Creature;
                if (!AllyTest.IsAlly(creature))
                {
                    return;
                }

                if (MoveOrders.Has(creature))
                {
                    MoveOrders.Clear(creature);
                    MoveTargeting.Notify("Move order cancelled.");
                    ModLog.Info("ally " + AllyTest.IdOf(creature) + " move order cancelled");
                    return;
                }

                MoveTargeting.Arm(creature);
            }
            catch (Exception error)
            {
                ModLog.Error("could not start a move order", error);
            }
        }
    }
}
