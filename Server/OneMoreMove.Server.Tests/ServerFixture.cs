using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OneMoreMove.Persistence;

namespace OneMoreMove.Server.Tests
{
    /// <summary>A real server in memory with its own SQLite file and the game's 30 seed levels as pack 1.</summary>
    public sealed class ServerFixture : IDisposable
    {
        public const string AdminKey = "test-admin-key";

        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"omm-server-{Guid.NewGuid():N}.db");

        public ServerFixture()
        {
            Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            {
                host.UseEnvironment("Testing");
                host.UseSetting("Server:SigningKey", "test-signing-key-0123456789abcdef-0123456789");
                host.UseSetting("Server:AdminApiKey", AdminKey);
                host.UseSetting("Server:RegistrationsPerHour", "10000");
                host.UseSetting("Server:Database:ConnectionString", $"Data Source={_databasePath}");
                host.UseSetting("Server:SeedLevelFolder", SeedFolder());
            });
        }

        public WebApplicationFactory<Program> Factory { get; }

        public HttpClient Client() => Factory.CreateClient();

        public async Task<(HttpClient Client, RegisterResponse Account)> SignedInClientAsync()
        {
            var client = Client();
            var account = await (await client.PostAsync("/api/v1/accounts", null)).Content.ReadFromJsonAsync<RegisterResponse>();
            var token = await (await client.PostAsJsonAsync("/api/v1/sessions", new SignInRequest { PlayerId = account.PlayerId, Secret = account.Secret }))
                .Content.ReadFromJsonAsync<TokenResponse>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            return (client, account);
        }

        public void Dispose()
        {
            Factory.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_databasePath)) File.Delete(_databasePath);
        }

        private static string SeedFolder()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "Assets", "_Project", "Content", "LevelSource");
                if (Directory.Exists(candidate)) return candidate;
            }

            throw new DirectoryNotFoundException("Seed levels not found above the test output folder.");
        }
    }
}
