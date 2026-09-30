using System;
using OneMoreMove.Session;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    public sealed class WinPanel : UiScreen
    {
        private readonly VisualElement _stars;
        private readonly Label _moves;
        private readonly Label _best;
        private readonly Label _note;
        private readonly Button _next;
        private readonly Button _replay;

        public WinPanel(VisualElement root) : base(root)
        {
            _stars = Find<VisualElement>(root, "win-stars");
            _moves = Find<Label>(root, "win-moves");
            _best = Find<Label>(root, "win-best");
            _note = Find<Label>(root, "win-note");
            _next = Find<Button>(root, "next-button");
            _replay = Find<Button>(root, "replay-button");
            _next.clicked += () => NextClicked?.Invoke();
            _replay.clicked += () => ReplayClicked?.Invoke();
            Find<Button>(root, "win-levels-button").clicked += () => LevelsClicked?.Invoke();
        }

        public event Action NextClicked;
        public event Action ReplayClicked;
        public event Action LevelsClicked;

        protected override Focusable InitialFocus => _next.style.display == DisplayStyle.None ? _replay : _next;

        public void Bind(CompletionOutcome outcome, int parMoves, bool hasNext)
        {
            UiIcons.FillStars(_stars, outcome.Stars, 64f);
            _moves.text = Strings.MovesResult(outcome.Moves, parMoves);
            _best.text = Strings.BestMoves(outcome.BestMoves) + (outcome.IsNewBestMoves && !outcome.IsFirstCompletion ? "  (yeni rekor)" : string.Empty);
            _note.text = hasNext ? string.Empty : Strings.AllLevelsComplete;
            _note.style.display = hasNext ? DisplayStyle.None : DisplayStyle.Flex;
            _next.style.display = hasNext ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
