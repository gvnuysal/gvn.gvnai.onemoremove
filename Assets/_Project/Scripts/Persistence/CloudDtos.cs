using System;
using System.Collections.Generic;

namespace OneMoreMove.Persistence
{
    // Wire format of the game server API (JSON, camelCase). Shared verbatim by the game client (Newtonsoft) and the
    // server (System.Text.Json), so both sides always agree. Plain properties only.

    public sealed class RegisterResponse
    {
        public string PlayerId { get; set; }
        public string Secret { get; set; }
        public string DisplayName { get; set; }
    }

    public sealed class SignInRequest
    {
        public string PlayerId { get; set; }
        public string Secret { get; set; }
    }

    public sealed class TokenResponse
    {
        public string Token { get; set; }
        public DateTime ExpiresUtc { get; set; }
    }

    public sealed class CloudProgressDto
    {
        public string LevelId { get; set; }
        public int Revision { get; set; }
        public bool Completed { get; set; }
        public int? BestMoves { get; set; }
        public int BestStars { get; set; }
        public DateTime LastPlayedUtc { get; set; }
    }

    public sealed class ProgressSyncDocument
    {
        public List<CloudProgressDto> Records { get; set; } = new List<CloudProgressDto>();
    }

    public sealed class RunRequest
    {
        public int Revision { get; set; }

        /// <summary>Commands as <see cref="OneMoreMove.Core.Direction"/> names ("Right", "Wait", ...).</summary>
        public List<string> Moves { get; set; } = new List<string>();
    }

    public sealed class RunResponse
    {
        public bool Accepted { get; set; }
        public string Error { get; set; }
        public int Moves { get; set; }
        public int BestMoves { get; set; }
        public int Rank { get; set; }
        public int Players { get; set; }
    }

    public sealed class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string DisplayName { get; set; }
        public int Moves { get; set; }
        public DateTime AchievedUtc { get; set; }
    }

    public sealed class LeaderboardResponse
    {
        public string LevelId { get; set; }
        public int Revision { get; set; }
        public List<LeaderboardEntryDto> Entries { get; set; } = new List<LeaderboardEntryDto>();
    }

    public sealed class LevelPackSummary
    {
        public int Version { get; set; }
        public int LevelCount { get; set; }
        public string Sha256 { get; set; }
        public DateTime PublishedUtc { get; set; }
    }

    /// <summary>A versioned set of levels; each entry is a <see cref="LevelJson"/> document.</summary>
    public sealed class LevelPackDocument
    {
        public int Version { get; set; }
        public List<string> Levels { get; set; } = new List<string>();
    }

    public sealed class ErrorResponse
    {
        public string Error { get; set; }
        public List<string> Details { get; set; } = new List<string>();
    }
}
