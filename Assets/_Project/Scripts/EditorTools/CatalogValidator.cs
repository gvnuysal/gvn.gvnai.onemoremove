using System.Collections.Generic;
using System.Linq;
using System.Text;
using OneMoreMove.Content;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using UnityEditor;
using UnityEngine;

namespace OneMoreMove.EditorTools
{
    public sealed class LevelCheck
    {
        public LevelCheck(string levelId, IReadOnlyList<string> errors, IReadOnlyList<string> warnings, SolverResult solverResult)
        {
            LevelId = levelId;
            Errors = errors;
            Warnings = warnings;
            SolverResult = solverResult;
        }

        public string LevelId { get; }
        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
        public SolverResult SolverResult { get; }
        public bool Passed => Errors.Count == 0;
    }

    /// <summary>
    /// Release gate for content: structure, replay of the stored solution through the real rules, and a completed
    /// shortest-path search that confirms the stored optimum. Unsolved or unfinished searches block the release.
    /// </summary>
    public static class CatalogValidator
    {
        public const string CatalogPath = "Assets/_Project/Content/LevelCatalog.asset";

        public static LevelCheck Check(LevelDefinition level)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            var report = LevelValidator.Validate(level);
            errors.AddRange(report.Issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.ToString()));
            warnings.AddRange(report.Issues.Where(i => i.Severity == ValidationSeverity.Warning).Select(i => i.ToString()));
            if (errors.Count > 0) return new LevelCheck(level.Id, errors, warnings, null);

            var replay = SolutionReplayer.Replay(level, level.KnownSolution);
            if (!replay.ReachedGoal)
            {
                errors.Add(replay.FailedStep >= 0
                    ? $"Known solution is rejected at step {replay.FailedStep + 1} ({replay.FailureReason})."
                    : "Known solution does not reach the goal.");
            }

            var solved = BfsSolver.Solve(level, SolverBudget.Exhaustive);
            switch (solved.Status)
            {
                case SolverStatus.Unsolvable:
                    errors.Add("Level is unsolvable (search completed).");
                    break;
                case SolverStatus.Unknown:
                    errors.Add($"Search did not finish ({solved.StopReason}); the level cannot be published.");
                    break;
                case SolverStatus.Solved:
                    if (!level.OptimalMoves.HasValue) errors.Add($"OptimalMoves is not set; the search found {solved.Path.Count}. Use Solve.");
                    else if (level.OptimalMoves.Value != solved.Path.Count)
                        errors.Add($"OptimalMoves is {level.OptimalMoves.Value} but the shortest solution has {solved.Path.Count} moves.");
                    break;
            }

            return new LevelCheck(level.Id, errors, warnings, solved);
        }

        public static IReadOnlyList<LevelCheck> Check(LevelCatalog catalog, out List<string> catalogErrors)
        {
            catalogErrors = new List<string>();
            if (catalog.Levels.Count == 0) catalogErrors.Add("Catalog has no levels.");
            if (catalog.Levels.Any(l => l == null)) catalogErrors.Add("Catalog contains an empty slot.");

            var definitions = catalog.BuildDefinitions();
            foreach (var duplicate in definitions.GroupBy(d => d.Id).Where(g => g.Count() > 1))
            {
                catalogErrors.Add($"Duplicate level id '{duplicate.Key}'.");
            }

            return definitions.Select(Check).ToArray();
        }

        public static string Format(IReadOnlyList<LevelCheck> checks, IReadOnlyList<string> catalogErrors)
        {
            var sb = new StringBuilder();
            foreach (var error in catalogErrors) sb.AppendLine($"CATALOG ERROR: {error}");
            foreach (var check in checks)
            {
                var solver = check.SolverResult != null ? $" | {check.SolverResult}" : string.Empty;
                sb.AppendLine($"{(check.Passed ? "PASS" : "FAIL")} {check.LevelId}{solver}");
                foreach (var e in check.Errors) sb.AppendLine($"    error: {e}");
                foreach (var w in check.Warnings) sb.AppendLine($"    warning: {w}");
            }

            return sb.ToString();
        }

        [MenuItem("One More Move/Validate Level Catalog")]
        public static void ValidateFromMenu()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"No catalog at {CatalogPath}. Run One More Move/Setup Project first.");
                return;
            }

            var checks = Check(catalog, out var catalogErrors);
            var text = Format(checks, catalogErrors);
            if (catalogErrors.Count == 0 && checks.All(c => c.Passed)) Debug.Log("Level catalog is valid.\n" + text);
            else Debug.LogError("Level catalog has errors.\n" + text);
        }

        /// <summary>Batch entry point: exits with 1 when any level fails.</summary>
        public static void ValidateBatch()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"No catalog at {CatalogPath}.");
                EditorApplication.Exit(1);
                return;
            }

            var checks = Check(catalog, out var catalogErrors);
            Debug.Log(Format(checks, catalogErrors));
            EditorApplication.Exit(catalogErrors.Count == 0 && checks.All(c => c.Passed) ? 0 : 1);
        }
    }
}
