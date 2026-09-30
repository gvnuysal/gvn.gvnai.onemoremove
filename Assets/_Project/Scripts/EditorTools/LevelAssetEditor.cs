using System.Collections.Generic;
using System.IO;
using System.Linq;
using OneMoreMove.Content;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using OneMoreMove.Persistence;
using UnityEditor;
using UnityEngine;

namespace OneMoreMove.EditorTools
{
    /// <summary>
    /// Level editor inspector: paint the board, validate, solve with BFS, replay the stored solution and step through
    /// moves. It never reimplements rules; every step goes through <see cref="RulesEngine"/>.
    /// </summary>
    [CustomEditor(typeof(LevelAsset))]
    public sealed class LevelAssetEditor : UnityEditor.Editor
    {
        private enum Tool
        {
            Floor,
            Wall,
            GateOpen,
            GateClosed,
            Player,
            Echo,
            Goal,
            RemoveEcho
        }

        private static readonly string[] ToolNames = { "Zemin", "Duvar", "Kapı (açık)", "Kapı (kapalı)", "Oyuncu", "Yankı", "Hedef", "Yankıyı sil" };
        private static readonly string[] MetaFields = { "id", "displayName", "revision", "rulesVersion", "isTutorial", "parMoves" };

        private Tool _tool = Tool.Wall;
        private BoardState _playState;
        private readonly List<BoardState> _playHistory = new List<BoardState>();
        private string _status;
        private MessageType _statusType = MessageType.Info;
        private bool _showSolution = true;

        private LevelAsset Asset => (LevelAsset)target;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            foreach (var field in MetaFields) EditorGUILayout.PropertyField(serializedObject.FindProperty(field));
            serializedObject.ApplyModifiedProperties();

            var level = Asset.ToDefinition();
            DrawSize(level);
            level = Asset.ToDefinition();

            EditorGUILayout.Space();
            _tool = (Tool)GUILayout.SelectionGrid((int)_tool, ToolNames, 4);
            DrawGrid(level);
            DrawValidation(level);
            DrawSolverTools(level);
            DrawPlayTest(level);
            DrawJsonTools(level);

            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.HelpBox(_status, _statusType);
        }

        private void DrawSize(LevelDefinition level)
        {
            EditorGUILayout.BeginHorizontal();
            var width = EditorGUILayout.IntSlider("Genişlik", level.Width, 1, LevelDefinition.MaxDimension);
            var height = EditorGUILayout.IntSlider("Yükseklik", level.Height, 1, LevelDefinition.MaxDimension);
            EditorGUILayout.EndHorizontal();
            if (width == level.Width && height == level.Height) return;

            bool Inside(GridPos p) => p.X < width && p.Y < height;
            Apply(new LevelDefinition(level.Id, level.Name, level.Revision, level.RulesVersion, width, height,
                level.Walls.Where(Inside), level.Gates.Where(g => Inside(g.Position)), level.PlayerStart,
                level.EchoStart, level.Goal, level.ParMoves, null, null, level.IsTutorial), "Resize level");
        }

        private void DrawGrid(LevelDefinition level)
        {
            var state = _playState ?? level.CreateInitialState();
            var path = _showSolution && _playState == null ? SolutionCells(level) : new Dictionary<GridPos, string>();
            const float cell = 44f;

            var rect = GUILayoutUtility.GetRect(level.Width * cell, level.Height * cell, GUILayout.ExpandWidth(false));
            for (var y = 0; y < level.Height; y++)
            {
                for (var x = 0; x < level.Width; x++)
                {
                    var pos = new GridPos(x, y);
                    var r = new Rect(rect.x + x * cell, rect.y + y * cell, cell - 2f, cell - 2f);
                    EditorGUI.DrawRect(r, level.IsWall(pos) ? new Color(0.29f, 0.32f, 0.44f) : new Color(0.13f, 0.15f, 0.22f));

                    var label = CellLabel(level, state, pos);
                    if (path.TryGetValue(pos, out var steps) && label.Length == 0) label = steps;
                    var style = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = LabelColor(level, state, pos) } };
                    GUI.Label(r, label, style);

                    if (_playState == null && Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
                    {
                        Paint(level, pos);
                        Event.current.Use();
                    }
                }
            }

            EditorGUILayout.LabelField("P oyuncu · E yankı · G hedef · o/x kapı açık/kapalı · rakamlar: bilinen çözümdeki oyuncu yolu", EditorStyles.miniLabel);
            _showSolution = EditorGUILayout.Toggle("Çözüm yolunu göster", _showSolution);
        }

        private static string CellLabel(LevelDefinition level, BoardState state, GridPos pos)
        {
            if (state.Player == pos) return "P";
            if (state.Echo == pos) return "E";
            var gate = level.GateIndexAt(pos);
            var gateText = gate >= 0 ? (state.IsGateOpen(gate) ? "o" : "x") : string.Empty;
            return level.Goal == pos ? "G" + gateText : gateText;
        }

        private static Color LabelColor(LevelDefinition level, BoardState state, GridPos pos)
        {
            if (state.Player == pos) return new Color(0.96f, 0.77f, 0.32f);
            if (state.Echo == pos) return new Color(0.34f, 0.8f, 0.95f);
            return level.Goal == pos ? Color.white : new Color(0.8f, 0.82f, 0.86f);
        }

        /// <summary>Step numbers per cell; a cell the player waits on lists every step spent there ("3,4").</summary>
        private static Dictionary<GridPos, string> SolutionCells(LevelDefinition level)
        {
            var cells = new Dictionary<GridPos, string>();
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            var step = 0;
            foreach (var direction in level.KnownSolution)
            {
                var result = rules.TryMove(state, direction);
                if (!result.Accepted) break;
                state = result.NextState;
                step++;
                cells[state.Player] = direction.IsWait() && cells.TryGetValue(state.Player, out var earlier) ? earlier + "," + step : step.ToString();
            }

            return cells;
        }

        private void Paint(LevelDefinition level, GridPos pos)
        {
            var walls = level.Walls.Where(w => w != pos).ToList();
            var gates = level.Gates.Where(g => g.Position != pos).ToList();
            var player = level.PlayerStart;
            var echo = level.EchoStart;
            var goal = level.Goal;

            switch (_tool)
            {
                case Tool.Floor: break;
                case Tool.Wall: walls.Add(pos); break;
                case Tool.GateOpen: gates.Add(new GateDefinition(pos, true)); break;
                case Tool.GateClosed: gates.Add(new GateDefinition(pos, false)); break;
                case Tool.Player:
                    walls = level.Walls.Where(w => w != pos).ToList();
                    gates = level.Gates.ToList();
                    player = pos;
                    break;
                case Tool.Echo:
                    walls = level.Walls.Where(w => w != pos).ToList();
                    gates = level.Gates.ToList();
                    echo = pos;
                    break;
                case Tool.Goal:
                    walls = level.Walls.Where(w => w != pos).ToList();
                    gates = level.Gates.ToList();
                    goal = pos;
                    break;
                case Tool.RemoveEcho:
                    walls = level.Walls.ToList();
                    gates = level.Gates.ToList();
                    echo = null;
                    break;
            }

            // Any board change invalidates the proven optimum and the stored solution.
            Apply(new LevelDefinition(level.Id, level.Name, level.Revision, level.RulesVersion, level.Width, level.Height,
                walls, gates, player, echo, goal, level.ParMoves, null, null, level.IsTutorial), "Paint level");
            SetStatus("Tahta değişti: çözüm ve optimum silindi. Yayınlanmış bir bölümse Revision değerini artırın.", MessageType.Warning);
        }

        private static void DrawValidation(LevelDefinition level)
        {
            var report = LevelValidator.Validate(level);
            foreach (var issue in report.Issues)
            {
                EditorGUILayout.HelpBox(issue.Message, issue.Severity == ValidationSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }

        private void DrawSolverTools(LevelDefinition level)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Çözüm", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Optimum", level.OptimalMoves?.ToString() ?? "doğrulanmadı");
            EditorGUILayout.LabelField("Bilinen çözüm", level.KnownSolution.Count == 0 ? "-" : string.Join(" ", level.KnownSolution));

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Çöz (BFS)"))
            {
                var result = BfsSolver.Solve(level, SolverBudget.Exhaustive);
                if (result.Status == SolverStatus.Solved)
                {
                    Apply(level.WithSolution(result.Path.Count, result.Path), "Solve level");
                    SetStatus($"{result}. Par: {level.ParMoves}.", MessageType.Info);
                }
                else
                {
                    SetStatus(result.Status == SolverStatus.Unsolvable ? $"Çözümsüz (arama tamamlandı). {result}" : $"Arama bitmedi, sonuç bilinmiyor. {result}", MessageType.Error);
                }
            }

            if (GUILayout.Button("Çözümü tekrar oynat"))
            {
                var replay = SolutionReplayer.Replay(level, level.KnownSolution);
                SetStatus(replay.ReachedGoal
                        ? $"Bilinen çözüm gerçek kurallarla hedefe ulaşıyor ({level.KnownSolution.Count} hamle)."
                        : $"Bilinen çözüm başarısız: adım {replay.FailedStep + 1}, {replay.FailureReason}.",
                    replay.ReachedGoal ? MessageType.Info : MessageType.Error);
            }

            if (GUILayout.Button("Tam kontrol")) SetStatus(CatalogValidator.Format(new[] { CatalogValidator.Check(level) }, new string[0]), MessageType.Info);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPlayTest(LevelDefinition level)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Deneme", EditorStyles.boldLabel);
            if (_playState == null)
            {
                if (GUILayout.Button("Tek adım denemeyi başlat"))
                {
                    _playState = level.CreateInitialState();
                    _playHistory.Clear();
                }

                return;
            }

            EditorGUILayout.LabelField("Durum", _playState.ToString());
            var rules = new RulesEngine(level);
            EditorGUILayout.BeginHorizontal();
            foreach (var direction in Directions.Commands)
            {
                if (!GUILayout.Button(direction.ToString())) continue;
                var result = rules.TryMove(_playState, direction);
                if (result.Accepted)
                {
                    _playHistory.Add(_playState);
                    _playState = result.NextState;
                    SetStatus(string.Join(", ", result.Events), MessageType.Info);
                }
                else
                {
                    SetStatus($"Reddedildi: {result.RejectReason}", MessageType.Warning);
                }
            }

            EditorGUI.BeginDisabledGroup(_playHistory.Count == 0);
            if (GUILayout.Button("Geri al"))
            {
                _playState = _playHistory[_playHistory.Count - 1];
                _playHistory.RemoveAt(_playHistory.Count - 1);
            }

            EditorGUI.EndDisabledGroup();
            if (GUILayout.Button("Bitir")) _playState = null;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawJsonTools(LevelDefinition level)
        {
            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("JSON dışa aktar…"))
            {
                var path = EditorUtility.SaveFilePanel("Export level", "Assets/_Project/Content/LevelSource", level.Id, "json");
                if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, LevelJson.ToJson(level));
            }

            if (GUILayout.Button("JSON içe aktar…"))
            {
                var path = EditorUtility.OpenFilePanel("Import level", "Assets/_Project/Content/LevelSource", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        Apply(LevelJson.Parse(File.ReadAllText(path)), "Import level JSON");
                    }
                    catch (System.Exception e)
                    {
                        SetStatus(e.Message, MessageType.Error);
                    }
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Apply(LevelDefinition level, string undoName)
        {
            Undo.RecordObject(Asset, undoName);
            Asset.CopyFrom(level);
            EditorUtility.SetDirty(Asset);
            _playState = null;
        }

        private void SetStatus(string text, MessageType type)
        {
            _status = text;
            _statusType = type;
        }
    }
}
