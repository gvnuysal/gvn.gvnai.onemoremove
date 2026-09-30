using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    public sealed class MainMenuScreen : UiScreen
    {
        private readonly Button _continue;
        private readonly Button _levels;
        private readonly Label _message;

        public MainMenuScreen(VisualElement root) : base(root)
        {
            _continue = Find<Button>(root, "continue-button");
            _levels = Find<Button>(root, "levels-button");
            _message = Find<Label>(root, "menu-message");
            _continue.clicked += () => ContinueClicked?.Invoke();
            _levels.clicked += () => LevelsClicked?.Invoke();
            Find<Button>(root, "settings-button").clicked += () => SettingsClicked?.Invoke();
            var quit = Find<Button>(root, "quit-button");
            quit.clicked += () => QuitClicked?.Invoke();
            // iOS apps must not quit themselves (App Store guideline); the home gesture does that.
            if (Application.platform == RuntimePlatform.IPhonePlayer) quit.style.display = DisplayStyle.None;
            SetMessage(null);
        }

        public event Action ContinueClicked;
        public event Action LevelsClicked;
        public event Action SettingsClicked;
        public event Action QuitClicked;

        protected override Focusable InitialFocus => _continue.enabledSelf ? _continue : _levels;

        public void SetCanContinue(bool canContinue) => _continue.SetEnabled(canContinue);

        public void SetMessage(string text)
        {
            _message.text = text ?? string.Empty;
            _message.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
