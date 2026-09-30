using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneMoreMove.Core
{
    /// <summary>
    /// Compact text format for tests and seed content. One string per row, top row first.
    /// <code>
    ///   .  floor        #  wall        G  goal
    ///   P  player       E  echo
    ///   o  open gate    x  closed gate
    ///   O  open gate + goal            X  closed gate + goal
    /// </code>
    /// </summary>
    public static class AsciiLevelParser
    {
        public sealed class Options
        {
            public string Id = "ascii";
            public string Name = string.Empty;
            public int Revision = 1;
            public int? ParMoves;
            public int? OptimalMoves;
            public IEnumerable<Direction> KnownSolution;
            public bool IsTutorial;
        }

        public static LevelDefinition Parse(params string[] rows) => Parse(new Options(), rows);

        public static LevelDefinition Parse(Options options, IReadOnlyList<string> rows)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (rows == null || rows.Count == 0) throw new FormatException("Map has no rows.");

            var width = rows[0].Length;
            if (rows.Any(r => r.Length != width)) throw new FormatException("All map rows must have the same length.");

            var walls = new List<GridPos>();
            var gates = new List<GateDefinition>();
            GridPos? player = null, echo = null, goal = null;

            for (var y = 0; y < rows.Count; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pos = new GridPos(x, y);
                    switch (rows[y][x])
                    {
                        case '.': break;
                        case '#': walls.Add(pos); break;
                        case 'P': player = Single(player, pos, 'P'); break;
                        case 'E': echo = Single(echo, pos, 'E'); break;
                        case 'G': goal = Single(goal, pos, 'G'); break;
                        case 'o': gates.Add(new GateDefinition(pos, true)); break;
                        case 'x': gates.Add(new GateDefinition(pos, false)); break;
                        case 'O': gates.Add(new GateDefinition(pos, true)); goal = Single(goal, pos, 'O'); break;
                        case 'X': gates.Add(new GateDefinition(pos, false)); goal = Single(goal, pos, 'X'); break;
                        default: throw new FormatException($"Unknown map symbol '{rows[y][x]}' at {pos}.");
                    }
                }
            }

            if (!player.HasValue) throw new FormatException("Map has no player (P).");
            if (!goal.HasValue) throw new FormatException("Map has no goal (G, O or X).");

            var solution = options.KnownSolution?.ToArray() ?? Array.Empty<Direction>();
            var par = options.ParMoves ?? Math.Max(1, options.OptimalMoves ?? solution.Length);
            return new LevelDefinition(options.Id, options.Name, options.Revision, RulesEngine.Version, width, rows.Count,
                walls, gates, player.Value, echo, goal.Value, par, options.OptimalMoves, solution, options.IsTutorial);
        }

        /// <summary>Renders a level (and optionally a state) back to rows. Pieces hide the cell below them.</summary>
        public static string[] Render(LevelDefinition level, BoardState state = null)
        {
            state ??= level.CreateInitialState();
            var rows = new string[level.Height];
            var sb = new StringBuilder(level.Width);
            for (var y = 0; y < level.Height; y++)
            {
                sb.Clear();
                for (var x = 0; x < level.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    if (state.Player == pos) sb.Append('P');
                    else if (state.Echo == pos) sb.Append('E');
                    else if (level.IsWall(pos)) sb.Append('#');
                    else
                    {
                        var gate = level.GateIndexAt(pos);
                        var isGoal = level.Goal == pos;
                        if (gate >= 0)
                        {
                            var open = state.IsGateOpen(gate);
                            sb.Append(isGoal ? (open ? 'O' : 'X') : (open ? 'o' : 'x'));
                        }
                        else sb.Append(isGoal ? 'G' : '.');
                    }
                }

                rows[y] = sb.ToString();
            }

            return rows;
        }

        private static GridPos Single(GridPos? existing, GridPos pos, char symbol)
        {
            if (existing.HasValue) throw new FormatException($"Map contains more than one '{symbol}' ({existing.Value} and {pos}).");
            return pos;
        }
    }
}
