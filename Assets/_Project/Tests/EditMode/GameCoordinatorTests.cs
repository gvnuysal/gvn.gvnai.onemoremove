using System;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    public sealed class GameCoordinatorTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public SaveData Last;
            public int Saves;
            public SaveLoadResult Load() => Last == null ? new SaveLoadResult(SaveLoadStatus.NoSave, null) : new SaveLoadResult(SaveLoadStatus.Loaded, Last);
            public void Save(SaveData data) { Last = data; Saves++; }
            public void Flush() { }
        }

        private static LevelLibrary Library(int tutorialRevision = 1)
        {
            var tutorial = TestLevels.GateTutorial();
            tutorial = new LevelDefinition(tutorial.Id, "Kapı", tutorialRevision, 1, tutorial.Width, tutorial.Height, tutorial.Walls, tutorial.Gates,
                tutorial.PlayerStart, null, tutorial.Goal, 5, 4, new[] { Direction.Right, Direction.Right, Direction.Right, Direction.Right });
            return new LevelLibrary(new[] { tutorial, TestLevels.EchoExample() }, 1);
        }

        [Test]
        public void Win_IsRecordedOnce_AndUnlocksTheNextLevel()
        {
            var store = new MemoryStore();
            var game = new GameCoordinator(Library(), store);
            game.StartLevel(0);

            MoveOutcome last = null;
            for (var i = 0; i < 4; i++) last = game.Move(Direction.Right);
            Assert.That(last.Completion, Is.Not.Null);
            Assert.That(last.Completion.Stars, Is.EqualTo(3));

            var afterWin = game.Move(Direction.Right);
            Assert.That(afterWin.Result.Accepted, Is.False);
            Assert.That(afterWin.Completion, Is.Null, "A win is processed only once.");
            Assert.That(game.Progress.IsUnlocked(game.Library, 1), Is.True);
            Assert.That(store.Last.ActiveSession, Is.Null, "A finished level is not a resumable session.");
        }

        [Test]
        public void HalfFinishedSession_ResumesAfterRestart()
        {
            var store = new MemoryStore();
            var game = new GameCoordinator(Library(), store);
            game.StartLevel(0);
            game.Move(Direction.Right);
            game.Move(Direction.Right);
            game.Flush();

            var reopened = new GameCoordinator(Library(), store);
            reopened.Load();

            Assert.That(reopened.HasResumableSession, Is.True);
            Assert.That(reopened.FindContinueIndex(), Is.EqualTo(0));
            Assert.That(reopened.Current.State.Player, Is.EqualTo(new GridPos(2, 0)));
            Assert.That(reopened.Current.History.Count, Is.EqualTo(2));
        }

        [Test]
        public void UpdatedLevelRevision_DiscardsTheSession_AndSaysWhy()
        {
            var store = new MemoryStore();
            var game = new GameCoordinator(Library(), store);
            game.StartLevel(0);
            game.Move(Direction.Right);

            var reopened = new GameCoordinator(Library(tutorialRevision: 2), store);
            reopened.Load();

            Assert.That(reopened.HasResumableSession, Is.False);
            Assert.That(reopened.ResumeDiscardReason, Is.EqualTo(ResumeDiscardReason.LevelUpdated));
        }

        [Test]
        public void EveryAcceptedChange_RequestsASave_RejectedMovesDoNot()
        {
            var store = new MemoryStore();
            var game = new GameCoordinator(Library(), store);
            game.StartLevel(0);
            var saves = store.Saves;

            game.Move(Direction.Left);
            Assert.That(store.Saves, Is.EqualTo(saves));
            game.Move(Direction.Right);
            game.Undo();
            Assert.That(store.Saves, Is.EqualTo(saves + 2));
        }

        [Test]
        public void LockedLevel_CannotBeStarted()
        {
            var game = new GameCoordinator(Library(), new MemoryStore());
            Assert.Throws<InvalidOperationException>(() => game.StartLevel(1));
        }
    }
}
