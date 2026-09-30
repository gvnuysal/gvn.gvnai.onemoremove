using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Persistence
{
    /// <summary><see cref="IRemoteService"/> over HTTPS with <see cref="HttpClient"/> (works in the editor, players and IL2CPP).</summary>
    public sealed class HttpRemoteService : IRemoteService
    {
        private static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore
        };

        private readonly HttpClient _http;

        /// <param name="http">A client whose <see cref="HttpClient.BaseAddress"/> is the server root.</param>
        public HttpRemoteService(HttpClient http)
        {
            _http = http ?? throw new ArgumentNullException(nameof(http));
            if (_http.BaseAddress == null) throw new ArgumentException("HttpClient.BaseAddress is required.", nameof(http));
        }

        public static HttpRemoteService Create(string serverUrl, TimeSpan? timeout = null) =>
            new HttpRemoteService(new HttpClient { BaseAddress = new Uri(serverUrl), Timeout = timeout ?? TimeSpan.FromSeconds(10) });

        public async Task<PlayerAccount> RegisterAsync(CancellationToken cancellationToken)
        {
            var response = await SendAsync<RegisterResponse>(HttpMethod.Post, "api/v1/accounts", null, null, cancellationToken);
            return new PlayerAccount(response.PlayerId, response.Secret, response.DisplayName);
        }

        public async Task<string> SignInAsync(PlayerAccount account, CancellationToken cancellationToken)
        {
            var body = new SignInRequest { PlayerId = account.PlayerId, Secret = account.Secret };
            var response = await SendAsync<TokenResponse>(HttpMethod.Post, "api/v1/sessions", body, null, cancellationToken);
            return response.Token;
        }

        public async Task<IReadOnlyList<ProgressRecord>> SyncProgressAsync(string token, IReadOnlyList<ProgressRecord> local, CancellationToken cancellationToken)
        {
            var body = new ProgressSyncDocument { Records = (local ?? Array.Empty<ProgressRecord>()).Select(ToDto).ToList() };
            var response = await SendAsync<ProgressSyncDocument>(HttpMethod.Put, "api/v1/progress", body, token, cancellationToken);
            return response.Records.Select(FromDto).ToArray();
        }

        public async Task<RunResult> SubmitRunAsync(string token, string levelId, int revision, IReadOnlyList<Direction> moves, CancellationToken cancellationToken)
        {
            var body = new RunRequest { Revision = revision, Moves = moves.Select(m => m.ToString()).ToList() };
            var response = await SendAsync<RunResponse>(HttpMethod.Post, $"api/v1/levels/{Uri.EscapeDataString(levelId)}/runs", body, token,
                cancellationToken, acceptUnprocessable: true);
            return new RunResult(response.Accepted, response.Moves, response.BestMoves, response.Rank, response.Players, response.Error);
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string levelId, int revision, int top, CancellationToken cancellationToken)
        {
            var path = $"api/v1/levels/{Uri.EscapeDataString(levelId)}/leaderboard?revision={revision}&top={top}";
            var response = await SendAsync<LeaderboardResponse>(HttpMethod.Get, path, null, null, cancellationToken);
            return response.Entries.Select(e => new LeaderboardEntry(e.Rank, e.DisplayName, e.Moves, e.AchievedUtc)).ToArray();
        }

        public static CloudProgressDto ToDto(ProgressRecord r) => new CloudProgressDto
        {
            LevelId = r.LevelId,
            Revision = r.LevelRevision,
            Completed = r.Completed,
            BestMoves = r.BestMoves,
            BestStars = r.BestStars,
            LastPlayedUtc = r.LastPlayedUtc
        };

        public static ProgressRecord FromDto(CloudProgressDto d) =>
            new ProgressRecord(d.LevelId, d.Revision, d.Completed, d.BestMoves, d.BestStars, DateTime.SpecifyKind(d.LastPlayedUtc, DateTimeKind.Utc));

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object body, string token, CancellationToken cancellationToken,
            bool acceptUnprocessable = false)
        {
            using (var request = new HttpRequestMessage(method, path))
            {
                if (token != null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body != null) request.Content = new StringContent(JsonConvert.SerializeObject(body, Json), Encoding.UTF8, "application/json");

                using (var response = await _http.SendAsync(request, cancellationToken))
                {
                    var text = response.Content != null ? await response.Content.ReadAsStringAsync() : string.Empty;
                    if (response.StatusCode == HttpStatusCode.Unauthorized) throw new RemoteAuthException("The server rejected the credentials.");
                    var unprocessable = (int)response.StatusCode == 422 && acceptUnprocessable;
                    if (!response.IsSuccessStatusCode && !unprocessable)
                    {
                        throw new IOException($"{method} {path} failed: {(int)response.StatusCode} {response.ReasonPhrase} {text}");
                    }

                    return JsonConvert.DeserializeObject<T>(text, Json) ?? throw new IOException($"{method} {path} returned no body.");
                }
            }
        }
    }

    /// <summary>Keeps the anonymous account next to the save file (never inside it, so a shared save leaks no secret).</summary>
    public sealed class FileAccountStore : IAccountStore
    {
        private readonly string _path;

        public FileAccountStore(string directory, string fileName = "account.json")
        {
            _path = Path.Combine(directory, fileName);
        }

        public PlayerAccount Load()
        {
            try
            {
                if (!File.Exists(_path)) return null;
                var dto = JsonConvert.DeserializeObject<RegisterResponse>(File.ReadAllText(_path));
                return string.IsNullOrEmpty(dto?.PlayerId) || string.IsNullOrEmpty(dto.Secret) ? null : new PlayerAccount(dto.PlayerId, dto.Secret, dto.DisplayName);
            }
            catch (Exception e) when (e is IOException || e is JsonException)
            {
                return null;
            }
        }

        public void Save(PlayerAccount account)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path) ?? ".");
            var dto = new RegisterResponse { PlayerId = account.PlayerId, Secret = account.Secret, DisplayName = account.DisplayName };
            File.WriteAllText(_path, JsonConvert.SerializeObject(dto));
        }
    }
}
