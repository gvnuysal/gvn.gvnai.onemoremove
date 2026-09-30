using OneMoreMove.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace OneMoreMove.Presentation.UI
{
    internal static class UiIcons
    {
        public static VisualElement Icon(Shape shape, Color tint, float size, float rotation = 0f)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("icon");
            icon.style.width = size;
            icon.style.height = size;
            icon.style.backgroundImage = new StyleBackground(ShapeSpriteFactory.Get(shape));
            icon.style.unityBackgroundImageTintColor = tint;
            if (!Mathf.Approximately(rotation, 0f)) icon.style.rotate = new Rotate(new Angle(rotation, AngleUnit.Degree));
            return icon;
        }

        public static float Rotation(Direction direction)
        {
            switch (direction)
            {
                case Direction.Right: return 90f;
                case Direction.Down: return 180f;
                case Direction.Left: return 270f;
                default: return 0f;
            }
        }

        /// <summary>Filled and empty stars differ in shape fill and in the accompanying text, not only colour.</summary>
        public static void FillStars(VisualElement container, int stars, float size)
        {
            container.Clear();
            for (var i = 0; i < 3; i++)
            {
                var filled = i < stars;
                var star = Icon(Shape.Star, filled ? Palette.Primary : new Color(1f, 1f, 1f, 0.18f), size);
                star.AddToClassList(filled ? "star--filled" : "star--empty");
                container.Add(star);
            }

            container.tooltip = Strings.Stars(stars);
        }
    }
}
