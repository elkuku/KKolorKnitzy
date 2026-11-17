using UnityEngine;

namespace KnotAndSpool
{
    public static class Palette
    {
        /// Letters used in picture templates, in palette order.
        public const string Letters = "RBYGPOKTWNDL";

        static readonly Color[] Yarn =
        {
            Hex(0xE5484D), // R red
            Hex(0x3E7BFA), // B blue
            Hex(0xF5C02B), // Y yellow
            Hex(0x4CB963), // G green
            Hex(0x8E5BD9), // P purple
            Hex(0xF28A30), // O orange
            Hex(0xF48FB1), // K pink
            Hex(0x22B5B0), // T teal
            Hex(0xF7F1E3), // W cream white
            Hex(0x9A6B4A), // N brown
            Hex(0x3D3A4B), // D dark
            Hex(0x9FD3F5), // L light blue
        };

        public static int Count => Yarn.Length;
        public static Color Get(int i) => Yarn[i % Yarn.Length];
        public static int IndexOf(char letter) => Letters.IndexOf(letter);

        public static readonly Color BackgroundTop = Hex(0xFFF8EE);
        public static readonly Color BackgroundBottom = Hex(0xF4DFC4);
        public static readonly Color Background = Hex(0xFBF0E0);
        public static readonly Color Panel = Hex(0xFFFBF4);
        public static readonly Color Frame = Hex(0xD9B98F);
        public static readonly Color Shelf = Hex(0xE6CBA4);
        public static readonly Color SlotEmpty = Hex(0xD7B88F);
        public static readonly Color WoodLight = Hex(0xEBCB98);
        public static readonly Color WoodDark = Hex(0xB98552);
        public static readonly Color Outline = Hex(0x3B2F2A);
        public static readonly Color Text = Hex(0x4A3A30);
        public static readonly Color Accent = Hex(0xE7835F);
        public static readonly Color Shadow = new Color(0.25f, 0.15f, 0.05f, 0.22f);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// Multiply brightness, keep alpha.
        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);
    }
}
