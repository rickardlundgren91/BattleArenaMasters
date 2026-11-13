using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // The old mode sheet (MenuHome3.cs HubModeSheet in the ONE_PLAY style): a dark scrim, a sheet sliding up from the bottom
    // with a handle, "Choose mode", one card per mode (its icon, name and a line under it; the chosen card warm with the gold
    // rune frame and a gold tick, the others dim with a chevron), CLASSIC opening its BOT DIFFICULTY under it, and the orange
    // PLAY at the bottom, which starts the chosen mode. Home's PLAY and MODES icon follow the mode the sheet's PLAY last started (nr 129).
    // BAM's modes: CLASSIC (3v3 against bots), TRAINING (the guided match) and HERO TEST ("Try out any hero", the old mode
    // list's entry); RANKED is not in BAM. 2026-10-31 (Rickard's pick on a card): TRAINING and HERO TEST share one card, PRACTICE,
    // which opens TUTORIAL / HERO TEST under it the way CLASSIC opens its BOT DIFFICULTY; Home still plays the one picked. Online builds add ONLINE MATCH under them (OnlineEntries.cs); friends are invited from Home.
    public static partial class Screens
    {
        enum Mode { Classic, Training, HeroTest, NewMap } // NewMap: a CLASSIC match on the second map (Grove), PRACTICE only until tested (Rickard 2026-11-04)

        static readonly Mode[] SheetModes = { Mode.Classic, Mode.Training }; // Training's card is PRACTICE (Training or HeroTest)
        static Mode lastPractice = Mode.Training;
        static bool IsPractice(Mode m) => m == Mode.Training || m == Mode.HeroTest || m == Mode.NewMap;
        static RectTransform modePracticeExpand;
        static HubChoice modePractice;
        const float PracH = 18.5f * Cw;
        static Mode homeMode = Mode.Classic; // CLASSIC when the game starts, as in the old game
        // what the open sheet shows: tapping a card only looks; Home's PLAY changes when the sheet's own PLAY is pressed
        // (full review 2026-11-13 nr 129: a look at PRACTICE made Home's PLAY start the tutorial after the sheet closed)
        static Mode sheetMode = Mode.Classic;

        sealed class ModeCard { public Mode Id; public RectTransform Rect; public CanvasGroup Icon; public GameObject On, Off, Tick, Chevron; public Image Fill; }

        static readonly ModeCard[] modeCards = new ModeCard[2];
        static RectTransform modeSheet, modeExpand, modeSheetPlay;
        static Image modeScrim;
        static HubChoice modeDifficulty, modeParty;
        static Text diffLabel;
        static RectTransform modePartyRow;
        static Text modePlaySub;
        static ModesFx modesFx;

        const float OptH = 17 * Cw, OptGap = 2.4f * Cw, ExpH = 20.5f * Cw, SheetX = 4 * Cw;

        static string ModeTitle(Mode m) => m == Mode.Classic ? "CLASSIC" : m == Mode.HeroTest ? "HERO TEST" : m == Mode.NewMap ? "NEW MAP" : "TUTORIAL";
        // the sheet's two cards: CLASSIC, and PRACTICE for both Training and HeroTest
        static string CardTitle(Mode m) => m == Mode.Classic ? "CLASSIC" : "PRACTICE";
        static string CardSub(Mode m) => m == Mode.Classic ? "3v3 against bots" : "Tutorial, any hero, or the new map";

        // what Home's PLAY and the sheet's PLAY start (HubPlayNow)
        static void PlayMode()
        {
            if (TestParty == null && OnlineHooks.PartyPlay != null && OnlineHooks.PartyPlay()) return; // a party: the leader's PLAY starts it (HomeParty.cs)
            if (homeMode == Mode.Training) StartTutorial();
            else if (homeMode == Mode.HeroTest) StartHeroTest((HeroId)BamSettings.LastHero);
            else if (homeMode == Mode.NewMap) StartMatch(new MatchSetup { Hero = (HeroId)BamSettings.LastHero, Difficulty = BamSettings.LastDifficulty, Map = MapId.Grove });
            else StartBotMatch();
        }

        /// <summary>HERO TEST (the old herotest): the hero against dummies at level 1; its own bar changes hero and level.</summary>
        static void StartHeroTest(HeroId hero) => StartMatch(new MatchSetup { Hero = hero, HeroTest = true, Level = 1 });

        static PartyView Party() => CurrentParty();
        const float PartyRowH = 17.5f * Cw;
        static float ExpNow => ExpH + (Party() != null ? PartyRowH : 0);

        static string PlaySubFor(Mode m, bool withDifficulty) =>
            m == Mode.Classic ? "3V3 · VS BOTS" + (withDifficulty ? " · " + DifficultyNames[(int)BamSettings.LastDifficulty].ToUpperInvariant() : "") : ModeTitle(m);

        static void BuildModes()
        {
            var rt = UiKit.NewRect("Modes", root);
            UiKit.Stretch(rt);
            modes = rt.gameObject;
            modeScrim = UiKit.Fill(rt, new Color(5 / 255f, 7 / 255f, 26 / 255f, 0.62f));
            modeScrim.raycastTarget = true;
            var close = modeScrim.gameObject.AddComponent<Button>();
            close.transition = Selectable.Transition.None;
            close.navigation = new Navigation { mode = Navigation.Mode.None };
            close.onClick.AddListener(() => { BamAudio.Play(SfxId.UiBack); CloseModes(); });
            var safe = UiKit.SafeArea(rt);

            // the sheet: dark glass from #1a1f4a to #0a0d26, round top corners, gold edge; it reaches past the bottom edge
            modeSheet = UiKit.NewRect("Sheet", safe);
            modeSheet.anchorMin = new Vector2(0, 0);
            modeSheet.anchorMax = new Vector2(1, 0);
            modeSheet.pivot = new Vector2(0.5f, 1);
            var glass = Pic(modeSheet, "Glass", UiKit.Rounded, Color.white);
            Radius(glass, 6 * Cw);
            UiKit.Stretch(glass.rectTransform, 0, 0, 0, -6 * Cw - 400);
            glass.raycastTarget = true; // taps on the sheet do not close it
            glass.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var grad = UiKit.NewRect("Gradient", glass.rectTransform);
            UiKit.Stretch(grad);
            var gr = grad.gameObject.AddComponent<RawImage>();
            gr.texture = HomeArt.Gradient(new Color(26 / 255f, 31 / 255f, 74 / 255f, 1f), new Color(10 / 255f, 13 / 255f, 38 / 255f, 1f)); // solid: at 0.97 Home's PLAY and the tab bar showed through
            gr.raycastTarget = false;
            var edge = Pic(glass.rectTransform, "Edge", HomeArt.RoundRing(0.4f / 6f), UiKit.Trim);
            UiKit.Stretch(edge.rectTransform);
            RingRadius(edge, 6 * Cw);

            var handle = Pic(modeSheet, "Handle", UiKit.Rounded, new Color(1, 1, 1, 0.25f));
            Radius(handle, 0.6f * Cw);
            Mid(handle.rectTransform, 0.5f, 1, 0, -1.8f * Cw - 0.6f * Cw, 12 * Cw, 1.2f * Cw);

            var title = UiKit.Label(modeSheet, "Choose mode", Mathf.RoundToInt(5.6f * Cw), TextAnchor.MiddleLeft, HubText);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretched(title.rectTransform, SheetX, 6 * Cw + 3.5f * Cw - 4 * Cw, SheetX, 8 * Cw);

            for (int i = 0; i < SheetModes.Length; i++) modeCards[i] = BuildModeCard(SheetModes[i]);
            BuildOnlineRows();

            // CLASSIC's BOT DIFFICULTY (the map pick of the old sheet is left out: BAM has one arena)
            modeExpand = UiKit.NewRect("Classic", modeSheet);
            diffLabel = Treb(modeExpand, "BOT DIFFICULTY", 2.7f, UiKit.TextDim);
            Stretched(diffLabel.rectTransform, 0, Cw + 1.8f * Cw - 2.5f * Cw, 0, 5 * Cw);
            var segs = UiKit.NewRect("Difficulty", modeExpand);
            Stretched(segs, 0, Cw + 4.5f * Cw, 0, 12 * Cw);
            modeDifficulty = new HubChoice(segs, DifficultyNames, i =>
            {
                if (i == (int)Difficulty.Hard && !BamSettings.HardOpen) // locked like Ultra: Hard opens after 3 wins against Normal (Rickard 2026-11-04)
                {
                    ShowMenuHint("Hard opens after " + BamSettings.NormalWinsForHard + " wins against Normal (" + Mathf.Min(BamSettings.NormalWins, BamSettings.NormalWinsForHard) + "/" + BamSettings.NormalWinsForHard + ")");
                    RefreshModes(); return;
                }
                if (i == (int)Difficulty.Ultra && !BamSettings.UltraOpen) // locked: says why only when tapped, the label stays short (Rickard 2026-10-31)
                {
                    ShowMenuHint("Ultra opens after " + BamSettings.UltraWins + " wins against Hard (" + Mathf.Min(BamSettings.HardWins, BamSettings.UltraWins) + "/" + BamSettings.UltraWins + ")");
                    RefreshModes(); return;
                }
                // in a party the leader picks the bots for everyone (MpRoom.SetDiff); the others are told and the choice goes back
                if (TestParty != null) TestParty.Bots = (Difficulty)i;
                else if (Party() != null) OnlineHooks.PartyBots?.Invoke((Difficulty)i);
                else BamSettings.LastDifficulty = (Difficulty)i;
                RefreshHome();
                RefreshModes();
            }, true);

            // in a party, under it: TOGETHER (the party against bots) or AGAINST EACH OTHER, the leader's pick as the bots
            modePartyRow = UiKit.NewRect("Party", modeExpand);
            var partyLabel = Treb(modePartyRow, "PARTY", 2.7f, UiKit.TextDim);
            Stretched(partyLabel.rectTransform, 0, 1.8f * Cw - 2.5f * Cw, 0, 5 * Cw);
            var psegs = UiKit.NewRect("Mode", modePartyRow);
            Stretched(psegs, 0, 4.5f * Cw, 0, 12 * Cw);
            modeParty = new HubChoice(psegs, new[] { "TOGETHER", "AGAINST EACH OTHER" }, i =>
            {
                if (TestParty != null) TestParty.Mode = i; else OnlineHooks.PartyMode?.Invoke(i);
                RefreshModes();
            }, true);

            // PRACTICE's pick: the guided tutorial or HERO TEST
            modePracticeExpand = UiKit.NewRect("Practice", modeSheet);
            var pracLabel = Treb(modePracticeExpand, "PRACTICE", 2.7f, UiKit.TextDim);
            Stretched(pracLabel.rectTransform, 0, Cw + 1.8f * Cw - 2.5f * Cw, 0, 5 * Cw);
            var prsegs = UiKit.NewRect("Pick", modePracticeExpand);
            Stretched(prsegs, 0, Cw + 4.5f * Cw, 0, 12 * Cw);
            modePractice = new HubChoice(prsegs, new[] { "TUTORIAL", "HERO TEST", "NEW MAP" }, i =>
            {
                sheetMode = lastPractice = i == 0 ? Mode.Training : i == 1 ? Mode.HeroTest : Mode.NewMap;
                RefreshModes();
            }, true);

            modeSheetPlay = BuildSheetPlay(modeSheet, out modePlaySub);

            modesFx = modes.AddComponent<ModesFx>();
            modes.SetActive(false);
        }

        static ModeCard BuildModeCard(Mode m)
        {
            var c = new ModeCard { Id = m };
            c.Rect = UiKit.NewRect(CardTitle(m), modeSheet);
            // the warm glow behind the chosen card
            var glow = Pic(c.Rect, "Glow", HomeArt.ModeGlow, Color.white);
            Mid(glow.rectTransform, 0.5f, 1, 0, -OptH / 2, 1080 - 2 * SheetX, OptH * 1.4f); // inside the sheet: 1.2 x the width glowed out past the screen's sides
            c.Fill = Pic(c.Rect, "Fill", UiKit.Rounded, new Color(1, 1, 1, 0.04f));
            UiKit.Stretch(c.Fill.rectTransform);
            Radius(c.Fill, 3.4f * Cw);
            c.Fill.raycastTarget = true;
            var on = UiKit.NewRect("On", c.Rect);
            UiKit.Stretch(on);
            RuneFrame(on, 3.4f * Cw);
            glow.transform.SetParent(on, true);
            glow.transform.SetAsFirstSibling();
            c.On = on.gameObject;
            var off = Pic(c.Rect, "Quiet", HomeArt.RoundRing(0.3f / 3.4f), new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.35f));
            UiKit.Stretch(off.rectTransform);
            RingRadius(off, 3.4f * Cw);
            c.Off = off.gameObject;

            // the top OptH: icon, name, the line under it, tick or chevron
            var head = UiKit.NewRect("Head", c.Rect);
            Stretched(head, 0, 0, 0, OptH);
            var icon = UiKit.NewRect("Icon", head);
            Mid(icon, 0, 0.5f, 3 * Cw + 5 * Cw, 0, 10 * Cw, 10 * Cw);
            c.Icon = icon.gameObject.AddComponent<CanvasGroup>();
            c.Icon.blocksRaycasts = false;
            ModeIcon(icon, m, 10 * Cw);
            float tx = 3 * Cw + 5 * Cw + 8 * Cw;
            var name = UiKit.Label(head, CardTitle(m), Mathf.RoundToInt(4.4f * Cw), TextAnchor.MiddleLeft, HubText);
            name.horizontalOverflow = HorizontalWrapMode.Overflow;
            Mid(name.rectTransform, 0, 0.5f, tx + 30 * Cw, 2.3f * Cw, 60 * Cw, 6 * Cw);
            var sub = Treb(head, CardSub(m), 2.9f, new Color32(0xc6, 0xca, 0xe6, 0xff));
            Mid(sub.rectTransform, 0, 0.5f, tx + 30 * Cw, -2.9f * Cw, 60 * Cw, 5 * Cw);
            var tick = UiKit.NewRect("Tick", head);
            Mid(tick, 1, 0.5f, -6 * Cw, 0, 4 * Cw, 4 * Cw);
            Tick(tick, 1.8f * Cw * 0.72f, UiKit.Trim);
            c.Tick = tick.gameObject;
            var chev = UiKit.NewRect("Chevron", head);
            Mid(chev, 1, 0.5f, -6 * Cw, 0, 3 * Cw, 3 * Cw);
            ModeChevron(chev, 1.4f * Cw, UiKit.TextDim);
            c.Chevron = chev.gameObject;

            var b = head.gameObject.AddComponent<Button>();
            var hit = head.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            b.targetGraphic = hit;
            b.transition = Selectable.Transition.None;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var mode = m;
            b.onClick.AddListener(() =>
            {
                BamAudio.Play(SfxId.UiTap);
                // both cards open their pick under them, so the sheet stays; PRACTICE comes back to the last practice picked
                sheetMode = mode == Mode.Classic ? Mode.Classic : IsPractice(sheetMode) ? sheetMode : lastPractice;
                RefreshModes();
            });
            head.gameObject.AddComponent<ButtonFx>().PressScale = false;
            return c;
        }

        // the sheet's PLAY: the same orange button as on Home (Rickard's button image, dark edge, PLAY and what it starts)
        static RectTransform BuildSheetPlay(RectTransform parent, out Text sub)
        {
            float h = 14 * Cw, r = Mathf.Min(h * 0.32f, 5 * Cw), b = 0.7f * Cw;
            var rt = UiKit.NewRect("Play", parent);
            var shadow = Pic(rt, "Shadow", UiKit.Rounded, new Color(0, 0, 0, 0.45f));
            Radius(shadow, r);
            UiKit.Stretch(shadow.rectTransform, 0, Cw, 0, -Cw);
            var edgeImg = Pic(rt, "Edge", UiKit.Rounded, new Color32(0x0b, 0x13, 0x30, 0xff));
            UiKit.Stretch(edgeImg.rectTransform);
            Radius(edgeImg, r);
            var face = Pic(rt, "Face", UiKit.Rounded, UiKit.Orange);
            Radius(face, r - b);
            UiKit.Stretch(face.rectTransform, b, b, b, b);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var art = UiKit.NewRect("Art", face.rectTransform);
            UiKit.Stretch(art);
            var raw = art.gameObject.AddComponent<RawImage>();
            raw.texture = Picture("menu_btn_orange");
            raw.raycastTarget = false;
            var label = UiKit.Label(rt, "PLAY", Mathf.RoundToInt(6.6f * Cw), TextAnchor.MiddleCenter, Color.white);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            LabelEdge(label, new Color32(0x8a, 0x32, 0x00, 0xff), 6.6f * Cw * 0.042f, new Color32(0xb0, 0x4a, 0x00, 0xff), 6.6f * Cw * 0.083f);
            Mid(label.rectTransform, 0.5f, 0.5f, 0, 0.08f * h, 60 * Cw, 6.6f * Cw * 1.4f);
            sub = UiKit.Label(rt, "", Mathf.RoundToInt(3 * Cw), TextAnchor.MiddleCenter, Color.white);
            sub.horizontalOverflow = HorizontalWrapMode.Overflow;
            LabelEdge(sub, new Color32(0x6e, 0x26, 0x00, 0xff), 0.3f * Cw, new Color(60 / 255f, 15 / 255f, 0, 0.85f), 0.45f * Cw);
            Mid(sub.rectTransform, 0.5f, 0.5f, 0, -0.28f * h, 80 * Cw, 5 * Cw);
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var button = rt.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                BamAudio.Play(SfxId.UiConfirm);
                homeMode = sheetMode; // only here does the sheet change what Home's PLAY starts (nr 129)
                RefreshHome();
                PlayMode(); // StartMatch hides every screen, the sheet too
            });
            rt.gameObject.AddComponent<ButtonFx>();
            return rt;
        }

        static void LabelEdge(Text t, Color stroke, float width, Color shadow, float drop)
        {
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = stroke;
            o.effectDistance = new Vector2(width, -width);
            var s = t.gameObject.AddComponent<Shadow>();
            s.effectColor = shadow;
            s.effectDistance = new Vector2(0, -drop);
        }

        static void CloseModes()
        {
            if (modesFx != null && modes.activeSelf) modesFx.Close();
            else Pop();
        }

        // the cards, CLASSIC's difficulty and PLAY from the top down; the sheet's height follows its content
        static void RefreshModes()
        {
            if (modeSheet == null) return;
            var party = Party();
            bool ultraOpen = BamSettings.UltraOpen;
            modeDifficulty.Dim((int)Difficulty.Hard, !BamSettings.HardOpen); // Hard opens after 3 wins against Normal (Rickard 2026-11-04)
            modeDifficulty.Dim((int)Difficulty.Ultra, !ultraOpen);
            modeDifficulty.Set((int)(party != null ? party.Bots : BamSettings.LastDifficulty));
            // Ultra opens after 30 wins against Hard bots (Rickard 2026-10-31)
            diffLabel.text = "BOT DIFFICULTY"; // a locked Ultra is dimmed; tapping it tells how to open it (ShowHint)
            modePartyRow.gameObject.SetActive(party != null);
            if (party != null) { modeParty.Set(party.Mode); Stretched(modePartyRow, 0, ExpH, 0, PartyRowH); }
            float expH = ExpNow;
            float y = 6 * Cw + 11 * Cw;
            for (int i = 0; i < modeCards.Length; i++)
            {
                var c = modeCards[i];
                bool on = c.Id == sheetMode || IsPractice(c.Id) && IsPractice(sheetMode), open = on;
                float oh = OptH + (open ? (c.Id == Mode.Classic ? expH : PracH) : 0);
                Stretched(c.Rect, SheetX, y, SheetX, oh);
                c.On.SetActive(on);
                c.Off.SetActive(!on);
                c.Fill.color = on ? new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.08f) : new Color(1, 1, 1, 0.04f);
                c.Icon.alpha = on ? 1 : 0.45f;
                c.Tick.SetActive(on);
                c.Chevron.SetActive(!on);
                if (open && c.Id == Mode.Classic)
                {
                    modeExpand.gameObject.SetActive(true);
                    Stretched(modeExpand, SheetX + 3 * Cw, y + OptH, SheetX + 3 * Cw, expH);
                }
                if (open && IsPractice(c.Id))
                {
                    modePracticeExpand.gameObject.SetActive(true);
                    modePractice.Set(sheetMode == Mode.HeroTest ? 1 : sheetMode == Mode.NewMap ? 2 : 0);
                    Stretched(modePracticeExpand, SheetX + 3 * Cw, y + OptH, SheetX + 3 * Cw, PracH);
                }
                y += oh + OptGap;
            }
            if (sheetMode != Mode.Classic) modeExpand.gameObject.SetActive(false);
            if (!IsPractice(sheetMode)) modePracticeExpand.gameObject.SetActive(false);
            y = LayoutOnlineRows(y);
            Stretched(modeSheetPlay, SheetX, y + Cw, SheetX, 14 * Cw);
            modePlaySub.text = party != null ? "WITH YOUR PARTY" : PlaySubFor(sheetMode, false);
            float rows = SheetModes.Length + OnlineRowCount;
            modeSheet.sizeDelta = new Vector2(0, 19 * Cw + rows * OptH + (rows - 1) * OptGap + (sheetMode == Mode.Classic ? expH : PracH) + 4 * Cw + 13 * Cw + 6 * Cw + 6 * Cw);
        }

        // HubModeIcon: the mode's sign on a dark rounded square of size s (classic the robot, training two open pages,
        // hero test a target)
        static void ModeIcon(RectTransform box, Mode m, float s)
        {
            if (m == Mode.Classic) { HomeFx.RobotIcon(box, 0, s); return; }
            var back = new Color32(0x22, 0x2a, 0x62, 0xff);
            var sq = Pic(box, "Back", UiKit.Rounded, back);
            Radius(sq, s * 0.26f);
            Mid(sq.rectTransform, 0.5f, 0.5f, 0, 0, s, s);
            float k = s * 0.3f;
            if (m == Mode.HeroTest)
            {
                float lw = 0.22f * k;
                for (int i = 0; i < 2; i++)
                {
                    float r = i == 0 ? k : 0.55f * k, outer = r + lw / 2;
                    Mid(Pic(box, "Ring", HomeArt.CircleRing(lw / outer), UiKit.Primary).rectTransform, 0.5f, 0.5f, 0, 0, 2 * outer, 2 * outer);
                }
                Mid(Pic(box, "Dot", UiKit.Circle, UiKit.Primary).rectTransform, 0.5f, 0.5f, 0, 0, 0.4f * k, 0.4f * k);
                return;
            }
            for (int p = 0; p < 2; p++)
            {
                float px = p == 0 ? -1.05f * k + 0.49f * k : 0.07f * k + 0.49f * k;
                var page = Pic(box, "Page", UiKit.Rounded, UiKit.Primary);
                Radius(page, 0.15f * k);
                Mid(page.rectTransform, 0.5f, 0.5f, px, 0, 0.98f * k, 1.7f * k);
                for (int i = 0; i < 3; i++)
                    Mid(Pic(box, "Line", null, back).rectTransform, 0.5f, 0.5f, px - 0.49f * k + 0.2f * k + 0.3f * k, -(-0.45f * k + i * 0.4f * k + 0.05f * k), 0.6f * k, 0.1f * k);
            }
        }

        // HubChevron: a thin ›
        static void ModeChevron(RectTransform box, float k, Color color)
        {
            float t = 0.35f * Cw, len = k * Mathf.Sqrt(2) * 0.75f;
            var a = Pic(box, "A", null, color);
            Mid(a.rectTransform, 0.5f, 0.5f, 0, k * 0.5f, len, t);
            a.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
            var b = Pic(box, "B", null, color);
            Mid(b.rectTransform, 0.5f, 0.5f, 0, -k * 0.5f, len, t);
            b.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        // HubRuneFrame3 on a card that stretches: the soft light on top, the gold edge, the thin inner line, gold studs
        static void RuneFrame(RectTransform face, float r)
        {
            var sheen = Pic(face, "Sheen", UiKit.Rounded, new Color(1f, 236 / 255f, 190 / 255f, 0.07f));
            Radius(sheen, r * 0.7f);
            sheen.rectTransform.anchorMin = new Vector2(0, 1);
            sheen.rectTransform.anchorMax = new Vector2(1, 1);
            sheen.rectTransform.pivot = new Vector2(0.5f, 1);
            sheen.rectTransform.offsetMin = new Vector2(0.8f * Cw, -0.6f * Cw - OptH * 0.38f);
            sheen.rectTransform.offsetMax = new Vector2(-0.8f * Cw, -0.6f * Cw);
            var gold = Pic(face, "Gold", HomeArt.RoundRing(0.45f * Cw / r), UiKit.Trim);
            UiKit.Stretch(gold.rectTransform);
            RingRadius(gold, r);
            var inner = Pic(face, "Inner", HomeArt.RoundRing(0.25f * Cw / (r - Cw)), new Color(UiKit.Trim.r, UiKit.Trim.g, UiKit.Trim.b, 0.3f));
            UiKit.Stretch(inner.rectTransform, Cw, Cw, Cw, Cw);
            RingRadius(inner, r - Cw);
            float k = 0.9f * Cw, d = r * 0.55f;
            for (int i = 0; i < 4; i++)
            {
                var stud = Pic(face, "Stud", null, UiKit.Gold);
                float ax = i % 2, ay = i < 2 ? 1 : 0;
                Mid(stud.rectTransform, ax, ay, ax == 0 ? d : -d, ay == 1 ? -d : d, k * 1.414f, k * 1.414f);
                stud.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        /// <summary>The sheet slides up with the old ease (16 per second, then EaseOut) over the fading scrim, and back down to close.</summary>
        sealed class ModesFx : MonoBehaviour
        {
            float k;
            bool closing;

            void OnEnable()
            {
                k = 0;
                closing = false;
                sheetMode = homeMode; // the sheet opens on what Home plays now
                RefreshModes();
                Apply();
            }

            public void Close() => closing = true;

            void Update()
            {
                float target = closing ? 0 : 1;
                k += (target - k) * Mathf.Min(1, Time.unscaledDeltaTime * 16);
                if (Mathf.Abs(target - k) < 0.002f) k = target;
                Apply();
                if (closing && k <= 0.002f) { closing = false; Pop(); }
            }

            void Apply()
            {
                float e = 1 - (1 - k) * (1 - k);
                modeScrim.color = new Color(5 / 255f, 7 / 255f, 26 / 255f, 0.62f * k);
                float h = modeSheet.sizeDelta.y;
                modeSheet.anchoredPosition = new Vector2(0, h * e);
            }
        }
    }

    static partial class HomeArt
    {
        static Sprite modeGlow;

        // the warm glow behind the chosen mode card: rgba(217,164,65,0.16) in the middle to nothing
        public static Sprite ModeGlow => modeGlow != null ? modeGlow : modeGlow = Radial("ModeGlow",
            (0f, new Color(217 / 255f, 164 / 255f, 65 / 255f, 0.16f)), (1f, new Color(217 / 255f, 164 / 255f, 65 / 255f, 0f)));
    }
}
