using System.Linq;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;

namespace OneMoreMove.LevelLab
{
    internal static class Report
    {
        public static string Line(string name, LevelDefinition level, LevelAnalysis a)
        {
            if (!a.IsSolvable) return $"{name,-24} UNSOLVABLE{(a.Complete ? "" : " (incomplete search)")}";

            return $"{name,-24} opt={a.OptimalMoves,2} par={level.ParMoves,2}/{SuggestedPar(a.OptimalMoves.Value),-2} sol={Solution(a),-16} ways={a.OptimalSolutionCount,-3} " +
                   $"wait={a.WaitsInSolution} eblk={a.EchoBlocksInSolution} | noWait={Opt(a.OptimalWithoutWait)} " +
                   $"noGates={Opt(a.OptimalWithoutGates)} noEcho={(level.EchoStart.HasValue ? Opt(a.OptimalWithoutEcho) : "-")} " +
                   $"states={a.ReachableStates} dead={a.DeadEndRatio:0.00}{(a.Complete ? "" : " INCOMPLETE")}";
        }

        public static int SuggestedPar(int optimal) => LevelAnalyzer.SuggestedPar(optimal);

        public static string Board(LevelDefinition level) => string.Join("\n", AsciiLevelParser.Render(level).Select(r => "    " + r));

        private static string Solution(LevelAnalysis a) =>
            string.Concat(a.Solution.Select(d => d == Direction.Wait ? "W" : d.ToString().Substring(0, 1)));

        private static string Opt(int? value) => value?.ToString() ?? "x";
    }
}
