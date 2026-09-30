using System;
using OneMoreMove.Core;
using OneMoreMove.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>
    /// In-game overlay. Its buttons are not focusable: arrow keys must move the piece, not the UI focus.
    /// Every button has a keyboard shortcut instead.
    /// </summary>
    public sealed class HudScreen : UiScreen
    {
        private readonly Label _levelName;
        private readonly Label _moves;
        private readonly Label _undoCount;
        private readonly Label _feedback;
        private readonly Label _tip;
        private readonly Button _undo;
        private readonly Button _hint;

        public HudScreen(VisualElement root) : base(root)
        {
            _levelName = Find<Label>(root, "level-name");
            _moves = Find<Label>(root, "move-count");
            _undoCount = Find<Label>(root, "undo-count");
            _feedback = Find<Label>(root, "feedback");
            _tip = Find<Label>(root, "level-tip");
            _undo = Find<Button>(root, "undo-button");
            _hint = Find<Button>(root, "hint-button");
            var restart = Find<Button>(root, "restart-button");
            var menu = Find<Button>(root, "menu-button");

            _undo.clicked += () => UndoClicked?.Invoke();
            _hint.clicked += () => HintClicked?.Invoke();
            restart.clicked += () => RestartClicked?.Invoke();
            menu.clicked += () => MenuClicked?.Invoke();

            foreach (var button in new[] { _undo, _hint, restart, menu }) button.focusable = false;

            if (Application.isMobilePlatform)
            {
                menu.text = Strings.MenuButtonTouch;
                _undo.text = Strings.UndoButtonTouch;
                restart.text = Strings.RestartButtonTouch;
                _hint.text = Strings.HintButtonTouch;
            }

            foreach (var direction in Directions.All)
            {
                var button = Find<Button>(root, "dir-" + direction.ToString().ToLowerInvariant());
                button.focusable = false;
                button.tooltip = Strings.DirectionName(direction);
                button.Add(UiIcons.Icon(Shape.Arrow, Palette.Primary, 44f, UiIcons.Rotation(direction)));
                button.clicked += () => DirectionClicked?.Invoke(direction);
                button.RegisterCallback<PointerEnterEvent>(_ => DirectionHovered?.Invoke(direction));
                button.RegisterCallback<PointerLeaveEvent>(_ => DirectionUnhovered?.Invoke());
            }

            var wait = Find<Button>(root, "dir-wait");
            wait.focusable = false;
            wait.tooltip = Strings.DirectionName(Direction.Wait) + (Application.isMobilePlatform ? string.Empty : " (Boşluk)");
            wait.Add(UiIcons.Icon(Shape.Wait, Palette.Primary, 36f));
            wait.clicked += () => DirectionClicked?.Invoke(Direction.Wait);
            wait.RegisterCallback<PointerEnterEvent>(_ => DirectionHovered?.Invoke(Direction.Wait));
            wait.RegisterCallback<PointerLeaveEvent>(_ => DirectionUnhovered?.Invoke());

            SetFeedback(null);
        }

        public event Action<Direction> DirectionClicked;
        public event Action<Direction> DirectionHovered;
        public event Action DirectionUnhovered;
        public event Action UndoClicked;
        public event Action RestartClicked;
        public event Action HintClicked;
        public event Action MenuClicked;

        public void Refresh(GameSession session, int levelIndex, bool hintPending)
        {
            if (session == null) return;

            _levelName.text = $"{Strings.LevelNumber(levelIndex)}. {session.Level.Name}";
            _moves.text = Strings.Moves(session.State.MoveCount);
            _undoCount.text = Strings.UndoAvailable(session.CanUndo ? session.History.Count : 0);
            _undo.SetEnabled(session.CanUndo);
            _hint.SetEnabled(!session.IsWon && !hintPending);
            _hint.EnableInClassList("hud-button--used", session.HintUsed);

            var tip = session.IsWon ? null : session.Level.Tip;
            _tip.text = tip ?? string.Empty;
            _tip.EnableInClassList("level-tip--hidden", string.IsNullOrEmpty(tip));
        }

        public void SetFeedback(string text)
        {
            _feedback.text = text ?? string.Empty;
            _feedback.EnableInClassList("feedback--hidden", string.IsNullOrEmpty(text));
        }
    }
}
