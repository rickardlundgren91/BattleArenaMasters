using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // Home, the Modes sheet and the Heroes page, laid out like the old game's (ABC2 Hub v3, Rickard's screenshots 2026-10-25):
    // Home shows the chosen hero on the platform with MODES and a big orange PLAY at the bottom; Heroes is a grid of cards.
    // Online, shop, rank, currencies and friends are left out. PLAY starts a match with the chosen hero and bot difficulty.
    public static partial class Screens
    {
        static GameObject mainMenu, heroSelect, modes;
        static bool tutorialOffered; // once per app run, so Play is never nagging

        static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        static readonly string[] StatNames = { "Health", "Damage", "Speed", "Difficulty" };

        // ---- Home ----

        static RectTransform homePortrait, homeHero;
        static Text homeSelected, playSub;

        static void BuildMainMenu()
        {
            var body = NewScreen("Home", false, out mainMenu);

            // The platform the hero stands on: dark stone, a mint ring under the feet and a faint gold rune ring round it.
            float platY = NavH + 24 + 200 + 260;
            var glow = UiKit.Fill(body, new Color(1f, 0.88f, 0.63f, 0.07f), UiKit.Circle);
            UiKit.Place(glow, 0.5f, 0, 0, platY - 120, 760, 760);
            var stone = UiKit.Fill(body, new Color(0.07f, 0.08f, 0.17f, 0.95f), UiKit.Circle);
            UiKit.Place(stone, 0.5f, 0, 0, platY - 110, 820, 220);
            var gold = UiKit.Fill(body, new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.55f), UiKit.CircleRing);
            UiKit.Place(gold, 0.5f, 0, 0, platY - 95, 720, 190);
            var mint = UiKit.Fill(body, UiKit.Primary, UiKit.CircleRing);
            UiKit.Place(mint, 0.5f, 0, 0, platY - 62, 470, 124);

            // The hero: a big badge for now (a 3D model later).
            homeHero = UiKit.HeroBadge(body, HeroId.Bram);
            UiKit.Place(homeHero, 0.5f, 0, 0, platY - 20, 440, 440);

            // Top bar: portrait, the game's name.
            var top = UiKit.Panel(body, new Color(0.04f, 0.05f, 0.13f, 0.92f));
            UiKit.Band(top, true, 16, 130, 16);
            UiKit.Frame(top.rectTransform).color = new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.6f);
            homePortrait = UiKit.HeroBadge(top.transform, HeroId.Bram);
            UiKit.Place(homePortrait, 0, 0.5f, 14, 0, 104, 104);
            var name = UiKit.Label(top.transform, BamInfo.ShortName, 52, TextAnchor.LowerLeft, UiKit.TextColor);
            HeaderText(name, 140, 0, 64);
            UiKit.AddShadow(name, 3);
            var sub = UiKit.Label(top.transform, BamInfo.AppName, 32, TextAnchor.UpperLeft, UiKit.Orange);
            HeaderText(sub, 140, -46, 46);

            // "<Hero> selected", as in the old game.
            var pill = UiKit.Panel(body, new Color(0f, 0f, 0f, 0.45f));
            pill.pixelsPerUnitMultiplier = 0.7f;
            pill.raycastTarget = false;
            UiKit.Band(pill, true, 166, 76, 60);
            homeSelected = UiKit.Label(pill.transform, "", 34, TextAnchor.MiddleCenter, UiKit.TextColor);
            homeSelected.font = UiKit.BodyFont;
            UiKit.Stretch(homeSelected.rectTransform);

            // Bottom row: MODES and PLAY.
            var row = UiKit.NewRect("Bottom row", body);
            UiKit.Band(row, false, NavH + 24, 200, 24);
            var modesButton = UiKit.Button(row, "MODES", () => Push(modes), UiKit.Muted, SfxId.UiTap, 30);
            UiKit.Place(modesButton, 0, 0.5f, 0, 0, 200, 200);
            UiKit.Frame((RectTransform)modesButton.transform).color = UiKit.Orange;
            var modesLabel = UiKit.ButtonText(modesButton);
            modesLabel.alignment = TextAnchor.LowerCenter;
            modesLabel.rectTransform.offsetMin = new Vector2(0, 14);
            RobotIcon(modesButton.targetGraphic.rectTransform);

            var play = UiKit.Button(row, "PLAY  >", OnPlay, UiKit.Orange, SfxId.UiConfirm, 92);
            var playRt = (RectTransform)play.transform;
            playRt.anchorMin = new Vector2(0, 0);
            playRt.anchorMax = new Vector2(1, 1);
            playRt.offsetMin = new Vector2(224, 0);
            playRt.offsetMax = Vector2.zero;
            var playLabel = UiKit.ButtonText(play);
            playLabel.rectTransform.offsetMin = new Vector2(12, 52);
            UiKit.AddOutline(playLabel, 4);
            playSub = UiKit.Label(play.targetGraphic.transform, "", 32, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Band(playSub, false, 22, 44, 10);
            playSub.horizontalOverflow = HorizontalWrapMode.Overflow;
            var subEdge = playSub.gameObject.AddComponent<Outline>();
            subEdge.effectColor = new Color(0.43f, 0.15f, 0f);
            subEdge.effectDistance = new Vector2(2, -2);
        }

        // The old MODES icon: a small mint robot head.
        static void RobotIcon(RectTransform face)
        {
            var head = UiKit.Fill(face, UiKit.Primary, UiKit.Rounded);
            head.pixelsPerUnitMultiplier = 2.5f;
            UiKit.Place(head, 0.5f, 1, 0, -50, 76, 60);
            var stalk = UiKit.Fill(face, UiKit.Primary);
            UiKit.Place(stalk, 0.5f, 1, 0, -36, 6, 16);
            var knob = UiKit.Fill(face, UiKit.Primary, UiKit.Circle);
            UiKit.Place(knob, 0.5f, 1, 0, -26, 14, 14);
            var dark = new Color(0.04f, 0.06f, 0.16f);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark, UiKit.Circle), 0.5f, 0.5f, -16, 4, 14, 14);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark, UiKit.Circle), 0.5f, 0.5f, 16, 4, 14, 14);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark), 0.5f, 0.5f, 0, -14, 30, 5);
        }

        static void RefreshHome()
        {
            var id = (HeroId)BamSettings.LastHero;
            var info = HeroCatalog.Get(id);
            SetBadge(homeHero, id);
            SetBadge(homePortrait, id);
            homeSelected.text = info.Name + " - " + info.Title + " selected";
            playSub.text = "3V3 · VS BOTS · " + DifficultyNames[(int)BamSettings.LastDifficulty].ToUpperInvariant();
        }

        static void SetBadge(RectTransform badge, HeroId id)
        {
            badge.GetChild(0).GetComponent<Image>().color = UiKit.HeroColor(id);
            badge.GetComponentInChildren<Text>().text = HeroCatalog.Get(id).Name.Substring(0, 1);
        }

        static void OnPlay()
        {
            if (!BamSettings.TutorialDone && !tutorialOffered)
            {
                tutorialOffered = true;
                dialog.Show("New to the arena?", "Play a short guided match first to learn how to move, attack and use your special.",
                    "Skip", StartBotMatch,
                    "Tutorial", StartTutorial,
                    UiKit.Muted, UiKit.Primary, () => dialog.Hide());
                return;
            }
            StartBotMatch();
        }

        static void StartBotMatch()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = BamSettings.LastDifficulty, Tutorial = false });
        }

        static void StartTutorial()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = Difficulty.Easy, Tutorial = true });
        }

        // ---- the Modes sheet ----

        static Segmented modeDifficulty;

        static void BuildModes()
        {
            var rt = UiKit.NewRect("Modes", root);
            UiKit.Stretch(rt);
            modes = rt.gameObject;
            var veil = UiKit.Fill(rt, UiKit.Dim);
            veil.raycastTarget = true;
            var close = veil.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.onClick.AddListener(() => { BamAudio.Play(SfxId.UiBack); Pop(); });
            var safe = UiKit.SafeArea(rt);

            var sheet = UiKit.Panel(safe, PageFill);
            UiKit.Band(sheet, false, 24, 640, 20);
            UiKit.Frame(sheet.rectTransform);
            var title = UiKit.Label(sheet.transform, "Modes", 64, TextAnchor.MiddleLeft, UiKit.TextColor);
            UiKit.Band(title, true, 20, 90, 36);

            var bots = UiKit.Label(sheet.transform, "3v3 against bots", 40, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Band(bots, true, 130, 60, 36);
            var diffRow = UiKit.NewRect("Difficulty", sheet.transform);
            UiKit.Band(diffRow, true, 200, 116, 36);
            modeDifficulty = new Segmented(diffRow, DifficultyNames, i =>
            {
                BamSettings.LastDifficulty = (Difficulty)i;
                RefreshHome();
            });

            var training = UiKit.Button(sheet.transform, "Training", () => StartTutorial(), UiKit.Muted, SfxId.UiConfirm, 52);
            var trt = (RectTransform)training.transform;
            UiKit.Band(trt, true, 350, 130, 36);
            var hint = UiKit.Label(sheet.transform, "A short guided match that teaches the basics.", 30, TextAnchor.MiddleCenter, UiKit.TextDim);
            hint.font = UiKit.BodyFont;
            UiKit.Band(hint, true, 486, 50, 36);
            var done = UiKit.Button(sheet.transform, "Done", () => Pop(), UiKit.Primary, SfxId.UiBack, 48);
            UiKit.Place(done, 0.5f, 0, 0, 30, 360, 100);
            modes.SetActive(false);
        }

        // ---- Heroes ----

        static Image[] cardFrames;
        static GameObject[] cardChecks;
        static Text detailName, detailTitle, detailRole, specialName, specialDesc, passiveName, passiveDesc;
        static Image[,] statBars;
        static ScrollRect heroScroll;

        static void BuildHeroSelect()
        {
            var content = NewPage("Heroes", out heroSelect, out var panel);
            tabPanels[1] = panel;
            heroScroll = panel.GetComponentInChildren<ScrollRect>();
            VList(content, 16, new RectOffset(8, 8, 8, 30));

            cardFrames = new Image[HeroCatalog.Count];
            cardChecks = new GameObject[HeroCatalog.Count];
            RectTransform row = null;
            for (int i = 0; i < HeroCatalog.Count; i++)
            {
                if (i % 3 == 0)
                {
                    row = UiKit.NewRect("Row", content);
                    row.gameObject.AddComponent<LayoutElement>().preferredHeight = 330;
                }
                int index = i;
                var info = HeroCatalog.All[i];
                var card = UiKit.NewRect(info.Name, row);
                int col = i % 3;
                card.anchorMin = new Vector2(col / 3f, 0);
                card.anchorMax = new Vector2((col + 1) / 3f, 1);
                card.offsetMin = new Vector2(8, 0);
                card.offsetMax = new Vector2(-8, 0);
                var bg = card.gameObject.AddComponent<Image>();
                bg.sprite = UiKit.Rounded;
                bg.type = Image.Type.Sliced;
                bg.color = CardFill;
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); PickHero(index); });
                card.gameObject.AddComponent<ButtonFx>();
                cardFrames[i] = UiKit.Frame(card);

                var badge = UiKit.HeroBadge(card, info.Id);
                UiKit.Place(badge, 0.5f, 1, 0, -22, 170, 170);
                var heroName = UiKit.Label(card, info.Name, 44, TextAnchor.MiddleCenter, UiKit.TextColor);
                UiKit.Band(heroName, false, 62, 56, 4);
                UiKit.AddShadow(heroName, 2);
                var role = UiKit.Label(card, info.Role, 30, TextAnchor.MiddleCenter, Lavender);
                role.font = UiKit.BodyFont;
                role.fontStyle = FontStyle.Bold;
                UiKit.Band(role, false, 22, 40, 4);

                // The old "selected" mark: a mint circle with a white tick in the top right corner.
                var check = UiKit.Fill(card, UiKit.Primary, UiKit.Circle);
                UiKit.Place(check, 1, 1, 10, 10, 56, 56);
                var tick1 = UiKit.Fill(check.rectTransform, Color.white);
                UiKit.Place(tick1, 0.5f, 0.5f, -8, -3, 16, 6).localRotation = Quaternion.Euler(0, 0, -45);
                var tick2 = UiKit.Fill(check.rectTransform, Color.white);
                UiKit.Place(tick2, 0.5f, 0.5f, 5, 1, 26, 6).localRotation = Quaternion.Euler(0, 0, 50);
                cardChecks[i] = check.gameObject;
            }

            // The chosen hero's details under the grid.
            var detail = UiKit.NewRect("Details", content);
            var dbg = detail.gameObject.AddComponent<Image>();
            dbg.sprite = UiKit.Rounded;
            dbg.type = Image.Type.Sliced;
            dbg.color = CardFill;
            dbg.raycastTarget = false;
            UiKit.Frame(detail).color = new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.75f);
            VList(detail, 10, new RectOffset(36, 36, 28, 36));

            detailName = DetailText(detail, 72, UiKit.Gold, 0);
            UiKit.AddOutline(detailName, 3);
            detailTitle = DetailText(detail, 42, UiKit.TextColor, 0);
            detailRole = DetailText(detail, 32, Lavender, 0);
            detailRole.font = UiKit.BodyFont;
            detailRole.fontStyle = FontStyle.Bold;

            statBars = new Image[StatNames.Length, 3];
            for (int s = 0; s < StatNames.Length; s++)
            {
                var statRow = UiKit.NewRect(StatNames[s], detail);
                statRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
                var label = UiKit.Label(statRow, StatNames[s], 36, TextAnchor.MiddleLeft, UiKit.TextColor);
                label.rectTransform.anchorMin = new Vector2(0, 0);
                label.rectTransform.anchorMax = new Vector2(0.42f, 1);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                for (int k = 0; k < 3; k++)
                {
                    var seg = UiKit.Fill(statRow, UiKit.Muted, UiKit.Rounded);
                    seg.pixelsPerUnitMultiplier = 2.5f;
                    var r = seg.rectTransform;
                    r.anchorMin = new Vector2(0.42f + 0.58f * k / 3f, 0.22f);
                    r.anchorMax = new Vector2(0.42f + 0.58f * (k + 1) / 3f, 0.78f);
                    r.offsetMin = new Vector2(5, 0);
                    r.offsetMax = new Vector2(-5, 0);
                    statBars[s, k] = seg;
                }
            }

            specialName = DetailText(detail, 44, UiKit.Gold, 14);
            specialDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            specialDesc.font = UiKit.BodyFont;
            passiveName = DetailText(detail, 44, UiKit.Gold, 10);
            passiveDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            passiveDesc.font = UiKit.BodyFont;
        }

        static void HeaderText(Text t, float left, float y, float h)
        {
            var r = t.rectTransform;
            r.anchorMin = new Vector2(0, 0.5f);
            r.anchorMax = new Vector2(1, 0.5f);
            r.pivot = new Vector2(0.5f, 0);
            r.offsetMin = new Vector2(left, y);
            r.offsetMax = new Vector2(0, y + h);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static Text DetailText(RectTransform list, int size, Color color, float spaceAbove)
        {
            if (spaceAbove > 0)
            {
                var gap = UiKit.NewRect("Gap", list);
                gap.gameObject.AddComponent<LayoutElement>().preferredHeight = spaceAbove;
            }
            return UiKit.Label(list, "", size, TextAnchor.UpperLeft, color);
        }

        /// <summary>A vertical scroll area filling its parent; returns the content to fill (it grows with its children).</summary>
        static RectTransform ScrollContent(RectTransform parent, out ScrollRect scroll)
        {
            var viewport = UiKit.NewRect("Viewport", parent);
            UiKit.Stretch(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var content = UiKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = parent.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            return content;
        }

        static void RefreshHeroSelect()
        {
            PickHero(BamSettings.LastHero, false);
            if (heroScroll != null) heroScroll.verticalNormalizedPosition = 1;
        }

        // Picking a card makes that hero the one PLAY uses (saved at once, like the old game).
        static void PickHero(int index, bool save = true)
        {
            index = Mathf.Clamp(index, 0, HeroCatalog.Count - 1);
            if (save) BamSettings.LastHero = index;
            for (int i = 0; i < cardFrames.Length; i++)
            {
                bool on = i == index;
                cardFrames[i].color = on ? UiKit.Primary : new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.75f);
                cardChecks[i].SetActive(on);
            }

            var info = HeroCatalog.All[index];
            detailName.text = info.Name;
            detailTitle.text = info.Title;
            detailRole.text = info.Role + (info.Ranged ? "  /  Ranged" : "  /  Melee");
            SetBar(0, info.Health);
            SetBar(1, info.Damage);
            SetBar(2, info.Speed);
            SetBar(3, info.Difficulty);
            specialName.text = "Special: " + info.SpecialName;
            specialDesc.text = info.SpecialDesc;
            passiveName.text = "Passive: " + info.PassiveName;
            passiveDesc.text = info.PassiveDesc;
        }

        static void SetBar(int stat, int value)
        {
            for (int k = 0; k < 3; k++) statBars[stat, k].color = k < value ? UiKit.Gold : UiKit.Muted;
        }
    }
}
