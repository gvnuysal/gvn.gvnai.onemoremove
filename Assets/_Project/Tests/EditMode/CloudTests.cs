using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    public sealed class CloudTests
    {
        private static readonly DateTime Monday = new DateTime(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);

        [Test]
        public void SameRevision_KeepsTheBestOfBothDevices()
        {
            var phone = new ProgressRecord("l05", 1, true, 14, 2, Monday);
            var tablet = new ProgressRecord("l05", 1, false, null, 0, Monday.AddDays(1));

            var merged = ProgressMerger.Merge(phone, tablet);

            Assert.That(merged.Completed, Is.True);
            Assert.That(merged.BestMoves, Is.EqualTo(14));
            Assert.That(merged.BestStars, Is.EqualTo(2));
            Assert.That(merged.LastPlayedUtc, Is.EqualTo(Monday.AddDays(1)));
            Assert.That(ProgressMerger.Merge(tablet, phone).BestMoves, Is.EqualTo(14), "Merging is commutative.");
        }

        [Test]
        public void NewerRevision_WinsBests_ButKeepsTheLevelUnlocked()
        {
            var old = new ProgressRecord("l09", 1, true, 9, 3, Monday);
            var updated = new ProgressRecord("l09", 2, false, null, 0, Monday);

            var merged = ProgressMerger.Merge(old, updated);

            Assert.That(merged.LevelRevision, Is.EqualTo(2));
            Assert.That(merged.BestMoves, Is.Null, "Moves on the old board are not comparable.");
            Assert.That(merged.Completed, Is.True);
        }

        [Test]
        public void Lists_MergeAsAUnion_AndAreIdempotent()
        {
            var a = new[] { new ProgressRecord("l01", 1, true, 2, 3, Monday) };
            var b = new[] { new ProgressRecord("l02", 1, true, 6, 3, Monday) };

            var merged = ProgressMerger.Merge(a, b);
            Assert.That(merged.Select(r => r.LevelId), Is.EqualTo(new[] { "l01", "l02" }));
            Assert.That(ProgressMerger.Merge(merged, merged).Count, Is.EqualTo(2));
        }

        [Test]
        public void MoveHistory_RecoversEveryCommand_IncludingWaits()
        {
            var session = new GameSession(TestLevels.Parse("P.x.G"));
            var played = new[] { Direction.Wait, Direction.Wait, Direction.Right, Direction.Right, Direction.Right, Direction.Right };
            foreach (var d in played) session.TryMove(d);

            Assert.That(session.IsWon, Is.True);
            Assert.That(MoveHistory.Reconstruct(session), Is.EqualTo(played));
        }

        [Test]
        public void MoveHistory_FollowsUndo()
        {
            var session = new GameSession(TestLevels.Parse("P.x.G", "....."));
            session.TryMove(Direction.Down);
            session.Undo();
            session.TryMove(Direction.Right);

            Assert.That(MoveHistory.Reconstruct(session), Is.EqualTo(new[] { Direction.Right }));
        }

        [Test]
        public async Task CloudSync_IsSilentlyOffline_WhenTheServerIsDown()
        {
            var sync = new CloudSync(new FakeRemote { Down = true }, new MemoryAccounts());

            Assert.That(await sync.SyncProgressAsync(Array.Empty<ProgressRecord>()), Is.Null);
            Assert.That(sync.Status, Is.EqualTo(CloudStatus.Offline));
        }

        [Test]
        public async Task CloudSync_RegistersOnce_AndSignsInAgainWhenTheTokenExpires()
        {
            var remote = new FakeRemote();
            var accounts = new MemoryAccounts();
            var sync = new CloudSync(remote, accounts);

            await sync.SyncProgressAsync(Array.Empty<ProgressRecord>());
            remote.ExpireTokens();
            var result = await sync.SyncProgressAsync(Array.Empty<ProgressRecord>());

            Assert.That(result, Is.Not.Null);
            Assert.That(sync.Status, Is.EqualTo(CloudStatus.Online));
            Assert.That(remote.Registrations, Is.EqualTo(1));
            Assert.That(remote.SignIns, Is.EqualTo(2));
            Assert.That(accounts.Load(), Is.Not.Null);
        }

        [Test]
        public async Task CloudSync_DoesNotSubmitUnfinishedRuns()
        {
            var remote = new FakeRemote();
            var session = new GameSession(TestLevels.Parse("P.x.G"));
            session.TryMove(Direction.Right);

            Assert.That(await new CloudSync(remote, new MemoryAccounts()).SubmitRunAsync(session), Is.Null);
            Assert.That(remote.Runs, Is.EqualTo(0));
        }

        private sealed class MemoryAccounts : IAccountStore
        {
            private PlayerAccount _account;
            public PlayerAccount Load() => _account;
            public void Save(PlayerAccount account) => _account = account;
        }

        private sealed class FakeRemote : IRemoteService
        {
            private int _tokenGeneration;

            public bool Down;
            public int Registrations;
            public int SignIns;
            public int Runs;

            public void ExpireTokens() => _tokenGeneration++;

            public Task<PlayerAccount> RegisterAsync(CancellationToken cancellationToken)
            {
                ThrowIfDown();
                Registrations++;
                return Task.FromResult(new PlayerAccount("p1", "secret", "Oyuncu-1"));
            }

            public Task<string> SignInAsync(PlayerAccount account, CancellationToken cancellationToken)
            {
                ThrowIfDown();
                SignIns++;
                return Task.FromResult("token-" + _tokenGeneration);
            }

            public Task<IReadOnlyList<ProgressRecord>> SyncProgressAsync(string token, IReadOnlyList<ProgressRecord> local, CancellationToken cancellationToken)
            {
                CheckToken(token);
                return Task.FromResult<IReadOnlyList<ProgressRecord>>(local.ToArray());
            }

            public Task<RunResult> SubmitRunAsync(string token, string levelId, int revision, IReadOnlyList<Direction> moves, CancellationToken cancellationToken)
            {
                CheckToken(token);
                Runs++;
                return Task.FromResult(new RunResult(true, moves.Count, moves.Count, 1, 1, null));
            }

            public Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string levelId, int revision, int top, CancellationToken cancellationToken) =>
                Task.FromResult<IReadOnlyList<LeaderboardEntry>>(Array.Empty<LeaderboardEntry>());

            private void CheckToken(string token)
            {
                ThrowIfDown();
                if (token != "token-" + _tokenGeneration) throw new RemoteAuthException("expired");
            }

            private void ThrowIfDown()
            {
                if (Down) throw new System.IO.IOException("connection refused");
            }
        }
    }
}
