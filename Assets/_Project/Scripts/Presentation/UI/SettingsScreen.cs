using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Session;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    public sealed class SettingsScreen : UiScreen
    {
        private static readonly int[] TextScales = { 100, 125, 150 };

        private readonly Toggle _reducedMotion;
        private readonly Toggle _preview;
        private readonly DropdownField _textSize;
        private readonly DropdownField _language;
        private readonly SliderInt _sfxVolume;
        private readonly SliderInt _musicVolume;
        private readonly Button _close;

        public SettingsScreen(VisualElement root) : base(root)
        {
            _reducedMotion = Find<Toggle>(root, "reduced-motion");
            _preview = Find<Toggle>(root, "move-preview");
            _textSize = Find<DropdownField>(root, "text-size");
            _language = Find<DropdownField>(root, "language");
            _sfxVolume = Find<SliderInt>(root, "sfx-volume");
            _musicVolume = Find<SliderInt>(root, "music-volume");
            _close = Find<Button>(root, "settings-close");

            _textSize.choices = TextScales.Select(Strings.TextScale).ToList();
            _language.choices = Localization.All.Select(Localization.NativeName).ToList();
            // Setting a field's label sends a ChangeEvent<string> from the label that bubbles to the field: only react to
            // events from the field itself, or relabelling the dropdown would look like picking a new value.
            _language.RegisterValueChangedCallback(e =>
            {
                if (e.target == _language) LanguageChanged?.Invoke(Localization.All[Math.Max(0, _language.index)]);
            });
            _reducedMotion.RegisterValueChangedCallback(e => ReducedMotionChanged?.Invoke(e.newValue));
            _preview.RegisterValueChangedCallback(e => MovePreviewChanged?.Invoke(e.newValue));
            _textSize.RegisterValueChangedCallback(e =>
            {
                if (e.target == _textSize) TextScaleChanged?.Invoke(TextScales[Math.Max(0, _textSize.index)]);
            });
            _sfxVolume.RegisterValueChangedCallback(e => SfxVolumeChanged?.Invoke(e.newValue / 100f));
            _musicVolume.RegisterValueChangedCallback(e => MusicVolumeChanged?.Invoke(e.newValue / 100f));
            _close.clicked += () => CloseClicked?.Invoke();
        }

        public event Action<Language> LanguageChanged;
        public event Action<bool> ReducedMotionChanged;
        public event Action<bool> MovePreviewChanged;
        public event Action<int> TextScaleChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<float> MusicVolumeChanged;
        public event Action CloseClicked;

        protected override Focusable InitialFocus => _reducedMotion;

        public void Bind(GameSettings settings)
        {
            _reducedMotion.SetValueWithoutNotify(settings.ReducedMotion);
            _preview.SetValueWithoutNotify(settings.ShowMovePreview);
            var index = Array.IndexOf(TextScales, settings.TextScalePercent);
            _textSize.choices = TextScales.Select(Strings.TextScale).ToList();
            _textSize.SetValueWithoutNotify(_textSize.choices[index < 0 ? 0 : index]);
            _language.SetValueWithoutNotify(Localization.NativeName(Localization.Current));
            _sfxVolume.SetValueWithoutNotify(Percent(settings.SfxVolume));
            _musicVolume.SetValueWithoutNotify(Percent(settings.MusicVolume));
        }

        private static int Percent(float volume) => (int)Math.Round(volume * 100f);
    }
}
