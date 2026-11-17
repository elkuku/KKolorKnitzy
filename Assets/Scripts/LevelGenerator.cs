using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    public class ThreadData
    {
        public int Color;
        public int Layer;              // higher = drawn on top
        public Vector2[] Points;
        public readonly List<int> CoveredBy = new List<int>(); // indices of threads lying on top of this one
    }

    public class LevelData
    {
        public int Number;
        public int Capacity;            // threads per spool
        public int Slots;               // spool slots in the tray
        public List<ThreadData> Threads;
        public List<List<int>> Columns; // spool colors per column, front first
    }

    /// Builds a random but always-solvable tangle for a level number (deterministic per level).
    public static class LevelGenerator
    {
        const int SamplesPerSegment = 8;
        const float CurveReach = 2.5f;
        const int MaxSlots = 5; // more doesn't fit on a portrait screen

        public static LevelData Generate(int level, Rect area)
        {
            var rng = new System.Random(level * 7919 + 17);
            int capacity = 3;
            int colorCount = Mathf.Clamp(3 + (level - 1) / 3, 3, 6);
            int spoolCount = Mathf.Clamp(3 + level, colorCount, 14);
            int columnCount = level < 4 ? 3 : 4;
            int slots = level < 8 ? 5 : 4;
            int threadCount = spoolCount * capacity;

            var threads = new List<ThreadData>(threadCount);
            for (int i = 0; i < threadCount; i++)
                threads.Add(new ThreadData { Layer = i, Points = MakeCurve(rng, area) });
            ComputeCoverage(threads);

            // Pick colors until the "pull from the top down" solution fits into the tray.
            List<int> spoolOrder = null;
            List<int> bestColors = null;
            int bestOpen = int.MaxValue;
            for (int attempt = 0; attempt < 300; attempt++)
            {
                var colors = new List<int>();
                for (int s = 0; s < spoolCount; s++)
                {
                    int c = s < colorCount ? s : rng.Next(colorCount);
                    for (int k = 0; k < capacity; k++) colors.Add(c);
                }
                Shuffle(colors, rng);
                for (int i = 0; i < threadCount; i++) threads[i].Color = colors[i];

                var order = PlanSpools(threads, capacity, out int maxOpen);
                if (maxOpen < bestOpen)
                {
                    bestOpen = maxOpen;
                    spoolOrder = order;
                    bestColors = colors;
                }
                if (maxOpen <= slots) break;
            }
            // Re-apply the colors that produced the best plan.
            for (int i = 0; i < threadCount; i++) threads[i].Color = bestColors[i];
            slots = Mathf.Clamp(bestOpen, slots, MaxSlots);

            // Deal spools round-robin so that the planned order is always reachable from the column fronts.
            var columns = new List<List<int>>();
            for (int c = 0; c < columnCount; c++) columns.Add(new List<int>());
            for (int i = 0; i < spoolOrder.Count; i++) columns[i % columnCount].Add(spoolOrder[i]);

            return new LevelData
            {
                Number = level,
                Capacity = capacity,
                Slots = slots,
                Threads = threads,
                Columns = columns,
            };
        }

        /// Simulates pulling threads strictly top-down; returns the order spools are needed in.
        static List<int> PlanSpools(List<ThreadData> threads, int capacity, out int maxOpen)
        {
            var order = new List<int>();
            var openCount = new Dictionary<int, int>(); // color -> threads on its open spool
            int open = 0;
            maxOpen = 0;
            for (int i = threads.Count - 1; i >= 0; i--)
            {
                int c = threads[i].Color;
                if (!openCount.TryGetValue(c, out int n) || n == 0)
                {
                    order.Add(c);
                    open++;
                    maxOpen = Mathf.Max(maxOpen, open);
                    n = 0;
                }
                n++;
                if (n == capacity)
                {
                    n = 0;
                    open--;
                }
                openCount[c] = n;
            }
            return order;
        }

        static Vector2[] MakeCurve(System.Random rng, Rect area)
        {
            // 4 control points scattered around a random centre, so each strand stays fairly local
            // (otherwise every strand crosses every other and only one is ever free).
            var ctrl = new Vector2[4];
            float cx = Mathf.Lerp(area.xMin, area.xMax, 0.15f + 0.7f * (float)rng.NextDouble());
            float cy = Mathf.Lerp(area.yMin, area.yMax, 0.15f + 0.7f * (float)rng.NextDouble());
            for (int i = 0; i < ctrl.Length; i++)
            {
                float x = cx + ((float)rng.NextDouble() * 2f - 1f) * CurveReach;
                float y = cy + ((float)rng.NextDouble() * 2f - 1f) * CurveReach;
                ctrl[i] = new Vector2(Mathf.Clamp(x, area.xMin, area.xMax), Mathf.Clamp(y, area.yMin, area.yMax));
            }

            var pts = new List<Vector2>();
            for (int seg = 0; seg < ctrl.Length - 1; seg++)
            {
                Vector2 p0 = ctrl[Mathf.Max(seg - 1, 0)], p1 = ctrl[seg], p2 = ctrl[seg + 1], p3 = ctrl[Mathf.Min(seg + 2, ctrl.Length - 1)];
                for (int s = 0; s < SamplesPerSegment; s++)
                    pts.Add(CatmullRom(p0, p1, p2, p3, s / (float)SamplesPerSegment));
            }
            pts.Add(ctrl[ctrl.Length - 1]);
            for (int i = 0; i < pts.Count; i++)
                pts[i] = new Vector2(Mathf.Clamp(pts[i].x, area.xMin, area.xMax), Mathf.Clamp(pts[i].y, area.yMin, area.yMax));
            return pts.ToArray();
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static void ComputeCoverage(List<ThreadData> threads)
        {
            for (int a = 0; a < threads.Count; a++)
            for (int b = a + 1; b < threads.Count; b++)
                if (Crosses(threads[a].Points, threads[b].Points))
                    threads[a].CoveredBy.Add(b); // b has the higher layer
        }

        static bool Crosses(Vector2[] a, Vector2[] b)
        {
            for (int i = 0; i < a.Length - 1; i++)
            for (int j = 0; j < b.Length - 1; j++)
                if (SegmentsIntersect(a[i], a[i + 1], b[j], b[j + 1]))
                    return true;
            return false;
        }

        static bool SegmentsIntersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            float d1 = Cross(q2 - q1, p1 - q1), d2 = Cross(q2 - q1, p2 - q1);
            float d3 = Cross(p2 - p1, q1 - p1), d4 = Cross(p2 - p1, q2 - p1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static void Shuffle<T>(List<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
