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
        const float NavH = 180f; // the old tab bar: cw(18)
        static readonly Color PageFill = new Color(0.027f, 0.03f, 0.09f, 0.97f);
        static readonly Color Lavender = new Color(0.45f, 0.47f, 0.85f);      // choices not picked
        static readonly Color NavIdle = new Color32(0xbc, 0xc2, 0xec, 0xff);  // the tabs not shown (HubNav, ONE_PLAY)
        static readonly Color NavBg = new Color32(0x0a, 0x0d, 0x26, 0xff);

        static GameObject nav;
        static Image[] navIcons;
        static Text[] navLabels;
        static Image[] navMarks;
        static GameObject[] navPages;
        static RectTransform[] tabPanels = new RectTransform[4]; // Home, Heroes, Settings, and the hero page under Heroes

        // ---- bottom navigation ----

        static void BuildNav()
        {
            var rt = UiKit.NewRect("Nav", root);
            UiKit.Stretch(rt);
            nav = rt.gameObject;
            // on a foldable or tablet the bar's colour and top line run on to the screen's sides (the tabs stay in the frame)
            var wideBar = UiKit.Fill(UiKit.WideArea(rt), NavBg);
            UiKit.Band(wideBar, false, 0, NavH, 0);
            var wideBelow = UiKit.Fill(wideBar.rectTransform, NavBg);
            wideBelow.rectTransform.anchorMin = new Vector2(0, 0);
            wideBelow.rectTransform.anchorMax = new Vector2(1, 0);
            wideBelow.rectTransform.pivot = new Vector2(0.5f, 1);
            wideBelow.rectTransform.sizeDelta = new Vector2(0, 400);
            wideBelow.rectTransform.anchoredPosition = Vector2.zero;
            UiKit.Band(UiKit.Fill(wideBar.rectTransform, new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.42f)), true, 0, 3, 0);
            var safe = UiKit.SafeArea(rt);
            var bar = UiKit.Fill(safe, NavBg);
            bar.raycastTarget = true;
            UiKit.Band(bar, false, 0, NavH, 0);
            // The bar's colour continues below the safe area to the screen's edge.
            var below = UiKit.Fill(bar.rectTransform, bar.color);
            below.rectTransform.anchorMin = new Vector2(0, 0);
            below.rectTransform.anchorMax = new Vector2(1, 0);
            below.rectTransform.pivot = new Vector2(0.5f, 1);
            below.rectTransform.sizeDelta = new Vector2(0, 400);
            below.rectTransform.anchoredPosition = Vector2.zero;
            var edge = UiKit.Fill(bar.rectTransform, new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.42f)); // HUB_LINE
            UiKit.Band(edge, true, 0, 3, 0);

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

                // HubNav: the icon at 40 % of the bar, the label at 78 %, a mint mark at the top of the shown tab
                var icon = UiKit.NewRect("Icon", tab);
                UiKit.Place(icon, 0.5f, 1, 0, -NavH * 0.4f, 0, 0);
                navIcons[i] = DrawIcon(icon, i);
                var label = UiKit.Label(tab, names[i], 26, TextAnchor.MiddleCenter, NavIdle);
                label.font = UiKit.BodyFont;
                label.fontStyle = FontStyle.Bold;
                UiKit.Place(label, 0.5f, 1, 0, -NavH * 0.78f + 20, 300, 40);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                navLabels[i] = label;
                var mark = UiKit.Fill(tab, UiKit.Primary, UiKit.Rounded);
                mark.pixelsPerUnitMultiplier = 28f / 4.5f;
                UiKit.Place(mark, 0.5f, 1, 0, -3, 120, 9);
                navMarks[i] = mark;
            }
            nav.SetActive(false);
            BuildModes(); // above the bar, so its veil covers it
        }

        // HubIcon, drawn like the old tab bar (s = cw(3.2)): a house with a door, a person, and HubGear for Settings. Every
        // part is a child Image; RefreshNav tints the light ones together and the dark cut-outs keep the bar's colour.
        static Image DrawIcon(RectTransform box, int kind)
        {
            const float s = 32f;
            Image Part(Sprite sprite, float x, float y, float w, float h)
            {
                var p = UiKit.Fill(box, NavIdle, sprite);
                var r = p.rectTransform;
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = new Vector2(x, y);
                r.sizeDelta = new Vector2(w, h);
                return p;
            }
            Image first;
            switch (kind)
            {
                case 0: // house: a roof over a body with a door
                    first = Part(HomeArt.Roof, 0, s * 0.55f, s * 2.2f, s);
                    Part(null, 0, -s * 0.45f, s * 1.5f, s * 1.1f);
                    var door = Part(UiKit.Rounded, 0, -s * 0.675f, s * 0.44f, s * 0.65f);
                    door.color = NavBg;
                    door.pixelsPerUnitMultiplier = 28f / (s * 0.12f);
                    break;
                case 1: // person: a head and the top half of an ellipse for the shoulders
                    first = Part(UiKit.Circle, 0, s * 0.45f, s, s);
                    var body = Part(UiKit.Circle, 0, -s * 1.0f, s * 1.9f, s * 1.6f);
                    body.type = Image.Type.Filled;
                    body.fillMethod = Image.FillMethod.Vertical;
                    body.fillOrigin = (int)Image.OriginVertical.Top;
                    body.fillAmount = 0.5f;
                    break;
                default: // gear
                    Gear(box, s, NavIdle, NavBg);
                    first = box.GetChild(0).GetComponent<Image>();
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
            if (top != null && top == heroPage) active = 1; // one hero belongs to the Heroes tab (HubTab)
            bool show = active >= 0 && (Flow.Game == null || !Flow.Game.InMatch);
            nav.SetActive(show);
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null) tabPanels[i].offsetMin = new Vector2(tabPanels[i].offsetMin.x, show ? NavH : 24f);
            if (!show) return;
            for (int i = 0; i < 3; i++)
            {
                var c = i == active ? UiKit.Primary : NavIdle;
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
    }
}
