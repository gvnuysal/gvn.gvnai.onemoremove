using System;
using OneMoreMove.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Pointer gestures on the board: a swipe moves in its dominant direction (touch and mouse), a tap on the player's
    /// own cell waits (touch only, so a desktop click never costs a move). A press that starts on a UI control belongs
    /// to the UI and is ignored.
    /// </summary>
    public sealed class TouchGestures
    {
        /// <summary>Minimum swipe length as a fraction of the shorter screen side.</summary>
        private const float SwipeFraction = 0.05f;

        private const float TapSeconds = 0.35f;

        private readonly VisualElement _uiRoot;
        private readonly BoardView _board;
        private readonly Func<GridPos?> _playerCell;

        private bool _tracking;
        private bool _touch;
        private Vector2 _start;
        private float _startTime;

        public TouchGestures(VisualElement uiRoot, BoardView board, Func<GridPos?> playerCell)
        {
            _uiRoot = uiRoot ?? throw new ArgumentNullException(nameof(uiRoot));
            _board = board ? board : throw new ArgumentNullException(nameof(board));
            _playerCell = playerCell ?? throw new ArgumentNullException(nameof(playerCell));
        }

        public event Action<Direction> Move;

        /// <summary>Call once per frame; <paramref name="active"/> is false while the board is not being played.</summary>
        public void Tick(bool active)
        {
            var pointer = Pointer.current;
            if (pointer == null || !active)
            {
                _tracking = false;
                return;
            }

            if (pointer.press.wasPressedThisFrame)
            {
                _start = pointer.position.ReadValue();
                _startTime = Time.unscaledTime;
                _touch = pointer is Touchscreen;
                _tracking = !IsOverControl(_start);
            }

            if (!_tracking || !pointer.press.wasReleasedThisFrame) return;
            _tracking = false;

            var delta = pointer.position.ReadValue() - _start;
            var threshold = Mathf.Min(Screen.width, Screen.height) * SwipeFraction;
            if (delta.magnitude >= threshold)
            {
                Move?.Invoke(DominantDirection(delta));
                return;
            }

            if (_touch && Time.unscaledTime - _startTime <= TapSeconds && _board.TryScreenToCell(_start, out var cell) && cell == _playerCell())
            {
                Move?.Invoke(Direction.Wait);
            }
        }

        /// <summary>Screen coordinates grow upwards, so a positive y delta is Up.</summary>
        public static Direction DominantDirection(Vector2 screenDelta) =>
            Mathf.Abs(screenDelta.x) >= Mathf.Abs(screenDelta.y)
                ? (screenDelta.x >= 0f ? Direction.Right : Direction.Left)
                : (screenDelta.y >= 0f ? Direction.Up : Direction.Down);

        private bool IsOverControl(Vector2 screenPosition)
        {
            var panel = _uiRoot.panel;
            if (panel == null) return false;

            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            for (var element = panel.Pick(panelPosition); element != null; element = element.parent)
            {
                if (element is Button || element is Toggle || element is Slider || element is SliderInt || element is ScrollView) return true;
            }

            return false;
        }
    }
}
