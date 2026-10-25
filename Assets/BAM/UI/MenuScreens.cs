using BAM.Art;
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
        static HeroStage homeStage;
        static RawImage homeBg, homePortraitImg;
        static Image homeTopFade, homeBottomFade, homeBottomFill, homeEdgeL, homeEdgeR;
        const float HomePlatY = NavH + 24 + 200 + 260; // the platform's middle, from the bottom of the safe area
        static Text homeSelected, playSub;

        static void BuildMainMenu()
        {
            var body = NewScreen("Home", false, out mainMenu);
            homeBody = body;
            BuildHomeBackground(body);

            // On the painted platform: a warm glow and a gold rune circle, seen at a slant, centred on platY.
            float platY = HomePlatY;
            var glow = UiKit.Fill(body, new Color(1f, 0.78f, 0.4f, 0.35f), Glow);
            PlaceOnPlatform(glow.rectTransform, platY, 980, 320);
            var rune = UiKit.Fill(body, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.9f), RuneCircle);
            PlaceOnPlatform(rune.rectTransform, platY, 820, 250);

            // The hero, big and in the middle: Rickard's 3D model filmed by its own camera (HeroStage); the letter badge
            // only if that fails. The camera sees 1.6 units from 0.1 below the feet, so even the tallest hero keeps air above
            // its head and the feet land 6.25 % up the picture, on the circle's middle.
            homeHero = UiKit.HeroBadge(body, HeroId.Bram);
            UiKit.Place(homeHero, 0.5f, 0, 0, platY - 20, 440, 440);
            var stageRt = UiKit.NewRect("Hero3D", body);
            UiKit.Place(stageRt, 0.5f, 0, 0, platY - 1080 * 0.0625f, 1080, 1080);
            homeStage = stageRt.gameObject.AddComponent<HeroStage>();

            // Top bar: a rounded glass pill with a gold edge; portrait, the game's name, the chosen hero.
            var top = UiKit.Panel(body, new Color(0.055f, 0.075f, 0.094f, 0.72f));
            top.pixelsPerUnitMultiplier = 0.45f;
            UiKit.Band(top, true, 16, 130, 16);
            var topEdge = UiKit.Frame(top.rectTransform);
            topEdge.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.6f);
            topEdge.pixelsPerUnitMultiplier = 0.45f;
            homePortrait = PortraitCircle(top.transform, 104, out homePortraitImg);
            UiKit.Place(homePortrait, 0, 0.5f, 14, 0, 104, 104);
            var name = UiKit.Label(top.transform, BamInfo.ShortName, 52, TextAnchor.LowerLeft, UiKit.TextColor);
            HeaderText(name, 140, 0, 64);
            UiKit.AddShadow(name, 3);
            homeSelected = UiKit.Label(top.transform, "", 32, TextAnchor.UpperLeft, UiKit.Gold);
            HeaderText(homeSelected, 140, -46, 46);

            // Upper right: a round dark glass button with a matte white gear (the only side button the beta needs).
            var gearRt = UiKit.NewRect("Settings", body);
            UiKit.Place(gearRt, 1, 1, -24, -170, 112, 112);
            var gearBg = gearRt.gameObject.AddComponent<Image>();
            gearBg.sprite = UiKit.Circle;
            gearBg.color = UiKit.Muted;
            var gearEdge = UiKit.Fill(gearRt, new Color(1, 1, 1, 0.22f), UiKit.CircleRing);
            gearEdge.name = "Edge";
            var gearIcon = UiKit.NewRect("Icon", gearRt);
            UiKit.Place(gearIcon, 0.5f, 0.5f, 0, 0, 64, 64);
            DrawIcon(gearIcon, 2);
            for (int k = 0; k < gearIcon.childCount; k++)
            {
                var part = gearIcon.GetChild(k).GetComponent<Image>();
                if (part != null && part.color.r + part.color.g + part.color.b > 0.4f) part.color = new Color(0.9f, 0.91f, 0.94f, 0.9f);
            }
            var gearButton = gearRt.gameObject.AddComponent<Button>();
            gearButton.targetGraphic = gearBg;
            gearButton.transition = Selectable.Transition.None;
            gearButton.navigation = new Navigation { mode = Navigation.Mode.None };
            gearButton.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); ShowTab(2); });
            gearRt.gameObject.AddComponent<ButtonFx>();

            // Bottom row: MODES and ENTER ARENA.
            var row = UiKit.NewRect("Bottom row", body);
            UiKit.Band(row, false, NavH + 24, 220, 24);
            var modesButton = UiKit.Button(row, "MODES", () => Push(modes), UiKit.Muted, SfxId.UiTap, 30);
            UiKit.Place(modesButton, 0, 0.5f, 0, 0, 210, 210);
            // gold-framed glass
            var modesEdge = UiKit.Frame(modesButton.targetGraphic.rectTransform);
            modesEdge.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.8f);
            var modesLabel = UiKit.ButtonText(modesButton);
            modesLabel.alignment = TextAnchor.LowerCenter;
            modesLabel.rectTransform.offsetMin = new Vector2(0, 14);
            RobotIcon(modesButton.targetGraphic.rectTransform);

            // ENTER ARENA: big glossy gold with a warm glow round it.
            var playGlow = UiKit.Fill(row, new Color(1f, 0.75f, 0.35f, 0.45f), Glow);
            playGlow.rectTransform.offsetMin = new Vector2(190, -50);
            playGlow.rectTransform.offsetMax = new Vector2(40, 50);
            var play = UiKit.Button(row, "ENTER ARENA", OnPlay, UiKit.Orange, SfxId.UiConfirm, 76);
            var playRt = (RectTransform)play.transform;
            playRt.anchorMin = new Vector2(0, 0);
            playRt.anchorMax = new Vector2(1, 1);
            playRt.offsetMin = new Vector2(234, 0);
            playRt.offsetMax = Vector2.zero;
            var shine = UiKit.Fill(play.targetGraphic.rectTransform, new Color(1f, 0.93f, 0.7f, 0.55f), UiKit.Rounded);
            shine.rectTransform.anchorMin = new Vector2(0, 0.45f);
            shine.rectTransform.offsetMin = new Vector2(4, 0);
            shine.rectTransform.offsetMax = new Vector2(-4, -4);
            shine.transform.SetSiblingIndex(0);
            var playEdge = UiKit.Frame(play.targetGraphic.rectTransform);
            playEdge.color = new Color(1f, 0.92f, 0.7f, 0.9f);
            var playLabel = UiKit.ButtonText(play);
            playLabel.rectTransform.offsetMin = new Vector2(12, 52);
            playLabel.horizontalOverflow = HorizontalWrapMode.Wrap; // shrinks to fit on narrow phones
            playLabel.verticalOverflow = VerticalWrapMode.Truncate;
            playLabel.resizeTextForBestFit = true;
            playLabel.resizeTextMinSize = 40;
            playLabel.resizeTextMaxSize = 76;
            playSub = UiKit.Label(play.targetGraphic.transform, "", 30, TextAnchor.MiddleCenter, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.75f));
            UiKit.Band(playSub, false, 22, 44, 10);
            playSub.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        // The MODES icon: a small gold robot head.
        static void RobotIcon(RectTransform face)
        {
            var head = UiKit.Fill(face, UiKit.Primary, UiKit.Rounded);
            head.pixelsPerUnitMultiplier = 2.5f;
            UiKit.Place(head, 0.5f, 1, 0, -50, 76, 60);
            var stalk = UiKit.Fill(face, UiKit.Primary);
            UiKit.Place(stalk, 0.5f, 1, 0, -36, 6, 16);
            var knob = UiKit.Fill(face, UiKit.Primary, UiKit.Circle);
            UiKit.Place(knob, 0.5f, 1, 0, -26, 14, 14);
            var dark = new Color(0.055f, 0.075f, 0.094f);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark, UiKit.Circle), 0.5f, 0.5f, -16, 4, 14, 14);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark, UiKit.Circle), 0.5f, 0.5f, 16, 4, 14, 14);
            UiKit.Place(UiKit.Fill(head.rectTransform, dark), 0.5f, 0.5f, 0, -14, 30, 5);
        }

        static void RefreshHome()
        {
            var id = (HeroId)BamSettings.LastHero;
            var info = HeroCatalog.Get(id);
            bool shown = homeStage.Show(id);
            homeHero.gameObject.SetActive(!shown);
            if (!shown) SetBadge(homeHero, id);
            homePortraitImg.texture = Portrait(id);
            LayoutHomeBackground();
            homeSelected.text = info.Name + " · " + info.Title;
            playSub.text = "3V3 · VS BOTS · " + DifficultyNames[(int)BamSettings.LastDifficulty].ToUpperInvariant();
        }

        // ---- pictures from the old game (Rickard's own, Resources/BAM/Menu) ----

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> pictures = new System.Collections.Generic.Dictionary<string, Texture2D>();
        static Sprite fadeV, fadeH, glowSprite, runeSprite;

        static void PlaceOnPlatform(RectTransform rt, float platY, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, platY - h / 2);
            rt.sizeDelta = new Vector2(w, h);
        }

        /// <summary>A soft round glow: white in the middle fading to nothing at the edge.</summary>
        static Sprite Glow
        {
            get
            {
                if (glowSprite != null) return glowSprite;
                const int n = 128;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BAM.UI.Glow" };
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float a = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                    tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
                tex.Apply(false, true);
                glowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return glowSprite;
            }
        }

        /// <summary>A gold summoning circle: two outer rings with tick marks, an inner ring and small rune dots.</summary>
        static Sprite RuneCircle
        {
            get
            {
                if (runeSprite != null) return runeSprite;
                const int n = 512;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BAM.UI.Rune" };
                var px = new Color32[n * n];
                float px1 = 2f / n; // one pixel in radius units
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 180f;
                    float a = 0;
                    a = Mathf.Max(a, Band(r, 0.975f, 0.012f, px1));
                    a = Mathf.Max(a, Band(r, 0.93f, 0.006f, px1));
                    a = Mathf.Max(a, Band(r, 0.72f, 0.01f, px1));
                    a = Mathf.Max(a, Band(r, 0.5f, 0.005f, px1) * 0.8f);
                    // ticks between the two outer rings, every 7.5 degrees
                    if (r > 0.935f && r < 0.97f)
                    {
                        float t = Mathf.Abs(Mathf.Repeat(ang + 3.75f, 7.5f) - 3.75f) * Mathf.Deg2Rad * r;
                        a = Mathf.Max(a, Mathf.Clamp01(1 - (t - 0.004f) / px1));
                    }
                    // rune dots between the middle rings, every 30 degrees, and a star of lines inside
                    float seg = Mathf.Abs(Mathf.Repeat(ang + 15f, 30f) - 15f) * Mathf.Deg2Rad;
                    float ddot = Mathf.Sqrt((r - 0.82f) * (r - 0.82f) + (seg * r) * (seg * r));
                    a = Mathf.Max(a, Mathf.Clamp01(1 - (ddot - 0.03f) / px1));
                    if (r < 0.72f && r > 0.5f)
                    {
                        float line = Mathf.Abs(Mathf.Repeat(ang, 60f) - 30f) * Mathf.Deg2Rad * r;
                        a = Mathf.Max(a, Mathf.Clamp01(1 - (line - 0.003f) / px1) * 0.6f);
                    }
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
                tex.SetPixels32(px);
                tex.Apply(true, true);
                runeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return runeSprite;
            }
        }

        // 1 inside a ring of half-width w around radius c, smoothed over one pixel.
        static float Band(float r, float c, float w, float px1) => Mathf.Clamp01(1 - (Mathf.Abs(r - c) - w) / px1);

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
            var ring = UiKit.Fill(parent, UiKit.Gold, UiKit.Circle);
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
        static RectTransform homeBackdrop;

        // Everything sits in the standing frame (the phone-shaped middle of a wide window), clipped to it.
        static void BuildHomeBackground(RectTransform body)
        {
            fadeV = MakeFade(true);
            fadeH = MakeFade(false);
            var screen = UiKit.NewRect("Backdrop", body);
            UiKit.Stretch(screen);
            screen.SetSiblingIndex(0); // under everything else on Home
            screen.gameObject.AddComponent<RectMask2D>();
            screen.gameObject.AddComponent<SizeWatch>().Changed = LayoutHomeBackground;
            homeBackdrop = screen;
            var bgRt = UiKit.NewRect("Painting", screen);
            bgRt.anchorMin = bgRt.anchorMax = bgRt.pivot = new Vector2(0, 1);
            homeBg = bgRt.gameObject.AddComponent<RawImage>();
            homeBg.texture = Picture("home_bg");
            homeBg.raycastTarget = false;
            var shade = UiKit.Fill(bgRt, new Color(0.055f, 0.075f, 0.094f, 0.2f));
            shade.name = "Shade";
            int i = 1;
            homeEdgeL = FadePart(screen, fadeH, new Color(0.03f, 0.04f, 0.05f, 0.5f), i++);
            homeEdgeR = FadePart(screen, fadeH, new Color(0.03f, 0.04f, 0.05f, 0.5f), i++);
            homeEdgeR.rectTransform.localScale = new Vector3(-1, 1, 1);
            homeTopFade = FadePart(screen, fadeV, new Color(0.055f, 0.075f, 0.094f, 0.92f), i++);
            homeBottomFade = FadePart(screen, fadeV, new Color(0.055f, 0.075f, 0.094f, 1f), i++);
            homeBottomFade.rectTransform.localScale = new Vector3(1, -1, 1);
            homeBottomFill = FadePart(screen, null, new Color(0.055f, 0.075f, 0.094f, 1f), i);
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
            if (homeBackdrop == null || homeBg == null) return;
            Vector2 size = homeBackdrop.rect.size;
            float W = size.x, H = size.y;
            if (W <= 0 || H <= 0 || homeBg.texture == null) return;
            float platFromTop = H - HomePlatY;
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

                // The "selected" mark: a gold circle with a dark tick in the top right corner.
                var check = UiKit.Fill(card, UiKit.Primary, UiKit.Circle);
                UiKit.Place(check, 1, 1, 10, 10, 56, 56);
                var tick1 = UiKit.Fill(check.rectTransform, UiKit.Ink);
                UiKit.Place(tick1, 0.5f, 0.5f, -8, -3, 16, 6).localRotation = Quaternion.Euler(0, 0, -45);
                var tick2 = UiKit.Fill(check.rectTransform, UiKit.Ink);
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
            UiKit.Frame(detail);
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
            var disc = UiKit.Fill(row, UiKit.Muted, UiKit.Circle);
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
                cardFrames[i].color = on ? UiKit.Gold : UiKit.Trim;
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

    /// <summary>
    /// Shows one hero's 3D model (ArtKit.CreateHeroPreview) in the UI: a small camera far from the arena films it into a
    /// RenderTexture drawn by a RawImage. The camera only runs while this is visible.
    /// </summary>
    sealed class HeroStage : MonoBehaviour
    {
        static readonly Vector3 Spot = new Vector3(500, 0, 500);
        const int Size = 768;

        RawImage image;
        Camera cam;
        RenderTexture rt;
        UnitView view;
        HeroId shownId;
        bool hasHero;

        /// <summary>Shows the hero; false if the model could not be made (the caller shows a fallback).</summary>
        public bool Show(HeroId id)
        {
            if (hasHero && id == shownId) return true;
            Setup();
            if (hasHero) { ArtKit.Release(view); view = null; hasHero = false; }
            try { view = ArtKit.CreateHeroPreview(id); }
            catch (System.Exception e) { Debug.LogException(e); view = null; }
            if (view == null) { image.enabled = false; return false; }
            view.transform.SetPositionAndRotation(Spot, Quaternion.Euler(0, 160, 0)); // faces the camera, a little turned
            view.SetMoving(false, 0);
            shownId = id;
            hasHero = true;
            image.enabled = true;
            cam.enabled = isActiveAndEnabled;
            return true;
        }

        void Setup()
        {
            if (cam != null) return;
            image = gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "BAM.UI.HeroStage", antiAliasing = 4 };
            image.texture = rt;
            var go = new GameObject("BAM.HeroStageCamera");
            DontDestroyOnLoad(go);
            cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 30;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 8;
            cam.targetTexture = rt;
            cam.depth = -50;
            // Level with the hero's chest, straight in front (it looks toward -z): 30 degrees at 3 units shows y -0.1 to 1.5.
            go.transform.position = Spot + new Vector3(0, 0.7f, -3f);
            go.transform.rotation = Quaternion.identity;
            cam.enabled = false;
        }

        void OnEnable() { if (cam != null) cam.enabled = hasHero; }
        void OnDisable() { if (cam != null) cam.enabled = false; }
    }

    /// <summary>Calls back when its RectTransform changes size (the window or the standing frame changed).</summary>
    sealed class SizeWatch : MonoBehaviour
    {
        public System.Action Changed;
        void OnRectTransformDimensionsChange() => Changed?.Invoke();
    }
}
