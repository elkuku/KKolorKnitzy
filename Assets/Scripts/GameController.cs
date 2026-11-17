using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    /// Unravel a patchwork fabric: tap reachable spools on the board to move them to the tray; spools in the
    /// tray wind the bottom square of any fabric column with their color until they are full. Columns slide
    /// down as squares are unravelled, and new squares scroll into view from the top.
    public class GameController : MonoBehaviour
    {
        enum State { Playing, Won, Lost }

        const string LevelKey = "unravelLevel";
        const float SlotSpacing = 1.7f;
        const float BoardCell = 1.38f;
        const float BoardSpoolScale = 0.82f;
        const float WindDuration = 0.45f;
        const float WindStagger = 0.12f;
        const int VisibleRowsMax = 9;

        // Spawned at startup if the scene has no controller, so any scene works.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameController>() == null)
                new GameObject("Game").AddComponent<GameController>();
        }

#if ENABLE_INPUT_SYSTEM
        // EnhancedTouch also reports touches that begin and end within a single frame (quick taps).
        void OnEnable() => UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        void OnDisable() => UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
#endif

        /// One knitted square of the fabric.
        class Square
        {
            public int Color;
            public Transform Transform;
            public SpriteRenderer Base;
            public SpriteRenderer[] Stitches;
            public Color BaseColor, StitchColor;

            public void SetAlpha(float a)
            {
                Base.color = new Color(BaseColor.r, BaseColor.g, BaseColor.b, a);
                foreach (var s in Stitches) s.color = new Color(StitchColor.r, StitchColor.g, StitchColor.b, a);
            }

            public void SetVisible(bool visible)
            {
                Base.enabled = visible;
                foreach (var s in Stitches) s.enabled = visible;
            }

            public void SetOrder(int order)
            {
                Base.sortingOrder = order;
                foreach (var s in Stitches) s.sortingOrder = order + 1;
            }
        }

        int level;
        State state;
        float overlayAt;
        LevelData data;
        Camera cam;
        Transform root;
        int inFlight;

        // fabric
        List<Square>[] fabric;   // per column, bottom square first
        float cellSize, windowBottom, windowTop, fabricLeft;
        int remaining, total;

        // tray
        Spool[] slots;
        Vector3[] slotPositions;

        // board
        Spool[,] board;
        bool[,] reachable;
        Vector3[,] boardPos;

        void Start()
        {
            Application.targetFrameRate = 60;
            level = Mathf.Max(1, PlayerPrefs.GetInt(LevelKey, 1));
            BuildLevel();
        }

        // ---------- level setup ----------

        void BuildLevel()
        {
            StopAllCoroutines();
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Level").transform;
            inFlight = 0;
            state = State.Playing;

            float size = SetupCamera();
            float worldWidth = 2f * size * cam.aspect;
            float worldPerPixel = 2f * size / Screen.height;
            float bottom = -size + 0.4f + Screen.safeArea.yMin * worldPerPixel;
            float top = size - HudHeightPixels() * worldPerPixel;

            Shapes.AddGradient(root, new Rect(-worldWidth / 2f, -size, worldWidth, 2f * size),
                Palette.BackgroundTop, Palette.BackgroundBottom, -100);

            data = LevelGenerator.Generate(level);

            // Bottom-up: board, tray, then the fabric window fills the rest up to the HUD.
            float boardTop = bottom + data.BoardHeight * BoardCell + 0.2f;
            BuildBoard(boardTop);
            float slotY = boardTop + 1.45f;
            BuildTray(slotY);
            BuildFabric(Rect.MinMaxRect(-4.2f, slotY + 1.5f, 4.2f, top - 0.9f));
            RefreshBoard();
        }

        float SetupCamera()
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(8f, 5.2f / cam.aspect);
            cam.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            return cam.orthographicSize;
        }

        void BuildFabric(Rect area)
        {
            int w = data.Width, h = data.Height;
            int visibleRows = Mathf.Min(h, VisibleRowsMax);
            cellSize = Mathf.Min(area.width / w, area.height / visibleRows);
            float windowW = cellSize * w, windowH = cellSize * visibleRows;
            // The window hangs from the top of the area; the fabric's bottom edge sits at the window bottom.
            windowTop = area.yMax;
            windowBottom = windowTop - windowH;
            fabricLeft = -windowW / 2f;
            var center = new Vector2(0f, (windowTop + windowBottom) / 2f);

            // Frame, canvas and a wooden rod the fabric hangs from.
            var frame = new Vector2(windowW + 0.4f, windowH + 0.4f);
            Shapes.AddShadow(root, "FrameShadow", center + new Vector2(0f, -0.25f), frame * 1.12f, Palette.Shadow, -22);
            Shapes.AddRect(root, "Frame", center, frame, Palette.Frame, -21);
            Shapes.AddRect(root, "Canvas", center, new Vector2(windowW + 0.1f, windowH + 0.1f), Palette.Panel, -20);
            Shapes.AddRect(root, "Rod", new Vector2(0f, windowTop + 0.35f), new Vector2(windowW + 1.2f, 0.36f), Palette.WoodDark, 40);
            Shapes.AddRect(root, "RodShine", new Vector2(0f, windowTop + 0.42f), new Vector2(windowW + 1.0f, 0.08f), new Color(1f, 0.9f, 0.75f, 0.45f), 41);
            foreach (float x in new[] { -windowW / 2f - 0.6f, windowW / 2f + 0.6f })
                Shapes.AddCircle(root, "RodKnob", new Vector2(x, windowTop + 0.35f), 0.5f, Palette.WoodDark, 42);
            // A curtain strip above the window hides squares that are still scrolling in.
            Shapes.AddRect(root, "TopCover", new Vector2(0f, windowTop + 0.12f), new Vector2(windowW + 0.4f, 0.26f), Palette.Frame, 39);

            fabric = new List<Square>[w];
            remaining = total = w * h;
            for (int c = 0; c < w; c++)
            {
                fabric[c] = new List<Square>();
                for (int r = 0; r < h; r++)
                {
                    var color = Palette.Get(data.Cells[r, c]);
                    var go = new GameObject("Square");
                    go.transform.SetParent(root, false);
                    go.transform.position = SquareTarget(c, r);
                    var sq = new Square
                    {
                        Color = data.Cells[r, c],
                        Transform = go.transform,
                        BaseColor = Palette.Shade(color, 0.74f),
                        StitchColor = color,
                    };
                    sq.Base = Shapes.AddRect(go.transform, "Base", Vector2.zero, Vector2.one * cellSize * 0.96f, sq.BaseColor, 0);
                    // 2×2 small stitches per square give a finer knitted texture
                    sq.Stitches = new SpriteRenderer[4];
                    float q = cellSize / 4f;
                    for (int i = 0; i < 4; i++)
                    {
                        var offset = new Vector2(i % 2 == 0 ? -q : q, i < 2 ? q : -q);
                        sq.Stitches[i] = Shapes.AddStitch(go.transform, "Stitch", offset, cellSize * 0.52f, sq.StitchColor, 1);
                    }
                    fabric[c].Add(sq);
                }
            }
            UpdateFabric(true);
        }

        Vector3 SquareTarget(int col, int index) =>
            new Vector3(fabricLeft + (col + 0.5f) * cellSize, windowBottom + (index + 0.5f) * cellSize, 0f);

        /// Slides squares toward their place in the column and fades those outside the window.
        void UpdateFabric(bool snap)
        {
            if (fabric == null) return;
            float follow = snap ? 1f : 1f - Mathf.Exp(-14f * Time.deltaTime);
            for (int c = 0; c < fabric.Length; c++)
            {
                var column = fabric[c];
                for (int i = 0; i < column.Count; i++)
                {
                    var sq = column[i];
                    var target = SquareTarget(c, i);
                    var p = Vector3.Lerp(sq.Transform.position, target, follow);
                    sq.Transform.position = p;
                    bool visible = p.y - cellSize * 0.5f < windowTop;
                    sq.SetVisible(visible);
                    if (visible) sq.SetAlpha(Mathf.Clamp01((windowTop - (p.y - cellSize * 0.5f)) / cellSize));
                }
            }
        }

        void BuildTray(float slotY)
        {
            slots = new Spool[data.Slots];
            slotPositions = new Vector3[data.Slots];
            float width = data.Slots * SlotSpacing + 0.3f;
            Shapes.AddShadow(root, "ShelfShadow", new Vector2(0f, slotY - 0.25f), new Vector2(width * 1.1f, 2.2f), Palette.Shadow, -16);
            Shapes.AddRect(root, "Shelf", new Vector2(0f, slotY), new Vector2(width, 1.9f), Palette.Shelf, -15);
            for (int i = 0; i < data.Slots; i++)
            {
                slotPositions[i] = new Vector3((i - (data.Slots - 1) / 2f) * SlotSpacing, slotY, 0f);
                Shapes.AddRect(root, $"Slot{i}", slotPositions[i], new Vector2(1.45f, 1.6f), Palette.SlotEmpty, -14);
            }
        }

        void BuildBoard(float boardTop)
        {
            int bw = data.BoardWidth, bh = data.BoardHeight;
            board = new Spool[bh, bw];
            boardPos = new Vector3[bh, bw];
            float left = -(bw - 1) / 2f * BoardCell;
            var size = new Vector2(bw * BoardCell + 0.3f, bh * BoardCell + 0.3f);
            var center = new Vector2(0f, boardTop - bh * BoardCell / 2f);
            Shapes.AddRect(root, "Board", center, size, Palette.Frame, -13);

            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
            {
                var p = new Vector3(left + c * BoardCell, boardTop - (r + 0.5f) * BoardCell, 0f);
                boardPos[r, c] = p;
                if (data.Walls[r, c])
                {
                    Shapes.AddRect(root, "Wall", p, Vector2.one * (BoardCell - 0.08f), Palette.WoodDark, -12);
                    Shapes.AddRect(root, "WallShine", p + new Vector3(0f, 0.38f, 0f), new Vector2(BoardCell - 0.3f, 0.08f), new Color(1f, 0.9f, 0.75f, 0.35f), -11);
                    continue;
                }
                Shapes.AddRect(root, "Tile", p, Vector2.one * (BoardCell - 0.08f), Palette.Panel, -12);

                var spec = data.Board[r, c];
                if (spec == null) continue;
                var go = new GameObject("Spool");
                go.transform.SetParent(root, false);
                go.transform.position = p;
                go.transform.localScale = Vector3.one * BoardSpoolScale;
                var spool = go.AddComponent<Spool>();
                spool.Init(spec.Color, spec.Count, spec.Hidden);
                board[r, c] = spool;
            }
        }

        // ---------- gameplay ----------

        void Update()
        {
            UpdateFabric(false);
            if (state == State.Playing)
            {
                if (TryGetTap(out var screenPos))
                    OnTap(cam.ScreenToWorldPoint(screenPos));

                WindStep();
                CheckEnd();
            }
            LogState();
        }

        // ---------- playtest log ----------

        string lastLoggedState;

        /// Writes the settled game state as one "KS_STATE {json}" log line whenever it changes, so an external
        /// playtester (tools/playtest.py over adb logcat) can play the game. Screen coordinates are top-left based.
        void LogState()
        {
            if (data == null) return;
            var sb = new System.Text.StringBuilder("KS_STATE {");
            bool overlay = state != State.Playing && Time.time >= overlayAt;
            float u = HudUnit();
            sb.Append($"\"level\":{level},\"state\":\"{state}\",\"overlay\":{(overlay ? 1 : 0)},\"busy\":{(inFlight > 0 ? 1 : 0)},");
            sb.Append($"\"boardSize\":[{data.BoardWidth},{data.BoardHeight}],\"walls\":[");
            bool firstWall = true;
            for (int r = 0; r < data.BoardHeight; r++)
            for (int c = 0; c < data.BoardWidth; c++)
            {
                if (!data.Walls[r, c]) continue;
                if (!firstWall) sb.Append(',');
                firstWall = false;
                sb.Append($"[{r},{c}]");
            }
            sb.Append("],");
            sb.Append($"\"screen\":[{Screen.width},{Screen.height}],");
            sb.Append($"\"button\":[{Screen.width / 2},{(int)(Screen.height * 0.32f + u * 8.1f)}],");
            sb.Append($"\"remaining\":{remaining},\"columns\":[");
            for (int c = 0; c < fabric.Length; c++)
            {
                if (c > 0) sb.Append(',');
                sb.Append('[');
                for (int i = 0; i < fabric[c].Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(fabric[c][i].Color);
                }
                sb.Append(']');
            }
            sb.Append("],\"tray\":[");
            for (int i = 0; i < slots.Length; i++)
            {
                if (i > 0) sb.Append(',');
                var s = slots[i];
                sb.Append(s == null ? "null" : $"[{s.ColorIndex},{s.Count - s.Reserved}]");
            }
            sb.Append("],\"board\":[");
            bool first = true;
            for (int r = 0; r < data.BoardHeight; r++)
            for (int c = 0; c < data.BoardWidth; c++)
            {
                var s = board[r, c];
                if (s == null) continue;
                var p = cam.WorldToScreenPoint(boardPos[r, c]);
                if (!first) sb.Append(',');
                first = false;
                // [shown color, count, reachable, screen x, screen y, true color, row, col]
                sb.Append($"[{(s.Hidden ? -1 : s.ColorIndex)},{s.Count},{(reachable[r, c] ? 1 : 0)},{(int)p.x},{(int)(Screen.height - p.y)},{s.ColorIndex},{r},{c}]");
            }
            sb.Append("]}");

            var line = sb.ToString();
            if (line == lastLoggedState) return;
            lastLoggedState = line;
            Debug.Log(line);
        }

        void OnTap(Vector2 world)
        {
            for (int r = 0; r < data.BoardHeight; r++)
            for (int c = 0; c < data.BoardWidth; c++)
            {
                var spool = board[r, c];
                if (spool == null) continue;
                var d = world - (Vector2)boardPos[r, c];
                if (Mathf.Abs(d.x) > BoardCell / 2f || Mathf.Abs(d.y) > BoardCell / 2f) continue;

                int slot = System.Array.IndexOf(slots, null);
                if (!reachable[r, c] || slot < 0)
                {
                    StartCoroutine(spool.Shake());
                    return;
                }
                board[r, c] = null;
                slots[slot] = spool;
                StartCoroutine(SendToSlot(spool, slot));
                RefreshBoard();
                return;
            }
        }

        void RefreshBoard()
        {
            int bh = data.BoardHeight, bw = data.BoardWidth;
            var occupied = new bool[bh, bw];
            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
                occupied[r, c] = board[r, c] != null;
            reachable = LevelGenerator.Reachable(data.Walls, occupied);

            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
            {
                var spool = board[r, c];
                if (spool == null) continue;
                spool.SetReachable(reachable[r, c]);
                if (reachable[r, c] && spool.Hidden) StartCoroutine(spool.Reveal());
            }
        }

        IEnumerator SendToSlot(Spool spool, int slot)
        {
            inFlight++;
            spool.SetReachable(true);
            if (spool.Hidden) StartCoroutine(spool.Reveal());
            yield return spool.MoveTo(slotPositions[slot], 1f, 0.3f);
            spool.InSlot = true;
            inFlight--;
        }

        float nextWindTick;

        /// One shared tick: each tray slot, in slot order, winds one square. This is exactly the order
        /// LevelGenerator.Simulate (and tools/playtest.py) assume, so verified levels play out the same way.
        void WindStep()
        {
            if (Time.time < nextWindTick) return;
            bool wound = false;
            foreach (var spool in slots)
            {
                if (spool == null || !spool.InSlot || spool.Completing || spool.Hidden) continue;
                if (spool.Reserved >= spool.Count) continue;

                int col = FindOpenColumn(spool);
                if (col < 0) continue;

                var square = fabric[col][0];
                fabric[col].RemoveAt(0); // the rest of the column slides down
                spool.Reserve();
                StartCoroutine(Unravel(square, spool));
                wound = true;
            }
            if (wound) nextWindTick = Time.time + WindStagger;
        }

        /// Column the spool winds from next (same rule the level generator verifies levels with).
        int FindOpenColumn(Spool spool)
        {
            int slot = System.Array.IndexOf(slots, spool);
            return LevelGenerator.PickColumn(fabric.Length, slots.Length, slot, spool.ColorIndex,
                c => fabric[c].Count > 0 ? fabric[c][0].Color : -1);
        }

        /// The square is pulled apart into a strand that winds onto the spool.
        IEnumerator Unravel(Square square, Spool spool)
        {
            inFlight++;
            var color = Palette.Get(square.Color);
            var start = square.Transform.position;
            var end = spool.ThreadOrigin;
            var control = (start + end) / 2f + new Vector3((start.x - end.x) * 0.3f, 0.4f, 0f);
            square.SetOrder(1500);
            square.SetVisible(true);
            square.SetAlpha(1f);

            var thread = MakeThread(color);
            const int segments = 12;
            for (float t = 0f; t < WindDuration; t += Time.deltaTime)
            {
                float k = t / WindDuration;
                float e = k * k * (3f - 2f * k);
                // the square shrinks and travels down the strand towards the spool
                square.Transform.position = Bezier(start, control, end, e);
                square.Transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, e);
                for (int i = 0; i < segments; i++)
                    thread.SetPosition(i, Bezier(start, control, end, Mathf.Lerp(e, 1f, i / (float)(segments - 1))));
                thread.widthMultiplier = 0.09f * (1f - 0.5f * k);
                yield return null;
            }
            Destroy(square.Transform.gameObject);
            Destroy(thread.gameObject);
            remaining--;

            spool.AddYarn();
            if (spool.Filled >= spool.Count)
            {
                spool.Completing = true;
                yield return spool.Complete();
                int slot = System.Array.IndexOf(slots, spool);
                if (slot >= 0) slots[slot] = null;
                Destroy(spool.gameObject);
            }
            inFlight--;
        }

        LineRenderer MakeThread(Color color)
        {
            var go = new GameObject("Thread");
            go.transform.SetParent(root, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = Shapes.SpriteMaterial;
            lr.useWorldSpace = true;
            lr.positionCount = 12;
            lr.widthMultiplier = 0.09f;
            lr.numCapVertices = 4;
            lr.startColor = lr.endColor = color;
            lr.sortingOrder = 1499;
            for (int i = 0; i < 12; i++) lr.SetPosition(i, go.transform.position);
            return lr;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        void CheckEnd()
        {
            if (inFlight > 0) return;

            if (remaining <= 0)
            {
                state = State.Won;
                overlayAt = Time.time + 0.8f;
                PlayerPrefs.SetInt(LevelKey, level + 1);
                PlayerPrefs.Save();
                return;
            }

            foreach (var s in slots)
                if (s != null && s.Reserved < s.Count && FindOpenColumn(s) >= 0)
                    return;

            bool slotFree = System.Array.IndexOf(slots, null) >= 0;
            bool anyReachable = false;
            for (int r = 0; r < data.BoardHeight; r++)
            for (int c = 0; c < data.BoardWidth; c++)
                if (board[r, c] != null && reachable[r, c]) anyReachable = true;

            if (!slotFree || !anyReachable)
            {
                state = State.Lost;
                overlayAt = Time.time + 0.4f;
            }
        }

        static bool TryGetTap(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var touch in UnityEngine.InputSystem.EnhancedTouch.Touch.activeTouches)
            {
                if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    screenPos = touch.screenPosition;
                    return true;
                }
            }
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
#else
            if (Input.GetMouseButtonDown(0))
            {
                screenPos = Input.mousePosition;
                return true;
            }
#endif
            screenPos = default;
            return false;
        }

        // ---------- HUD ----------

        GUIStyle pillStyle, buttonStyle, cardStyle, titleStyle, subtitleStyle;
        float styleUnit;

        /// HUD size unit in pixels.
        static float HudUnit() => Mathf.Min(Screen.height / 40f, Screen.width / 18f);

        /// Pixels from the top of the screen to the bottom of the HUD row.
        static float HudHeightPixels() => Screen.height - Screen.safeArea.yMax + HudUnit() * 2.7f;

        void EnsureStyles(float u)
        {
            if (pillStyle != null && Mathf.Approximately(styleUnit, u)) return;
            styleUnit = u;
            var bg = Shapes.GuiRoundRect;
            var border = new RectOffset(24, 24, 24, 24);

            GUIStyle Make(GUIStyle baseStyle, float fontScale, Color text)
            {
                var s = new GUIStyle(baseStyle)
                {
                    fontSize = Mathf.RoundToInt(u * fontScale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    border = border,
                    padding = new RectOffset((int)(u * 0.6f), (int)(u * 0.6f), 0, 0),
                };
                foreach (var st in new[] { s.normal, s.hover, s.active, s.focused })
                {
                    st.background = bg;
                    st.textColor = text;
                }
                return s;
            }

            pillStyle = Make(GUI.skin.box, 0.95f, Palette.Text);
            buttonStyle = Make(GUI.skin.button, 1.0f, Color.white);
            cardStyle = Make(GUI.skin.box, 1f, Palette.Text);
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.9f), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Palette.Text;
            subtitleStyle = new GUIStyle(titleStyle) { fontSize = Mathf.RoundToInt(u * 1.0f), fontStyle = FontStyle.Normal };
        }

        void OnGUI()
        {
            if (data == null) return;
            float u = HudUnit();
            EnsureStyles(u);

            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax + u * 0.6f;
            var oldBg = GUI.backgroundColor;

            string title = $"Level {level}";
            float pillW = pillStyle.CalcSize(new GUIContent(title)).x + u * 1.5f;
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.92f);
            GUI.Box(new Rect(safe.xMin + u * 0.8f, top, pillW, u * 1.9f), title, pillStyle);

            GUI.backgroundColor = Palette.Accent;
            if (GUI.Button(new Rect(safe.xMax - u * 5f, top, u * 4.2f, u * 1.9f), "Retry", buttonStyle))
                BuildLevel();

            // progress bar between the level label and the Retry button
            float barX = safe.xMin + u * 1.6f + pillW, barW = safe.xMax - u * 5.8f - barX;
            float barY = top + u * 0.75f;
            if (barW > u * 2f)
            {
                GUI.backgroundColor = new Color(0.85f, 0.75f, 0.62f, 0.6f);
                GUI.Box(new Rect(barX, barY, barW, u * 0.45f), GUIContent.none, pillStyle);
                float done = (total - remaining) / (float)total;
                if (done > 0f)
                {
                    GUI.backgroundColor = Palette.Get(3);
                    GUI.Box(new Rect(barX, barY, Mathf.Max(u * 0.45f, barW * done), u * 0.45f), GUIContent.none, pillStyle);
                }
            }

            GUI.backgroundColor = oldBg;
            if (state == State.Playing || Time.time < overlayAt) return;

            var oldColor = GUI.color;
            GUI.color = new Color(0.2f, 0.12f, 0.05f, 0.45f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = oldColor;

            bool won = state == State.Won;
            var card = new Rect(Screen.width / 2f - u * 8f, Screen.height * 0.32f, u * 16f, u * 11f);
            GUI.backgroundColor = Palette.Panel;
            GUI.Box(card, GUIContent.none, cardStyle);
            GUI.Label(new Rect(card.x, card.y + u * 1.2f, card.width, u * 2.5f), won ? "Unravelled!" : "Out of room!", titleStyle);
            GUI.Label(new Rect(card.x, card.y + u * 3.8f, card.width, u * 1.6f),
                won ? "Every square is wound up." : "The tray is full. Try another order.", subtitleStyle);
            GUI.backgroundColor = won ? Palette.Get(3) : Palette.Accent;
            if (GUI.Button(new Rect(card.x + u * 3f, card.y + u * 6.8f, card.width - u * 6f, u * 2.6f), won ? "Next level" : "Try again", buttonStyle))
            {
                if (won) level++;
                BuildLevel();
            }
            GUI.backgroundColor = oldBg;
        }
    }
}
