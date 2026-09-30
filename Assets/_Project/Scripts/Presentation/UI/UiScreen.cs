using System;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>A named part of App.uxml that can be shown or hidden. Screens only expose intents as events.</summary>
    public abstract class UiScreen
    {
        protected UiScreen(VisualElement root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Hide();
        }

        public VisualElement Root { get; }
        public bool IsVisible { get; private set; }

        /// <summary>Element that receives keyboard focus when the screen opens (visible focus for keyboard users).</summary>
        protected virtual Focusable InitialFocus => null;

        public virtual void Show()
        {
            Root.style.display = DisplayStyle.Flex;
            IsVisible = true;
            var focus = InitialFocus;
            if (focus != null) Root.schedule.Execute(() => focus.Focus());
        }

        public virtual void Hide()
        {
            Root.style.display = DisplayStyle.None;
            IsVisible = false;
        }

        protected static T Find<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"UI element '{name}' ({typeof(T).Name}) is missing from App.uxml.");
    }
}
