using System;
using System.Collections.Generic;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>
    /// Bounded stack of full <see cref="BoardState"/> snapshots. Small boards make whole snapshots simpler and safer than
    /// diffs. When full, the oldest entry is dropped, so <see cref="Count"/> is always the number of undoable steps.
    /// </summary>
    public sealed class UndoHistory
    {
        public const int DefaultCapacity = 500;

        private readonly BoardState[] _buffer;
        private int _start;

        public UndoHistory(int capacity = DefaultCapacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new BoardState[capacity];
        }

        public int Capacity => _buffer.Length;
        public int Count { get; private set; }

        public void Push(BoardState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            if (Count == _buffer.Length)
            {
                _buffer[_start] = state;
                _start = (_start + 1) % _buffer.Length;
                return;
            }

            _buffer[(_start + Count) % _buffer.Length] = state;
            Count++;
        }

        public bool TryPop(out BoardState state)
        {
            if (Count == 0)
            {
                state = null;
                return false;
            }

            Count--;
            var index = (_start + Count) % _buffer.Length;
            state = _buffer[index];
            _buffer[index] = null;
            return true;
        }

        public void Clear()
        {
            Array.Clear(_buffer, 0, _buffer.Length);
            _start = 0;
            Count = 0;
        }

        /// <summary>Oldest first; the last element is what <see cref="TryPop"/> returns next.</summary>
        public IReadOnlyList<BoardState> ToList()
        {
            var list = new BoardState[Count];
            for (var i = 0; i < Count; i++) list[i] = _buffer[(_start + i) % _buffer.Length];
            return list;
        }
    }
}
