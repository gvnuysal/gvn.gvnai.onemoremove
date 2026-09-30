using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    /// <summary>Scales every text element from a base size given by its USS class (t-title, t-heading, t-large, t-small).</summary>
    public static class TextScaler
    {
        public static void Apply(VisualElement root, int percent)
        {
            var scale = percent / 100f;
            root.Query<TextElement>().ForEach(text => text.style.fontSize = BaseSize(text) * scale);
        }

        private static float BaseSize(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent)
            {
                if (e.ClassListContains("t-title")) return 84f;
                if (e.ClassListContains("t-heading")) return 50f;
                if (e.ClassListContains("t-large")) return 38f;
                if (e.ClassListContains("t-small")) return 24f;
            }

            return 30f;
        }
    }
}
