using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>
    /// Exposes the visible UI Toolkit controls to VoiceOver (iOS) and TalkBack (Android) through Unity's accessibility
    /// hierarchy, and speaks announcements. Idle unless a screen reader is running. The hierarchy mirrors the screen in
    /// reading order and is rebuilt only when the visible controls or their texts change.
    /// </summary>
    public sealed class ScreenReaderSupport
    {
        private const float PollSeconds = 0.25f;
        private const int SliderStep = 10;

        private readonly VisualElement _root;
        private readonly Func<string> _boardDescription;
        private readonly Func<Rect?> _boardArea;
        private AccessibilityHierarchy _hierarchy;
        private string _signature;
        private float _untilPoll;

        /// <param name="boardDescription">Spoken board state while the board is shown, otherwise null.</param>
        /// <param name="boardArea">The board's panel-space rectangle while it is shown, otherwise null.</param>
        public ScreenReaderSupport(VisualElement root, Func<string> boardDescription, Func<Rect?> boardArea)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _boardDescription = boardDescription ?? throw new ArgumentNullException(nameof(boardDescription));
            _boardArea = boardArea ?? throw new ArgumentNullException(nameof(boardArea));
        }

        public static bool IsActive => AssistiveSupport.isScreenReaderEnabled;

        public AccessibilityHierarchy Hierarchy => _hierarchy;

        public void Tick(float deltaSeconds)
        {
            if (!IsActive)
            {
                if (_hierarchy != null) Release();
                return;
            }

            _untilPoll -= deltaSeconds;
            if (_untilPoll > 0f && _hierarchy != null) return;
            _untilPoll = PollSeconds;
            Rebuild(force: false);
        }

        public void Announce(string text)
        {
            if (IsActive && !string.IsNullOrEmpty(text)) AssistiveSupport.notificationDispatcher.SendAnnouncement(text);
        }

        /// <summary>Builds the hierarchy for what is on screen now. Returns true when it changed.</summary>
        public bool Rebuild(bool force)
        {
            var items = CollectVisible();
            var board = _boardDescription();
            var signature = Signature(items, board);
            if (!force && _hierarchy != null && signature == _signature) return false;

            var screenChanged = _signature == null || FirstLine(_signature) != FirstLine(signature);
            _signature = signature;
            _hierarchy = new AccessibilityHierarchy();

            foreach (var element in items)
            {
                if (element == BoardAnchor && board != null) AddBoard(board);
                else AddElement(element);
            }

            AssistiveSupport.activeHierarchy = _hierarchy;
            var dispatcher = AssistiveSupport.notificationDispatcher;
            if (screenChanged) dispatcher.SendScreenChanged(null);
            else dispatcher.SendLayoutChanged(null);
            return true;
        }

        private void Release()
        {
            _hierarchy = null;
            _signature = null;
            AssistiveSupport.activeHierarchy = null;
        }

        /// <summary>Marks where the board sits in reading order (after the HUD header, before the controls).</summary>
        private static readonly VisualElement BoardAnchor = new VisualElement();

        private List<VisualElement> CollectVisible()
        {
            // A visible overlay (win panel, settings, dialog) is modal: only its content is read.
            VisualElement scope = _root;
            _root.Query<VisualElement>(className: "overlay").ForEach(overlay =>
            {
                if (IsDisplayed(overlay)) scope = overlay;
            });

            var items = new List<VisualElement>();
            scope.Query<VisualElement>().ForEach(element =>
            {
                if (IsReadable(element) && IsDisplayed(element)) items.Add(element);
            });

            if (scope == _root && _boardDescription() != null)
            {
                // The board sits between the HUD header and the controls below it.
                var controls = items.FindIndex(e => IsInside(e, "hud-actions") || IsInside(e, "dpad"));
                items.Insert(controls < 0 ? items.Count : controls, BoardAnchor);
            }

            return items;
        }

        private static bool IsInside(VisualElement element, string ancestorName)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.name == ancestorName) return true;
            }

            return false;
        }

        private static bool IsReadable(VisualElement element)
        {
            if (element is Button || element is Toggle || element is SliderInt || element is DropdownField) return true;
            if (!(element is Label label) || string.IsNullOrWhiteSpace(label.text)) return false;

            // Labels inside a control are read as part of that control.
            for (var parent = element.parent; parent != null; parent = parent.parent)
            {
                if (parent is Button || parent is BaseField<bool> || parent is BaseField<int> || parent is BaseField<string>) return false;
            }

            return true;
        }

        private static bool IsDisplayed(VisualElement element)
        {
            if (!element.visible || element.resolvedStyle.opacity <= 0.01f) return false;
            for (var e = element; e != null; e = e.parent)
            {
                if (e.resolvedStyle.display == DisplayStyle.None) return false;
            }

            var bounds = element.worldBound;
            return bounds.width > 0f && bounds.height > 0f && !float.IsNaN(bounds.width);
        }

        private void AddElement(VisualElement element)
        {
            var node = _hierarchy.AddNode(LabelOf(element));
            node.frameGetter = () => ToScreen(element.worldBound);
            if (!element.enabledInHierarchy) node.state = AccessibilityState.Disabled;

            switch (element)
            {
                case Button button:
                    node.role = AccessibilityRole.Button;
                    node.invoked += () => Submit(button);
                    break;
                case Toggle toggle:
                    node.role = AccessibilityRole.Toggle;
                    node.value = toggle.value ? "açık" : "kapalı";
                    if (toggle.value) node.state |= AccessibilityState.Selected;
                    node.invoked += () =>
                    {
                        toggle.value = !toggle.value;
                        return true;
                    };
                    break;
                case SliderInt slider:
                    node.role = AccessibilityRole.Slider;
                    node.value = $"%{slider.value}";
                    node.incremented += () => slider.value = Math.Min(slider.highValue, slider.value + SliderStep);
                    node.decremented += () => slider.value = Math.Max(slider.lowValue, slider.value - SliderStep);
                    break;
                case DropdownField dropdown:
                    node.role = AccessibilityRole.Button;
                    node.value = dropdown.value;
                    node.hint = "Değiştirmek için iki kez dokun.";
                    node.invoked += () =>
                    {
                        if (dropdown.choices.Count > 0) dropdown.index = (dropdown.index + 1) % dropdown.choices.Count;
                        return true;
                    };
                    break;
                default:
                    node.role = element.ClassListContains("t-heading") || element.ClassListContains("t-title")
                        ? AccessibilityRole.Header
                        : AccessibilityRole.StaticText;
                    break;
            }
        }

        private void AddBoard(string description)
        {
            var node = _hierarchy.AddNode(description);
            node.role = AccessibilityRole.StaticText;
            node.frameGetter = () => _boardArea() is Rect area ? ToScreen(area) : Rect.zero;
        }

        /// <summary>The visible text of a control, falling back to its children and tooltips (icon buttons, level cards).</summary>
        public static string LabelOf(VisualElement element)
        {
            switch (element)
            {
                case Button button when !string.IsNullOrWhiteSpace(button.text):
                    return button.text;
                case BaseField<bool> field:
                    return field.label;
                case BaseField<int> field:
                    return field.label;
                case BaseField<string> field:
                    return field.label;
                case Label label:
                    return label.text;
            }

            var parts = new List<string>();
            element.Query<VisualElement>().ForEach(child =>
            {
                if (child is Label label && !string.IsNullOrWhiteSpace(label.text)) parts.Add(label.text);
                else if (!string.IsNullOrWhiteSpace(child.tooltip)) parts.Add(child.tooltip);
            });

            return string.Join(", ", parts.Distinct());
        }

        private static bool Submit(Button button)
        {
            if (!button.enabledInHierarchy) return false;
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }

            return true;
        }

        /// <summary>Panel space (top-left origin, panel units) to screen pixels with a top-left origin.</summary>
        private Rect ToScreen(Rect panelRect)
        {
            var panelSize = _root.panel?.visualTree.layout.size ?? Vector2.zero;
            if (panelSize.x <= 0f || panelSize.y <= 0f) return Rect.zero;

            var sx = Screen.width / panelSize.x;
            var sy = Screen.height / panelSize.y;
            return new Rect(panelRect.x * sx, panelRect.y * sy, panelRect.width * sx, panelRect.height * sy);
        }

        private static string Signature(IEnumerable<VisualElement> items, string board)
        {
            var sb = new StringBuilder();
            foreach (var element in items)
            {
                if (element == BoardAnchor) sb.Append("board|").Append(board);
                else sb.Append(element.name).Append('|').Append(LabelOf(element)).Append('|').Append(element.enabledInHierarchy)
                    .Append('|').Append(ValueOf(element));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static string ValueOf(VisualElement element)
        {
            switch (element)
            {
                case Toggle toggle: return toggle.value.ToString();
                case SliderInt slider: return slider.value.ToString();
                case DropdownField dropdown: return dropdown.value;
                default: return string.Empty;
            }
        }

        /// <summary>The first readable item identifies the screen (its title), so a new title means a new screen.</summary>
        private static string FirstLine(string signature)
        {
            var end = signature.IndexOf('\n');
            return end < 0 ? signature : signature.Substring(0, end);
        }
    }
}
