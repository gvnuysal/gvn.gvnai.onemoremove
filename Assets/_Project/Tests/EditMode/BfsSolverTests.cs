using System;
using System.Threading;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;

namespace OneMoreMove.Tests
{
    public sealed class BfsSolverTests
    {
        [Test]
        public void GateTutorial_IsSolvedInFourRights()
        {
            var result = BfsSolver.Solve(TestLevels.GateTutorial(), SolverBudget.Default);

            Assert.That(result.Status, Is.EqualTo(SolverStatus.Solved));
            Assert.That(result.Path, Is.EqualTo(new[] { Direction.Right, Direction.Right, Direction.Right, Direction.Right }));
        }

        [Test]
        public void GateClosedFromTheStartSide_NeedsAWait()
        {
            var result = BfsSolver.Solve(TestLevels.Parse("PxG"), SolverBudget.Default);

            Assert.That(result.Status, Is.EqualTo(SolverStatus.Solved));
            Assert.That(result.Path, Is.EqualTo(new[] { Direction.Wait, Direction.Right, Direction.Right }));
        }

        [Test]
        public void EchoExample_IsSolvedInThreeMoves()
        {
            var result = BfsSolver.Solve(TestLevels.EchoExample(), SolverBudget.Default);
            Assert.That(result.Path.Count, Is.EqualTo(3));
        }

        [Test]
        public void SolverPath_ReplaysThroughTheRealRules()
        {
            var level = TestLevels.Parse(".x.o.", "#.E.#", "o.P.x", ".#.#.", "x...G");
            var result = BfsSolver.Solve(level, SolverBudget.Default);

            Assert.That(result.Status, Is.EqualTo(SolverStatus.Solved));
            Assert.That(SolutionReplayer.Replay(level, result.Path).ReachedGoal, Is.True);
        }

        [Test]
        public void WalledOffGoal_IsProvenUnsolvable()
        {
            var result = BfsSolver.Solve(TestLevels.Parse("P.#G"), SolverBudget.Default);
            Assert.That(result.Status, Is.EqualTo(SolverStatus.Unsolvable));
        }

        [Test]
        public void ExhaustedBudget_IsUnknown_NotUnsolvable()
        {
            var result = BfsSolver.Solve(TestLevels.Parse("P....", ".....", "....G"), new SolverBudget(1, TimeSpan.FromSeconds(10)));
            Assert.That(result.Status, Is.EqualTo(SolverStatus.Unknown));
            Assert.That(result.StopReason, Is.EqualTo(SolverStopReason.StateBudget));
        }

        [Test]
        public void Cancellation_IsUnknown()
        {
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();
                var result = BfsSolver.Solve(TestLevels.GateTutorial(), SolverBudget.Default, cts.Token);
                Assert.That(result.Status, Is.EqualTo(SolverStatus.Unknown));
                Assert.That(result.StopReason, Is.EqualTo(SolverStopReason.Cancelled));
            }
        }

        [Test]
        public void SolvingFromAMidGameState_UsesThatState()
        {
            var level = TestLevels.GateTutorial();
            var state = new RulesEngine(level).TryMove(level.CreateInitialState(), Direction.Right).NextState;
            var result = BfsSolver.Solve(level, state, SolverBudget.Default);
            Assert.That(result.Path.Count, Is.EqualTo(3));
        }
    }
}
