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
        const float SlotSpacing = 1.5f;
        const float BoardCell = 1.3f;
        const float BoardSpoolScale = 0.95f;
        const float TraySpoolScale = 0.95f;
        const float FabricMaxWidth = 6.0f;
        const float WindDuration = 0.45f;
        const float WindStagger = 0.12f;

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
        Palette.Theme theme;
        Camera cam;
        Transform root;
        int inFlight;

        // fabric
        List<Square>[] fabric;   // per column, bottom square first
        float cellSize, windowBottom, windowTop, fabricLeft;
        int remaining;

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
            float bottom = -size + 0.9f + Screen.safeArea.yMin * worldPerPixel;

            theme = Palette.ThemeFor(level);
            cam.backgroundColor = theme.Bottom;
            Shapes.AddGradient(root, new Rect(-worldWidth / 2f, -size, worldWidth, 2f * size), theme.Top, theme.Bottom, -100);

            data = LevelGenerator.Generate(level);

            // Bottom-up: board on a darker floor, the tray above it, then the fabric hangs from the top of the screen.
            float boardTop = bottom + data.BoardHeight * BoardCell;
            float floorTop = boardTop + 0.55f;
            float floorH = floorTop + size + 1f;
            Shapes.AddRect(root, "Floor", new Vector2(0f, floorTop - floorH / 2f), new Vector2(worldWidth + 2f, floorH), theme.Floor, -90);
            Shapes.AddRect(root, "FloorEdge", new Vector2(0f, floorTop - 0.06f), new Vector2(worldWidth + 2f, 0.12f), theme.FloorEdge, -89);
            BuildBoard(boardTop);
            float slotY = floorTop + 1.45f;
            BuildTray(slotY);
            BuildFabric(Rect.MinMaxRect(-FabricMaxWidth / 2f, slotY + 2.0f, FabricMaxWidth / 2f, size));
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
            return cam.orthographicSize;
        }

        void BuildFabric(Rect area)
        {
            int w = data.Width, h = data.Height;
            // A narrow strip of fabric in a wooden frame that runs up off the top of the screen.
            // The fabric's bottom edge sits just above the tray; taller fabric scrolls in from the top of the screen.
            cellSize = Mathf.Min(area.width / w, area.height / h, 1.0f);
            float windowW = cellSize * w;
            windowBottom = area.yMin;
            windowTop = area.yMax;
            fabricLeft = -windowW / 2f;

            float frameH = windowTop - windowBottom + 2f;
            var frameCenter = new Vector2(0f, windowBottom - 0.2f + frameH / 2f);
            Shapes.AddShadow(root, "FrameShadow", new Vector2(0f, windowBottom - 0.3f), new Vector2(windowW + 2.2f, 1.2f), Palette.Shadow, -23);
            Shapes.AddRect(root, "Frame", frameCenter, new Vector2(windowW + 0.7f, frameH), Palette.WoodDark, -22);
            Shapes.AddRect(root, "FrameInner", frameCenter + new Vector2(0f, 0.1f), new Vector2(windowW + 0.4f, frameH), Palette.WoodLight, -21);
            Shapes.AddRect(root, "Canvas", frameCenter + new Vector2(0f, 0.2f), new Vector2(windowW + 0.06f, frameH), Palette.Shade(theme.Tile, 0.9f), -20);
            // the edge where the yarn leaves the fabric
            Shapes.AddRect(root, "Lip", new Vector2(0f, windowBottom - 0.12f), new Vector2(windowW + 0.4f, 0.16f), Palette.WoodDark, 60);

            fabric = new List<Square>[w];
            remaining = w * h;
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
            for (int i = 0; i < data.Slots; i++)
            {
                slotPositions[i] = new Vector3((i - (data.Slots - 1) / 2f) * SlotSpacing, slotY, 0f);
                // empty slot: a soft white pad the spool stands on
                Shapes.AddShadow(root, $"SlotShadow{i}", slotPositions[i] + new Vector3(0f, -0.1f, 0f), new Vector2(1.6f, 1.6f), new Color(0f, 0f, 0f, 0.08f), -15);
                Shapes.AddRect(root, $"Slot{i}", slotPositions[i], new Vector2(1.18f, 1.18f), new Color(1f, 1f, 1f, 0.85f), -14);
            }
        }

        void BuildBoard(float boardTop)
        {
            int bw = data.BoardWidth, bh = data.BoardHeight;
            board = new Spool[bh, bw];
            boardPos = new Vector3[bh, bw];
            float left = -(bw - 1) / 2f * BoardCell;

            for (int r = 0; r < bh; r++)
            for (int c = 0; c < bw; c++)
            {
                var p = new Vector3(left + c * BoardCell, boardTop - (r + 0.5f) * BoardCell, 0f);
                boardPos[r, c] = p;
                // walls are simply gaps where the floor shows through
                if (data.Walls[r, c]) continue;
                Shapes.AddRect(root, "TileEdge", p + new Vector3(0f, -0.06f, 0f), Vector2.one * (BoardCell - 0.06f), Palette.Shade(theme.Tile, 0.86f), -13);
                Shapes.AddRect(root, "Tile", p, Vector2.one * (BoardCell - 0.06f), theme.Tile, -12);

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
            spool.StandUp();
            yield return spool.MoveTo(slotPositions[slot], TraySpoolScale, 0.3f);
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
                thread.widthMultiplier = 0.15f * (1f - 0.4f * k);
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
            lr.widthMultiplier = 0.15f;
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

        GUIStyle pillStyle, levelStyle, buttonStyle, cardStyle, titleStyle, subtitleStyle;
        float styleUnit;

        /// HUD size unit in pixels.
        static float HudUnit() => Mathf.Min(Screen.height / 40f, Screen.width / 18f);

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
            levelStyle = Make(GUI.skin.box, 0.55f, Palette.Text);
            levelStyle.padding = new RectOffset((int)(u * 0.4f), (int)(u * 0.4f), 0, 0);
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

            // Top-left corner beside the fabric: a round restart button with the level number under it.
            float x = safe.xMin + u * 0.4f;
            var retry = new Rect(x, top, u * 2.2f, u * 2.2f);
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.9f);
            if (GUI.Button(retry, GUIContent.none, pillStyle))
                BuildLevel();
            var oldColor0 = GUI.color;
            GUI.color = theme.Accent;
            GUI.DrawTexture(new Rect(retry.x + u * 0.25f, retry.y + u * 0.25f, u * 1.7f, u * 1.7f), Shapes.RetryIcon);
            GUI.color = oldColor0;

            string title = $"Level {level}";
            float pillW = levelStyle.CalcSize(new GUIContent(title)).x + u * 0.9f;
            GUI.backgroundColor = new Color(1f, 1f, 1f, 0.75f);
            GUI.Box(new Rect(x, top + u * 2.6f, pillW, u * 1.2f), title, levelStyle);

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
            GUI.backgroundColor = won ? Palette.Get(3) : theme.Accent;
            if (GUI.Button(new Rect(card.x + u * 3f, card.y + u * 6.8f, card.width - u * 6f, u * 2.6f), won ? "Next level" : "Try again", buttonStyle))
            {
                if (won) level++;
                BuildLevel();
            }
            GUI.backgroundColor = oldBg;
        }
    }
}
