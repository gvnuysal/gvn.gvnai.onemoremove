using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;

namespace OneMoreMove.Session
{
    public enum HintKind
    {
        /// <summary>Stage 1: remind the player of the rule that matters next.</summary>
        MechanicReminder,

        /// <summary>Stage 2: highlight the next move of a shortest solution from the current state.</summary>
        SuggestedDirection,

        /// <summary>The current state is proven unsolvable: suggest undo or restart.</summary>
        SuggestUndo,

        /// <summary>The search did not finish ("İpucu hazırlanamadı").</summary>
        Unavailable,

        AlreadySolved
    }

    public enum MechanicTopic
    {
        Movement,
        Gates,
        Echo
    }

    public sealed class Hint
    {
        public Hint(HintKind kind, MechanicTopic topic, Direction? suggestedDirection, int? movesToGoal, int stateVersion)
        {
            Kind = kind;
            Topic = topic;
            SuggestedDirection = suggestedDirection;
            MovesToGoal = movesToGoal;
            StateVersion = stateVersion;
        }

        public HintKind Kind { get; }
        public MechanicTopic Topic { get; }
        public Direction? SuggestedDirection { get; }
        public int? MovesToGoal { get; }

        /// <summary>The <see cref="GameSession.StateVersion"/> the hint was computed for.</summary>
        public int StateVersion { get; }
    }

    /// <summary>
    /// Searches from the player's current state (never replays the level's start solution blindly).
    /// The search runs on a worker thread on pure data only.
    /// </summary>
    public sealed class HintService
    {
        private readonly SolverBudget _budget;

        public HintService(SolverBudget budget)
        {
            _budget = budget;
        }

        public Task<Hint> RequestAsync(LevelDefinition level, BoardState state, int stage, int stateVersion, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var result = BfsSolver.Solve(level, state, _budget, cancellationToken);
                return Build(level, state, stage, stateVersion, result);
            }, cancellationToken);
        }

        public static Hint Build(LevelDefinition level, BoardState state, int stage, int stateVersion, SolverResult result)
        {
            if (state.IsWon) return new Hint(HintKind.AlreadySolved, MechanicTopic.Movement, null, 0, stateVersion);

            switch (result.Status)
            {
                case SolverStatus.Unsolvable:
                    return new Hint(HintKind.SuggestUndo, DefaultTopic(level, state), null, null, stateVersion);
                case SolverStatus.Unknown:
                    return new Hint(HintKind.Unavailable, DefaultTopic(level, state), null, null, stateVersion);
            }

            var next = result.Path[0];
            var topic = TopicFor(level, state, next);
            return stage <= 1
                ? new Hint(HintKind.MechanicReminder, topic, null, result.Path.Count, stateVersion)
                : new Hint(HintKind.SuggestedDirection, topic, next, result.Path.Count, stateVersion);
        }

        private static MechanicTopic TopicFor(LevelDefinition level, BoardState state, Direction next)
        {
            // Waiting only ever matters because of gate timing.
            if (next.IsWait() && level.Gates.Count > 0) return MechanicTopic.Gates;

            var move = new RulesEngine(level).TryMove(state, next);
            if (move.Events.Any(e => e.Type == MoveEventType.EchoBlocked)) return MechanicTopic.Echo;
            if (level.GateIndexAt(move.NextState.Player) >= 0 || level.GateIndexAt(state.Player) >= 0) return MechanicTopic.Gates;
            return DefaultTopic(level, state);
        }

        private static MechanicTopic DefaultTopic(LevelDefinition level, BoardState state)
        {
            if (state.HasEcho) return MechanicTopic.Echo;
            if (level.Gates.Count > 0) return MechanicTopic.Gates;
            return MechanicTopic.Movement;
        }
    }
}
