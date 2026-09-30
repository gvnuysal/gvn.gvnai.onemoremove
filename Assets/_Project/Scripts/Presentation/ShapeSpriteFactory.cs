using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreMove.Presentation
{
    public enum Shape
    {
        Tile,
        Circle,
        Diamond,
        Target,
        GateClosed,
        GateOpen,
        Frame,
        Arrow,
        Star,
        Wait
    }

    /// <summary>
    /// Generates the simple geometric art at runtime (white, anti-aliased, 1 world unit per sprite) so the prototype
    /// ships without imported textures. Sprites are cached for the lifetime of the app.
    /// </summary>
    public static class ShapeSpriteFactory
    {
        private const int Size = 128;
        private const int Samples = 4;
        private static readonly Dictionary<Shape, Sprite> Cache = new Dictionary<Shape, Sprite>();

        public static Sprite Get(Shape shape)
        {
            if (Cache.TryGetValue(shape, out var sprite) && sprite != null) return sprite;

            sprite = Create(shape.ToString(), Inside(shape));
            Cache[shape] = sprite;
            return sprite;
        }

        private static Func<float, float, bool> Inside(Shape shape)
        {
            switch (shape)
            {
                case Shape.Tile: return (x, y) => RoundedBox(x, y, 0.92f, 0.18f);
                case Shape.Circle: return (x, y) => x * x + y * y <= 0.95f * 0.95f;
                case Shape.Diamond: return (x, y) => Mathf.Abs(x) + Mathf.Abs(y) <= 0.97f;
                case Shape.Target:
                    return (x, y) =>
                    {
                        var r = Mathf.Sqrt(x * x + y * y);
                        return (r >= 0.68f && r <= 0.92f) || r <= 0.3f;
                    };
                case Shape.GateClosed:
                    return (x, y) =>
                    {
                        var frame = RoundedBox(x, y, 0.94f, 0.16f) && !RoundedBox(x, y, 0.76f, 0.1f);
                        var bar = RoundedBox(x, y, 0.76f, 0.1f) && (Mathf.Abs(x) < 0.08f || Mathf.Abs(Mathf.Abs(x) - 0.42f) < 0.08f);
                        return frame || bar;
                    };
                case Shape.GateOpen:
                    return (x, y) => RoundedBox(x, y, 0.94f, 0.16f) && !RoundedBox(x, y, 0.8f, 0.1f) && Mathf.Abs(x) > 0.42f && Mathf.Abs(y) > 0.42f;
                case Shape.Frame: return (x, y) => RoundedBox(x, y, 0.96f, 0.16f) && !RoundedBox(x, y, 0.8f, 0.1f);
                case Shape.Arrow:
                    return (x, y) =>
                    {
                        var head = y >= -0.1f && y <= 0.9f - 1.25f * Mathf.Abs(x);
                        var stem = Mathf.Abs(x) <= 0.22f && y >= -0.9f && y < -0.1f;
                        return head || stem;
                    };
                case Shape.Star: return InsideStar;
                case Shape.Wait: return (x, y) => Mathf.Abs(Mathf.Abs(x) - 0.36f) <= 0.18f && Mathf.Abs(y) <= 0.78f;
                default: throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        private static bool RoundedBox(float x, float y, float halfSize, float radius)
        {
            var qx = Mathf.Abs(x) - halfSize + radius;
            var qy = Mathf.Abs(y) - halfSize + radius;
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius <= 0f;
        }

        private static readonly Vector2[] StarPoints = BuildStar();

        private static Vector2[] BuildStar()
        {
            var points = new Vector2[10];
            for (var i = 0; i < 10; i++)
            {
                var angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                var radius = i % 2 == 0 ? 0.97f : 0.42f;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius - 0.05f);
            }

            return points;
        }

        private static bool InsideStar(float x, float y)
        {
            var inside = false;
            for (int i = 0, j = StarPoints.Length - 1; i < StarPoints.Length; j = i++)
            {
                var a = StarPoints[i];
                var b = StarPoints[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }

            return inside;
        }

        private static Sprite Create(string name, Func<float, float, bool> inside)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            var pixels = new Color32[Size * Size];
            const float step = 2f / Size;
            const float subStep = step / Samples;
            for (var py = 0; py < Size; py++)
            {
                for (var px = 0; px < Size; px++)
                {
                    var hits = 0;
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            var x = -1f + px * step + (sx + 0.5f) * subStep;
                            var y = -1f + py * step + (sy + 0.5f) * subStep;
                            if (inside(x, y)) hits++;
                        }
                    }

                    pixels[py * Size + px] = new Color32(255, 255, 255, (byte)(255 * hits / (Samples * Samples)));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
