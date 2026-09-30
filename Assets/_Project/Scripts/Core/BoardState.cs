using System;

namespace OneMoreMove.Core
{
    /// <summary>
    /// Immutable dynamic state of a level. Static board data lives in <see cref="LevelDefinition"/>.
    /// </summary>
    public sealed class BoardState : IEquatable<BoardState>
    {
        public BoardState(GridPos player, GridPos? echo, ulong gateOpenBits, int moveCount, bool isWon)
        {
            if (moveCount < 0) throw new ArgumentOutOfRangeException(nameof(moveCount));

            Player = player;
            Echo = echo;
            GateOpenBits = gateOpenBits;
            MoveCount = moveCount;
            IsWon = isWon;
        }

        public GridPos Player { get; }
        public GridPos? Echo { get; }
        public bool HasEcho => Echo.HasValue;
        public ulong GateOpenBits { get; }
        public int MoveCount { get; }
        public bool IsWon { get; }

        public bool IsGateOpen(int gateIndex) => (GateOpenBits & (1UL << gateIndex)) != 0;

        /// <summary>Identity used by the solver: positions and gates only, never counters or presentation data.</summary>
        public StateKey ToKey() => new StateKey(Player, Echo, GateOpenBits);

        public bool Equals(BoardState other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (other is null) return false;
            return Player == other.Player
                   && Echo == other.Echo
                   && GateOpenBits == other.GateOpenBits
                   && MoveCount == other.MoveCount
                   && IsWon == other.IsWon;
        }

        public override bool Equals(object obj) => Equals(obj as BoardState);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ToKey().GetHashCode();
                hash = (hash * 397) ^ MoveCount;
                return (hash * 397) ^ (IsWon ? 1 : 0);
            }
        }

        public override string ToString() =>
            $"Player{Player} Echo{(Echo.HasValue ? Echo.Value.ToString() : "-")} Gates=0x{GateOpenBits:X} Moves={MoveCount}{(IsWon ? " WON" : string.Empty)}";
    }
}
