using UnityEngine;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// High-contrast palette: one primary (player, focus) and one accent (echo). State is always also encoded by shape,
    /// never by colour alone.
    /// </summary>
    public static class Palette
    {
        public static readonly Color Background = Hex(0x12141C);
        public static readonly Color Floor = Hex(0x222637);
        public static readonly Color Wall = Hex(0x4A5170);
        public static readonly Color Goal = Hex(0xE9ECF5);
        public static readonly Color Gate = Hex(0xC9CEDB);
        public static readonly Color GateOpen = new Color(0.79f, 0.81f, 0.86f, 0.55f);
        public static readonly Color Primary = Hex(0xF5C451);
        public static readonly Color Accent = Hex(0x56CCF2);
        public static readonly Color Reject = Hex(0xFF5A5A);
        public static readonly Color Hint = Hex(0xFFFFFF);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        private static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
