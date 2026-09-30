using System;
using OneMoreMove.Session;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    public sealed class LevelSelectScreen : UiScreen
    {
        private readonly VisualElement _grid;
        private readonly Button _back;
        private Button _firstOpen;

        public LevelSelectScreen(VisualElement root) : base(root)
        {
            _grid = Find<VisualElement>(root, "level-grid");
            _back = Find<Button>(root, "level-select-back");
            _back.clicked += () => BackClicked?.Invoke();

            // Touch screens scroll by dragging; the desktop scrollbar only takes space there.
            var scroll = root.Q<ScrollView>();
            if (scroll != null && Application.isMobilePlatform) scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        public event Action<int> LevelChosen;
        public event Action BackClicked;

        protected override Focusable InitialFocus => (Focusable)_firstOpen ?? _back;

        public void Populate(LevelLibrary library, ProgressService progress, int highlightIndex)
        {
            _grid.Clear();
            _firstOpen = null;
            for (var i = 0; i < library.Count; i++)
            {
                var index = i;
                var level = library.Levels[i];
                var record = progress.Get(level.Id);
                var unlocked = progress.IsUnlocked(library, i);

                var button = new Button(() => LevelChosen?.Invoke(index)) { name = $"level-{i}" };
                button.AddToClassList("level-card");
                button.EnableInClassList("level-card--locked", !unlocked);
                button.EnableInClassList("level-card--completed", record?.Completed == true);
                button.SetEnabled(unlocked);

                var number = new Label(Strings.LevelNumber(i)) { pickingMode = PickingMode.Ignore };
                number.AddToClassList("level-card__number");
                number.AddToClassList("t-heading");
                button.Add(number);

                var title = new Label(unlocked ? level.NameIn(Localization.Code) : Strings.Locked) { pickingMode = PickingMode.Ignore };
                title.AddToClassList("level-card__title");
                title.AddToClassList("t-small");
                button.Add(title);

                var stars = new VisualElement { pickingMode = PickingMode.Ignore };
                stars.AddToClassList("stars");
                UiIcons.FillStars(stars, record?.BestStars ?? 0, 26f);
                button.Add(stars);

                _grid.Add(button);
                if (unlocked && (_firstOpen == null || i == highlightIndex)) _firstOpen = button;
            }
        }
    }
}
