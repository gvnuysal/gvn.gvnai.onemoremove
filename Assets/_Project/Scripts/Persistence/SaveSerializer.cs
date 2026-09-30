using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Persistence
{
    public enum SaveReadStatus
    {
        Ok,
        Invalid,
        FutureVersion
    }

    public sealed class SaveReadResult
    {
        public SaveReadResult(SaveReadStatus status, SaveData data, string error)
        {
            Status = status;
            Data = data;
            Error = error;
        }

        public SaveReadStatus Status { get; }
        public SaveData Data { get; }
        public string Error { get; }
    }

    /// <summary>Converts <see cref="SaveData"/> to and from the versioned, checksummed JSON document.</summary>
    public sealed class SaveSerializer
    {
        public const int CurrentSchemaVersion = 1;

        private static readonly JsonSerializer Json = JsonSerializer.Create(new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore
        });

        private readonly SaveMigrator _migrator;

        public SaveSerializer(SaveMigrator migrator = null)
        {
            _migrator = migrator ?? new SaveMigrator(CurrentSchemaVersion);
        }

        public int SchemaVersion => _migrator.CurrentVersion;

        public string Serialize(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var dto = ToDto(data);
            dto.SchemaVersion = _migrator.CurrentVersion;
            dto.Checksum = null;

            // Hash the document exactly as the reader will see it after parsing, so both sides agree byte for byte.
            var document = Parse(JObject.FromObject(dto, Json).ToString(Formatting.None));
            document["checksum"] = SaveChecksum.Compute(document);
            return document.ToString(Formatting.Indented);
        }

        public SaveReadResult Deserialize(string text)
        {
            JObject document;
            try
            {
                document = Parse(text);
            }
            catch (Exception e) when (e is JsonException || e is InvalidCastException)
            {
                return Invalid($"Not valid JSON: {e.Message}");
            }

            var version = document.Value<int?>("schemaVersion");
            if (!version.HasValue) return Invalid("Missing schemaVersion.");
            if (_migrator.IsFutureVersion(version.Value))
                return new SaveReadResult(SaveReadStatus.FutureVersion, null, $"Schema {version.Value} is newer than {_migrator.CurrentVersion}.");

            var checksum = document.Value<string>("checksum");
            if (string.IsNullOrEmpty(checksum) || checksum != SaveChecksum.Compute(document)) return Invalid("Checksum mismatch.");

            if (!_migrator.TryMigrate(document, out var migrationError)) return Invalid(migrationError);

            try
            {
                var dto = document.ToObject<SaveEnvelopeDto>(Json);
                var error = Validate(dto);
                return error == null ? new SaveReadResult(SaveReadStatus.Ok, FromDto(dto), null) : Invalid(error);
            }
            catch (Exception e) when (e is JsonException || e is ArgumentException || e is FormatException || e is OverflowException)
            {
                return Invalid($"Schema error: {e.Message}");
            }
        }

        private static JObject Parse(string text)
        {
            using (var reader = new JsonTextReader(new StringReader(text)) { DateParseHandling = DateParseHandling.None, FloatParseHandling = FloatParseHandling.Double })
            {
                var token = JToken.ReadFrom(reader);
                if (reader.Read()) throw new JsonReaderException("Unexpected content after the document.");
                return token as JObject ?? throw new JsonReaderException("Root must be an object.");
            }
        }

        private static SaveReadResult Invalid(string error) => new SaveReadResult(SaveReadStatus.Invalid, null, error);

        private static string Validate(SaveEnvelopeDto dto)
        {
            if (dto == null) return "Empty document.";
            if (dto.Progress == null) return "Missing progress.";
            if (dto.Settings == null) return "Missing settings.";
            foreach (var p in dto.Progress)
            {
                if (p == null || string.IsNullOrEmpty(p.LevelId)) return "Progress entry without level id.";
                if (p.BestStars < 0 || p.BestStars > StarRating.MaxStars) return $"Invalid stars for {p.LevelId}.";
                if (p.BestMoves.HasValue && p.BestMoves.Value < 0) return $"Invalid best moves for {p.LevelId}.";
            }

            var s = dto.Session;
            if (s == null) return null;
            if (string.IsNullOrEmpty(s.LevelId)) return "Session without level id.";
            if (!IsValid(s.State)) return "Session without a valid state.";
            if (s.Undo == null || s.Undo.Any(u => !IsValid(u))) return "Session undo history is invalid.";
            if (s.Attempts < 1 || s.ActiveSeconds < 0 || double.IsNaN(s.ActiveSeconds)) return "Session counters are invalid.";
            return null;
        }

        private static bool IsValid(BoardStateDto state) => state != null && state.Player != null && state.MoveCount >= 0;

        private static SaveEnvelopeDto ToDto(SaveData data)
        {
            var s = data.Settings;
            return new SaveEnvelopeDto
            {
                RulesVersion = data.RulesVersion,
                ContentVersion = data.ContentVersion,
                LastPlayedLevelId = data.LastPlayedLevelId,
                Session = data.ActiveSession == null ? null : new SessionDto
                {
                    LevelId = data.ActiveSession.LevelId,
                    LevelRevision = data.ActiveSession.LevelRevision,
                    State = ToDto(data.ActiveSession.State),
                    Undo = data.ActiveSession.UndoStates.Select(ToDto).ToList(),
                    HintUsed = data.ActiveSession.HintUsed,
                    Attempts = data.ActiveSession.Attempts,
                    ActiveSeconds = data.ActiveSession.ActiveSeconds
                },
                Progress = data.Progress.Select(p => new ProgressDto
                {
                    LevelId = p.LevelId,
                    LevelRevision = p.LevelRevision,
                    Completed = p.Completed,
                    BestMoves = p.BestMoves,
                    BestStars = p.BestStars,
                    LastPlayedUnixSeconds = new DateTimeOffset(DateTime.SpecifyKind(p.LastPlayedUtc, DateTimeKind.Utc)).ToUnixTimeSeconds()
                }).ToList(),
                Settings = new SettingsDto
                {
                    ReducedMotion = s.ReducedMotion,
                    ShowMovePreview = s.ShowMovePreview,
                    TextScalePercent = s.TextScalePercent,
                    SfxVolume = s.SfxVolume,
                    MusicVolume = s.MusicVolume
                }
            };
        }

        private static SaveData FromDto(SaveEnvelopeDto dto)
        {
            SessionSnapshot session = null;
            if (dto.Session != null)
            {
                var s = dto.Session;
                session = new SessionSnapshot(s.LevelId, s.LevelRevision, FromDto(s.State), s.Undo.Select(FromDto).ToArray(), s.HintUsed, s.Attempts, s.ActiveSeconds);
            }

            var progress = dto.Progress.Select(p => new ProgressRecord(p.LevelId, p.LevelRevision, p.Completed, p.BestMoves, p.BestStars,
                DateTimeOffset.FromUnixTimeSeconds(p.LastPlayedUnixSeconds).UtcDateTime)).ToList();

            var settings = new GameSettings
            {
                ReducedMotion = dto.Settings.ReducedMotion,
                ShowMovePreview = dto.Settings.ShowMovePreview,
                TextScalePercent = dto.Settings.TextScalePercent,
                SfxVolume = dto.Settings.SfxVolume,
                MusicVolume = dto.Settings.MusicVolume
            };
            settings.Normalize();

            return new SaveData(dto.RulesVersion, dto.ContentVersion, session, progress, settings, dto.LastPlayedLevelId);
        }

        private static BoardStateDto ToDto(BoardState state) => new BoardStateDto
        {
            Player = new PosDto { X = state.Player.X, Y = state.Player.Y },
            Echo = state.Echo.HasValue ? new PosDto { X = state.Echo.Value.X, Y = state.Echo.Value.Y } : null,
            GateOpenBits = state.GateOpenBits,
            MoveCount = state.MoveCount,
            IsWon = state.IsWon
        };

        private static BoardState FromDto(BoardStateDto dto) => new BoardState(
            new GridPos(dto.Player.X, dto.Player.Y),
            dto.Echo == null ? (GridPos?)null : new GridPos(dto.Echo.X, dto.Echo.Y),
            dto.GateOpenBits,
            dto.MoveCount,
            dto.IsWon);
    }
}
