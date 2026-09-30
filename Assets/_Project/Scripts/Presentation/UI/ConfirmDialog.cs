using System;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    public sealed class ConfirmDialog : UiScreen
    {
        private readonly Label _text;
        private readonly Button _cancel;
        private Action _onConfirm;

        public ConfirmDialog(VisualElement root) : base(root)
        {
            _text = Find<Label>(root, "dialog-text");
            _cancel = Find<Button>(root, "dialog-cancel");
            Find<Button>(root, "dialog-confirm").clicked += () =>
            {
                var action = _onConfirm;
                Close();
                action?.Invoke();
            };
            _cancel.clicked += Close;
        }

        public event Action Closed;

        /// <summary>Safe default: cancel has the focus, so an accidental Enter does not confirm.</summary>
        protected override Focusable InitialFocus => _cancel;

        public void Ask(string text, Action onConfirm)
        {
            _text.text = text;
            _onConfirm = onConfirm;
            Show();
        }

        public void Close()
        {
            _onConfirm = null;
            Hide();
            Closed?.Invoke();
        }
    }
}
