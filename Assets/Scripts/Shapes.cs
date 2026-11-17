using UnityEngine;

namespace KnotAndSpool
{
    /// Code-generated sprites, textures and materials, so the game needs no art assets.
    public static class Shapes
    {
        static Sprite circle, softCircle, roundRect, stitch, gradient;
        static Texture2D guiRoundRect, retryIcon;
        static Material spriteMaterial;
        static Font font;

        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));
        public static Sprite SoftCircle => softCircle != null ? softCircle : (softCircle = MakeSoftCircle(64));
        public static Sprite RoundRect => roundRect != null ? roundRect : (roundRect = MakeRoundRect(64, 12));
        public static Sprite Stitch => stitch != null ? stitch : (stitch = MakeStitch(96));
        public static Sprite Gradient => gradient != null ? gradient : (gradient = MakeGradient(256));
        public static Texture2D GuiRoundRect => guiRoundRect != null ? guiRoundRect : (guiRoundRect = MakeRoundRectTexture(64, 24));
        public static Texture2D RetryIcon => retryIcon != null ? retryIcon : (retryIcon = MakeRetryIcon(96));
        public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));

        /// The default sprite material (always included in builds); also used for LineRenderers.
        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    var go = new GameObject("tmp");
                    spriteMaterial = go.AddComponent<SpriteRenderer>().sharedMaterial;
                    Object.Destroy(go);
                }
                return spriteMaterial;
            }
        }

        // ---------- scene helpers ----------

        public static SpriteRenderer AddCircle(Transform parent, string name, Vector2 localPos, float diameter, Color color, int order)
        {
            var sr = NewRenderer(parent, name, localPos, order);
            sr.sprite = Circle;
            sr.color = color;
            sr.transform.localScale = new Vector3(diameter, diameter, 1f);
            return sr;
        }

        public static SpriteRenderer AddEllipse(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var sr = AddCircle(parent, name, localPos, 1f, color, order);
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }

        /// Soft blurred ellipse, used for drop shadows.
        public static SpriteRenderer AddShadow(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var sr = NewRenderer(parent, name, localPos, order);
            sr.sprite = SoftCircle;
            sr.color = color;
            sr.transform.localScale = new Vector3(size.x, size.y, 1f);
            return sr;
        }

        public static SpriteRenderer AddRect(Transform parent, string name, Vector2 localPos, Vector2 size, Color color, int order)
        {
            var sr = NewRenderer(parent, name, localPos, order);
            sr.sprite = RoundRect;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = size;
            sr.color = color;
            return sr;
        }

        public static SpriteRenderer AddStitch(Transform parent, string name, Vector2 localPos, float size, Color color, int order)
        {
            var sr = NewRenderer(parent, name, localPos, order);
            sr.sprite = Stitch;
            sr.color = color;
            sr.transform.localScale = new Vector3(size, size, 1f);
            return sr;
        }

        /// Vertical gradient filling the given world rectangle.
        public static SpriteRenderer AddGradient(Transform parent, Rect rect, Color top, Color bottom, int order)
        {
            // Solid bottom color, with the top color fading out over it.
            var sr = AddRect(parent, "Background", rect.center, rect.size + Vector2.one, bottom, order);
            var overlay = NewRenderer(sr.transform, "Top", Vector2.zero, order + 1);
            overlay.sprite = Gradient;
            overlay.drawMode = SpriteDrawMode.Sliced;
            overlay.size = rect.size;
            overlay.color = top;
            return sr;
        }

        public static TextMesh AddText(Transform parent, string name, Vector2 localPos, float height, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.characterSize = height * 10f / tm.fontSize;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = order;
            return tm;
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, Vector2 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        // ---------- texture generation ----------

        static Sprite MakeCircle(int size)
        {
            var tex = NewTexture(size);
            float r = size / 2f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(r - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeSoftCircle(int size)
        {
            var tex = NewTexture(size);
            float r = size / 2f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * (3f - 2f * a) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRoundRect(int size, int radius)
        {
            var tex = MakeRoundRectTexture(size, radius);
            // 128 px per unit: corner radius ≈ 0.09 world units regardless of sliced size
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Texture2D MakeRoundRectTexture(int size, int radius)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // signed distance to a rounded square
                var p = new Vector2(Mathf.Abs(x + 0.5f - h), Mathf.Abs(y + 0.5f - h));
                var q = p - new Vector2(h - radius, h - radius);
                float dist = Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - dist) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// A knit stitch: two plump loops leaning into a "V", shaded so they look like yarn.
        static Sprite MakeStitch(int size)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            // Loops touch at the bottom and splay apart at the top, like the "V" of a knit stitch.
            const float a = 0.19f, b = 0.47f, tilt = 30f * Mathf.Deg2Rad;
            var left = new Vector2(0.37f, 0.52f);
            var right = new Vector2(0.63f, 0.52f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
                float best = 0f, value = 0f;
                for (int side = 0; side < 2; side++)
                {
                    var c = side == 0 ? left : right;
                    float ang = side == 0 ? tilt : -tilt; // left loop leans left at the top, right loop leans right
                    var d = p - c;
                    float cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    var local = new Vector2(d.x * cs + d.y * sn, -d.x * sn + d.y * cs);
                    float n = Mathf.Sqrt((local.x / a) * (local.x / a) + (local.y / b) * (local.y / b));
                    float cover = Mathf.Clamp01((1f - n) * a * size * 0.8f);
                    if (cover <= best) continue;
                    best = cover;
                    // rounded shading: bright centre, darker rim, plus a soft highlight on the upper-left
                    float shade = Mathf.Lerp(1f, 0.62f, n * n);
                    float highlight = Mathf.Clamp01(1f - Vector2.Distance(local, new Vector2(-a * 0.35f, b * 0.35f)) / (a * 0.9f));
                    value = Mathf.Clamp01(shade + highlight * 0.18f);
                }
                byte v = (byte)(value * 255);
                px[y * size + x] = new Color32(v, v, v, (byte)(best * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// Circular "restart" arrow, white on transparent.
        static Texture2D MakeRetryIcon(int size)
        {
            var tex = NewTexture(size);
            var px = new Color32[size * size];
            var c = new Vector2(size / 2f, size / 2f);
            float outer = size * 0.36f, inner = size * 0.24f;
            // arrow head at the end of the ring, pointing into the gap
            float headAngle = 60f * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Cos(headAngle), Mathf.Sin(headAngle));
            var tangent = new Vector2(-dir.y, dir.x); // counter-clockwise, into the gap
            var mid = c + dir * (outer + inner) / 2f;
            var tip = mid + tangent * size * 0.16f;
            var b1 = mid + dir * size * 0.17f;
            var b2 = mid - dir * size * 0.17f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                var d = p - c;
                float r = d.magnitude;
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (ang < 0f) ang += 360f;
                float a = 0f;
                // ring from the arrow head round to ~30° before it
                if (ang < 60f || ang > 100f)
                    a = Mathf.Clamp01(Mathf.Min(r - inner, outer - r) + 0.5f);
                if (InTriangle(p, tip, b1, b2)) a = 1f;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Side(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
            float d1 = Side(p, a, b), d2 = Side(p, b, c), d3 = Side(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }

        /// White with alpha fading from 1 (top) to 0 (bottom); stacked over a solid color it makes a gradient.
        static Sprite MakeGradient(int height)
        {
            var tex = new Texture2D(4, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color32[4 * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < 4; x++)
                px[y * 4 + x] = new Color32(255, 255, 255, (byte)(y * 255 / (height - 1)));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f), height, 0,
                SpriteMeshType.FullRect, Vector4.zero);
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
    }
}
