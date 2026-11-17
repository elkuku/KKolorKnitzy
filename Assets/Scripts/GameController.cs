using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    /// Builds levels, handles taps on spools, pulls free threads onto matching spools, and draws the HUD.
    public class GameController : MonoBehaviour
    {
        enum State { Playing, Won, Lost }

        const string LevelKey = "level";
        const float SlotSpacing = 1.7f;
        const float ColumnSpacing = 2.0f;
        const float StackSpacing = 1.75f;
        const float PullDuration = 0.4f;
        const float PullStagger = 0.12f;

        // Spawned at startup by the bootstrap so any scene works.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<GameController>() == null)
                new GameObject("Game").AddComponent<GameController>();
        }

        int level;
        State state;
        LevelData data;
        Camera cam;
        Transform root;

        readonly List<YarnThread> threads = new List<YarnThread>();
        readonly List<List<Spool>> columns = new List<List<Spool>>();
        Spool[] slots;
        Vector3[] slotPositions;
        Vector3[] columnTops;
        int visiblePerColumn;
        int inFlight;
        bool looksDirty;

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
            threads.Clear();
            columns.Clear();
            inFlight = 0;
            state = State.Playing;

            float size = SetupCamera();
            var knotArea = Rect.MinMaxRect(-4.3f, -size * 0.05f, 4.3f, size - 2.0f);
            data = LevelGenerator.Generate(level, knotArea);

            var panelRect = Rect.MinMaxRect(knotArea.xMin - 0.4f, knotArea.yMin - 0.4f, knotArea.xMax + 0.4f, knotArea.yMax + 0.4f);
            Shapes.AddRect(root, "KnotPanel", panelRect.center, panelRect.size, Palette.Panel, -10);

            // Threads
            var byIndex = new List<YarnThread>();
            for (int i = 0; i < data.Threads.Count; i++)
            {
                var go = new GameObject($"Thread{i}");
                go.transform.SetParent(root, false);
                byIndex.Add(go.AddComponent<YarnThread>());
            }
            for (int i = 0; i < data.Threads.Count; i++)
            {
                foreach (int c in data.Threads[i].CoveredBy) byIndex[i].CoveredBy.Add(byIndex[c]);
                byIndex[i].Init(data.Threads[i]);
            }
            threads.AddRange(byIndex);

            // Tray slots
            float slotY = knotArea.yMin - 1.9f;
            slots = new Spool[data.Slots];
            slotPositions = new Vector3[data.Slots];
            for (int i = 0; i < data.Slots; i++)
            {
                slotPositions[i] = new Vector3((i - (data.Slots - 1) / 2f) * SlotSpacing, slotY, 0f);
                Shapes.AddRect(root, $"Slot{i}", slotPositions[i], new Vector2(1.4f, 1.6f), Palette.SlotEmpty, -5);
            }

            // Spool columns
            float columnTop = slotY - 2.3f;
            float bottom = -size + 0.9f;
            visiblePerColumn = Mathf.Clamp(Mathf.FloorToInt((columnTop - bottom) / StackSpacing) + 1, 1, 3);
            columnTops = new Vector3[data.Columns.Count];
            for (int c = 0; c < data.Columns.Count; c++)
            {
                columnTops[c] = new Vector3((c - (data.Columns.Count - 1) / 2f) * ColumnSpacing, columnTop, 0f);
                var list = new List<Spool>();
                foreach (int color in data.Columns[c])
                {
                    var go = new GameObject("Spool");
                    go.transform.SetParent(root, false);
                    var spool = go.AddComponent<Spool>();
                    spool.Init(color, data.Capacity);
                    list.Add(spool);
                }
                columns.Add(list);
                LayoutColumn(c, false);
            }
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

        void LayoutColumn(int c, bool animate)
        {
            var list = columns[c];
            for (int i = 0; i < list.Count; i++)
            {
                bool visible = i < visiblePerColumn;
                var target = columnTops[c] + Vector3.down * (i * StackSpacing);
                if (visible && animate && list[i].gameObject.activeSelf)
                {
                    StartCoroutine(list[i].MoveTo(target, 0.2f));
                }
                else
                {
                    list[i].transform.position = target;
                }
                list[i].SetVisible(visible);
            }
        }

        // ---------- gameplay ----------

        void Update()
        {
            if (state != State.Playing) return;

            if (TryGetTap(out var screenPos))
                OnTap(cam.ScreenToWorldPoint(screenPos));

            PullFreeThreads();

            if (looksDirty)
            {
                looksDirty = false;
                foreach (var t in threads)
                    if (t != null && !t.Taken) t.RefreshLook();
            }

            CheckEnd();
        }

        void OnTap(Vector2 world)
        {
            for (int c = 0; c < columns.Count; c++)
            {
                if (columns[c].Count == 0) continue;
                var spool = columns[c][0];
                if (Vector2.Distance(world, spool.transform.position) > Spool.TapRadius) continue;

                int slot = System.Array.IndexOf(slots, null);
                if (slot < 0)
                {
                    StartCoroutine(spool.Shake());
                    return;
                }
                columns[c].RemoveAt(0);
                slots[slot] = spool;
                StartCoroutine(SendToSlot(spool, slot));
                LayoutColumn(c, true);
                return;
            }
        }

        IEnumerator SendToSlot(Spool spool, int slot)
        {
            inFlight++;
            yield return spool.MoveTo(slotPositions[slot], 0.22f);
            spool.InSlot = true;
            inFlight--;
        }

        void PullFreeThreads()
        {
            foreach (var spool in slots)
            {
                if (spool == null || !spool.InSlot || spool.Completing) continue;
                if (spool.Reserved >= spool.Capacity || Time.time < spool.NextPullTime) continue;

                var thread = FindFreeThread(spool.ColorIndex);
                if (thread == null) continue;

                thread.Taken = true;
                spool.Reserved++;
                spool.NextPullTime = Time.time + PullStagger;
                looksDirty = true;
                StartCoroutine(Pull(thread, spool));
            }
        }

        YarnThread FindFreeThread(int color)
        {
            // Prefer the topmost free thread of that color.
            for (int i = threads.Count - 1; i >= 0; i--)
            {
                var t = threads[i];
                if (!t.Taken && t.ColorIndex == color && t.IsFree) return t;
            }
            return null;
        }

        IEnumerator Pull(YarnThread thread, Spool spool)
        {
            inFlight++;
            yield return thread.ReelInto(spool.YarnCenter, PullDuration);
            spool.AddYarn();
            if (spool.Filled >= spool.Capacity)
            {
                spool.Completing = true;
                yield return spool.Complete();
                int slot = System.Array.IndexOf(slots, spool);
                if (slot >= 0) slots[slot] = null;
                Destroy(spool.gameObject);
            }
            inFlight--;
        }

        void CheckEnd()
        {
            if (inFlight > 0) return;

            bool anyLeft = false;
            foreach (var t in threads)
                if (!t.Taken) { anyLeft = true; break; }
            if (!anyLeft)
            {
                state = State.Won;
                PlayerPrefs.SetInt(LevelKey, level + 1);
                PlayerPrefs.Save();
                return;
            }

            // Anything pullable right now?
            foreach (var s in slots)
                if (s != null && s.Reserved < s.Capacity && FindFreeThread(s.ColorIndex) != null)
                    return;

            bool slotFree = System.Array.IndexOf(slots, null) >= 0;
            bool spoolsLeft = false;
            foreach (var col in columns)
                if (col.Count > 0) { spoolsLeft = true; break; }

            if (!slotFree || !spoolsLeft) state = State.Lost;
        }

        static bool TryGetTap(out Vector2 screenPos)
        {
#if ENABLE_INPUT_SYSTEM
            var pointer = UnityEngine.InputSystem.Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
            {
                screenPos = pointer.position.ReadValue();
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

        GUIStyle labelStyle, titleStyle, buttonStyle;

        void OnGUI()
        {
            float u = Mathf.Min(Screen.height / 40f, Screen.width / 18f);
            if (labelStyle == null || labelStyle.fontSize != Mathf.RoundToInt(u * 1.1f))
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(u * 1.1f), fontStyle = FontStyle.Bold };
                labelStyle.normal.textColor = Palette.Text;
                titleStyle = new GUIStyle(labelStyle) { fontSize = Mathf.RoundToInt(u * 1.8f), alignment = TextAnchor.MiddleCenter };
                titleStyle.normal.textColor = Color.white;
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(u * 1.0f), fontStyle = FontStyle.Bold };
            }

            var safe = Screen.safeArea;
            float top = Screen.height - safe.yMax + u * 0.5f;

            GUI.Label(new Rect(safe.xMin + u, top, Screen.width / 2f, u * 2f), $"Level {level}", labelStyle);
            if (GUI.Button(new Rect(safe.xMax - u * 5f, top, u * 4f, u * 1.8f), "Retry", buttonStyle))
                BuildLevel();

            if (state == State.Playing) return;

            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = old;

            bool won = state == State.Won;
            GUI.Label(new Rect(0, Screen.height * 0.38f, Screen.width, u * 3f), won ? "Untangled!" : "Out of room!", titleStyle);
            var btn = new Rect(Screen.width / 2f - u * 5f, Screen.height * 0.38f + u * 4f, u * 10f, u * 2.4f);
            if (GUI.Button(btn, won ? "Next level" : "Try again", buttonStyle))
            {
                if (won) level++;
                BuildLevel();
            }
        }
    }
}
