using System;
using System.Linq;
using NUnit.Framework;
using OneMoreMove.Core;

namespace OneMoreMove.Tests
{
    public sealed class LevelValidationTests
    {
        private static LevelDefinition Make(GridPos player, GridPos? echo, GridPos goal, GridPos[] walls = null, GateDefinition[] gates = null, bool tutorial = false) =>
            new LevelDefinition("t", "", 1, RulesEngine.Version, 5, 5, walls, gates, player, echo, goal, 3, null, new[] { Direction.Right }, tutorial);

        private static string[] Codes(LevelDefinition level) => LevelValidator.Validate(level).Errors.Select(e => e.Code).ToArray();

        [Test]
        public void ValidLevel_HasNoErrors()
        {
            Assert.That(LevelValidator.Validate(TestLevels.GateTutorial()).IsValid, Is.True);
        }

        [Test]
        public void CoordinatesOutsideTheBoard_AreErrors()
        {
            var level = Make(new GridPos(0, 0), null, new GridPos(9, 9), new[] { new GridPos(-1, 0) }, new[] { new GateDefinition(new GridPos(5, 0), true) });
            Assert.That(Codes(level), Is.SupersetOf(new[] { "goal.outOfBounds", "wall.outOfBounds", "gate.outOfBounds" }));
        }

        [Test]
        public void PiecesOnWalls_AndSharedStarts_AreErrors()
        {
            var level = Make(new GridPos(1, 1), new GridPos(1, 1), new GridPos(2, 2), new[] { new GridPos(2, 2) });
            Assert.That(Codes(level), Is.SupersetOf(new[] { "echo.onPlayer", "goal.onWall" }));
        }

        [Test]
        public void TwoGatesOnOneCell_IsAnError_GateOnGoalIsFine()
        {
            var level = Make(new GridPos(0, 0), null, new GridPos(2, 0), null,
                new[] { new GateDefinition(new GridPos(2, 0), true), new GateDefinition(new GridPos(2, 0), false) });
            var codes = Codes(level);
            Assert.That(codes, Does.Contain("gate.duplicate"));
            Assert.That(codes, Does.Not.Contain("goal.onGate"));
        }

        [Test]
        public void StartOnGoal_IsAnErrorOutsideTutorials()
        {
            Assert.That(Codes(Make(new GridPos(0, 0), null, new GridPos(0, 0))), Does.Contain("player.onGoal"));
            Assert.That(Codes(Make(new GridPos(0, 0), null, new GridPos(0, 0), tutorial: true)), Does.Not.Contain("player.onGoal"));
        }

        [Test]
        public void OversizedBoard_CannotBeConstructed()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new LevelDefinition("t", "", 1, 1, LevelDefinition.MaxDimension + 1, 1, null, null, default, null, default, 1, null, null));
        }

        [Test]
        public void AsciiParser_RoundTripsThroughRender()
        {
            var rows = new[] { ".x.o.", "#.E.#", "o.P.X" };
            Assert.That(AsciiLevelParser.Render(AsciiLevelParser.Parse(rows)), Is.EqualTo(rows));
        }

        [Test]
        public void AsciiParser_RejectsUnknownSymbolsAndDuplicates()
        {
            Assert.Throws<FormatException>(() => AsciiLevelParser.Parse("P?G"));
            Assert.Throws<FormatException>(() => AsciiLevelParser.Parse("PPG"));
            Assert.Throws<FormatException>(() => AsciiLevelParser.Parse("P..", "..G."));
        }

        [Test]
        public void Replayer_ReportsTheFirstRejectedStep()
        {
            var result = SolutionReplayer.Replay(TestLevels.Parse("P#G", "..."), new[] { Direction.Down, Direction.Up, Direction.Right });
            Assert.That(result.ReachedGoal, Is.False);
            Assert.That(result.FailedStep, Is.EqualTo(2));
            Assert.That(result.FailureReason, Is.EqualTo(RejectReason.Wall));
        }
    }
}
