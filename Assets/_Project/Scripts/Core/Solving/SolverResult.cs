using System;
using System.Collections.Generic;

namespace OneMoreMove.Core.Solving
{
    public enum SolverStatus
    {
        /// <summary>A shortest path was found.</summary>
        Solved,

        /// <summary>The whole reachable state space was searched without reaching the goal (a proof).</summary>
        Unsolvable,

        /// <summary>The search stopped early (budget or cancellation); nothing is proven.</summary>
        Unknown
    }

    public enum SolverStopReason
    {
        None,
        StateBudget,
        TimeBudget,
        Cancelled
    }

    public sealed class SolverResult
    {
        private SolverResult(SolverStatus status, IReadOnlyList<Direction> path, int exploredStates, SolverStopReason stopReason, TimeSpan elapsed)
        {
            Status = status;
            Path = path;
            ExploredStates = exploredStates;
            StopReason = stopReason;
            Elapsed = elapsed;
        }

        public SolverStatus Status { get; }

        /// <summary>Shortest command sequence when <see cref="Status"/> is Solved; otherwise empty.</summary>
        public IReadOnlyList<Direction> Path { get; }

        public int ExploredStates { get; }
        public SolverStopReason StopReason { get; }
        public TimeSpan Elapsed { get; }

        internal static SolverResult Solved(IReadOnlyList<Direction> path, int explored, TimeSpan elapsed) =>
            new SolverResult(SolverStatus.Solved, path, explored, SolverStopReason.None, elapsed);

        internal static SolverResult Unsolvable(int explored, TimeSpan elapsed) =>
            new SolverResult(SolverStatus.Unsolvable, Array.Empty<Direction>(), explored, SolverStopReason.None, elapsed);

        internal static SolverResult Unknown(SolverStopReason reason, int explored, TimeSpan elapsed) =>
            new SolverResult(SolverStatus.Unknown, Array.Empty<Direction>(), explored, reason, elapsed);

        public override string ToString() => Status == SolverStatus.Solved
            ? $"Solved in {Path.Count} moves ({ExploredStates} states, {Elapsed.TotalMilliseconds:0} ms)"
            : $"{Status} {(StopReason != SolverStopReason.None ? StopReason.ToString() : string.Empty)} ({ExploredStates} states, {Elapsed.TotalMilliseconds:0} ms)";
    }
}
