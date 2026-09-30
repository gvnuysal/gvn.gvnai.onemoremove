using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>An anonymous device account. The secret never leaves the device except to sign in.</summary>
    public sealed class PlayerAccount
    {
        public PlayerAccount(string playerId, string secret, string displayName)
        {
            PlayerId = playerId;
            Secret = secret;
            DisplayName = displayName;
        }

        public string PlayerId { get; }
        public string Secret { get; }
        public string DisplayName { get; }
    }

    public sealed class RunResult
    {
        public RunResult(bool accepted, int moves, int bestMoves, int rank, int players, string error)
        {
            Accepted = accepted;
            Moves = moves;
            BestMoves = bestMoves;
            Rank = rank;
            Players = players;
            Error = error;
        }

        public bool Accepted { get; }
        public int Moves { get; }

        /// <summary>This player's best verified result on the level revision.</summary>
        public int BestMoves { get; }

        /// <summary>1-based rank of <see cref="BestMoves"/> among <see cref="Players"/>.</summary>
        public int Rank { get; }

        public int Players { get; }
        public string Error { get; }
    }

    public sealed class LeaderboardEntry
    {
        public LeaderboardEntry(int rank, string displayName, int moves, DateTime achievedUtc)
        {
            Rank = rank;
            DisplayName = displayName;
            Moves = moves;
            AchievedUtc = achievedUtc;
        }

        public int Rank { get; }
        public string DisplayName { get; }
        public int Moves { get; }
        public DateTime AchievedUtc { get; }
    }

    /// <summary>Thrown by <see cref="IRemoteService"/> when the server rejects the credentials.</summary>
    public sealed class RemoteAuthException : Exception
    {
        public RemoteAuthException(string message) : base(message)
        {
        }
    }

    /// <summary>The game server's API as the game sees it. Transport errors surface as exceptions.</summary>
    public interface IRemoteService
    {
        Task<PlayerAccount> RegisterAsync(CancellationToken cancellationToken);
        Task<string> SignInAsync(PlayerAccount account, CancellationToken cancellationToken);
        Task<IReadOnlyList<ProgressRecord>> SyncProgressAsync(string token, IReadOnlyList<ProgressRecord> local, CancellationToken cancellationToken);
        Task<RunResult> SubmitRunAsync(string token, string levelId, int revision, IReadOnlyList<Direction> moves, CancellationToken cancellationToken);
        Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string levelId, int revision, int top, CancellationToken cancellationToken);
    }

    public interface IAccountStore
    {
        /// <summary>The stored account, or null before the first registration.</summary>
        PlayerAccount Load();

        void Save(PlayerAccount account);
    }

    public enum CloudStatus
    {
        Idle,
        Online,
        Offline
    }

    /// <summary>
    /// Offline-first cloud features: the game never waits for the network and never fails because of it. Every call
    /// returns null when the server cannot be reached, and <see cref="Status"/> tells the UI what happened.
    /// </summary>
    public sealed class CloudSync
    {
        private readonly IRemoteService _remote;
        private readonly IAccountStore _accounts;
        private string _token;

        public CloudSync(IRemoteService remote, IAccountStore accounts)
        {
            _remote = remote ?? throw new ArgumentNullException(nameof(remote));
            _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        }

        public CloudStatus Status { get; private set; } = CloudStatus.Idle;
        public string LastError { get; private set; }
        public PlayerAccount Account { get; private set; }

        /// <summary>Uploads local progress and returns the merged progress of every device, or null when offline.</summary>
        public Task<IReadOnlyList<ProgressRecord>> SyncProgressAsync(IReadOnlyList<ProgressRecord> local, CancellationToken cancellationToken = default) =>
            WithTokenAsync(token => _remote.SyncProgressAsync(token, local, cancellationToken), cancellationToken);

        /// <summary>Submits a won session for server-side verification; null when offline or the history is incomplete.</summary>
        public Task<RunResult> SubmitRunAsync(GameSession wonSession, CancellationToken cancellationToken = default)
        {
            if (wonSession == null || !wonSession.IsWon) return Task.FromResult<RunResult>(null);
            var moves = MoveHistory.Reconstruct(wonSession);
            if (moves == null) return Task.FromResult<RunResult>(null);

            return WithTokenAsync(token => _remote.SubmitRunAsync(token, wonSession.LevelId, wonSession.LevelRevision, moves, cancellationToken),
                cancellationToken);
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(string levelId, int revision, int top = 10,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var entries = await _remote.GetLeaderboardAsync(levelId, revision, top, cancellationToken);
                MarkOnline();
                return entries;
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                MarkOffline(e);
                return null;
            }
        }

        private async Task<T> WithTokenAsync<T>(Func<string, Task<T>> call, CancellationToken cancellationToken) where T : class
        {
            try
            {
                var token = await EnsureTokenAsync(cancellationToken);
                try
                {
                    var result = await call(token);
                    MarkOnline();
                    return result;
                }
                catch (RemoteAuthException)
                {
                    // Expired token: sign in again once.
                    _token = null;
                    var result = await call(await EnsureTokenAsync(cancellationToken));
                    MarkOnline();
                    return result;
                }
            }
            catch (Exception e) when (!(e is OperationCanceledException))
            {
                MarkOffline(e);
                return null;
            }
        }

        private async Task<string> EnsureTokenAsync(CancellationToken cancellationToken)
        {
            if (_token != null) return _token;

            Account = Account ?? _accounts.Load();
            if (Account == null)
            {
                Account = await _remote.RegisterAsync(cancellationToken);
                _accounts.Save(Account);
            }

            _token = await _remote.SignInAsync(Account, cancellationToken);
            return _token;
        }

        private void MarkOnline()
        {
            Status = CloudStatus.Online;
            LastError = null;
        }

        private void MarkOffline(Exception e)
        {
            Status = CloudStatus.Offline;
            LastError = e.Message;
        }
    }
}
