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

        static RectTransform homePortrait, homeHero, homeBody;
        static RawImage homeBg, homePortraitImg;
        static Image homeTopFade, homeBottomFade, homeBottomFill, homeEdgeL, homeEdgeR;
        const float HomePlatY = NavH + 24 + 200 + 260; // the platform's middle, from the bottom of the safe area
        static Text homeSelected, playSub;

        static void BuildMainMenu()
        {
            var body = NewScreen("Home", false, out mainMenu);
            homeBody = body;
            BuildHomeBackground((RectTransform)body.parent);

            // On the painted platform: a soft warm spotlight, a faint gold rune ring and a mint ring under the feet.
            float platY = HomePlatY;
            var glow = UiKit.Fill(body, new Color(1f, 0.88f, 0.63f, 0.07f), UiKit.Circle);
            UiKit.Place(glow, 0.5f, 0, 0, platY - 120, 760, 760);
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
            homePortrait = PortraitCircle(top.transform, 104, out homePortraitImg);
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
            homePortraitImg.texture = Portrait(id);
            LayoutHomeBackground();
            homeSelected.text = info.Name + " - " + info.Title + " selected";
            playSub.text = "3V3 · VS BOTS · " + DifficultyNames[(int)BamSettings.LastDifficulty].ToUpperInvariant();
        }

        // ---- pictures from the old game (Rickard's own, Resources/BAM/Menu) ----

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> pictures = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static Sprite fadeV, fadeH;

        static Texture2D Picture(string name)
        {
            if (pictures.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("BAM/Menu/" + name);
            if (t == null) Debug.LogError("[BAM.UI] picture missing: Resources/BAM/Menu/" + name);
            pictures[name] = t;
            return t;
        }

        static string Lower(HeroId id) => HeroCatalog.Get(id).Name.ToLowerInvariant();
        static Texture2D Portrait(HeroId id) => Picture("portrait_" + Lower(id));

        /// <summary>A round portrait with a gold ring (the old top bar's hero picture).</summary>
        static RectTransform PortraitCircle(Transform parent, float size, out RawImage img)
        {
            var ring = UiKit.Fill(parent, UiKit.Trim, UiKit.Circle);
            ring.rectTransform.sizeDelta = new Vector2(size, size);
            var mask = UiKit.Fill(ring.rectTransform, Color.white, UiKit.Circle);
            UiKit.Stretch(mask.rectTransform, 5, 5, 5, 5);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var pic = UiKit.NewRect("Portrait", mask.rectTransform);
            UiKit.Stretch(pic);
            img = pic.gameObject.AddComponent<RawImage>();
            img.raycastTarget = false;
            img.uvRect = new Rect(0.115f, 0.1f, 0.77f, 0.9f); // the 400x308 portraits: a square around the face
            return ring.rectTransform;
        }

        // The painting fills the screen behind Home like in the old game: scaled so its platform (at 47 % across, 79 % down
        // the picture) sits under the hero, a little darker, with dark fades at the top, the sides and below it.
        static void BuildHomeBackground(RectTransform screen)
        {
            fadeV = MakeFade(true);
            fadeH = MakeFade(false);
            var bgRt = UiKit.NewRect("Painting", screen);
            bgRt.SetSiblingIndex(1); // over the plain gradient, under everything else
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0, 1);
            homeBg = bgRt.gameObject.AddComponent<RawImage>();
            homeBg.texture = Picture("home_bg");
            homeBg.raycastTarget = false;
            var shade = UiKit.Fill(bgRt, new Color(0.04f, 0.055f, 0.16f, 0.2f));
            shade.name = "Shade";
            int i = 2;
            homeEdgeL = FadePart(screen, fadeH, new Color(0.024f, 0.035f, 0.12f, 0.5f), i++);
            homeEdgeR = FadePart(screen, fadeH, new Color(0.024f, 0.035f, 0.12f, 0.5f), i++);
            homeEdgeR.rectTransform.localScale = new Vector3(-1, 1, 1);
            homeTopFade = FadePart(screen, fadeV, new Color(0.04f, 0.05f, 0.15f, 0.92f), i++);
            homeBottomFade = FadePart(screen, fadeV, new Color(0.055f, 0.07f, 0.19f, 1f), i++);
            homeBottomFade.rectTransform.localScale = new Vector3(1, -1, 1);
            homeBottomFill = FadePart(screen, null, new Color(0.055f, 0.07f, 0.19f, 1f), i);
        }

        static Image FadePart(RectTransform screen, Sprite sprite, Color color, int sibling)
        {
            var img = UiKit.Fill(screen, color, sprite);
            img.transform.SetSiblingIndex(sibling);
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 1);
            return img;
        }

        // White with alpha 1 at the top (or left) fading to 0.
        static Sprite MakeFade(bool vertical)
        {
            int n = 64;
            var tex = new Texture2D(vertical ? 1 : n, vertical ? n : 1, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAM.UI.Fade" };
            for (int k = 0; k < n; k++)
            {
                float a = vertical ? k / (n - 1f) : 1f - k / (n - 1f);
                var c = new Color(1, 1, 1, a * a * (3 - 2 * a));
                if (vertical) tex.SetPixel(0, k, c); else tex.SetPixel(k, 0, c);
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static void LayoutHomeBackground()
        {
            var screen = (RectTransform)homeBody.parent;
            Vector2 size = screen.rect.size;
            float W = size.x, H = size.y;
            if (W <= 0 || H <= 0 || homeBg.texture == null) return;
            float platFromTop = H - (homeBody.anchorMin.y * H + HomePlatY);
            float iw = homeBg.texture.width, ih = homeBg.texture.height;
            float s = Mathf.Max(platFromTop / (0.786f * ih), W / iw);
            float dw = iw * s, dh = ih * s, dx = W / 2 - 0.4725f * dw, dy = platFromTop - 0.786f * dh;
            if (dy > 0) dy = 0; // never a gap at the top
            var r = homeBg.rectTransform;
            r.anchoredPosition = new Vector2(dx, -dy);
            r.sizeDelta = new Vector2(dw, dh);

            float edge = W * 0.24f;
            SetPart(homeEdgeL, -W / 2 + edge / 2, 0, edge, H);
            SetPart(homeEdgeR, W / 2 - edge / 2, 0, edge, H);
            SetPart(homeTopFade, 0, 0, W, W * 0.52f);
            float fb = Mathf.Min(-dy + dh - W * 0.24f, platFromTop + 200f);
            SetPart(homeBottomFade, 0, fb, W, W * 0.3f);
            SetPart(homeBottomFill, 0, fb + W * 0.3f - 1, W, Mathf.Max(0, H - fb - W * 0.3f + 1));
        }

        // Places a fade part by its top edge (y measured down from the screen's top), centred at x.
        static void SetPart(Image img, float x, float top, float w, float h)
        {
            var r = img.rectTransform;
            // the flipped parts (scale -1) keep their pivot, so their top is the same edge
            r.anchoredPosition = new Vector2(x, -top - (r.localScale.y < 0 ? h : 0));
            r.sizeDelta = new Vector2(w, h);
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
            // 3 cards a row on a phone held upright; on a wide screen as many as fit, so the details stay in view.
            var grid = UiKit.NewRect("Grid", content);
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.spacing = new Vector2(16, 16);
            gl.padding = new RectOffset(8, 8, 0, 0);
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.childAlignment = TextAnchor.UpperCenter;
            grid.gameObject.AddComponent<HeroGridFit>().Grid = gl;
            for (int i = 0; i < HeroCatalog.Count; i++)
            {
                int index = i;
                var info = HeroCatalog.All[i];
                var card = UiKit.NewRect(info.Name, grid);
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

                // The whole portrait in the top of the card, rounded corners, never stretched or cut.
                var holder = UiKit.NewRect("PortraitArea", card);
                holder.anchorMin = new Vector2(0, 1);
                holder.anchorMax = new Vector2(1, 1);
                holder.pivot = new Vector2(0.5f, 1);
                holder.offsetMin = new Vector2(8, -248);
                holder.offsetMax = new Vector2(-8, -8);
                var mask = UiKit.Fill(holder, Color.white, UiKit.Rounded);
                var fit = mask.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                var tex = Portrait(info.Id);
                fit.aspectRatio = tex != null ? (float)tex.width / tex.height : 1.3f;
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var pic = UiKit.NewRect("Portrait", mask.rectTransform);
                UiKit.Stretch(pic);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
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

            specialName = IconTitle(detail, out specialIcon, 14);
            specialDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            specialDesc.font = UiKit.BodyFont;
            passiveName = IconTitle(detail, out passiveIcon, 10);
            passiveDesc = DetailText(detail, 34, UiKit.TextDim, 0);
            passiveDesc.font = UiKit.BodyFont;
        }

        static Image specialIcon, passiveIcon;

        // "Special: Hammer Throw" with the ability's icon in front, like the old hero page.
        static Text IconTitle(RectTransform list, out Image icon, float spaceAbove)
        {
            var gap = UiKit.NewRect("Gap", list);
            gap.gameObject.AddComponent<LayoutElement>().preferredHeight = spaceAbove;
            var row = UiKit.NewRect("Ability", list);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 72;
            var disc = UiKit.Fill(row, new Color(0.1f, 0.11f, 0.25f), UiKit.Circle);
            UiKit.Place(disc, 0, 0.5f, 0, 0, 72, 72);
            icon = UiKit.Fill(disc.rectTransform, UiKit.Gold);
            UiKit.Stretch(icon.rectTransform, 12, 12, 12, 12);
            icon.preserveAspect = true;
            var t = UiKit.Label(row, "", 44, TextAnchor.MiddleLeft, UiKit.Gold);
            UiKit.Stretch(t.rectTransform, 90, 0, 0, 0);
            return t;
        }

        static Sprite IconSprite(string name)
        {
            var tex = Picture(name);
            if (tex == null) return null;
            if (!iconSprites.TryGetValue(name, out var sp))
            {
                sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                iconSprites[name] = sp;
            }
            return sp;
        }

        static readonly System.Collections.Generic.Dictionary<string, Sprite> iconSprites = new System.Collections.Generic.Dictionary<string, Sprite>();

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
            specialIcon.sprite = IconSprite("special_" + Lower(info.Id));
            passiveIcon.sprite = IconSprite("passive_" + Lower(info.Id));
            specialDesc.text = info.SpecialDesc;
            passiveName.text = "Passive: " + info.PassiveName;
            passiveDesc.text = info.PassiveDesc;
        }

        static void SetBar(int stat, int value)
        {
            for (int k = 0; k < 3; k++) statBars[stat, k].color = k < value ? UiKit.Gold : UiKit.Muted;
        }
    }

    /// <summary>Picks the hero grid's column count and card size from its width (3 upright, more on wide screens).</summary>
    sealed class HeroGridFit : MonoBehaviour
    {
        public GridLayoutGroup Grid;
        float lastWidth = -1;

        void OnRectTransformDimensionsChange() => Fit();
        void OnEnable() => Fit();

        void Fit()
        {
            if (Grid == null) return;
            float w = ((RectTransform)transform).rect.width;
            if (w <= 0 || Mathf.Abs(w - lastWidth) < 0.5f) return;
            lastWidth = w;
            float inner = w - Grid.padding.left - Grid.padding.right;
            int count = transform.childCount;
            int cols = Mathf.Clamp(Mathf.FloorToInt((inner + Grid.spacing.x) / (380 + Grid.spacing.x)), 3, Mathf.Max(3, count));
            Grid.constraintCount = cols;
            Grid.cellSize = new Vector2((inner - Grid.spacing.x * (cols - 1)) / cols, 370);
        }
    }
}
