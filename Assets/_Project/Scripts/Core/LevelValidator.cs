using System.Collections.Generic;
using System.Linq;

namespace OneMoreMove.Core
{
    public enum ValidationSeverity
    {
        Warning,
        Error
    }

    public readonly struct ValidationIssue
    {
        public readonly ValidationSeverity Severity;
        public readonly string Code;
        public readonly string Message;

        public ValidationIssue(ValidationSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public override string ToString() => $"[{Severity}] {Code}: {Message}";
    }

    public sealed class ValidationReport
    {
        public ValidationReport(IReadOnlyList<ValidationIssue> issues)
        {
            Issues = issues;
        }

        public IReadOnlyList<ValidationIssue> Issues { get; }
        public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);
        public IEnumerable<ValidationIssue> Errors => Issues.Where(i => i.Severity == ValidationSeverity.Error);

        public override string ToString() => Issues.Count == 0 ? "OK" : string.Join("\n", Issues);
    }

    /// <summary>Structural checks. Solvability is proven separately by the solver and a replay of the known solution.</summary>
    public static class LevelValidator
    {
        public static ValidationReport Validate(LevelDefinition level)
        {
            var issues = new List<ValidationIssue>();
            void Error(string code, string message) => issues.Add(new ValidationIssue(ValidationSeverity.Error, code, message));
            void Warning(string code, string message) => issues.Add(new ValidationIssue(ValidationSeverity.Warning, code, message));

            if (string.IsNullOrWhiteSpace(level.Id)) Error("id.missing", "Level id is empty.");
            if (level.Revision < 1) Error("revision.invalid", "Revision must be at least 1.");
            if (level.RulesVersion < 1 || level.RulesVersion > RulesEngine.Version)
                Error("rules.unsupported", $"Rules version {level.RulesVersion} is not supported (current {RulesEngine.Version}).");
            if (level.CellCount < 2) Error("size.tooSmall", "A board needs at least two cells.");

            var seenWalls = new HashSet<GridPos>();
            foreach (var wall in level.Walls)
            {
                if (!level.InBounds(wall)) Error("wall.outOfBounds", $"Wall {wall} is outside the board.");
                if (!seenWalls.Add(wall)) Warning("wall.duplicate", $"Wall {wall} is listed twice.");
            }

            var seenGates = new HashSet<GridPos>();
            foreach (var gate in level.Gates)
            {
                if (!level.InBounds(gate.Position)) Error("gate.outOfBounds", $"Gate {gate.Position} is outside the board.");
                if (!seenGates.Add(gate.Position)) Error("gate.duplicate", $"More than one gate at {gate.Position}.");
                if (level.IsWall(gate.Position)) Error("gate.onWall", $"Gate {gate.Position} is on a wall.");
            }

            CheckCell(level, level.PlayerStart, "player", Error);
            CheckCell(level, level.Goal, "goal", Error);
            if (level.EchoStart.HasValue)
            {
                CheckCell(level, level.EchoStart.Value, "echo", Error);
                if (level.EchoStart.Value == level.PlayerStart) Error("echo.onPlayer", "Player and echo must start on different cells.");
            }

            if (level.PlayerStart == level.Goal)
            {
                if (level.IsTutorial) Warning("player.onGoal", "Tutorial starts on the goal.");
                else Error("player.onGoal", "Player starts on the goal.");
            }

            if (level.ParMoves < 1) Error("par.invalid", "Par moves must be at least 1.");
            if (level.OptimalMoves.HasValue)
            {
                if (level.OptimalMoves.Value < 1) Error("optimal.invalid", "Optimal moves must be at least 1.");
                if (level.OptimalMoves.Value > level.ParMoves) Warning("par.belowOptimal", $"Par {level.ParMoves} is below the optimal {level.OptimalMoves.Value}.");
                if (level.KnownSolution.Count != level.OptimalMoves.Value)
                    Error("solution.length", $"Known solution has {level.KnownSolution.Count} moves but optimal is {level.OptimalMoves.Value}.");
            }

            if (level.KnownSolution.Any(d => !d.IsValid())) Error("solution.command", "Known solution contains an undefined command.");
            if (level.KnownSolution.Count == 0) Warning("solution.missing", "No known solution stored.");

            return new ValidationReport(issues);
        }

        private static void CheckCell(LevelDefinition level, GridPos cell, string what, System.Action<string, string> error)
        {
            if (!level.InBounds(cell)) error($"{what}.outOfBounds", $"The {what} cell {cell} is outside the board.");
            else if (level.IsWall(cell)) error($"{what}.onWall", $"The {what} cell {cell} is a wall.");
        }
    }
}
