using System;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    public enum ResumeDiscardReason
    {
        None,

        /// <summary>The level's revision (or the rules) changed since the session was saved; it restarts.</summary>
        LevelUpdated,

        /// <summary>The saved level no longer exists in the catalog.</summary>
        LevelMissing,

        /// <summary>The snapshot did not fit the level (corrupted but checksummed data).</summary>
        InvalidSnapshot
    }

    public sealed class MoveOutcome
    {
        public MoveOutcome(MoveResult result, CompletionOutcome completion)
        {
            Result = result;
            Completion = completion;
        }

        public MoveResult Result { get; }

        /// <summary>Set exactly once per win, on the move that won the level.</summary>
        public CompletionOutcome Completion { get; }
    }

    /// <summary>
    /// Application service: owns the active <see cref="GameSession"/>, progress and settings, and requests saves
    /// after every accepted change. Presentation talks to this class only.
    /// </summary>
    public sealed class GameCoordinator
    {
        private readonly ISaveStore _store;
        private readonly Func<DateTime> _utcNow;
        private string _lastPlayedLevelId;

        public GameCoordinator(LevelLibrary library, ISaveStore store, Func<DateTime> utcNow = null)
        {
            Library = library ?? throw new ArgumentNullException(nameof(library));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            Progress = new ProgressService();
            Settings = new GameSettings();
        }

        public LevelLibrary Library { get; }
        public ProgressService Progress { get; private set; }
        public GameSettings Settings { get; private set; }
        public GameSession Current { get; private set; }
        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.NoSave;
        public ResumeDiscardReason ResumeDiscardReason { get; private set; }

        public int CurrentIndex => Current == null ? -1 : Library.IndexOf(Current.LevelId);
        public bool HasResumableSession => Current != null && !Current.IsWon;

        public void Load()
        {
            var result = _store.Load();
            LoadStatus = result.Status;
            ResumeDiscardReason = ResumeDiscardReason.None;
            Current = null;

            var data = result.Data;
            if (data == null) return;

            Progress = new ProgressService(data.Progress);
            Settings = data.Settings.Clone();
            Settings.Normalize();
            _lastPlayedLevelId = data.LastPlayedLevelId;

            var snapshot = data.ActiveSession;
            if (snapshot == null) return;

            if (!Library.TryGet(snapshot.LevelId, out var level))
            {
                ResumeDiscardReason = ResumeDiscardReason.LevelMissing;
                return;
            }

            if (level.Revision != snapshot.LevelRevision || data.RulesVersion != RulesEngine.Version)
            {
                ResumeDiscardReason = ResumeDiscardReason.LevelUpdated;
                return;
            }

            try
            {
                Current = GameSession.FromSnapshot(level, snapshot);
            }
            catch (ArgumentException)
            {
                ResumeDiscardReason = ResumeDiscardReason.InvalidSnapshot;
            }
        }

        /// <summary>
        /// Level that "Devam Et" opens: the half-finished session, otherwise the first incomplete level, otherwise the
        /// level after the last one played. Returns -1 when there is nothing sensible to continue.
        /// </summary>
        public int FindContinueIndex()
        {
            if (HasResumableSession) return CurrentIndex;

            for (var i = 0; i < Library.Count; i++)
            {
                if (!Progress.IsCompleted(Library.Levels[i].Id)) return Progress.IsUnlocked(Library, i) ? i : -1;
            }

            var last = Library.IndexOf(_lastPlayedLevelId);
            return last >= 0 ? Math.Min(last + 1, Library.Count - 1) : -1;
        }

        /// <summary>Opens a level. Re-opening the level of the unfinished session resumes it.</summary>
        public GameSession StartLevel(int index)
        {
            if (!Progress.IsUnlocked(Library, index)) throw new InvalidOperationException($"Level {index} is locked.");

            var level = Library.Levels[index];
            if (!(HasResumableSession && Current.LevelId == level.Id))
            {
                Current = new GameSession(level);
            }

            _lastPlayedLevelId = level.Id;
            Progress.MarkPlayed(level, _utcNow());
            RequestSave();
            return Current;
        }

        public MoveOutcome Move(Direction direction)
        {
            var session = RequireSession();
            var result = session.TryMove(direction);
            if (!result.Accepted) return new MoveOutcome(result, null);

            CompletionOutcome completion = null;
            if (result.NextState.IsWon)
            {
                completion = Progress.RecordCompletion(session.Level, result.NextState.MoveCount, session.HintUsed, _utcNow());
            }

            RequestSave();
            return new MoveOutcome(result, completion);
        }

        public bool Undo()
        {
            if (!RequireSession().Undo()) return false;
            RequestSave();
            return true;
        }

        public void Restart()
        {
            RequireSession().Restart();
            RequestSave();
        }

        /// <summary>Marks the hint as used for this attempt and persists that fact immediately.</summary>
        public int TakeHint()
        {
            var stage = RequireSession().TakeHint();
            RequestSave();
            return stage;
        }

        public void UpdateSettings(Action<GameSettings> change)
        {
            var copy = Settings.Clone();
            change(copy);
            copy.Normalize();
            Settings = copy;
            RequestSave();
        }

        public void Tick(double deltaSeconds) => Current?.AddActiveTime(deltaSeconds);

        public SaveData BuildSaveData()
        {
            // A won session is not "half-finished": Continue moves on instead of reopening a finished board.
            var snapshot = HasResumableSession ? Current.ToSnapshot() : null;
            return new SaveData(RulesEngine.Version, Library.ContentVersion, snapshot, Progress.ToList(), Settings.Clone(), _lastPlayedLevelId);
        }

        public void RequestSave() => _store.Save(BuildSaveData());

        /// <summary>Saves the latest state (including active time) and waits for the write to finish.</summary>
        public void Flush()
        {
            RequestSave();
            _store.Flush();
        }

        private GameSession RequireSession() => Current ?? throw new InvalidOperationException("No level is active.");
    }
}
