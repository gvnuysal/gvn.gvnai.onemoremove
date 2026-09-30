using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;
using UnityEngine;

namespace OneMoreMove.Content
{
    /// <summary>Ordered list of shipped levels. Order defines unlocking.</summary>
    [CreateAssetMenu(menuName = "One More Move/Level Catalog", fileName = "LevelCatalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [Tooltip("Increase when any shipped level changes; stored in saves for diagnostics.")]
        [SerializeField, Min(1)] private int contentVersion = 1;

        [SerializeField] private List<LevelAsset> levels = new List<LevelAsset>();

        public int ContentVersion => contentVersion;
        public IReadOnlyList<LevelAsset> Levels => levels;

        public IReadOnlyList<LevelDefinition> BuildDefinitions() => levels.Where(l => l != null).Select(l => l.ToDefinition()).ToArray();

        public void SetLevels(IEnumerable<LevelAsset> assets, int version)
        {
            levels = assets.ToList();
            contentVersion = version;
        }
    }
}
