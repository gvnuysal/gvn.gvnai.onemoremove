using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Persistence;
using OneMoreMove.Session;

namespace OneMoreMove.Server.Tests
{
    public sealed class ServerTests
    {
        private ServerFixture _server;

        [SetUp]
        public void SetUp() => _server = new ServerFixture();

        [TearDown]
        public void TearDown() => _server.Dispose();

        [Test]
        public async Task Accounts_SignInWithTheirSecretOnly()
        {
            var client = _server.Client();
            var register = await client.PostAsync("/api/v1/accounts", null);
            Assert.That(register.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            var account = await register.Content.ReadFromJsonAsync<RegisterResponse>();
            Assert.That(account.DisplayName, Does.StartWith("Oyuncu-"));

            var ok = await client.PostAsJsonAsync("/api/v1/sessions", new SignInRequest { PlayerId = account.PlayerId, Secret = account.Secret });
            Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That((await ok.Content.ReadFromJsonAsync<TokenResponse>()).Token, Is.Not.Empty);

            var wrong = await client.PostAsJsonAsync("/api/v1/sessions", new SignInRequest { PlayerId = account.PlayerId, Secret = "guess" });
            Assert.That(wrong.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        }

        [Test]
        public async Task Progress_RequiresSignIn_AndMergesDevices()
        {
            Assert.That((await _server.Client().GetAsync("/api/v1/progress")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            var (client, _) = await _server.SignedInClientAsync();
            var phone = new ProgressSyncDocument { Records = { Record("l01_first_step", 1, true, 3, 2), Record("l02_walls", 1, true, 9, 1) } };
            var tablet = new ProgressSyncDocument { Records = { Record("l01_first_step", 1, true, 2, 3), Record("l03_detour", 1, false, null, 0) } };

            await client.PutAsJsonAsync("/api/v1/progress", phone);
            var merged = await (await client.PutAsJsonAsync("/api/v1/progress", tablet)).Content.ReadFromJsonAsync<ProgressSyncDocument>();

            var first = merged.Records.Single(r => r.LevelId == "l01_first_step");
            Assert.That(first.BestMoves, Is.EqualTo(2));
            Assert.That(first.BestStars, Is.EqualTo(3));
            Assert.That(merged.Records.Select(r => r.LevelId), Is.EquivalentTo(new[] { "l01_first_step", "l02_walls", "l03_detour" }));

            var stored = await client.GetFromJsonAsync<ProgressSyncDocument>("/api/v1/progress");
            Assert.That(stored.Records.Count, Is.EqualTo(3));
        }

        [Test]
        public async Task Progress_RejectsNonsense()
        {
            var (client, _) = await _server.SignedInClientAsync();
            var bad = new ProgressSyncDocument { Records = { Record("l01_first_step", 1, true, 3, 9) } };
            Assert.That((await client.PutAsJsonAsync("/api/v1/progress", bad)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task Runs_AreReplayedAndRanked()
        {
            var (fast, _) = await _server.SignedInClientAsync();
            var (slow, _) = await _server.SignedInClientAsync();

            var best = await Submit(fast, "tutorial_gate_01", 1, "Right", "Right", "Right", "Right");
            Assert.That(best.Accepted, Is.True);
            Assert.That(best.Moves, Is.EqualTo(4));
            Assert.That(best.Rank, Is.EqualTo(1));

            // Two waits bring the gate back to its start phase, so this also reaches the goal, two moves later.
            var longer = await Submit(slow, "tutorial_gate_01", 1, "Wait", "Wait", "Right", "Right", "Right", "Right");
            Assert.That(longer.Accepted, Is.True);
            Assert.That(longer.Rank, Is.EqualTo(2));
            Assert.That(longer.Players, Is.EqualTo(2));

            var board = await _server.Client().GetFromJsonAsync<LeaderboardResponse>("/api/v1/levels/tutorial_gate_01/leaderboard");
            Assert.That(board.Entries.Select(e => e.Moves), Is.EqualTo(new[] { 4, 6 }));
        }

        [Test]
        public async Task Runs_ThatBreakTheRules_AreRejected()
        {
            var (client, _) = await _server.SignedInClientAsync();

            var illegal = await client.PostAsJsonAsync("/api/v1/levels/tutorial_gate_01/runs", Run(1, "Left"));
            Assert.That((int)illegal.StatusCode, Is.EqualTo(422));
            Assert.That((await illegal.Content.ReadFromJsonAsync<RunResponse>()).Error, Does.Contain("Move 1"));

            var unfinished = await client.PostAsJsonAsync("/api/v1/levels/tutorial_gate_01/runs", Run(1, "Right"));
            Assert.That((int)unfinished.StatusCode, Is.EqualTo(422));

            var unknown = await client.PostAsJsonAsync("/api/v1/levels/no_such_level/runs", Run(1, "Right"));
            Assert.That(unknown.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

            var garbage = await client.PostAsJsonAsync("/api/v1/levels/tutorial_gate_01/runs", Run(1, "Jump"));
            Assert.That(garbage.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        }

        [Test]
        public async Task SeedLevels_ArePublishedAsPackOne()
        {
            var packs = await _server.Client().GetFromJsonAsync<List<LevelPackSummary>>("/api/v1/level-packs");
            Assert.That(packs.Single().Version, Is.EqualTo(1));
            Assert.That(packs.Single().LevelCount, Is.EqualTo(30));

            var pack = await _server.Client().GetFromJsonAsync<LevelPackDocument>("/api/v1/level-packs/1");
            Assert.That(pack.Levels.Count, Is.EqualTo(30));
            Assert.That(LevelJson.Parse(pack.Levels[0]).OptimalMoves, Is.Not.Null, "Published levels carry their proven optimum.");
        }

        [Test]
        public async Task LevelPacks_ArePublishedOnlyByAdmins_AndOnlyWhenProven()
        {
            var client = _server.Client();
            var level = "{ \"id\": \"extra_01\", \"name\": \"Ek\", \"revision\": 1, \"parMoves\": 4, \"map\": [\"PxG\"] }";
            var pack = new LevelPackDocument { Levels = { level } };

            Assert.That((await client.PostAsJsonAsync("/api/v1/level-packs", pack)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

            client.DefaultRequestHeaders.Add("X-Admin-Key", ServerFixture.AdminKey);
            var unsolvable = new LevelPackDocument { Levels = { "{ \"id\": \"broken\", \"revision\": 1, \"parMoves\": 3, \"map\": [\"P#G\"] }" } };
            var rejected = await client.PostAsJsonAsync("/api/v1/level-packs", unsolvable);
            Assert.That((int)rejected.StatusCode, Is.EqualTo(422));
            Assert.That((await rejected.Content.ReadFromJsonAsync<ErrorResponse>()).Details.Single(), Does.Contain("broken"));

            var published = await client.PostAsJsonAsync("/api/v1/level-packs", pack);
            Assert.That(published.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That((await published.Content.ReadFromJsonAsync<LevelPackSummary>()).Version, Is.EqualTo(2));

            // The new level is immediately verifiable.
            var (player, _) = await _server.SignedInClientAsync();
            var run = await Submit(player, "extra_01", 1, "Wait", "Right", "Right");
            Assert.That(run.Accepted, Is.True);

            // A published revision cannot silently change its board.
            var changed = new LevelPackDocument { Levels = { "{ \"id\": \"extra_01\", \"revision\": 1, \"parMoves\": 4, \"map\": [\"P.G\"] }" } };
            Assert.That((int)(await client.PostAsJsonAsync("/api/v1/level-packs", changed)).StatusCode, Is.EqualTo(422));
        }

        [Test]
        public async Task GameClient_SyncsAndSubmitsThroughTheRealApi()
        {
            var sync = new CloudSync(new HttpRemoteService(_server.Client()), new MemoryAccounts());

            var local = new[] { new ProgressRecord("l01_first_step", 1, true, 2, 3, DateTime.UtcNow) };
            var merged = await sync.SyncProgressAsync(local);
            Assert.That(sync.Status, Is.EqualTo(CloudStatus.Online), sync.LastError);
            Assert.That(merged.Single().BestMoves, Is.EqualTo(2));

            var level = AsciiLevelParser.Parse(new AsciiLevelParser.Options { Id = "tutorial_gate_01", ParMoves = 5 }, new[] { "P.x.G" });
            var session = new GameSession(level);
            foreach (var d in new[] { Direction.Wait, Direction.Wait, Direction.Right, Direction.Right, Direction.Right, Direction.Right })
                session.TryMove(d);

            var run = await sync.SubmitRunAsync(session);
            Assert.That(run.Accepted, Is.True, run.Error);
            Assert.That(run.Moves, Is.EqualTo(6));

            var board = await sync.GetLeaderboardAsync("tutorial_gate_01", 1);
            Assert.That(board.Single().DisplayName, Is.EqualTo(sync.Account.DisplayName));
        }

        [Test]
        public async Task GameClient_StaysUsableOffline()
        {
            var offline = new CloudSync(HttpRemoteService.Create("http://127.0.0.1:9", TimeSpan.FromSeconds(2)), new MemoryAccounts());
            var result = await offline.SyncProgressAsync(Array.Empty<ProgressRecord>());
            Assert.That(result, Is.Null);
            Assert.That(offline.Status, Is.EqualTo(CloudStatus.Offline));
        }

        private static CloudProgressDto Record(string id, int revision, bool completed, int? moves, int stars) => new CloudProgressDto
        {
            LevelId = id, Revision = revision, Completed = completed, BestMoves = moves, BestStars = stars, LastPlayedUtc = DateTime.UtcNow
        };

        private static RunRequest Run(int revision, params string[] moves) => new RunRequest { Revision = revision, Moves = moves.ToList() };

        private static async Task<RunResponse> Submit(System.Net.Http.HttpClient client, string levelId, int revision, params string[] moves)
        {
            var response = await client.PostAsJsonAsync($"/api/v1/levels/{levelId}/runs", Run(revision, moves));
            return await response.Content.ReadFromJsonAsync<RunResponse>();
        }

        private sealed class MemoryAccounts : IAccountStore
        {
            private PlayerAccount _account;
            public PlayerAccount Load() => _account;
            public void Save(PlayerAccount account) => _account = account;
        }
    }
}
