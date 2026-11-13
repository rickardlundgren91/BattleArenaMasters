using System.Collections.Generic;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The old game's v3 pages with ONE_PLAY (ABC2 MenuHome.cs HubPage, Menu.cs DrawPage/H2, MenuPages3.cs PageHeroes3 and
    // StickySelect, MenuHeroFit.cs PageHero3Fit), ported exactly (Rickard 2026-10-26, read from his live Speltest):
    // - every subpage: the menu gradient with a mint glow, "‹ Back" at the top left, one glass sheet with a gold top edge, an H2
    //   title with a gold rule and diamond
    // - Heroes: the grid only looks; a tap opens the hero's own page with the 3D figure (tap = attack, drag =
    //   turn), ‹ › to browse, role, stat bars against the best in the roster, special and passive, and SELECT at the bottom
    public static partial class Screens
    {
        const float PageTop = 14 * Cw, PageX = 3.5f * Cw;
        static readonly Color HubPanel = new Color(16 / 255f, 20 / 255f, 54 / 255f, 0.8f);
        static readonly Color HubLine = new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.42f);
        static readonly Color HubBody = new Color32(0xd6, 0xda, 0xfa, 0xff);
        static readonly Color HubRole = new Color32(0xc9, 0xcd, 0xf0, 0xff);
        static readonly Color HubPicBack = new Color32(0x1d, 0x23, 0x50, 0xff);
        static readonly Color HubSelFill = new Color32(0x18, 0x20, 0x4f, 0xff);
        static readonly Color HubDark = new Color32(0x06, 0x23, 0x1a, 0xff);

        // Home's painting behind every subpage, dimmed, so the pages feel like the same place (Rickard 2026-10-29: "samma
        // känsla, behåll vår stil"). Covers the frame, centred; nothing when the picture is missing.
        static void PageArt(RectTransform body)
        {
            var tex = Picture("home_bg", true);
            if (tex == null) return;
            var clip = UiKit.NewRect("Art", body);
            UiKit.Stretch(clip);
            clip.gameObject.AddComponent<RectMask2D>();
            var pic = UiKit.NewRect("Painting", clip);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = tex;
            raw.raycastTarget = false;
            var fit = pic.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fit.aspectRatio = tex.width / (float)tex.height;
            UiKit.Fill(clip, new Color(6 / 255f, 9 / 255f, 30 / 255f, 0.55f)).name = "Shade";
        }

        /// <summary>A subpage: background, Back pill, the glass sheet and (with a title) the H2. Returns the scroll content.
        /// back false (the Heroes and Settings tabs, 2026-10-31): no Back pill, the tab bar's HOME is the way back, and the sheet
        /// starts near the top instead of under the pill. The Android back button still works (HandleBack).</summary>
        static RectTransform NewPage(string title, out GameObject go, out RectTransform panelRt, bool scroll = true, float headExtra = 0, bool back = true)
        {
            var rt = UiKit.NewRect(title ?? "Page", root);
            UiKit.Stretch(rt);
            go = rt.gameObject;
            UiKit.Fill(rt, Color.black).raycastTarget = true; // black outside the standing frame
            UiKit.Background(UiKit.WideArea(rt)); // the gradient reaches the sides on a foldable or tablet
            var body = UiKit.SafeArea(rt);
            UiKit.Background(body);
            PageArt(body);
            var glow = UiKit.Fill(body, Color.white, HomeArt.PageGlow);
            glow.rectTransform.anchorMin = new Vector2(-0.35f, 0.32f);
            glow.rectTransform.anchorMax = new Vector2(1.35f, 1.04f);

            // DrawPage: a dark shadow, the glass from #1a1f4a to #0a0d26 with rounded top corners and the gold edge
            panelRt = UiKit.NewRect("Sheet", body);
            UiKit.Stretch(panelRt, 0, back ? PageTop : 4 * Cw, 0, NavH);
            float r = 4 * Cw;
            var shadow = Pic(panelRt, "Shadow", UiKit.Rounded, new Color(0, 0, 0, 0.35f));
            Radius(shadow, r);
            UiKit.Stretch(shadow.rectTransform, 0, -Cw, 0, -r);
            var glass = Pic(panelRt, "Glass", UiKit.Rounded, Color.white);
            Radius(glass, r);
            UiKit.Stretch(glass.rectTransform, 0, 0, 0, -r); // the bottom corners hide under the tab bar
            glass.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var grad = UiKit.NewRect("Gradient", glass.rectTransform);
            UiKit.Stretch(grad);
            var gi = grad.gameObject.AddComponent<RawImage>();
            gi.texture = HomeArt.Gradient(new Color32(0x1a, 0x1f, 0x4a, 0xd0), new Color32(0x0a, 0x0d, 0x26, 0xe6)); // glass: Home's art shows through
            gi.raycastTarget = false;
            var edge = Pic(glass.rectTransform, "Gold", HomeArt.RoundRing(0.4f / 4f), HubGold);
            RingRadius(edge, r);

            float contentTop = 2 * Cw;
            if (title != null) contentTop = H2(panelRt, title);

            // Back: the same as the Android back button
            if (back) BackPill(body);

            go.SetActive(false);
            if (!scroll) return panelRt;
            var area = UiKit.NewRect("Area", panelRt);
            UiKit.Stretch(area, PageX, contentTop + headExtra, PageX, 0);
            return ScrollContent(area, out _);
        }

        static RectTransform BackPill(RectTransform body)
        {
            var back = UiKit.NewRect("Back", body);
            var pill = Pic(back, "Pill", UiKit.Rounded, new Color(1, 1, 1, 0.1f));
            float bh = 3.6f * Cw * 1.2f + 3.6f * Cw;
            Radius(pill, bh / 2);
            var label = UiKit.Label(pill.transform, "‹ Back", Mathf.RoundToInt(3.6f * Cw), TextAnchor.MiddleCenter, Color.white);
            UiKit.Stretch(label.rectTransform);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float bw = label.preferredWidth + 8 * Cw;
            TopLeft(pill.rectTransform, 3 * Cw, 2.5f * Cw, bw, bh);
            TopLeft(back, 0, 0, bw + 6 * Cw, 13 * Cw); // a larger touch area from the frame's corner
            var hit = back.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var b = back.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => HandleBack());
            back.gameObject.AddComponent<ButtonFx>().PressScale = false;
            back.gameObject.AddComponent<HomePress>();
            return back;
        }

        // H2: the title in Lilita with a dark drop, a short gold rule and a diamond under it. Returns where the page starts.
        static float H2(RectTransform sheet, string text)
        {
            float s = 6.4f * Cw, by = 2 * Cw + Cw + s * 0.6f; // the old baseline
            var t = UiKit.Label(sheet, text, Mathf.RoundToInt(s), TextAnchor.MiddleLeft, Color.white);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color32(0x0b, 0x13, 0x30, 0xff);
            sh.effectDistance = new Vector2(0, -0.6f * Cw);
            TopLeft(t.rectTransform, PageX, by - 0.35f * s - 0.6f * s, 80 * Cw, 1.2f * s);
            float ly = by + 1.6f * Cw, lw = 16 * Cw, k = 0.8f * Cw;
            TopLeft(Pic(sheet, "Rule", null, HubGold).rectTransform, PageX, ly - 0.15f * Cw, lw, 0.3f * Cw);
            var d = Pic(sheet, "Diamond", null, UiKit.Gold).rectTransform;
            TopLeft(d, PageX + lw + 1.6f * Cw - k * 0.707f, ly - k * 0.707f, k * 1.414f, k * 1.414f);
            d.pivot = new Vector2(0.5f, 0.5f);
            d.anchoredPosition = new Vector2(PageX + lw + 1.6f * k, -ly);
            d.localRotation = Quaternion.Euler(0, 0, 45);
            return 2 * Cw + Cw + s + 4.5f * Cw;
        }

        // ---- Heroes: the grid ----

        static readonly List<HeroCard> heroCards = new List<HeroCard>();
        static ScrollRect heroScroll;

        sealed class HeroCard { public int Index; public Image Fill, Edge, EdgeOn; public GameObject Check, Lock; public Text LockLevel; public RectTransform Box, Pic; public float Aspect; }

        // One grid with all heroes (Rickard 2026-10-29: no ALL / BY ROLE bar)
        static void BuildHeroSelect()
        {
            var content = NewPage("Heroes", out heroSelect, out var panel, true, back: false); // a tab: HOME is in the bar under it
            tabPanels[1] = panel;
            heroScroll = panel.GetComponentInChildren<ScrollRect>();
            VList(content, 0, new RectOffset(0, 0, 0, Mathf.RoundToInt(4 * Cw)));

            var gridAll = HeroGrid(content, "All");
            for (int i = 0; i < HeroCatalog.MenuList.Length; i++) BuildHeroCard(gridAll, i); // in the editor also Ragn and Nässla (HeroCatalog.MenuList)
            // under the grid (2026-10-31): nothing showed that a card opens the hero's own page
            var hint = Treb(content, "Tap a hero to see what they do", 2.9f, UiKit.TextDim);
            hint.fontStyle = FontStyle.Normal;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 8 * Cw;
            if (heroScroll != null) heroScroll.verticalNormalizedPosition = 1;
            BuildHeroPage();
        }

        static RectTransform HeroGrid(RectTransform parent, string name)
        {
            var grid = UiKit.NewRect(name, parent);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.spacing = new Vector2(2.4f * Cw, 2.4f * Cw);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 3;
            gl.childAlignment = TextAnchor.UpperLeft;
            grid.gameObject.AddComponent<HeroGridFit>().Grid = gl;
            return grid;
        }

        // One card (PageHeroes3 Card): the portrait on a dark rounded box, the name, the role with its sign, a mint check if chosen
        static void BuildHeroCard(RectTransform grid, int index)
        {
            var info = HeroCatalog.MenuList[index];
            var card = UiKit.NewRect(info.Name, grid);
            var c = new HeroCard { Index = index };
            c.Fill = card.gameObject.AddComponent<Image>();
            c.Fill.sprite = UiKit.Rounded;
            c.Fill.type = Image.Type.Sliced;
            Radius(c.Fill, 3.4f * Cw);
            c.Edge = Pic(card, "Edge", HomeArt.RoundRing(0.4f / 3.4f), HubLine);
            RingRadius(c.Edge, 3.4f * Cw);
            c.EdgeOn = Pic(card, "EdgeOn", HomeArt.RoundRing(0.6f / 3.4f), UiKit.Primary);
            RingRadius(c.EdgeOn, 3.4f * Cw);

            var box = Pic(card, "Picture", UiKit.Rounded, HubPicBack);
            Radius(box, 2.6f * Cw);
            var br = box.rectTransform;
            br.anchorMin = new Vector2(0, 1);
            br.anchorMax = new Vector2(1, 1);
            br.pivot = new Vector2(0.5f, 1);
            br.offsetMin = new Vector2(1.5f * Cw, 0);
            br.offsetMax = new Vector2(-1.5f * Cw, -1.5f * Cw);
            c.Box = br; // pic * 0.82 tall: FitHeroCards sets it from the cell width
            var clip = Pic(br, "Clip", UiKit.Rounded, Color.white); // the portrait keeps to the rounded box; the name below does not
            Radius(clip, 2.6f * Cw);
            clip.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var area = UiKit.NewRect("Area", clip.rectTransform);
            UiKit.Stretch(area, 0, Cw, 0, 0);
            var pic = UiKit.NewRect("Portrait", area);
            var raw = pic.gameObject.AddComponent<RawImage>();
            raw.texture = Portrait(info.Id);
            raw.enabled = raw.texture != null;
            raw.raycastTarget = false;
            c.Pic = pic;
            c.Aspect = 400f / 308f; // the portraits' own shape (an imported texture may report its power-of-two size)

            var name = UiKit.Label(br, info.Name, Mathf.RoundToInt(3.6f * Cw), TextAnchor.MiddleCenter, UiKit.TextColor);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Mid(name.rectTransform, 0.5f, 0, 0, -3.4f * Cw, 40 * Cw, 5 * Cw);
            RoleTag(br, info.Role, -7.8f * Cw, HubRole, 2.9f, 1.9f);

            // the old "selected" mark: a mint circle with a dark tick, top right
            var check = Pic(card, "Check", UiKit.Circle, UiKit.Primary);
            Mid(check.rectTransform, 1, 1, -3 * Cw, -3 * Cw, 5.2f * Cw, 5.2f * Cw);
            Tick(check.rectTransform, 1.3f * Cw, HubDark);
            c.Check = check.gameObject;
            c.Lock = BuildCardLock(card, out c.LockLevel); // HeroLocks.cs

            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); OpenHero(index); });
            card.gameObject.AddComponent<ButtonFx>();
            heroCards.Add(c);
        }

        // The role's sign and name side by side, centred at y under the parent's bottom edge.
        static void RoleTag(RectTransform parent, string role, float y, Color color, float sizeCw, float iconCw)
        {
            var text = UiKit.Label(parent, role, Mathf.RoundToInt(sizeCw * Cw), TextAnchor.MiddleLeft, color);
            text.font = UiKit.BodyFont;
            text.fontStyle = FontStyle.Bold;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            float rw = text.preferredWidth + 5.6f * Cw;
            var tr = text.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0);
            tr.pivot = new Vector2(0, 0.5f);
            tr.anchoredPosition = new Vector2(-rw / 2 + 4.6f * Cw, y);
            tr.sizeDelta = new Vector2(rw, sizeCw * Cw * 1.5f);
            var icon = UiKit.NewRect("RoleIcon", parent);
            Mid(icon, 0.5f, 0, -rw / 2 + 1.9f * Cw, y, 0, 0);
            RoleIcon(icon, role, iconCw * Cw, color);
        }

        static void RefreshHeroSelect()
        {
            ClampHero();
            int chosen = HeroCatalog.MenuIndexOf((HeroId)BamSettings.LastHero); // LastHero is a HeroId, the cards go in list order (MenuList: with the editor's preview heroes)
            foreach (var c in heroCards)
            {
                int lv = LockLevel(c.Index);
                c.Lock.SetActive(lv > 0);
                if (lv > 0) c.LockLevel.text = "LEVEL " + lv;
                bool on = c.Index == chosen;
                c.Fill.color = on ? HubSelFill : HubPanel;
                c.Edge.enabled = !on;
                c.EdgeOn.enabled = on;
                c.Check.SetActive(on);
            }
        }

        // ---- one hero (PageHero3Fit) ----

        static GameObject heroPage;
        static int heroPeek;
        static RectTransform heroContent, heroStageRt, heroSelectRt;
        static LayoutElement heroStageSize, heroNameSize, heroStatsSize;
        static HeroStage heroStage;
        static Image heroRing, heroRingGlow, heroRingShadow;
        static GameObject heroSelectedTag;
        static Text heroCount, heroName, heroTitle, heroRoleText, heroSelectText;
        static RawImage heroSelectMint;
        static Texture heroSelectMintTex;
        static GameObject heroSelectLock;
        static RectTransform heroRolePill, heroRoleIcon;
        const int StatRows = 7;
        static readonly Image[] heroBars = new Image[StatRows];
        static readonly Text[] heroValues = new Text[StatRows];
        static readonly AbilityCard[] heroAbilities = new AbilityCard[2];
        static float[] heroMax;

        static readonly Image[] heroDots = new Image[3];
        static Text heroDifficulty;

        /// <summary>Full holds the whole text; Open shows it, closed shows one line ending in "more" (full review 2026-11-13 nr 128).</summary>
        sealed class AbilityCard { public RectTransform Rect; public LayoutElement Size; public Image Icon; public Text Kind, Name, Desc; public string Full; public bool Open; }

        static void BuildHeroPage()
        {
            var content = NewPage(null, out heroPage, out var panel);
            tabPanels[3] = panel;
            heroContent = content;
            var list = VList(content, 2.2f * Cw, new RectOffset(0, 0, 0, Mathf.RoundToInt(20 * Cw)));
            list.childControlHeight = true;

            // the stage: dark glass, a mint glow, the ring, the 3D figure; ‹ › browse; SELECTED and i/n on top
            heroStageRt = UiKit.NewRect("Stage", content);
            heroStageSize = heroStageRt.gameObject.AddComponent<LayoutElement>();
            heroStageSize.preferredHeight = 50 * Cw;
            var stageBack = Pic(heroStageRt, "Glass", UiKit.Rounded, HubPanel);
            Radius(stageBack, 4 * Cw);
            stageBack.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var glow = Pic(stageBack.rectTransform, "Glow", HomeArt.StageGlow, Color.white);
            glow.rectTransform.anchorMin = new Vector2(-0.1f, -0.1f);
            glow.rectTransform.anchorMax = new Vector2(1.1f, 1.0f);
            heroRingShadow = Pic(stageBack.rectTransform, "Shadow", HomeArt.RingShadow, Color.white);
            heroRingGlow = Pic(stageBack.rectTransform, "RingGlow", null, new Color(UiKit.Primary.r, UiKit.Primary.g, UiKit.Primary.b, 0.3f));
            heroRing = Pic(stageBack.rectTransform, "Ring", null, UiKit.Primary);
            var pic = UiKit.NewRect("Hero3D", stageBack.rectTransform);
            UiKit.Stretch(pic);
            var picture = pic.gameObject.AddComponent<RawImage>();
            picture.raycastTarget = false;
            picture.enabled = false;

            var hit = UiKit.NewRect("Swing", heroStageRt);
            UiKit.Stretch(hit, 14 * Cw, 0, 14 * Cw, 0);
            hit.gameObject.AddComponent<Image>().color = Color.clear;
            heroStage = hit.gameObject.AddComponent<HeroStage>();
            heroStage.Picture = picture;
            heroStage.Spot = new Vector3(540, 0, 500);
            heroStage.HeroFill = 0.74f;
            heroStage.Sway = 20f;
            heroStage.Tapped = () => heroStage.Swing();

            Arrow(heroStageRt, -1, () => OpenHero((heroPeek + HeroCatalog.MenuList.Length - 1) % HeroCatalog.MenuList.Length));
            Arrow(heroStageRt, 1, () => OpenHero((heroPeek + 1) % HeroCatalog.MenuList.Length));

            var tag = Pic(heroStageRt, "Selected", UiKit.Rounded, new Color(10 / 255f, 40 / 255f, 34 / 255f, 0.85f));
            Radius(tag, 3.2f * Cw);
            TopLeft(tag.rectTransform, 3 * Cw, 3 * Cw, 24 * Cw, 6.4f * Cw);
            var tickBox = UiKit.NewRect("Tick", tag.rectTransform);
            Mid(tickBox, 0, 0.5f, 3.4f * Cw, -0.2f * Cw, 0, 0);
            Tick(tickBox, 1.2f * Cw, UiKit.Primary);
            var selText = Treb(tag.rectTransform, "SELECTED", 2.8f, UiKit.Primary);
            Mid(selText.rectTransform, 0, 0.5f, 6 * Cw, -0.2f * Cw, 20 * Cw, 5 * Cw);
            selText.rectTransform.pivot = new Vector2(0, 0.5f);
            heroSelectedTag = tag.gameObject;
            heroCount = Treb(heroStageRt, "", 2.8f, UiKit.TextDim);
            heroCount.alignment = TextAnchor.MiddleRight;
            Mid(heroCount.rectTransform, 1, 1, -4 * Cw, -6.2f * Cw, 20 * Cw, 5 * Cw);
            heroCount.rectTransform.pivot = new Vector2(1, 0.5f);
            TryButton(heroStageRt);

            // the name and title on the left, the role on the right
            var nameRow = UiKit.NewRect("Name", content);
            heroNameSize = nameRow.gameObject.AddComponent<LayoutElement>();
            heroNameSize.preferredHeight = 12.5f * Cw;
            heroName = UiKit.Label(nameRow, "", Mathf.RoundToInt(6.4f * Cw), TextAnchor.MiddleLeft, Color.white);
            heroName.horizontalOverflow = HorizontalWrapMode.Overflow;
            var nsh = heroName.gameObject.AddComponent<Shadow>();
            nsh.effectColor = new Color32(0x0b, 0x13, 0x30, 0xff);
            nsh.effectDistance = new Vector2(0, -0.5f * Cw);
            TopLeft(heroName.rectTransform, Cw, 3.6f * Cw - 4 * Cw, 60 * Cw, 8 * Cw);
            heroTitle = Treb(nameRow, "", 3, UiKit.TextDim);
            TopLeft(heroTitle.rectTransform, Cw, 8.6f * Cw - 2.5f * Cw, 60 * Cw, 5 * Cw);
            heroRolePill = Pic(nameRow, "Role", UiKit.Rounded, new Color(UiKit.Primary.r, UiKit.Primary.g, UiKit.Primary.b, 0.14f)).rectTransform;
            Radius(heroRolePill.GetComponent<Image>(), 3.2f * Cw);
            heroRolePill.anchorMin = heroRolePill.anchorMax = heroRolePill.pivot = new Vector2(1, 1);
            heroRoleText = Treb(heroRolePill, "", 2.9f, UiKit.Primary);
            heroRoleText.rectTransform.anchorMin = heroRoleText.rectTransform.anchorMax = heroRoleText.rectTransform.pivot = new Vector2(0, 0.5f);
            // Difficulty as three dots under the role, Rickard's own 1..3 from HeroCatalog (full review 2026-11-13 nr 118 + 128);
            // Tyra, the tutorial's hero, says "Good first hero" in place of the word
            for (int i = 0; i < 3; i++)
            {
                heroDots[i] = Pic(nameRow, "Dot", UiKit.Circle, UiKit.Primary);
                var dr = heroDots[i].rectTransform;
                dr.anchorMin = dr.anchorMax = dr.pivot = new Vector2(1, 1);
                dr.sizeDelta = new Vector2(1.6f * Cw, 1.6f * Cw);
                dr.anchoredPosition = new Vector2(-(2 - i) * 2.4f * Cw, -10.1f * Cw);
            }
            heroDifficulty = Treb(nameRow, "", 2.5f, UiKit.TextDim);
            heroDifficulty.alignment = TextAnchor.MiddleRight;
            var hdr = heroDifficulty.rectTransform;
            hdr.anchorMin = hdr.anchorMax = hdr.pivot = new Vector2(1, 1);
            hdr.sizeDelta = new Vector2(30 * Cw, 4 * Cw);
            hdr.anchoredPosition = new Vector2(-3 * 2.4f * Cw - 0.6f * Cw, -8.9f * Cw);

            // what the hero does comes before the numbers: special and passive one line each, tap for the whole text (nr 128)
            for (int k = 0; k < 2; k++) heroAbilities[k] = BuildAbilityCard(content, k == 0);

            // stats: label | bar | value, the bars against the best in the roster
            var stats = UiKit.NewRect("Stats", content);
            heroStatsSize = stats.gameObject.AddComponent<LayoutElement>();
            float rowH = 5.4f * Cw;
            heroStatsSize.preferredHeight = 2.6f * Cw + StatRows * rowH + 1.2f * Cw;
            CardBack(stats, 4 * Cw);
            // "Attack time": 1.85 s is a time, not a speed (2026-10-31). "Damage/s" and "Special" added from the tester's wish for exact
            // numbers (2026-11-02): damage per second of steady attacking, and the hits the special needs with the fastest time to fill it.
            string[] labels = { "Health", "Damage", "Attack time", "Damage/s", "Range", "Move speed", "Special" };
            float bh = 1.5f * Cw;
            for (int i = 0; i < StatRows; i++)
            {
                float cy = 1.3f * Cw + i * rowH + rowH / 2 + 0.6f * Cw;
                var lab = Treb(stats, labels[i], 2.9f, UiKit.TextColor);
                TopLeft(lab.rectTransform, 4 * Cw, cy - 2.5f * Cw, 22 * Cw, 5 * Cw);
                var track = Pic(stats, "Bar", UiKit.Rounded, new Color(1, 1, 1, 0.1f));
                Radius(track, bh / 2);
                var trt = track.rectTransform;
                trt.anchorMin = new Vector2(0, 1);
                trt.anchorMax = new Vector2(1, 1);
                trt.pivot = new Vector2(0, 0.5f);
                trt.offsetMin = new Vector2(26 * Cw, -cy - bh / 2);
                trt.offsetMax = new Vector2(-25 * Cw, -cy + bh / 2);
                var fill = Pic(trt, "Fill", UiKit.Rounded, UiKit.Primary);
                Radius(fill, bh / 2);
                fill.rectTransform.anchorMax = new Vector2(0.5f, 1);
                heroBars[i] = fill;
                var val = Treb(stats, "", 2.8f, UiKit.TextDim);
                val.alignment = TextAnchor.MiddleRight;
                var vr = val.rectTransform;
                vr.anchorMin = vr.anchorMax = vr.pivot = new Vector2(1, 1);
                vr.anchoredPosition = new Vector2(-4 * Cw, -cy + 2.5f * Cw);
                vr.sizeDelta = new Vector2(22 * Cw, 5 * Cw);
                heroValues[i] = val;
            }

            BuildSpellCard(content); // battle spell (MenuSpell.cs), after the stats since the abilities moved up (full review 2026-11-13 nr 128)

            // SELECT: fixed at the bottom of the sheet over a dark fade (StickySelect)
            var fadeRt = UiKit.NewRect("SelectFade", panel);
            float h = 14 * Cw, y = 3.2f * Cw;
            fadeRt.anchorMin = new Vector2(0, 0);
            fadeRt.anchorMax = new Vector2(1, 0);
            fadeRt.pivot = new Vector2(0.5f, 0);
            fadeRt.sizeDelta = new Vector2(0, y + h + 7 * Cw);
            var fade = fadeRt.gameObject.AddComponent<RawImage>();
            fade.texture = HomeArt.Gradient(new Color(16 / 255f, 26 / 255f, 60 / 255f, 0), new Color32(0x10, 0x1a, 0x3c, 0xff), (h - 2 * Cw) / (y + h + 7 * Cw));
            fade.raycastTarget = false;

            heroSelectRt = UiKit.NewRect("Select", panel);
            heroSelectRt.anchorMin = new Vector2(0, 0);
            heroSelectRt.anchorMax = new Vector2(1, 0);
            heroSelectRt.pivot = new Vector2(0.5f, 0);
            heroSelectRt.offsetMin = new Vector2(PageX, y);
            heroSelectRt.offsetMax = new Vector2(-PageX, y + h);
            var drop = Pic(heroSelectRt, "Shadow", UiKit.Rounded, new Color(0, 0, 0, 0.45f));
            Radius(drop, 4 * Cw);
            UiKit.Stretch(drop.rectTransform, 0, Cw, 0, -Cw);
            var face = Pic(heroSelectRt, "Face", UiKit.Rounded, Color.white);
            Radius(face, 4 * Cw);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var mint = UiKit.NewRect("Mint", face.rectTransform);
            UiKit.Stretch(mint);
            var mi = mint.gameObject.AddComponent<RawImage>();
            mi.texture = HomeArt.Gradient(new Color32(0x43, 0xf0, 0xbd, 0xff), new Color32(0x22, 0xb8, 0x8c, 0xff));
            mi.raycastTarget = false;
            heroSelectMint = mi; heroSelectMintTex = mi.texture;
            var sheen = Pic(face.rectTransform, "Sheen", UiKit.Rounded, new Color(1, 1, 1, 0.18f));
            Radius(sheen, 3.4f * Cw);
            TopLeft(sheen.rectTransform, 0.7f * Cw, 0.7f * Cw, 1080, h * 0.42f);
            sheen.rectTransform.anchorMax = new Vector2(1, 1);
            sheen.rectTransform.offsetMax = new Vector2(-0.7f * Cw, -0.7f * Cw);
            heroSelectText = UiKit.Label(heroSelectRt, "", Mathf.RoundToInt(5.2f * Cw), TextAnchor.MiddleCenter, HubDark);
            UiKit.Stretch(heroSelectText.rectTransform);
            heroSelectText.horizontalOverflow = HorizontalWrapMode.Overflow;
            var selLock = Padlock(heroSelectRt, 1.8f * Cw, UiKit.TextDim);
            selLock.anchorMin = selLock.anchorMax = new Vector2(0.5f, 0.5f);
            selLock.anchoredPosition = new Vector2(-25 * Cw, 0);
            heroSelectLock = selLock.gameObject;
            var hitImg = heroSelectRt.gameObject.AddComponent<Image>();
            hitImg.color = Color.clear;
            var sb = heroSelectRt.gameObject.AddComponent<Button>();
            sb.transition = Selectable.Transition.None;
            sb.navigation = new Navigation { mode = Navigation.Mode.None };
            sb.onClick.AddListener(() =>
            {
                if (LockLevel(heroPeek) > 0) { BamAudio.Play(SfxId.UiBack); return; } // locked: the button says the level
                BamAudio.Play(SfxId.UiConfirm);
                BamSettings.LastHero = (int)HeroCatalog.MenuList[heroPeek].Id; // a preview hero too (editor only, Rickard 2026-11-10)
                ShowTab(0);
            });
            heroSelectRt.gameObject.AddComponent<ButtonFx>().PressScale = false;
            heroSelectRt.gameObject.AddComponent<HomePress>();
        }

        // TRY (BAM's own, Rickard: HERO TEST reachable from the hero pick): a dark glass pill with a mint edge at the stage's
        // bottom right, the hero test's target sign and TRY; it starts HERO TEST with the hero on the page at level 1
        static void TryButton(RectTransform stage)
        {
            float h = 8 * Cw, w = 21 * Cw;
            var rt = UiKit.NewRect("Try", stage);
            Mid(rt, 1, 0, -3 * Cw - w / 2, 3 * Cw + h / 2, w, h);
            var back = Pic(rt, "Back", UiKit.Rounded, new Color(14 / 255f, 18 / 255f, 48 / 255f, 0.85f));
            UiKit.Stretch(back.rectTransform);
            Radius(back, h / 2);
            back.raycastTarget = true;
            var edge = Pic(rt, "Edge", HomeArt.RoundRing(0.35f * Cw / (h / 2)), UiKit.Primary);
            UiKit.Stretch(edge.rectTransform);
            RingRadius(edge, h / 2);
            float k = 1.5f * Cw, lw = 0.22f * k;
            for (int i = 0; i < 2; i++)
            {
                float r = i == 0 ? k : 0.55f * k, outer = r + lw / 2;
                Mid(Pic(rt, "Ring", HomeArt.CircleRing(lw / outer), UiKit.Primary).rectTransform, 0, 0.5f, h / 2 + 0.5f * Cw, 0, 2 * outer, 2 * outer);
            }
            Mid(Pic(rt, "Dot", UiKit.Circle, UiKit.Primary).rectTransform, 0, 0.5f, h / 2 + 0.5f * Cw, 0, 0.4f * k, 0.4f * k);
            var label = UiKit.Label(rt, "TRY", Mathf.RoundToInt(3.8f * Cw), TextAnchor.MiddleCenter, HubText);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Mid(label.rectTransform, 0.5f, 0.5f, 2.2f * Cw, 0, w - h, h);
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = back;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiConfirm); StartHeroTest(HeroCatalog.MenuList[heroPeek].Id); });
            rt.gameObject.AddComponent<ButtonFx>();
        }

        // PageHeroes3: the picture box is pic wide and pic * 0.82 tall (pic = the card less cw(1.5) each side), the portrait
        // contained in it below a cw(1) margin, whole and undistorted. Set here, not by AspectRatioFitters inside the grid.
        internal static void FitHeroCards(float cell)
        {
            float pic = cell - 3 * Cw, ph = pic * 0.82f, ah = ph - Cw;
            foreach (var c in heroCards)
            {
                if (c.Box == null) continue;
                c.Box.offsetMin = new Vector2(1.5f * Cw, -1.5f * Cw - ph);
                c.Box.offsetMax = new Vector2(-1.5f * Cw, -1.5f * Cw);
                float w = pic, h = pic / c.Aspect;
                if (h > ah) { h = ah; w = ah * c.Aspect; }
                c.Pic.anchorMin = c.Pic.anchorMax = c.Pic.pivot = new Vector2(0.5f, 0.5f);
                c.Pic.anchoredPosition = Vector2.zero;
                c.Pic.sizeDelta = new Vector2(w, h);
            }
        }

        // Special or passive: the icon on a dark rounded square, KIND · note, the name, the text under it
        static AbilityCard BuildAbilityCard(RectTransform content, bool special)
        {
            var a = new AbilityCard();
            a.Rect = UiKit.NewRect(special ? "Special" : "Passive", content);
            a.Size = a.Rect.gameObject.AddComponent<LayoutElement>();
            CardBack(a.Rect, 4 * Cw);
            float ic = 10.5f * Cw, tx = 3 * Cw + ic + 2.5f * Cw;
            var box = Pic(a.Rect, "IconBox", UiKit.Rounded, HubPicBack);
            Radius(box, 2.6f * Cw);
            TopLeft(box.rectTransform, 3 * Cw, 2 * Cw, ic, ic);
            a.Icon = Pic(box.rectTransform, "Icon", null, Color.white);
            UiKit.Stretch(a.Icon.rectTransform, ic * 0.12f, ic * 0.12f, ic * 0.12f, ic * 0.12f);
            a.Icon.preserveAspect = true;
            a.Kind = Treb(a.Rect, "", 2.5f, special ? new Color32(0xff, 0xb4, 0x3a, 0xff) : (Color32)UiKit.TextDim);
            Stretched(a.Kind.rectTransform, tx, 3.3f * Cw - 2.5f * Cw, 3 * Cw, 5 * Cw);
            a.Name = UiKit.Label(a.Rect, "", Mathf.RoundToInt(3.9f * Cw), TextAnchor.MiddleLeft, UiKit.TextColor);
            a.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretched(a.Name.rectTransform, tx, 7.2f * Cw - 3 * Cw, 3 * Cw, 6 * Cw);
            a.Desc = UiKit.Label(a.Rect, "", Mathf.RoundToInt(2.9f * Cw), TextAnchor.UpperLeft, HubBody);
            a.Desc.font = UiKit.BodyFont;
            a.Desc.lineSpacing = 1.1f;
            Stretched(a.Desc.rectTransform, tx, 11.4f * Cw - 2 * Cw, 3 * Cw, 40 * Cw);
            a.Desc.supportRichText = true;
            var b = a.Rect.gameObject.AddComponent<Button>(); // the card is the button: "more" opens the whole text, a second tap closes it
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() =>
            {
                BamAudio.Play(SfxId.UiTap);
                a.Open = !a.Open;
                ShowAbilityText(a);
                LayoutRebuilder.ForceRebuildLayoutImmediate(heroContent);
            });
            return a;
        }

        static void OpenHero(int index)
        {
            heroPeek = Mathf.Clamp(index, 0, HeroCatalog.MenuList.Length - 1);
            if (stack.Count == 0 || stack[stack.Count - 1] != heroPage) Push(heroPage);
            else RefreshHeroPage();
        }

        static void RefreshHeroPage()
        {
            var info = HeroCatalog.MenuList[heroPeek];
            var def = Defs.Hero(info.Id);
            bool chosen = BamSettings.LastHero == (int)info.Id;

            if (heroMax == null)
            {   // the bars against the best of the heroes in the game (HeroCatalog.All), not Hilde and Oaken who wait
                heroMax = new float[StatRows];
                foreach (var hi in HeroCatalog.All)
                {
                    var d = Defs.Hero(hi.Id);
                    float[] dv = StatBarValues(d);
                    for (int i = 0; i < StatRows; i++) heroMax[i] = Mathf.Max(heroMax[i], dv[i]);
                }
            }
            float[] v = StatBarValues(def);
            // "+26/lvl" and the units (2026-10-31, Rickard's screenshot review: "(+26)" and bare "4" / "2.19" said nothing)
            string[] txt =
            {
                Num(def.Hp) + "  +" + Num(def.HpGrowth) + "/lvl", Num(def.Dmg) + "  +" + Num(def.DmgGrowth) + "/lvl", Num(def.Cd) + " s",
                Num(Round1(def.Dmg / Mathf.Max(0.1f, def.Cd))), Num(def.Range) + " tiles", Num(def.Speed) + " tiles/s",
                SpecialHits(info.Id, def) + " hits · " + Num(Round1(FirstCharge(info.Id, def) * def.Cd)) + " s",
            };
            for (int i = 0; i < StatRows; i++)
            {
                heroBars[i].rectTransform.anchorMax = new Vector2(Mathf.Clamp01(v[i] / Mathf.Max(0.001f, heroMax[i])), 1);
                heroValues[i].text = txt[i];
            }

            heroName.text = info.Name;
            heroTitle.text = info.Title;
            heroRoleText.text = info.Role.ToUpperInvariant();
            float rw = heroRoleText.preferredWidth + 9 * Cw;
            heroRolePill.anchoredPosition = new Vector2(0, -2 * Cw);
            heroRolePill.sizeDelta = new Vector2(rw, 6.4f * Cw);
            heroRoleText.rectTransform.anchoredPosition = new Vector2(6 * Cw, 0);
            heroRoleText.rectTransform.sizeDelta = new Vector2(rw, 5 * Cw);
            if (heroRoleIcon != null) Object.Destroy(heroRoleIcon.gameObject);
            heroRoleIcon = UiKit.NewRect("RoleIcon", heroRolePill);
            Mid(heroRoleIcon, 0, 0.5f, 3.2f * Cw, 0, 0, 0);
            RoleIcon(heroRoleIcon, info.Role, 1.5f * Cw, UiKit.Primary);
            bool firstHero = info.Id == HeroId.Tyra;
            heroDifficulty.text = firstHero ? "GOOD FIRST HERO" : "DIFFICULTY";
            heroDifficulty.color = firstHero ? UiKit.Primary : UiKit.TextDim;
            int diff = Mathf.Clamp(info.Difficulty, 1, 3);
            for (int i = 0; i < 3; i++) heroDots[i].color = i < diff ? (Color)UiKit.Primary : new Color(1, 1, 1, 0.16f);

            string lower = Lower(info.Id);
            RefreshSpellCard();
            FillAbility(heroAbilities[0], "SPECIAL · " + SpecialHits(info.Id, def) + " hits to charge", info.SpecialTitle, info.SpecialText, HeroCatalog.SpecialIcon(info.Id));
            FillAbility(heroAbilities[1], "PASSIVE", info.PassiveName, info.PassiveDesc, "passive_" + lower);

            heroSelectedTag.SetActive(chosen);
            heroCount.text = (heroPeek + 1) + "/" + HeroCatalog.MenuList.Length;
            heroSelectRt.gameObject.SetActive(!chosen); // already chosen: SELECTED on the stage says it; the editor's preview heroes can be selected too (Rickard 2026-11-10)
            SelectLocked(LockLevel(heroPeek), info.Name);

            // the stage takes the height that is left, so everything fits without scrolling on a phone
            LayoutRebuilder.ForceRebuildLayoutImmediate(heroContent);
            float view = ((RectTransform)heroContent.parent).rect.height;
            float below = heroNameSize.preferredHeight + heroStatsSize.preferredHeight + heroAbilities[0].Size.preferredHeight
                          + heroAbilities[1].Size.preferredHeight + 3 * 2.2f * Cw;
            float stH = Mathf.Clamp(view - 19 * Cw - below - 2.2f * Cw, 40 * Cw, 66 * Cw);
            heroStageSize.preferredHeight = stH;
            LayoutRebuilder.ForceRebuildLayoutImmediate(heroContent);

            float feet = 6 * Cw, figH = stH * 0.74f, rx = figH * 0.3f, ry = figH * 0.075f;
            EllipseAt(heroRingShadow, null, rx * 1.1f, ry * 1.4f, feet);
            EllipseAt(heroRingGlow, HomeArt.EllipseRing(1.8f * Cw / rx, out float f1), rx, f1, feet);
            EllipseAt(heroRing, HomeArt.EllipseRing((chosen ? 0.65f : 0.45f) * Cw / rx, out float f2), rx, f2, feet);
            heroRingGlow.enabled = chosen;
            heroStage.FeetAt = feet / stH;
            heroStage.Show(info.Id);
            heroStage.Frame();
            var scroll = heroContent.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1;
        }

        static void FillAbility(AbilityCard a, string kind, string name, string desc, string icon)
        {
            a.Kind.text = kind;
            a.Name.text = name;
            a.Full = desc ?? "";
            a.Open = false; // every hero opens on the short lines
            a.Icon.sprite = IconSprite(icon);
            a.Icon.enabled = a.Icon.sprite != null;
            ShowAbilityText(a);
        }

        const string MoreTag = "<color=#3ee6b0>more</color>";

        // Closed: the first sentence cut to one line, then "… more" (or the whole text when it fits on one line). Open: the whole text.
        static void ShowAbilityText(AbilityCard a)
        {
            float w = Mathf.Max(100, heroContent.rect.width - (3 * Cw + 10.5f * Cw + 2.5f * Cw) - 3 * Cw);
            var wrap = a.Desc.GetGenerationSettings(new Vector2(w, 0));
            var line = wrap;
            line.horizontalOverflow = HorizontalWrapMode.Overflow;
            line.richText = false;
            var gen = a.Desc.cachedTextGeneratorForLayout;
            float px = a.Desc.pixelsPerUnit;
            string full = a.Full;
            bool fits = gen.GetPreferredWidth(full, line) / px <= w;
            string shown = full;
            if (!a.Open && !fits)
            {
                string cut = full;
                int dot = cut.IndexOf(". ", System.StringComparison.Ordinal);
                if (dot > 0) cut = cut.Substring(0, dot + 1);
                const string tail = "… more";
                while (cut.Length > 0 && gen.GetPreferredWidth(cut + tail, line) / px > w)
                {
                    int sp = cut.LastIndexOf(' ');
                    cut = sp > 0 ? cut.Substring(0, sp) : "";
                }
                cut = cut.TrimEnd(' ', ',', ':', ';');
                shown = (cut.EndsWith(".") ? cut + " " : cut + "… ") + MoreTag;
            }
            a.Desc.text = shown;
            float dh = gen.GetPreferredHeight(shown, wrap) / px;
            a.Size.preferredHeight = Mathf.Max(10.5f * Cw + 4 * Cw, 10.2f * Cw + dh + 1.6f * Cw);
        }

        // Kugg anchors from KuggS.MinShots loaded shots up to his full MaxCharge, so his page says "2–6" (full review 2026-11-13 nr 128)
        static string SpecialHits(HeroId id, HeroDef d) => id == HeroId.Kugg ? KuggS.MinShots + "–" + d.MaxCharge : d.MaxCharge.ToString();

        // The hits before the special can first be used (Unit.SpecialReady): Kugg's MinShots, everyone else's MaxCharge
        static int FirstCharge(HeroId id, HeroDef d) => id == HeroId.Kugg ? KuggS.MinShots : d.MaxCharge;

        // What each stat bar measures, longer = better: faster attacks and a quicker special give longer bars.
        // The special fills one charge per hit (Combat.OnHeroHit), so hits x attack time is the fastest it can be ready.
        static float[] StatBarValues(HeroDef d)
        {
            float cd = Mathf.Max(0.1f, d.Cd);
            return new[] { d.Hp, d.Dmg, 1 / cd, d.Dmg / cd, d.Range, d.Speed, 1 / Mathf.Max(0.1f, d.MaxCharge * cd) };
        }

        static float Round1(float x) => Mathf.Round(x * 10) / 10;

        static string Num(float x) => x.ToString(Mathf.Approximately(x, Mathf.Round(x)) ? "0" : "0.##", System.Globalization.CultureInfo.InvariantCulture);

        // An ellipse on the stage centred feet up from its bottom: a soft shadow (sprite null keeps RingShadow) or a ring.
        static void EllipseAt(Image img, Sprite ring, float rx, float ryOrScale, float feet)
        {
            var r = img.rectTransform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0);
            if (ring == null)
            {
                r.sizeDelta = new Vector2(2 * rx, 2 * ryOrScale);
                r.anchoredPosition = new Vector2(0, feet - ryOrScale);
                return;
            }
            img.sprite = ring;
            float w = 2 * rx * ryOrScale, h = w * ring.rect.height / ring.rect.width;
            r.sizeDelta = new Vector2(w, h);
            r.anchoredPosition = new Vector2(0, feet - h / 2);
        }

        // HubArrow: a faint circle with a white triangle, cw(11), on the stage's side
        static void Arrow(RectTransform stage, int dir, System.Action go)
        {
            float s = 11 * Cw, t = s / 2 * 0.38f;
            var rt = UiKit.NewRect(dir < 0 ? "Prev" : "Next", stage);
            Mid(rt, dir < 0 ? 0 : 1, 0.5f, dir < 0 ? 2 * Cw + s / 2 : -2 * Cw - s / 2, 0, 9 * Cw, 11 * Cw);
            var circle = Pic(rt, "Circle", UiKit.Circle, new Color(1, 1, 1, 0.08f));
            Mid(circle.rectTransform, 0.5f, 0.5f, 0, 0, s, s);
            var tri = Pic(rt, "Triangle", HomeArt.Roof, Color.white);
            Mid(tri.rectTransform, 0.5f, 0.5f, dir * 0.15f * t, 0, 2 * t, 1.7f * t);
            tri.rectTransform.localRotation = Quaternion.Euler(0, 0, -90 * dir);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); go(); });
            rt.gameObject.AddComponent<ButtonFx>().PressScale = false;
        }

        /// <summary>HubSwitch: a pill track (mint when on), a knob with a soft shadow that slides over. The row is the button.</summary>
        sealed class HubSwitch : MonoBehaviour
        {
            Image track, edge, knob;
            RectTransform knobRt, shadowRt;
            System.Action<bool> changed;
            float k, target;
            public bool On { get; private set; }
            const float Tw = 12.5f * Cw, Th = 7 * Cw;

            public void Build(RectTransform row, System.Action<bool> onChange)
            {
                changed = onChange;
                var box = UiKit.NewRect("Switch", row);
                Mid(box, 1, 0.5f, -3.5f * Cw - Tw / 2, 0, Tw, Th);
                track = Pic(box, "Track", UiKit.Rounded, Color.white);
                Radius(track, Th / 2);
                edge = Pic(box, "Edge", HomeArt.RoundRing(0.35f / 3.5f), new Color(154 / 255f, 162 / 255f, 214 / 255f, 0.45f));
                RingRadius(edge, Th / 2);
                float r = Th / 2 - 0.8f * Cw;
                shadowRt = Pic(box, "Shadow", UiKit.Circle, new Color(0, 0, 0, 0.25f)).rectTransform;
                knob = Pic(box, "Knob", UiKit.Circle, Color.white);
                knobRt = knob.rectTransform;
                Mid(shadowRt, 0, 0.5f, 0, -0.3f * Cw, 2 * r, 2 * r);
                Mid(knobRt, 0, 0.5f, 0, 0, 2 * r, 2 * r);
                var b = gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiToggle); Set(!On, false); changed?.Invoke(On); });
                gameObject.AddComponent<ButtonFx>().PressScale = false;
            }

            public void Set(bool on, bool instant)
            {
                On = on;
                target = on ? 1 : 0;
                if (instant) k = target;
                track.color = on ? new Color(UiKit.Primary.r, UiKit.Primary.g, UiKit.Primary.b, 0.85f) : new Color(1, 1, 1, 0.14f);
                edge.enabled = !on;
                knob.color = on ? Color.white : (Color)new Color32(0xc3, 0xc8, 0xf0, 0xff);
                Place();
            }

            void Update()
            {
                if (Mathf.Approximately(k, target)) return;
                k += (target - k) * (1 - Mathf.Exp(-18 * Time.unscaledDeltaTime));
                if (Mathf.Abs(k - target) < 0.002f) k = target;
                Place();
            }

            void Place()
            {
                float x = Th / 2 + (Tw - Th) * k;
                knobRt.anchoredPosition = new Vector2(x, 0);
                shadowRt.anchoredPosition = new Vector2(x, -0.3f * Cw);
            }
        }

        /// <summary>HubSegments: short choices side by side in a dark box on the right of the row; the chosen one mint.</summary>
        sealed class HubChoice
        {
            readonly Image[] fills;
            readonly Text[] labels;
            readonly bool[] dims;
            // the chosen one filled mint with dark letters everywhere (Rickard 2026-10-31 for the mode sheet; full review
            // 2026-11-13 nr 132: Settings too, where the chosen one was a framed dark box beside filled grey ones and read backwards)

            // full: the segments fill the row (the mode sheet's BOT DIFFICULTY); else they sit on the right of a settings row
            public HubChoice(RectTransform row, string[] names, System.Action<int> chosen, bool full = false)
            {
                var box = Pic(row, "Choices", UiKit.Rounded, new Color(8 / 255f, 10 / 255f, 30 / 255f, 0.55f));
                Radius(box, 2.6f * Cw);
                if (full) UiKit.Stretch(box.rectTransform);
                else
                {
                    box.rectTransform.anchorMin = new Vector2(0.58f, 0.5f);
                    box.rectTransform.anchorMax = new Vector2(1, 0.5f);
                    box.rectTransform.pivot = new Vector2(1, 0.5f);
                    box.rectTransform.offsetMin = new Vector2(0, -5.5f * Cw);
                    box.rectTransform.offsetMax = new Vector2(-2.5f * Cw, 5.5f * Cw);
                }
                int n = names.Length;
                fills = new Image[n]; labels = new Text[n]; dims = new bool[n];
                for (int i = 0; i < n; i++)
                {
                    int index = i;
                    var cell = UiKit.NewRect(names[i], box.rectTransform);
                    cell.anchorMin = new Vector2((float)i / n, 0);
                    cell.anchorMax = new Vector2((float)(i + 1) / n, 1);
                    cell.offsetMin = new Vector2(i == 0 ? Cw : Cw / 2, Cw);
                    cell.offsetMax = new Vector2(i == n - 1 ? -Cw : -Cw / 2, -Cw);
                    fills[i] = cell.gameObject.AddComponent<Image>();
                    fills[i].sprite = UiKit.Rounded;
                    fills[i].type = Image.Type.Sliced;
                    Radius(fills[i], 2 * Cw);
                    labels[i] = UiKit.Label(cell, names[i], Mathf.RoundToInt(3.6f * Cw), TextAnchor.MiddleCenter, UiKit.TextDim);
                    UiKit.Stretch(labels[i].rectTransform);
                    var b = cell.gameObject.AddComponent<Button>();
                    b.transition = Selectable.Transition.None;
                    b.navigation = new Navigation { mode = Navigation.Mode.None };
                    b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiToggle); Set(index); chosen?.Invoke(index); });
                    cell.gameObject.AddComponent<ButtonFx>().PressScale = false;
                }
            }

            public void Set(int sel)
            {
                for (int i = 0; i < fills.Length; i++)
                {
                    bool on = i == sel;
                    fills[i].color = on ? UiKit.Primary : new Color(1, 1, 1, 0.04f);
                    labels[i].color = on ? (Color)new Color32(0x06, 0x28, 0x1d, 0xff) : dims[i] ? new Color(UiKit.TextDim.r, UiKit.TextDim.g, UiKit.TextDim.b, UiKit.TextDim.a * 0.35f) : UiKit.TextDim;
                }
            }

            /// <summary>A locked choice: its label is faded (Set still draws it).</summary>
            public void Dim(int i, bool dim) => dims[i] = dim;

            /// <summary>One segment and its label, for what a sheet adds on top (the mode sheet's padlock and "2/3", nr 130).</summary>
            public RectTransform CellAt(int i) => (RectTransform)fills[i].transform;
            public Text LabelAt(int i) => labels[i];
        }

        // ---- shared pieces ----

        // HubCardBack: the glass card with the gold edge
        static void CardBack(RectTransform rt, float r)
        {
            var back = Pic(rt, "Card", UiKit.Rounded, HubPanel);
            Radius(back, r);
            back.transform.SetAsFirstSibling();
            var edge = Pic(rt, "Edge", HomeArt.RoundRing(0.4f * Cw / r), HubLine);
            RingRadius(edge, r);
            edge.transform.SetSiblingIndex(1);
        }

        // HubCheck: a tick of two strokes, s from the middle
        static void Tick(RectTransform parent, float s, Color color)
        {
            var a = Pic(parent, "Tick", null, color);
            Mid(a.rectTransform, 0.5f, 0.5f, -0.65f * s, -0.35f * s, 0.99f * s + 0.45f * s * 0.5f, 0.45f * s);
            a.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var b = Pic(parent, "Tick", null, color);
            Mid(b.rectTransform, 0.5f, 0.5f, 0.35f * s, 0, 1.91f * s + 0.45f * s * 0.5f, 0.45f * s);
            b.rectTransform.localRotation = Quaternion.Euler(0, 0, 47);
        }

        // HubRoleIcon: a shield (Tank), crossed swords (Fighter), a dagger (Assassin), a star (Mage), a target (Marksman), a plus (Support)
        static void RoleIcon(RectTransform box, string role, float s, Color col)
        {
            switch (role)
            {
                case "Marksman":
                    Mid(Pic(box, "Ring", HomeArt.CircleRing(0.3f), col).rectTransform, 0.5f, 0.5f, 0, 0, 2.3f * s, 2.3f * s);
                    Mid(Pic(box, "Dot", UiKit.Circle, col).rectTransform, 0.5f, 0.5f, 0, 0, 0.7f * s, 0.7f * s);
                    break;
                case "Mage":
                    Mid(Pic(box, "Star", HomeArt.Diamond, col).rectTransform, 0.5f, 0.5f, 0, 0, 0.8f * s, 2.2f * s);
                    Mid(Pic(box, "Star", HomeArt.Diamond, col).rectTransform, 0.5f, 0.5f, 0, 0, 2.2f * s, 0.8f * s);
                    break;
                case "Tank":
                    Mid(Pic(box, "Shield", HomeArt.Shield, col).rectTransform, 0.5f, 0.5f, 0, -0.1f * s, 1.7f * s, 2f * s);
                    break;
                case "Fighter":
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var sword = UiKit.NewRect("Sword", box);
                        Mid(sword, 0.5f, 0.5f, 0, 0, 0, 0);
                        sword.localRotation = Quaternion.Euler(0, 0, -45 * k);
                        Mid(Pic(sword, "Blade", null, col).rectTransform, 0.5f, 0.5f, 0, 0.35f * s, 0.28f * s, 1.6f * s);
                        Mid(Pic(sword, "Guard", null, col).rectTransform, 0.5f, 0.5f, 0, -0.51f * s, s, 0.22f * s);
                    }
                    break;
                case "Assassin":
                    var dagger = UiKit.NewRect("Dagger", box);
                    Mid(dagger, 0.5f, 0.5f, 0, 0, 0, 0);
                    dagger.localRotation = Quaternion.Euler(0, 0, -45);
                    Mid(Pic(dagger, "Blade", HomeArt.Roof, col).rectTransform, 0.5f, 0.5f, 0, 0.45f * s, 0.44f * s, 1.6f * s);
                    Mid(Pic(dagger, "Guard", null, col).rectTransform, 0.5f, 0.5f, 0, -0.46f * s, 1.1f * s, 0.22f * s);
                    Mid(Pic(dagger, "Grip", null, col).rectTransform, 0.5f, 0.5f, 0, -0.85f * s, 0.24f * s, 0.55f * s);
                    break;
                default: // Support
                    Mid(Pic(box, "Plus", null, col).rectTransform, 0.5f, 0.5f, 0, 0, 0.6f * s, 2 * s);
                    Mid(Pic(box, "Plus", null, col).rectTransform, 0.5f, 0.5f, 0, 0, 2 * s, 0.6f * s);
                    break;
            }
        }

        static Text Treb(Transform parent, string text, float sizeCw, Color color)
        {
            var t = UiKit.Label(parent, text, Mathf.RoundToInt(sizeCw * Cw), TextAnchor.MiddleLeft, color);
            t.font = UiKit.BodyFont;
            t.fontStyle = FontStyle.Bold;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        static Image Pic(Transform parent, string name, Sprite sprite, Color color)
        {
            var img = UiKit.Fill(parent, color, sprite);
            img.name = name;
            return img;
        }

        // UiKit.Rounded has 28 px corners at 100 px per unit: this gives corners of radius r.
        static void Radius(Image img, float r) => img.pixelsPerUnitMultiplier = 28f / Mathf.Max(1f, r);
        static void RingRadius(Image img, float r) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = HomeArt.RingRadius / Mathf.Max(1f, r); }

        // Top-left corner at (x, y), y measured down from the parent's top.
        static RectTransform TopLeft(RectTransform r, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        // Centred on (x, y) from the anchor point (ax, ay) of the parent, y up.
        static RectTransform Mid(RectTransform r, float ax, float ay, float x, float y, float w, float h)
        {
            r.anchorMin = r.anchorMax = new Vector2(ax, ay);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
            return r;
        }

        // Full width minus left/right, top at y down from the parent's top, h tall.
        static void Stretched(RectTransform r, float left, float y, float right, float h)
        {
            r.anchorMin = new Vector2(0, 1);
            r.anchorMax = new Vector2(1, 1);
            r.pivot = new Vector2(0.5f, 1);
            r.offsetMin = new Vector2(left, -y - h);
            r.offsetMax = new Vector2(-right, -y);
        }
    }

    /// <summary>Three cards a row (PageHeroes3): the cell width from the grid's width, the height the picture plus cw(13).</summary>
    sealed class HeroGridFit : MonoBehaviour
    {
        public GridLayoutGroup Grid;
        float lastWidth = -1;

        void OnRectTransformDimensionsChange() => Fit();
        void OnEnable() { lastWidth = -1; Fit(); } // again on every show: the cards are built after the grid

        void Fit()
        {
            if (Grid == null) return;
            float w = ((RectTransform)transform).rect.width;
            if (w <= 0 || Mathf.Abs(w - lastWidth) < 0.5f) return;
            lastWidth = w;
            float cell = (w - Grid.spacing.x * 2) / 3;
            Grid.cellSize = new Vector2(cell, cell - 3 * 10.8f + 13 * 10.8f);
            Screens.FitHeroCards(cell);
        }
    }

    static partial class HomeArt
    {
        static Sprite pageGlow, stageGlow, shield;
        static readonly Dictionary<long, Texture2D> gradients = new Dictionary<long, Texture2D>();

        /// <summary>A vertical gradient, top colour to bottom colour; with solidFrom the bottom part that long (0..1) is solid.</summary>
        public static Texture2D Gradient(Color top, Color bottom, float solidFrom = 0)
        {
            long key = ((long)((Color32)top).GetHashCode() << 32) ^ (uint)((Color32)bottom).GetHashCode() ^ (long)(solidFrom * 1000) << 16;
            if (gradients.TryGetValue(key, out var tex)) return tex;
            const int N = 128;
            tex = new Texture2D(1, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAM.UI.Gradient" };
            for (int y = 0; y < N; y++)
            {
                float v = y / (N - 1f); // 0 at the bottom
                float k = solidFrom > 0 ? Mathf.Clamp01((1 - v) / (1 - solidFrom)) : 1 - v;
                tex.SetPixel(0, y, Color.Lerp(top, bottom, v <= solidFrom ? 1 : k));
            }
            tex.Apply(false, true);
            gradients[key] = tex;
            return tex;
        }

        // the pages' mint glow (DrawHub): rgba(62,230,176,0.16) in the middle, rgba(62,120,230,0.06) at 60 %, clear at the edge
        public static Sprite PageGlow => pageGlow != null ? pageGlow : pageGlow = Radial("PageGlow",
            (0f, new Color(0.24f, 0.9f, 0.69f, 0.16f)), (0.6f, new Color(0.24f, 0.47f, 0.9f, 0.06f)), (1f, new Color(0.24f, 0.47f, 0.9f, 0f)));
        public static Sprite StageGlow => stageGlow != null ? stageGlow : stageGlow = PageGlow;

        // a shield: straight top and sides, curving down to a point
        public static Sprite Shield => shield != null ? shield : shield = Make("Shield", 68, 80, (x, y) =>
        {
            float u = (x - 34) / 34f, v = 1 - y / 80f; // v 0 at the top
            float a;
            if (v < 0.45f) a = Mathf.Clamp01((1 - Mathf.Abs(u)) * 34 + 0.5f);
            else { float k = (v - 0.45f) / 0.55f, half = 1 - k * k; a = Mathf.Clamp01((half - Mathf.Abs(u)) * 34 + 0.5f); }
            return White(a * Mathf.Clamp01(y + 0.5f) * Mathf.Clamp01(80 - y + 0.5f));
        });
    }
}
