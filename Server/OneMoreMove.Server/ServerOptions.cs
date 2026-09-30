using System;

namespace OneMoreMove.Server
{
    /// <summary>Configuration section "Server" (appsettings.json, environment variables such as Server__SigningKey).</summary>
    public sealed class ServerOptions
    {
        public DatabaseOptions Database { get; set; } = new DatabaseOptions();

        /// <summary>HMAC key for access tokens, at least 32 characters. Required outside Development.</summary>
        public string SigningKey { get; set; }

        public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromDays(30);

        /// <summary>Key for publishing level packs (X-Admin-Key header). Publishing is disabled when empty.</summary>
        public string AdminApiKey { get; set; }

        /// <summary>Folder with the game's seed level JSON files; published as pack 1 when the database has no packs.</summary>
        public string SeedLevelFolder { get; set; } = "levels";

        /// <summary>Anonymous accounts one IP address may create per hour.</summary>
        public int RegistrationsPerHour { get; set; } = 20;

        public int MaxRunMoves { get; set; } = 2000;
        public int MaxProgressRecords { get; set; } = 1000;
    }

    public sealed class DatabaseOptions
    {
        /// <summary>"Sqlite" (development, tests) or "Postgres" (production).</summary>
        public string Provider { get; set; } = "Sqlite";

        public string ConnectionString { get; set; } = "Data Source=onemoremove.db";
    }
}
