using System;
using System.Collections.Generic;

namespace OneMoreMove.Core
{
    /// <summary>
    /// Outcome of <see cref="RulesEngine.TryMove"/>. When rejected, <see cref="NextState"/> is the unchanged input state.
    /// </summary>
    public sealed class MoveResult
    {
        private static readonly MoveEvent[] NoEvents = Array.Empty<MoveEvent>();

        private MoveResult(bool accepted, Direction direction, BoardState previousState, BoardState nextState, IReadOnlyList<MoveEvent> events, RejectReason rejectReason)
        {
            Accepted = accepted;
            Direction = direction;
            PreviousState = previousState;
            NextState = nextState;
            Events = events;
            RejectReason = rejectReason;
        }

        public bool Accepted { get; }
        public Direction Direction { get; }
        public BoardState PreviousState { get; }
        public BoardState NextState { get; }
        public IReadOnlyList<MoveEvent> Events { get; }
        public RejectReason RejectReason { get; }

        /// <summary>For rejected moves: the cell the player tried to enter.</summary>
        public GridPos AttemptedTarget => PreviousState.Player.Step(Direction);

        internal static MoveResult Accept(Direction direction, BoardState previous, BoardState next, IReadOnlyList<MoveEvent> events) =>
            new MoveResult(true, direction, previous, next, events, RejectReason.None);

        internal static MoveResult Reject(Direction direction, BoardState state, RejectReason reason) =>
            new MoveResult(false, direction, state, state, NoEvents, reason);

        public override string ToString() => Accepted ? $"Accepted {Direction}: {NextState}" : $"Rejected {Direction}: {RejectReason}";
    }
}
