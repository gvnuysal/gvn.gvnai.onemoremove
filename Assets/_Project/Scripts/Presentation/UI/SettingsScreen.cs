using System;
using System.Collections.Generic;
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
        private readonly SliderInt _sfxVolume;
        private readonly SliderInt _musicVolume;
        private readonly Button _close;

        public SettingsScreen(VisualElement root) : base(root)
        {
            _reducedMotion = Find<Toggle>(root, "reduced-motion");
            _preview = Find<Toggle>(root, "move-preview");
            _textSize = Find<DropdownField>(root, "text-size");
            _sfxVolume = Find<SliderInt>(root, "sfx-volume");
            _musicVolume = Find<SliderInt>(root, "music-volume");
            _close = Find<Button>(root, "settings-close");

            _textSize.choices = new List<string> { "%100", "%125", "%150" };
            _reducedMotion.RegisterValueChangedCallback(e => ReducedMotionChanged?.Invoke(e.newValue));
            _preview.RegisterValueChangedCallback(e => MovePreviewChanged?.Invoke(e.newValue));
            _textSize.RegisterValueChangedCallback(_ => TextScaleChanged?.Invoke(TextScales[Math.Max(0, _textSize.index)]));
            _sfxVolume.RegisterValueChangedCallback(e => SfxVolumeChanged?.Invoke(e.newValue / 100f));
            _musicVolume.RegisterValueChangedCallback(e => MusicVolumeChanged?.Invoke(e.newValue / 100f));
            _close.clicked += () => CloseClicked?.Invoke();
        }

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
            _textSize.SetValueWithoutNotify(_textSize.choices[index < 0 ? 0 : index]);
            _sfxVolume.SetValueWithoutNotify(Percent(settings.SfxVolume));
            _musicVolume.SetValueWithoutNotify(Percent(settings.MusicVolume));
        }

        private static int Percent(float volume) => (int)Math.Round(volume * 100f);
    }
}
