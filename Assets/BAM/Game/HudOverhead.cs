using System.Collections.Generic;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The marks the old game draws by the heroes' health bars (ABC2 Hud.cs drawHero): the name over every hero
    /// (white with a dark edge), a thin crossed-out eye over a hero of the player's team while the enemy cannot see it (fading in
    /// and out over about 0.15 s), a small pulsing red diamond by an enemy's bar while its special is ready
    /// (HudTelegraph.cs drawCharged), the interrupt window as a bar under the health bar (drawChannel),
    /// The special's charge is the thin row in the 3D bar itself (HealthBar), on the own team only; no level badge (Rickard 2026-10-28 and again 2026-10-30,
    /// the HUD top shows the level). Placed each frame over the art's 3D bars.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color NameAlly = Hex(0x9fd4ff), NameFoe = Hex(0xff9aa4);
        static readonly Color EyeStroke = Hex(0xdfe9e6), EyeEdge = Hex(0x08101a), SeenEye = Hex(0xff5a5a);

        sealed class Overhead
        {
            public RectTransform Root;
            public Text Name;
            public Image Eye, Diamond, ChanBack, ChanFill;
            public Image ItemMark; public readonly Image[] Echo = new Image[ItemS.EchoMax]; // HudItems.cs
            public Outline NameEdge, DiamondEdge;   // kept from the build, so no GetComponent every frame
            public float EyeA;
            // name tags pushed apart where they collide (video review 2026-11-03: "MTyraFenn" when three stood together):
            // NameW = the label's width, measured only when its text or size changes; NameX = the sideways shift now, PushX = the
            // shift it eases towards (Declutter)
            public float NameW, NameX, PushX;
            public int NameFont;
            public bool Shown;
        }

        readonly Overhead[] overheads = new Overhead[10];
        readonly List<Unit> overUnits = new List<Unit>(10);
        RectTransform overRoot;

        void BuildOverhead()
        {
            overRoot = UiKit.NewRect("Overhead", canvasRect);
            UiKit.Stretch(overRoot);
            overRoot.SetAsFirstSibling(); // under the HUD
            overRoot.gameObject.AddComponent<Canvas>(); // follows the heroes every frame: rebuilt alone, not the whole HUD (PERF-02)
            for (int i = 0; i < overheads.Length; i++)
            {
                var o = new Overhead();
                o.Root = UiKit.NewRect("Hero", overRoot);
                o.Root.anchorMin = o.Root.anchorMax = new Vector2(0.5f, 0.5f);
                o.Root.sizeDelta = Vector2.zero;
                o.Name = UiKit.Label(o.Root, "", 26, TextAnchor.LowerCenter, Color.white);
                o.Name.rectTransform.anchorMin = o.Name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                o.Name.rectTransform.pivot = new Vector2(0.5f, 0f);
                o.Name.rectTransform.sizeDelta = new Vector2(300f, 40f);
                o.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                o.Name.raycastTarget = false;
                o.NameEdge = o.Name.gameObject.AddComponent<Outline>();
                o.NameEdge.effectColor = new Color(8f / 255f, 10f / 255f, 24f / 255f, 0.9f);
                o.NameEdge.effectDistance = new Vector2(1.5f, -1.5f);
                var eye = UiKit.NewRect("Eye", o.Root);
                eye.anchorMin = eye.anchorMax = eye.pivot = new Vector2(0.5f, 0.5f);
                o.Eye = eye.gameObject.AddComponent<Image>();
                o.Eye.sprite = EyeSprite;
                o.Eye.raycastTarget = false;
                var dia = UiKit.NewRect("Charged", o.Root);
                dia.anchorMin = dia.anchorMax = dia.pivot = new Vector2(0.5f, 0.5f);
                dia.localRotation = Quaternion.Euler(0f, 0f, 45f);
                o.Diamond = dia.gameObject.AddComponent<Image>();
                o.Diamond.raycastTarget = false;
                o.DiamondEdge = o.Diamond.gameObject.AddComponent<Outline>();
                o.DiamondEdge.effectColor = new Color(0.07f, 0.05f, 0.09f, 0.9f);
                o.DiamondEdge.effectDistance = new Vector2(1.5f, -1.5f);
                o.ChanBack = Plain(o.Root, "ChannelBack", new Color(8f / 255f, 10f / 255f, 24f / 255f, 0.85f));
                o.ChanFill = Plain(o.ChanBack.rectTransform, "Fill", Color.white);
                o.ChanFill.rectTransform.anchorMin = o.ChanFill.rectTransform.anchorMax = o.ChanFill.rectTransform.pivot = new Vector2(0f, 0.5f);
                BuildItemMarks(o);
                o.Root.gameObject.SetActive(false);
                overheads[i] = o;
            }
        }

        void BeginOverhead()
        {
            for (int i = 0; i < overheads.Length; i++) { overheads[i].EyeA = 0f; overheads[i].NameX = overheads[i].PushX = 0f; overheads[i].Root.gameObject.SetActive(false); }
        }

        void OverheadFrame(bool hidden)
        {
            bool on = !hidden && !(m.Tut != null && m.Tut.Island) && m.State != MatchState.Over;
            // one tile in canvas units: the camera frames MatchCamera's TilesAcross over the standing frame, which is narrower than the canvas
            // on a wide window, so the marks are sized from the camera, not from the canvas
            float ts = OverheadTile();
            float dt = Time.unscaledDeltaTime;
            // the heroes, then the live Fenn clones (they are not in m.Heroes): an enemy decoy must wear Fenn's name too
            overUnits.Clear();
            overUnits.AddRange(m.Heroes);
            for (int i = 0; i < m.Units.Count && overUnits.Count < overheads.Length; i++)
                if (m.Units[i].IsClone && m.Units[i].Alive) overUnits.Add(m.Units[i]);
            for (int i = 0; i < overheads.Length; i++)
            {
                var o = overheads[i];
                Unit u = i < overUnits.Count ? overUnits[i] : null;
                Vector3 bar = Vector3.zero; float bw = 0f;
                bool show = on && u != null && u.Alive && views.BarOf(u, out bar, out bw);
                if (o.Root.gameObject.activeSelf != show) o.Root.gameObject.SetActive(show);
                o.Shown = show;
                if (!show) { o.EyeA = 0f; o.NameX = o.PushX = 0f; continue; }
                Vector3 sp = cam.Camera.WorldToScreenPoint(bar);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 at);
                o.Root.anchoredPosition = at;
                float size = ts * 1.05f;

                // the name over every hero (Rickard 2026-10-26 "ha namn över alla spelare", the old game showed only your own):
                // small with a dark edge just above the bar; "You" in white for the player, the hero's name in a light team colour
                // a clone wears its owner's name and charged mark; the charge bar stays under the real hero only
                Unit who = u.IsClone && u.Owner != null ? u.Owner : u;
                bool mine = who == m.Player;
                if (!o.Name.gameObject.activeSelf) o.Name.gameObject.SetActive(true);
                // online: the other people's names (Rickard 2026-10-30 "när man kör med andra spelare ska man se deras namn"), bots keep the hero's
                var seats = m.Config.Seats;
                string person = !mine && seats != null && who.Seat >= 0 && who.Seat < seats.Length ? seats[who.Seat].Name : null;
                string label = mine ? "You" : !string.IsNullOrEmpty(person) ? person : HeroCatalog.Get(who.Hero).Name;
                int font = Mathf.Max(8, Mathf.RoundToInt(ts * (mine ? 0.27f : 0.23f)));
                if (o.Name.text != label || o.NameFont != font)
                {
                    o.Name.text = label; o.Name.fontSize = font; o.NameFont = font;
                    o.NameW = o.Name.preferredWidth;
                }
                o.Name.color = mine ? Color.white : u.Team == m.Player.Team ? NameAlly : NameFoe;
                o.Name.rectTransform.sizeDelta = new Vector2(ts * 4f, ts * 0.5f);
                o.Name.rectTransform.anchoredPosition = new Vector2(o.NameX, size * 0.17f);
                {
                    float ew = Mathf.Max(1f, ts * 0.035f);
                    o.NameEdge.effectDistance = new Vector2(ew, -ew);
                }

                // the crossed-out eye while the enemy cannot see this hero of yours; an open red eye while it stands in grass but
                // the enemy sees it (a hit in the last 2 s, its own auto attacks on minions too, an enemy within 2.5 tiles, the
                // enemy tower's reach), so a player knows the grass is not hiding them (tester feedback 2026-11-04)
                bool hiddenEye = u.Team == m.Player.Team && m.HiddenFromFoes(u);
                bool seenEye = !hiddenEye && u.Team == m.Player.Team && u.Alive && m.Map.InGrass(u.X, u.Y);
                if (hiddenEye || seenEye) { var want = seenEye ? SeenEyeSprite : EyeSprite; if (o.Eye.sprite != want) o.Eye.sprite = want; }
                o.EyeA = hiddenEye || seenEye ? Mathf.Min(1f, o.EyeA + dt * 7f) : Mathf.Max(0f, o.EyeA - dt * 7f);
                bool eyeOn = o.EyeA > 0.01f;
                if (o.Eye.enabled != eyeOn) o.Eye.enabled = eyeOn;
                if (eyeOn)
                {
                    float r = ts * 0.22f, pop = 0.8f + 0.2f * o.EyeA;
                    o.Eye.rectTransform.sizeDelta = new Vector2(r * 3.2f * pop, r * 3.2f * pop);
                    // its bottom on the name's top, not over the name (it was centred half a tile up, its lower half on the letters)
                    o.Eye.rectTransform.anchoredPosition = new Vector2(0f, ts * (NameTop + 0.02f) + r * 1.6f * pop);
                    o.Eye.color = o.Eye.sprite == SeenEyeSprite ? new Color(SeenEye.r, SeenEye.g, SeenEye.b, o.EyeA) : new Color(1f, 1f, 1f, o.EyeA);
                }

                // an enemy whose special is ready (drawCharged): a pulsing red diamond by the right end of its bar (it can fire now).
                // Enemies only: an enemy's bar has no charge row (MatchViews), and a teammate's row already turns gold when ready, so
                // the mint diamond on teammates showed it twice (review 2026-11-13, nr 9; Rickard 2026-11-13)
                bool charged = u.Team != m.Player.Team && who.SpecialReady;
                if (o.Diamond.enabled != charged) o.Diamond.enabled = charged;
                if (charged)
                {
                    float r = ts * 0.17f * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f) + 0.25f), side = r * 1.414f;
                    o.Diamond.rectTransform.sizeDelta = new Vector2(side, side);
                    o.Diamond.rectTransform.anchoredPosition = new Vector2(bw * ts / 2f + 8f, 0f);
                    o.Diamond.color = new Color(1f, 0.16f, 0.2f, 0.95f);
                    o.DiamondEdge.effectColor = new Color(1f, 0.85f, 0.85f, 0.9f);
                }

                float under = -ts * 0.17f - 4f; // under the bar and its charge row

                // the interrupt window (drawChannel): a small bar under the health bar empties as it closes, orange and
                // blinking on an enemy (stun it now), pale on your own team
                float chT = 0f, chV = 0f;
                for (int s = 0; s < u.StatusCount; s++)
                {
                    var st = u.GetStatus(s);
                    if (st.Type == St.Channel && st.T > 0f) { chT = st.T; chV = st.V; break; }
                }
                bool chan = chV > 0f;
                if (o.ChanBack.enabled != chan) { o.ChanBack.enabled = chan; o.ChanFill.enabled = chan; }
                if (chan)
                {
                    bool foe = u.Team != m.Player.Team;
                    float k = Mathf.Clamp01(chT / chV), cw = bw * ts * 0.8f;
                    o.ChanBack.rectTransform.sizeDelta = new Vector2(cw + 2f, 6f);
                    o.ChanBack.rectTransform.anchoredPosition = new Vector2(0f, under);
                    o.ChanFill.rectTransform.anchoredPosition = new Vector2(1f, 0f);
                    o.ChanFill.rectTransform.sizeDelta = new Vector2(cw * k, 4f);
                    o.ChanFill.color = foe ? new Color(1f, 0.62f, 0.15f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 30f)) : new Color(1f, 1f, 1f, 0.7f);
                }
                ItemMarksFrame(o, u, ts, bw);
            }
            Declutter(ts, dt);
        }

        /// <summary>Name tags that would lie on each other slide sideways apart (video review 2026-11-03: three heroes in a clump
        /// read "MTyraFenn"). Only sideways, so the stack above the bar (eye, poison bubbles, rising numbers) stays free; each tag
        /// eases to its spot (no jitter while the clump moves) and back over its own bar once there is room again.</summary>
        void Declutter(float ts, float dt)
        {
            const float Gap = 0.08f; // tiles between two neighbouring tags
            float rowH = ts * 0.32f, maxShift = ts * 1.6f;
            for (int i = 0; i < overheads.Length; i++) overheads[i].PushX = 0f;
            // a few rounds of pushing each colliding pair apart by half their overlap each, left one left, right one right
            for (int round = 0; round < 4; round++)
            {
                bool moved = false;
                for (int i = 0; i < overheads.Length; i++)
                {
                    var a = overheads[i];
                    if (!a.Shown) continue;
                    for (int j = i + 1; j < overheads.Length; j++)
                    {
                        var b = overheads[j];
                        if (!b.Shown) continue;
                        Vector2 pa = a.Root.anchoredPosition, pb = b.Root.anchoredPosition;
                        if (Mathf.Abs(pa.y - pb.y) >= rowH) continue;
                        float ax = pa.x + a.PushX, bx = pb.x + b.PushX;
                        float need = (a.NameW + b.NameW) / 2f + ts * Gap, d = bx - ax;
                        float over = need - Mathf.Abs(d);
                        if (over <= 0.5f) continue;
                        float dir = d >= 0f ? 1f : -1f; // b goes right of a (on a tie the later hero goes right)
                        a.PushX -= dir * over / 2f; b.PushX += dir * over / 2f;
                        moved = true;
                    }
                }
                if (!moved) break;
            }
            float k = Mathf.Min(1f, dt * 12f);
            for (int i = 0; i < overheads.Length; i++)
            {
                var o = overheads[i];
                if (!o.Shown) continue;
                o.NameX += (Mathf.Clamp(o.PushX, -maxShift, maxShift) - o.NameX) * k;
                var rt = o.Name.rectTransform;
                rt.anchoredPosition = new Vector2(o.NameX, rt.anchoredPosition.y);
            }
        }

        // The stack over a hero's bar, in tiles of the screen up from the bar's centre (Rickard 2026-11-03 "lite mycket överlapp"):
        // the name from 0.18 to NameTop, then the crossed-out eye or the poison bubbles (StatusFx puts them there), then the
        // rising numbers (HudTop.PlaceFloat), so nothing lies on anything else
        const float NameTop = 0.5f, EyeTop = 1.24f, BubblesTop = 1.02f;

        /// <summary>One tile in canvas units (the camera frames MatchCamera's TilesAcross over the standing frame).</summary>
        float OverheadTile() => cam.PixelsPerTile * (Screen.width > 1f ? canvasRect.rect.width / Screen.width : 1f);

        /// <summary>Canvas units up from a unit's bar centre that are free of its name, eye and poison bubbles.</summary>
        float OverheadClear(Unit u, float ts)
        {
            if (!u.IsHero) return ts * 0.12f; // a minion's bar has nothing over it
            float top = NameTop;
            if (u.Has(St.Poison)) top = Mathf.Max(top, BubblesTop);
            if (u.Team == m.Player.Team && (m.HiddenFromFoes(u) || u.Alive && m.Map.InGrass(u.X, u.Y))) top = Mathf.Max(top, EyeTop);
            return ts * (top + 0.04f);
        }

        static Image Plain(RectTransform parent, string name, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false; img.enabled = false;
            return img;
        }

        // The old drawHiddenEye: an almond outline, the pupil ring and a slash, thin pale strokes over a faint dark edge.
        static Sprite eyeSprite, seenEyeSprite;
        static Sprite EyeSprite => eyeSprite != null ? eyeSprite : (eyeSprite = MakeEye(true));
        /// <summary>The same eye without the slash (seen in grass), tinted SeenEye by its Image.</summary>
        static Sprite SeenEyeSprite => seenEyeSprite != null ? seenEyeSprite : (seenEyeSprite = MakeEye(false));
        static Sprite MakeEye(bool slash)
        {
            {
                const int n = 96;
                float c = n / 2f, r = n / 3.2f, w = r * 1.25f, h = r * 0.62f;
                var lines = new List<Vector2[]>();
                lines.Add(Quad(new Vector2(c - w, c), new Vector2(c, c + h * 2f), new Vector2(c + w, c)));
                lines.Add(Quad(new Vector2(c + w, c), new Vector2(c, c - h * 2f), new Vector2(c - w, c)));
                var ring = new Vector2[33];
                for (int i = 0; i < ring.Length; i++) { float a = i * Mathf.PI * 2f / 32f; ring[i] = new Vector2(c + Mathf.Cos(a) * h * 0.55f, c + Mathf.Sin(a) * h * 0.55f); }
                lines.Add(ring);
                if (slash) lines.Add(new[] { new Vector2(c - w * 0.8f, c - h * 1.3f), new Vector2(c + w * 0.8f, c + h * 1.3f) });
                else { var dot = new Vector2[17]; for (int i = 0; i < dot.Length; i++) { float a = i * Mathf.PI * 2f / 16f; dot[i] = new Vector2(c + Mathf.Cos(a) * h * 0.2f, c + Mathf.Sin(a) * h * 0.2f); } lines.Add(dot); }
                float dark = Mathf.Max(2.2f, r * 0.2f) * 0.5f, pale = Mathf.Max(1.1f, r * 0.09f) * 0.5f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Eye", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = float.MaxValue;
                        foreach (var l in lines)
                            for (int i = 1; i < l.Length; i++) d = Mathf.Min(d, SegDist(p, l[i - 1], l[i]));
                        float aPale = Mathf.Clamp01(pale + 0.5f - d) * 0.8f, aDark = Mathf.Clamp01(dark + 0.5f - d) * 0.35f;
                        float a = aPale + aDark * (1f - aPale);
                        Color col = a > 0f ? Color.Lerp(EyeEdge, EyeStroke, aPale / Mathf.Max(1e-4f, a)) : Color.clear;
                        col.a = a;
                        px[y * n + x] = col;
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            }
        }

        static Vector2[] Quad(Vector2 a, Vector2 ctl, Vector2 b)
        {
            var pts = new Vector2[17];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / 16f, u = 1f - t;
                pts[i] = u * u * a + 2f * u * t * ctl + t * t * b;
            }
            return pts;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }
    }
}
