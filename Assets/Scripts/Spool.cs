using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    /// A wooden spool that winds a fixed number of fabric squares, getting fatter as it fills up.
    public class Spool : MonoBehaviour
    {
        static readonly Color HiddenYarn = Palette.Hex(0xB9B4AE);

        public int ColorIndex { get; private set; }
        public int Count { get; private set; }     // squares the spool holds when full
        public int Reserved { get; private set; }  // squares assigned (including ones still flying in)
        public int Filled { get; private set; }    // squares wound
        public bool Hidden { get; private set; }
        public bool InSlot { get; set; }
        public bool Completing { get; set; }

        public Vector3 ThreadOrigin => transform.position;

        readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
        readonly List<Color> baseColors = new List<Color>();
        readonly List<SpriteRenderer> rings = new List<SpriteRenderer>();
        Transform body, yarnRoot;
        SpriteRenderer yarn, band;
        TextMesh label;
        bool dim;

        public void Init(int colorIndex, int count, bool hidden)
        {
            ColorIndex = colorIndex;
            Count = count;
            Hidden = hidden;

            Shapes.AddShadow(transform, "Shadow", new Vector2(0f, -0.7f), new Vector2(1.6f, 0.5f), Palette.Shadow, 499);
            body = new GameObject("Body").transform;
            body.SetParent(transform, false);

            Part(Shapes.AddRect(body, "Core", Vector2.zero, new Vector2(0.46f, 1.1f), Palette.WoodLight, 500));
            band = Part(Shapes.AddRect(body, "Band", Vector2.zero, new Vector2(0.46f, 0.42f), Color.white, 501));

            yarnRoot = new GameObject("Yarn").transform;
            yarnRoot.SetParent(body, false);
            yarn = Part(Shapes.AddRect(yarnRoot, "Wound", Vector2.zero, new Vector2(1.04f, 0.84f), Color.white, 502));
            for (int i = 0; i < 4; i++)
                rings.Add(Part(Shapes.AddRect(yarnRoot, $"Ring{i}", new Vector2(0f, -0.27f + i * 0.18f), new Vector2(0.98f, 0.05f), Color.white, 503)));
            Part(Shapes.AddRect(yarnRoot, "Shine", new Vector2(-0.32f, 0.02f), new Vector2(0.1f, 0.6f), new Color(1f, 1f, 1f, 0.35f), 504));

            foreach (float y in new[] { 0.55f, -0.55f })
            {
                Part(Shapes.AddRect(body, "Cap", new Vector2(0f, y), new Vector2(1.3f, 0.24f), Palette.WoodDark, 505));
                Part(Shapes.AddRect(body, "CapShine", new Vector2(0f, y + 0.05f), new Vector2(1.12f, 0.06f), new Color(1f, 0.9f, 0.75f, 0.45f), 506));
            }

            Part(Shapes.AddCircle(body, "BadgeRim", new Vector2(0.5f, 0.6f), 0.6f, Palette.Frame, 507));
            Part(Shapes.AddCircle(body, "Badge", new Vector2(0.5f, 0.6f), 0.5f, Color.white, 508));
            label = Shapes.AddText(body, "Count", new Vector2(0.5f, 0.6f), 0.3f, Palette.Text, 509);

            ApplyYarnColor(hidden ? HiddenYarn : Palette.Get(colorIndex));
            UpdateLabel();
            yarnRoot.gameObject.SetActive(false); // starts empty
        }

        SpriteRenderer Part(SpriteRenderer sr)
        {
            parts.Add(sr);
            baseColors.Add(sr.color);
            return sr;
        }

        void SetBase(SpriteRenderer sr, Color c)
        {
            int i = parts.IndexOf(sr);
            baseColors[i] = c;
            sr.color = dim ? Darken(c) : c;
        }

        void ApplyYarnColor(Color c)
        {
            SetBase(yarn, c);
            SetBase(band, c);
            foreach (var r in rings) SetBase(r, Palette.Shade(c, 0.8f));
        }

        static Color Darken(Color c) => new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, c.a);

        void UpdateLabel() => label.text = Hidden ? "?" : (Count - Reserved).ToString();

        /// Spools that can't be tapped yet are drawn darker.
        public void SetReachable(bool reachable)
        {
            if (dim == !reachable) return;
            dim = !reachable;
            for (int i = 0; i < parts.Count; i++)
                parts[i].color = dim ? Darken(baseColors[i]) : baseColors[i];
            label.color = dim ? Darken(Palette.Text) : Palette.Text;
        }

        public IEnumerator Reveal()
        {
            Hidden = false;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                body.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.15f), 1f, 1f);
                yield return null;
            }
            ApplyYarnColor(Palette.Get(ColorIndex));
            UpdateLabel();
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                body.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / 0.15f), 1f, 1f);
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        public void Reserve()
        {
            Reserved++;
            UpdateLabel();
        }

        public void AddYarn()
        {
            Filled++;
            float f = Filled / (float)Count;
            yarnRoot.gameObject.SetActive(true);
            yarnRoot.localScale = new Vector3(Mathf.Lerp(0.5f, 1f, f), Mathf.Lerp(0.75f, 1f, f), 1f);
            StartCoroutine(Bounce());
        }

        public IEnumerator MoveTo(Vector3 target, float scale, float duration)
        {
            var start = transform.position;
            var startScale = transform.localScale.x;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                k = 1f - (1f - k) * (1f - k);
                // small hop on the way
                transform.position = Vector3.Lerp(start, target, k) + Vector3.up * (Mathf.Sin(k * Mathf.PI) * 0.6f);
                transform.localScale = Vector3.one * Mathf.Lerp(startScale, scale, k);
                yield return null;
            }
            transform.position = target;
            transform.localScale = Vector3.one * scale;
            yield return Bounce();
        }

        public IEnumerator Bounce()
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                float k = t / 0.25f;
                float s = 1f + Mathf.Sin(k * Mathf.PI) * 0.12f * (1f - k);
                body.localScale = new Vector3(1f / s, s, 1f);
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        /// Full spool: pop, then shrink away.
        public IEnumerator Complete()
        {
            var start = transform.position;
            float baseScale = transform.localScale.x;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1f, 1.2f, t / 0.15f);
                yield return null;
            }
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                transform.localScale = Vector3.one * baseScale * Mathf.Lerp(1.2f, 0f, k);
                transform.position = start + Vector3.up * (0.8f * k);
                yield return null;
            }
        }

        public IEnumerator Shake()
        {
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                body.localPosition = Vector3.right * (Mathf.Sin(t * 60f) * 0.08f);
                yield return null;
            }
            body.localPosition = Vector3.zero;
        }
    }
}
