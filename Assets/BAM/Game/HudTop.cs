using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The old game's top HUD (ABC2 HudTop.cs drawTopHud, Hud.cs drawTeamPortraits): three portraits per team from the screen
    /// edges (gold edge on the player, dark with the respawn seconds while dead), the towers' health bars with their percent
    /// (emptying towards the edge, a light trail behind), skull kill counters, the centre box with level, clock and level
    /// progress, the team stars under the bars. Also the floating damage numbers (drawFloatText: white for the player's damage,
    /// red for damage to the player, gold for the player's big hits, on heroes only). No kill feed: Rickard took the kill texts
    /// out of the old game on 2026-10-20.
    /// </summary>
    sealed partial class Hud
    {
        const float TopM = 12f, PortraitS = 84f, PortraitGap = 6f, EdgeM = 18f, BarH = 30f, BoxW = 190f;
        static readonly Color TopOwn = Hex(0x2fe3d0), TopFoe = Hex(0xff9a2e), TopPanel = Hex(0x0b1a3a), TopEdge = Hex(0x1b2d5e);
        static readonly Color PortraitBack = Hex(0x2b3a6a), PlayerEdge = Hex(0xffd75a), DeadEdge = new Color(0.59f, 0.63f, 0.75f, 0.6f);
        static readonly Color OwnTrail = Hex(0xb8fff6), FoeTrail = Hex(0xffe0a8), DmgToPlayer = Hex(0xff6b6b);
        static readonly Color BigHit = Hex(0xffc857), CreepHit = new Color(1f, 1f, 1f, 0.7f);
        static readonly Color StarOff = new Color(1f, 1f, 1f, 0.18f);
        static string[] intStrings;

        RectTransform topRoot;
        Text timeText, levelText, ownKills, foeKills;
        Image levelFill;
        readonly Image[] stars = new Image[6];
        readonly Portrait[] portraits = new Portrait[6];
        readonly TowerBar[] bars = new TowerBar[2];
        int shownSeconds, shownLevel, shownK0, shownK1, shownS0, shownS1;

        sealed class Portrait
        {
            public RectTransform Root;
            public Image Edge, Dark;
            public RawImage Pic;
            public Text Secs;
            public Unit Unit;
            public int Shown = -2;
            public bool WasAlive = true;
        }

        sealed class TowerBar
        {
            public Image Fill, Trail;
            public Text Percent;
            public float Trailing = 1f;
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
            for (int i = 0; i < 2; i++) { bars[i].Trailing = 1f; bars[i].Shown = -1; }
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
            int secs = Mathf.Max(0, Mathf.FloorToInt(m.Time));
            if (secs != shownSeconds) { shownSeconds = secs; timeText.text = (secs / 60) + ":" + (secs % 60).ToString("00"); }
            if (m.Level != shownLevel) { shownLevel = m.Level; levelText.text = "LVL " + m.Level; }
            levelFill.fillAmount = m.Level >= Rules.MaxLevel ? 1f : Mathf.Clamp01(Mathf.Max(0f, m.Time) % Rules.SecondsPerLevel / Rules.SecondsPerLevel);
            int own = m.Player.Team, foe = Teams.Other(own);
            if (m.Kills[own] != shownK0) { shownK0 = m.Kills[own]; ownKills.text = Int(shownK0); }
            if (m.Kills[foe] != shownK1) { shownK1 = m.Kills[foe]; foeKills.text = Int(shownK1); }
            if (m.Stars[own] != shownS0 || m.Stars[foe] != shownS1)
            {
                shownS0 = m.Stars[own]; shownS1 = m.Stars[foe];
                for (int i = 0; i < 3; i++)
                {
                    stars[i].color = i < shownS0 ? UiKit.Gold : StarOff;
                    stars[3 + i].color = i < shownS1 ? UiKit.Gold : StarOff;
                }
            }
            Bar(bars[0], m.Towers[own], dt);
            Bar(bars[1], m.Towers[foe], dt);
            for (int i = 0; i < portraits.Length; i++) PortraitFrame(portraits[i], i < 3 ? TopOwn : TopFoe);
        }

        static void Bar(TowerBar b, Unit tower, float dt)
        {
            float k = tower != null ? Mathf.Clamp01(tower.Hp / tower.MaxHp) : 0f;
            if (tower != null && !tower.Alive) k = 0f;
            b.Trailing = Mathf.Max(k, b.Trailing - 0.5f * dt);
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
                p.Edge.color = u == m.Player ? PlayerEdge : u.Alive ? team : DeadEdge;
                p.Shown = -2;
            }
            if (!u.Alive)
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

            // the centre box: level, clock, the bar to the next level
            var box = UiKit.Panel(topRoot, TopPanel);
            box.raycastTarget = false;
            box.pixelsPerUnitMultiplier = 2f;
            UiKit.Place(box, 0.5f, 1f, 0f, -TopM, BoxW, PortraitS + 6f + BarH);
            EdgeLine(box, TopEdge);
            levelText = UiKit.Label(box.transform, "LVL 1", 30, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Place(levelText, 0.5f, 1f, 0f, -4f, BoxW, 36f);
            UiKit.AddOutline(levelText, 2);
            timeText = UiKit.Label(box.transform, "0:00", 50, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(timeText, 0.5f, 0.5f, 0f, -4f, BoxW, 56f);
            UiKit.AddOutline(timeText, 3);
            var lvBack = UiKit.NewRect("LevelBar", box.transform);
            UiKit.Place(lvBack, 0.5f, 0f, 0f, 12f, BoxW - 36f, 10f);
            var lvBackImg = lvBack.gameObject.AddComponent<Image>();
            lvBackImg.color = new Color(1f, 1f, 1f, 0.15f);
            lvBackImg.raycastTarget = false;
            levelFill = UiKit.Fill(lvBack, UiKit.Gold);
            levelFill.sprite = WhiteSprite; // Filled needs a sprite
            levelFill.type = Image.Type.Filled;
            levelFill.fillMethod = Image.FillMethod.Horizontal;
            levelFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            // portraits from both edges, the player's team on the left
            for (int i = 0; i < portraits.Length; i++)
            {
                bool foe = i >= 3;
                int k = foe ? i - 3 : i;
                float x = EdgeM + k * (PortraitS + PortraitGap);
                portraits[i] = MakePortrait(topRoot, foe ? 1f : 0f, foe ? -x : x, foe);
            }

            // kill counters between the portraits and the centre box
            ownKills = KillCounter(-1, TopOwn);
            foeKills = KillCounter(1, TopFoe);

            // tower bars under the portraits; the fill sits against the centre and empties towards the edge
            float barY = -(TopM + PortraitS + 6f);
            bars[0] = MakeBar(false, barY);
            bars[1] = MakeBar(true, barY);

            // the team stars under the bars, by the centre box
            for (int i = 0; i < 6; i++)
            {
                bool foe = i >= 3;
                int k = foe ? i - 3 : i;
                float x = BoxW * 0.5f + 22f + k * 32f;
                var s = Circle(topRoot, "Star", 26f, StarOff);
                UiKit.Place(s, 0.5f, 1f, foe ? x : -x, barY - BarH - 6f, 26f, 26f);
                stars[i] = s.GetComponent<Image>();
            }
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

        Portrait MakePortrait(RectTransform parent, float side, float x, bool foe)
        {
            var p = new Portrait();
            p.Root = UiKit.NewRect("Portrait", parent);
            UiKit.Place(p.Root, side, 1f, x, -TopM, PortraitS, PortraitS);
            p.Root.pivot = new Vector2(side, 1f);
            p.Root.anchoredPosition = new Vector2(x, -TopM);
            p.Edge = UiKit.Fill(p.Root, foe ? TopFoe : TopOwn, UiKit.Rounded);
            p.Edge.pixelsPerUnitMultiplier = 2.4f;
            var inner = UiKit.Fill(p.Edge.transform, PortraitBack, UiKit.Rounded);
            inner.pixelsPerUnitMultiplier = 2.8f;
            UiKit.Stretch(inner.rectTransform, 4, 4, 4, 4);
            inner.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pic = UiKit.NewRect("Pic", inner.transform);
            UiKit.Stretch(pic);
            p.Pic = pic.gameObject.AddComponent<RawImage>();
            p.Pic.raycastTarget = false;
            p.Pic.uvRect = new Rect(0.115f, 0.1f, 0.77f, 0.9f); // as the menus: a square around the face of the 400x308 renders
            p.Dark = UiKit.Fill(inner.transform, new Color(4f / 255f, 6f / 255f, 20f / 255f, 0.68f));
            p.Dark.enabled = false;
            p.Secs = UiKit.Label(p.Root, "", 52, TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(p.Secs.rectTransform);
            UiKit.AddOutline(p.Secs, 3);
            p.Secs.gameObject.SetActive(false);
            return p;
        }

        Text KillCounter(int dir, Color color)
        {
            var rt = UiKit.NewRect("Kills", topRoot);
            float w = 110f;
            UiKit.Place(rt, 0.5f, 1f, dir * (BoxW * 0.5f + 10f + w * 0.5f), -(TopM + 14f), w, 56f);
            MakeSkull(rt, 40f, color, new Vector2(-28f, 0f));
            var t = UiKit.Label(rt, "0", 50, TextAnchor.MiddleLeft, Color.white);
            UiKit.Place(t, 0.5f, 0.5f, 26f, 0f, 70f, 56f);
            UiKit.AddOutline(t, 3);
            return t;
        }

        // The old drawSkull: a round head, a jaw, two dark eyes and the gaps between the teeth.
        static void MakeSkull(RectTransform parent, float r2, Color color, Vector2 at)
        {
            float r = r2 * 0.5f;
            var root = UiKit.NewRect("Skull", parent);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(r2, r2);
            root.anchoredPosition = at;
            var head = Circle(root, "Head", r * 1.64f, color);
            head.anchoredPosition = new Vector2(0f, r * 0.12f);
            var jaw = Rect(root, "Jaw", r * 0.96f, r * 0.62f, color);
            jaw.anchoredPosition = new Vector2(0f, -r * 0.61f);
            Circle(root, "Eye", r * 0.48f, TopPanel).anchoredPosition = new Vector2(-r * 0.33f, r * 0.1f);
            Circle(root, "Eye", r * 0.48f, TopPanel).anchoredPosition = new Vector2(r * 0.33f, r * 0.1f);
            Rect(root, "Gap", r * 0.08f, r * 0.34f, TopPanel).anchoredPosition = new Vector2(-r * 0.12f, -r * 0.75f);
            Rect(root, "Gap", r * 0.08f, r * 0.34f, TopPanel).anchoredPosition = new Vector2(r * 0.12f, -r * 0.75f);
        }

        TowerBar MakeBar(bool foe, float y)
        {
            var b = new TowerBar();
            var rt = UiKit.NewRect(foe ? "EnemyTower" : "OwnTower", topRoot);
            rt.anchorMin = new Vector2(foe ? 0.5f : 0f, 1f);
            rt.anchorMax = new Vector2(foe ? 1f : 0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            float inner = BoxW * 0.5f + 10f;
            rt.offsetMin = new Vector2(foe ? inner : EdgeM, y - BarH);
            rt.offsetMax = new Vector2(foe ? -EdgeM : -inner, y);
            var back = UiKit.Fill(rt, TopPanel, UiKit.Rounded);
            back.pixelsPerUnitMultiplier = 3f;
            var clip = UiKit.NewRect("Clip", rt);
            UiKit.Stretch(clip, 3, 3, 3, 3);
            clip.gameObject.AddComponent<RectMask2D>();
            b.Trail = UiKit.Fill(clip, foe ? FoeTrail : OwnTrail);
            b.Fill = UiKit.Fill(clip, foe ? TopFoe : TopOwn);
            foreach (var img in new[] { b.Trail, b.Fill })
            {
                img.sprite = WhiteSprite; // Filled needs a sprite
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Horizontal;
                img.fillOrigin = (int)(foe ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right);
            }
            var shine = UiKit.Fill(clip, new Color(1f, 1f, 1f, 0.16f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0.6f);
            EdgeLine(back, TopEdge);
            b.Percent = UiKit.Label(rt, "100%", 24, foe ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, Color.white);
            UiKit.Stretch(b.Percent.rectTransform, 12, 0, 12, 0);
            UiKit.AddOutline(b.Percent, 2);
            return b;
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

        static void EdgeLine(Image img, Color color)
        {
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(2f, -2f);
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
