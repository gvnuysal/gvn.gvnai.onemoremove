using System;
using System.Linq;
using System.Threading;
using OneMoreMove.Core;
using OneMoreMove.Session;
using UnityEngine;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Turns player intents into coordinator calls and board animations. Input is refused while a transition plays,
    /// so one accepted key press is exactly one move.
    /// </summary>
    public sealed class GameplayController : IDisposable
    {
        private readonly GameCoordinator _game;
        private readonly BoardView _board;
        private readonly HintService _hints;
        private CancellationTokenSource _hintCancellation;

        public GameplayController(GameCoordinator game, BoardView board, HintService hints)
        {
            _game = game ?? throw new ArgumentNullException(nameof(game));
            _board = board ? board : throw new ArgumentNullException(nameof(board));
            _hints = hints ?? throw new ArgumentNullException(nameof(hints));
        }

        /// <summary>HUD values (moves, undo count, hint flag) changed.</summary>
        public event Action StateChanged;

        /// <summary>Short explanation for the player; null clears it.</summary>
        public event Action<string> Feedback;

        /// <summary>Every attempted move, accepted or rejected, as the board starts showing it (sound, haptics).</summary>
        public event Action<MoveResult> MovePlayed;

        public event Action Undone;
        public event Action Restarted;

        /// <summary>Raised once per win, after the winning move has finished animating.</summary>
        public event Action<CompletionOutcome> LevelCompleted;

        public GameSession Session => _game.Current;
        public bool IsAnimating => _board.IsAnimating;
        public bool IsHintPending { get; private set; }
        public bool NeedsRestartConfirmation => Session != null && Session.State.MoveCount > 0 && !Session.IsWon;

        public void Open(int levelIndex)
        {
            CancelHint();
            var session = _game.StartLevel(levelIndex);
            _board.Build(session.Level, session.State);
            Feedback?.Invoke(null);
            StateChanged?.Invoke();
        }

        public bool TryMove(Direction direction)
        {
            var session = Session;
            if (session == null || session.IsWon || _board.IsAnimating) return false;

            CancelHint();
            var outcome = _game.Move(direction);
            var reducedMotion = _game.Settings.ReducedMotion;
            if (!outcome.Result.Accepted)
            {
                _board.ShowRejected(outcome.Result, reducedMotion);
                Feedback?.Invoke(Strings.Reject(outcome.Result.RejectReason));
                MovePlayed?.Invoke(outcome.Result);
                return false;
            }

            var echoBlocked = outcome.Result.Events.FirstOrDefault(e => e.Type == MoveEventType.EchoBlocked);
            Feedback?.Invoke(outcome.Result.Events.Any(e => e.Type == MoveEventType.EchoBlocked) ? Strings.EchoBlocked(echoBlocked.EchoBlockReason) : null);

            var completion = outcome.Completion;
            _board.PlayMove(outcome.Result, reducedMotion, () =>
            {
                if (completion != null) LevelCompleted?.Invoke(completion);
            });
            MovePlayed?.Invoke(outcome.Result);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryUndo()
        {
            if (Session == null || _board.IsAnimating) return false;

            CancelHint();
            if (!_game.Undo()) return false;

            _board.Snap(Session.State);
            Feedback?.Invoke(null);
            Undone?.Invoke();
            StateChanged?.Invoke();
            return true;
        }

        public void Restart()
        {
            if (Session == null) return;

            CancelHint();
            _board.CompleteAnimations();
            _game.Restart();
            _board.Snap(Session.State);
            Feedback?.Invoke(null);
            Restarted?.Invoke();
            StateChanged?.Invoke();
        }

        public void ShowPreview(Direction direction)
        {
            var session = Session;
            if (!_game.Settings.ShowMovePreview || session == null || session.IsWon || _board.IsAnimating) return;
            _board.ShowPreview(session.Preview(direction));
        }

        public void HidePreview() => _board.HidePreview();

        /// <summary>
        /// Searches from the current state on a worker thread. The result is dropped if the state changed meanwhile.
        /// </summary>
        public async void RequestHint()
        {
            var session = Session;
            if (session == null || session.IsWon || IsHintPending || _board.IsAnimating) return;

            var stage = _game.TakeHint();
            var version = session.StateVersion;
            CancelHint();
            var cancellation = new CancellationTokenSource();
            _hintCancellation = cancellation;
            IsHintPending = true;
            Feedback?.Invoke(Strings.HintThinking);
            StateChanged?.Invoke();

            try
            {
                var hint = await _hints.RequestAsync(session.Level, session.State, stage, version, cancellation.Token);
                if (cancellation.IsCancellationRequested || Session != session || session.StateVersion != hint.StateVersion) return;

                Feedback?.Invoke(Strings.HintText(hint));
                if (hint.Kind == HintKind.SuggestedDirection && hint.SuggestedDirection.HasValue) _board.ShowHint(hint.SuggestedDirection.Value);
            }
            catch (OperationCanceledException)
            {
                // The player moved on; nothing to show.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Feedback?.Invoke(Strings.HintText(new Hint(HintKind.Unavailable, MechanicTopic.Movement, null, null, version)));
            }
            finally
            {
                if (_hintCancellation == cancellation)
                {
                    IsHintPending = false;
                    _hintCancellation = null;
                    StateChanged?.Invoke();
                }

                cancellation.Dispose();
            }
        }

        public void Tick(float deltaSeconds) => _game.Tick(deltaSeconds);

        /// <summary>Focus loss / pause / quit: settle the board on the applied state and write the save.</summary>
        public void Suspend()
        {
            _board.CompleteAnimations();
            _game.Flush();
        }

        public void Dispose() => CancelHint();

        private void CancelHint()
        {
            _board.HideHint();
            if (_hintCancellation == null) return;

            _hintCancellation.Cancel();
            _hintCancellation = null;
            IsHintPending = false;
        }
    }
}
