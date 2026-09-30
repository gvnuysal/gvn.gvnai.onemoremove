using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    public sealed class CompletionOutcome
    {
        public CompletionOutcome(string levelId, int moves, int stars, int bestMoves, int bestStars, bool isNewBestMoves, bool isFirstCompletion)
        {
            LevelId = levelId;
            Moves = moves;
            Stars = stars;
            BestMoves = bestMoves;
            BestStars = bestStars;
            IsNewBestMoves = isNewBestMoves;
            IsFirstCompletion = isFirstCompletion;
        }

        public string LevelId { get; }
        public int Moves { get; }
        public int Stars { get; }
        public int BestMoves { get; }
        public int BestStars { get; }
        public bool IsNewBestMoves { get; }
        public bool IsFirstCompletion { get; }
    }

    public sealed class ProgressService
    {
        private readonly Dictionary<string, ProgressRecord> _records = new Dictionary<string, ProgressRecord>(StringComparer.Ordinal);

        public ProgressService(IEnumerable<ProgressRecord> records = null)
        {
            if (records == null) return;
            foreach (var record in records.Where(r => r != null && !string.IsNullOrEmpty(r.LevelId)))
            {
                _records[record.LevelId] = record;
            }
        }

        public ProgressRecord Get(string levelId) => levelId != null && _records.TryGetValue(levelId, out var record) ? record : null;

        public bool IsCompleted(string levelId) => Get(levelId)?.Completed == true;

        /// <summary>The first level is always open; every other level opens when the previous one is completed.</summary>
        public bool IsUnlocked(LevelLibrary library, int index)
        {
            if (index < 0 || index >= library.Count) return false;
            return index == 0 || IsCompleted(library.Levels[index].Id) || IsCompleted(library.Levels[index - 1].Id);
        }

        public CompletionOutcome RecordCompletion(LevelDefinition level, int moves, bool hintUsed, DateTime utcNow)
        {
            var stars = StarRating.Calculate(level, moves, hintUsed);
            var existing = Get(level.Id);

            // Old best moves from a different revision are not comparable with the updated level.
            var comparableBest = existing != null && existing.LevelRevision == level.Revision ? existing.BestMoves : null;
            var isNewBestMoves = !comparableBest.HasValue || moves < comparableBest.Value;
            var bestMoves = isNewBestMoves ? moves : comparableBest.Value;
            var bestStars = Math.Max(stars, existing?.BestStars ?? 0);

            _records[level.Id] = new ProgressRecord(level.Id, level.Revision, true, bestMoves, bestStars, utcNow);
            return new CompletionOutcome(level.Id, moves, stars, bestMoves, bestStars, isNewBestMoves, existing?.Completed != true);
        }

        public void MarkPlayed(LevelDefinition level, DateTime utcNow)
        {
            var existing = Get(level.Id);
            _records[level.Id] = existing == null
                ? new ProgressRecord(level.Id, level.Revision, false, null, 0, utcNow)
                : new ProgressRecord(existing.LevelId, existing.LevelRevision, existing.Completed, existing.BestMoves, existing.BestStars, utcNow);
        }

        public IReadOnlyList<ProgressRecord> ToList() => _records.Values.OrderBy(r => r.LevelId, StringComparer.Ordinal).ToArray();
    }
}
