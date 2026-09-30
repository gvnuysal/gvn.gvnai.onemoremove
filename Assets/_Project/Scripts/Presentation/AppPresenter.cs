using System;
using OneMoreMove.Core;
using OneMoreMove.Presentation.Audio;
using OneMoreMove.Presentation.UI;
using OneMoreMove.Session;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation
{
    /// <summary>Screen flow and input routing. Owns no game state: it asks the coordinator and the gameplay controller.</summary>
    public sealed class AppPresenter : IDisposable
    {
        private readonly VisualElement _root;
        private readonly GameCoordinator _game;
        private readonly GameplayController _gameplay;
        private readonly BoardView _board;
        private readonly InputController _input;
        private readonly AudioService _audio;
        private readonly Action _quit;

        private readonly MainMenuScreen _mainMenu;
        private readonly LevelSelectScreen _levelSelect;
        private readonly HudScreen _hud;
        private readonly WinPanel _win;
        private readonly SettingsScreen _settings;
        private readonly ConfirmDialog _dialog;
        private readonly ScreenLayout _layout;
        private readonly TouchGestures _gestures;

        public AppPresenter(VisualElement root, GameCoordinator game, GameplayController gameplay, BoardView board, InputController input,
            AudioService audio, Action quit)
        {
            _root = root ?? throw new ArgumentNullException(nameof(root));
            _game = game;
            _gameplay = gameplay;
            _board = board;
            _input = input;
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _quit = quit;

            _mainMenu = new MainMenuScreen(Find("main-menu"));
            _levelSelect = new LevelSelectScreen(Find("level-select"));
            _hud = new HudScreen(Find("hud"));
            _win = new WinPanel(Find("win-panel"));
            _settings = new SettingsScreen(Find("settings"));
            _dialog = new ConfirmDialog(Find("dialog"));
            _layout = new ScreenLayout(root, board);
            _gestures = new TouchGestures(root, board, () => _gameplay.Session?.State.Player);

            Wire();
        }

        private bool OverlayOpen => _win.IsVisible || _settings.IsVisible || _dialog.IsVisible;

        /// <summary>True while the player is looking at the board with nothing on top (counts active play time).</summary>
        public bool IsPlaying => _hud.IsVisible && !OverlayOpen;

        public void Start()
        {
            ApplyTextScale();
            _audio.SetVolumes(_game.Settings.SfxVolume, _game.Settings.MusicVolume);
            _audio.StartMusic();
            var message = Strings.LoadStatus(_game.LoadStatus) ?? Strings.ResumeDiscarded(_game.ResumeDiscardReason);
            ShowMainMenu(message);
        }

        public void Tick(float deltaSeconds)
        {
            _layout.Tick();
            _gestures.Tick(IsPlaying);
            if (IsPlaying) _gameplay.Tick(deltaSeconds);
        }

        public void Dispose()
        {
            _input.Move -= OnMove;
            _input.Undo -= OnUndo;
            _input.Restart -= OnRestart;
            _input.Hint -= OnHint;
            _input.Back -= OnBack;
            _gestures.Move -= OnMove;
        }

        private void Wire()
        {
            _input.Move += OnMove;
            _input.Undo += OnUndo;
            _input.Restart += OnRestart;
            _input.Hint += OnHint;
            _input.Back += OnBack;
            _gestures.Move += OnMove;

            _mainMenu.ContinueClicked += () =>
            {
                var index = _game.FindContinueIndex();
                if (index >= 0) OpenLevel(index);
            };
            _mainMenu.LevelsClicked += ShowLevelSelect;
            _mainMenu.SettingsClicked += OpenSettings;
            _mainMenu.QuitClicked += () => _quit?.Invoke();

            _levelSelect.LevelChosen += OpenLevel;
            _levelSelect.BackClicked += () => ShowMainMenu(null);

            _hud.DirectionClicked += d => OnMove(d);
            _hud.DirectionHovered += d => _gameplay.ShowPreview(d);
            _hud.DirectionUnhovered += () => _gameplay.HidePreview();
            _hud.UndoClicked += OnUndo;
            _hud.RestartClicked += OnRestart;
            _hud.HintClicked += OnHint;
            _hud.MenuClicked += () => ShowMainMenu(null);

            _win.NextClicked += () => OpenLevel(_game.CurrentIndex + 1);
            _win.ReplayClicked += () =>
            {
                _win.Hide();
                _gameplay.Restart();
                UpdateInput();
            };
            _win.LevelsClicked += ShowLevelSelect;

            _settings.ReducedMotionChanged += value => _game.UpdateSettings(s => s.ReducedMotion = value);
            _settings.MovePreviewChanged += value =>
            {
                _game.UpdateSettings(s => s.ShowMovePreview = value);
                if (!value) _gameplay.HidePreview();
            };
            _settings.TextScaleChanged += value =>
            {
                _game.UpdateSettings(s => s.TextScalePercent = value);
                ApplyTextScale();
            };
            _settings.SfxVolumeChanged += value =>
            {
                _game.UpdateSettings(s => s.SfxVolume = value);
                _audio.SetVolumes(_game.Settings.SfxVolume, _game.Settings.MusicVolume);
                _audio.Play(Sound.Step);
            };
            _settings.MusicVolumeChanged += value =>
            {
                _game.UpdateSettings(s => s.MusicVolume = value);
                _audio.SetVolumes(_game.Settings.SfxVolume, _game.Settings.MusicVolume);
            };
            _settings.CloseClicked += CloseSettings;
            _dialog.Closed += UpdateInput;

            _gameplay.StateChanged += RefreshHud;
            _gameplay.Feedback += _hud.SetFeedback;
            _gameplay.LevelCompleted += OnLevelCompleted;
            _gameplay.MovePlayed += _audio.PlayMove;
            _gameplay.Undone += () => _audio.Play(Sound.Undo);
            _gameplay.Restarted += () => _audio.Play(Sound.Undo);
        }

        private void OnMove(Direction direction)
        {
            if (IsPlaying) _gameplay.TryMove(direction);
        }

        private void OnUndo()
        {
            if (IsPlaying) _gameplay.TryUndo();
        }

        private void OnHint()
        {
            if (IsPlaying) _gameplay.RequestHint();
        }

        private void OnRestart()
        {
            if (!IsPlaying) return;

            if (!_gameplay.NeedsRestartConfirmation)
            {
                _gameplay.Restart();
                return;
            }

            _dialog.Ask(Strings.RestartConfirm, _gameplay.Restart);
            UpdateInput();
        }

        private void OnBack()
        {
            if (_dialog.IsVisible) _dialog.Close();
            else if (_settings.IsVisible) CloseSettings();
            else if (_win.IsVisible) ShowLevelSelect();
            else if (_hud.IsVisible || _levelSelect.IsVisible) ShowMainMenu(null);
        }

        private void OnLevelCompleted(CompletionOutcome outcome)
        {
            var index = _game.CurrentIndex;
            var hasNext = index + 1 < _game.Library.Count;
            _win.Bind(outcome, _game.Current.Level.ParMoves, hasNext);
            _win.Show();
            TextScaler.Apply(_win.Root, _game.Settings.TextScalePercent);
            UpdateInput();
        }

        private void OpenLevel(int index)
        {
            if (index < 0 || index >= _game.Library.Count || !_game.Progress.IsUnlocked(_game.Library, index)) return;

            HideAll();
            _board.gameObject.SetActive(true);
            _gameplay.Open(index);
            _hud.Show();
            UpdateInput();
        }

        private void ShowMainMenu(string message)
        {
            HideAll();
            _board.gameObject.SetActive(false);
            _mainMenu.SetCanContinue(_game.FindContinueIndex() >= 0);
            _mainMenu.SetMessage(message);
            _mainMenu.Show();
            UpdateInput();
        }

        private void ShowLevelSelect()
        {
            HideAll();
            _board.gameObject.SetActive(false);
            _levelSelect.Populate(_game.Library, _game.Progress, _game.FindContinueIndex());
            TextScaler.Apply(_levelSelect.Root, _game.Settings.TextScalePercent);
            _levelSelect.Show();
            UpdateInput();
        }

        private void OpenSettings()
        {
            _settings.Bind(_game.Settings);
            _settings.Show();
            UpdateInput();
        }

        private void CloseSettings()
        {
            _settings.Hide();
            if (_mainMenu.IsVisible) _mainMenu.Show();
            UpdateInput();
        }

        private void HideAll()
        {
            _mainMenu.Hide();
            _levelSelect.Hide();
            _hud.Hide();
            _win.Hide();
            _settings.Hide();
            _dialog.Hide();
            _gameplay.HidePreview();
        }

        private void RefreshHud() => _hud.Refresh(_gameplay.Session, _game.CurrentIndex, _gameplay.IsHintPending);

        private void UpdateInput() => _input.SetGameplayEnabled(IsPlaying);

        private void ApplyTextScale() => TextScaler.Apply(_root, _game.Settings.TextScalePercent);

        private VisualElement Find(string name) =>
            _root.Q<VisualElement>(name) ?? throw new InvalidOperationException($"UI element '{name}' is missing from App.uxml.");
    }
}
