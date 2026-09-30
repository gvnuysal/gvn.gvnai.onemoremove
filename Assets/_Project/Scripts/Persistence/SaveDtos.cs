using System.Collections.Generic;
using Newtonsoft.Json;

namespace OneMoreMove.Persistence
{
    // Wire format of save.json. These types are the schema: renaming a property is a schema change and needs a migration.

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class SaveEnvelopeDto
    {
        [JsonProperty("schemaVersion", Order = 0)] public int SchemaVersion;
        [JsonProperty("rulesVersion", Order = 1)] public int RulesVersion;
        [JsonProperty("contentVersion", Order = 2)] public int ContentVersion;
        [JsonProperty("lastPlayedLevelId", Order = 3)] public string LastPlayedLevelId;
        [JsonProperty("session", Order = 4)] public SessionDto Session;
        [JsonProperty("progress", Order = 5)] public List<ProgressDto> Progress = new List<ProgressDto>();
        [JsonProperty("settings", Order = 6)] public SettingsDto Settings = new SettingsDto();
        [JsonProperty("checksum", Order = 7)] public string Checksum;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class SessionDto
    {
        [JsonProperty("levelId")] public string LevelId;
        [JsonProperty("levelRevision")] public int LevelRevision;
        [JsonProperty("state")] public BoardStateDto State;
        [JsonProperty("undo")] public List<BoardStateDto> Undo = new List<BoardStateDto>();
        [JsonProperty("hintUsed")] public bool HintUsed;
        [JsonProperty("attempts")] public int Attempts;
        [JsonProperty("activeSeconds")] public double ActiveSeconds;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class BoardStateDto
    {
        [JsonProperty("player")] public PosDto Player;
        [JsonProperty("echo")] public PosDto Echo;
        [JsonProperty("gates")] public ulong GateOpenBits;
        [JsonProperty("moves")] public int MoveCount;
        [JsonProperty("won")] public bool IsWon;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class PosDto
    {
        [JsonProperty("x")] public int X;
        [JsonProperty("y")] public int Y;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class ProgressDto
    {
        [JsonProperty("levelId")] public string LevelId;
        [JsonProperty("levelRevision")] public int LevelRevision;
        [JsonProperty("completed")] public bool Completed;
        [JsonProperty("bestMoves")] public int? BestMoves;
        [JsonProperty("bestStars")] public int BestStars;
        [JsonProperty("lastPlayedUnix")] public long LastPlayedUnixSeconds;
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class SettingsDto
    {
        [JsonProperty("reducedMotion")] public bool ReducedMotion;
        [JsonProperty("showMovePreview")] public bool ShowMovePreview;
        [JsonProperty("textScalePercent")] public int TextScalePercent = 100;
        [JsonProperty("sfxVolume")] public float SfxVolume = 1f;
        [JsonProperty("musicVolume")] public float MusicVolume = 0.6f;
    }
}
