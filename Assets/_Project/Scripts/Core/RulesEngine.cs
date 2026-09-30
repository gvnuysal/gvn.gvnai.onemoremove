using System;
using System.Collections.Generic;

namespace OneMoreMove.Core
{
    /// <summary>
    /// The single source of truth for game rules. Gameplay, preview, hints, the solver and the level editor
    /// all call <see cref="TryMove"/>; nothing else may reimplement movement, gates or echo behaviour.
    /// </summary>
    public sealed class RulesEngine
    {
        /// <summary>
        /// Bump when rule behaviour changes; saved sessions and verified solutions depend on it.
        /// v2: <see cref="Direction.Wait"/> — without it every gate is permanently passable or not (grid parity).
        /// </summary>
        public const int Version = 2;

        public RulesEngine(LevelDefinition level)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
        }

        public LevelDefinition Level { get; }

        /// <summary>
        /// Resolves one move as an atomic transaction. <paramref name="state"/> is never modified; on rejection the
        /// result carries the same state instance and no events.
        /// </summary>
        public MoveResult TryMove(BoardState state, MoveCommand command)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            // 1. Is the input acceptable at all?
            var direction = command.Direction;
            if (!direction.IsValid()) return MoveResult.Reject(direction, state, RejectReason.InvalidCommand);
            if (state.IsWon) return MoveResult.Reject(direction, state, RejectReason.LevelAlreadyWon);
            if (direction.IsWait()) return Wait(state);

            // 3. Validate the player's target against the gates and pieces at the start of the turn.
            var offset = direction.ToOffset();
            var playerFrom = state.Player;
            var playerTo = playerFrom + offset;
            var reject = CheckPlayerTarget(state, playerTo);
            if (reject != RejectReason.None) return MoveResult.Reject(direction, state, reject);

            // 2 + 4. Work on local copies; the input state stays untouched.
            var events = new List<MoveEvent>(4) { MoveEvent.PlayerMoved(playerFrom, playerTo) };

            // 5. The echo tries the opposite direction, still against turn-start gates.
            var echo = state.Echo;
            if (echo.HasValue)
            {
                var echoFrom = echo.Value;
                var echoTo = echoFrom - offset;
                var block = CheckEchoTarget(state, echoTo, playerTo);
                if (block == EchoBlockReason.None)
                {
                    echo = echoTo;
                    events.Add(MoveEvent.EchoMoved(echoFrom, echoTo));
                }
                else
                {
                    events.Add(MoveEvent.EchoBlocked(echoFrom, block));
                }
            }

            // 6. Every gate flips exactly once per accepted move.
            var gatesBefore = state.GateOpenBits;
            var gatesAfter = gatesBefore ^ Level.AllGatesMask;
            if (Level.Gates.Count > 0) events.Add(MoveEvent.GatesToggled(gatesBefore, gatesAfter));

            // 9. Count the move and check the win after all effects.
            var isWon = playerTo == Level.Goal;
            var next = new BoardState(playerTo, echo, gatesAfter, state.MoveCount + 1, isWon);

            // 7. Invariants: a violation here is a rules bug, never a player error.
            EnsureInvariants(next);

            if (isWon) events.Add(MoveEvent.LevelWon(playerTo));
            return MoveResult.Accept(direction, state, next, events);
        }

        public MoveResult TryMove(BoardState state, Direction direction) => TryMove(state, new MoveCommand(direction));

        /// <summary>
        /// Nobody moves, so there is nothing to validate: a piece already standing on a closed gate may stay there.
        /// The turn still passes — gates flip and the move counts.
        /// </summary>
        private MoveResult Wait(BoardState state)
        {
            var events = new List<MoveEvent>(2) { MoveEvent.PlayerWaited(state.Player) };

            var gatesBefore = state.GateOpenBits;
            var gatesAfter = gatesBefore ^ Level.AllGatesMask;
            if (Level.Gates.Count > 0) events.Add(MoveEvent.GatesToggled(gatesBefore, gatesAfter));

            var isWon = state.Player == Level.Goal;
            var next = new BoardState(state.Player, state.Echo, gatesAfter, state.MoveCount + 1, isWon);
            EnsureInvariants(next);

            if (isWon) events.Add(MoveEvent.LevelWon(state.Player));
            return MoveResult.Accept(Direction.Wait, state, next, events);
        }

        /// <summary>The level is won only when the player (not the echo) stands on the goal.</summary>
        public bool CheckWin(BoardState state) => state != null && state.Player == Level.Goal;

        /// <summary>True when a piece may enter <paramref name="cell"/> with the given gate bits (ignores pieces).</summary>
        public bool IsEnterable(GridPos cell, ulong gateOpenBits)
        {
            if (!Level.InBounds(cell) || Level.IsWall(cell)) return false;
            var gate = Level.GateIndexAt(cell);
            return gate < 0 || (gateOpenBits & (1UL << gate)) != 0;
        }

        private RejectReason CheckPlayerTarget(BoardState state, GridPos target)
        {
            if (!Level.InBounds(target)) return RejectReason.OutOfBounds;
            if (Level.IsWall(target)) return RejectReason.Wall;
            if (IsClosedGate(target, state.GateOpenBits)) return RejectReason.GateClosed;

            // No swapping in the same turn: the echo's turn-start cell is always blocked.
            if (state.Echo.HasValue && state.Echo.Value == target) return RejectReason.BlockedByEcho;
            return RejectReason.None;
        }

        private EchoBlockReason CheckEchoTarget(BoardState state, GridPos target, GridPos newPlayer)
        {
            if (!Level.InBounds(target)) return EchoBlockReason.OutOfBounds;
            if (Level.IsWall(target)) return EchoBlockReason.Wall;
            if (IsClosedGate(target, state.GateOpenBits)) return EchoBlockReason.GateClosed;
            if (target == newPlayer) return EchoBlockReason.BlockedByPlayer;
            return EchoBlockReason.None;
        }

        private bool IsClosedGate(GridPos cell, ulong gateOpenBits)
        {
            var gate = Level.GateIndexAt(cell);
            return gate >= 0 && (gateOpenBits & (1UL << gate)) == 0;
        }

        private void EnsureInvariants(BoardState next)
        {
            if (!Level.InBounds(next.Player))
                throw new InvalidOperationException($"Rules invariant violated: player left the board at {next.Player}.");
            if (next.Echo.HasValue && !Level.InBounds(next.Echo.Value))
                throw new InvalidOperationException($"Rules invariant violated: echo left the board at {next.Echo.Value}.");
            if (next.Echo.HasValue && next.Echo.Value == next.Player)
                throw new InvalidOperationException($"Rules invariant violated: player and echo share cell {next.Player}.");
        }
    }
}
