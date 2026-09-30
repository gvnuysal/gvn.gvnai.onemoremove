using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;

namespace OneMoreMove.Session
{
    /// <summary>Ordered, read-only set of playable levels built from the content catalog.</summary>
    public sealed class LevelLibrary
    {
        private readonly Dictionary<string, int> _indexById;

        public LevelLibrary(IEnumerable<LevelDefinition> levels, int contentVersion)
        {
            Levels = (levels ?? throw new ArgumentNullException(nameof(levels))).ToArray();
            ContentVersion = contentVersion;
            _indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < Levels.Count; i++)
            {
                if (_indexById.ContainsKey(Levels[i].Id)) throw new ArgumentException($"Duplicate level id '{Levels[i].Id}'.");
                _indexById.Add(Levels[i].Id, i);
            }
        }

        public IReadOnlyList<LevelDefinition> Levels { get; }
        public int ContentVersion { get; }
        public int Count => Levels.Count;

        public int IndexOf(string levelId) => levelId != null && _indexById.TryGetValue(levelId, out var index) ? index : -1;

        public bool TryGet(string levelId, out LevelDefinition level)
        {
            var index = IndexOf(levelId);
            level = index >= 0 ? Levels[index] : null;
            return level != null;
        }
    }
}
