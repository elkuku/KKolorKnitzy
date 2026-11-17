using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace KnotAndSpool
{
    /// A spool of yarn. On the board it is seen from above (a round spool top); in the tray it stands up as a
    /// yarn cylinder with a progress bar that fills as fabric squares are wound onto it.
    public class Spool : MonoBehaviour
    {
        static readonly Color HiddenTop = Palette.Hex(0xF4F1F4);
        static readonly Color HiddenSide = Palette.Hex(0xC9C3CC);
        static readonly Color HiddenMark = Palette.Hex(0x9A93A0);

        public int ColorIndex { get; private set; }
        public int Count { get; private set; }     // squares the spool holds when full
        public int Reserved { get; private set; }  // squares assigned (including ones still flying in)
        public int Filled { get; private set; }    // squares wound
        public bool Hidden { get; private set; }
        public bool InSlot { get; set; }
        public bool Completing { get; set; }

        /// Where the yarn strand attaches: the top of the standing spool.
        public Vector3 ThreadOrigin => transform.position + Vector3.up * (0.55f * transform.localScale.y);

        // board (top-down) view
        readonly List<SpriteRenderer> boardParts = new List<SpriteRenderer>();
        readonly List<Color> boardColors = new List<Color>();
        SpriteRenderer boardSide, boardTop, boardRing, boardHole;
        TextMesh hiddenMark;

        // tray (standing) view
        Transform body, boardView, trayView;
        SpriteRenderer barFill;
        TextMesh percent;
        const float BarWidth = 1.0f;

        bool dim;

        public void Init(int colorIndex, int count, bool hidden)
        {
            ColorIndex = colorIndex;
            Count = count;
            Hidden = hidden;

            body = new GameObject("Body").transform;
            body.SetParent(transform, false);
            BuildBoardView();
            BuildTrayView();
            trayView.gameObject.SetActive(false);
            ApplyBoardColors();
        }

        void BuildBoardView()
        {
            boardView = new GameObject("BoardView").transform;
            boardView.SetParent(body, false);
            BoardPart(Shapes.AddShadow(boardView, "Shadow", new Vector2(0f, -0.26f), new Vector2(1.25f, 0.55f), new Color(0f, 0f, 0f, 0.22f), 498));
            boardSide = BoardPart(Shapes.AddEllipse(boardView, "Side", new Vector2(0f, -0.1f), new Vector2(0.98f, 0.94f), Color.white, 500));
            boardTop = BoardPart(Shapes.AddEllipse(boardView, "Top", new Vector2(0f, 0.04f), new Vector2(0.98f, 0.9f), Color.white, 501));
            boardRing = BoardPart(Shapes.AddEllipse(boardView, "Ring", new Vector2(0f, 0.04f), new Vector2(0.56f, 0.52f), Color.white, 502));
            boardHole = BoardPart(Shapes.AddEllipse(boardView, "Hole", new Vector2(0f, 0.04f), new Vector2(0.2f, 0.18f), Color.white, 503));
            BoardPart(Shapes.AddEllipse(boardView, "Shine", new Vector2(-0.2f, 0.25f), new Vector2(0.26f, 0.12f), new Color(1f, 1f, 1f, 0.55f), 504));
            hiddenMark = Shapes.AddText(boardView, "Mark", new Vector2(0f, 0.04f), 0.5f, HiddenMark, 505);
        }

        void BuildTrayView()
        {
            trayView = new GameObject("TrayView").transform;
            trayView.SetParent(body, false);
            var color = Palette.Get(ColorIndex);
            var dark = Palette.Shade(color, 0.72f);
            var light = Palette.Tint(color, 0.35f);

            Shapes.AddShadow(trayView, "Shadow", new Vector2(0f, -0.6f), new Vector2(1.5f, 0.45f), new Color(0f, 0f, 0f, 0.2f), 498);
            Shapes.AddEllipse(trayView, "BottomRim", new Vector2(0f, -0.48f), new Vector2(1.08f, 0.36f), dark, 500);
            Shapes.AddRect(trayView, "Yarn", new Vector2(0f, -0.02f), new Vector2(0.92f, 0.9f), color, 501);
            for (int i = 0; i < 5; i++)
                Shapes.AddRect(trayView, $"Strand{i}", new Vector2(0f, -0.36f + i * 0.17f), new Vector2(0.9f, 0.045f), Palette.Shade(color, 0.84f), 502);
            Shapes.AddRect(trayView, "Shine", new Vector2(-0.27f, -0.02f), new Vector2(0.1f, 0.74f), new Color(1f, 1f, 1f, 0.32f), 503);
            Shapes.AddEllipse(trayView, "TopRim", new Vector2(0f, 0.46f), new Vector2(1.08f, 0.38f), light, 504);
            Shapes.AddEllipse(trayView, "TopInner", new Vector2(0f, 0.46f), new Vector2(0.62f, 0.2f), color, 505);
            Shapes.AddEllipse(trayView, "Hole", new Vector2(0f, 0.46f), new Vector2(0.22f, 0.08f), dark, 506);

            // progress bar and percentage under the spool
            Shapes.AddRect(trayView, "BarBack", new Vector2(0f, -0.92f), new Vector2(BarWidth, 0.16f), new Color(1f, 1f, 1f, 0.75f), 500);
            barFill = Shapes.AddRect(trayView, "BarFill", new Vector2(-BarWidth / 2f, -0.92f), new Vector2(0.01f, 0.16f), color, 501);
            barFill.enabled = false;
            percent = Shapes.AddText(trayView, "Percent", new Vector2(0f, -1.22f), 0.3f, Palette.Text, 502);
            UpdateProgress();
        }

        SpriteRenderer BoardPart(SpriteRenderer sr)
        {
            boardParts.Add(sr);
            boardColors.Add(sr.color);
            return sr;
        }

        void SetBoardColor(SpriteRenderer sr, Color c)
        {
            int i = boardParts.IndexOf(sr);
            boardColors[i] = c;
            sr.color = dim ? Darken(c) : c;
        }

        void ApplyBoardColors()
        {
            if (Hidden)
            {
                SetBoardColor(boardSide, HiddenSide);
                SetBoardColor(boardTop, HiddenTop);
                SetBoardColor(boardRing, HiddenTop);
                SetBoardColor(boardHole, HiddenTop);
                hiddenMark.text = "?";
                return;
            }
            var color = Palette.Get(ColorIndex);
            SetBoardColor(boardSide, Palette.Shade(color, 0.7f));
            SetBoardColor(boardTop, color);
            SetBoardColor(boardRing, Palette.Tint(color, 0.3f));
            SetBoardColor(boardHole, Palette.Shade(color, 0.55f));
            hiddenMark.text = "";
        }

        static Color Darken(Color c) => new Color(c.r * 0.78f, c.g * 0.78f, c.b * 0.78f, c.a);

        /// Spools that can't be tapped yet are drawn a little darker.
        public void SetReachable(bool reachable)
        {
            if (dim == !reachable) return;
            dim = !reachable;
            for (int i = 0; i < boardParts.Count; i++)
                boardParts[i].color = dim ? Darken(boardColors[i]) : boardColors[i];
        }

        public IEnumerator Reveal()
        {
            Hidden = false;
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                body.localScale = new Vector3(Mathf.Lerp(1f, 0f, t / 0.15f), 1f, 1f);
                yield return null;
            }
            ApplyBoardColors();
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                body.localScale = new Vector3(Mathf.Lerp(0f, 1f, t / 0.15f), 1f, 1f);
                yield return null;
            }
            body.localScale = Vector3.one;
        }

        public void Reserve() => Reserved++;

        public void AddYarn()
        {
            Filled++;
            UpdateProgress();
            StartCoroutine(Bounce());
        }

        void UpdateProgress()
        {
            float f = Count > 0 ? Filled / (float)Count : 0f;
            barFill.enabled = f > 0f;
            float w = Mathf.Max(0.16f, BarWidth * f);
            barFill.size = new Vector2(w, 0.16f);
            barFill.transform.localPosition = new Vector3(-BarWidth / 2f + w / 2f, -0.92f, 0f);
            percent.text = $"{Mathf.RoundToInt(f * 100f)}%";
        }

        /// Flips from the board look to the standing spool used in the tray.
        public void StandUp()
        {
            SetReachable(true);
            if (Hidden)
            {
                Hidden = false;
                ApplyBoardColors();
            }
            boardView.gameObject.SetActive(false);
            trayView.gameObject.SetActive(true);
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
