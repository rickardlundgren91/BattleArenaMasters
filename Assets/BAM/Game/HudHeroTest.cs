using System;
using System.Collections.Generic;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The HERO TEST bar (ABC2 Hud.cs drawHeroTestBar / HeroTestDown, herotest.js; Rickard's training area): a small dark bar at
    /// the top left under the tower bars with ‹ hero ▾ › Lv N Reset on the first row and CD 1s / CD off, Hide and Exit on the
    /// second. The name opens a list of every hero; ‹ › and the list start the test again with that hero, Lv steps the level
    /// (1..12, then 1) and starts again, Reset starts the same test again, CD makes the special ready 1 s after use
    /// (Match.HeroTestFastCd), Hide folds it to a small TEST button, Exit goes back to the menu. Sizes are the old ones in
    /// its ~412 px wide canvas, scaled to the HUD. The old FX and Skin buttons are left out (one look per hero here).
    /// </summary>
    sealed partial class Hud
    {
        const string HtOpenKey = "bam.ht.open", HtFastKey = "bam.ht.fastcd";
        static readonly Color HtPanel = new Color(12f / 255f, 16f / 255f, 40f / 255f, 0.72f), HtListPanel = new Color(12f / 255f, 16f / 255f, 40f / 255f, 0.94f);
        static readonly Color HtButton = new Color(1f, 1f, 1f, 0.14f), HtButtonOn = new Color(1f, 1f, 1f, 0.3f), HtPicked = Hex(0x3a5bd8);

        sealed class HtButtonRec
        {
            public RectTransform Root;
            public Image Back;
            public Text Label;
            public Image Caret; // the ▾ after the hero's name
        }

        RectTransform htRoot, htListRoot, htCatcher;
        Image htPanel, htListBack;
        HtButtonRec htPrev, htName, htNext, htLevel, htReset, htCd, htHide, htExit, htShow;
        readonly List<HtButtonRec> htPicks = new List<HtButtonRec>();
        bool htList;

        // read from PlayerPrefs once, then kept (the bar asks every frame; on Android each read is a native call)
        static int htOpenC = -1, htFastC = -1;
        static bool HtOpen
        {
            get { if (htOpenC < 0) htOpenC = PlayerPrefs.GetInt(HtOpenKey, 1); return htOpenC == 1; }
            set { htOpenC = value ? 1 : 0; PlayerPrefs.SetInt(HtOpenKey, htOpenC); PlayerPrefs.Save(); }
        }
        static bool HtFastCd
        {
            get { if (htFastC < 0) htFastC = PlayerPrefs.GetInt(HtFastKey, 0); return htFastC == 1; }
            set { htFastC = value ? 1 : 0; PlayerPrefs.SetInt(HtFastKey, htFastC); PlayerPrefs.Save(); }
        }
        HtButtonRec[] htBar;
        static readonly string[] htLevelText = new string[32];

        void BuildHeroTest()
        {
            htRoot = UiKit.NewRect("HeroTest", safe);
            UiKit.Stretch(htRoot);
            // a tap anywhere else closes the hero list (old: the tap is eaten)
            htCatcher = UiKit.NewRect("Catcher", htRoot);
            UiKit.Stretch(htCatcher, -400f, -400f, -400f, -400f);
            var c = htCatcher.gameObject.AddComponent<Image>();
            c.color = Color.clear;
            var cb = htCatcher.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.onClick.AddListener(() => htList = false);
            htPanel = TopLeftImage(htRoot, "Panel", HtPanel);
            htPrev = HtMake("‹", () => HtStep(-1));
            htName = HtMake("", () => htList = !htList);
            htName.Caret = TopLeftImage(htName.Root, "Caret", Color.white);
            htName.Caret.sprite = CaretSprite;
            htName.Caret.rectTransform.anchorMin = htName.Caret.rectTransform.anchorMax = htName.Caret.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            htNext = HtMake("›", () => HtStep(1));
            htLevel = HtMake("", HtNextLevel);
            htReset = HtMake("Reset", () => flow.RestartMatch());
            htCd = HtMake("", () => { HtFastCd = !HtFastCd; m.HeroTestFastCd = HtFastCd; });
            htHide = HtMake("Hide", () => HtOpen = false);
            htExit = HtMake("Exit", () => flow.QuitToMenu());
            htShow = HtMake("TEST", () => HtOpen = true);
            htListRoot = UiKit.NewRect("List", htRoot);
            htListRoot.anchorMin = htListRoot.anchorMax = htListRoot.pivot = new Vector2(0f, 1f);
            htListBack = UiKit.Fill(htListRoot, HtListPanel, UiKit.Rounded);
            htListBack.raycastTarget = true;
            for (int i = 0; i < HeroCatalog.HeroTestList.Length; i++)
            {
                HeroId id = HeroCatalog.HeroTestList[i].Id;
                var b = HtMake(HeroCatalog.HeroTestList[i].Name, () => { htList = false; HtStart(id, m.Config.HeroTestLevel); }, htListRoot);
                htPicks.Add(b);
            }
            htRoot.gameObject.SetActive(false);
        }

        void BeginHeroTest()
        {
            htList = false;
            if (heroTest) m.HeroTestFastCd = HtFastCd;
            htRoot.gameObject.SetActive(heroTest);
        }

        void HtStep(int dir)
        {
            var all = HeroCatalog.HeroTestList;
            int i = Array.FindIndex(all, h => h.Id == m.Player.Hero);
            HtStart(all[(i + dir + all.Length) % all.Length].Id, m.Config.HeroTestLevel);
        }

        void HtNextLevel() => HtStart(m.Player.Hero, m.Config.HeroTestLevel % Rules.MaxLevel + 1);

        void HtStart(HeroId hero, int level)
        {
            htList = false;
            flow.StartMatch(new MatchSetup { Hero = hero, HeroTest = true, Level = level, Difficulty = BamSettings.LastDifficulty });
        }

        void HeroTestFrame(bool hidden)
        {
            bool on = heroTest && !hidden;
            if (htRoot.gameObject.activeSelf != on) htRoot.gameObject.SetActive(on);
            if (!on) return;
            float s = SafeW / 412f, H = safe.rect.height;
            float x = 6f * s, y = H * 0.09f, bh = 24f * s, gap = 3f * s, fs = 11f * s;
            bool open = HtOpen;
            htCatcher.gameObject.SetActive(open && htList);
            htShow.Root.gameObject.SetActive(!open);
            htPanel.gameObject.SetActive(open);
            if (htBar == null) htBar = new[] { htPrev, htName, htNext, htLevel, htReset, htCd, htHide, htExit };
            foreach (var b in htBar) if (b.Root.gameObject.activeSelf != open) b.Root.gameObject.SetActive(open);
            htListRoot.gameObject.SetActive(open && htList);
            if (!open)
            {
                HtPlace(htShow, 4f * s, y, 40f * s, bh, fs, false);
                return;
            }

            string nm = HeroCatalog.Get(m.Player.Hero).Name;
            SetText(htName.Label, nm);
            int lv = m.Config.HeroTestLevel;
            SetText(htLevel.Label, lv >= 0 && lv < htLevelText.Length ? htLevelText[lv] ?? (htLevelText[lv] = "Lv " + lv) : "Lv " + lv);
            SetText(htCd.Label, m.HeroTestFastCd ? "CD 1s" : "CD off");
            float caret = fs * 0.9f;
            float wName = Mathf.Max(7.5f * fs, TextWidth(htName.Label, fs) + caret + 16f * s);
            float wLevel = Mathf.Max(bh, TextWidth(htLevel.Label, fs) + 16f * s), wReset = TextWidth(htReset.Label, fs) + 16f * s;
            float row1 = bh + gap + wName + gap + bh + gap + wLevel + gap + wReset;
            float row2 = 48f * s + gap + 42f * s + gap + 42f * s;
            float total = Mathf.Max(row1, row2) + 8f * s;
            var pr = htPanel.rectTransform;
            pr.anchoredPosition = new Vector2(x, -y);
            pr.sizeDelta = new Vector2(total, 2f * bh + gap + 8f * s);
            Corners(htPanel, 12f * s);

            float bx = x + 4f * s, by = y + 4f * s;
            HtPlace(htPrev, bx, by, bh, bh, fs * 1.6f, false); bx += bh + gap;
            HtPlace(htName, bx, by, wName, bh, fs, htList);
            htName.Label.rectTransform.offsetMax = new Vector2(-caret, 0f);
            htName.Caret.rectTransform.anchoredPosition = new Vector2(wName / 2f - caret * 0.9f, 0f);
            htName.Caret.rectTransform.sizeDelta = new Vector2(caret, caret);
            bx += wName + gap;
            HtPlace(htNext, bx, by, bh, bh, fs * 1.6f, false); bx += bh + gap;
            HtPlace(htLevel, bx, by, wLevel, bh, fs, false); bx += wLevel + gap;
            HtPlace(htReset, bx, by, wReset, bh, fs, false);
            bx = x + 4f * s; by += bh + gap;
            HtPlace(htCd, bx, by, 48f * s, bh, fs, m.HeroTestFastCd); bx += 48f * s + gap;
            HtPlace(htHide, bx, by, 42f * s, bh, fs, false); bx += 42f * s + gap;
            HtPlace(htExit, bx, by, 42f * s, bh, fs, false);

            if (htList)
            {
                // two columns of every hero under the bar; the current one in blue
                float lx = x, ly = y + 2f * bh + gap + 12f * s, cw = 96f * s, chh = 24f * s;
                int rows = (htPicks.Count + 1) / 2;
                htListRoot.anchoredPosition = new Vector2(lx, -ly);
                htListRoot.sizeDelta = new Vector2(2f * cw + 4f * s + 12f * s, rows * (chh + 4f * s) + 8f * s);
                Corners(htListBack, 12f * s);
                for (int i = 0; i < htPicks.Count; i++)
                {
                    var b = htPicks[i];
                    HtPlace(b, 6f * s + (i % 2) * (cw + 4f * s), 6f * s + (i / 2) * (chh + 4f * s), cw, chh, fs, false);
                    b.Back.color = HeroCatalog.HeroTestList[i].Id == m.Player.Hero ? HtPicked : HtButton;
                }
                htListRoot.SetAsLastSibling();
            }
        }

        static float TextWidth(Text t, float size)
        {
            t.fontSize = Mathf.Max(6, Mathf.RoundToInt(size * 1.35f)); // Lilita's caps sit a little lower than the old bold
            return t.preferredWidth;
        }

        // a button's rect from the parent's top left (the old canvas's x, y down)
        static void HtPlace(HtButtonRec b, float x, float y, float w, float h, float fs, bool on)
        {
            b.Root.anchoredPosition = new Vector2(x, -y);
            b.Root.sizeDelta = new Vector2(w, h);
            Corners(b.Back, 8f * h / 24f); // the old 8 px corners on a 24 px button
            b.Back.color = on ? HtButtonOn : HtButton;
            b.Label.fontSize = Mathf.Max(6, Mathf.RoundToInt(fs * 1.35f));
        }

        HtButtonRec HtMake(string label, Action onClick, RectTransform parent = null)
        {
            var b = new HtButtonRec();
            b.Root = UiKit.NewRect(label.Length > 0 ? label : "Button", parent != null ? parent : htRoot);
            b.Root.anchorMin = b.Root.anchorMax = b.Root.pivot = new Vector2(0f, 1f);
            b.Back = UiKit.Fill(b.Root, HtButton, UiKit.Rounded);
            b.Back.raycastTarget = true;
            var btn = b.Root.gameObject.AddComponent<Button>();
            btn.targetGraphic = b.Back;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); onClick(); });
            b.Label = UiKit.Label(b.Root, label, 15, TextAnchor.MiddleCenter, Color.white);
            b.Label.raycastTarget = false;
            b.Label.horizontalOverflow = HorizontalWrapMode.Overflow;
            b.Label.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Stretch(b.Label.rectTransform);
            return b;
        }

        static Image TopLeftImage(Transform parent, string name, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        // ▾: a small triangle pointing down (the font has no such glyph)
        static Sprite caretSprite;
        static Sprite CaretSprite
        {
            get
            {
                if (caretSprite != null) return caretSprite;
                const int n = 32;
                var tri = new List<Vector2> { new Vector2(4f, 9f), new Vector2(28f, 9f), new Vector2(16f, 25f) }; // y down
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Caret", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int hits = 0;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                                if (Inside(tri, new Vector2(x + (sx + 0.5f) / 4f, n - (y + (sy + 0.5f) / 4f)))) hits++;
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                caretSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return caretSprite;
            }
        }
    }
}
