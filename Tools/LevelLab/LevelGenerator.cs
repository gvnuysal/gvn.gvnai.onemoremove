using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;

namespace OneMoreMove.LevelLab
{
    /// <summary>What a band of the content plan asks for. Null or zero limits are not checked.</summary>
    internal sealed class GeneratorProfile
    {
        public (int W, int H)[] Sizes = { (5, 5) };
        public (int Min, int Max) Walls = (3, 7);
        public (int Min, int Max) Gates = (0, 0);
        public bool Echo;
        public (int Min, int Max) Optimal = (6, 10);
        public bool RequireWait;

        /// <summary>For levels before Wait is taught: waiting must never shorten the solution.</summary>
        public bool ForbidWaitBenefit;

        /// <summary>At least one gate that is closed at the start lies on the shortest solution (it opens in time).</summary>
        public bool ClosedGateOnPath;
        public int MinWaits;
        public int MaxWaits = int.MaxValue;

        /// <summary>A longer route without waiting must exist, so waiting is a choice rather than the only way.</summary>
        public bool RequireNoWaitRoute;
        public int MinGateCost;
        public int MinEchoEffect;
        public long MaxSolutions = 6;
        public double MinDeadEnds;

        public static readonly IReadOnlyDictionary<string, GeneratorProfile> All = new Dictionary<string, GeneratorProfile>
        {
            ["moves"] = new GeneratorProfile { Walls = (4, 9), Optimal = (6, 10), MaxSolutions = 3 },
            ["gate-rhythm"] = new GeneratorProfile { Gates = (2, 4), Optimal = (6, 10), ForbidWaitBenefit = true, ClosedGateOnPath = true, MinGateCost = 2, MaxSolutions = 2 },
            ["gate-final"] = new GeneratorProfile { Sizes = new[] { (5, 5), (6, 5), (6, 6) }, Walls = (3, 10), Gates = (3, 7), Optimal = (10, 16), RequireWait = true, MaxWaits = 3, RequireNoWaitRoute = true, ClosedGateOnPath = true, MinGateCost = 3, MaxSolutions = 3 },
            ["gates"] = new GeneratorProfile { Gates = (2, 4), Optimal = (6, 10), RequireWait = true, MinGateCost = 3 },
            ["gates-hard"] = new GeneratorProfile { Sizes = new[] { (5, 5), (6, 5) }, Walls = (3, 8), Gates = (3, 6), Optimal = (9, 14), RequireWait = true, MinWaits = 2, MinGateCost = 4, MaxSolutions = 4 },
            ["echo"] = new GeneratorProfile { Echo = true, Optimal = (6, 10), MinEchoEffect = 3, MaxSolutions = 4 },
            ["mixed"] = new GeneratorProfile { Sizes = new[] { (5, 5), (6, 5) }, Walls = (3, 8), Gates = (1, 3), Echo = true, Optimal = (7, 12), MinGateCost = 2, MinEchoEffect = 2 },
            ["long"] = new GeneratorProfile { Sizes = new[] { (6, 6), (7, 5) }, Walls = (5, 11), Gates = (2, 5), Echo = true, Optimal = (13, 20), MinGateCost = 3, MinEchoEffect = 2, MaxSolutions = 8, MinDeadEnds = 0.1 },
            ["master"] = new GeneratorProfile { Sizes = new[] { (5, 5), (6, 5), (6, 6) }, Walls = (3, 9), Gates = (2, 5), Echo = true, Optimal = (9, 15), MinGateCost = 4, MinEchoEffect = 3, MaxSolutions = 3, MinDeadEnds = 0.2 },
        };
    }

    internal sealed class Candidate
    {
        public LevelDefinition Level;
        public LevelAnalysis Analysis;
        public double Score;
    }

    /// <summary>Random maps filtered by a <see cref="GeneratorProfile"/> and ranked; a designer still picks and polishes.</summary>
    internal static class LevelGenerator
    {
        public static List<Candidate> Search(GeneratorProfile profile, int seed, int tries)
        {
            var random = new Random(seed);
            var seen = new HashSet<string>();
            var found = new List<Candidate>();

            for (var i = 0; i < tries; i++)
            {
                var rows = RandomMap(random, profile);
                if (rows == null || !seen.Add(string.Join("/", rows))) continue;

                var level = AsciiLevelParser.Parse(rows);
                if (!LevelValidator.Validate(level).IsValid) continue;

                var candidate = Evaluate(level, profile);
                if (candidate != null) found.Add(candidate);
            }

            return found.OrderByDescending(c => c.Score).ToList();
        }

        private static Candidate Evaluate(LevelDefinition level, GeneratorProfile p)
        {
            // Cheap filter first: the plain solver rejects most maps before the full analysis runs.
            var quick = BfsSolver.Solve(level, SolverBudget.Default);
            if (quick.Status != SolverStatus.Solved || quick.Path.Count < p.Optimal.Min || quick.Path.Count > p.Optimal.Max) return null;

            var a = LevelAnalyzer.Analyze(level, 200_000);
            if (!a.Complete || !a.IsSolvable) return null;
            var opt = a.OptimalMoves.Value;

            if (a.OptimalSolutionCount > p.MaxSolutions) return null;
            if (p.RequireWait && !a.RequiresWait) return null;
            if (p.ForbidWaitBenefit && a.OptimalWithoutWait != opt) return null;
            if (p.ClosedGateOnPath && !CrossesInitiallyClosedGate(level, a.Solution)) return null;
            if (a.WaitsInSolution < p.MinWaits || a.WaitsInSolution > p.MaxWaits) return null;
            if (p.RequireNoWaitRoute && a.OptimalWithoutWait == null) return null;
            if (p.MinGateCost > 0 && a.OptimalWithoutGates.HasValue && opt - a.OptimalWithoutGates.Value < p.MinGateCost) return null;
            if (p.MinEchoEffect > 0)
            {
                var echoCost = a.OptimalWithoutEcho.HasValue ? opt - a.OptimalWithoutEcho.Value : p.MinEchoEffect;
                if (echoCost < p.MinEchoEffect && a.EchoBlocksInSolution < p.MinEchoEffect) return null;
            }

            if (a.DeadEndRatio < p.MinDeadEnds) return null;

            // Prefer few solutions, real traps, and solutions that use the mechanics.
            var score = -3.0 * Math.Min(a.OptimalSolutionCount, 10) + 10 * a.DeadEndRatio + Math.Min(a.EchoBlocksInSolution, 4)
                        + Math.Min(a.WaitsInSolution, 3) + 0.2 * opt;
            return new Candidate { Level = level, Analysis = a, Score = score };
        }

        private static bool CrossesInitiallyClosedGate(LevelDefinition level, IReadOnlyList<Direction> path)
        {
            var cell = level.PlayerStart;
            foreach (var command in path)
            {
                cell = cell.Step(command);
                var gate = level.GateIndexAt(cell);
                if (gate >= 0 && !level.Gates[gate].InitiallyOpen) return true;
            }

            return false;
        }

        private static string[] RandomMap(Random random, GeneratorProfile p)
        {
            var (w, h) = p.Sizes[random.Next(p.Sizes.Length)];
            var grid = new char[h][];
            for (var y = 0; y < h; y++) grid[y] = Enumerable.Repeat('.', w).ToArray();

            var cells = Enumerable.Range(0, w * h).OrderBy(_ => random.Next()).GetEnumerator();
            GridPos Put(char c)
            {
                cells.MoveNext();
                var pos = new GridPos(cells.Current % w, cells.Current / w);
                grid[pos.Y][pos.X] = c;
                return pos;
            }

            var player = Put('P');
            var goal = Put('G');
            if (Math.Abs(player.X - goal.X) + Math.Abs(player.Y - goal.Y) < 3) return null;
            if (p.Echo) Put('E');

            var cellsLeft = w * h - (p.Echo ? 3 : 2);
            var gates = random.Next(p.Gates.Min, p.Gates.Max + 1);
            var walls = random.Next(p.Walls.Min, p.Walls.Max + 1);
            if (gates + walls > cellsLeft) return null;
            for (var i = 0; i < gates; i++) Put(random.Next(2) == 0 ? 'o' : 'x');
            for (var i = 0; i < walls; i++) Put('#');

            return grid.Select(r => new string(r)).ToArray();
        }
    }
}
