using System;
using BAM.Audio;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The old game's emotes (ABC2 Hud.cs playerEmote and drawGlassHeart, HudEmote.cs; Rickard 2026-10-26 "vi ska ha emotes"):
    /// a small glass heart button up and to the right of the SPECIAL. A short tap sends the heart; holding it (0.22 s, or moving the
    /// finger) opens a wheel of emotes above the button, and letting go on one sends it (anywhere else sends nothing). The emote
    /// shows above the hero for 2 s as a white bubble (the heart) or a white pill with the word, and the button waits 2 s before
    /// the next one. A random other hero answers with a heart 60 % of the time, 0.6 to 1.6 s later. The heart and GG are seen by
    /// everyone, the other words only by the sender's team. Picture only: the rules never see an emote. The wheel's two team
    /// signals (ATTACK, RETREAT) are not here: the new rules have no signals for the bots to follow.
    /// </summary>
    sealed partial class Hud
    {
        static readonly string[] Emotes = { "", "GG", "Nice!", "Thanks!", "Haha", "Oops", "Help!", "Wow!" }; // 0 = the heart (drawn)
        const float EmoteTime = 2f, WheelOpenTime = 0.22f;
        static readonly Color BubbleWhite = Hex(0xf4f4f7), BubbleInk = Hex(0x2b2350), HeartRed = Hex(0xe8243c);
        static readonly Color HeartPlate = new Color(20f / 255f, 28f / 255f, 62f / 255f, 0.75f), WheelPick = new Color(1f, 214f / 255f, 90f / 255f, 0.9f);

        sealed class EmoteMark
        {
            public RectTransform Root;
            public Image Shadow, Body, Heart;
            public Text Word;
        }

        sealed class EmoteSlot
        {
            public RectTransform Root;
            public Image Pick;
            public EmoteMark Mark;
        }

        RectTransform heartRoot, wheelRoot, emoteLayer;
        CanvasGroup heartGroup;
        EmoteButton heartButton;
        readonly EmoteMark[] emoteMarks = new EmoteMark[8];
        readonly EmoteSlot[] wheelSlots = new EmoteSlot[Emotes.Length];
        // per hero (by its place in m.Heroes): when its emote started (match time) and which one; NaN = none
        readonly float[] emoteAt = new float[8];
        readonly int[] emoteKind = new int[8];
        readonly float[] emoteHeard = new float[8];
        float emoteLast = -1e9f, heldAt;
        bool holding;
        int wheelPick = -1;
        Vector2 holdFrom, holdNow;

        void BuildEmote()
        {
            // the bubbles over the heroes: with the overhead marks, under the HUD
            emoteLayer = UiKit.NewRect("Emotes", canvasRect);
            UiKit.Stretch(emoteLayer);
            emoteLayer.SetSiblingIndex(Mathf.Min(canvasRect.childCount - 1, overRoot.GetSiblingIndex() + 1));
            for (int i = 0; i < emoteMarks.Length; i++)
            {
                emoteMarks[i] = MakeEmoteMark(emoteLayer, true);
                emoteMarks[i].Root.gameObject.SetActive(false);
            }

            // the heart button
            heartRoot = UiKit.NewRect("Heart", safe);
            heartRoot.anchorMin = heartRoot.anchorMax = heartRoot.pivot = new Vector2(0.5f, 0.5f);
            heartGroup = heartRoot.gameObject.AddComponent<CanvasGroup>();
            var plate = UiKit.Fill(heartRoot, HeartPlate, UiKit.Circle);
            plate.raycastTarget = true;
            var edge = UiKit.Fill(heartRoot, new Color(1f, 1f, 1f, 0.3f), UiKit.CircleRing);
            edge.raycastTarget = false;
            var heart = UiKit.Fill(heartRoot, HeartRed, HeartSprite);
            heart.preserveAspect = true;
            UiKit.Stretch(heart.rectTransform);
            heart.rectTransform.anchorMin = new Vector2(0.25f, 0.27f);
            heart.rectTransform.anchorMax = new Vector2(0.75f, 0.77f);
            heartButton = heartRoot.gameObject.AddComponent<EmoteButton>();
            heartButton.Down = HeartDown;
            heartButton.Move = HeartMove;
            heartButton.Up = HeartUp;

            // the wheel, above everything else in the HUD while it is open
            wheelRoot = UiKit.NewRect("EmoteWheel", safe);
            wheelRoot.anchorMin = wheelRoot.anchorMax = wheelRoot.pivot = new Vector2(0.5f, 0.5f);
            var back = UiKit.Fill(wheelRoot, new Color(8f / 255f, 10f / 255f, 30f / 255f, 0.55f), UiKit.Circle);
            back.raycastTarget = false;
            var ring = UiKit.Fill(wheelRoot, new Color(1f, 1f, 1f, 0.18f), UiKit.CircleRing);
            ring.raycastTarget = false;
            for (int i = 0; i < wheelSlots.Length; i++)
            {
                var s = new EmoteSlot();
                s.Root = UiKit.NewRect("Slot" + i, wheelRoot);
                s.Root.anchorMin = s.Root.anchorMax = s.Root.pivot = new Vector2(0.5f, 0.5f);
                s.Pick = UiKit.Fill(s.Root, WheelPick, UiKit.Circle);
                s.Pick.enabled = false;
                s.Mark = MakeEmoteMark(s.Root, false);
                SetEmote(s.Mark, i);
                wheelSlots[i] = s;
            }
            wheelRoot.gameObject.SetActive(false);
        }

        void BeginEmote()
        {
            for (int i = 0; i < emoteAt.Length; i++) { emoteAt[i] = float.NaN; emoteKind[i] = 0; }
            emoteLast = -1e9f;
            holding = false; wheelPick = -1;
            if (wheelRoot != null) wheelRoot.gameObject.SetActive(false);
        }

        bool EmoteReady => Time.unscaledTime - emoteLast >= EmoteTime;

        void PlayerEmote(int kind)
        {
            if (!EmoteReady || m.State != MatchState.Play || tutorial) return;
            int me = m.Heroes.IndexOf(m.Player);
            if (me < 0 || me >= emoteAt.Length) return;
            emoteLast = Time.unscaledTime;
            emoteAt[me] = m.Time; emoteKind[me] = kind;
            // a bot answers with a heart now and then, as in the old offline game
            int n = 0;
            for (int i = 0; i < m.Heroes.Count && i < emoteAt.Length; i++) if (BotCanEmote(i)) n++;
            if (n > 0 && UnityEngine.Random.value < 0.6f)
            {
                int pick = UnityEngine.Random.Range(0, n);
                for (int i = 0; i < m.Heroes.Count && i < emoteAt.Length; i++)
                {
                    if (!BotCanEmote(i)) continue;
                    if (pick-- == 0) { emoteAt[i] = m.Time + UnityEngine.Random.Range(0.6f, 1.6f); emoteKind[i] = 0; break; }
                }
            }
        }

        bool BotCanEmote(int i)
        {
            var h = m.Heroes[i];
            return h != m.Player && h.Alive && h.IsRealHero && !(!float.IsNaN(emoteAt[i]) && m.Time - emoteAt[i] < EmoteTime);
        }

        // ---------------------------------------------------------------- the button and the wheel

        void HeartDown(Vector2 at)
        {
            if (m.State != MatchState.Play || tutorial) return;
            holding = true; heldAt = Time.unscaledTime; holdFrom = holdNow = at; wheelPick = -1;
        }

        void HeartMove(Vector2 at)
        {
            if (!holding) return;
            holdNow = at;
            wheelPick = -1;
            float best = SlotR * 1.6f;
            for (int i = 0; i < wheelSlots.Length; i++)
            {
                float d = (at - SlotCentre(i)).magnitude;
                if (d < best) { best = d; wheelPick = i; }
            }
        }

        void HeartUp(Vector2 at)
        {
            if (!holding) return;
            HeartMove(at);
            holding = false;
            bool quick = Time.unscaledTime - heldAt < WheelOpenTime && (holdNow - holdFrom).magnitude <= SafeW * 0.04f;
            if (wheelPick >= 0) PlayerEmote(wheelPick);
            else if (quick) PlayerEmote(0); // a short tap: the heart
            wheelPick = -1;
        }

        bool WheelShown => holding && (Time.unscaledTime - heldAt >= WheelOpenTime || (holdNow - holdFrom).magnitude > SafeW * 0.04f);
        float SafeW => safe.rect.width;
        float WheelR => SafeW * 0.21f;
        float SlotR => SafeW * 0.065f;

        // the heart: up and to the right of the SPECIAL, where the old first item button's arc put it (mirrored left-handed)
        Vector2 HeartCentre()
        {
            float W = SafeW;
            Vector3 c = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
            float side = leftHanded ? -1f : 1f;
            return new Vector2(c.x + side * 0.105f * W, c.y + 0.27f * W);
        }

        // the wheel opens above the button, kept on screen, so the thumb and the SPECIAL never cover it
        Vector2 WheelCentre()
        {
            Rect r = safe.rect;
            Vector2 e = HeartCentre();
            float mrg = WheelR + SlotR * 1.25f;
            return new Vector2(Mathf.Clamp(e.x, r.xMin + mrg, r.xMax - mrg), Mathf.Clamp(e.y + mrg * 1.1f, r.yMin + mrg, r.yMax - mrg));
        }

        Vector2 SlotCentre(int i)
        {
            float a = Mathf.PI / 2f - i * Mathf.PI * 2f / wheelSlots.Length; // from the top, clockwise
            return WheelCentre() + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * WheelR;
        }

        void EmoteFrame(bool hidden)
        {
            // the button: not in the guided match, not after the end; half seen while it waits
            bool button = !hidden && !tutorial && m.State != MatchState.Over;
            if (heartRoot.gameObject.activeSelf != button) heartRoot.gameObject.SetActive(button);
            if (!button || m.State != MatchState.Play) { holding = false; wheelPick = -1; }
            if (button)
            {
                float r = SafeW * 0.034f;
                heartRoot.sizeDelta = new Vector2(2f * r, 2f * r);
                heartRoot.localPosition = HeartCentre(); // all of the button's and the wheel's places are local to the safe area's pivot
                heartGroup.alpha = EmoteReady ? 1f : 0.5f;
            }

            bool wheel = WheelShown;
            if (wheelRoot.gameObject.activeSelf != wheel) { wheelRoot.gameObject.SetActive(wheel); if (wheel) wheelRoot.SetAsLastSibling(); }
            if (wheel)
            {
                Vector2 c = WheelCentre();
                float R = WheelR, sr = SlotR, back = 2f * (R + sr * 1.3f);
                wheelRoot.localPosition = c;
                wheelRoot.sizeDelta = new Vector2(back, back);
                float alpha = EmoteReady ? 1f : 0.5f;
                for (int i = 0; i < wheelSlots.Length; i++)
                {
                    var s = wheelSlots[i];
                    bool on = wheelPick == i;
                    float rr = sr * (on ? 1.18f : 1f);
                    s.Root.anchoredPosition = SlotCentre(i) - c;
                    s.Root.sizeDelta = new Vector2(2f * rr * 1.12f, 2f * rr * 1.12f);
                    s.Pick.enabled = on;
                    LayoutEmote(s.Mark, i, rr, alpha);
                }
            }

            // the bubbles over the heroes
            float ts = cam.PixelsPerTile * (Screen.width > 1f ? canvasRect.rect.width / Screen.width : 1f);
            for (int i = 0; i < emoteMarks.Length; i++)
            {
                var mk = emoteMarks[i];
                Unit u = i < m.Heroes.Count ? m.Heroes[i] : null;
                float a = float.IsNaN(emoteAt[i]) ? -1f : m.Time - emoteAt[i];
                Vector3 bar = Vector3.zero;
                bool show = !hidden && u != null && u.Alive && a >= 0f && a < EmoteTime && m.State != MatchState.Over
                            && (emoteKind[i] <= 1 || u.Team == m.Player.Team) && views.BarOf(u, out bar, out _);
                if (mk.Root.gameObject.activeSelf != show) mk.Root.gameObject.SetActive(show);
                if (!show) continue;
                // each emote pops with Speltest's emote sound once, also a bot's answer (Sound.cs "emote")
                if (emoteHeard[i] != emoteAt[i]) { emoteHeard[i] = emoteAt[i]; BamAudio.Play(SfxId.Emote, 0.8f); }
                Vector3 sp = cam.Camera.WorldToScreenPoint(bar);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 at);
                float size = ts * 1.05f;
                mk.Root.anchoredPosition = at + new Vector2(0f, size * 0.77f); // the old by - size * 1.55 against the bar's by - size * 0.78
                float r = ts * 0.42f * Mathf.Min(1f, a * 6f) * (a > EmoteTime - 0.25f ? (EmoteTime - a) / 0.25f : 1f);
                SetEmote(mk, emoteKind[i]);
                LayoutEmote(mk, emoteKind[i], r, 1f);
            }
        }

        // ---------------------------------------------------------------- one emote's picture

        EmoteMark MakeEmoteMark(Transform parent, bool shadow)
        {
            var mk = new EmoteMark();
            mk.Root = UiKit.NewRect("Emote", parent);
            mk.Root.anchorMin = mk.Root.anchorMax = mk.Root.pivot = new Vector2(0.5f, 0.5f);
            mk.Root.sizeDelta = Vector2.zero;
            if (shadow)
            {
                mk.Shadow = MarkImage(mk.Root, "Shadow", new Color(0f, 0f, 0f, 0.25f));
            }
            mk.Body = MarkImage(mk.Root, "Body", BubbleWhite);
            mk.Heart = MarkImage(mk.Root, "Heart", HeartRed);
            mk.Heart.sprite = HeartSprite;
            mk.Heart.preserveAspect = true;
            mk.Word = UiKit.Label(mk.Root, "", 20, TextAnchor.MiddleCenter, BubbleInk);
            mk.Word.raycastTarget = false;
            mk.Word.horizontalOverflow = HorizontalWrapMode.Overflow;
            mk.Word.verticalOverflow = VerticalWrapMode.Overflow;
            mk.Word.rectTransform.anchorMin = mk.Word.rectTransform.anchorMax = mk.Word.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return mk;
        }

        static Image MarkImage(RectTransform parent, string name, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static void SetEmote(EmoteMark mk, int kind)
        {
            bool heart = kind <= 0 || kind >= Emotes.Length;
            if (mk.Heart.enabled != heart) mk.Heart.enabled = heart;
            if (mk.Word.enabled == heart) mk.Word.enabled = !heart;
            string t = heart ? "" : Emotes[kind];
            if (mk.Word.text != t) mk.Word.text = t;
        }

        // drawEmoteKind: the heart in a round white bubble, a word in a white pill (its font smaller for longer words)
        static void LayoutEmote(EmoteMark mk, int kind, float r, float alpha)
        {
            bool on = r > 0.5f;
            if (mk.Body.gameObject.activeSelf != on) mk.Body.gameObject.SetActive(on);
            if (!on) { if (mk.Shadow != null) mk.Shadow.enabled = false; mk.Heart.enabled = false; mk.Word.enabled = false; return; }
            bool heart = kind <= 0 || kind >= Emotes.Length;
            Vector2 box;
            if (heart)
            {
                box = new Vector2(2f * r, 2f * r);
                mk.Body.sprite = UiKit.Circle;
                mk.Body.type = Image.Type.Simple;
                mk.Heart.enabled = true;
                mk.Heart.rectTransform.sizeDelta = new Vector2(r * 1.25f, r * 1.25f);
                mk.Heart.rectTransform.anchoredPosition = new Vector2(0f, -r * 0.05f);
                mk.Heart.color = new Color(HeartRed.r, HeartRed.g, HeartRed.b, alpha);
            }
            else
            {
                string t = Emotes[kind];
                mk.Word.enabled = true;
                mk.Word.fontSize = Mathf.Max(6, Mathf.RoundToInt(r * (t.Length <= 3 ? 0.8f : t.Length <= 5 ? 0.62f : 0.5f)));
                float h = r * 1.5f, w = Mathf.Max(r * 2f, mk.Word.preferredWidth + r * 0.7f);
                box = new Vector2(w, h);
                mk.Body.sprite = UiKit.Rounded;
                mk.Body.type = Image.Type.Sliced;
                Corners(mk.Body, h * 0.5f);
                mk.Word.rectTransform.sizeDelta = box;
                mk.Word.rectTransform.anchoredPosition = new Vector2(0f, -1f);
                var c = BubbleInk; c.a = alpha; mk.Word.color = c;
            }
            mk.Body.rectTransform.sizeDelta = box;
            var bc = BubbleWhite; bc.a = alpha; mk.Body.color = bc;
            if (mk.Shadow != null)
            {
                mk.Shadow.enabled = true;
                mk.Shadow.sprite = mk.Body.sprite;
                mk.Shadow.type = mk.Body.type;
                mk.Shadow.pixelsPerUnitMultiplier = mk.Body.pixelsPerUnitMultiplier;
                mk.Shadow.rectTransform.sizeDelta = box;
                mk.Shadow.rectTransform.anchoredPosition = new Vector2(0f, -r * 0.08f);
            }
        }

        // drawHeart: two bezier lobes meeting at a point at the bottom, filled
        static Sprite heartSprite;
        static Sprite HeartSprite
        {
            get
            {
                if (heartSprite != null) return heartSprite;
                const int n = 96;
                float r = n * 0.4f, cx = n / 2f, cy = n / 2f;
                // the outline in the old canvas's y-down space, centred on (cx, cy)
                var pts = new System.Collections.Generic.List<Vector2>();
                Vector2 p0 = new Vector2(cx, cy + r * 0.75f);
                AddCubic(pts, p0, new Vector2(cx - r * 1.25f, cy - r * 0.05f), new Vector2(cx - r * 0.6f, cy - r * 0.95f), new Vector2(cx, cy - r * 0.35f));
                AddCubic(pts, new Vector2(cx, cy - r * 0.35f), new Vector2(cx + r * 0.6f, cy - r * 0.95f), new Vector2(cx + r * 1.25f, cy - r * 0.05f), p0);
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Heart", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // 4x4 samples per pixel for a smooth edge; texture rows go up, the outline's y goes down
                        int hits = 0;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                                if (Inside(pts, new Vector2(x + (sx + 0.5f) / 4f, n - (y + (sy + 0.5f) / 4f)))) hits++;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                heartSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return heartSprite;
            }
        }

        static void AddCubic(System.Collections.Generic.List<Vector2> pts, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            for (int i = 0; i <= 24; i++)
            {
                float t = i / 24f, u = 1f - t;
                pts.Add(u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d);
            }
        }

        static bool Inside(System.Collections.Generic.List<Vector2> poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }
    }

    /// <summary>The emote heart's touch: down, moves and up in the HUD's safe-area coordinates.</summary>
    sealed class EmoteButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public Action<Vector2> Down, Move, Up;
        int pointer = int.MinValue;

        /// <summary>Forget the finger (no Up): an inactive button never gets its OnPointerUp, so a stale id would block every later press.</summary>
        public void Release() => pointer = int.MinValue;

        void OnDisable() => pointer = int.MinValue;

        public void OnPointerDown(PointerEventData e)
        {
            if (pointer != int.MinValue) return;
            pointer = e.pointerId;
            Down?.Invoke(Local(e.position));
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId == pointer) Move?.Invoke(Local(e.position));
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != pointer) return;
            pointer = int.MinValue;
            Up?.Invoke(Local(e.position));
        }

        Vector2 Local(Vector2 screen)
        {
            var safe = (RectTransform)transform.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(safe, screen, null, out Vector2 v);
            return v;
        }
    }
}
