using System.Collections;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Presentation;
using OneMoreMove.Session;
using OneMoreMove.Core.Solving;
using UnityEngine;
using UnityEngine.TestTools;

namespace OneMoreMove.Tests
{
    public sealed class GameplayPlayModeTests
    {
        private sealed class MemoryStore : ISaveStore
        {
            public int Flushes;
            public SaveLoadResult Load() => new SaveLoadResult(SaveLoadStatus.NoSave, null);
            public void Save(SaveData data) { }
            public void Flush() => Flushes++;
        }

        private GameObject _root;
        private BoardView _board;
        private GameCoordinator _game;
        private GameplayController _gameplay;
        private MemoryStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("PlayModeTestRoot");
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(_root.transform);
            _board = new GameObject("Board").AddComponent<BoardView>();
            _board.transform.SetParent(_root.transform);
            _board.SetCamera(camera);

            var tutorial = AsciiLevelParser.Parse(new AsciiLevelParser.Options { Id = "tutorial", ParMoves = 5, OptimalMoves = 4,
                KnownSolution = new[] { Direction.Right, Direction.Right, Direction.Right, Direction.Right } }, new[] { "P.x.G" });
            _store = new MemoryStore();
            _game = new GameCoordinator(new LevelLibrary(new[] { tutorial }, 1), _store);
            _gameplay = new GameplayController(_game, _board, new HintService(SolverBudget.Hint));
            _gameplay.Open(0);
        }

        [TearDown]
        public void TearDown()
        {
            _gameplay.Dispose();
            Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator InputDuringAnimation_IsRejected_SoOnePressIsOneMove()
        {
            Assert.That(_gameplay.TryMove(Direction.Right), Is.True);
            Assert.That(_board.IsAnimating, Is.True);
            Assert.That(_gameplay.TryMove(Direction.Right), Is.False, "Second input during the transition must not apply.");
            Assert.That(_game.Current.State.MoveCount, Is.EqualTo(1));

            yield return WaitUntilIdle();

            Assert.That(_gameplay.TryMove(Direction.Right), Is.True);
            Assert.That(_game.Current.State.MoveCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Suspend_SnapsTheBoardToTheAppliedState_AndFlushesTheSave()
        {
            _gameplay.TryMove(Direction.Right);
            _gameplay.Suspend();

            Assert.That(_board.IsAnimating, Is.False);
            Assert.That(_board.PlayerWorldPosition, Is.EqualTo(_board.CellToWorld(_game.Current.State.Player)));
            Assert.That(_store.Flushes, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator Win_IsReportedOnce_AfterTheWinningMoveFinishesAnimating()
        {
            var completions = 0;
            _gameplay.LevelCompleted += _ => completions++;

            for (var i = 0; i < 4; i++)
            {
                Assert.That(_gameplay.TryMove(Direction.Right), Is.True, $"Move {i + 1}");
                if (i < 3) yield return WaitUntilIdle();
            }

            Assert.That(completions, Is.Zero, "The win panel waits for the transition.");
            yield return WaitUntilIdle();
            Assert.That(completions, Is.EqualTo(1));

            Assert.That(_gameplay.TryMove(Direction.Right), Is.False);
            yield return null;
            Assert.That(completions, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RejectedMoveFeedback_DoesNotBlockTheNextInput()
        {
            Assert.That(_gameplay.TryMove(Direction.Left), Is.False, "Out of bounds.");
            Assert.That(_board.IsAnimating, Is.False, "The reject flash is decorative and must not block input.");
            Assert.That(_gameplay.TryMove(Direction.Right), Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Wait_KeepsThePlayerInPlace_AndFlipsTheGates()
        {
            var gateBefore = _game.Current.State.IsGateOpen(0);

            Assert.That(_gameplay.TryMove(Direction.Wait), Is.True);
            yield return WaitUntilIdle();

            Assert.That(_game.Current.State.MoveCount, Is.EqualTo(1));
            Assert.That(_game.Current.State.IsGateOpen(0), Is.Not.EqualTo(gateBefore));
            Assert.That(_board.PlayerWorldPosition, Is.EqualTo(_board.CellToWorld(new GridPos(0, 0))));
        }

        [UnityTest]
        public IEnumerator Undo_SnapsBackWithoutAnimation()
        {
            _gameplay.TryMove(Direction.Right);
            yield return WaitUntilIdle();

            Assert.That(_gameplay.TryUndo(), Is.True);
            Assert.That(_board.IsAnimating, Is.False);
            Assert.That(_board.PlayerWorldPosition, Is.EqualTo(_board.CellToWorld(new GridPos(0, 0))));
        }

        private IEnumerator WaitUntilIdle()
        {
            var timeout = Time.realtimeSinceStartup + 2f;
            while (_board.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
            Assert.That(_board.IsAnimating, Is.False, "Animation did not finish in time.");
        }
    }
}
