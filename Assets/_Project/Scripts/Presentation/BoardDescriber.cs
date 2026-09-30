using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;
using static OneMoreMove.Presentation.Localization;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Spoken descriptions of the board and of moves for screen readers. Columns and rows count from 1, starting at
    /// the top left, like the level editor.
    /// </summary>
    public static class BoardDescriber
    {
        public static string Describe(LevelDefinition level, BoardState state)
        {
            var parts = new List<string>
            {
                L($"Tahta {level.Width} sütun, {level.Height} satır.", $"Board {level.Width} columns, {level.Height} rows."),
                L($"Taşın {Cell(state.Player)}.", $"Your piece {Cell(state.Player)}."),
                L($"Hedef {Cell(level.Goal)}.", $"Goal {Cell(level.Goal)}.")
            };

            if (state.Echo.HasValue) parts.Add(L($"Yankı {Cell(state.Echo.Value)}.", $"Echo {Cell(state.Echo.Value)}."));

            var neighbours = Directions.All
                .Select(d => (Direction: d, Cell: state.Player.Step(d)))
                .Select(n => $"{Strings.DirectionName(n.Direction)}: {Describe(level, state, n.Cell)}");
            parts.Add(L("Çevren: ", "Around you: ") + string.Join(", ", neighbours) + ".");

            if (level.Gates.Count > 0)
            {
                var open = Enumerable.Range(0, level.Gates.Count).Count(state.IsGateOpen);
                var closed = level.Gates.Count - open;
                parts.Add(L($"Kapılar: {open} açık, {closed} kapalı.", $"Gates: {open} open, {closed} closed."));
            }

            return string.Join(" ", parts);
        }

        /// <summary>Short announcement after a move, in the order the board shows it.</summary>
        public static string DescribeMove(MoveResult result)
        {
            if (!result.Accepted) return Strings.Reject(result.RejectReason);

            var parts = new List<string> { Strings.DirectionName(result.Direction) + "." };
            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case MoveEventType.EchoBlocked: parts.Add(Strings.EchoBlocked(e.EchoBlockReason) + "."); break;
                    case MoveEventType.GatesToggled: parts.Add(L("Kapılar değişti.", "Gates changed.")); break;
                    case MoveEventType.LevelWon: parts.Add(L("Bölüm tamamlandı.", "Level complete.")); break;
                }
            }

            return string.Join(" ", parts);
        }

        private static string Describe(LevelDefinition level, BoardState state, GridPos cell)
        {
            if (!level.InBounds(cell)) return L("kenar", "edge");
            if (level.IsWall(cell)) return L("duvar", "wall");
            if (state.Echo == cell) return L("yankı", "echo");

            var gate = level.GateIndexAt(cell);
            var what = gate >= 0
                ? (state.IsGateOpen(gate) ? L("açık kapı", "open gate") : L("kapalı kapı", "closed gate"))
                : L("boş", "empty");
            return cell == level.Goal ? what + L(" hedef", ", goal") : what;
        }

        private static string Cell(GridPos cell) =>
            L($"{cell.X + 1}. sütun {cell.Y + 1}. satırda", $"at column {cell.X + 1}, row {cell.Y + 1}");
    }
}
