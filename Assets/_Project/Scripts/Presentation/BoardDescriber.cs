using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;

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
                $"Tahta {level.Width} sütun, {level.Height} satır.",
                $"Taşın {Cell(state.Player)}.",
                $"Hedef {Cell(level.Goal)}."
            };

            if (state.Echo.HasValue) parts.Add($"Yankı {Cell(state.Echo.Value)}.");

            var neighbours = Directions.All
                .Select(d => (Direction: d, Cell: state.Player.Step(d)))
                .Select(n => $"{Strings.DirectionName(n.Direction)}: {Describe(level, state, n.Cell)}");
            parts.Add("Çevren: " + string.Join(", ", neighbours) + ".");

            if (level.Gates.Count > 0)
            {
                var open = Enumerable.Range(0, level.Gates.Count).Count(state.IsGateOpen);
                parts.Add($"Kapılar: {open} açık, {level.Gates.Count - open} kapalı.");
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
                    case MoveEventType.GatesToggled: parts.Add("Kapılar değişti."); break;
                    case MoveEventType.LevelWon: parts.Add("Bölüm tamamlandı."); break;
                }
            }

            return string.Join(" ", parts);
        }

        private static string Describe(LevelDefinition level, BoardState state, GridPos cell)
        {
            if (!level.InBounds(cell)) return "kenar";
            if (level.IsWall(cell)) return "duvar";
            if (state.Echo == cell) return "yankı";

            var gate = level.GateIndexAt(cell);
            var what = gate >= 0 ? (state.IsGateOpen(gate) ? "açık kapı" : "kapalı kapı") : "boş";
            return cell == level.Goal ? what + " hedef" : what;
        }

        private static string Cell(GridPos cell) => $"{cell.X + 1}. sütun {cell.Y + 1}. satırda";
    }
}
