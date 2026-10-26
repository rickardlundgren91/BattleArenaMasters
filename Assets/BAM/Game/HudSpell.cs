using System.Collections.Generic;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The battle spell button (ABC2 HudSpell.cs, Rickard 2026-10-23; Sprint added 2026-10-26): a round button left of and below
    /// the SPECIAL (mirrored left-handed). A tap casts it; Blink goes the way the joystick steers, or the way the hero faces.
    /// Blink can also be dragged to aim (direction and distance, the landing ring on the ground) and fires on release; letting
    /// go on the cancel mark above the button casts nothing, as with the SPECIAL. The ring round the glyph fills while it
    /// recharges with the seconds in the middle, and shows the recall's channel. "Recall", "Haste", "Sprint" and "Cancelled"
    /// rise over the hero as in the old floatText. Hidden in the guided match (Match.SpellsOn).
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color SpellDisc = Hex(0x1d2a55), SpellIdle = Hex(0x8fa8c8), SpellGlyphIdle = Hex(0xaab8d0), SpellCancelled = Hex(0xc8d0e8);

        RectTransform spellRoot, spellAimRoot, spellCancelMark, spellMaxRing, spellDot;
        Image spellArc, spellGlyph, spellCancelFill, spellCancelEdge, spellDotImg;
        Text spellSecs, spellCancelText;
        SpellId spellShownId = SpellId.None;
        bool spellAiming, spellMoved, spellOverCancel;
        float spellAng, spellK;
        Vector2 spellFrom, spellFinger;
        bool spellQueued;
        float spellQueuedAngle, spellQueuedK;

        void BuildSpell()
        {
            spellRoot = UiKit.NewRect("Spell", safe);
            spellRoot.anchorMin = spellRoot.anchorMax = spellRoot.pivot = new Vector2(0.5f, 0.5f);
            var disc = UiKit.Fill(spellRoot, SpellDisc, UiKit.Circle);
            disc.raycastTarget = true;
            var track = UiKit.Fill(spellRoot, new Color(1f, 1f, 1f, 0.15f), SpellRingSprite);
            track.raycastTarget = false;
            spellArc = UiKit.Fill(spellRoot, SpellIdle, SpellRingSprite);
            spellArc.type = Image.Type.Filled;
            spellArc.fillMethod = Image.FillMethod.Radial360;
            spellArc.fillOrigin = (int)Image.Origin360.Top;
            spellArc.fillClockwise = true;
            spellGlyph = UiKit.Fill(spellRoot, Color.white);
            spellSecs = UiKit.Label(spellRoot, "", 30, TextAnchor.MiddleCenter, Color.white);
            spellSecs.raycastTarget = false;
            UiKit.Stretch(spellSecs.rectTransform, -20f, -20f, -20f, -20f);
            var touch = spellRoot.gameObject.AddComponent<EmoteButton>(); // down, moves and up in the safe area's coordinates
            touch.Down = SpellDown;
            touch.Move = SpellMove;
            touch.Up = SpellUp;

            // the drag: the cancel mark above the button, the reach ring and the finger's dot (old drawSpellAim)
            spellAimRoot = UiKit.NewRect("SpellAim", safe);
            spellAimRoot.anchorMin = spellAimRoot.anchorMax = spellAimRoot.pivot = new Vector2(0.5f, 0.5f);
            spellAimRoot.sizeDelta = Vector2.zero;
            spellMaxRing = UiKit.NewRect("Reach", spellAimRoot);
            spellMaxRing.anchorMin = spellMaxRing.anchorMax = spellMaxRing.pivot = new Vector2(0.5f, 0.5f);
            var reach = spellMaxRing.gameObject.AddComponent<Image>();
            reach.raycastTarget = false;
            reach.color = new Color(1f, 1f, 1f, 0.35f);
            spellCancelMark = UiKit.NewRect("Cancel", spellAimRoot);
            spellCancelMark.anchorMin = spellCancelMark.anchorMax = spellCancelMark.pivot = new Vector2(0.5f, 0.5f);
            spellCancelFill = UiKit.Fill(spellCancelMark, Color.black, UiKit.Circle);
            spellCancelEdge = UiKit.Fill(spellCancelMark, Color.white, null);
            for (int i = 0; i < 2; i++)
            {
                var bar = UiKit.NewRect("Cross", spellCancelMark);
                bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                bar.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
                var img = bar.gameObject.AddComponent<Image>();
                img.sprite = UiKit.Rounded;
                img.type = Image.Type.Sliced;
                img.raycastTarget = false;
            }
            spellDot = UiKit.NewRect("Finger", spellAimRoot);
            spellDot.anchorMin = spellDot.anchorMax = spellDot.pivot = new Vector2(0.5f, 0.5f);
            spellDotImg = spellDot.gameObject.AddComponent<Image>();
            spellDotImg.sprite = UiKit.Circle;
            spellDotImg.raycastTarget = false;
            spellCancelText = UiKit.Label(spellAimRoot, "CANCEL", 30, TextAnchor.MiddleCenter, Hex(0xffb3b3));
            spellCancelText.raycastTarget = false;
            spellCancelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            spellCancelText.rectTransform.anchorMin = spellCancelText.rectTransform.anchorMax = spellCancelText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            spellAimRoot.gameObject.SetActive(false);
            spellRoot.gameObject.SetActive(false);
        }

        void BeginSpell()
        {
            spellAiming = spellMoved = spellOverCancel = false;
            spellQueued = false;
            spellShownId = SpellId.None;
            if (spellRoot != null) { spellRoot.gameObject.SetActive(false); spellAimRoot.gameObject.SetActive(false); }
        }

        bool SpellShown => m.SpellsOn && m.State == MatchState.Play && m.Player != null && m.Player.Spell != SpellId.None;
        float SpellR => SafeW * 0.05f;

        // SPELL_BTN: 1.58 SPECIAL radii to the inner side of the SPECIAL's centre, 0.7 radii lower
        Vector2 SpellCentre()
        {
            Vector3 c = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
            float sr = SpecialButton.Radius, side = leftHanded ? 1f : -1f;
            return new Vector2(c.x + side * sr * 1.58f, c.y - sr * 0.7f);
        }

        Vector2 SpellCancelCentre() => SpellCentre() + new Vector2(0f, SpellR * 3.8f);

        static Color SpellColor(SpellId s)
        {
            int c = SpellDefs.Color(s);
            return Hex(c);
        }

        // ---------------------------------------------------------------- touch

        void SpellDown(Vector2 at)
        {
            if (!SpellShown) return;
            if (m.Player.Spell != SpellId.Blink) { SpellPress(); return; }
            spellAiming = true; spellMoved = false; spellOverCancel = false; spellFrom = spellFinger = at; // Blink: tap = the way you move, drag = aim
        }

        void SpellMove(Vector2 at)
        {
            if (!spellAiming) return;
            float r = SpellR;
            Vector2 b = SpellCentre(), d = at - b;
            if (!spellMoved && (at - spellFrom).magnitude > r * 0.5f) spellMoved = true;
            if (!spellMoved) return;
            spellFinger = at;
            spellOverCancel = (at - SpellCancelCentre()).magnitude < r * 1.3f; // only the cancel mark cancels
            spellAng = Mathf.Atan2(-d.y, d.x); // screen up is -y on the map
            spellK = Mathf.Clamp01((d.magnitude - r * 0.35f) / (r * 2.5f));
        }

        void SpellUp(Vector2 at)
        {
            if (!spellAiming) return;
            SpellMove(at);
            spellAiming = false;
            if (!spellMoved) SpellPress();
            else if (!spellOverCancel) QueueSpell(spellAng, spellK);
        }

        // a tap: Blink goes the way the joystick steers, or the way the hero faces when it is still
        void SpellPress()
        {
            Vector2 v = joy != null ? joy.Value : Vector2.zero;
            QueueSpell(v.sqrMagnitude > 0f ? Mathf.Atan2(-v.y, v.x) : float.NaN, 1f);
        }

        void QueueSpell(float angle, float k)
        {
            if (m == null || !m.Player.Alive || !SpellShown) return;
            spellQueued = true; spellQueuedAngle = angle; spellQueuedK = k;
        }

        /// <summary>Puts a queued cast into this tick's input (called by TakeInput).</summary>
        void TakeSpell(ref PlayerInput input)
        {
            if (!spellQueued) return;
            spellQueued = false;
            input.Spell = true;
            input.SpellAngle = spellQueuedAngle;
            input.SpellK = spellQueuedK;
        }

        void CancelSpellTouch()
        {
            spellAiming = false; spellMoved = false; spellOverCancel = false; spellQueued = false;
        }

        // ---------------------------------------------------------------- every frame

        void SpellFrame(bool hidden)
        {
            var p = m.Player;
            bool show = !hidden && SpellShown;
            if (spellRoot.gameObject.activeSelf != show) spellRoot.gameObject.SetActive(show);
            if (!show) spellAiming = false;
            bool aim = show && spellAiming && spellMoved && p.Alive && p.Spell == SpellId.Blink;
            if (spellAimRoot.gameObject.activeSelf != aim) { spellAimRoot.gameObject.SetActive(aim); if (aim) spellAimRoot.SetAsLastSibling(); }
            rings.SetBlink(false, Vector2.zero, Color.white);
            if (!show) return;

            float r = SpellR;
            Vector2 b = SpellCentre();
            spellRoot.localPosition = b;
            spellRoot.sizeDelta = new Vector2(2f * r, 2f * r);
            if (spellShownId != p.Spell)
            {
                spellShownId = p.Spell;
                spellGlyph.sprite = SpellGlyphSprite(p.Spell);
            }

            bool chan = Match.SpellChanneling(p);
            float left = m.SpellLeft(p), cd = SpellDefs.Cooldown(p.Spell);
            // a cooldown under a second (Recall's 0.1 s) is not worth a countdown: the button simply stays lit
            bool ready = p.Alive && (m.SpellReady(p) || cd < 1f);
            float frac = chan ? Mathf.Clamp01((m.Time - p.SpellChanStart) / SpellDefs.RecallChannel) : ready ? 1f : Mathf.Clamp01(1f - left / Mathf.Max(0.01f, cd));
            Color col = SpellColor(p.Spell);
            spellArc.fillAmount = frac;
            spellArc.color = ready || chan ? col : SpellIdle;
            spellGlyph.color = ready || chan ? col : new Color(SpellGlyphIdle.r, SpellGlyphIdle.g, SpellGlyphIdle.b, 0.5f);
            bool secs = !ready && !chan && p.Alive;
            if (spellSecs.enabled != secs) spellSecs.enabled = secs;
            if (secs)
            {
                SetText(spellSecs, Mathf.CeilToInt(left).ToString());
                spellSecs.fontSize = Mathf.RoundToInt(r * 0.62f * 1.35f);
            }

            if (!aim) return;
            // the drag: reach ring, the cancel mark, the finger's dot, and the landing ring on the ground
            spellAimRoot.localPosition = b;
            float mx = r * 2.85f;
            spellMaxRing.sizeDelta = new Vector2(2f * mx, 2f * mx);
            RoundRing(spellMaxRing.GetComponent<Image>(), mx, 2f);
            bool on = spellOverCancel;
            float zr = r * 1.3f * (on ? 1.12f : 1f);
            spellCancelMark.anchoredPosition = new Vector2(0f, r * 3.8f);
            spellCancelMark.sizeDelta = new Vector2(2f * zr, 2f * zr);
            spellCancelFill.color = on ? new Color(0.9f, 0.2f, 0.2f, 0.55f) : new Color(0.04f, 0.05f, 0.1f, 0.5f);
            RoundRing(spellCancelEdge, zr, 2f);
            spellCancelEdge.color = on ? new Color(1f, 0.55f, 0.55f, 0.95f) : new Color(1f, 1f, 1f, 0.5f);
            float k = zr * 0.38f, lw = Mathf.Max(3f, zr * 0.16f);
            for (int i = 0; i < spellCancelMark.childCount; i++)
            {
                var ch = spellCancelMark.GetChild(i);
                if (ch.name != "Cross") continue;
                var img = ch.GetComponent<Image>();
                ((RectTransform)ch).sizeDelta = new Vector2(2f * k * 1.414f + lw, lw);
                Corners(img, lw / 2f);
                img.color = on ? Color.white : new Color(1f, 1f, 1f, 0.75f);
            }
            Vector2 kd = spellFinger - b;
            if (kd.magnitude > mx) kd *= mx / kd.magnitude;
            spellDot.anchoredPosition = kd;
            spellDot.sizeDelta = new Vector2(r * 1.1f, r * 1.1f);
            spellDotImg.color = on ? new Color(1f, 90f / 255f, 90f / 255f, 0.85f) : new Color(1f, 1f, 1f, 0.85f);
            if (spellCancelText.enabled != on) spellCancelText.enabled = on;
            spellCancelText.fontSize = Mathf.RoundToInt(Mathf.Max(11f, r * 0.6f) * 1.35f);
            spellCancelText.rectTransform.anchoredPosition = new Vector2(0f, r * 1.5f);
            if (!on)
            {
                m.BlinkPoint(p, spellAng, spellK, out float x, out float y);
                rings.SetBlink(true, new Vector2(x, y), col);
            }
        }

        // the old floatText for the spells: who cast what, rising over the hero
        void OnSpellEvent(SimEvent e)
        {
            var u = e.A;
            if (u == null || !u.Alive || (u.Team != m.Player.Team && !m.IsVisibleTo(u, m.Player.Team))) return;
            // only a cancel gets a word; casting a spell shows no "Haste"/"Sprint"/"Recall" text, its look on the hero is enough
            // (Rickard 2026-10-26: "ska inte stå typ haste bara när man får den")
            if (e.Type == EventType.SpellCancel) FloatWord(u, "Cancelled", SpellCancelled);
        }

        // ---------------------------------------------------------------- pictures

        // the ring round the glyph: 0.16 of the radius wide at 0.9 of it
        static Sprite spellRingSprite;
        static Sprite SpellRingSprite
        {
            get
            {
                if (spellRingSprite != null) return spellRingSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.SpellRing", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float h = n / 2f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude / h;
                        float a = Mathf.Clamp01((0.08f - Mathf.Abs(d - 0.9f)) * h + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                spellRingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return spellRingSprite;
            }
        }

        // SpellGlyph: Blink two chevrons, Recall a house, Shield a shield, Haste a lightning bolt; Sprint (new) an arrow with speed
        // lines. White, tinted by the button; drawn in the old canvas's y-down space with k = half the button's radius.
        static readonly Dictionary<SpellId, Sprite> glyphs = new Dictionary<SpellId, Sprite>();
        static Sprite SpellGlyphSprite(SpellId s)
        {
            if (glyphs.TryGetValue(s, out Sprite sp)) return sp;
            const int n = 96;
            float k = n * 0.25f, cx = n / 2f, cy = n / 2f, lw = k * 0.28f;
            var fills = new List<List<Vector2>>();
            var strokes = new List<Vector2[]>();
            Vector2 P(float x, float y) => new Vector2(cx + x * k, cy + y * k);
            switch (s)
            {
                case SpellId.Blink:
                    for (int i = 0; i < 2; i++)
                    {
                        float o = (i - 0.5f) * 0.9f;
                        strokes.Add(new[] { P(o - 0.35f, -0.7f), P(o + 0.35f, 0f), P(o - 0.35f, 0.7f) });
                    }
                    break;
                case SpellId.Recall:
                    fills.Add(new List<Vector2> { P(-1f, 0f), P(0f, -1f), P(1f, 0f), P(0.7f, 0f), P(0.7f, 0.9f), P(-0.7f, 0.9f), P(-0.7f, 0f) });
                    break;
                case SpellId.Shield:
                    fills.Add(new List<Vector2> { P(-0.8f, -0.8f), P(0.8f, -0.8f), P(0.8f, 0.1f), P(0f, 1f), P(-0.8f, 0.1f) });
                    break;
                case SpellId.Sprint:
                    fills.Add(new List<Vector2> { P(-0.05f, -0.8f), P(0.95f, 0f), P(-0.05f, 0.8f), P(0.2f, 0f) });
                    strokes.Add(new[] { P(-0.95f, -0.35f), P(-0.3f, -0.35f) });
                    strokes.Add(new[] { P(-0.95f, 0.35f), P(-0.3f, 0.35f) });
                    strokes.Add(new[] { P(-0.7f, 0f), P(-0.15f, 0f) });
                    break;
                default: // Haste
                    fills.Add(new List<Vector2> { P(0.3f, -1f), P(-0.6f, 0.15f), P(-0.05f, 0.15f), P(-0.3f, 1f), P(0.6f, -0.2f), P(0.05f, -0.2f) });
                    break;
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Spell" + s, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                        {
                            var q = new Vector2(x + (sx + 0.5f) / 4f, n - (y + (sy + 0.5f) / 4f)); // texture rows go up
                            bool hit = false;
                            foreach (var f in fills) if (Inside(f, q)) { hit = true; break; }
                            if (!hit)
                                foreach (var l in strokes)
                                {
                                    for (int i = 1; i < l.Length && !hit; i++) if (SegDist(q, l[i - 1], l[i]) <= lw / 2f) hit = true;
                                    if (hit) break;
                                }
                            if (hit) hits++;
                        }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            sp = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            glyphs[s] = sp;
            return sp;
        }
    }
}
