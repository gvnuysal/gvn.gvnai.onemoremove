using System.Linq;
using NUnit.Framework;
using OneMoreMove.Content;
using OneMoreMove.EditorTools;
using UnityEditor;

namespace OneMoreMove.Tests
{
    /// <summary>
    /// Release gate from the design document: every shipped level passes structural checks, its stored solution
    /// replays through the real rules and a completed search confirms the stored optimum.
    /// </summary>
    public sealed class CatalogReleaseGateTests
    {
        [Test]
        public void EveryShippedLevel_IsValidSolvedAndOptimal()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogValidator.CatalogPath);
            Assert.That(catalog, Is.Not.Null, "Run One More Move/Setup Project to create the catalog.");

            var checks = CatalogValidator.Check(catalog, out var catalogErrors);
            var report = CatalogValidator.Format(checks, catalogErrors);
            Assert.That(catalogErrors, Is.Empty, report);
            Assert.That(checks.All(c => c.Passed), Is.True, report);
        }

        [Test]
        public void TheDocumentGateTutorial_IsShippedWithFourRights()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogValidator.CatalogPath);
            var tutorial = catalog.BuildDefinitions().Single(l => l.Id == "tutorial_gate_01");
            Assert.That(tutorial.OptimalMoves, Is.EqualTo(4));
            Assert.That(tutorial.ParMoves, Is.EqualTo(5));
        }
    }
}
