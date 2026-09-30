using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;

namespace OneMoreMove.Tests
{
    public sealed class LevelAnalyzerTests
    {
        [Test]
        public void GateClosedFromTheApproach_RequiresAWait_AndHasNoDeadEnds()
        {
            var a = LevelAnalyzer.Analyze(TestLevels.Parse("PxG"));

            Assert.That(a.Complete, Is.True);
            Assert.That(a.OptimalMoves, Is.EqualTo(3));
            Assert.That(a.WaitsInSolution, Is.EqualTo(1));
            Assert.That(a.OptimalWithoutWait, Is.Null);
            Assert.That(a.RequiresWait, Is.True);
            Assert.That(a.DeadEndRatio, Is.EqualTo(0.0), "Without an echo a wait can always fix the gate phase.");
        }

        [Test]
        public void GateAndEcho_ReportsEveryVariant()
        {
            var level = TestLevels.Parse("PEG.#", ".#xo#", ".....", "..x..", ".....");
            var a = LevelAnalyzer.Analyze(level);

            Assert.That(a.OptimalMoves, Is.EqualTo(7));
            Assert.That(a.OptimalSolutionCount, Is.EqualTo(5));
            Assert.That(a.OptimalWithoutWait, Is.EqualTo(10));
            Assert.That(a.OptimalWithoutGates, Is.EqualTo(6));
            Assert.That(a.OptimalWithoutEcho, Is.EqualTo(2));
            Assert.That(a.EchoBlocksInSolution, Is.EqualTo(3));
            Assert.That(a.ReachableStates, Is.EqualTo(791));
            Assert.That(SolutionReplayer.Replay(level, a.Solution).ReachedGoal, Is.True);
        }

        [Test]
        public void EchoTraps_ShowUpAsDeadEnds()
        {
            var a = LevelAnalyzer.Analyze(TestLevels.Parse(".#.xPo", ".Eo.#.", ".G##..", "#...#.", "..##.."));
            Assert.That(a.OptimalMoves, Is.EqualTo(11));
            Assert.That(a.DeadEndRatio, Is.GreaterThan(0.5));
        }

        [Test]
        public void UnreachableGoal_IsUnsolvable()
        {
            var a = LevelAnalyzer.Analyze(TestLevels.Parse("P.#G"));
            Assert.That(a.IsSolvable, Is.False);
            Assert.That(a.Complete, Is.True);
            Assert.That(a.DeadEndRatio, Is.EqualTo(1.0));
        }
    }
}
