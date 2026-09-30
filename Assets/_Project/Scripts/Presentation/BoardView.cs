using System;
using System.Collections.Generic;
using OneMoreMove.Core;
using UnityEngine;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Draws a level with pooled sprite renderers and replays <see cref="MoveResult"/> events. It never decides rules:
    /// every position it shows comes from a <see cref="BoardState"/> computed by the core.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        private const int FloorOrder = 0;
        private const int GoalOrder = 1;
        private const int GateOrder = 2;
        private const int FeedbackOrder = 3;
        private const int EchoOrder = 5;
        private const int PlayerOrder = 6;
        private const int GhostOrder = 7;
        private const int HintOrder = 8;
        private const float PieceScale = 0.62f;
        private const float BoardPadding = 0.5f;

        /// <summary>Small boards (the 5×1 tutorial) are framed like 5×5 ones so cell size stays consistent.</summary>
        private const int MinFramedCells = 5;
        private const float MaxReserved = 0.45f;

        [SerializeField] private Camera boardCamera;

        [Tooltip("Duration of one move transition in seconds (design target 0.12–0.2).")]
        [SerializeField, Range(0.05f, 0.4f)] private float moveDuration = 0.15f;

        [Tooltip("Screen fractions kept free for the HUD above and below the board.")]
        [SerializeField, Range(0f, MaxReserved)] private float topReserved = 0.2f;

        [SerializeField, Range(0f, MaxReserved)] private float bottomReserved = 0.27f;

        private readonly TweenRunner _tweens = new TweenRunner();
        private readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _inUse = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _gates = new List<SpriteRenderer>();

        private LevelDefinition _level;
        private SpriteRenderer _player;
        private SpriteRenderer _echo;
        private SpriteRenderer _playerGhost;
        private SpriteRenderer _echoGhost;
        private SpriteRenderer _rejectFlash;
        private SpriteRenderer _echoFlash;
        private SpriteRenderer _hintArrow;
        private float _hintTime = -1f;
        private float _lastAspect;

        public bool IsAnimating => _tweens.IsRunning;
        public float MoveDuration => moveDuration;
        public LevelDefinition Level => _level;
        public Vector3 PlayerWorldPosition => _player != null ? _player.transform.position : Vector3.zero;

        public void SetCamera(Camera value) => boardCamera = value;

        /// <summary>Lays out a level and shows <paramref name="state"/> without animation. Reuses pooled renderers.</summary>
        public void Build(LevelDefinition level, BoardState state)
        {
            _tweens.Cancel();
            ReleaseAll();
            _level = level ?? throw new ArgumentNullException(nameof(level));

            for (var y = 0; y < level.Height; y++)
            {
                for (var x = 0; x < level.Width; x++)
                {
                    var cell = new GridPos(x, y);
                    var wall = level.IsWall(cell);
                    Take(Shape.Tile, wall ? Palette.Wall : Palette.Floor, FloorOrder, CellToWorld(cell), wall ? 0.98f : 0.94f);
                }
            }

            Take(Shape.Target, Palette.Goal, GoalOrder, CellToWorld(level.Goal), 0.62f);

            foreach (var gate in level.Gates)
            {
                _gates.Add(Take(Shape.GateClosed, Palette.Gate, GateOrder, CellToWorld(gate.Position), 1f));
            }

            _player = Take(Shape.Circle, Palette.Primary, PlayerOrder, Vector3.zero, PieceScale);
            _echo = level.EchoStart.HasValue ? Take(Shape.Diamond, Palette.Accent, EchoOrder, Vector3.zero, PieceScale) : null;
            _playerGhost = Take(Shape.Circle, Palette.WithAlpha(Palette.Primary, 0.35f), GhostOrder, Vector3.zero, PieceScale);
            _echoGhost = Take(Shape.Diamond, Palette.WithAlpha(Palette.Accent, 0.35f), GhostOrder, Vector3.zero, PieceScale);
            _rejectFlash = Take(Shape.Frame, Palette.WithAlpha(Palette.Reject, 0f), FeedbackOrder, Vector3.zero, 1f);
            _echoFlash = Take(Shape.Frame, Palette.WithAlpha(Palette.Accent, 0f), FeedbackOrder, Vector3.zero, 1f);
            _hintArrow = Take(Shape.Arrow, Palette.WithAlpha(Palette.Hint, 0f), HintOrder, Vector3.zero, 0.42f);
            HidePreview();
            HideHint();

            FitCamera(force: true);
            Snap(state);
        }

        /// <summary>Shows a state immediately (undo, restart, resume).</summary>
        public void Snap(BoardState state)
        {
            _tweens.CompleteAll();
            HideHint();
            _player.transform.position = CellToWorld(state.Player);
            _player.transform.localScale = Vector3.one * PieceScale;
            if (_echo != null && state.Echo.HasValue)
            {
                _echo.transform.position = CellToWorld(state.Echo.Value);
                _echo.transform.localScale = Vector3.one * PieceScale;
            }

            for (var i = 0; i < _gates.Count; i++) ApplyGate(i, state.IsGateOpen(i));
        }

        /// <summary>Animates an accepted move; <paramref name="onComplete"/> runs when the board shows the final state.</summary>
        public void PlayMove(MoveResult result, bool reducedMotion, Action onComplete = null)
        {
            if (!result.Accepted) throw new ArgumentException("Only accepted moves can be played.", nameof(result));

            _tweens.CompleteAll();
            HidePreview();
            HideHint();
            var duration = reducedMotion ? 0f : moveDuration;

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case MoveEventType.PlayerMoved:
                        MovePiece(_player.transform, e.From, e.To, duration);
                        break;
                    case MoveEventType.PlayerWaited:
                        if (duration > 0f) Pulse(_player.transform, 0f, 0.12f, PieceScale);
                        break;
                    case MoveEventType.EchoMoved:
                        if (_echo != null) MovePiece(_echo.transform, e.From, e.To, duration);
                        break;
                    case MoveEventType.EchoBlocked:
                        if (_echo != null) ShowEchoBlocked(e.From, result.Direction.Opposite(), duration);
                        break;
                    case MoveEventType.GatesToggled:
                        ToggleGates(e.GateBitsAfter, duration);
                        break;
                    case MoveEventType.LevelWon:
                        if (duration > 0f) Pulse(_player.transform, duration, 0.25f, PieceScale);
                        break;
                }
            }

            if (duration <= 0f) _tweens.CompleteAll();
            _tweens.WhenIdle(onComplete);
        }

        /// <summary>Short edge flash on the blocked cell plus a bump of the player towards it.</summary>
        public void ShowRejected(MoveResult result, bool reducedMotion)
        {
            var target = result.AttemptedTarget;
            var flashCell = _level.InBounds(target) ? target : result.PreviousState.Player;
            Flash(_rejectFlash, Palette.Reject, flashCell, 0.45f);
            if (!reducedMotion) Bump(_player.transform, result.PreviousState.Player, result.Direction, moveDuration, PieceScale, blocking: false);
        }

        /// <summary>Translucent pieces where the move would put them. Does not touch the real state.</summary>
        public void ShowPreview(MoveResult preview)
        {
            HidePreview();
            if (preview == null || !preview.Accepted) return;

            SetVisible(_playerGhost, CellToWorld(preview.NextState.Player));
            if (preview.NextState.Echo.HasValue && _echo != null) SetVisible(_echoGhost, CellToWorld(preview.NextState.Echo.Value));
        }

        public void HidePreview()
        {
            if (_playerGhost != null) _playerGhost.enabled = false;
            if (_echoGhost != null) _echoGhost.enabled = false;
        }

        public void ShowHint(Direction direction)
        {
            var offset = direction.ToOffset();
            _hintArrow.sprite = ShapeSpriteFactory.Get(direction.IsWait() ? Shape.Wait : Shape.Arrow);
            _hintArrow.transform.position = _player.transform.position + new Vector3(offset.X, -offset.Y, 0f) * 0.62f;
            _hintArrow.transform.rotation = Quaternion.Euler(0f, 0f, DirectionAngle(direction));
            _hintArrow.enabled = true;
            _hintTime = 0f;
        }

        public void HideHint()
        {
            _hintTime = -1f;
            if (_hintArrow != null) _hintArrow.enabled = false;
        }

        /// <summary>Jumps every running transition to its end (focus loss, reduced motion).</summary>
        public void CompleteAnimations() => _tweens.CompleteAll();

        public Vector3 CellToWorld(GridPos cell)
        {
            var x = cell.X - (_level.Width - 1) * 0.5f;
            var y = (_level.Height - 1) * 0.5f - cell.Y;
            return new Vector3(x, y, 0f);
        }

        /// <summary>The board cell under a screen position (pixels, origin bottom left); false when off the board.</summary>
        public bool TryScreenToCell(Vector2 screenPosition, out GridPos cell)
        {
            cell = default;
            if (boardCamera == null || _level == null) return false;

            var world = boardCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -boardCamera.transform.position.z));
            cell = new GridPos(Mathf.RoundToInt(world.x + (_level.Width - 1) * 0.5f), Mathf.RoundToInt((_level.Height - 1) * 0.5f - world.y));
            return _level.InBounds(cell);
        }

        /// <summary>Screen height fractions covered by the HUD above and below the board; the board is fitted into the rest.</summary>
        public void SetReservedFractions(float top, float bottom)
        {
            top = Mathf.Clamp(top, 0f, MaxReserved);
            bottom = Mathf.Clamp(bottom, 0f, MaxReserved);
            if (Mathf.Approximately(top, topReserved) && Mathf.Approximately(bottom, bottomReserved)) return;

            topReserved = top;
            bottomReserved = bottom;
            FitCamera(force: true);
        }

        private void Update()
        {
            _tweens.Tick(Time.unscaledDeltaTime);
            FitCamera(force: false);

            if (_hintTime >= 0f && _hintArrow != null)
            {
                _hintTime += Time.unscaledDeltaTime;
                _hintArrow.color = Palette.WithAlpha(Palette.Hint, 0.55f + 0.45f * Mathf.Sin(_hintTime * 6f));
            }
        }

        private void MovePiece(Transform piece, GridPos from, GridPos to, float duration)
        {
            var start = CellToWorld(from);
            var end = CellToWorld(to);
            _tweens.Add(0f, duration, t => piece.position = Vector3.LerpUnclamped(start, end, t), () => piece.position = end);
        }

        private void ShowEchoBlocked(GridPos at, Direction attempted, float duration)
        {
            Flash(_echoFlash, Palette.Accent, at, Mathf.Max(duration * 2f, 0.3f));
            if (duration > 0f) Bump(_echo.transform, at, attempted, duration, PieceScale, blocking: true);
        }

        private void ToggleGates(ulong bitsAfter, float duration)
        {
            for (var i = 0; i < _gates.Count; i++)
            {
                var index = i;
                var open = (bitsAfter & (1UL << i)) != 0;
                var gate = _gates[i].transform;
                _tweens.Add(0f, duration, t =>
                {
                    if (t >= 0.5f) ApplyGate(index, open);
                    gate.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(t * Mathf.PI));
                }, () =>
                {
                    ApplyGate(index, open);
                    gate.localScale = Vector3.one;
                });
            }
        }

        private void ApplyGate(int index, bool open)
        {
            var renderer = _gates[index];
            renderer.sprite = ShapeSpriteFactory.Get(open ? Shape.GateOpen : Shape.GateClosed);
            renderer.color = open ? Palette.GateOpen : Palette.Gate;
        }

        private void Bump(Transform piece, GridPos cell, Direction direction, float duration, float scale, bool blocking)
        {
            var home = CellToWorld(cell);
            var offset = direction.ToOffset();
            var push = new Vector3(offset.X, -offset.Y, 0f) * 0.18f;
            _tweens.Add(0f, duration, t => piece.position = home + push * Mathf.Sin(t * Mathf.PI), () =>
            {
                piece.position = home;
                piece.localScale = Vector3.one * scale;
            }, blocking);
        }

        private void Pulse(Transform piece, float delay, float amount, float scale)
        {
            _tweens.Add(delay, 0.25f, t => piece.localScale = Vector3.one * scale * (1f + amount * Mathf.Sin(t * Mathf.PI)),
                () => piece.localScale = Vector3.one * scale, blocking: false);
        }

        private void Flash(SpriteRenderer renderer, Color color, GridPos cell, float duration)
        {
            renderer.transform.position = CellToWorld(cell);
            renderer.enabled = true;
            // Hold most of the alpha early so the flash stays readable, then fade out.
            _tweens.Add(0f, duration, t => renderer.color = Palette.WithAlpha(color, 1f - t * t * t), () => renderer.enabled = false, blocking: false);
        }

        private static void SetVisible(SpriteRenderer renderer, Vector3 position)
        {
            renderer.transform.position = position;
            renderer.enabled = true;
        }

        private static float DirectionAngle(Direction direction)
        {
            switch (direction)
            {
                case Direction.Right: return -90f;
                case Direction.Down: return 180f;
                case Direction.Left: return 90f;
                default: return 0f;
            }
        }

        private void FitCamera(bool force)
        {
            if (boardCamera == null || _level == null) return;
            if (!force && Mathf.Approximately(_lastAspect, boardCamera.aspect)) return;

            _lastAspect = boardCamera.aspect;
            var freeFraction = Mathf.Max(0.2f, 1f - topReserved - bottomReserved);
            var neededHeight = (Mathf.Max(_level.Height, MinFramedCells) + BoardPadding * 2f) / freeFraction;
            var neededHeightForWidth = (Mathf.Max(_level.Width, MinFramedCells) + BoardPadding * 2f) / Mathf.Max(0.1f, boardCamera.aspect);
            var totalHeight = Mathf.Max(neededHeight, neededHeightForWidth);

            boardCamera.orthographic = true;
            boardCamera.orthographicSize = totalHeight * 0.5f;
            var centerOffset = (bottomReserved - topReserved) * 0.5f * totalHeight;
            boardCamera.transform.position = new Vector3(0f, -centerOffset, -10f);
        }

        private SpriteRenderer Take(Shape shape, Color color, int order, Vector3 position, float scale)
        {
            SpriteRenderer renderer;
            if (_pool.Count > 0)
            {
                renderer = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                var go = new GameObject("BoardSprite");
                go.transform.SetParent(transform, false);
                renderer = go.AddComponent<SpriteRenderer>();
            }

            renderer.gameObject.SetActive(true);
            renderer.enabled = true;
            renderer.sprite = ShapeSpriteFactory.Get(shape);
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.position = position;
            renderer.transform.rotation = Quaternion.identity;
            renderer.transform.localScale = Vector3.one * scale;
            _inUse.Add(renderer);
            return renderer;
        }

        private void ReleaseAll()
        {
            foreach (var renderer in _inUse)
            {
                if (renderer == null) continue;
                renderer.gameObject.SetActive(false);
                _pool.Add(renderer);
            }

            _inUse.Clear();
            _gates.Clear();
        }
    }
}
