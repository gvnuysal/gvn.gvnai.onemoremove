using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using OneMoreMove.Persistence;

namespace OneMoreMove.Server.Levels
{
    /// <summary>
    /// Every level the server knows, keyed by id and revision, built from the published level packs. A run is only
    /// ever verified against the exact revision the player played.
    /// </summary>
    public sealed class LevelRegistry
    {
        private readonly ConcurrentDictionary<(string Id, int Revision), LevelDefinition> _levels =
            new ConcurrentDictionary<(string, int), LevelDefinition>();

        public int Count => _levels.Count;

        public bool TryGet(string id, int revision, out LevelDefinition level) => _levels.TryGetValue((id, revision), out level);

        public int? LatestRevision(string id)
        {
            var revisions = _levels.Keys.Where(k => k.Id == id).Select(k => k.Revision).ToArray();
            return revisions.Length == 0 ? (int?)null : revisions.Max();
        }

        public void Add(IEnumerable<LevelDefinition> levels)
        {
            foreach (var level in levels) _levels[(level.Id, level.Revision)] = level;
        }

        /// <summary>
        /// Parses and proves every level of a pack the way the game's release gate does: structure, a completed shortest
        /// path search, and a replay of that path. A published (id, revision) may never change its board.
        /// </summary>
        public PackValidation Validate(LevelPackDocument pack)
        {
            var errors = new List<string>();
            var levels = new List<LevelDefinition>();
            if (pack?.Levels == null || pack.Levels.Count == 0)
            {
                errors.Add("The pack has no levels.");
                return new PackValidation(levels, errors);
            }

            var seen = new HashSet<(string, int)>();
            for (var i = 0; i < pack.Levels.Count; i++)
            {
                LevelDefinition level;
                try
                {
                    level = LevelJson.Parse(pack.Levels[i]);
                }
                catch (Exception e) when (e is FormatException || e is JsonException || e is ArgumentException)
                {
                    errors.Add($"Level {i + 1}: {e.Message}");
                    continue;
                }

                var name = $"{level.Id} r{level.Revision}";
                if (!seen.Add((level.Id, level.Revision)))
                {
                    errors.Add($"{name}: duplicate in the pack.");
                    continue;
                }

                var report = LevelValidator.Validate(level);
                if (!report.IsValid)
                {
                    errors.Add($"{name}: {report}");
                    continue;
                }

                var solved = BfsSolver.Solve(level, SolverBudget.Exhaustive);
                if (solved.Status != SolverStatus.Solved)
                {
                    errors.Add($"{name}: {solved}");
                    continue;
                }

                var proven = level.WithSolution(solved.Path.Count, solved.Path);
                if (!SolutionReplayer.Replay(proven, proven.KnownSolution).ReachedGoal)
                {
                    errors.Add($"{name}: the solver path does not replay.");
                    continue;
                }

                if (TryGet(level.Id, level.Revision, out var published) && Board(published) != Board(proven))
                {
                    errors.Add($"{name}: this revision is already published with a different board; increase the revision.");
                    continue;
                }

                levels.Add(proven);
            }

            return new PackValidation(levels, errors);
        }

        /// <summary>The game's seed files as a pack document (pack 1 on an empty database).</summary>
        public static LevelPackDocument ReadSeedFolder(string folder, int version)
        {
            var files = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json").OrderBy(p => p, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();
            return new LevelPackDocument { Version = version, Levels = files.Select(File.ReadAllText).ToList() };
        }

        public static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private static string Board(LevelDefinition level) => string.Join("\n", AsciiLevelParser.Render(level));
    }

    public sealed class PackValidation
    {
        public PackValidation(IReadOnlyList<LevelDefinition> levels, IReadOnlyList<string> errors)
        {
            Levels = levels;
            Errors = errors;
        }

        public IReadOnlyList<LevelDefinition> Levels { get; }
        public IReadOnlyList<string> Errors { get; }
        public bool IsValid => Errors.Count == 0;
    }
}
