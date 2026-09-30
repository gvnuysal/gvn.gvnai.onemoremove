using System;
using System.IO;
using OneMoreMove.Presentation;
using UnityEditor;
using UnityEngine;

namespace OneMoreMove.EditorTools
{
    /// <summary>
    /// Draws the app icon in code from the game's own palette and shapes: the player piece one step away from the goal
    /// ("one more move"). Opaque and square; iOS and Android apply their own masks.
    /// </summary>
    public static class IconGenerator
    {
        public const string IconPath = "Assets/_Project/Art/AppIcon.png";
        private const int Size = 1024;
        private const int Samples = 3;

        [MenuItem("One More Move/Generate App Icon")]
        public static Texture2D Generate()
        {
            var pixels = new Color32[Size * Size];
            for (var py = 0; py < Size; py++)
            {
                for (var px = 0; px < Size; px++)
                {
                    var color = new Color(0f, 0f, 0f, 0f);
                    for (var sy = 0; sy < Samples; sy++)
                    {
                        for (var sx = 0; sx < Samples; sx++)
                        {
                            // -1..1 with y up, like ShapeSpriteFactory.
                            var x = -1f + 2f * (px + (sx + 0.5f) / Samples) / Size;
                            var y = -1f + 2f * (py + (sy + 0.5f) / Samples) / Size;
                            color += Shade(x, y);
                        }
                    }

                    pixels[py * Size + px] = color / (Samples * Samples);
                }
            }

            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath) ?? "Assets");
            File.WriteAllBytes(IconPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        }

        private static Color Shade(float x, float y)
        {
            // Keep the artwork inside the central 66 % that survives every platform's icon mask.
            x *= 1.18f;
            y *= 1.18f;

            // Two floor tiles side by side: the player on the left, the goal on the right.
            var color = Palette.Background;
            if (RoundedBox(x + 0.42f, y, 0.36f, 0.08f) || RoundedBox(x - 0.42f, y, 0.36f, 0.08f)) color = Palette.Floor;

            var goal = Mathf.Sqrt((x - 0.42f) * (x - 0.42f) + y * y);
            if ((goal >= 0.19f && goal <= 0.27f) || goal <= 0.09f) color = Palette.Goal;

            var player = Mathf.Sqrt((x + 0.42f) * (x + 0.42f) + y * y);
            if (player <= 0.26f) color = Palette.Primary;

            // A short arrow between them: the one more move.
            var shaft = Mathf.Abs(y) <= 0.035f && x >= -0.1f && x <= 0.02f;
            var head = x >= 0.02f && x <= 0.12f && Mathf.Abs(y) <= 0.09f * (0.12f - x) / 0.1f;
            if (shaft || head) color = Palette.Primary;

            color.a = 1f;
            return color;
        }

        private static bool RoundedBox(float x, float y, float halfSize, float radius)
        {
            var qx = Mathf.Abs(x) - halfSize + radius;
            var qy = Mathf.Abs(y) - halfSize + radius;
            var outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius <= 0f;
        }
    }
}
