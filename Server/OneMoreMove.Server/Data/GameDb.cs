using System;
using Microsoft.EntityFrameworkCore;

namespace OneMoreMove.Server.Data
{
    public sealed class Player
    {
        public Guid Id { get; set; }
        public byte[] SecretSalt { get; set; }
        public byte[] SecretHash { get; set; }
        public string DisplayName { get; set; }
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>Synced progress, one row per player and level (the merged best of every device).</summary>
    public sealed class ProgressRow
    {
        public Guid PlayerId { get; set; }
        public string LevelId { get; set; }
        public int Revision { get; set; }
        public bool Completed { get; set; }
        public int? BestMoves { get; set; }
        public int BestStars { get; set; }
        public DateTime LastPlayedUtc { get; set; }
    }

    /// <summary>A player's best verified run on one level revision; the leaderboard reads these.</summary>
    public sealed class BestRun
    {
        public Guid PlayerId { get; set; }
        public string LevelId { get; set; }
        public int Revision { get; set; }
        public int Moves { get; set; }

        /// <summary>The replayed commands, kept for audits ("Right Wait Up ...").</summary>
        public string Commands { get; set; }

        public DateTime AchievedUtc { get; set; }
    }

    public sealed class LevelPack
    {
        public int Version { get; set; }
        public string Sha256 { get; set; }
        public DateTime PublishedUtc { get; set; }

        /// <summary>The pack as published (LevelPackDocument JSON).</summary>
        public string Document { get; set; }
    }

    public sealed class GameDb : DbContext
    {
        public GameDb(DbContextOptions<GameDb> options) : base(options)
        {
        }

        public DbSet<Player> Players => Set<Player>();
        public DbSet<ProgressRow> Progress => Set<ProgressRow>();
        public DbSet<BestRun> BestRuns => Set<BestRun>();
        public DbSet<LevelPack> LevelPacks => Set<LevelPack>();

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<Player>(e =>
            {
                e.HasKey(p => p.Id);
                e.Property(p => p.DisplayName).HasMaxLength(32).IsRequired();
            });

            model.Entity<ProgressRow>(e =>
            {
                e.HasKey(p => new { p.PlayerId, p.LevelId });
                e.Property(p => p.LevelId).HasMaxLength(64);
            });

            model.Entity<BestRun>(e =>
            {
                e.HasKey(r => new { r.PlayerId, r.LevelId, r.Revision });
                e.Property(r => r.LevelId).HasMaxLength(64);
                e.HasIndex(r => new { r.LevelId, r.Revision, r.Moves, r.AchievedUtc });
            });

            model.Entity<LevelPack>(e =>
            {
                e.HasKey(p => p.Version);
                e.Property(p => p.Version).ValueGeneratedNever();
            });
        }
    }
}
