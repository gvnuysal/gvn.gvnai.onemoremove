using OneMoreMove.Core;

namespace OneMoreMove.Tests
{
    /// <summary>The hand-verifiable examples from the design document, plus small fixtures.</summary>
    internal static class TestLevels
    {
        /// <summary>5×1: player (0,0), closed gate (2,0), goal (4,0). Solution: four Rights.</summary>
        public static LevelDefinition GateTutorial() => AsciiLevelParser.Parse(
            new AsciiLevelParser.Options { Id = "tutorial_gate_01", ParMoves = 5, IsTutorial = true },
            new[] { "P.x.G" });

        /// <summary>5×2 empty: player (1,0), echo (3,1), goal (4,0). Three Rights.</summary>
        public static LevelDefinition EchoExample() => AsciiLevelParser.Parse(
            new AsciiLevelParser.Options { Id = "echo_example", ParMoves = 3 },
            new[] { ".P..G", "...E." });

        /// <summary>Same as <see cref="EchoExample"/> with a wall at (2,1).</summary>
        public static LevelDefinition EchoExampleWithWall() => AsciiLevelParser.Parse(
            new AsciiLevelParser.Options { Id = "echo_wall", ParMoves = 3 },
            new[] { ".P..G", "..#E." });

        public static LevelDefinition Parse(params string[] rows) => AsciiLevelParser.Parse(rows);
    }
}
