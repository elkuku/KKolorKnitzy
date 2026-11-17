using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    public class SpoolSpec
    {
        public readonly int Color;
        public readonly int Count; // stitches of yarn on this spool
        public bool Hidden;        // color shown as "?" until the spool becomes reachable

        public SpoolSpec(int color, int count)
        {
            Color = color;
            Count = count;
        }
    }

    public class LevelData
    {
        public int Number;
        public int Width, Height;
        public int[,] Cells;                   // [row, col] palette index, row 0 = bottom (unravelled first)
        public int Slots;                      // spool slots in the tray
        public int BoardWidth, BoardHeight;
        public bool[,] Walls;                  // [row, col] blocked board cells
        public SpoolSpec[,] Board;             // [row, col] spool or null (empty)
        public int Jitter;                     // how deeply needed spools are buried (difficulty, for reports)
    }

    /// Builds a level: a patchwork fabric plus a spool board that is guaranteed solvable.
    ///
    /// Rules: each column of the fabric unravels from the bottom, so the open squares are the lowest
    /// remaining square of each column. A spool in the tray winds open squares of its color until it is full.
    public static class LevelGenerator
    {
        const int MaxSlots = 5;
        public const int BoardWidth = 6;

        // Yarn colors used for fabrics (palette indices): red, blue, yellow, green, purple, orange, pink, teal, light blue, brown.
        static readonly int[] FabricColors = { 0, 1, 2, 3, 4, 5, 6, 7, 11, 9 };

        public static LevelData Generate(int level)
        {
            var rng = new System.Random(level * 7919 + 17);
            int w = level <= 3 ? 6 : 7;
            int h = Mathf.Min(8 + level, 14);
            int colorCount = Mathf.Clamp(3 + (level - 1) / 2, 3, 7);
            int capacity = level <= 3 ? 5 : 6;
            int slots = level == 1 ? 5 : level <= 4 ? 4 : 3;
            // How far needed spools get buried behind ones that aren't needed yet.
            int window = level == 1 ? 0 : level == 2 ? 1 : level <= 4 ? 2 : 3;
            float hiddenChance = level < 3 ? 0f : level < 8 ? 0.3f : 0.4f;

            for (int attempt = 0; ; attempt++)
            {
                var cells = MakeFabric(rng, w, h, PickColors(rng, colorCount));
                var sequence = PlanSpools(cells, capacity, out int maxOpen);
                bool lastChance = attempt >= 80;
                if (maxOpen > slots && !lastChance) continue;
                int traySlots = Mathf.Clamp(Mathf.Max(slots, maxOpen), 1, MaxSlots);

                var layout = MakeBoardLayout(level, sequence.Count, rng, out var walls, out int extra);
                // Try the hardest dealing first; fall back to gentler ones. Every candidate is verified by
                // playing it through with the game's own winding rules.
                for (int jitter = window; jitter >= 0; jitter--)
                {
                    for (int t = 0; t < (jitter == 0 ? 1 : 15); t++)
                    {
                        var dealt = Jitter(sequence, jitter, rng);
                        if (!Simulate(cells, dealt, traySlots) && !(lastChance && jitter == 0)) continue;

                        var data = new LevelData
                        {
                            Number = level,
                            Width = w,
                            Height = h,
                            Cells = cells,
                            Slots = traySlots,
                            BoardWidth = BoardWidth,
                            BoardHeight = walls.GetLength(0),
                            Walls = walls,
                            Board = new SpoolSpec[walls.GetLength(0), BoardWidth],
                            Jitter = jitter,
                        };
                        for (int i = 0; i < dealt.Count; i++)
                        {
                            var cell = layout[extra + i];
                            dealt[i].Hidden = cell.y > 0 && rng.NextDouble() < hiddenChance;
                            data.Board[cell.y, cell.x] = dealt[i];
                        }
                        return data;
                    }
                }
            }
        }

        /// Shuffles the needed order locally: each spool may move up to `window` places later.
        static List<SpoolSpec> Jitter(List<SpoolSpec> sequence, int window, System.Random rng)
        {
            var keys = new double[sequence.Count];
            var idx = new List<int>();
            for (int i = 0; i < sequence.Count; i++)
            {
                keys[i] = i + rng.NextDouble() * (window + 1);
                idx.Add(i);
            }
            idx.Sort((x, y) => keys[x].CompareTo(keys[y]));
            var result = new List<SpoolSpec>();
            foreach (int i in idx) result.Add(new SpoolSpec(sequence[i].Color, sequence[i].Count));
            return result;
        }

        /// Column the spool in tray slot `slot` winds from: a column whose bottom square has its color, closest
        /// to the spool (in normalized width, so it doesn't depend on screen size). Shared with the game.
        public static int PickColumn(int columns, int slotCount, int slot, int color, System.Func<int, int> bottomColor)
        {
            // Exact integer distance |(c+½)/columns − (slot+½)/slotCount| scaled by 2·columns·slotCount, so ties
            // (e.g. two columns symmetric around the spool) always go to the leftmost column on every platform.
            int best = -1;
            int bestDist = int.MaxValue;
            for (int c = 0; c < columns; c++)
            {
                if (bottomColor(c) != color) continue;
                int dist = System.Math.Abs((2 * c + 1) * slotCount - (2 * slot + 1) * columns);
                if (dist < bestDist)
                {
                    best = c;
                    bestDist = dist;
                }
            }
            return best;
        }

        /// Plays the level the "intended" way: whenever nothing can be wound, take the next spool in board
        /// order into the first free slot. Returns false if the tray clogs.
        public static bool Simulate(int[,] cells, List<SpoolSpec> takeOrder, int slots)
        {
            int h = cells.GetLength(0), w = cells.GetLength(1);
            var next = new int[w];
            var trayColor = new int[slots];
            var trayNeed = new int[slots];
            for (int i = 0; i < slots; i++) trayColor[i] = -1;
            int left = h * w, k = 0;
            System.Func<int, int> bottom = c => next[c] < h ? cells[next[c], c] : -1;

            while (true)
            {
                bool progress = true;
                while (progress)
                {
                    progress = false;
                    for (int s = 0; s < slots; s++)
                    {
                        if (trayColor[s] < 0) continue;
                        int col = PickColumn(w, slots, s, trayColor[s], bottom);
                        if (col < 0) continue;
                        next[col]++;
                        left--;
                        progress = true;
                        if (--trayNeed[s] == 0) trayColor[s] = -1;
                    }
                }
                if (left == 0) return true;
                if (k >= takeOrder.Count) return false;

                int free = System.Array.IndexOf(trayColor, -1);
                if (free < 0) return false;
                trayColor[free] = takeOrder[k].Color;
                trayNeed[free] = takeOrder[k].Count;
                k++;
            }
        }

        static int[] PickColors(System.Random rng, int count)
        {
            var pool = new List<int>(FabricColors);
            var picked = new int[count];
            for (int i = 0; i < count; i++)
            {
                int j = rng.Next(pool.Count);
                picked[i] = pool[j];
                pool.RemoveAt(j);
            }
            return picked;
        }

        /// Patchwork: vertical runs of 1–3 squares per color, often continuing the left neighbour's color so
        /// patches form wider blocks.
        static int[,] MakeFabric(System.Random rng, int w, int h, int[] colors)
        {
            var cells = new int[h, w];
            for (int c = 0; c < w; c++)
            {
                int r = 0;
                while (r < h)
                {
                    int run = 1 + rng.Next(3);
                    int color = c > 0 && rng.NextDouble() < 0.55 ? cells[r, c - 1] : colors[rng.Next(colors.Length)];
                    for (int k = 0; k < run && r < h; k++, r++) cells[r, c] = color;
                }
            }
            return cells;
        }

        /// Board shape (walls) plus a removal order: each cell in the list is reachable once all earlier cells
        /// are empty. The first `extra` cells start empty; spools are dealt onto the rest in order.
        static List<Vector2Int> MakeBoardLayout(int level, int spoolCount, System.Random rng, out bool[,] walls, out int extra)
        {
            int bw = BoardWidth;
            int wallCount = level <= 2 ? 0 : Mathf.Min(1 + level / 2, 6);
            int bh = Mathf.CeilToInt((spoolCount + wallCount) / (float)bw);
            extra = bw * bh - spoolCount - wallCount;

            walls = null;
            for (int attempt = 0; attempt < 200; attempt++)
            {
                walls = new bool[bh, bw];
                if (bh < 2) break;
                for (int k = 0; k < wallCount; k++)
                {
                    int r = 1 + rng.Next(bh - 1), c = rng.Next(bw);
                    if (walls[r, c]) k--;
                    else walls[r, c] = true;
                }
                if (AllConnected(walls)) break;
                if (attempt == 199) walls = new bool[bh, bw];
            }

            var occupied = new bool[bh, bw];
            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
                occupied[r, c] = !walls[r, c];

            var order = new List<Vector2Int>();
            var candidates = new List<Vector2Int>();
            while (true)
            {
                var reach = Reachable(walls, occupied);
                candidates.Clear();
                for (int r = 0; r < bh; r++)
                for (int c = 0; c < bw; c++)
                    if (occupied[r, c] && reach[r, c]) candidates.Add(new Vector2Int(c, r));
                if (candidates.Count == 0) break;
                var pick = candidates[rng.Next(candidates.Count)];
                occupied[pick.y, pick.x] = false;
                order.Add(pick);
            }
            return order;
        }

        /// A cell is reachable if it is in the top row, or touches an empty cell connected to the top through
        /// empty cells.
        public static bool[,] Reachable(bool[,] walls, bool[,] occupied)
        {
            int bh = walls.GetLength(0), bw = walls.GetLength(1);
            var open = new bool[bh, bw]; // empty and connected to the top
            var queue = new Queue<Vector2Int>();
            for (int c = 0; c < bw; c++)
                if (!walls[0, c] && !occupied[0, c])
                {
                    open[0, c] = true;
                    queue.Enqueue(new Vector2Int(c, 0));
                }
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in Directions)
                {
                    var q = p + d;
                    if (q.x < 0 || q.y < 0 || q.x >= bw || q.y >= bh) continue;
                    if (open[q.y, q.x] || walls[q.y, q.x] || occupied[q.y, q.x]) continue;
                    open[q.y, q.x] = true;
                    queue.Enqueue(q);
                }
            }

            var reach = new bool[bh, bw];
            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
            {
                if (walls[r, c]) continue;
                bool ok = r == 0;
                foreach (var d in Directions)
                {
                    int rr = r + d.y, cc = c + d.x;
                    if (rr >= 0 && cc >= 0 && rr < bh && cc < bw && open[rr, cc]) ok = true;
                }
                reach[r, c] = ok;
            }
            return reach;
        }

        static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        static bool AllConnected(bool[,] walls)
        {
            int bh = walls.GetLength(0), bw = walls.GetLength(1);
            var empty = new bool[bh, bw]; // pretend everything is empty: is every cell connected to the top?
            var reach = Reachable(walls, empty);
            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
                if (!walls[r, c] && !reach[r, c]) return false;
            return true;
        }

        /// Plays the level greedily: wind whatever an open spool can; when stuck, open a spool for the color
        /// with the most open squares. Returns the spools in the order they were needed.
        static List<SpoolSpec> PlanSpools(int[,] cells, int capacity, out int maxOpen)
        {
            int h = cells.GetLength(0), w = cells.GetLength(1);
            var next = new int[w];
            var unassigned = new Dictionary<int, int>();
            foreach (int c in cells)
                unassigned[c] = unassigned.TryGetValue(c, out int n) ? n + 1 : 1;

            var open = new Dictionary<int, int>(); // color -> yarn left on its open spool
            var sequence = new List<SpoolSpec>();
            int left = h * w;
            maxOpen = 0;

            while (left > 0)
            {
                bool knitted = false;
                for (int col = 0; col < w && !knitted; col++)
                {
                    if (next[col] >= h) continue;
                    int color = cells[next[col], col];
                    if (!open.TryGetValue(color, out int yarn)) continue;

                    next[col]++;
                    left--;
                    if (--yarn == 0) open.Remove(color);
                    else open[color] = yarn;
                    knitted = true;
                }
                if (knitted) continue;

                int best = -1, bestCount = -1;
                var openCounts = new int[Palette.Count];
                for (int col = 0; col < w; col++)
                    if (next[col] < h) openCounts[cells[next[col], col]]++;
                for (int color = 0; color < openCounts.Length; color++)
                {
                    if (openCounts[color] > bestCount)
                    {
                        best = color;
                        bestCount = openCounts[color];
                    }
                }

                int count = Mathf.Min(capacity, unassigned[best]);
                unassigned[best] -= count;
                open[best] = count;
                sequence.Add(new SpoolSpec(best, count));
                maxOpen = Mathf.Max(maxOpen, open.Count);
            }
            return sequence;
        }
    }
}
