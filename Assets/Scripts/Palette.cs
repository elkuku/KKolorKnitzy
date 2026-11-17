using UnityEngine;

namespace KnotAndSpool
{
    public static class Palette
    {
        static readonly Color[] Yarn =
        {
            Hex(0xE5484D), // red
            Hex(0x3E7BFA), // blue
            Hex(0xF5C02B), // yellow
            Hex(0x3BB273), // green
            Hex(0x8E5BD9), // purple
            Hex(0xF28A30), // orange
            Hex(0xF06BAA), // pink
            Hex(0x22B5B0), // teal
        };

        public static int Count => Yarn.Length;
        public static Color Get(int i) => Yarn[i % Yarn.Length];

        public static readonly Color Background = Hex(0xFBF3E6);
        public static readonly Color Panel = Hex(0xF1E3CC);
        public static readonly Color SlotEmpty = Hex(0xE6D3B5);
        public static readonly Color WoodLight = Hex(0xE9C893);
        public static readonly Color WoodDark = Hex(0xB98552);
        public static readonly Color Outline = Hex(0x3B2F2A);
        public static readonly Color Text = Hex(0x4A3A30);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// Blocked threads: faded toward the background so the hue stays recognisable.
        public static Color Dim(Color c) => Color.Lerp(c, Background, 0.55f);
    }
}
