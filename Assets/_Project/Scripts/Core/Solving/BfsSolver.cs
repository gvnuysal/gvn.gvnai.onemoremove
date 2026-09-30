using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace OneMoreMove.Core.Solving
{
    /// <summary>
    /// Breadth-first search over <see cref="StateKey"/>s using the real <see cref="RulesEngine"/>. Every accepted move
    /// costs 1, so the first path that reaches the goal is a shortest one. Pure data only: safe on a worker thread.
    /// </summary>
    public static class BfsSolver
    {
        private const int CheckInterval = 256;

        public static SolverResult Solve(LevelDefinition level, SolverBudget budget, CancellationToken cancellationToken = default) =>
            Solve(level, level.CreateInitialState(), budget, cancellationToken);

        public static SolverResult Solve(LevelDefinition level, BoardState start, SolverBudget budget, CancellationToken cancellationToken = default)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (start == null) throw new ArgumentNullException(nameof(start));

            var stopwatch = Stopwatch.StartNew();
            var rules = new RulesEngine(level);
            if (rules.CheckWin(start)) return SolverResult.Solved(Array.Empty<Direction>(), 0, stopwatch.Elapsed);

            var startKey = start.ToKey();
            var parents = new Dictionary<StateKey, Parent> { [startKey] = default };
            var queue = new Queue<BoardState>();
            queue.Enqueue(start);
            var explored = 0;

            while (queue.Count > 0)
            {
                if (explored % CheckInterval == 0)
                {
                    if (cancellationToken.IsCancellationRequested) return SolverResult.Unknown(SolverStopReason.Cancelled, explored, stopwatch.Elapsed);
                    if (stopwatch.Elapsed > budget.MaxDuration) return SolverResult.Unknown(SolverStopReason.TimeBudget, explored, stopwatch.Elapsed);
                }

                if (explored >= budget.MaxStates) return SolverResult.Unknown(SolverStopReason.StateBudget, explored, stopwatch.Elapsed);

                var current = queue.Dequeue();
                var currentKey = current.ToKey();
                explored++;

                foreach (var direction in Directions.Commands)
                {
                    var result = rules.TryMove(current, direction);
                    if (!result.Accepted) continue;

                    var nextKey = result.NextState.ToKey();
                    if (parents.ContainsKey(nextKey)) continue;

                    parents.Add(nextKey, new Parent(currentKey, direction));
                    if (result.NextState.IsWon)
                    {
                        return SolverResult.Solved(BuildPath(parents, startKey, nextKey), explored, stopwatch.Elapsed);
                    }

                    queue.Enqueue(result.NextState);
                }
            }

            return SolverResult.Unsolvable(explored, stopwatch.Elapsed);
        }

        private static IReadOnlyList<Direction> BuildPath(Dictionary<StateKey, Parent> parents, StateKey startKey, StateKey goalKey)
        {
            var path = new List<Direction>();
            var key = goalKey;
            while (key != startKey)
            {
                var parent = parents[key];
                path.Add(parent.Direction);
                key = parent.Key;
            }

            path.Reverse();
            return path;
        }

        private readonly struct Parent
        {
            public readonly StateKey Key;
            public readonly Direction Direction;

            public Parent(StateKey key, Direction direction)
            {
                Key = key;
                Direction = direction;
            }
        }
    }
}
