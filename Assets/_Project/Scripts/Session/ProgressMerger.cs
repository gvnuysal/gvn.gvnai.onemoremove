using System;
using System.Collections.Generic;
using System.Linq;

namespace OneMoreMove.Session
{
    /// <summary>
    /// Merges progress from two devices (or a device and the cloud). Commutative and idempotent, so the order of syncs
    /// never matters and nothing earned is lost. Shared by the game and the server.
    /// </summary>
    public static class ProgressMerger
    {
        public static IReadOnlyList<ProgressRecord> Merge(IEnumerable<ProgressRecord> a, IEnumerable<ProgressRecord> b)
        {
            var merged = new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);
            foreach (var record in (a ?? Enumerable.Empty<ProgressRecord>()).Concat(b ?? Enumerable.Empty<ProgressRecord>()))
            {
                if (record == null || string.IsNullOrEmpty(record.LevelId)) continue;
                merged[record.LevelId] = merged.TryGetValue(record.LevelId, out var existing) ? Merge(existing, record) : record;
            }

            return merged.Values.OrderBy(r => r.LevelId, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Same revision: best of both (completed if either is, fewest moves, most stars). Different revisions: the newer
        /// revision's bests win, because moves on an old board are not comparable, but completion is kept so a level
        /// stays unlocked.
        /// </summary>
        public static ProgressRecord Merge(ProgressRecord a, ProgressRecord b)
        {
            if (a == null) return b;
            if (b == null) return a;
            if (!string.Equals(a.LevelId, b.LevelId, StringComparison.Ordinal)) throw new ArgumentException("Records belong to different levels.");

            var lastPlayed = a.LastPlayedUtc >= b.LastPlayedUtc ? a.LastPlayedUtc : b.LastPlayedUtc;
            if (a.LevelRevision != b.LevelRevision)
            {
                var newer = a.LevelRevision > b.LevelRevision ? a : b;
                var older = ReferenceEquals(newer, a) ? b : a;
                return new ProgressRecord(newer.LevelId, newer.LevelRevision, newer.Completed || older.Completed, newer.BestMoves,
                    newer.BestStars, lastPlayed);
            }

            int? bestMoves = a.BestMoves.HasValue && b.BestMoves.HasValue
                ? Math.Min(a.BestMoves.Value, b.BestMoves.Value)
                : a.BestMoves ?? b.BestMoves;
            return new ProgressRecord(a.LevelId, a.LevelRevision, a.Completed || b.Completed, bestMoves, Math.Max(a.BestStars, b.BestStars),
                lastPlayed);
        }
    }
}
