using System.Linq;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    public sealed class GameSessionTests
    {
        [Test]
        public void RejectedMove_DoesNotTouchStateCounterOrHistory()
        {
            var session = new GameSession(TestLevels.Parse("P#G", "..."));
            var before = session.State;

            var result = session.TryMove(Direction.Right);

            Assert.That(result.Accepted, Is.False);
            Assert.That(session.State, Is.SameAs(before));
            Assert.That(session.History.Count, Is.Zero);
            Assert.That(session.StateVersion, Is.Zero);
        }

        [Test]
        public void Undo_RestoresEveryFieldOfThePreviousSnapshot()
        {
            var session = new GameSession(TestLevels.Parse(".P.o.", "..E.G"));
            var start = session.State;
            session.TryMove(Direction.Right);
            var afterFirst = session.State;
            session.TryMove(Direction.Down);

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State, Is.EqualTo(afterFirst));
            Assert.That(session.Undo(), Is.True);
            Assert.That(session.State, Is.EqualTo(start));
            Assert.That(session.Undo(), Is.False);
        }

        [Test]
        public void WonLevel_CannotBeUndone_ButCanBeRestarted()
        {
            var session = new GameSession(TestLevels.GateTutorial());
            for (var i = 0; i < 4; i++) session.TryMove(Direction.Right);

            Assert.That(session.IsWon, Is.True);
            Assert.That(session.Undo(), Is.False);

            session.Restart();
            Assert.That(session.State, Is.EqualTo(session.Level.CreateInitialState()));
            Assert.That(session.History.Count, Is.Zero);
            Assert.That(session.Attempts, Is.EqualTo(2));
        }

        [Test]
        public void HintUsed_SurvivesUndo_ResetsOnRestart()
        {
            var session = new GameSession(TestLevels.GateTutorial());
            session.TryMove(Direction.Right);
            Assert.That(session.TakeHint(), Is.EqualTo(1));
            Assert.That(session.TakeHint(), Is.EqualTo(2), "Second request at the same state escalates.");
            session.Undo();
            Assert.That(session.HintUsed, Is.True);
            Assert.That(session.TakeHint(), Is.EqualTo(1), "A new state starts at the reminder again.");

            session.Restart();
            Assert.That(session.HintUsed, Is.False);
        }

        [Test]
        public void Preview_DoesNotChangeTheSession()
        {
            var session = new GameSession(TestLevels.EchoExample());
            var before = session.State;
            var preview = session.Preview(Direction.Right);

            Assert.That(preview.Accepted, Is.True);
            Assert.That(session.State, Is.SameAs(before));
            Assert.That(session.StateVersion, Is.Zero);
        }

        [Test]
        public void UndoHistory_IsBounded_AndKeepsTheNewestEntries()
        {
            var session = new GameSession(TestLevels.Parse("P....", ".....", "....G"), undoCapacity: 3);
            session.TryMove(Direction.Right);
            session.TryMove(Direction.Right);
            session.TryMove(Direction.Right);
            session.TryMove(Direction.Right);

            Assert.That(session.History.Count, Is.EqualTo(3));
            Assert.That(session.History.ToList().Select(s => s.MoveCount), Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void Snapshot_RoundTrip_RestoresTheSession()
        {
            var level = TestLevels.EchoExample();
            var session = new GameSession(level);
            session.TryMove(Direction.Right);
            session.TakeHint();
            session.AddActiveTime(12.5);

            var restored = GameSession.FromSnapshot(level, session.ToSnapshot());

            Assert.That(restored.State, Is.EqualTo(session.State));
            Assert.That(restored.History.ToList(), Is.EqualTo(session.History.ToList()));
            Assert.That(restored.HintUsed, Is.True);
            Assert.That(restored.ActiveSeconds, Is.EqualTo(12.5));
        }

        [Test]
        public void Snapshot_ForAnotherRevision_IsRefused()
        {
            var level = TestLevels.GateTutorial();
            var snapshot = new GameSession(level).ToSnapshot();
            var updated = new LevelDefinition(level.Id, level.Name, level.Revision + 1, level.RulesVersion, level.Width, level.Height,
                level.Walls, level.Gates, level.PlayerStart, level.EchoStart, level.Goal, level.ParMoves, level.OptimalMoves, level.KnownSolution);

            Assert.Throws<System.ArgumentException>(() => GameSession.FromSnapshot(updated, snapshot));
        }
    }
}
