using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OneMoreMove.Core;

namespace OneMoreMove.Persistence
{
    /// <summary>
    /// Text format for level content (seed files, import/export). Either explicit coordinates as in the design
    /// document, or a compact <c>map</c> in <see cref="AsciiLevelParser"/> notation that replaces the board fields.
    /// </summary>
    public static class LevelJson
    {
        public static LevelDefinition Parse(string json)
        {
            var dto = JsonConvert.DeserializeObject<LevelDto>(json) ?? throw new FormatException("Empty level document.");
            if (string.IsNullOrWhiteSpace(dto.Id)) throw new FormatException("Level id is required.");

            var solution = (dto.Solution ?? new List<string>()).Select(ParseDirection).ToArray();

            if (dto.Map != null && dto.Map.Count > 0)
            {
                var fromMap = AsciiLevelParser.Parse(new AsciiLevelParser.Options(), dto.Map);
                return new LevelDefinition(dto.Id, dto.Name, dto.Revision, dto.RulesVersion, fromMap.Width, fromMap.Height,
                    fromMap.Walls, fromMap.Gates, fromMap.PlayerStart, fromMap.EchoStart, fromMap.Goal,
                    dto.ParMoves, dto.OptimalMoves, solution, dto.Tutorial, dto.Tip);
            }

            if (dto.Player == null || dto.Goal == null) throw new FormatException($"Level '{dto.Id}' needs a player and a goal.");
            return new LevelDefinition(dto.Id, dto.Name, dto.Revision, dto.RulesVersion, dto.Width, dto.Height,
                (dto.Walls ?? new List<PosDto>()).Select(ToPos),
                (dto.Gates ?? new List<GateDto>()).Select(g => new GateDefinition(new GridPos(g.X, g.Y), g.Open)),
                ToPos(dto.Player), dto.Echo == null ? (GridPos?)null : ToPos(dto.Echo), ToPos(dto.Goal),
                dto.ParMoves, dto.OptimalMoves, solution, dto.Tutorial, dto.Tip);
        }

        public static string ToJson(LevelDefinition level)
        {
            var dto = new LevelDto
            {
                Id = level.Id,
                Name = level.Name,
                Revision = level.Revision,
                RulesVersion = level.RulesVersion,
                Tutorial = level.IsTutorial,
                Tip = level.Tip,
                Width = level.Width,
                Height = level.Height,
                Walls = level.Walls.Select(w => new PosDto { X = w.X, Y = w.Y }).ToList(),
                Gates = level.Gates.Select(g => new GateDto { X = g.Position.X, Y = g.Position.Y, Open = g.InitiallyOpen }).ToList(),
                Player = new PosDto { X = level.PlayerStart.X, Y = level.PlayerStart.Y },
                Echo = level.EchoStart.HasValue ? new PosDto { X = level.EchoStart.Value.X, Y = level.EchoStart.Value.Y } : null,
                Goal = new PosDto { X = level.Goal.X, Y = level.Goal.Y },
                ParMoves = level.ParMoves,
                OptimalMoves = level.OptimalMoves,
                Solution = level.KnownSolution.Select(d => d.ToString()).ToList()
            };
            return JsonConvert.SerializeObject(dto, Formatting.Indented);
        }

        private static GridPos ToPos(PosDto p) => new GridPos(p.X, p.Y);

        private static Direction ParseDirection(string value) =>
            Directions.TryParse(value, out var direction) ? direction : throw new FormatException($"Unknown solution command '{value}'.");

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class LevelDto
        {
            [JsonProperty("id")] public string Id;
            [JsonProperty("name")] public string Name;
            [JsonProperty("revision")] public int Revision = 1;
            [JsonProperty("rulesVersion")] public int RulesVersion = RulesEngine.Version;
            [JsonProperty("tutorial")] public bool Tutorial;
            [JsonProperty("tip", NullValueHandling = NullValueHandling.Ignore)] public string Tip;
            [JsonProperty("map", NullValueHandling = NullValueHandling.Ignore)] public List<string> Map;
            [JsonProperty("width")] public int Width;
            [JsonProperty("height")] public int Height;
            [JsonProperty("walls")] public List<PosDto> Walls;
            [JsonProperty("gates")] public List<GateDto> Gates;
            [JsonProperty("player")] public PosDto Player;
            [JsonProperty("echo")] public PosDto Echo;
            [JsonProperty("goal")] public PosDto Goal;
            [JsonProperty("parMoves")] public int ParMoves;
            [JsonProperty("optimalMoves")] public int? OptimalMoves;
            [JsonProperty("solution")] public List<string> Solution;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class GateDto
        {
            [JsonProperty("x")] public int X;
            [JsonProperty("y")] public int Y;
            [JsonProperty("open")] public bool Open;
        }
    }
}
