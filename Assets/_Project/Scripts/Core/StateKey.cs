using System;

namespace OneMoreMove.Core
{
    /// <summary>
    /// Search identity of a <see cref="BoardState"/>. Equality compares every field, so hash collisions
    /// never merge distinct states. If a future rule adds state (a counter, a colour), it must be added here.
    /// </summary>
    public readonly struct StateKey : IEquatable<StateKey>
    {
        public readonly GridPos Player;
        public readonly GridPos? Echo;
        public readonly ulong GateOpenBits;

        public StateKey(GridPos player, GridPos? echo, ulong gateOpenBits)
        {
            Player = player;
            Echo = echo;
            GateOpenBits = gateOpenBits;
        }

        public bool Equals(StateKey other) => Player == other.Player && Echo == other.Echo && GateOpenBits == other.GateOpenBits;
        public override bool Equals(object obj) => obj is StateKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Player.GetHashCode();
                hash = (hash * 397) ^ (Echo.HasValue ? Echo.Value.GetHashCode() + 1 : 0);
                return (hash * 397) ^ GateOpenBits.GetHashCode();
            }
        }

        public static bool operator ==(StateKey a, StateKey b) => a.Equals(b);
        public static bool operator !=(StateKey a, StateKey b) => !a.Equals(b);
    }
}
