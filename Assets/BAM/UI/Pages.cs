using System;
using BAM.Audio;
using BAM.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The old game's page frame (ABC2 Hub, Rickard's screenshots 2026-10-25): a "< Back" pill, then one dark panel with a gold
    // edge and the page title, and the bottom navigation bar on the tab pages (Home, Heroes, Settings).
    public static partial class Screens
    {
        const float NavH = 150f;
        static readonly Color PageBg = new Color(0.02f, 0.024f, 0.075f);      // the near-black navy behind the panel
        static readonly Color PageFill = new Color(0.027f, 0.03f, 0.09f, 0.97f);
        static readonly Color CardFill = new Color(0.035f, 0.04f, 0.11f);
        static readonly Color Lavender = new Color(0.45f, 0.47f, 0.85f);      // inactive tabs, choices not picked
        static readonly Color Section = new Color(0.2f, 0.85f, 0.45f);        // the green section headers (AUDIO, GRAPHICS...)

        static GameObject nav;
        static Image[] navIcons;
        static Text[] navLabels;
        static Image[] navMarks;
        static GameObject[] navPages;
        static RectTransform[] tabPanels = new RectTransform[3];

        /// <summary>A page: dark background, a back pill and a framed panel with the title. Returns the panel's scroll content.</summary>
        static RectTransform NewPage(string title, out GameObject go, out RectTransform panelRt, bool scroll = true)
        {
            var rt = UiKit.NewRect(title, root);
            UiKit.Stretch(rt);
            go = rt.gameObject;
            UiKit.Fill(rt, PageBg).raycastTarget = true;
            var body = UiKit.SafeArea(rt);

            var back = PillButton(body, "<  Back", () => HandleBack());
            UiKit.Place(back, 0, 1, 24, -20, 190, 80);

            var panel = UiKit.Panel(body, PageFill);
            panelRt = panel.rectTransform;
            UiKit.Stretch(panelRt, 0, 120, 0, NavH);
            var frame = UiKit.Frame(panelRt);
            frame.pixelsPerUnitMultiplier = 0.8f;

            var t = UiKit.Label(panelRt, title, 64, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Band(t, true, 16, 90, 30);
            UiKit.AddShadow(t, 3);
            var line = UiKit.Fill(panelRt, UiKit.Trim);
            line.rectTransform.anchorMin = line.rectTransform.anchorMax = line.rectTransform.pivot = new Vector2(0, 1);
            line.rectTransform.anchoredPosition = new Vector2(32, -108);
            line.rectTransform.sizeDelta = new Vector2(Mathf.Max(80, title.Length * 30), 4);
            var dot = UiKit.Fill(line.rectTransform, UiKit.Orange);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(1, 0.5f);
            dot.rectTransform.sizeDelta = new Vector2(12, 12);
            dot.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            go.SetActive(false);
            if (!scroll) return panelRt;
            var area = UiKit.NewRect("Area", panelRt);
            UiKit.Stretch(area, 16, 130, 16, 16);
            var content = ScrollContent(area, out _);
            return content;
        }

        /// <summary>The small dark "&lt; Back" pill of the old pages.</summary>
        static Button PillButton(Transform parent, string text, Action onClick)
        {
            var rt = UiKit.NewRect(text, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UiKit.Rounded;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.7f;
            img.color = new Color(0.1f, 0.11f, 0.22f);
            var label = UiKit.Label(rt, text, 38, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiBack); onClick?.Invoke(); });
            rt.gameObject.AddComponent<ButtonFx>();
            return b;
        }

        // ---- bottom navigation ----

        static void BuildNav()
        {
            var rt = UiKit.NewRect("Nav", root);
            UiKit.Stretch(rt);
            nav = rt.gameObject;
            var safe = UiKit.SafeArea(rt);
            var bar = UiKit.Fill(safe, new Color(0.04f, 0.045f, 0.12f));
            bar.raycastTarget = true;
            UiKit.Band(bar, false, 0, NavH, 0);
            // The bar's colour continues below the safe area to the screen's edge.
            var below = UiKit.Fill(bar.rectTransform, bar.color);
            below.rectTransform.anchorMin = new Vector2(0, 0);
            below.rectTransform.anchorMax = new Vector2(1, 0);
            below.rectTransform.pivot = new Vector2(0.5f, 1);
            below.rectTransform.sizeDelta = new Vector2(0, 400);
            below.rectTransform.anchoredPosition = Vector2.zero;
            var edge = UiKit.Fill(bar.rectTransform, new Color(1, 1, 1, 0.06f));
            UiKit.Band(edge, true, 0, 2, 0);

            string[] names = { "HOME", "HEROES", "SETTINGS" };
            navPages = new[] { mainMenu, heroSelect, settings };
            navIcons = new Image[3];
            navLabels = new Text[3];
            navMarks = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                var tab = UiKit.NewRect(names[i], bar.rectTransform);
                tab.anchorMin = new Vector2(i / 3f, 0);
                tab.anchorMax = new Vector2((i + 1) / 3f, 1);
                tab.offsetMin = tab.offsetMax = Vector2.zero;
                var hit = tab.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var b = tab.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); ShowTab(index); });
                tab.gameObject.AddComponent<ButtonFx>();

                var icon = UiKit.NewRect("Icon", tab);
                UiKit.Place(icon, 0.5f, 1, 0, -22, 64, 64);
                navIcons[i] = DrawIcon(icon, i);
                var label = UiKit.Label(tab, names[i], 30, TextAnchor.MiddleCenter, Lavender);
                UiKit.Band(label, false, 14, 44, 0);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                navLabels[i] = label;
                var mark = UiKit.Fill(tab, UiKit.Primary);
                UiKit.Place(mark, 0.5f, 1, 0, 0, 120, 6);
                navMarks[i] = mark;
            }
            nav.SetActive(false);
            BuildModes(); // above the bar, so its veil covers it
        }

        // Simple code-drawn icons: a house, a person, a gear. All parts share the icon's colour through a CanvasGroup-free trick:
        // every part is a child Image, and RefreshNav tints them together.
        static Image DrawIcon(RectTransform box, int kind)
        {
            Image Part(Sprite s, float x, float y, float w, float h, float rot = 0)
            {
                var p = UiKit.Fill(box, Lavender, s);
                var r = p.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x, y);
                r.sizeDelta = new Vector2(w, h);
                r.localRotation = Quaternion.Euler(0, 0, rot);
                if (s == UiKit.Rounded) p.pixelsPerUnitMultiplier = 4f;
                return p;
            }
            Image first;
            switch (kind)
            {
                case 0: // house: a roof (turned square) over a body with a door gap
                    first = Part(UiKit.Rounded, 0, 10, 40, 40, 45);
                    Part(UiKit.Rounded, 0, -10, 44, 34);
                    Part(null, 0, -16, 12, 22).color = new Color(0.04f, 0.045f, 0.12f);
                    break;
                case 1: // person: head and shoulders
                    first = Part(UiKit.Circle, 0, 14, 30, 30);
                    Part(UiKit.Rounded, 0, -16, 52, 26);
                    break;
                default: // gear: eight teeth round a ring
                    first = Part(UiKit.Circle, 0, 0, 44, 44);
                    for (int t = 0; t < 8; t++)
                    {
                        float a = t * Mathf.PI / 4f;
                        Part(null, Mathf.Cos(a) * 24, Mathf.Sin(a) * 24, 12, 12, t * 45f);
                    }
                    Part(UiKit.Circle, 0, 0, 18, 18).color = new Color(0.04f, 0.045f, 0.12f);
                    break;
            }
            return first;
        }

        static void ShowTab(int index)
        {
            ClearStack();
            Push(mainMenu);
            if (index > 0) Push(navPages[index]);
        }

        // The bar shows on the three tab pages outside a match; the tab pages make room for it only while it shows.
        static void RefreshNav()
        {
            if (nav == null) return;
            GameObject top = null;
            for (int i = stack.Count - 1; i >= 0 && top == null; i--) if (!IsOverlay(stack[i])) top = stack[i];
            int active = -1;
            for (int i = 0; i < navPages.Length; i++) if (navPages[i] == top) active = i;
            bool show = active >= 0 && (Flow.Game == null || !Flow.Game.InMatch);
            nav.SetActive(show);
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null) tabPanels[i].offsetMin = new Vector2(tabPanels[i].offsetMin.x, show ? NavH : 24f);
            if (!show) return;
            for (int i = 0; i < 3; i++)
            {
                var c = i == active ? UiKit.Primary : Lavender;
                navLabels[i].color = c;
                navMarks[i].enabled = i == active;
                var box = navIcons[i].transform.parent;
                for (int k = 0; k < box.childCount; k++)
                {
                    var img = box.GetChild(k).GetComponent<Image>();
                    // the dark cut-outs (door, gear hole) keep their colour
                    if (img != null && img.color.r + img.color.g + img.color.b > 0.4f) img.color = c;
                }
            }
        }

        // ---- page rows (Settings) ----

        static Text SectionHeader(RectTransform list, string text)
        {
            var t = UiKit.Label(list, text, 32, TextAnchor.LowerLeft, Section);
            t.font = UiKit.BodyFont;
            t.fontStyle = FontStyle.Bold;
            t.gameObject.AddComponent<LayoutElement>().preferredHeight = 70;
            return t;
        }

        /// <summary>A dark row with a gold edge, a label on the left (plain bold font, like the old Settings).</summary>
        static RectTransform PageRow(RectTransform list, string text, float height, out Text label)
        {
            var row = UiKit.NewRect(text, list);
            var bg = row.gameObject.AddComponent<Image>();
            bg.sprite = UiKit.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = CardFill;
            bg.raycastTarget = false;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            UiKit.Frame(row).color = new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.75f);
            label = UiKit.Label(row, text, 40, TextAnchor.MiddleLeft, UiKit.TextColor);
            label.font = UiKit.BodyFont;
            label.fontStyle = FontStyle.Bold;
            UiKit.Stretch(label.rectTransform, 36, 0, 260, 0);
            return row;
        }
    }
}
