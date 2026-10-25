using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // Main menu and hero select.
    public static partial class Screens
    {
        static GameObject mainMenu, heroSelect;
        static bool tutorialOffered; // once per app run, so Play is never nagging

        // ---- main menu ----

        static void BuildMainMenu()
        {
            var body = NewScreen("Main Menu", false, out mainMenu);

            var emblem = UiKit.Fill(body, UiKit.Gold, UiKit.Circle);
            UiKit.Place(emblem, 0.5f, 1, 0, -150, 280, 280);
            var ring = UiKit.Fill(emblem.rectTransform, UiKit.Ink, UiKit.Circle);
            UiKit.Stretch(ring.rectTransform, 16, 16, 16, 16);
            var bam = UiKit.Label(ring.rectTransform, BamInfo.ShortName, 104, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Stretch(bam.rectTransform, 0, 0, 0, 8);
            bam.horizontalOverflow = HorizontalWrapMode.Overflow;

            var line1 = UiKit.Label(body, "Battle Arena", 96, TextAnchor.MiddleCenter, UiKit.TextColor);
            UiKit.Band(line1, true, 470, 130, 30);
            UiKit.AddOutline(line1, 4);
            var line2 = UiKit.Label(body, "MASTERS", 150, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Band(line2, true, 580, 190, 30);
            UiKit.AddOutline(line2, 5);

            var play = UiKit.Button(body, "PLAY", OnPlay, UiKit.Gold, SfxId.UiConfirm, 96);
            UiKit.Place(play, 0.5f, 0, 0, 620, 680, 200);
            UiKit.AddOutline(UiKit.ButtonText(play), 3);
            var tutorial = UiKit.Button(body, "Tutorial", () => StartTutorial(), UiKit.Primary);
            UiKit.Place(tutorial, 0.5f, 0, 0, 440, 560, 140);
            var settingsButton = UiKit.Button(body, "Settings", () => Push(settings), UiKit.Muted);
            UiKit.Place(settingsButton, 0.5f, 0, 0, 270, 560, 140);

            var version = UiKit.Label(body, "v" + Application.version, 32, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Band(version, false, 40, 50, 30);
        }

        static void OnPlay()
        {
            if (!BamSettings.TutorialDone && !tutorialOffered)
            {
                tutorialOffered = true;
                dialog.Show("New to the arena?", "Play a short guided match first to learn how to move, attack and use your special.",
                    "Skip", () => Push(heroSelect),
                    "Tutorial", () => StartTutorial(),
                    UiKit.Muted, UiKit.Good, () => dialog.Hide());
                return;
            }
            Push(heroSelect);
        }

        static void StartTutorial()
        {
            StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = Difficulty.Easy, Tutorial = true });
        }

        // ---- hero select ----

        static readonly string[] DifficultyNames = { "Easy", "Normal", "Hard" };
        static readonly string[] StatNames = { "Health", "Damage", "Speed", "Difficulty" };

        static int pickedHero;
        static Segmented difficulty;
        static RectTransform[] badgeSlots;
        static Image[] badgeRings;
        static Image detailBadgeColor;
        static Text detailInitial, detailName, detailTitle, detailRole, specialName, specialDesc, passiveName, passiveDesc;
        static Image[,] statBars;
        static ScrollRect detailScroll;

        static void BuildHeroSelect()
        {
            var body = NewScreen("Hero Select", false, out heroSelect);
            TopBar(body, "Choose Your Hero");

            // One row of seven badges, each taking a seventh of the width so it fits any phone.
            var row = UiKit.NewRect("Badges", body);
            UiKit.Band(row, true, 160, 180, 16);
            badgeSlots = new RectTransform[HeroCatalog.Count];
            badgeRings = new Image[HeroCatalog.Count];
            for (int i = 0; i < HeroCatalog.Count; i++)
            {
                int index = i;
                var info = HeroCatalog.All[i];
                var slot = UiKit.NewRect(info.Name, row);
                slot.anchorMin = new Vector2((float)i / HeroCatalog.Count, 0);
                slot.anchorMax = new Vector2((float)(i + 1) / HeroCatalog.Count, 1);
                slot.offsetMin = slot.offsetMax = Vector2.zero;
                var hit = slot.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var b = slot.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); PickHero(index); });
                slot.gameObject.AddComponent<ButtonFx>();

                var badge = UiKit.HeroBadge(slot, info.Id);
                UiKit.Place(badge, 0.5f, 1, 0, -6, 116, 116);
                badgeRings[i] = badge.GetComponent<Image>();
                var name = UiKit.Label(slot, info.Name, 30, TextAnchor.MiddleCenter, UiKit.TextColor);
                UiKit.Band(name, false, 0, 44, 0);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                badgeSlots[i] = slot;
            }

            // Bottom: Start, and the bot difficulty above it.
            var start = UiKit.Button(body, "START", OnStart, UiKit.Gold, SfxId.UiConfirm, 84);
            UiKit.Place(start, 0.5f, 0, 0, 40, 640, 170);
            UiKit.AddOutline(UiKit.ButtonText(start), 3);
            var diffRow = UiKit.NewRect("Difficulty", body);
            UiKit.Band(diffRow, false, 240, 110, 40);
            difficulty = new Segmented(diffRow, DifficultyNames, null);
            var diffLabel = UiKit.Label(body, "Bot difficulty", 36, TextAnchor.MiddleCenter, UiKit.TextDim);
            UiKit.Band(diffLabel, false, 355, 50, 40);

            // Middle: the picked hero's details, scrollable in case a long text meets a short screen.
            var panel = UiKit.Panel(body, UiKit.PanelColor);
            UiKit.Stretch(panel.rectTransform, 24, 360, 24, 420);
            var content = ScrollContent(panel.rectTransform, out detailScroll);
            VList(content, 14, new RectOffset(36, 36, 30, 36));

            var header = UiKit.NewRect("Header", content);
            header.gameObject.AddComponent<LayoutElement>().preferredHeight = 200;
            var big = UiKit.HeroBadge(header, HeroId.Bram);
            UiKit.Place(big, 0, 0.5f, 0, 0, 190, 190);
            detailBadgeColor = big.GetChild(0).GetComponent<Image>();
            detailInitial = big.GetComponentInChildren<Text>();
            detailName = UiKit.Label(header, "", 84, TextAnchor.LowerLeft, UiKit.Gold);
            HeaderText(detailName, 220, 100, 100);
            UiKit.AddOutline(detailName, 3);
            detailTitle = UiKit.Label(header, "", 44, TextAnchor.UpperLeft, UiKit.TextColor);
            HeaderText(detailTitle, 220, 46, 58);
            detailRole = UiKit.Label(header, "", 36, TextAnchor.UpperLeft, UiKit.TextDim);
            HeaderText(detailRole, 220, 4, 46);

            statBars = new Image[StatNames.Length, 3];
            for (int s = 0; s < StatNames.Length; s++)
            {
                var statRow = UiKit.NewRect(StatNames[s], content);
                statRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 50;
                var label = UiKit.Label(statRow, StatNames[s], 38, TextAnchor.MiddleLeft, UiKit.TextColor);
                label.rectTransform.anchorMin = new Vector2(0, 0);
                label.rectTransform.anchorMax = new Vector2(0.42f, 1);
                label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                for (int k = 0; k < 3; k++)
                {
                    var seg = UiKit.Fill(statRow, UiKit.Muted, UiKit.Rounded);
                    seg.pixelsPerUnitMultiplier = 2.5f;
                    var r = seg.rectTransform;
                    r.anchorMin = new Vector2(0.42f + 0.58f * k / 3f, 0.2f);
                    r.anchorMax = new Vector2(0.42f + 0.58f * (k + 1) / 3f, 0.8f);
                    r.offsetMin = new Vector2(5, 0);
                    r.offsetMax = new Vector2(-5, 0);
                    statBars[s, k] = seg;
                }
            }

            specialName = DetailText(content, 46, UiKit.Gold, 16);
            specialDesc = DetailText(content, 36, UiKit.TextDim, 0);
            passiveName = DetailText(content, 46, UiKit.Gold, 10);
            passiveDesc = DetailText(content, 36, UiKit.TextDim, 0);
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
            difficulty.Set((int)BamSettings.LastDifficulty);
            PickHero(BamSettings.LastHero);
        }

        static void PickHero(int index)
        {
            pickedHero = Mathf.Clamp(index, 0, HeroCatalog.Count - 1);
            for (int i = 0; i < badgeSlots.Length; i++)
            {
                bool on = i == pickedHero;
                badgeRings[i].color = on ? UiKit.Gold : Color.white;
                badgeSlots[i].GetChild(0).localScale = on ? new Vector3(1.15f, 1.15f, 1f) : Vector3.one;
            }

            var info = HeroCatalog.All[pickedHero];
            detailBadgeColor.color = UiKit.HeroColor(info.Id);
            detailInitial.text = info.Name.Substring(0, 1);
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
            detailScroll.verticalNormalizedPosition = 1;
        }

        static void SetBar(int stat, int value)
        {
            for (int k = 0; k < 3; k++) statBars[stat, k].color = k < value ? UiKit.Gold : UiKit.Muted;
        }

        static void OnStart()
        {
            var diff = (Difficulty)Mathf.Max(0, difficulty.Selected);
            BamSettings.LastHero = pickedHero;
            BamSettings.LastDifficulty = diff;
            StartMatch(new MatchSetup { Hero = HeroCatalog.All[pickedHero].Id, Difficulty = diff, Tutorial = false });
        }
    }
}
