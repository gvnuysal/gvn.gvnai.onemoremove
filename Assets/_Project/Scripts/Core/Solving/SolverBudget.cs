using System;

namespace OneMoreMove.Core.Solving
{
    /// <summary>Limits for a search. Exhausting a budget yields <see cref="SolverStatus.Unknown"/>, never Unsolvable.</summary>
    public readonly struct SolverBudget
    {
        public readonly int MaxStates;
        public readonly TimeSpan MaxDuration;

        public SolverBudget(int maxStates, TimeSpan maxDuration)
        {
            if (maxStates < 1) throw new ArgumentOutOfRangeException(nameof(maxStates));
            MaxStates = maxStates;
            MaxDuration = maxDuration;
        }

        /// <summary>Generous for 5×5 boards (≈38k reachable states at most with 6 gates).</summary>
        public static SolverBudget Default => new SolverBudget(500_000, TimeSpan.FromSeconds(3));

        /// <summary>Hint searches run while the player waits, so they get a tighter time limit.</summary>
        public static SolverBudget Hint => new SolverBudget(500_000, TimeSpan.FromSeconds(1.5));

        /// <summary>Editor and release checks: large enough to finish every shipped level.</summary>
        public static SolverBudget Exhaustive => new SolverBudget(20_000_000, TimeSpan.FromMinutes(2));
    }
}
