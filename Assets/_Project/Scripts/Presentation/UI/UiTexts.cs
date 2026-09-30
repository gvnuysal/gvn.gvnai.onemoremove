using System;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>
    /// Writes the current language into every static text of App.uxml. The UXML keeps Turkish texts only as a preview
    /// for the UI Builder; at runtime these values win. Dynamic texts (moves, level names, tips) are set by their screens.
    /// </summary>
    internal static class UiTexts
    {
        public static void Apply(VisualElement root)
        {
            Text(root, "title", Strings.GameTitle);
            Text(root, "tagline", Strings.Tagline);
            Text(root, "continue-button", Strings.Continue);
            Text(root, "levels-button", Strings.Levels);
            Text(root, "settings-button", Strings.Settings);
            Text(root, "quit-button", Strings.Quit);

            Text(root, "levels-heading", Strings.Levels);
            Text(root, "level-select-back", Strings.Back);

            Text(root, "menu-button", Strings.WithKey(Strings.Menu, "Esc"));
            Text(root, "undo-button", Strings.WithKey(Strings.Undo, "Z"));
            Text(root, "restart-button", Strings.WithKey(Strings.Restart, "R"));
            Text(root, "hint-button", Strings.WithKey(Strings.HintLabel, "H"));

            Text(root, "win-heading", Strings.LevelComplete);
            Text(root, "next-button", Strings.NextLevel);
            Text(root, "replay-button", Strings.Replay);
            Text(root, "win-levels-button", Strings.Levels);

            Text(root, "settings-heading", Strings.Settings);
            Label<Toggle>(root, "reduced-motion", Strings.ReducedMotion);
            Label<Toggle>(root, "move-preview", Strings.MovePreview);
            Text(root, "move-preview-help", Strings.MovePreviewHelp);
            Label<DropdownField>(root, "language", Strings.LanguageLabel);
            Label<DropdownField>(root, "text-size", Strings.TextSize);
            Label<SliderInt>(root, "sfx-volume", Strings.SfxVolume);
            Label<SliderInt>(root, "music-volume", Strings.MusicVolume);
            Text(root, "settings-close", Strings.Close);

            Text(root, "dialog-cancel", Strings.No);
            Text(root, "dialog-confirm", Strings.Yes);
        }

        private static void Text(VisualElement root, string name, string text) => Find<TextElement>(root, name).text = text;

        private static void Label<T>(VisualElement root, string name, string label) where T : VisualElement
        {
            switch (Find<T>(root, name))
            {
                case BaseField<bool> field: field.label = label; break;
                case BaseField<int> field: field.label = label; break;
                case BaseField<string> field: field.label = label; break;
                default: throw new InvalidOperationException($"'{name}' has no label.");
            }
        }

        private static T Find<T>(VisualElement root, string name) where T : VisualElement =>
            root.Q<T>(name) ?? throw new InvalidOperationException($"UI element '{name}' ({typeof(T).Name}) is missing from App.uxml.");
    }
}
