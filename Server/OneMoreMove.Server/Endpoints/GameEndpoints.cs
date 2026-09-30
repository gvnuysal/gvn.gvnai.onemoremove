using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OneMoreMove.Core;
using OneMoreMove.Persistence;
using OneMoreMove.Server.Auth;
using OneMoreMove.Server.Data;
using OneMoreMove.Server.Levels;
using OneMoreMove.Session;

namespace OneMoreMove.Server.Endpoints
{
    public static class GameEndpoints
    {
        public const string RegisterRateLimit = "register";
        private const int MaxLevelIdLength = 64;

        public static void MapGameEndpoints(this IEndpointRouteBuilder app)
        {
            var api = app.MapGroup("/api/v1");

            api.MapPost("/accounts", RegisterAsync).RequireRateLimiting(RegisterRateLimit);
            api.MapPost("/sessions", SignInAsync);

            api.MapGet("/progress", GetProgressAsync).RequireAuthorization();
            api.MapPut("/progress", SyncProgressAsync).RequireAuthorization();

            api.MapPost("/levels/{levelId}/runs", SubmitRunAsync).RequireAuthorization();
            api.MapGet("/levels/{levelId}/leaderboard", GetLeaderboardAsync);

            api.MapGet("/level-packs", ListPacksAsync);
            api.MapGet("/level-packs/{version:int}", GetPackAsync);
            api.MapPost("/level-packs", PublishPackAsync);
        }

        // Accounts ---------------------------------------------------------------------------------------------------

        private static async Task<IResult> RegisterAsync(GameDb db)
        {
            var secret = SecretHasher.NewSecret();
            var (salt, hash) = SecretHasher.Hash(secret);
            var player = new Player
            {
                Id = Guid.NewGuid(),
                SecretSalt = salt,
                SecretHash = hash,
                DisplayName = "Oyuncu-" + RandomNumberGenerator.GetInt32(1000, 10000),
                CreatedUtc = DateTime.UtcNow
            };

            db.Players.Add(player);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/players/{player.Id}",
                new RegisterResponse { PlayerId = player.Id.ToString(), Secret = secret, DisplayName = player.DisplayName });
        }

        private static async Task<IResult> SignInAsync(SignInRequest request, GameDb db, TokenIssuer tokens)
        {
            if (request == null || !Guid.TryParse(request.PlayerId, out var id)) return Results.Unauthorized();

            var player = await db.Players.FindAsync(id);
            if (player == null || !SecretHasher.Verify(request.Secret, player.SecretSalt, player.SecretHash)) return Results.Unauthorized();

            var (token, expires) = tokens.Issue(player.Id);
            return Results.Ok(new TokenResponse { Token = token, ExpiresUtc = expires });
        }

        // Progress ---------------------------------------------------------------------------------------------------

        private static async Task<IResult> GetProgressAsync(ClaimsPrincipal user, GameDb db)
        {
            if (!TokenIssuer.TryGetPlayerId(user, out var playerId)) return Results.Unauthorized();
            return Results.Ok(new ProgressSyncDocument { Records = (await LoadProgressAsync(db, playerId)).Select(HttpRemoteService.ToDto).ToList() });
        }

        /// <summary>Merges the device's progress into the stored progress and returns the result for the device to adopt.</summary>
        private static async Task<IResult> SyncProgressAsync(ProgressSyncDocument document, ClaimsPrincipal user, GameDb db, ServerOptions options)
        {
            if (!TokenIssuer.TryGetPlayerId(user, out var playerId)) return Results.Unauthorized();

            var incoming = document?.Records ?? new List<CloudProgressDto>();
            var problems = ValidateProgress(incoming, options.MaxProgressRecords);
            if (problems.Count > 0) return Results.BadRequest(new ErrorResponse { Error = "Invalid progress.", Details = problems });

            var stored = await LoadProgressAsync(db, playerId);
            var merged = ProgressMerger.Merge(stored, incoming.Select(HttpRemoteService.FromDto));

            var rows = await db.Progress.Where(p => p.PlayerId == playerId).ToDictionaryAsync(p => p.LevelId);
            foreach (var record in merged)
            {
                if (!rows.TryGetValue(record.LevelId, out var row))
                {
                    row = new ProgressRow { PlayerId = playerId, LevelId = record.LevelId };
                    db.Progress.Add(row);
                }

                row.Revision = record.LevelRevision;
                row.Completed = record.Completed;
                row.BestMoves = record.BestMoves;
                row.BestStars = record.BestStars;
                row.LastPlayedUtc = record.LastPlayedUtc;
            }

            await db.SaveChangesAsync();
            return Results.Ok(new ProgressSyncDocument { Records = merged.Select(HttpRemoteService.ToDto).ToList() });
        }

        private static async Task<IReadOnlyList<ProgressRecord>> LoadProgressAsync(GameDb db, Guid playerId) =>
            (await db.Progress.AsNoTracking().Where(p => p.PlayerId == playerId).ToListAsync())
            .Select(p => new ProgressRecord(p.LevelId, p.Revision, p.Completed, p.BestMoves, p.BestStars, DateTime.SpecifyKind(p.LastPlayedUtc, DateTimeKind.Utc)))
            .ToArray();

        private static List<string> ValidateProgress(IReadOnlyCollection<CloudProgressDto> records, int max)
        {
            var problems = new List<string>();
            if (records.Count > max) problems.Add($"At most {max} records per sync.");
            foreach (var r in records)
            {
                if (string.IsNullOrWhiteSpace(r?.LevelId) || r.LevelId.Length > MaxLevelIdLength) problems.Add("Every record needs a level id (max 64 characters).");
                else if (r.Revision < 1 || r.BestStars < 0 || r.BestStars > 3 || r.BestMoves < 0) problems.Add($"{r.LevelId}: values out of range.");
            }

            return problems;
        }

        // Runs and leaderboards --------------------------------------------------------------------------------------

        /// <summary>
        /// Replays the submitted commands through the real rules on the exact level revision. Only a run that reaches the
        /// goal counts; its length is the replayed move count, never a number the client claims.
        /// </summary>
        private static async Task<IResult> SubmitRunAsync(string levelId, RunRequest request, ClaimsPrincipal user, GameDb db,
            LevelRegistry levels, ServerOptions options)
        {
            if (!TokenIssuer.TryGetPlayerId(user, out var playerId)) return Results.Unauthorized();
            if (request?.Moves == null || request.Moves.Count == 0 || request.Moves.Count > options.MaxRunMoves)
                return Results.BadRequest(new ErrorResponse { Error = $"A run needs 1 to {options.MaxRunMoves} moves." });
            if (!levels.TryGet(levelId, request.Revision, out var level))
                return Results.NotFound(new ErrorResponse { Error = $"Unknown level {levelId} revision {request.Revision}." });

            var commands = new List<Direction>(request.Moves.Count);
            foreach (var move in request.Moves)
            {
                if (!Directions.TryParse(move, out var direction)) return Results.BadRequest(new ErrorResponse { Error = $"Unknown move '{move}'." });
                commands.Add(direction);
            }

            var replay = SolutionReplayer.Replay(level, commands);
            if (!replay.ReachedGoal)
            {
                var error = replay.FailedStep >= 0
                    ? $"Move {replay.FailedStep + 1} is not allowed ({replay.FailureReason})."
                    : "The moves do not reach the goal.";
                return Results.UnprocessableEntity(new RunResponse { Accepted = false, Error = error, Moves = commands.Count });
            }

            var moves = replay.FinalState.MoveCount;
            var best = await db.BestRuns.FindAsync(playerId, levelId, request.Revision);
            if (best == null)
            {
                best = new BestRun { PlayerId = playerId, LevelId = levelId, Revision = request.Revision };
                db.BestRuns.Add(best);
            }

            if (best.Moves == 0 || moves < best.Moves)
            {
                best.Moves = moves;
                best.Commands = string.Join(" ", commands);
                best.AchievedUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }

            var runs = db.BestRuns.AsNoTracking().Where(r => r.LevelId == levelId && r.Revision == request.Revision);
            var ahead = await runs.CountAsync(r => r.Moves < best.Moves || (r.Moves == best.Moves && r.AchievedUtc < best.AchievedUtc));
            var players = await runs.CountAsync();
            return Results.Ok(new RunResponse { Accepted = true, Moves = moves, BestMoves = best.Moves, Rank = ahead + 1, Players = players });
        }

        private static async Task<IResult> GetLeaderboardAsync(string levelId, int? revision, int? top, GameDb db, LevelRegistry levels)
        {
            var rev = revision ?? levels.LatestRevision(levelId);
            if (rev == null || !levels.TryGet(levelId, rev.Value, out _)) return Results.NotFound(new ErrorResponse { Error = $"Unknown level {levelId}." });

            var count = Math.Clamp(top ?? 10, 1, 100);
            var rows = await db.BestRuns.AsNoTracking()
                .Where(r => r.LevelId == levelId && r.Revision == rev.Value)
                .OrderBy(r => r.Moves).ThenBy(r => r.AchievedUtc)
                .Take(count)
                .Join(db.Players, r => r.PlayerId, p => p.Id, (r, p) => new { r.Moves, r.AchievedUtc, p.DisplayName })
                .ToListAsync();

            return Results.Ok(new LeaderboardResponse
            {
                LevelId = levelId,
                Revision = rev.Value,
                Entries = rows.Select((r, i) => new LeaderboardEntryDto
                {
                    Rank = i + 1,
                    DisplayName = r.DisplayName,
                    Moves = r.Moves,
                    AchievedUtc = DateTime.SpecifyKind(r.AchievedUtc, DateTimeKind.Utc)
                }).ToList()
            });
        }

        // Level packs ------------------------------------------------------------------------------------------------

        private static async Task<IResult> ListPacksAsync(GameDb db)
        {
            var packs = await db.LevelPacks.AsNoTracking().OrderBy(p => p.Version).ToListAsync();
            return Results.Ok(packs.Select(p => new LevelPackSummary
            {
                Version = p.Version,
                Sha256 = p.Sha256,
                PublishedUtc = DateTime.SpecifyKind(p.PublishedUtc, DateTimeKind.Utc),
                LevelCount = JsonConvert.DeserializeObject<LevelPackDocument>(p.Document)?.Levels.Count ?? 0
            }).ToList());
        }

        private static async Task<IResult> GetPackAsync(int version, GameDb db)
        {
            var pack = await db.LevelPacks.AsNoTracking().FirstOrDefaultAsync(p => p.Version == version);
            return pack == null
                ? Results.NotFound(new ErrorResponse { Error = $"No level pack {version}." })
                : Results.Content(pack.Document, "application/json", Encoding.UTF8);
        }

        /// <summary>Publishes the next pack version after proving every level; the whole pack is rejected on any error.</summary>
        private static async Task<IResult> PublishPackAsync(HttpRequest http, LevelPackDocument document, GameDb db, LevelRegistry levels,
            ServerOptions options)
        {
            if (!IsAdmin(http, options)) return Results.Unauthorized();

            var validation = levels.Validate(document);
            if (!validation.IsValid)
                return Results.UnprocessableEntity(new ErrorResponse { Error = "The pack was rejected.", Details = validation.Errors.ToList() });

            var summary = await PublishAsync(db, levels, document, validation);
            return Results.Created($"/api/v1/level-packs/{summary.Version}", summary);
        }

        internal static async Task<LevelPackSummary> PublishAsync(GameDb db, LevelRegistry levels, LevelPackDocument document, PackValidation validation)
        {
            var version = (await db.LevelPacks.MaxAsync(p => (int?)p.Version) ?? 0) + 1;
            var published = new LevelPackDocument { Version = version, Levels = validation.Levels.Select(LevelJson.ToJson).ToList() };
            var json = JsonConvert.SerializeObject(published);
            var pack = new LevelPack { Version = version, Document = json, Sha256 = LevelRegistry.Sha256(json), PublishedUtc = DateTime.UtcNow };

            db.LevelPacks.Add(pack);
            await db.SaveChangesAsync();
            levels.Add(validation.Levels);
            return new LevelPackSummary { Version = version, LevelCount = validation.Levels.Count, Sha256 = pack.Sha256, PublishedUtc = pack.PublishedUtc };
        }

        private static bool IsAdmin(HttpRequest request, ServerOptions options)
        {
            if (string.IsNullOrEmpty(options.AdminApiKey)) return false;
            var given = request.Headers["X-Admin-Key"].ToString();
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(given), Encoding.UTF8.GetBytes(options.AdminApiKey));
        }
    }
}
