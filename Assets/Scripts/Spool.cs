using System.Collections;
using UnityEngine;

namespace KnotAndSpool
{
    /// A wooden spool of one color that collects a fixed number of threads.
    public class Spool : MonoBehaviour
    {
        public const float TapRadius = 0.8f;

        public int ColorIndex { get; private set; }
        public int Capacity { get; private set; }
        public int Reserved { get; set; }     // threads assigned (including ones still flying in)
        public int Filled { get; private set; }
        public bool InSlot { get; set; }      // arrived in the tray and may pull threads
        public bool Completing { get; set; }
        public float NextPullTime { get; set; }

        SpriteRenderer yarn;
        const float YarnHeight = 0.9f;

        public Vector3 YarnCenter => transform.position;

        public void Init(int colorIndex, int capacity)
        {
            ColorIndex = colorIndex;
            Capacity = capacity;
            var c = Palette.Get(colorIndex);

            Shapes.AddRect(transform, "Core", Vector2.zero, new Vector2(0.5f, 1.05f), Palette.WoodLight, 500);
            Shapes.AddRect(transform, "Label", Vector2.zero, new Vector2(0.5f, 0.3f), c, 501);
            yarn = Shapes.AddRect(transform, "Yarn", Vector2.zero, new Vector2(0.5f, YarnHeight), c, 502);
            yarn.enabled = false;
            Shapes.AddRect(transform, "CapTop", new Vector2(0, 0.6f), new Vector2(1.2f, 0.24f), Palette.WoodDark, 503);
            Shapes.AddRect(transform, "CapBottom", new Vector2(0, -0.6f), new Vector2(1.2f, 0.24f), Palette.WoodDark, 503);
        }

        public void AddYarn()
        {
            Filled++;
            yarn.enabled = true;
            float f = Filled / (float)Capacity;
            yarn.size = new Vector2(Mathf.Lerp(0.6f, 1.05f, f), YarnHeight);
        }

        public IEnumerator MoveTo(Vector3 target, float duration)
        {
            var start = transform.position;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                k = 1f - (1f - k) * (1f - k);
                transform.position = Vector3.Lerp(start, target, k);
                yield return null;
            }
            transform.position = target;
        }

        /// Little pop, then shrink away.
        public IEnumerator Complete()
        {
            var start = transform.position;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.2f, t / 0.15f);
                yield return null;
            }
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                float k = t / 0.3f;
                transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0f, k);
                transform.position = start + Vector3.up * (0.8f * k);
                yield return null;
            }
        }

        public IEnumerator Shake()
        {
            var start = transform.position;
            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                transform.position = start + Vector3.right * (Mathf.Sin(t * 60f) * 0.08f);
                yield return null;
            }
            transform.position = start;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);
    }
}
