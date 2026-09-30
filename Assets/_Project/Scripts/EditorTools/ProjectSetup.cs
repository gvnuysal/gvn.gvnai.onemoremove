using System;
using System.IO;
using System.Linq;
using OneMoreMove.Content;
using OneMoreMove.Core;
using OneMoreMove.Core.Solving;
using OneMoreMove.Persistence;
using OneMoreMove.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace OneMoreMove.EditorTools
{
    /// <summary>
    /// Reproducible project setup: imports seed levels (JSON → ScriptableObjects, solved and verified), builds the
    /// level catalog, the UI panel settings and the Main scene. Safe to run repeatedly.
    /// </summary>
    public static class ProjectSetup
    {
        private const string Root = "Assets/_Project";
        private const string LevelSourceFolder = Root + "/Content/LevelSource";
        private const string LevelFolder = Root + "/Content/Levels";
        private const string PanelSettingsPath = Root + "/UI/PanelSettings.asset";
        private const string ThemePath = Root + "/UI/RuntimeTheme.tss";
        private const string AppUxmlPath = Root + "/UI/App.uxml";
        private const string ScenePath = Root + "/Scenes/Main.unity";

        [MenuItem("One More Move/Setup Project (keep edited levels)")]
        public static void SetupFromMenu() => Setup(overwriteLevels: false);

        [MenuItem("One More Move/Reimport Seed Levels (overwrite)")]
        public static void ReimportFromMenu()
        {
            if (EditorUtility.DisplayDialog("Reimport seed levels", "Level assets will be overwritten from Content/LevelSource JSON files.", "Overwrite", "Cancel"))
            {
                ImportSeedLevels(overwrite: true);
                BuildCatalog();
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>Batch entry point (-executeMethod). Exits with 1 on any failure.</summary>
        public static void RunBatch()
        {
            try
            {
                Setup(overwriteLevels: Environment.GetCommandLineArgs().Contains("-overwriteLevels"));
                var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogValidator.CatalogPath);
                var checks = CatalogValidator.Check(catalog, out var catalogErrors);
                Debug.Log("[ProjectSetup] Catalog check:\n" + CatalogValidator.Format(checks, catalogErrors));
                EditorApplication.Exit(catalogErrors.Count == 0 && checks.All(c => c.Passed) ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Setup(bool overwriteLevels)
        {
            ImportSeedLevels(overwriteLevels);
            var catalog = BuildCatalog();
            var panelSettings = BuildPanelSettings();
            BuildScene(catalog, panelSettings);
            BuildTools.ConfigurePlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectSetup] Done.");
        }

        public static void ImportSeedLevels(bool overwrite)
        {
            EnsureFolder(LevelFolder);
            foreach (var path in Directory.GetFiles(LevelSourceFolder, "*.json").OrderBy(p => p, StringComparer.Ordinal))
            {
                var assetPath = $"{LevelFolder}/{Path.GetFileNameWithoutExtension(path)}.asset";
                var existing = AssetDatabase.LoadAssetAtPath<LevelAsset>(assetPath);
                if (existing != null && !overwrite) continue;

                var level = SolveAndVerify(LevelJson.Parse(File.ReadAllText(path)), path);
                var asset = existing != null ? existing : ScriptableObject.CreateInstance<LevelAsset>();
                asset.CopyFrom(level);
                if (existing == null) AssetDatabase.CreateAsset(asset, assetPath);
                EditorUtility.SetDirty(asset);
                Debug.Log($"[ProjectSetup] Imported {level.Id}: optimal {level.OptimalMoves}, par {level.ParMoves}.");
            }
        }

        /// <summary>Attaches the proven shortest solution. Throws if the level is invalid or not provably solvable.</summary>
        public static LevelDefinition SolveAndVerify(LevelDefinition level, string source)
        {
            var report = LevelValidator.Validate(level);
            if (!report.IsValid) throw new InvalidOperationException($"{source}: invalid level\n{report}");

            var result = BfsSolver.Solve(level, SolverBudget.Exhaustive);
            if (result.Status != SolverStatus.Solved) throw new InvalidOperationException($"{source}: {result}");

            var solved = level.WithSolution(result.Path.Count, result.Path);
            if (!SolutionReplayer.Replay(solved, solved.KnownSolution).ReachedGoal)
                throw new InvalidOperationException($"{source}: solver path failed replay.");
            return solved;
        }

        private static LevelCatalog BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogValidator.CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LevelCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogValidator.CatalogPath);
            }

            var levels = AssetDatabase.FindAssets("t:LevelAsset", new[] { LevelFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(AssetDatabase.LoadAssetAtPath<LevelAsset>)
                .ToArray();

            catalog.SetLevels(levels, Math.Max(1, catalog.ContentVersion));
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static PanelSettings BuildPanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            settings.clearColor = false;
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void BuildScene(LevelCatalog catalog, PanelSettings panelSettings)
        {
            EnsureFolder(Path.GetDirectoryName(ScenePath)?.Replace('\\', '/'));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Palette.Background;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            var game = new GameObject("Game");
            var document = game.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AppUxmlPath);

            var boardObject = new GameObject("Board");
            boardObject.transform.SetParent(game.transform, false);
            var board = boardObject.AddComponent<BoardView>();
            board.SetCamera(camera);

            var bootstrap = game.AddComponent<GameBootstrap>();
            bootstrap.Configure(catalog, document, board, camera);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void EnsureFolder(string path)
        {
            if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
