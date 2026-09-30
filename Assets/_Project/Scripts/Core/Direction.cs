using System;
using System.Collections.Generic;

namespace OneMoreMove.Core
{
    /// <summary>
    /// The four orthogonal move directions plus <see cref="Wait"/>, the zero step. There is no diagonal movement.
    /// Values are serialized (level assets, solutions); only append new members.
    /// </summary>
    public enum Direction
    {
        Up = 0,
        Right = 1,
        Down = 2,
        Left = 3,

        /// <summary>The player stays in place; the turn still passes (gates toggle, the move counts).</summary>
        Wait = 4
    }

    public static class Directions
    {
        /// <summary>All directions in a fixed order; the solver relies on this order for deterministic output.</summary>
        public static readonly IReadOnlyList<Direction> All = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left };

        /// <summary>Every player command: <see cref="All"/> followed by <see cref="Direction.Wait"/>, so equal-length solutions prefer moving.</summary>
        public static readonly IReadOnlyList<Direction> Commands = new[] { Direction.Up, Direction.Right, Direction.Down, Direction.Left, Direction.Wait };

        public static bool IsValid(this Direction direction) => (uint)direction <= (uint)Direction.Wait;

        public static bool IsWait(this Direction direction) => direction == Direction.Wait;

        public static Direction Opposite(this Direction direction)
        {
            EnsureDefined(direction);
            return direction == Direction.Wait ? Direction.Wait : (Direction)(((int)direction + 2) & 3);
        }

        /// <summary>Grid offset of one step. y grows downwards, so Up is (0,-1).</summary>
        public static GridPos ToOffset(this Direction direction)
        {
            switch (direction)
            {
                case Direction.Up: return new GridPos(0, -1);
                case Direction.Right: return new GridPos(1, 0);
                case Direction.Down: return new GridPos(0, 1);
                case Direction.Left: return new GridPos(-1, 0);
                case Direction.Wait: return new GridPos(0, 0);
                default: throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
            }
        }

        public static bool TryParse(string value, out Direction direction)
        {
            direction = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "up": case "u": direction = Direction.Up; return true;
                case "right": case "r": direction = Direction.Right; return true;
                case "down": case "d": direction = Direction.Down; return true;
                case "left": case "l": direction = Direction.Left; return true;
                case "wait": case ".": direction = Direction.Wait; return true;
                default: return false;
            }
        }

        private static void EnsureDefined(Direction direction)
        {
            if (!direction.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction.");
            }
        }
    }
}
