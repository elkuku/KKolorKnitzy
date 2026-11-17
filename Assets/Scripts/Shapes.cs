using UnityEngine;

namespace KnotAndSpool
{
    /// Code-generated sprites and materials, so the game needs no art assets.
    public static class Shapes
    {
        static Sprite circle, roundRect;
        static Material spriteMaterial;

        public static Sprite Circle => circle != null ? circle : (circle = MakeCircle(128));
        public static Sprite RoundRect => roundRect != null ? roundRect : (roundRect = MakeRoundRect(64, 12));

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

        public static SpriteRenderer AddCircle(Transform parent, string name, Vector2 localPos, float diameter, Color color, int order)
        {
            var sr = NewRenderer(parent, name, localPos, order);
            sr.sprite = Circle;
            sr.color = color;
            sr.transform.localScale = new Vector3(diameter, diameter, 1f);
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

        static SpriteRenderer NewRenderer(Transform parent, string name, Vector2 localPos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

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

        static Sprite MakeRoundRect(int size, int radius)
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
            // 128 px per unit: corner radius ≈ 0.09 world units regardless of sliced size
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 128f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        static Texture2D NewTexture(int size) =>
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
    }
}
