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

        /// Pastel colour scheme of a level: sky, floor under the board, board tiles.
        public readonly struct Theme
        {
            public readonly Color Top, Bottom, Floor, FloorEdge, Tile, Accent;

            public Theme(int top, int bottom, int floor, int tile, int accent)
            {
                Top = Hex(top);
                Bottom = Hex(bottom);
                Floor = Hex(floor);
                FloorEdge = Shade(Floor, 0.85f);
                Tile = Hex(tile);
                Accent = Hex(accent);
            }
        }

        static readonly Theme[] Themes =
        {
            new Theme(0xF9E4EC, 0xF2C9D9, 0x9C82A6, 0xFBEAF1, 0xE0679A), // rose
            new Theme(0xDDF1E6, 0xBFE2D0, 0x5F9585, 0xEEF8F2, 0x3FA37B), // mint
            new Theme(0xE6E2F6, 0xCFC8EE, 0x8B85AC, 0xF3F1FB, 0x7C6BD6), // lilac
            new Theme(0xFCE0D6, 0xF4B3A2, 0xB0665C, 0xFCEEE9, 0xE36E55), // coral
            new Theme(0xDCE9FA, 0xBBD3F2, 0x7D93B9, 0xEEF4FD, 0x4C86D9), // sky
            new Theme(0xF3E7DD, 0xE6D0C0, 0x9E847A, 0xFAF2EC, 0xC27A52), // sand
        };

        public static Theme ThemeFor(int level) => Themes[(Mathf.Max(1, level) - 1) % Themes.Length];

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

        /// Multiply brightness, keep alpha.
        public static Color Shade(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a);

        /// Blend toward white, keep alpha.
        public static Color Tint(Color c, float f) => new Color(Mathf.Lerp(c.r, 1f, f), Mathf.Lerp(c.g, 1f, f), Mathf.Lerp(c.b, 1f, f), c.a);
    }
}
