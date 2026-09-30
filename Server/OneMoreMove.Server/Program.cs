using System;
using System.IO;
using System.Linq;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OneMoreMove.Persistence;
using OneMoreMove.Server;
using OneMoreMove.Server.Auth;
using OneMoreMove.Server.Data;
using OneMoreMove.Server.Endpoints;
using OneMoreMove.Server.Levels;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
builder.Services.AddSingleton(options);

builder.Services.AddDbContext<GameDb>(db =>
{
    if (string.Equals(options.Database.Provider, "Postgres", StringComparison.OrdinalIgnoreCase)) db.UseNpgsql(options.Database.ConnectionString);
    else db.UseSqlite(options.Database.ConnectionString);
});

var tokens = new TokenIssuer(options);
builder.Services.AddSingleton(tokens);
builder.Services.AddSingleton<LevelRegistry>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwt => jwt.TokenValidationParameters = tokens.ValidationParameters);
builder.Services.AddAuthorization();
builder.Services.AddHealthChecks();
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(GameEndpoints.RegisterRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = options.RegistrationsPerHour, Window = TimeSpan.FromHours(1) }));
});

var app = builder.Build();

await InitializeAsync(app, options);

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapGameEndpoints();
app.Run();

// Creates the schema, publishes the game's own levels as pack 1 on an empty database, and loads every pack into the
// registry that verifies runs.
static async System.Threading.Tasks.Task InitializeAsync(WebApplication app, ServerOptions options)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<GameDb>();
    var levels = scope.ServiceProvider.GetRequiredService<LevelRegistry>();
    var log = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

    await db.Database.EnsureCreatedAsync();

    foreach (var pack in await db.LevelPacks.AsNoTracking().OrderBy(p => p.Version).ToListAsync())
    {
        var document = JsonConvert.DeserializeObject<LevelPackDocument>(pack.Document);
        var validation = levels.Validate(document);
        if (!validation.IsValid) throw new InvalidOperationException($"Stored level pack {pack.Version} no longer validates: {string.Join("; ", validation.Errors)}");
        levels.Add(validation.Levels);
    }

    if (levels.Count == 0)
    {
        var folder = Path.IsPathRooted(options.SeedLevelFolder)
            ? options.SeedLevelFolder
            : Path.Combine(app.Environment.ContentRootPath, options.SeedLevelFolder);
        var seed = LevelRegistry.ReadSeedFolder(folder, 1);
        var validation = levels.Validate(seed);
        if (!validation.IsValid) throw new InvalidOperationException($"Seed levels in {folder} are invalid: {string.Join("; ", validation.Errors)}");
        if (validation.Levels.Count > 0) await GameEndpoints.PublishAsync(db, levels, seed, validation);
    }

    log.LogInformation("Serving {Count} level revisions.", levels.Count);
}

/// <summary>Entry point marker for WebApplicationFactory in the integration tests.</summary>
public partial class Program
{
}
