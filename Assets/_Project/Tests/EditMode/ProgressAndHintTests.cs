using System;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    public sealed class ProgressAndHintTests
    {
        private static readonly DateTime Now = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        private static LevelDefinition Level(int par, int? optimal, int revision = 1) =>
            new LevelDefinition("lvl", "", revision, 1, 5, 1, null, null, new GridPos(0, 0), null, new GridPos(4, 0), par, optimal, null);

        [TestCase(4, false, 3)]
        [TestCase(4, true, 2)]
        [TestCase(5, false, 2)]
        [TestCase(6, false, 1)]
        public void Stars_FollowTheDocumentRules(int moves, bool hintUsed, int expected)
        {
            Assert.That(StarRating.Calculate(Level(par: 5, optimal: 4), moves, hintUsed), Is.EqualTo(expected));
        }

        [Test]
        public void NoProvenOptimal_CapsAtTwoStars()
        {
            Assert.That(StarRating.Calculate(Level(par: 5, optimal: null), 4, false), Is.EqualTo(2));
        }

        [Test]
        public void WorseAttempt_NeverLowersTheBests()
        {
            var progress = new ProgressService();
            var level = Level(5, 4);
            progress.RecordCompletion(level, 4, false, Now);
            var worse = progress.RecordCompletion(level, 9, true, Now);

            Assert.That(worse.Stars, Is.EqualTo(1));
            Assert.That(worse.BestStars, Is.EqualTo(3));
            Assert.That(worse.BestMoves, Is.EqualTo(4));
            Assert.That(worse.IsNewBestMoves, Is.False);
        }

        [Test]
        public void NewRevision_DoesNotCompareOldBestMoves_ButKeepsCompletion()
        {
            var progress = new ProgressService();
            progress.RecordCompletion(Level(5, 4, revision: 1), 4, false, Now);
            var outcome = progress.RecordCompletion(Level(9, 8, revision: 2), 8, false, Now);

            Assert.That(outcome.BestMoves, Is.EqualTo(8));
            Assert.That(outcome.IsFirstCompletion, Is.False);
        }

        [Test]
        public void Unlocking_FollowsCompletionOrder()
        {
            var a = Level(5, 4);
            var b = new LevelDefinition("b", "", 1, 1, 5, 1, null, null, new GridPos(0, 0), null, new GridPos(4, 0), 5, 4, null);
            var library = new LevelLibrary(new[] { a, b }, 1);
            var progress = new ProgressService();

            Assert.That(progress.IsUnlocked(library, 0), Is.True);
            Assert.That(progress.IsUnlocked(library, 1), Is.False);
            progress.RecordCompletion(a, 4, false, Now);
            Assert.That(progress.IsUnlocked(library, 1), Is.True);
        }

        [Test]
        public void Hint_Stage1IsAReminder_Stage2GivesTheNextShortestMove()
        {
            var level = TestLevels.GateTutorial();
            var state = level.CreateInitialState();
            var solved = BfsSolver.Solve(level, state, SolverBudget.Default);

            var reminder = HintService.Build(level, state, 1, 0, solved);
            var direction = HintService.Build(level, state, 2, 0, solved);

            Assert.That(reminder.Kind, Is.EqualTo(HintKind.MechanicReminder));
            Assert.That(reminder.Topic, Is.EqualTo(MechanicTopic.Gates));
            Assert.That(direction.Kind, Is.EqualTo(HintKind.SuggestedDirection));
            Assert.That(direction.SuggestedDirection, Is.EqualTo(Direction.Right));
            Assert.That(direction.MovesToGoal, Is.EqualTo(4));
        }

        [Test]
        public void Hint_FromCurrentState_IsAppliedToThatState()
        {
            var level = TestLevels.Parse("..P..", ".....", "G....");
            var session = new GameSession(level);
            session.TryMove(Direction.Left);
            var hint = new HintService(SolverBudget.Hint).RequestAsync(level, session.State, 2, session.StateVersion).Result;

            Assert.That(hint.Kind, Is.EqualTo(HintKind.SuggestedDirection));
            Assert.That(session.Preview(hint.SuggestedDirection.Value).Accepted, Is.True);
            Assert.That(hint.MovesToGoal, Is.EqualTo(3));
        }

        [Test]
        public void Hint_UnsolvableSuggestsUndo_UnknownIsUnavailable()
        {
            var dead = TestLevels.Parse("P.#G");
            var unsolvable = HintService.Build(dead, dead.CreateInitialState(), 2, 0, BfsSolver.Solve(dead, SolverBudget.Default));
            Assert.That(unsolvable.Kind, Is.EqualTo(HintKind.SuggestUndo));

            var open = TestLevels.Parse("P....", "....G");
            var unknown = HintService.Build(open, open.CreateInitialState(), 2, 0, BfsSolver.Solve(open, new SolverBudget(1, TimeSpan.FromSeconds(1))));
            Assert.That(unknown.Kind, Is.EqualTo(HintKind.Unavailable));
        }
    }
}
