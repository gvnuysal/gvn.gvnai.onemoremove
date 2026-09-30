using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>
    /// Adapts the UI to the device: keeps it inside the safe area (notches, rounded corners, home indicator), switches
    /// the portrait layout, and tells the board how much of the screen the HUD covers so the board is never hidden
    /// behind controls. The HUD is measured after layout, so text scaling and orientation are handled the same way.
    /// </summary>
    public sealed class ScreenLayout
    {
        public const string PortraitClass = "app--portrait";

        /// <summary>Breathing room between the HUD and the board, as a fraction of the panel height.</summary>
        private const float Gap = 0.01f;

        private readonly VisualElement _app;
        private readonly VisualElement _hud;
        private readonly VisualElement _hudTop;
        private readonly VisualElement _tip;
        private readonly VisualElement _dpad;
        private readonly VisualElement _actions;
        private readonly BoardView _board;

        private Rect _lastSafeArea;
        private Vector2Int _lastScreen;

        public ScreenLayout(VisualElement root, BoardView board)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            _board = board ? board : throw new ArgumentNullException(nameof(board));
            _app = Find(root, "app");
            _hud = Find(root, "hud");
            _hudTop = Find(root, "hud-top");
            _tip = Find(root, "level-tip");
            _dpad = Find(root, "dpad");
            _actions = Find(root, "hud-actions");

            foreach (var element in new[] { _hud, _hudTop, _tip, _dpad, _actions })
            {
                element.RegisterCallback<GeometryChangedEvent>(_ => MeasureHud());
            }
        }

        public bool IsPortrait { get; private set; }

        /// <summary>Panel-space area between the HUD rows where the board is drawn (for screen reader frames).</summary>
        public Rect BoardArea { get; private set; }

        /// <summary>Call once per frame; does work only when the screen size or safe area changed.</summary>
        public void Tick()
        {
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (screen == _lastScreen && Screen.safeArea == _lastSafeArea) return;

            _lastScreen = screen;
            _lastSafeArea = Screen.safeArea;
            IsPortrait = screen.y > screen.x;
            _app.EnableInClassList(PortraitClass, IsPortrait);
            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            var panel = _app.panel;
            if (panel == null || Screen.width <= 0 || Screen.height <= 0) return;

            var safe = Screen.safeArea;
            // Screen space has its origin at the bottom left, panel space at the top left.
            var topLeft = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMin, Screen.height - safe.yMax));
            var bottomRight = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(safe.xMax, Screen.height - safe.yMin));
            var full = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(Screen.width, Screen.height));

            var left = Mathf.Max(0f, topLeft.x);
            var top = Mathf.Max(0f, topLeft.y);
            var right = Mathf.Max(0f, full.x - bottomRight.x);
            var bottom = Mathf.Max(0f, full.y - bottomRight.y);
            _app.style.marginLeft = left;
            _app.style.marginTop = top;
            _app.style.marginRight = right;
            _app.style.marginBottom = bottom;

            // Dimming overlays still cover the whole screen, notch area included; only their panels stay safe.
            _app.Query<VisualElement>(className: "overlay").ForEach(overlay =>
            {
                overlay.style.left = -left;
                overlay.style.top = -top;
                overlay.style.right = -right;
                overlay.style.bottom = -bottom;
                overlay.style.paddingLeft = left;
                overlay.style.paddingTop = top;
                overlay.style.paddingRight = right;
                overlay.style.paddingBottom = bottom;
            });
        }

        private void MeasureHud()
        {
            if (_hud.resolvedStyle.display == DisplayStyle.None) return;

            var panelHeight = _app.panel?.visualTree.layout.height ?? 0f;
            if (panelHeight <= 0f || float.IsNaN(panelHeight)) return;

            var top = _hudTop.worldBound.yMax;
            if (_tip.resolvedStyle.display != DisplayStyle.None) top = Mathf.Max(top, _tip.worldBound.yMax);

            var bottomEdge = Mathf.Min(_dpad.worldBound.yMin, _actions.worldBound.yMin);
            if (float.IsNaN(top) || float.IsNaN(bottomEdge)) return;

            BoardArea = new Rect(0f, top, _app.worldBound.width, Mathf.Max(0f, bottomEdge - top));
            _board.SetReservedFractions(top / panelHeight + Gap, (panelHeight - bottomEdge) / panelHeight + Gap);
        }

        private static VisualElement Find(VisualElement root, string name) =>
            root.Q<VisualElement>(name) ?? throw new InvalidOperationException($"UI element '{name}' is missing from App.uxml.");
    }
}
