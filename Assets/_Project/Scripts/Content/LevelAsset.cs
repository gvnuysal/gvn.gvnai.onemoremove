using System;
using System.Collections.Generic;
using System.Linq;
using OneMoreMove.Core;
using UnityEngine;

namespace OneMoreMove.Content
{
    [Serializable]
    public struct GateData
    {
        public Vector2Int position;
        public bool initiallyOpen;
    }

    [Serializable]
    public struct LevelTranslation
    {
        [Tooltip("Language code, e.g. \"en\".")]
        public string language;

        public string name;

        [TextArea(1, 3)] public string tip;
    }

    /// <summary>
    /// Designer-editable level data. The asset is never modified at runtime: gameplay works on the immutable
    /// <see cref="LevelDefinition"/> returned by <see cref="ToDefinition"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "One More Move/Level", fileName = "Level")]
    public sealed class LevelAsset : ScriptableObject
    {
        [SerializeField] private string id = "level_id";
        [SerializeField] private string displayName = "Yeni Bölüm";
        [SerializeField, Min(1)] private int revision = 1;
        [SerializeField, Min(1)] private int rulesVersion = RulesEngine.Version;
        [SerializeField] private bool isTutorial;

        [Tooltip("Optional one-line teaching text shown in the HUD while this level is played.")]
        [SerializeField, TextArea(1, 3)] private string tip;

        [Tooltip("Name and tip in other languages (the fields above are Turkish, the source language).")]
        [SerializeField] private List<LevelTranslation> translations = new List<LevelTranslation>();

        [SerializeField, Range(1, LevelDefinition.MaxDimension)] private int width = 5;
        [SerializeField, Range(1, LevelDefinition.MaxDimension)] private int height = 5;
        [SerializeField] private List<Vector2Int> walls = new List<Vector2Int>();
        [SerializeField] private List<GateData> gates = new List<GateData>();
        [SerializeField] private Vector2Int playerStart;
        [SerializeField] private bool hasEcho;
        [SerializeField] private Vector2Int echoStart;
        [SerializeField] private Vector2Int goal = new Vector2Int(4, 4);

        [Tooltip("Designer target (2 stars).")]
        [SerializeField, Min(1)] private int parMoves = 5;

        [Tooltip("Only set by a completed shortest-path search (3 stars). Use the Solve button.")]
        [SerializeField] private bool hasOptimalMoves;

        [SerializeField, Min(0)] private int optimalMoves;
        [SerializeField] private List<Direction> knownSolution = new List<Direction>();

        public string Id => id;
        public string DisplayName => displayName;

        public LevelDefinition ToDefinition()
        {
            return new LevelDefinition(
                id,
                displayName,
                revision,
                rulesVersion,
                width,
                height,
                walls.Select(ToGrid),
                gates.Select(g => new GateDefinition(ToGrid(g.position), g.initiallyOpen)),
                ToGrid(playerStart),
                hasEcho ? ToGrid(echoStart) : (GridPos?)null,
                ToGrid(goal),
                parMoves,
                hasOptimalMoves ? optimalMoves : (int?)null,
                knownSolution,
                isTutorial,
                tip,
                translations.Where(t => !string.IsNullOrEmpty(t.language))
                    .GroupBy(t => t.language)
                    .ToDictionary(g => g.Key, g => new LevelText(g.First().name, g.First().tip)));
        }

        /// <summary>Overwrites every field from a definition (import, editor tools).</summary>
        public void CopyFrom(LevelDefinition level)
        {
            id = level.Id;
            displayName = level.Name;
            revision = level.Revision;
            rulesVersion = level.RulesVersion;
            isTutorial = level.IsTutorial;
            tip = level.Tip;
            translations = level.Translations
                .Select(t => new LevelTranslation { language = t.Key, name = t.Value.Name, tip = t.Value.Tip })
                .ToList();
            width = level.Width;
            height = level.Height;
            walls = level.Walls.Select(ToVector).ToList();
            gates = level.Gates.Select(g => new GateData { position = ToVector(g.Position), initiallyOpen = g.InitiallyOpen }).ToList();
            playerStart = ToVector(level.PlayerStart);
            hasEcho = level.EchoStart.HasValue;
            echoStart = level.EchoStart.HasValue ? ToVector(level.EchoStart.Value) : Vector2Int.zero;
            goal = ToVector(level.Goal);
            parMoves = level.ParMoves;
            hasOptimalMoves = level.OptimalMoves.HasValue;
            optimalMoves = level.OptimalMoves ?? 0;
            knownSolution = level.KnownSolution.ToList();
        }

        private static GridPos ToGrid(Vector2Int v) => new GridPos(v.x, v.y);
        private static Vector2Int ToVector(GridPos p) => new Vector2Int(p.X, p.Y);
    }
}
