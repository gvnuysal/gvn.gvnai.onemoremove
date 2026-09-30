using System;
using System.Collections.Generic;
using System.Linq;

namespace OneMoreMove.Core.Solving
{
    /// <summary>Design metrics for one level; every number comes from searches through the real <see cref="RulesEngine"/>.</summary>
    public sealed class LevelAnalysis
    {
        internal LevelAnalysis(
            bool complete,
            IReadOnlyList<Direction> solution,
            long optimalSolutionCount,
            int echoBlocksInSolution,
            int? optimalWithoutWait,
            int? optimalWithoutGates,
            int? optimalWithoutEcho,
            int reachableStates,
            double deadEndRatio)
        {
            Complete = complete;
            Solution = solution;
            OptimalSolutionCount = optimalSolutionCount;
            EchoBlocksInSolution = echoBlocksInSolution;
            OptimalWithoutWait = optimalWithoutWait;
            OptimalWithoutGates = optimalWithoutGates;
            OptimalWithoutEcho = optimalWithoutEcho;
            ReachableStates = reachableStates;
            DeadEndRatio = deadEndRatio;
        }

        /// <summary>False when a search hit the state limit; the other numbers are then lower bounds or unknown.</summary>
        public bool Complete { get; }

        /// <summary>A shortest solution, or null when the level is unsolvable.</summary>
        public IReadOnlyList<Direction> Solution { get; }

        public bool IsSolvable => Solution != null;
        public int? OptimalMoves => Solution?.Count;

        /// <summary>Distinct shortest command sequences (saturates at <see cref="long.MaxValue"/>). 1 means a unique solution.</summary>
        public long OptimalSolutionCount { get; }

        public int WaitsInSolution => Solution?.Count(d => d == Direction.Wait) ?? 0;
        public int EchoBlocksInSolution { get; }

        /// <summary>Shortest solution using directional moves only; null when Wait is required.</summary>
        public int? OptimalWithoutWait { get; }

        /// <summary>Shortest solution with every gate turned into floor. A value below the optimum means gates cost moves.</summary>
        public int? OptimalWithoutGates { get; }

        /// <summary>Shortest solution with the echo removed; null when the level has no echo or cannot be solved without it.</summary>
        public int? OptimalWithoutEcho { get; }

        public int ReachableStates { get; }

        /// <summary>Share of reachable states from which the goal can no longer be reached (only undo or restart helps).</summary>
        public double DeadEndRatio { get; }

        public bool RequiresWait => IsSolvable && (OptimalWithoutWait == null || OptimalWithoutWait > OptimalMoves);
    }

    /// <summary>
    /// Difficulty report used by the editor tools, the release gate and the LevelLab console tool. Rules are never
    /// reimplemented here: every transition is <see cref="RulesEngine.TryMove(BoardState, Direction)"/>.
    /// </summary>
    public static class LevelAnalyzer
    {
        public const int DefaultMaxStates = 2_000_000;

        public static LevelAnalysis Analyze(LevelDefinition level, int maxStates = DefaultMaxStates)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));

            var main = Search(level, Directions.Commands, maxStates);
            var withoutWait = Search(level, Directions.All, maxStates);
            var withoutGates = level.Gates.Count > 0 ? Search(WithoutGates(level), Directions.Commands, maxStates) : main;
            var withoutEcho = level.EchoStart.HasValue ? Search(WithoutEcho(level), Directions.Commands, maxStates) : null;
            var graph = Explore(level, maxStates);

            var complete = main.Complete && withoutWait.Complete && withoutGates.Complete && (withoutEcho?.Complete ?? true) && graph.Complete;
            return new LevelAnalysis(
                complete,
                main.Path,
                main.PathCount,
                main.Path == null ? 0 : CountEchoBlocks(level, main.Path),
                withoutWait.Path?.Count,
                withoutGates.Path?.Count,
                withoutEcho?.Path?.Count,
                graph.States,
                graph.DeadEndRatio);
        }

        public static LevelDefinition WithoutGates(LevelDefinition level) => Copy(level, Array.Empty<GateDefinition>(), level.EchoStart);

        public static LevelDefinition WithoutEcho(LevelDefinition level) => Copy(level, level.Gates, null);

        private static LevelDefinition Copy(LevelDefinition level, IEnumerable<GateDefinition> gates, GridPos? echo) =>
            new LevelDefinition(level.Id, level.Name, level.Revision, level.RulesVersion, level.Width, level.Height, level.Walls,
                gates, level.PlayerStart, echo, level.Goal, level.ParMoves, null, null, level.IsTutorial, level.Tip);

        private static int CountEchoBlocks(LevelDefinition level, IReadOnlyList<Direction> path)
        {
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            var blocks = 0;
            foreach (var command in path)
            {
                var result = rules.TryMove(state, command);
                blocks += result.Events.Count(e => e.Type == MoveEventType.EchoBlocked);
                state = result.NextState;
            }

            return blocks;
        }

        private sealed class SearchResult
        {
            public bool Complete;
            public IReadOnlyList<Direction> Path;
            public long PathCount;
        }

        /// <summary>Layered BFS that also counts shortest command sequences.</summary>
        private static SearchResult Search(LevelDefinition level, IReadOnlyList<Direction> commands, int maxStates)
        {
            var rules = new RulesEngine(level);
            var start = level.CreateInitialState();
            if (start.IsWon) return new SearchResult { Complete = true, Path = Array.Empty<Direction>(), PathCount = 1 };

            var startKey = start.ToKey();
            var depth = new Dictionary<StateKey, int> { [startKey] = 0 };
            var ways = new Dictionary<StateKey, long> { [startKey] = 1 };
            var parents = new Dictionary<StateKey, (StateKey Key, Direction Command)>();
            var layer = new List<BoardState> { start };
            var level0 = 0;

            while (layer.Count > 0)
            {
                var next = new List<BoardState>();
                long winning = 0;
                (StateKey Key, Direction Command)? firstWin = null;

                foreach (var state in layer)
                {
                    var key = state.ToKey();
                    foreach (var command in commands)
                    {
                        var result = rules.TryMove(state, command);
                        if (!result.Accepted) continue;

                        var nextState = result.NextState;
                        if (nextState.IsWon)
                        {
                            winning = SaturatingAdd(winning, ways[key]);
                            if (firstWin == null) firstWin = (key, command);
                            continue;
                        }

                        var nextKey = nextState.ToKey();
                        if (!depth.TryGetValue(nextKey, out var d))
                        {
                            if (depth.Count >= maxStates) return new SearchResult { Complete = false };
                            depth.Add(nextKey, level0 + 1);
                            ways.Add(nextKey, ways[key]);
                            parents.Add(nextKey, (key, command));
                            next.Add(nextState);
                        }
                        else if (d == level0 + 1)
                        {
                            ways[nextKey] = SaturatingAdd(ways[nextKey], ways[key]);
                        }
                    }
                }

                if (firstWin != null)
                {
                    var path = new List<Direction> { firstWin.Value.Command };
                    var cursor = firstWin.Value.Key;
                    while (cursor != startKey)
                    {
                        var parent = parents[cursor];
                        path.Add(parent.Command);
                        cursor = parent.Key;
                    }

                    path.Reverse();
                    return new SearchResult { Complete = true, Path = path, PathCount = winning };
                }

                layer = next;
                level0++;
            }

            return new SearchResult { Complete = true };
        }

        private sealed class GraphResult
        {
            public bool Complete;
            public int States;
            public double DeadEndRatio;
        }

        /// <summary>Explores every reachable state, then walks backwards from the won states to find dead ends.</summary>
        private static GraphResult Explore(LevelDefinition level, int maxStates)
        {
            var rules = new RulesEngine(level);
            var start = level.CreateInitialState();
            var startKey = start.ToKey();
            var seen = new HashSet<StateKey> { startKey };
            var predecessors = new Dictionary<StateKey, List<StateKey>>();
            var won = new List<StateKey>();
            var queue = new Queue<BoardState>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                var key = state.ToKey();
                if (state.IsWon)
                {
                    won.Add(key);
                    continue;
                }

                foreach (var command in Directions.Commands)
                {
                    var result = rules.TryMove(state, command);
                    if (!result.Accepted) continue;

                    var nextKey = result.NextState.ToKey();
                    if (!predecessors.TryGetValue(nextKey, out var list)) predecessors[nextKey] = list = new List<StateKey>();
                    list.Add(key);
                    if (seen.Add(nextKey))
                    {
                        if (seen.Count > maxStates) return new GraphResult { Complete = false, States = seen.Count };
                        queue.Enqueue(result.NextState);
                    }
                }
            }

            var alive = new HashSet<StateKey>(won);
            var back = new Queue<StateKey>(won);
            while (back.Count > 0)
            {
                if (!predecessors.TryGetValue(back.Dequeue(), out var list)) continue;
                foreach (var previous in list)
                {
                    if (alive.Add(previous)) back.Enqueue(previous);
                }
            }

            return new GraphResult { Complete = true, States = seen.Count, DeadEndRatio = 1.0 - (double)alive.Count / seen.Count };
        }

        private static long SaturatingAdd(long a, long b) => a > long.MaxValue - b ? long.MaxValue : a + b;
    }
}
