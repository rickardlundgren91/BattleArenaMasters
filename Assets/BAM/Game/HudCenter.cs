using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The middle of the screen as the old game draws it with the Glass HUD (ABC2 Hud.cs drawHUD, HudGlass.cs): "Match starts in"
    /// with the seconds on a glass banner during the countdown, the one-line start banner DESTROY THE ENEMY TOWER (gold and
    /// orange on slim glass) for 2.5 s after it, while dead a dim screen with RESPAWN IN, the seconds and who did it, and
    /// VICTORY or DEFEAT until the result comes up. Also the pause button on a small round glass plate at the right edge, where
    /// the old game had its glass tool button.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color BannerFill = new Color(8f / 255f, 12f / 255f, 30f / 255f, 0.78f), BannerGlow = new Color(1f, 190f / 255f, 90f / 255f, 0.14f);
        static readonly Color BannerInner = new Color(232f / 255f, 180f / 255f, 80f / 255f, 0.3f), Diamond = Hex(0xf0c060), Ink2 = Hex(0x1a1f4a);
        const string StartLine = "<color=#f6d27a>DESTROY THE </color><color=#ff8a1f>ENEMY TOWER</color>";

        RectTransform countBanner, startBanner, pauseRoot;
        CanvasGroup startGroup;
        Text countTitle, countNum, startText, centerText, respawnTitle, respawnText, defeatedText;
        Image dim;
        int shownCount, shownRespawn;
        bool overShown;

        void BeginCenter()
        {
            shownCount = shownRespawn = -1;
            overShown = false;
            centerText.text = "";
            countBanner.gameObject.SetActive(false);
            countTitle.gameObject.SetActive(false);
            countNum.gameObject.SetActive(false);
            startBanner.gameObject.SetActive(false);
            SetRespawn(false);
        }

        void SetRespawn(bool on)
        {
            dim.enabled = on;
            respawnTitle.gameObject.SetActive(on);
            respawnText.gameObject.SetActive(on);
            if (!on) defeatedText.gameObject.SetActive(false);
        }

        void Center()
        {
            LayoutCenter();
            // the countdown: "Match starts in" and the seconds on a glass banner
            bool counting = m.State == MatchState.Countdown && !tutorial && !heroTest;
            if (countBanner.gameObject.activeSelf != counting)
            {
                countBanner.gameObject.SetActive(counting);
                countTitle.gameObject.SetActive(counting);
                countNum.gameObject.SetActive(counting);
            }
            if (counting)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(-m.Time));
                if (n != shownCount)
                {
                    shownCount = n;
                    countNum.text = Num(n);
                    if (n <= 3) BamAudioCountdown();
                }
            }

            // the start banner: full for 1.5 s, then fading out over the last second
            float left = bannerUntil - Time.unscaledTime;
            bool banner = left > 0f && !tutorial && !heroTest; // no start banner in HERO TEST (old Hud)
            if (startBanner.gameObject.activeSelf != banner) startBanner.gameObject.SetActive(banner);
            if (banner) startGroup.alpha = Mathf.Clamp01(left);

            // the over word until the result comes up
            if (m.State == MatchState.Over && !tutorial && !overShown)
            {
                overShown = true;
                bool won = m.Winner == m.Player.Team;
                centerText.text = won ? "VICTORY" : "DEFEAT";
            }

            var p = m.Player;
            bool waiting = !p.Alive && m.State == MatchState.Play;
            if (respawnText.gameObject.activeSelf != waiting)
            {
                SetRespawn(waiting);
                bool by = waiting && killedBy != null && (killedBy.IsHero || killedBy.Kind == UnitKind.Wolf || killedBy.Kind == UnitKind.Tower);
                defeatedText.gameObject.SetActive(by);
                if (by) defeatedText.text = "Defeated by " + KillerName(killedBy);
            }
            if (waiting)
            {
                int s = Mathf.Max(1, Mathf.CeilToInt(p.RespawnT));
                if (s != shownRespawn) { shownRespawn = s; respawnText.text = Num(s); }
            }

            bool denied = Time.unscaledTime < deniedUntil;
            if (deniedText.gameObject.activeSelf != denied) deniedText.gameObject.SetActive(denied);
        }

        static void BamAudioCountdown() => BAM.Audio.BamAudio.Play(BAM.Audio.SfxId.Countdown);

        // "Defeated by Fenn · Hero" as the old hitByName · hitByKind
        static string KillerName(Unit k)
        {
            var who = k.Kind == UnitKind.Wolf && k.Owner != null ? k.Owner : k.IsClone && k.Owner != null ? k.Owner : k;
            if (who.Kind == UnitKind.Tower) return "the tower";
            return HeroCatalog.Get(who.Hero).Name + (k.Kind == UnitKind.Wolf ? " · Wolf" : " · Hero");
        }

        // ---------------------------------------------------------------- layout and building

        Vector2 centerLaid;

        void LayoutCenter()
        {
            Vector2 size = safe.rect.size;
            if (size == centerLaid || size.x < 1f) return;
            centerLaid = size;
            float W = size.x, H = size.y;
            // the countdown banner: W*0.15..0.85 across, from 0.26 H down, the words at 0.3 H and the seconds under them
            Box(countBanner, W * 0.7f, H * 0.085f, H * 0.26f);
            GlassBannerLayout(countBanner, W);
            Line(countTitle, W * 0.04f, -H * 0.3f + W * 0.02f, W);
            Line(countNum, W * 0.06f, -H * 0.335f + W * 0.03f, W);
            // the start banner: one line at 0.27 H on a slim plate
            float fs = W * 0.052f, ph = H * 0.06f;
            startText.fontSize = Mathf.RoundToInt(fs);
            startText.text = StartLine;
            float tw = startText.preferredWidth;
            Box(startBanner, Mathf.Min(W - 32f, tw + fs * 2.2f), ph, H * 0.27f - ph / 2f);
            GlassBannerLayout(startBanner, W);
            // respawn: RESPAWN IN at 0.36 H, the seconds at 0.44 H, who did it at 0.5 H (baselines in the old canvas)
            Line(respawnTitle, W * 0.045f, -H * 0.36f + W * 0.02f, W);
            Line(respawnText, W * 0.14f, -H * 0.44f + W * 0.05f, W);
            Line(defeatedText, W * 0.045f, -H * 0.5f + W * 0.02f, W);
            Line(centerText, W * 0.115f, -H * 0.22f, W);
            // the glass tool (score tab, or pause in the guided match): the old glass tool's place under the strip at the right edge
            float r = W * 0.034f, pad = W * 0.01f, gb = Mathf.Max(4f, H * 0.01f) + (W * 0.064f + W * 0.024f) + Mathf.Max(7f, W * 0.02f) * 1.6f;
            pauseRoot.anchorMin = pauseRoot.anchorMax = pauseRoot.pivot = new Vector2(1f, 1f);
            pauseRoot.sizeDelta = Vector2.one * (2f * (r + pad));
            pauseRoot.anchoredPosition = new Vector2(-(W * 0.025f - pad), -(gb + H * 0.012f - pad));
            toolPause.fontSize = Mathf.RoundToInt(r * 1.1f);
        }

        static void Box(RectTransform rt, float w, float h, float top)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(0f, -top);
        }

        static void Line(Text t, float font, float y, float w)
        {
            t.fontSize = Mathf.Max(8, Mathf.RoundToInt(font));
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(w, font * 1.6f);
            rt.anchoredPosition = new Vector2(0f, y + font * 0.8f);
        }

        void BuildCenter()
        {
            dim = UiKit.Fill(safe, new Color(0f, 0f, 0f, 0.35f));
            UiKit.Stretch(dim.rectTransform, -2000f, -2000f, -2000f, -2000f); // out past every screen edge: the sides on a foldable, and on a phone
            // over the status and navigation bars too, where the game picture runs on (it stopped at the safe area's top and bottom and
            // left a hard lighter band there while dead, Rickard 2026-11-02 "nåt filter över som inte hänger med")
            dim.transform.SetAsFirstSibling(); // under the controls
            dim.enabled = false;

            countBanner = GlassBanner(safe);
            countTitle = InkLabel(safe, "Match starts in", Color.white, 4);
            countNum = InkLabel(safe, "", Color.white, 4);
            countTitle.gameObject.SetActive(false);
            countNum.gameObject.SetActive(false);

            startBanner = GlassBanner(safe);
            startGroup = startBanner.gameObject.AddComponent<CanvasGroup>();
            startGroup.blocksRaycasts = false;
            startText = TopLabel(startBanner, Color.white, TextAnchor.MiddleCenter);
            startText.supportRichText = true;
            startText.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(startText.rectTransform);

            centerText = InkLabel(safe, "", Color.white, 6);
            respawnTitle = InkLabel(safe, "RESPAWN IN", Color.white, 4);
            respawnText = InkLabel(safe, "", Color.white, 6);
            defeatedText = InkLabel(safe, "", Color.white, 4);
        }

        // the old small()/big(): white with a thick dark navy edge
        static Text InkLabel(Transform parent, string text, Color color, float edge)
        {
            var t = UiKit.Label(parent, text, 40, TextAnchor.MiddleCenter, color);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = Ink2;
            o.effectDistance = new Vector2(edge * 0.5f, -edge * 0.5f);
            return t;
        }

        // the old glassBanner: a dark plate, a soft gold glow, a gold edge, a faint inner line and gold diamonds at the ends
        static RectTransform GlassBanner(Transform parent)
        {
            var rt = UiKit.NewRect("Banner", parent);
            var glow = UiKit.Fill(rt, BannerGlow);
            UiKit.Stretch(glow.rectTransform, -3, -3, -3, -3);
            glow.name = "Glow";
            var fill = UiKit.Fill(rt, BannerFill, UiKit.Rounded);
            fill.name = "Plate";
            var edge = UiKit.Fill(rt, GlassGold);
            edge.name = "Edge";
            var inner = UiKit.Fill(rt, BannerInner);
            inner.name = "Inner";
            UiKit.Stretch(inner.rectTransform, 4, 4, 4, 4);
            for (int i = 0; i < 2; i++)
            {
                var d = UiKit.NewRect("Diamond", rt);
                d.anchorMin = d.anchorMax = new Vector2(i, 0.5f);
                d.pivot = new Vector2(0.5f, 0.5f);
                d.anchoredPosition = new Vector2(i == 0 ? 4f : -4f, 0f);
                d.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var img = d.gameObject.AddComponent<Image>();
                img.color = Diamond;
                img.raycastTarget = false;
            }
            rt.gameObject.SetActive(false);
            return rt;
        }

        static void GlassBannerLayout(RectTransform rt, float W)
        {
            float h = rt.sizeDelta.y, c = Mathf.Min(h * 0.28f, W * 0.04f);
            for (int i = 0; i < rt.childCount; i++)
            {
                var ch = rt.GetChild(i);
                var img = ch.GetComponent<Image>();
                switch (ch.name)
                {
                    case "Glow": RoundRing(img, c + 3f, 6f); break;
                    case "Plate": Corners(img, c); break;
                    case "Edge": RoundRing(img, c, 2f); break;
                    case "Inner": RoundRing(img, c * 0.7f, 1f); break;
                    case "Diamond": float d = Mathf.Max(4f, h * 0.09f) * 1.41f; ((RectTransform)ch).sizeDelta = new Vector2(d, d); break;
                }
            }
        }

        void BuildPause()
        {
            pauseRoot = UiKit.NewRect("Pause", safe);
            var plate = UiKit.Fill(pauseRoot, GlassFill, UiKit.Circle);
            plate.raycastTarget = true;
            var edge = UiKit.Fill(pauseRoot, GlassEdge, UiKit.CircleRing);
            edge.raycastTarget = false;
            var b = pauseRoot.gameObject.AddComponent<Button>();
            b.targetGraphic = plate;
            b.onClick.AddListener(ToolTapped); // the score tab (HudScore.cs); pause in the guided match
            toolIcon = UiKit.Fill(pauseRoot, Color.white, ScoreIconSprite);
            UiKit.Stretch(toolIcon.rectTransform, 0f, 0f, 0f, 0f);
            toolPause = TopLabel(pauseRoot, Color.white, TextAnchor.MiddleCenter);
            toolPause.text = "II";
            toolPause.enabled = false;
            UiKit.Stretch(toolPause.rectTransform);
        }
    }
}
