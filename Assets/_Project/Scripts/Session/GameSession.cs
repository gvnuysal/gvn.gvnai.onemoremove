using System;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>
    /// One attempt at one level: current state, undo history and hint flag. All rules are delegated to
    /// <see cref="RulesEngine"/>; the session only decides what to keep.
    /// </summary>
    public sealed class GameSession
    {
        private int _hintStateVersion = -1;
        private int _hintStage;

        public GameSession(LevelDefinition level, int undoCapacity = UndoHistory.DefaultCapacity)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Rules = new RulesEngine(level);
            History = new UndoHistory(undoCapacity);
            State = level.CreateInitialState();
            Attempts = 1;
        }

        public LevelDefinition Level { get; }
        public RulesEngine Rules { get; }
        public UndoHistory History { get; }
        public BoardState State { get; private set; }

        /// <summary>Once a hint is taken, it stays recorded for this attempt, even after undo.</summary>
        public bool HintUsed { get; private set; }

        public int Attempts { get; private set; }
        public double ActiveSeconds { get; private set; }

        /// <summary>Increments on every state change; async work (hints) uses it to detect stale results.</summary>
        public int StateVersion { get; private set; }

        public string LevelId => Level.Id;
        public int LevelRevision => Level.Revision;
        public bool IsWon => State.IsWon;
        public bool CanUndo => History.Count > 0 && !State.IsWon;

        /// <summary>Applies the move when the rules accept it. A rejected move leaves state and history unchanged.</summary>
        public MoveResult TryMove(Direction direction)
        {
            var result = Rules.TryMove(State, direction);
            if (!result.Accepted) return result;

            History.Push(State);
            State = result.NextState;
            StateVersion++;
            return result;
        }

        /// <summary>What <see cref="TryMove"/> would do, without changing anything.</summary>
        public MoveResult Preview(Direction direction) => Rules.TryMove(State, direction);

        /// <summary>Restores the previous full snapshot. A won level is final for this attempt.</summary>
        public bool Undo()
        {
            if (!CanUndo || !History.TryPop(out var previous)) return false;

            State = previous;
            StateVersion++;
            return true;
        }

        /// <summary>Starts a new attempt from the level definition.</summary>
        public void Restart()
        {
            State = Level.CreateInitialState();
            History.Clear();
            HintUsed = false;
            Attempts++;
            StateVersion++;
        }

        /// <summary>Marks the hint as used and returns the stage to show: 1 = mechanic reminder, 2 = suggested direction.</summary>
        public int TakeHint()
        {
            HintUsed = true;
            if (_hintStateVersion != StateVersion)
            {
                _hintStateVersion = StateVersion;
                _hintStage = 0;
            }

            _hintStage = Math.Min(2, _hintStage + 1);
            return _hintStage;
        }

        public void AddActiveTime(double seconds)
        {
            if (seconds > 0 && !State.IsWon) ActiveSeconds += seconds;
        }

        public SessionSnapshot ToSnapshot() =>
            new SessionSnapshot(Level.Id, Level.Revision, State, History.ToList(), HintUsed, Attempts, ActiveSeconds);

        /// <summary>Rebuilds a session from a snapshot. Throws <see cref="ArgumentException"/> if it does not fit the level.</summary>
        public static GameSession FromSnapshot(LevelDefinition level, SessionSnapshot snapshot, int undoCapacity = UndoHistory.DefaultCapacity)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.LevelId != level.Id || snapshot.LevelRevision != level.Revision)
                throw new ArgumentException($"Snapshot for {snapshot.LevelId} r{snapshot.LevelRevision} does not match {level.Id} r{level.Revision}.");

            EnsurePlausible(level, snapshot.State);
            var session = new GameSession(level, undoCapacity);
            foreach (var state in snapshot.UndoStates)
            {
                EnsurePlausible(level, state);
                session.History.Push(state);
            }

            session.State = snapshot.State;
            session.HintUsed = snapshot.HintUsed;
            session.Attempts = Math.Max(1, snapshot.Attempts);
            session.ActiveSeconds = Math.Max(0, snapshot.ActiveSeconds);
            return session;
        }

        private static void EnsurePlausible(LevelDefinition level, BoardState state)
        {
            if (state == null) throw new ArgumentException("Snapshot contains a null state.");
            if (!level.InBounds(state.Player) || level.IsWall(state.Player)) throw new ArgumentException($"Invalid player cell {state.Player}.");
            if (state.HasEcho != level.EchoStart.HasValue) throw new ArgumentException("Echo presence does not match the level.");
            if (state.Echo.HasValue && (!level.InBounds(state.Echo.Value) || level.IsWall(state.Echo.Value) || state.Echo.Value == state.Player))
                throw new ArgumentException($"Invalid echo cell {state.Echo.Value}.");
            if ((state.GateOpenBits & ~level.AllGatesMask) != 0) throw new ArgumentException("Gate bits exceed the level's gates.");
            if (state.IsWon != (state.Player == level.Goal)) throw new ArgumentException("Win flag does not match the player cell.");
        }
    }
}
