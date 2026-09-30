using System.Collections.Generic;

namespace OneMoreMove.Core
{
    public sealed class ReplayResult
    {
        public ReplayResult(bool reachedGoal, BoardState finalState, int failedStep, RejectReason failureReason)
        {
            ReachedGoal = reachedGoal;
            FinalState = finalState;
            FailedStep = failedStep;
            FailureReason = failureReason;
        }

        /// <summary>True when every command was accepted and the last one won the level.</summary>
        public bool ReachedGoal { get; }
        public BoardState FinalState { get; }

        /// <summary>Zero-based index of the first rejected command, or -1.</summary>
        public int FailedStep { get; }
        public RejectReason FailureReason { get; }
    }

    /// <summary>Replays commands through the real rules; used to verify stored solutions and for determinism tests.</summary>
    public static class SolutionReplayer
    {
        public static ReplayResult Replay(LevelDefinition level, IEnumerable<Direction> commands) =>
            Replay(new RulesEngine(level), level.CreateInitialState(), commands);

        public static ReplayResult Replay(RulesEngine rules, BoardState start, IEnumerable<Direction> commands)
        {
            var state = start;
            var step = 0;
            foreach (var direction in commands)
            {
                var result = rules.TryMove(state, direction);
                if (!result.Accepted) return new ReplayResult(false, state, step, result.RejectReason);
                state = result.NextState;
                step++;
            }

            return new ReplayResult(state.IsWon, state, -1, RejectReason.None);
        }
    }
}
