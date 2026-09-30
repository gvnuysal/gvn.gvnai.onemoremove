using System;
using System.Collections.Generic;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>Everything needed to resume a half-finished level exactly as it was.</summary>
    public sealed class SessionSnapshot
    {
        public SessionSnapshot(string levelId, int levelRevision, BoardState state, IReadOnlyList<BoardState> undoStates,
            bool hintUsed, int attempts, double activeSeconds)
        {
            LevelId = levelId;
            LevelRevision = levelRevision;
            State = state ?? throw new ArgumentNullException(nameof(state));
            UndoStates = undoStates ?? Array.Empty<BoardState>();
            HintUsed = hintUsed;
            Attempts = attempts;
            ActiveSeconds = activeSeconds;
        }

        public string LevelId { get; }
        public int LevelRevision { get; }
        public BoardState State { get; }

        /// <summary>Oldest first.</summary>
        public IReadOnlyList<BoardState> UndoStates { get; }

        public bool HintUsed { get; }
        public int Attempts { get; }
        public double ActiveSeconds { get; }
    }

    /// <summary>Best results per level. Bests are stored separately and never lowered by a worse attempt.</summary>
    public sealed class ProgressRecord
    {
        public ProgressRecord(string levelId, int levelRevision, bool completed, int? bestMoves, int bestStars, DateTime lastPlayedUtc)
        {
            LevelId = levelId;
            LevelRevision = levelRevision;
            Completed = completed;
            BestMoves = bestMoves;
            BestStars = bestStars;
            LastPlayedUtc = lastPlayedUtc;
        }

        public string LevelId { get; }

        /// <summary>Revision the <see cref="BestMoves"/> was achieved on; best moves are not compared across revisions.</summary>
        public int LevelRevision { get; }

        public bool Completed { get; }
        public int? BestMoves { get; }
        public int BestStars { get; }
        public DateTime LastPlayedUtc { get; }
    }

    public sealed class GameSettings
    {
        public const int MinTextScalePercent = 100;
        public const int MaxTextScalePercent = 150;

        /// <summary>Removes move/gate transitions; the board snaps to the result.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Shows a translucent preview of where pieces will land for the hovered direction.</summary>
        public bool ShowMovePreview { get; set; }

        public int TextScalePercent { get; set; } = MinTextScalePercent;

        // Reserved for the audio milestone; stored now so the save schema does not need a migration later.
        public float SfxVolume { get; set; } = 1f;
        public float MusicVolume { get; set; } = 0.6f;

        public GameSettings Clone() => (GameSettings)MemberwiseClone();

        public void Normalize()
        {
            TextScalePercent = Math.Max(MinTextScalePercent, Math.Min(MaxTextScalePercent, TextScalePercent));
            SfxVolume = Math.Max(0f, Math.Min(1f, SfxVolume));
            MusicVolume = Math.Max(0f, Math.Min(1f, MusicVolume));
        }
    }

    /// <summary>The persisted aggregate. Persistence maps it to a versioned envelope.</summary>
    public sealed class SaveData
    {
        public SaveData(int rulesVersion, int contentVersion, SessionSnapshot activeSession, IReadOnlyList<ProgressRecord> progress,
            GameSettings settings, string lastPlayedLevelId)
        {
            RulesVersion = rulesVersion;
            ContentVersion = contentVersion;
            ActiveSession = activeSession;
            Progress = progress ?? Array.Empty<ProgressRecord>();
            Settings = settings ?? new GameSettings();
            LastPlayedLevelId = lastPlayedLevelId;
        }

        public int RulesVersion { get; }
        public int ContentVersion { get; }
        public SessionSnapshot ActiveSession { get; }
        public IReadOnlyList<ProgressRecord> Progress { get; }
        public GameSettings Settings { get; }
        public string LastPlayedLevelId { get; }
    }
}
