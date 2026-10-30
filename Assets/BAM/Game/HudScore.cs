using BAM.Audio;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The old game's score tab (ABC2 Hud.cs drawScoreTab, HudV3.cs drawScoreIcon): the glass tool button under the strip's right
    /// end shows a list icon; a tap opens a dark sheet with YOUR TEAM and ENEMY TEAM, each hero's portrait (dimmed with the respawn
    /// seconds while dead), name, role and K / D / A, and EXIT GAME at the bottom, which asks "Leave the match?" with LEAVE and STAY.
    /// A tap anywhere else closes it. Against bots the match waits while it is open (GameFlow reads <see cref="MenuOpen"/>,
    /// Rickard 2026-10-30); online it goes on, as in the old game. In the guided match (which had no score tab) the button is the
    /// pause button instead.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color ScoreDim = new Color(6f / 255f, 9f / 255f, 26f / 255f, 0.86f), ScoreBlue = Hex(0x3aa0ff), ScoreRed = Hex(0xff3b4e);
        static readonly Color ScoreSub = Hex(0xa9b4e8), RowMine = new Color(58f / 255f, 160f / 255f, 1f, 0.28f), RowOther = new Color(30f / 255f, 40f / 255f, 88f / 255f, 0.85f);
        static readonly Color ExitRed = Hex(0xe8434f), ExitEdge = Hex(0xffb3b9), StayBlue = Hex(0x2b3a7a);

        sealed class ScoreRow
        {
            public RectTransform Root;
            public Image Back, Stripe, Dark;
            public RawImage Pic;
            public RectTransform Disc;
            public Text Name, Role, Kda, Secs;
            // what the row shows now, so its texts and portrait are only rebuilt when they change (no per-frame garbage)
            public Unit Shown;
            public int K = -1, D = -1, A = -1, S = -1;
        }

        RectTransform scoreRoot;
        Text[] scoreHeads = new Text[2], scoreKdaHeads = new Text[2];
        readonly ScoreRow[] scoreRows = new ScoreRow[6];
        Button scoreExit, scoreLeave, scoreStay;
        Text scoreHint, scoreAsk;
        Image toolIcon;
        Text toolPause;
        bool scoreOpen, scoreAskLeave;
        Vector2 scoreLaidOut;

        void BuildScore()
        {
            scoreRoot = UiKit.NewRect("Score", safe);
            UiKit.Stretch(scoreRoot);
            var dim = UiKit.Fill(scoreRoot, ScoreDim);
            UiKit.Stretch(dim.rectTransform, -400f, -400f, -400f, -400f); // over the bars outside the safe area too
            dim.raycastTarget = true;
            var close = dim.gameObject.AddComponent<Button>();
            close.targetGraphic = dim;
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => { if (scoreAskLeave) scoreAskLeave = false; else CloseScore(); });
            for (int t = 0; t < 2; t++)
            {
                scoreHeads[t] = ScoreText(scoreRoot, t == 0 ? "YOUR TEAM" : "ENEMY TEAM", t == 0 ? ScoreBlue : ScoreRed, TextAnchor.MiddleLeft);
                scoreKdaHeads[t] = ScoreText(scoreRoot, "K / D / A", ScoreSub, TextAnchor.MiddleRight);
            }
            for (int i = 0; i < scoreRows.Length; i++) scoreRows[i] = MakeScoreRow();
            scoreExit = ScoreButton("EXIT GAME", ExitRed, () => scoreAskLeave = true);
            scoreLeave = ScoreButton("LEAVE", ExitRed, () => { CloseScore(); flow.QuitToMenu(); });
            scoreStay = ScoreButton("STAY", StayBlue, () => scoreAskLeave = false);
            scoreStay.GetComponentsInChildren<Image>(true)[1].color = ScoreSub; // its edge
            scoreHint = ScoreText(scoreRoot, "Tap anywhere else to close", ScoreSub, TextAnchor.MiddleCenter);
            scoreAsk = ScoreText(scoreRoot, "Leave the match?", Color.white, TextAnchor.MiddleCenter);
            scoreRoot.gameObject.SetActive(false);
        }

        void BeginScore()
        {
            scoreOpen = scoreAskLeave = false;
            if (scoreRoot != null) scoreRoot.gameObject.SetActive(false);
            foreach (var row in scoreRows) if (row != null) row.Shown = null; // a new match: every row rebuilds
        }

        /// <summary>The glass tool: the score tab in a match, pause in the guided match.</summary>
        void ToolTapped()
        {
            BamAudio.Play(SfxId.UiTap);
            if (tutorial) { flow.Pause(); return; }
            if (m.State != MatchState.Play && m.State != MatchState.Countdown) return;
            CancelTouches();
            scoreOpen = true; scoreAskLeave = false;
        }

        void CloseScore() { scoreOpen = false; scoreAskLeave = false; }

        /// <summary>The score sheet (the in-match menu) is open: an offline match holds while it is.</summary>
        public bool MenuOpen => scoreOpen;

        void ScoreFrame()
        {
            if (toolIcon.enabled == tutorial) { toolIcon.enabled = !tutorial; toolPause.enabled = tutorial; }
            if (tutorial || (m.State != MatchState.Play && m.State != MatchState.Countdown)) scoreOpen = false;
            if (scoreRoot.gameObject.activeSelf != scoreOpen) { scoreRoot.gameObject.SetActive(scoreOpen); if (scoreOpen) scoreRoot.SetAsLastSibling(); }
            if (!scoreOpen) return;
            float W = safe.rect.width, H = safe.rect.height;
            if (new Vector2(W, H) != scoreLaidOut) { scoreLaidOut = new Vector2(W, H); LayoutScore(W, H); }

            int used0 = 0, used1 = 0;
            for (int i = 0; i < scoreRows.Length; i++) scoreRows[i].Root.gameObject.SetActive(false);
            int n = 0;
            for (int i = 0; i < m.Heroes.Count && n < scoreRows.Length; i++)
            {
                var u = m.Heroes[i];
                if (!u.IsRealHero) continue;
                int t = u.Team == m.Player.Team ? 0 : 1;
                int slot = t == 0 ? used0 : used1;
                if (slot >= 3) continue;
                if (t == 0) used0++; else used1++;
                var row = scoreRows[n++];
                PlaceScoreRow(row, t, slot, W, H);
                row.Root.gameObject.SetActive(true);
                if (row.Shown != u)
                {
                    row.Shown = u; row.K = row.D = row.A = row.S = -1;
                    var info = HeroCatalog.Get(u.Hero);
                    row.Pic.texture = Resources.Load<Texture2D>("BAM/Menu/portrait_" + info.Name.ToLowerInvariant());
                    row.Back.color = u == m.Player ? RowMine : RowOther;
                    row.Stripe.color = t == 0 ? ScoreBlue : ScoreRed;
                    SetText(row.Name, info.Name);
                    SetText(row.Role, u == m.Player ? "You · " + info.Role : info.Role);
                }
                if (u.Kills != row.K || u.Deaths != row.D || u.Assists != row.A)
                {
                    row.K = u.Kills; row.D = u.Deaths; row.A = u.Assists;
                    SetText(row.Kda, u.Kills + " / " + u.Deaths + " / " + u.Assists);
                }
                bool dead = !u.Alive;
                if (row.Dark.enabled != dead) { row.Dark.enabled = dead; row.Secs.enabled = dead; }
                if (dead) SetText(row.Secs, Int(Mathf.CeilToInt(Mathf.Max(0f, u.RespawnT))));
            }

            scoreExit.gameObject.SetActive(!scoreAskLeave);
            scoreHint.gameObject.SetActive(!scoreAskLeave);
            scoreLeave.gameObject.SetActive(scoreAskLeave);
            scoreStay.gameObject.SetActive(scoreAskLeave);
            scoreAsk.gameObject.SetActive(scoreAskLeave);
        }

        static void SetText(Text t, string s) { if (t.text != s) t.text = s; }

        void LayoutScore(float W, float H)
        {
            float x0 = W * 0.05f, w = W * 0.9f;
            for (int t = 0; t < 2; t++)
            {
                float y0 = t == 0 ? H * 0.14f : H * 0.48f, fs = W * 0.038f;
                ScoreAt(scoreHeads[t], x0, y0 + H * 0.02f, w, fs, TextAnchor.MiddleLeft);
                ScoreAt(scoreKdaHeads[t], x0, y0 + H * 0.02f, w * 0.62f, fs, TextAnchor.MiddleRight);
            }
            float bh = H * 0.06f;
            ScoreRect(scoreExit, W * 0.25f, H * 0.86f, W * 0.5f, bh);
            ScoreRect(scoreLeave, W * 0.2f, H * 0.86f, W * 0.29f, bh);
            ScoreRect(scoreStay, W * 0.51f, H * 0.86f, W * 0.29f, bh);
            ScoreAt(scoreHint, 0f, H * 0.86f + bh + H * 0.025f, W, W * 0.03f, TextAnchor.MiddleCenter);
            ScoreAt(scoreAsk, 0f, H * 0.86f - H * 0.03f, W, W * 0.042f, TextAnchor.MiddleCenter);
        }

        void PlaceScoreRow(ScoreRow row, int team, int i, float W, float H)
        {
            float rowH = H * 0.075f, x0 = W * 0.05f, w = W * 0.9f, y0 = team == 0 ? H * 0.14f : H * 0.48f;
            float y = y0 + H * 0.045f + i * (rowH + H * 0.01f), r = rowH * 0.36f;
            var rt = row.Root;
            rt.anchoredPosition = new Vector2(x0, -y);
            rt.sizeDelta = new Vector2(w, rowH);
            Corners(row.Back, rowH * 0.2f);
            var st = row.Stripe.rectTransform;
            st.anchoredPosition = new Vector2(0f, -rowH * 0.15f);
            st.sizeDelta = new Vector2(4f, rowH * 0.7f);
            row.Disc.anchoredPosition = new Vector2(W * 0.025f + r, -rowH / 2f);
            row.Disc.sizeDelta = new Vector2(2f * r, 2f * r);
            row.Secs.fontSize = Mathf.RoundToInt(r * 0.8f);
            float tx = r * 2f + W * 0.05f;
            ScoreAt(row.Name, tx, rowH / 2f - rowH * 0.15f, w * 0.45f, rowH * 0.27f, TextAnchor.MiddleLeft);
            ScoreAt(row.Role, tx, rowH / 2f + rowH * 0.18f, w * 0.45f, rowH * 0.22f, TextAnchor.MiddleLeft);
            ScoreAt(row.Kda, 0f, rowH / 2f, w * 0.62f, rowH * 0.3f, TextAnchor.MiddleRight);
        }

        ScoreRow MakeScoreRow()
        {
            var row = new ScoreRow();
            row.Root = UiKit.NewRect("Row", scoreRoot);
            row.Root.anchorMin = row.Root.anchorMax = row.Root.pivot = new Vector2(0f, 1f);
            row.Back = UiKit.Fill(row.Root, RowOther, UiKit.Rounded);
            var stripe = UiKit.NewRect("Stripe", row.Root);
            stripe.anchorMin = stripe.anchorMax = stripe.pivot = new Vector2(0f, 1f);
            row.Stripe = stripe.gameObject.AddComponent<Image>();
            row.Stripe.raycastTarget = false;
            row.Disc = UiKit.NewRect("Portrait", row.Root);
            row.Disc.anchorMin = row.Disc.anchorMax = new Vector2(0f, 1f);
            row.Disc.pivot = new Vector2(0.5f, 0.5f);
            var mask = UiKit.Fill(row.Disc, Hex(0x2b2350), UiKit.Circle);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pic = UiKit.NewRect("Pic", mask.transform);
            UiKit.Stretch(pic);
            row.Pic = pic.gameObject.AddComponent<RawImage>();
            row.Pic.raycastTarget = false;
            row.Pic.uvRect = new Rect(0.115f, 0f, 0.77f, 1f);
            row.Dark = UiKit.Fill(row.Disc, new Color(0f, 0f, 0f, 0.55f), UiKit.Circle);
            row.Dark.enabled = false;
            row.Secs = ScoreText(row.Disc, "", Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(row.Secs.rectTransform, -20f, -20f, -20f, -20f);
            row.Secs.enabled = false;
            row.Name = ScoreText(row.Root, "", Color.white, TextAnchor.MiddleLeft);
            row.Role = ScoreText(row.Root, "", ScoreSub, TextAnchor.MiddleLeft);
            row.Kda = ScoreText(row.Root, "", Color.white, TextAnchor.MiddleRight);
            return row;
        }

        static Text ScoreText(Transform parent, string s, Color color, TextAnchor anchor)
        {
            var t = UiKit.Label(parent, s, 30, anchor, color);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        /// <summary>A text whose box starts at x, centred on y (both from the parent's top left), w wide.</summary>
        static void ScoreAt(Text t, float x, float y, float w, float size, TextAnchor anchor)
        {
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, size * 1.6f);
            t.fontSize = Mathf.RoundToInt(size);
            t.alignment = anchor;
        }

        Button ScoreButton(string label, Color color, System.Action onClick)
        {
            var rt = UiKit.NewRect(label, scoreRoot);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            var plate = UiKit.Fill(rt, color, UiKit.Rounded);
            plate.raycastTarget = true;
            var edge = UiKit.Fill(rt, ExitEdge, UiKit.Ring);
            edge.raycastTarget = false;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = plate;
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); onClick(); });
            var t = ScoreText(rt, label, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(t.rectTransform);
            return b;
        }

        static void ScoreRect(Button b, float x, float y, float w, float h)
        {
            var rt = (RectTransform)b.transform;
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            var imgs = b.GetComponentsInChildren<Image>(true);
            Corners(imgs[0], h * 0.3f);
            Corners(imgs[1], h * 0.3f);
            b.GetComponentInChildren<Text>(true).fontSize = Mathf.RoundToInt(h * 0.42f);
        }

        // HudV3 drawScoreIcon: three rows, a dot and a rounded bar each, white
        static Sprite scoreIconSprite;
        static Sprite ScoreIconSprite
        {
            get
            {
                if (scoreIconSprite != null) return scoreIconSprite;
                const int n = 64;
                float r = n / 2f, lw = r * 0.9f, lh = r * 0.14f, cx = n / 2f, x0 = cx - lw / 2f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.ScoreIcon", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = float.MaxValue;
                        for (int i = -1; i <= 1; i++)
                        {
                            float ry = cx + i * r * 0.32f;
                            d = Mathf.Min(d, (p - new Vector2(x0 + lh * 0.6f, ry)).magnitude - lh * 0.6f);
                            d = Mathf.Min(d, SegDist(p, new Vector2(x0 + lh * 1.8f + lh / 2f, ry), new Vector2(x0 + lw - lh / 2f, ry)) - lh / 2f);
                        }
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(0.5f - d)));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                scoreIconSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return scoreIconSprite;
            }
        }
    }
}
