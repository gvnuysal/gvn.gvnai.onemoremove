using OneMoreMove.Content;
using OneMoreMove.Core.Solving;
using OneMoreMove.Persistence;
using OneMoreMove.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Composition root: builds the object graph by hand (no DI framework needed at this size) and forwards Unity
    /// lifecycle events. The only MonoBehaviour that knows about every layer.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private LevelCatalog catalog;
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private BoardView boardView;
        [SerializeField] private Camera boardCamera;
        [SerializeField] private int targetFrameRate = 60;

        private GameCoordinator _game;
        private GameplayController _gameplay;
        private InputController _input;
        private AppPresenter _app;

        public GameCoordinator Game => _game;
        public GameplayController Gameplay => _gameplay;

        /// <summary>Test seam: keeps automated runs away from the player's real save file.</summary>
        internal static string SaveDirectoryOverride { get; set; }

        public void Configure(LevelCatalog levelCatalog, UIDocument document, BoardView board, Camera cameraForBoard)
        {
            catalog = levelCatalog;
            uiDocument = document;
            boardView = board;
            boardCamera = cameraForBoard;
        }

        private void Start()
        {
            Application.targetFrameRate = targetFrameRate;
            boardCamera.backgroundColor = Palette.Background;
            boardCamera.clearFlags = CameraClearFlags.SolidColor;

            // The path is read on the main thread; the queued store then writes on a worker thread.
            var store = new QueuedSaveStore(new FileSaveStore(SaveDirectoryOverride ?? Application.persistentDataPath), Debug.LogException);
            var library = new LevelLibrary(catalog.BuildDefinitions(), catalog.ContentVersion);
            _game = new GameCoordinator(library, store);
            _game.Load();

            boardView.SetCamera(boardCamera);
            _gameplay = new GameplayController(_game, boardView, new HintService(SolverBudget.Hint));
            _input = new InputController();
            _app = new AppPresenter(uiDocument.rootVisualElement, _game, _gameplay, boardView, _input, Quit);
            _app.Start();
        }

        private void Update() => _app?.Tick(Time.unscaledDeltaTime);

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) _gameplay?.Suspend();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) _gameplay?.Suspend();
        }

        private void OnApplicationQuit() => _gameplay?.Suspend();

        private void OnDestroy()
        {
            _app?.Dispose();
            _gameplay?.Dispose();
            _input?.Dispose();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
