using System;
using OneMoreMove.Core;
using UnityEngine.InputSystem;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Keyboard/gamepad bindings built in code. Button actions fire once per press: holding a key never produces a
    /// series of moves.
    /// </summary>
    public sealed class InputController : IDisposable
    {
        private readonly InputActionMap _gameplay = new InputActionMap("Gameplay");
        private readonly InputActionMap _global = new InputActionMap("Global");

        public InputController()
        {
            AddMove(Direction.Up, "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up");
            AddMove(Direction.Right, "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right");
            AddMove(Direction.Down, "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down");
            AddMove(Direction.Left, "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left");
            AddMove(Direction.Wait, "<Keyboard>/space", "<Keyboard>/period", "<Gamepad>/buttonSouth");
            Add(_gameplay, "Undo", () => Undo?.Invoke(), "<Keyboard>/z", "<Keyboard>/backspace", "<Gamepad>/buttonWest");
            Add(_gameplay, "Restart", () => Restart?.Invoke(), "<Keyboard>/r", "<Gamepad>/select");
            Add(_gameplay, "Hint", () => Hint?.Invoke(), "<Keyboard>/h", "<Gamepad>/buttonNorth");
            Add(_global, "Back", () => Back?.Invoke(), "<Keyboard>/escape", "<Gamepad>/buttonEast");
            _global.Enable();
        }

        public event Action<Direction> Move;
        public event Action Undo;
        public event Action Restart;
        public event Action Hint;
        public event Action Back;

        public bool GameplayEnabled => _gameplay.enabled;

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled) _gameplay.Enable();
            else _gameplay.Disable();
        }

        public void Dispose()
        {
            _gameplay.Dispose();
            _global.Dispose();
        }

        private void AddMove(Direction direction, params string[] bindings) =>
            Add(_gameplay, "Move" + direction, () => Move?.Invoke(direction), bindings);

        private static void Add(InputActionMap map, string name, Action handler, params string[] bindings)
        {
            var action = map.AddAction(name, InputActionType.Button);
            foreach (var binding in bindings) action.AddBinding(binding);
            action.performed += _ => handler();
        }
    }
}
