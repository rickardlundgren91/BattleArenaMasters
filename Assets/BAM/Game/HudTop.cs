using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The old game's top HUD as Rickard plays it now, the Glass HUD (ABC2 HudGlass.cs drawGlassTop, on by default; Rickard
    /// 2026-10-26 13:30): one thin see-through glass strip with a gold edge; three small round portraits at each end (gold
    /// setting, brighter and thicker on the player, grey and dark with the respawn seconds while dead, a small health bar under
    /// each); the two tower bars with their percent, emptying towards the edge with a light trail; a centre pill with
    /// kills · clock · kills, the level under the clock and its progress along the bottom; the star advantage as pips under each
    /// bar. Also the floating damage numbers (drawFloatText: white for the player's damage, red for damage to the player, gold
    /// for its big hits, small and dim on minions). No kill feed: Rickard took the kill texts out on 2026-10-20.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color TopOwn = Hex(0x2fe3d0), TopFoe = Hex(0xff9a2e);
        static readonly Color GlassFill = new Color(8f / 255f, 12f / 255f, 32f / 255f, 0.64f), PillFill = new Color(6f / 255f, 10f / 255f, 28f / 255f, 0.62f);
        static readonly Color GlassEdge = new Color(1f, 1f, 1f, 0.16f), GlassGold = new Color(232f / 255f, 180f / 255f, 80f / 255f, 0.75f);
        static readonly Color GlassGoldFaint = new Color(232f / 255f, 180f / 255f, 80f / 255f, 0.22f), LevelCream = Hex(0xf6e2b8);
        static readonly Color PortraitBack = Hex(0x2b3a6a), PlayerEdge = Hex(0xffd75a), MateEdge = Hex(0xc9953e), DeadEdge = new Color(0.59f, 0.63f, 0.75f, 0.6f);
        static readonly Color OwnTrail = Hex(0xb8fff6), FoeTrail = Hex(0xffe0a8), DmgToPlayer = Hex(0xff6b6b);
        static readonly Color BigHit = Hex(0xffc857), CreepHit = new Color(1f, 1f, 1f, 0.7f);
        static readonly Color StarOn = Hex(0xf4f6ff), StarOff = new Color(10f / 255f, 16f / 255f, 40f / 255f, 0.5f), StarGold = Hex(0xffc531);
        static readonly Color TextShadow = new Color(5f / 255f, 12f / 255f, 40f / 255f, 0.55f);
        static string[] intStrings;

        RectTransform topRoot, strip, pill;
        Image stripFill, stripGold, stripGoldInner, pillFill, pillGold;
        Text timeText, levelText, ownKills, foeKills;
        Image levelBack, levelFill;
        readonly Image[] stars = new Image[6], starGlyphs = new Image[6];
        readonly Portrait[] portraits = new Portrait[6];
        readonly TowerBar[] bars = new TowerBar[2];
        int shownSeconds, shownLevel, shownK0, shownK1, shownS0, shownS1;
        bool shownClock;
        Vector2 laidOut;

        sealed class Portrait
        {
            public RectTransform Root, HpBack;
            public Image Edge, Edge2, Dark, Hp;
            public RawImage Pic;
            public Text Secs;
            public Unit Unit;
            public int Shown = -2;
            public bool WasAlive = true;
        }

        sealed class TowerBar
        {
            public RectTransform Root;
            public Image Glow, Back, Fill, Trail, Edge;
            public Text Percent;
            public float Trailing = 1f, Since;
            public int Shown = -1;
        }

        // ---------------------------------------------------------------- floating damage numbers

        const int FloatCount = 24;
        const float FloatLife = 0.9f, FloatMerge = 0.2f;
        sealed class FloatText
        {
            public Text Label;
            public Unit Unit;
            public Vector3 At;
            public float Born = -99f, Value;
            public bool ToPlayer, Big;
            public float BaseA = 1f;
        }
        readonly FloatText[] floats = new FloatText[FloatCount];
        int nextFloat;

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        static string Int(int n)
        {
            if (intStrings == null) intStrings = new string[2000];
            if (n < 0 || n >= intStrings.Length) return n.ToString();
            return intStrings[n] ?? (intStrings[n] = n.ToString());
        }

        // ---------------------------------------------------------------- start of a match

        void BeginTop()
        {
            topRoot.gameObject.SetActive(!tutorial);
            shownSeconds = shownLevel = shownK0 = shownK1 = shownS0 = shownS1 = -1;
            shownClock = false; laidOut = Vector2.zero;
            int[] count = new int[2];
            for (int i = 0; i < portraits.Length; i++) { portraits[i].Unit = null; portraits[i].Root.gameObject.SetActive(false); }
            for (int i = 0; i < m.Heroes.Count; i++)
            {
                var h = m.Heroes[i];
                int team = h.Team == m.Player.Team ? 0 : 1;
                if (count[team] >= 3) continue;
                var p = portraits[team * 3 + count[team]++];
                p.Unit = h; p.Shown = -2; p.WasAlive = !h.Alive; // forces the first look
                p.Pic.texture = Resources.Load<Texture2D>("BAM/Menu/portrait_" + HeroCatalog.Get(h.Hero).Name.ToLowerInvariant());
                p.Root.gameObject.SetActive(true);
            }
            for (int i = 0; i < 2; i++) { bars[i].Trailing = 1f; bars[i].Since = 0f; bars[i].Shown = -1; }
            for (int i = 0; i < floats.Length; i++) { floats[i].Born = -99f; floats[i].Unit = null; floats[i].Label.gameObject.SetActive(false); }
        }

        // ---------------------------------------------------------------- after every tick: damage numbers and the kill feed

        void ReadEvents()
        {
            var ev = m.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.Type == EventType.Damage) OnDamage(e);
                else if (e.Type == EventType.Death && e.B != null && e.B.IsRealHero) OnHeroDeath(e);
            }
        }

        void OnDamage(SimEvent e)
        {
            var b = e.B;
            if (b == null || e.V < 0.5f) return;
            bool toPlayer = b == m.Player, byPlayer = e.A != null && (e.A == m.Player || e.A.Owner == m.Player);
            // the player's own hits on minions get small, dim numbers (the old game, all modes since 2026-10-25)
            bool creep = byPlayer && b.IsMinionLike && b.Team != m.Player.Team;
            if (!b.IsRealHero && !creep) return;
            if (!toPlayer && !byPlayer) return;
            if (!toPlayer && !m.IsVisibleTo(b, m.Player.Team)) return;
            float now = Time.unscaledTime;
            // a burn or a quick volley on the same hero adds to the number that is already rising
            for (int i = 0; i < floats.Length; i++)
            {
                var f = floats[i];
                if (f.Unit == b && f.ToPlayer == toPlayer && now - f.Born < FloatMerge)
                {
                    f.Value += e.V;
                    f.Label.text = Int(Mathf.RoundToInt(f.Value));
                    return;
                }
            }
            var t = floats[nextFloat];
            nextFloat = (nextFloat + 1) % floats.Length;
            t.Unit = b; t.Born = now; t.Value = e.V; t.ToPlayer = toPlayer;
            t.Big = !creep && e.V >= 0.15f * b.MaxHp; // HeroJuice: 15 % of its health or more
            t.At = views.DrawnAt(b);
            t.Label.text = Int(Mathf.RoundToInt(e.V));
            t.Label.color = toPlayer ? DmgToPlayer : creep ? CreepHit : t.Big ? BigHit : Color.white;
            t.Label.fontSize = creep ? 34 : t.Big ? 66 : 50;
            t.BaseA = creep ? 0.7f : 1f;
            t.Label.gameObject.SetActive(true);
        }

        void OnHeroDeath(SimEvent e)
        {
            var victim = e.B;
            if (victim == m.Player) killedBy = e.A;
        }
        string Colored(Unit u)
        {
            var hu = u.Owner != null && u.IsClone ? u.Owner : u;
            string name = hu.IsHero ? HeroCatalog.Get(hu.Hero).Name : "Tower";
            string col = hu.Team == m.Player.Team ? "#2fe3d0" : "#ff9a2e";
            return "<color=" + col + ">" + name + "</color>";
        }

        // ---------------------------------------------------------------- every frame

        void Top(float dt)
        {
            Floats();
            if (tutorial) return;
            Vector2 size = safe.rect.size;
            if (size != laidOut && size.x > 1f) { laidOut = size; LayoutGlass(size.x, size.y); }
            // the clock only while playing; in the countdown the level sits alone in the pill
            bool clock = m.State == MatchState.Play;
            if (clock != shownClock) { shownClock = clock; shownLevel = -1; timeText.gameObject.SetActive(clock); LayoutPillText(); }
            int secs = Mathf.Max(0, Mathf.FloorToInt(m.Time));
            if (clock && secs != shownSeconds) { shownSeconds = secs; timeText.text = (secs / 60) + ":" + (secs % 60).ToString("00"); }
            if (m.Level != shownLevel) { shownLevel = m.Level; levelText.text = m.Level >= Rules.MaxLevel ? "MAX" : "LVL " + m.Level; }
            levelFill.fillAmount = m.Level >= Rules.MaxLevel ? 1f : Mathf.Clamp01(Mathf.Max(0f, m.Time) % Rules.SecondsPerLevel / Rules.SecondsPerLevel);
            int own = m.Player.Team, foe = Teams.Other(own);
            if (m.Kills[own] != shownK0) { shownK0 = m.Kills[own]; ownKills.text = Int(shownK0); }
            if (m.Kills[foe] != shownK1) { shownK1 = m.Kills[foe]; foeKills.text = Int(shownK1); }
            if (m.Stars[own] != shownS0 || m.Stars[foe] != shownS1)
            {
                shownS0 = m.Stars[own]; shownS1 = m.Stars[foe];
                for (int i = 0; i < 3; i++)
                {
                    bool a = i < shownS0, b = i < shownS1;
                    stars[i].color = a ? StarOn : StarOff; starGlyphs[i].enabled = a;
                    stars[3 + i].color = b ? StarOn : StarOff; starGlyphs[3 + i].enabled = b;
                }
            }
            Bar(bars[0], m.Towers[own], dt);
            Bar(bars[1], m.Towers[foe], dt);
            for (int i = 0; i < portraits.Length; i++) PortraitFrame(portraits[i], i < 3 ? TopOwn : TopFoe);
        }

        // the old CombatFeel.Trail: up at once, down after 0.3 s at 0.9 of the bar per second
        static void Bar(TowerBar b, Unit tower, float dt)
        {
            float k = tower != null && tower.Alive ? Mathf.Clamp01(tower.Hp / tower.MaxHp) : 0f;
            if (k >= b.Trailing) { b.Trailing = k; b.Since = 0f; }
            else { b.Since += dt; if (b.Since > 0.3f) b.Trailing = Mathf.Max(k, b.Trailing - 0.9f * dt); }
            b.Fill.fillAmount = k;
            b.Trail.fillAmount = b.Trailing;
            int pct = Mathf.CeilToInt(k * 100f);
            if (pct != b.Shown) { b.Shown = pct; b.Percent.text = Int(pct) + "%"; }
        }

        void PortraitFrame(Portrait p, Color team)
        {
            var u = p.Unit;
            if (u == null) return;
            if (u.Alive != p.WasAlive)
            {
                p.WasAlive = u.Alive;
                p.Dark.enabled = !u.Alive;
                p.Secs.gameObject.SetActive(!u.Alive);
                p.HpBack.gameObject.SetActive(u.Alive);
                Color edge = !u.Alive ? DeadEdge : u == m.Player ? PlayerEdge : MateEdge;
                p.Edge.color = edge;
                p.Edge2.color = edge;
                p.Edge2.enabled = u == m.Player && u.Alive; // the player's setting is thicker
                p.Shown = -2;
            }
            if (u.Alive) p.Hp.fillAmount = Mathf.Clamp01(u.Hp / Mathf.Max(1f, u.MaxHp));
            else
            {
                int s = Mathf.Clamp(Mathf.CeilToInt(u.RespawnT), 0, 99);
                if (s != p.Shown) { p.Shown = s; p.Secs.text = Int(s); }
            }
        }

        void Floats()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < floats.Length; i++)
            {
                var f = floats[i];
                if (!f.Label.gameObject.activeSelf) continue;
                float age = now - f.Born;
                if (age >= FloatLife || m.State == MatchState.Over && age > 0.2f) { f.Label.gameObject.SetActive(false); f.Unit = null; continue; }
                if (f.Unit != null && f.Unit.Alive) f.At = views.DrawnAt(f.Unit);
                Vector3 sp = cam.Camera.WorldToScreenPoint(f.At + Vector3.up * 1.6f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local);
                float t = age / FloatLife;
                // a small pop, then it rises and fades over its last third
                float pop = age < 0.12f ? 1f + (f.Big ? 0.45f : 0.25f) * (1f - age / 0.12f) : 1f;
                f.Label.rectTransform.anchoredPosition = local + new Vector2(f.ToPlayer ? -26f : 26f, 30f + 90f * t);
                f.Label.rectTransform.localScale = Vector3.one * pop;
                var c = f.Label.color;
                c.a = f.BaseA * (t < 0.66f ? 1f : 1f - (t - 0.66f) / 0.34f);
                f.Label.color = c;
            }
        }

        // ---------------------------------------------------------------- building

        void BuildTop()
        {
            topRoot = UiKit.NewRect("Top", safe);
            UiKit.Stretch(topRoot);
            strip = TopRect("Strip");
            stripFill = UiKit.Fill(strip, GlassFill, UiKit.Rounded);
            Edge(UiKit.Fill(strip, GlassEdge, UiKit.Ring));
            stripGold = UiKit.Fill(strip, GlassGold, UiKit.Ring);
            stripGoldInner = UiKit.Fill(strip, GlassGoldFaint, UiKit.Ring);

            for (int i = 0; i < 2; i++) bars[i] = MakeBar(i == 1);
            for (int i = 0; i < portraits.Length; i++) portraits[i] = MakePortrait(i >= 3);

            pill = TopRect("Pill");
            pillFill = UiKit.Fill(pill, PillFill, UiKit.Rounded);
            Edge(UiKit.Fill(pill, GlassEdge, UiKit.Ring));
            pillGold = UiKit.Fill(pill, GlassGold, UiKit.Ring);
            ownKills = TopLabel(pill, TopOwn, TextAnchor.MiddleCenter);
            foeKills = TopLabel(pill, TopFoe, TextAnchor.MiddleCenter);
            timeText = TopLabel(pill, Color.white, TextAnchor.MiddleCenter);
            levelText = TopLabel(pill, LevelCream, TextAnchor.MiddleCenter);
            var lv = UiKit.NewRect("LevelBar", pill);
            levelBack = lv.gameObject.AddComponent<Image>();
            levelBack.color = new Color(1f, 1f, 1f, 0.14f);
            levelBack.raycastTarget = false;
            levelFill = UiKit.Fill(lv, LevelCream);
            levelFill.sprite = WhiteSprite; // Filled needs a sprite
            levelFill.type = Image.Type.Filled;
            levelFill.fillMethod = Image.FillMethod.Horizontal;
            levelFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            for (int i = 0; i < 6; i++)
            {
                var s = Circle(topRoot, "Star", 10f, StarOff);
                stars[i] = s.GetComponent<Image>();
                var g = UiKit.Fill(s, StarGold);
                g.sprite = StarSprite;
                UiKit.Stretch(g.rectTransform, -2, -2, -2, -2);
                g.enabled = false;
                starGlyphs[i] = g;
            }
        }

        RectTransform TopRect(string name)
        {
            var rt = UiKit.NewRect(name, topRoot);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            return rt;
        }

        static Text TopLabel(Transform parent, Color color, TextAnchor anchor)
        {
            var t = UiKit.Label(parent, "", 30, anchor, color);
            t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.raycastTarget = false;
            var sh = t.gameObject.AddComponent<Shadow>(); // hudText: a dark copy a little below
            sh.effectColor = TextShadow;
            sh.effectDistance = new Vector2(0f, -2f);
            return t;
        }

        static void Edge(Image img) { img.raycastTarget = false; }

        // Sizes as the old HudGlass.cs, in fractions of the frame's width W and height H, top-left origin.
        float gM, gS, gPad, gH, gStarD, gW;
        Rect gStrip, gPill;

        void LayoutGlass(float W, float H)
        {
            gW = W;
            gM = Mathf.Max(4f, H * 0.01f); gS = W * 0.064f; gPad = W * 0.012f; gH = gS + gPad * 2f;
            gStrip = new Rect(W * 0.02f, gM, W * 0.96f, gH);
            float pw = W * 0.22f;
            gPill = new Rect(W / 2f - pw / 2f, gM, pw, gH);
            gStarD = Mathf.Max(7f, W * 0.02f);

            Pill(strip, stripFill, gStrip);
            RoundRing(stripGold, gH * 0.5f, 1.5f);
            RoundRing(stripGoldInner, gH * 0.5f - 3f, 1f);
            UiKit.Stretch(stripGoldInner.rectTransform, 3, 3, 3, 3);
            Pill(pill, pillFill, gPill);
            RoundRing(pillGold, gH * 0.5f, 1.5f);
            LayoutPillText();

            for (int i = 0; i < portraits.Length; i++)
            {
                int dir = i < 3 ? -1 : 1, k = i % 3;
                var p = portraits[i];
                float d = gS, x = PortraitX(dir, k), cy = gStrip.y + gH / 2f;
                p.Root.anchoredPosition = new Vector2(x, -cy);
                p.Root.sizeDelta = new Vector2(d, d);
                float hw = d * 0.8f, hh = Mathf.Max(2.5f, d * 0.08f);
                p.HpBack.sizeDelta = new Vector2(hw, hh);
                p.HpBack.anchoredPosition = new Vector2(0f, -(d / 2f + 1.5f));
                p.Secs.fontSize = Mathf.RoundToInt(d * 0.56f);
            }

            for (int i = 0; i < 2; i++)
            {
                int dir = i == 0 ? -1 : 1;
                var b = bars[i];
                float bh = Mathf.Max(10f, gH * 0.38f), x0 = PortraitX(-1, 2) + gS / 2f + W * 0.014f, x1 = gPill.x - W * 0.012f;
                float y = gStrip.y + (gStrip.height - bh) / 2f;
                var r = dir < 0 ? new Rect(x0, y, x1 - x0, bh) : new Rect(W - x1, y, x1 - x0, bh);
                b.Root.anchoredPosition = new Vector2(r.x, -r.y);
                b.Root.sizeDelta = r.size;
                Corners(b.Back, bh / 2f);
                Corners(b.Glow, bh / 2f + 3f);
                RoundRing(b.Edge, bh / 2f, 1f);
                b.Percent.fontSize = Mathf.RoundToInt(Mathf.Max(10f, bh * 0.86f));
                // the star pips just under the bar, from the portraits' side
                float sd = gStarD, gp = sd * 0.35f, sy = gM + gH + sd * 0.75f, sx = dir < 0 ? r.x : r.x + r.width;
                for (int s = 0; s < 3; s++)
                {
                    var st = stars[i * 3 + s].rectTransform;
                    st.sizeDelta = new Vector2(sd, sd);
                    st.anchorMin = st.anchorMax = new Vector2(0f, 1f);
                    st.anchoredPosition = new Vector2(sx - dir * (sd / 2f + s * (sd + gp)), -sy);
                }
            }
        }

        void LayoutPillText()
        {
            if (gH <= 0f) return;
            float sh = gH, pw = gPill.width;
            Place(ownKills, -pw / 2f + sh * 0.6f, 0f, sh * 0.42f, sh);
            Place(foeKills, pw / 2f - sh * 0.6f, 0f, sh * 0.42f, sh);
            if (shownClock)
            {
                Place(timeText, 0f, sh * 0.1f, sh * 0.4f, pw);
                Place(levelText, 0f, -sh * 0.27f, sh * 0.22f, pw);
            }
            else Place(levelText, 0f, 0f, sh * 0.3f, pw);
            var lv = levelBack.rectTransform;
            lv.anchorMin = lv.anchorMax = lv.pivot = new Vector2(0.5f, 0f);
            lv.sizeDelta = new Vector2(pw * 0.42f, 2f);
            lv.anchoredPosition = new Vector2(0f, 1.5f);
        }

        static void Place(Text t, float x, float y, float font, float w)
        {
            t.fontSize = Mathf.Max(8, Mathf.RoundToInt(font));
            t.rectTransform.sizeDelta = new Vector2(w, font * 1.5f);
            t.rectTransform.anchoredPosition = new Vector2(x, y);
        }

        float PortraitX(int dir, int i)
        {
            float d = gS, gap = gW * 0.008f;
            return dir < 0 ? gStrip.x + gPad + d / 2f + i * (d + gap) : gStrip.x + gStrip.width - gPad - d / 2f - i * (d + gap);
        }

        static void Pill(RectTransform rt, Image fill, Rect r)
        {
            rt.anchoredPosition = new Vector2(r.x, -r.y);
            rt.sizeDelta = r.size;
            Corners(fill, r.height / 2f);
            for (int i = 0; i < rt.childCount; i++)
            {
                var img = rt.GetChild(i).GetComponent<Image>();
                if (img != null && img != fill && img.color == GlassEdge) RoundRing(img, r.height / 2f, 1.5f);
            }
        }

        // the Rounded sprite has 28 px corners: scaled so they come out at this radius
        static void Corners(Image img, float radius) { img.pixelsPerUnitMultiplier = 28f / Mathf.Max(1f, radius); }

        // A thin rounded outline (the old 1-1.5 px strokes): a 9-sliced ring whose line keeps this width at this corner radius.
        static readonly System.Collections.Generic.Dictionary<int, Sprite> thinRings = new System.Collections.Generic.Dictionary<int, Sprite>();
        const int RingCorner = 64;
        static void RoundRing(Image img, float radius, float width)
        {
            radius = Mathf.Max(1f, radius);
            int line = Mathf.Clamp(Mathf.RoundToInt(RingCorner * width / radius), 1, RingCorner);
            if (!thinRings.TryGetValue(line, out var sp)) thinRings[line] = sp = MakeRing(line);
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = RingCorner / radius;
        }

        static Sprite MakeRing(int line)
        {
            int n = RingCorner * 2 + 2;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Ring" + line, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            float r = RingCorner, half = n / 2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // distance inside the rounded square's edge
                    float qx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - (half - r), 0f), qy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - (half - r), 0f);
                    float d = r - Mathf.Sqrt(qx * qx + qy * qy); // 0 at the edge, grows inwards
                    float a = Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(line - d + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(RingCorner, RingCorner, RingCorner, RingCorner));
        }

        Portrait MakePortrait(bool foe)
        {
            var p = new Portrait();
            p.Root = UiKit.NewRect("Portrait", topRoot);
            p.Root.anchorMin = p.Root.anchorMax = new Vector2(0f, 1f);
            p.Root.pivot = new Vector2(0.5f, 0.5f);
            var mask = UiKit.Fill(p.Root, PortraitBack, UiKit.Circle);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pic = UiKit.NewRect("Pic", mask.transform);
            UiKit.Stretch(pic);
            p.Pic = pic.gameObject.AddComponent<RawImage>();
            p.Pic.raycastTarget = false;
            p.Pic.uvRect = new Rect(0.115f, 0f, 0.77f, 1f); // the 400x308 renders: full height, the sides cut, as the old drawImage
            p.Dark = UiKit.Fill(mask.transform, new Color(4f / 255f, 6f / 255f, 20f / 255f, 0.7f));
            p.Dark.enabled = false;
            p.Edge = UiKit.Fill(p.Root, MateEdge, UiKit.CircleRing);
            p.Edge2 = UiKit.Fill(p.Root, MateEdge, UiKit.CircleRing);
            UiKit.Stretch(p.Edge2.rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
            p.Edge2.enabled = false;
            p.HpBack = UiKit.NewRect("Hp", p.Root);
            p.HpBack.anchorMin = p.HpBack.anchorMax = new Vector2(0.5f, 0.5f);
            p.HpBack.pivot = new Vector2(0.5f, 1f);
            var hb = p.HpBack.gameObject.AddComponent<Image>();
            hb.color = new Color(0f, 0f, 0f, 0.6f);
            hb.raycastTarget = false;
            p.Hp = UiKit.Fill(p.HpBack, foe ? TopFoe : TopOwn);
            p.Hp.sprite = WhiteSprite;
            p.Hp.type = Image.Type.Filled;
            p.Hp.fillMethod = Image.FillMethod.Horizontal;
            p.Hp.fillOrigin = (int)Image.OriginHorizontal.Left;
            p.Secs = TopLabel(p.Root, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(p.Secs.rectTransform, -10, -10, -10, -10);
            p.Secs.gameObject.SetActive(false);
            p.Root.gameObject.SetActive(false);
            return p;
        }

        TowerBar MakeBar(bool foe)
        {
            var b = new TowerBar();
            b.Root = TopRect(foe ? "EnemyTower" : "OwnTower");
            Color team = foe ? TopFoe : TopOwn;
            b.Glow = UiKit.Fill(b.Root, new Color(team.r, team.g, team.b, 0.28f), UiKit.Rounded);
            UiKit.Stretch(b.Glow.rectTransform, -3, -3, -3, -3);
            b.Back = UiKit.Fill(b.Root, new Color(0f, 0f, 0f, 0.58f), UiKit.Rounded);
            b.Back.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            b.Trail = UiKit.Fill(b.Back.transform, foe ? FoeTrail : OwnTrail);
            b.Fill = UiKit.Fill(b.Back.transform, team);
            foreach (var img in new[] { b.Trail, b.Fill })
            {
                img.sprite = WhiteSprite; // Filled needs a sprite
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)(foe ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            }
            var shine = UiKit.Fill(b.Back.transform, new Color(1f, 1f, 1f, 0.18f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0.6f);
            b.Edge = UiKit.Fill(b.Root, GlassGold, UiKit.Ring);
            b.Percent = TopLabel(b.Root, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(b.Percent.rectTransform, 0, -10, 0, -10);
            return b;
        }

        // the old drawStar: a five-pointed gold star with a dark edge, drawn once into a small texture
        static Sprite starSprite;
        static Sprite StarSprite
        {
            get
            {
                if (starSprite != null) return starSprite;
                const int n = 64;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Star", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = Mathf.PI / 2f + i * Mathf.PI / 5f, rr = i % 2 == 1 ? 0.45f : 1f;
                    pts[i] = new Vector2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr) * 0.42f * n + new Vector2(n / 2f, n / 2f);
                }
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // 4x4 samples: inside the star (white, tinted gold by the image) or within the dark edge
                        int inside = 0, edge = 0;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                            {
                                var q = new Vector2(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f);
                                if (Inside(pts, q)) inside++;
                                else if (EdgeDist(pts, q) < n * 0.05f) edge++;
                            }
                        float a = (inside + edge) / 16f, w = inside + edge > 0 ? inside / (float)(inside + edge) : 0f;
                        byte c = (byte)Mathf.RoundToInt(Mathf.Lerp(0.12f, 1f, w) * 255f);
                        px[y * n + x] = new Color32(c, c, c, (byte)Mathf.RoundToInt(a * 255f));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                starSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return starSprite;
            }
        }

        static bool Inside(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x) c = !c;
            return c;
        }

        static float EdgeDist(Vector2[] poly, Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[j], b = poly[i], ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        void BuildFloats()
        {
            for (int i = 0; i < floats.Length; i++)
            {
                var t = UiKit.Label(canvasRect, "", 50, TextAnchor.MiddleCenter, Color.white);
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                t.rectTransform.sizeDelta = new Vector2(220f, 90f);
                t.raycastTarget = false;
                UiKit.AddOutline(t, 3);
                t.gameObject.SetActive(false);
                floats[i] = new FloatText { Label = t };
            }
        }

        static Sprite whiteSprite;
        static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null) whiteSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
                return whiteSprite;
            }
        }

        static RectTransform Rect(Transform parent, string name, float w, float h, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }
    }
}
