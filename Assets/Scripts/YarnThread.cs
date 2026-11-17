using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    /// One strand of yarn in the tangle.
    public class YarnThread : MonoBehaviour
    {
        const float Width = 0.24f;
        const float OutlineExtra = 0.07f;

        public int ColorIndex { get; private set; }
        public bool Taken { get; set; }
        public readonly List<YarnThread> CoveredBy = new List<YarnThread>();

        LineRenderer line, outline;
        Vector3[] points;
        Color color;

        public bool IsFree
        {
            get
            {
                foreach (var t in CoveredBy)
                    if (!t.Taken) return false;
                return true;
            }
        }

        public void Init(ThreadData data)
        {
            ColorIndex = data.Color;
            color = Palette.Get(data.Color);
            points = new Vector3[data.Points.Length];
            for (int i = 0; i < points.Length; i++) points[i] = data.Points[i];

            outline = MakeLine("Outline", Width + OutlineExtra, data.Layer * 2);
            line = MakeLine("Yarn", Width, data.Layer * 2 + 1);
            outline.startColor = outline.endColor = Palette.Outline;
            RefreshLook();
        }

        LineRenderer MakeLine(string name, float width, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Shapes.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.widthMultiplier = width;
            lr.numCapVertices = 6;
            lr.numCornerVertices = 4;
            lr.sortingOrder = order;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        /// Blocked threads are drawn dimmer so the player can see what is pullable.
        public void RefreshLook()
        {
            var c = IsFree ? color : Palette.Dim(color);
            line.startColor = line.endColor = c;
        }

        /// Animates the strand being wound onto a spool, then destroys itself.
        public IEnumerator ReelInto(Vector3 target, float duration)
        {
            line.sortingOrder = 3001;
            outline.sortingOrder = 3000;
            line.startColor = line.endColor = color;

            // Reel from the end nearest to the spool.
            if ((points[0] - target).sqrMagnitude > (points[points.Length - 1] - target).sqrMagnitude)
                System.Array.Reverse(points);

            var start = (Vector3[])points.Clone();
            int n = points.Length;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                for (int i = 0; i < n; i++)
                {
                    float local = Mathf.Clamp01(k * 1.6f - (i / (float)(n - 1)) * 0.6f);
                    local = local * local * (3f - 2f * local);
                    points[i] = Vector3.Lerp(start[i], target, local);
                }
                line.SetPositions(points);
                outline.SetPositions(points);
                line.widthMultiplier = Width * (1f - 0.4f * k);
                outline.widthMultiplier = (Width + OutlineExtra) * (1f - 0.4f * k);
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
