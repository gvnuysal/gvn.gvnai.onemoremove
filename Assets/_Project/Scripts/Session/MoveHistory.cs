using System.Collections.Generic;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>
    /// Recovers the command sequence of a session from its state history (undo states + current state), so a finished
    /// run can be verified by replaying it. No extra save data is needed: every accepted directional move shifts the
    /// player by one cell and a wait keeps it in place.
    /// </summary>
    public static class MoveHistory
    {
        /// <summary>The commands from the level start to the current state, or null when the history is incomplete.</summary>
        public static IReadOnlyList<Direction> Reconstruct(GameSession session)
        {
            if (session == null) return null;

            var states = new List<BoardState>(session.History.ToList()) { session.State };
            if (states.Count != session.State.MoveCount + 1 || states[0].MoveCount != 0) return null;

            var commands = new List<Direction>(states.Count - 1);
            for (var i = 1; i < states.Count; i++)
            {
                var step = states[i].Player - states[i - 1].Player;
                if (!TryDirection(step, out var direction)) return null;
                commands.Add(direction);
            }

            return commands;
        }

        private static bool TryDirection(GridPos step, out Direction direction)
        {
            foreach (var candidate in Directions.Commands)
            {
                if (candidate.ToOffset() == step)
                {
                    direction = candidate;
                    return true;
                }
            }

            direction = default;
            return false;
        }
    }
}
