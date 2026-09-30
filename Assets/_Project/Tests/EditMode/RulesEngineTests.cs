using System.Linq;
using NUnit.Framework;
using OneMoreMove.Core;

namespace OneMoreMove.Tests
{
    public sealed class RulesEngineTests
    {
        private static MoveResult Move(LevelDefinition level, BoardState state, Direction direction) =>
            new RulesEngine(level).TryMove(state, direction);

        [Test]
        public void OutOfBounds_IsRejected_AndNothingChanges()
        {
            var level = TestLevels.Parse("P.G");
            var start = level.CreateInitialState();

            var result = Move(level, start, Direction.Left);

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.OutOfBounds));
            Assert.That(result.NextState, Is.SameAs(start));
            Assert.That(result.Events, Is.Empty);
        }

        [Test]
        public void Wall_IsRejected()
        {
            var level = TestLevels.Parse("P#G");
            var result = Move(level, level.CreateInitialState(), Direction.Right);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.Wall));
        }

        [Test]
        public void ClosedGate_CannotBeEntered_AndGatesDoNotToggle()
        {
            var level = TestLevels.Parse("PxG");
            var start = level.CreateInitialState();

            var result = Move(level, start, Direction.Right);

            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.GateClosed));
            Assert.That(result.NextState.GateOpenBits, Is.EqualTo(start.GateOpenBits));
            Assert.That(result.NextState.MoveCount, Is.Zero);
        }

        [Test]
        public void OpenGate_CanBeEntered_ThenClosesOnThePlayer_WhoCanStillLeave()
        {
            var level = TestLevels.Parse("PoG");
            var rules = new RulesEngine(level);

            var onGate = rules.TryMove(level.CreateInitialState(), Direction.Right);
            Assert.That(onGate.Accepted, Is.True);
            Assert.That(onGate.NextState.Player, Is.EqualTo(new GridPos(1, 0)));
            Assert.That(onGate.NextState.IsGateOpen(0), Is.False, "Gate closes at the end of the move with the player on it.");

            var leave = rules.TryMove(onGate.NextState, Direction.Right);
            Assert.That(leave.Accepted, Is.True, "A piece on a closed gate may leave it.");
            Assert.That(leave.NextState.IsWon, Is.True);
        }

        [Test]
        public void ClosedGate_UnderThePlayer_DoesNotTrapIt()
        {
            // Player starts on a closed gate: leaving is always allowed; only entering is gated.
            var level = new LevelDefinition("start_on_gate", "", 1, 1, 3, 1, null, new[] { new GateDefinition(new GridPos(0, 0), false) },
                new GridPos(0, 0), null, new GridPos(2, 0), 2, null, null);
            var result = new RulesEngine(level).TryMove(level.CreateInitialState(), Direction.Right);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.IsGateOpen(0), Is.True);
        }

        [Test]
        public void EveryGate_TogglesExactlyOnce_PerAcceptedMove()
        {
            var level = TestLevels.Parse("P.o.x", "....G");
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            var before = state.GateOpenBits;

            var result = rules.TryMove(state, Direction.Down);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.GateOpenBits, Is.EqualTo(before ^ level.AllGatesMask));
            Assert.That(result.Events.Count(e => e.Type == MoveEventType.GatesToggled), Is.EqualTo(1));
        }

        [Test]
        public void Player_CannotEnterEchoTurnStartCell_NoSwap()
        {
            var level = TestLevels.Parse("PE.G");
            var result = Move(level, level.CreateInitialState(), Direction.Right);
            Assert.That(result.RejectReason, Is.EqualTo(RejectReason.BlockedByEcho));
        }

        [Test]
        public void Echo_CannotEnterPlayersNewCell_StaysAndMoveIsAccepted()
        {
            // Player moves Down into (1,1); echo at (1,2) tries Up into (1,1) and is blocked by the player.
            var level = TestLevels.Parse("#P#", "...", ".E.", "G..");
            var result = Move(level, level.CreateInitialState(), Direction.Down);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.Player, Is.EqualTo(new GridPos(1, 1)));
            Assert.That(result.NextState.Echo, Is.EqualTo(new GridPos(1, 2)));
            var blocked = result.Events.Single(e => e.Type == MoveEventType.EchoBlocked);
            Assert.That(blocked.EchoBlockReason, Is.EqualTo(EchoBlockReason.BlockedByPlayer));
        }

        [Test]
        public void Echo_BlockedByClosedGate_Stays_ButCanLeaveAClosedGateItStandsOn()
        {
            var blockedLevel = TestLevels.Parse("P.G", "xE.");
            var blocked = Move(blockedLevel, blockedLevel.CreateInitialState(), Direction.Right);
            Assert.That(blocked.Accepted, Is.True);
            Assert.That(blocked.NextState.Echo, Is.EqualTo(new GridPos(1, 1)));
            Assert.That(blocked.Events.Single(e => e.Type == MoveEventType.EchoBlocked).EchoBlockReason, Is.EqualTo(EchoBlockReason.GateClosed));

            // Echo starting on a closed gate cell may leave it.
            var onGate = new LevelDefinition("echo_on_gate", "", 1, 1, 3, 2, null, new[] { new GateDefinition(new GridPos(2, 1), false) },
                new GridPos(0, 0), new GridPos(2, 1), new GridPos(2, 0), 1, null, null);
            var leave = Move(onGate, onGate.CreateInitialState(), Direction.Right);
            Assert.That(leave.NextState.Echo, Is.EqualTo(new GridPos(1, 1)));
        }

        [Test]
        public void EchoReachingGoal_DoesNotWin()
        {
            var level = TestLevels.Parse("..P", "EG.");
            var result = Move(level, level.CreateInitialState(), Direction.Left);
            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.Echo, Is.EqualTo(new GridPos(1, 1)));
            Assert.That(result.NextState.IsWon, Is.False);
        }

        [Test]
        public void WonState_RejectsFurtherMoves()
        {
            var level = TestLevels.Parse("PG.");
            var rules = new RulesEngine(level);
            var won = rules.TryMove(level.CreateInitialState(), Direction.Right).NextState;
            Assert.That(won.IsWon, Is.True);
            Assert.That(rules.TryMove(won, Direction.Right).RejectReason, Is.EqualTo(RejectReason.LevelAlreadyWon));
        }

        [Test]
        public void InvalidCommand_IsRejected()
        {
            var level = TestLevels.Parse("PG");
            Assert.That(Move(level, level.CreateInitialState(), (Direction)42).RejectReason, Is.EqualTo(RejectReason.InvalidCommand));
        }

        [Test]
        public void Wait_TogglesGatesAndCountsTheMove_WhilePiecesStay()
        {
            var level = TestLevels.Parse("PxG", "E..");
            var start = level.CreateInitialState();

            var result = Move(level, start, Direction.Wait);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Events.Select(e => e.Type), Is.EqualTo(new[] { MoveEventType.PlayerWaited, MoveEventType.GatesToggled }));
            Assert.That(result.NextState.Player, Is.EqualTo(start.Player));
            Assert.That(result.NextState.Echo, Is.EqualTo(start.Echo), "The echo mirrors a zero step: it stays and is not blocked.");
            Assert.That(result.NextState.IsGateOpen(0), Is.True);
            Assert.That(result.NextState.MoveCount, Is.EqualTo(1));
        }

        [Test]
        public void Wait_OnAGateThatClosedUnderThePlayer_IsAccepted()
        {
            var level = TestLevels.Parse("PoG");
            var rules = new RulesEngine(level);
            var onGate = rules.TryMove(level.CreateInitialState(), Direction.Right).NextState;
            Assert.That(onGate.IsGateOpen(0), Is.False);

            var result = rules.TryMove(onGate, Direction.Wait);

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.NextState.IsGateOpen(0), Is.True);
        }

        [Test]
        public void Wait_WithoutGates_OnlyCountsTheMove()
        {
            var level = TestLevels.Parse("P.G");
            var result = Move(level, level.CreateInitialState(), Direction.Wait);
            Assert.That(result.Events.Select(e => e.Type), Is.EqualTo(new[] { MoveEventType.PlayerWaited }));
            Assert.That(result.NextState.ToKey(), Is.EqualTo(level.CreateInitialState().ToKey()));
            Assert.That(result.NextState.MoveCount, Is.EqualTo(1));
        }

        [Test]
        public void Wait_AfterWin_IsRejected()
        {
            var level = TestLevels.Parse("PG.");
            var rules = new RulesEngine(level);
            var won = rules.TryMove(level.CreateInitialState(), Direction.Right).NextState;
            Assert.That(rules.TryMove(won, Direction.Wait).RejectReason, Is.EqualTo(RejectReason.LevelAlreadyWon));
        }

        [Test]
        public void Wait_IsTheZeroStep()
        {
            Assert.That(Direction.Wait.ToOffset(), Is.EqualTo(new GridPos(0, 0)));
            Assert.That(Direction.Wait.Opposite(), Is.EqualTo(Direction.Wait));
            Assert.That(Direction.Left.Opposite(), Is.EqualTo(Direction.Right));
            Assert.That(Directions.TryParse("wait", out var parsed) && parsed == Direction.Wait, Is.True);
            Assert.That(Directions.Commands[Directions.Commands.Count - 1], Is.EqualTo(Direction.Wait));
        }

        [Test]
        public void EventsAreOrdered_PlayerEchoGatesWin()
        {
            var level = TestLevels.Parse("PO", "E.");
            var result = Move(level, level.CreateInitialState(), Direction.Right);
            Assert.That(result.Events.Select(e => e.Type), Is.EqualTo(new[]
            {
                MoveEventType.PlayerMoved, MoveEventType.EchoBlocked, MoveEventType.GatesToggled, MoveEventType.LevelWon
            }));
        }
    }
}
