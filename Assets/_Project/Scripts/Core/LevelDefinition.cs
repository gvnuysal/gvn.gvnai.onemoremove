using System;
using System.Collections.Generic;
using System.Linq;

namespace OneMoreMove.Core
{
    /// <summary>
    /// Immutable, engine-independent description of a level. It may hold structurally invalid data
    /// (for example a wall outside the board) so that <see cref="LevelValidator"/> can report it;
    /// such entries are ignored by the cell lookups.
    /// </summary>
    public sealed class LevelDefinition
    {
        /// <summary>Gate states are stored in a 64-bit mask.</summary>
        public const int MaxGates = 64;

        /// <summary>Upper bound that keeps cell lookups and solver state small; the design targets 5×5.</summary>
        public const int MaxDimension = 8;

        private readonly bool[] _wallByCell;
        private readonly int[] _gateIndexByCell;

        public LevelDefinition(
            string id,
            string name,
            int revision,
            int rulesVersion,
            int width,
            int height,
            IEnumerable<GridPos> walls,
            IEnumerable<GateDefinition> gates,
            GridPos playerStart,
            GridPos? echoStart,
            GridPos goal,
            int parMoves,
            int? optimalMoves,
            IEnumerable<Direction> knownSolution,
            bool isTutorial = false,
            string tip = null)
        {
            if (width < 1 || width > MaxDimension) throw new ArgumentOutOfRangeException(nameof(width), width, $"Width must be 1..{MaxDimension}.");
            if (height < 1 || height > MaxDimension) throw new ArgumentOutOfRangeException(nameof(height), height, $"Height must be 1..{MaxDimension}.");

            Id = id ?? string.Empty;
            Name = name ?? string.Empty;
            Revision = revision;
            RulesVersion = rulesVersion;
            Width = width;
            Height = height;
            Walls = (walls ?? Enumerable.Empty<GridPos>()).ToArray();
            Gates = (gates ?? Enumerable.Empty<GateDefinition>()).ToArray();
            PlayerStart = playerStart;
            EchoStart = echoStart;
            Goal = goal;
            ParMoves = parMoves;
            OptimalMoves = optimalMoves;
            KnownSolution = (knownSolution ?? Enumerable.Empty<Direction>()).ToArray();
            IsTutorial = isTutorial;
            Tip = string.IsNullOrWhiteSpace(tip) ? null : tip.Trim();

            if (Gates.Count > MaxGates) throw new ArgumentException($"A level can have at most {MaxGates} gates.", nameof(gates));

            _wallByCell = new bool[width * height];
            foreach (var wall in Walls)
            {
                if (InBounds(wall)) _wallByCell[ToIndex(wall)] = true;
            }

            _gateIndexByCell = new int[width * height];
            for (var i = 0; i < _gateIndexByCell.Length; i++) _gateIndexByCell[i] = -1;

            ulong initialBits = 0;
            for (var i = 0; i < Gates.Count; i++)
            {
                var gate = Gates[i];
                if (gate.InitiallyOpen) initialBits |= 1UL << i;
                if (InBounds(gate.Position) && _gateIndexByCell[ToIndex(gate.Position)] < 0)
                {
                    _gateIndexByCell[ToIndex(gate.Position)] = i;
                }
            }

            InitialGateBits = initialBits;
            AllGatesMask = Gates.Count == MaxGates ? ulong.MaxValue : (1UL << Gates.Count) - 1;
        }

        public string Id { get; }
        public string Name { get; }
        public int Revision { get; }
        public int RulesVersion { get; }
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<GridPos> Walls { get; }

        /// <summary>Gate order defines the bit index inside <see cref="BoardState.GateOpenBits"/>.</summary>
        public IReadOnlyList<GateDefinition> Gates { get; }

        public GridPos PlayerStart { get; }
        public GridPos? EchoStart { get; }
        public GridPos Goal { get; }

        /// <summary>Designer target move count (2-star threshold).</summary>
        public int ParMoves { get; }

        /// <summary>Shortest solution length, only set when proven by a completed search.</summary>
        public int? OptimalMoves { get; }

        public IReadOnlyList<Direction> KnownSolution { get; }
        public bool IsTutorial { get; }

        /// <summary>Optional one-line teaching text shown while the level is played (introduces a mechanic or control).</summary>
        public string Tip { get; }

        public ulong InitialGateBits { get; }
        public ulong AllGatesMask { get; }
        public int CellCount => Width * Height;

        public bool InBounds(GridPos pos) => pos.X >= 0 && pos.Y >= 0 && pos.X < Width && pos.Y < Height;

        public int ToIndex(GridPos pos) => pos.Y * Width + pos.X;

        public GridPos FromIndex(int index) => new GridPos(index % Width, index / Width);

        public bool IsWall(GridPos pos) => InBounds(pos) && _wallByCell[ToIndex(pos)];

        /// <summary>Returns the gate index at <paramref name="pos"/> or -1.</summary>
        public int GateIndexAt(GridPos pos) => InBounds(pos) ? _gateIndexByCell[ToIndex(pos)] : -1;

        public BoardState CreateInitialState()
        {
            return new BoardState(PlayerStart, EchoStart, InitialGateBits, 0, PlayerStart == Goal);
        }

        /// <summary>Returns a copy with a verified shortest solution attached.</summary>
        public LevelDefinition WithSolution(int? optimalMoves, IEnumerable<Direction> knownSolution)
        {
            return new LevelDefinition(Id, Name, Revision, RulesVersion, Width, Height, Walls, Gates, PlayerStart, EchoStart,
                Goal, ParMoves, optimalMoves, knownSolution, IsTutorial, Tip);
        }
    }
}
