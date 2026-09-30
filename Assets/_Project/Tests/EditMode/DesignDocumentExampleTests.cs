using System.Linq;
using NUnit.Framework;
using OneMoreMove.Core;

namespace OneMoreMove.Tests
{
    /// <summary>The worked examples on pages 4 and 5 of the design document, step by step.</summary>
    public sealed class DesignDocumentExampleTests
    {
        [Test]
        public void GateTutorial_FourRights_MatchTheTable()
        {
            var level = TestLevels.GateTutorial();
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            Assert.That(state.IsGateOpen(0), Is.False, "Start: gate closed.");

            var expected = new[]
            {
                (pos: new GridPos(1, 0), open: true),
                (pos: new GridPos(2, 0), open: false),
                (pos: new GridPos(3, 0), open: true),
                (pos: new GridPos(4, 0), open: false)
            };

            for (var i = 0; i < expected.Length; i++)
            {
                var result = rules.TryMove(state, Direction.Right);
                Assert.That(result.Accepted, Is.True, $"Step {i + 1} accepted");
                state = result.NextState;
                Assert.That(state.Player, Is.EqualTo(expected[i].pos), $"Step {i + 1} position");
                Assert.That(state.IsGateOpen(0), Is.EqualTo(expected[i].open), $"Step {i + 1} gate");
                Assert.That(state.IsWon, Is.EqualTo(i == 3), $"Step {i + 1} win");
            }

            Assert.That(state.MoveCount, Is.EqualTo(4));
        }

        [Test]
        public void EchoExample_ThreeRights_MirrorTheEcho()
        {
            var level = TestLevels.EchoExample();
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            var expected = new[]
            {
                (player: new GridPos(2, 0), echo: new GridPos(2, 1)),
                (player: new GridPos(3, 0), echo: new GridPos(1, 1)),
                (player: new GridPos(4, 0), echo: new GridPos(0, 1))
            };

            foreach (var step in expected)
            {
                state = rules.TryMove(state, Direction.Right).NextState;
                Assert.That(state.Player, Is.EqualTo(step.player));
                Assert.That(state.Echo, Is.EqualTo(step.echo));
            }

            Assert.That(state.IsWon, Is.True);
            Assert.That(state.MoveCount, Is.EqualTo(3));
        }

        [Test]
        public void EchoExampleWithWall_EchoStays_PlayerMoveAccepted()
        {
            var level = TestLevels.EchoExampleWithWall();
            var result = new RulesEngine(level).TryMove(level.CreateInitialState(), Direction.Right);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.Player, Is.EqualTo(new GridPos(2, 0)));
            Assert.That(result.NextState.Echo, Is.EqualTo(new GridPos(3, 1)));
            Assert.That(result.Events.Single(e => e.Type == MoveEventType.EchoBlocked).EchoBlockReason, Is.EqualTo(EchoBlockReason.Wall));
        }
    }
}
