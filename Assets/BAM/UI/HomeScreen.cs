using System.Collections.Generic;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BAM.UI
{
    // Home as an exact port of the old game's Home v3 with ONE_PLAY (ABC2 MenuHome3.cs HubHome3/HubSpot3/HubAura3/HubRing3/
    // HubTopBar3/HubModeBtn, MenuHome.cs PlayButton, MenuButtonFx.cs), Rickard 2026-10-26: "precis som i andra projektet".
    // The old code drew in cw units (cw(v) = v % of the width). Here everything is built once at a design width of 1080
    // (cw = 10.8) in two groups scaled to the frame's width: the top bar from the top, and the hero and the bottom row
    // from the top of the tab bar. Left out until the features exist: currencies, rank and bell. The party places and the name tags: HomeParty.cs.
    // 2026-10-31 (Rickard, "nu ska vi utveckla och förbättra"): the top bar without its glass pill and without the gear (Settings
    // is a tab), the MODES square became a mode chip over PLAY, PLAY is as wide as the screen, the bottom is darker, and PLAY
    // carries a "FIRST WIN TODAY" badge while the day's first-win XP is still there. Hero size, fonts, icons and INVITE as before.
    public static partial class Screens
    {
        static HomeFx homeFx;
        static HeroStage homeStage;
        static RectTransform homeHero; // the letter badge, only if the 3D model could not be made
        static RawImage homePortraitImg;
        static Text homeName, homeTitle, playSub; // playSub: the mode chip's text over PLAY (SetPlaySub)
        static RectTransform homeModeChip, homePlayLabel;
        static Text homeFirstWinText; // "FIRST WIN TODAY +50 XP" under PLAY, as the old sub line (SetPlaySub)
        static RectTransform homeModeIcon;
        static float homeModeIconSize;
        static int homeModeShown = -1;
        static GameObject homeTutCard;

        const float Cw = 10.8f;                 // cw(1) at the design width
        const float HeroH = 70 * Cw;            // the old heroH: min(cw(70), room under the top bar), always cw(70) on a phone
        const float BrowBottom = 4 * Cw, BrowH = 17 * Cw;
        const float FeetY = BrowBottom + BrowH + 20 * Cw; // the hero's feet, up from the tab bar's top
        static readonly Color HubGold = UiKit.Trim;                          // #d9a441
        static readonly Color HubText = new Color(0.95f, 0.96f, 1f);
        static readonly Color HubSoft = new Color32(0xcd, 0xd3, 0xfa, 0xff); // #cdd3fa
        static readonly Color HubGlass = new Color32(14, 18, 48, 255);

        static void BuildMainMenu()
        {
            var body = NewScreen("Home", false, out mainMenu);
            BuildHomeBackground(body);
            homeFx = body.gameObject.AddComponent<HomeFx>();
            homeFx.Build(body);
        }

        static void RefreshHome()
        {
            ClampHero(); // a hero that is locked for this account (HeroLocks.cs)
            var id = (HeroId)BamSettings.LastHero;
            var info = HeroCatalog.Get(id);
            bool shown = homeStage.Show(id);
            homeHero.gameObject.SetActive(!shown);
            if (!shown) SetBadge(homeHero, id);
            homePortraitImg.texture = Portrait(id);
            homePortraitImg.enabled = homePortraitImg.texture != null;
            homeName.text = info.Name;
            homeTitle.text = info.Title;
            SetPlaySub();
            if (homeModeShown != (int)homeMode)
            {
                homeModeShown = (int)homeMode;
                for (int i = homeModeIcon.childCount - 1; i >= 0; i--) Object.Destroy(homeModeIcon.GetChild(i).gameObject);
                ModeIcon(homeModeIcon, homeMode, homeModeIconSize);
            }
            homeTutCard.SetActive(BamSettings.TutorialCard);
            homeFx.Layout();
        }

        // ---- the painted background (HomeBg): scaled so its platform sits just above the hero's feet ----

        static readonly Dictionary<string, Texture2D> pictures = new Dictionary<string, Texture2D>();
        static RectTransform homeBackdrop;
        static RawImage homeBg;
        static Image homeTopFade, homeBottomFade, homeBottomFill, homeEdgeL, homeEdgeR, homeMidFade, homeSky, homeMelt;
        static float homePlatY, homeBrowY; // the platform's middle and the bottom row's top, up from the frame's bottom

        static void BuildHomeBackground(RectTransform body)
        {
            var fadeV = MakeFade(true);
            var fadeH = MakeFade(false);
            var screen = UiKit.NewRect("Backdrop", body);
            UiKit.Stretch(screen);
            screen.SetSiblingIndex(1); // over the plain gradient, under everything else on Home
            screen.gameObject.AddComponent<RectMask2D>();
            homeBackdrop = screen;
            var bgRt = UiKit.NewRect("Painting", screen);
            bgRt.anchorMin = bgRt.anchorMax = new Vector2(0, 1);
            bgRt.pivot = new Vector2(0.4725f, 1 - 0.786f); // on the platform, so the slow breath keeps it in place
            homeBg = bgRt.gameObject.AddComponent<RawImage>();
            homeBg.texture = Picture("home_bg");
            homeBg.raycastTarget = false;
            UiKit.Fill(bgRt, new Color(10 / 255f, 14 / 255f, 40 / 255f, 0.2f)).name = "Shade";
            // zoomed out for a party (HomeParty.cs) the painting's top edge can come into view: the dark sky above it, and the
            // edge melting into it (HomeBg, dy > 0)
            var sky = new Color(14 / 255f, 18 / 255f, 48 / 255f); // HUB_BG0
            homeSky = FadePart(screen, null, sky);
            homeMelt = FadePart(screen, fadeV, sky);
            homeSky.enabled = homeMelt.enabled = false;
            var edge = new Color(6 / 255f, 9 / 255f, 30 / 255f, 0.6f); // a little darker than the old 0.5: the hero stands out more
            homeEdgeL = FadePart(screen, fadeH, edge);
            homeEdgeR = FadePart(screen, fadeH, edge);
            homeEdgeR.rectTransform.localScale = new Vector3(-1, 1, 1);
            homeMidFade = FadePart(screen, fadeV, new Color(6 / 255f, 9 / 255f, 30 / 255f, 0.22f));
            homeTopFade = FadePart(screen, fadeV, new Color(10 / 255f, 13 / 255f, 38 / 255f, 0.92f));
            var bg0 = new Color(14 / 255f, 18 / 255f, 48 / 255f); // HUB_BG0 #0e1230
            homeBottomFade = FadePart(screen, fadeV, bg0);
            homeBottomFade.rectTransform.localScale = new Vector3(1, -1, 1);
            homeBottomFill = FadePart(screen, null, bg0);
        }

        static Image FadePart(RectTransform screen, Sprite sprite, Color color)
        {
            var img = UiKit.Fill(screen, color, sprite);
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
            float W = size.x, H = size.y, cw = W / 100f;
            if (W <= 0 || H <= 0 || homeBg.texture == null) return;
            float platY = H - homePlatY; // down from the top, as in the old code
            float iw = homeBg.texture.width, ih = homeBg.texture.height;
            float s = Mathf.Max(platY / (0.786f * ih), W / iw);
            float dw = iw * s, dh = ih * s, dx = W / 2 - 0.4725f * dw, dy = platY - 0.786f * dh;
            if (dy > 0) dy = 0; // never a gap at the top
            var r = homeBg.rectTransform;
            r.anchoredPosition = new Vector2(dx + 0.4725f * dw, -(dy + 0.786f * dh));
            r.sizeDelta = new Vector2(dw, dh);

            float ew = cw * 24;
            SetPart(homeEdgeL, -W / 2 + ew / 2, 0, ew, H);
            SetPart(homeEdgeR, W / 2 - ew / 2, 0, ew, H);
            SetPart(homeMidFade, 0, 0, W, cw * 70); // from the very top: starting at cw(20) it drew a hard line across the sky (Rickard's screenshot 2026-10-31)
            SetPart(homeTopFade, 0, 0, W, cw * 52);
            // dark from the mode chip down (it sits cw(2..10) over the bottom row), fading out just under the hero's feet
            float fh = cw * 18, fb = Mathf.Min(dy + dh - cw * 24, (H - homeBrowY) - cw * 6 - fh);
            SetPart(homeBottomFade, 0, fb, W, fh);
            SetPart(homeBottomFill, 0, fb + fh - 1, W, Mathf.Max(0, H - fb - fh + 1));
        }

        /// <summary>The sky over the painting while it is zoomed out (scale = its scale now) so far that its top edge shows.</summary>
        static void LayoutSky(float scale)
        {
            if (homeSky == null) return;
            var r = homeBg.rectTransform;
            float W = homeBackdrop.rect.width, top = -r.anchoredPosition.y - 0.786f * r.sizeDelta.y * scale;
            bool show = top > 0;
            homeSky.enabled = homeMelt.enabled = show;
            if (!show) return;
            SetPart(homeSky, 0, 0, W, top + 1);
            SetPart(homeMelt, 0, top - 1, W, W * 0.18f + 1);
        }

        // Places a fade part by its top edge (y measured down from the screen's top), centred at x.
        static void SetPart(Image img, float x, float top, float w, float h)
        {
            var r = img.rectTransform;
            // the flipped parts (scale -1) keep their pivot, so their top is the same edge
            r.anchoredPosition = new Vector2(x, -top - (r.localScale.y < 0 ? h : 0));
            r.sizeDelta = new Vector2(w, h);
        }

        static Texture2D Picture(string name, bool optional = false)
        {
            if (pictures.TryGetValue(name, out var t)) return t;
            t = Resources.Load<Texture2D>("BAM/Menu/" + name);
            if (t == null && !optional) Debug.LogError("[BAM.UI] picture missing: Resources/BAM/Menu/" + name);
            pictures[name] = t;
            return t;
        }

        static string Lower(HeroId id) => HeroCatalog.Get(id).Name.ToLowerInvariant();
        // A new hero (Bonk) can come before his portrait: then the dark picture box stays empty, like Speltest's Custom card.
        static Texture2D Portrait(HeroId id) => Picture("portrait_" + Lower(id), true);

        /// <summary>
        /// Builds Home's pieces, lays them out when the frame changes and runs the old per-frame motion: the breathing
        /// background, the turning rune circle, the aura and its motes, and PLAY's glow, shine and spring. No allocations.
        /// </summary>
        sealed partial class HomeFx : MonoBehaviour
        {
            RectTransform body, top, low, stage; // stage: the heroes on the platform, zoomed out with the party (HomeParty.cs)
            CanvasGroup rune, aura;
            readonly RectTransform[] marks = new RectTransform[12];
            readonly Image[] moteCore = new Image[7], moteGlow = new Image[7];
            RectTransform playScale, modesFace, shine;
            Image playGlow, innerGlow, playDark, shineImg;
            HomePress playPress, modesPress;
            float spring = 1, springV, shineAt = -9;
            bool playWasDown;
            const float PlayW = 1080 - 8 * Cw, PlayInnerH = BrowH - 2 * 0.7f * Cw;
            const float ChipH = 8 * Cw, ChipGap = 2.2f * Cw, ChipIcon = 5.4f * Cw;

            public void Build(RectTransform body)
            {
                this.body = body;
                top = Group("Top", 1);
                low = Group("Low", 0);
                stage = UiKit.NewRect("Stage", low);
                At(stage, 540, FeetY, 1080, 1800, 0.5f, FeetY / 1800f); // the same rect as low, scaled around the hero's feet
                BuildPartySlots(); // the party members first: they stand behind the player's hero
                BuildStage();
                BuildRow();
                OnlineFriendsPill(low, BrowBottom + BrowH); // online builds only
                invitePills = OnlineInvitePills(low, FeetY, HeroH); // online builds only
                BuildTopBar();
                BuildTutCard();
                BuildPartyTags();
                top.SetAsLastSibling(); // the top bar over the hero
                Layout();
            }

            RectTransform Group(string name, float y)
            {
                var g = UiKit.NewRect(name, body);
                g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, y);
                return g;
            }

            void OnRectTransformDimensionsChange() { if (low != null) Layout(); }

            public void Layout()
            {
                Vector2 size = body.rect.size;
                if (size.x <= 0 || size.y <= 0) return;
                float s = size.x / 1080f;
                top.localScale = low.localScale = new Vector3(s, s, 1);
                top.sizeDelta = new Vector2(1080, 30 * Cw);
                top.anchoredPosition = Vector2.zero;
                low.sizeDelta = new Vector2(1080, 1800);
                low.anchoredPosition = new Vector2(0, NavH);
                homePlatY = NavH + (FeetY + 6 * Cw) * s;
                homeBrowY = NavH + (BrowBottom + BrowH) * s;
                LayoutHomeBackground();
                homeStage.Frame();
                FrameParty();
            }

            // ---- the hero on the platform: spotlight, rune circle, aura, ring back, the model, ring front ----

            void BuildStage()
            {
                // The gold rune circle on the ground. HubSpot3's spotlight cone is left out: Rickard found the light triangle ugly (2026-10-28).
                rune = UiKit.NewRect("Rune", stage).gameObject.AddComponent<CanvasGroup>();
                rune.blocksRaycasts = false;
                var runeRt = (RectTransform)rune.transform;
                At(runeRt, 540, FeetY, 0, 0);
                float rx = 0.4f * HeroH;
                Ellipse(runeRt, rx, 0.3f * Cw, HubGold, 0);
                Ellipse(runeRt, rx * 0.86f, 0.3f * Cw, HubGold, 0);
                float k = 0.8f * Cw;
                for (int i = 0; i < 12; i++)
                {
                    Image m;
                    if (i % 3 == 0)
                    {
                        m = Img(runeRt, "Mark", HomeArt.Diamond, HubGold);
                        Size(m.rectTransform, 1.2f * k + 0.3f * Cw, k + 0.3f * Cw);
                    }
                    else
                    {
                        m = Img(runeRt, "Mark", null, HubGold);
                        Size(m.rectTransform, Mathf.Sqrt(k * k + 0.49f * k * k), 0.3f * Cw);
                        m.rectTransform.localRotation = Quaternion.Euler(0, 0, -35);
                    }
                    marks[i] = m.rectTransform;
                }

                // HubAura3: a soft mint glow on the ground and seven motes of light rising
                aura = UiKit.NewRect("Aura", stage).gameObject.AddComponent<CanvasGroup>();
                aura.blocksRaycasts = false;
                var auraRt = (RectTransform)aura.transform;
                At(auraRt, 540, FeetY, 0, 0);
                Size(Img(auraRt, "Glow", HomeArt.AuraGlow, Color.white).rectTransform, 2 * 0.48f * HeroH, 2 * 0.13f * HeroH);
                for (int i = 0; i < 7; i++)
                {
                    float r = (0.45f + 0.35f * ((i * 0.57f) % 1f)) * Cw;
                    moteGlow[i] = Img(stage, "MoteGlow", UiKit.Circle, Color.clear);
                    At(moteGlow[i].rectTransform, 540, FeetY, r * 4.8f, r * 4.8f);
                    moteCore[i] = Img(stage, "Mote", UiKit.Circle, Color.clear);
                    At(moteCore[i].rectTransform, 540, FeetY, r * 2, r * 2);
                }

                // HubRing3: a soft shadow, the contact shadow under the feet and the back half of the mint ring
                float ringX = 0.3f * HeroH, ringY = 0.075f * HeroH;
                var shadow = Img(stage, "Shadow", HomeArt.RingShadow, Color.white);
                At(shadow, 540, FeetY, 2 * ringX * 1.1f, 2 * ringY * 1.4f);
                var contact = Img(stage, "Contact", HomeArt.ContactShadow, Color.white);
                At(contact, 540, FeetY, 2 * ringX * 0.55f, 2 * ringY * 0.75f);
                var ringBack = UiKit.NewRect("RingBack", stage);
                At(ringBack, 540, FeetY, 0, 0);
                if (!RingPicture(ringBack, ringX, true))
                {
                    Half(Ellipse(ringBack, ringX, 1.8f * Cw, Fade(UiKit.Primary, 0.3f), 0), true);
                    Half(Ellipse(ringBack, ringX, 0.65f * Cw, UiKit.Primary, 0), true);
                }

                // the hero: Rickard's 3D model on a picture as wide as the screen (HubHero3), so no weapon is cut off
                homeHero = UiKit.HeroBadge(stage, HeroId.Bram);
                At(homeHero, 540, FeetY + 0.3f * HeroH, 0.6f * HeroH, 0.6f * HeroH);
                var pic = UiKit.NewRect("Hero3D", stage);
                At(pic, 540, FeetY - 0.1f * HeroH, 1080, 1.35f * HeroH, 0.5f, 0);
                var picture = pic.gameObject.AddComponent<RawImage>();
                picture.raycastTarget = false;
                picture.enabled = false;

                var ringFront = UiKit.NewRect("RingFront", stage);
                At(ringFront, 540, FeetY, 0, 0);
                if (!RingPicture(ringFront, ringX, false))
                {
                    Half(Ellipse(ringFront, ringX, 1.8f * Cw, Fade(UiKit.Primary, 0.3f), 0), false);
                    Half(Ellipse(ringFront, ringX, 0.65f * Cw, UiKit.Primary, 0), false);
                }

                // a tap on the hero opens Heroes; a drag sideways turns him (the old hubHero button, cw(40) wide)
                var hit = UiKit.NewRect("HeroHit", stage);
                At(hit, 540, FeetY - 8 * Cw, 40 * Cw, HeroH + 10 * Cw, 0.5f, 0);
                hit.gameObject.AddComponent<Image>().color = Color.clear;
                homeStage = hit.gameObject.AddComponent<HeroStage>();
                homeStage.Picture = picture;
                homeStage.FeetAt = 0.1f / 1.35f;
                homeStage.HeroFill = 1f / 1.35f;
                homeStage.Tapped = () => { BamAudio.Play(SfxId.UiTap); ShowTab(1); };
            }

            // ---- the bottom row: the mode chip and PLAY ----

            void BuildRow()
            {
                // the mode chip (was the MODES square, HubModeBtn): the mode's own icon, then its name in gold and what it is,
                // and a small ▾; a tap opens MODES. Its width follows the text (SetPlaySub), at most to FRIENDS' round button.
                float r = ChipH / 2;
                homeModeChip = UiKit.NewRect("Modes", low);
                At(homeModeChip, 4 * Cw, BrowBottom + BrowH + ChipGap, 60 * Cw, ChipH, 0, 0); // on PLAY's left edge, clear of FRIENDS on the right
                var shadow = Img(homeModeChip, "Shadow", UiKit.Rounded, new Color(0, 0, 0, 0.35f));
                Round(shadow, r);
                UiKit.Stretch(shadow.rectTransform, 0, 0.6f * Cw, 0, -0.6f * Cw);
                modesFace = UiKit.NewRect("Face", homeModeChip);
                UiKit.Stretch(modesFace);
                Round(Img(modesFace, "Fill", UiKit.Rounded, new Color(HubGlass.r, HubGlass.g, HubGlass.b, 0.9f)), r);
                RoundRing(Img(modesFace, "Gold", HomeArt.RoundRing(0.3f * Cw / r), Fade(HubGold, 0.6f)), r);
                homeModeIcon = UiKit.NewRect("ModeIcon", modesFace);
                homeModeIcon.anchorMin = homeModeIcon.anchorMax = homeModeIcon.pivot = new Vector2(0, 0.5f);
                homeModeIcon.anchoredPosition = new Vector2(1.4f * Cw, 0);
                homeModeIcon.sizeDelta = new Vector2(ChipIcon, ChipIcon);
                homeModeIconSize = ChipIcon;
                playSub = Lil(modesFace, "", 3f, HubSoft);
                playSub.supportRichText = true;
                playSub.alignment = TextAnchor.MiddleLeft;
                var lr = playSub.rectTransform;
                lr.anchorMin = new Vector2(0, 0); lr.anchorMax = new Vector2(1, 1); lr.pivot = new Vector2(0, 0.5f);
                lr.offsetMin = new Vector2(1.4f * Cw + ChipIcon + 1.4f * Cw, 0); lr.offsetMax = new Vector2(-5 * Cw, 0);
                // the ▾: a small chevron turned down
                var down = UiKit.NewRect("Down", modesFace);
                down.anchorMin = down.anchorMax = down.pivot = new Vector2(1, 0.5f);
                down.anchoredPosition = new Vector2(-2.6f * Cw, 0); down.sizeDelta = Vector2.zero;
                down.localRotation = Quaternion.Euler(0, 0, -90);
                Chevron(down, 0, 0, 0.9f * Cw, 0.45f * Cw, HubSoft);
                var hit = homeModeChip.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var b = homeModeChip.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); Push(modes); });
                homeModeChip.gameObject.AddComponent<ButtonFx>().PressScale = false;
                modesPress = homeModeChip.gameObject.AddComponent<HomePress>();

                BuildPlay(4 * Cw);
            }

            // PlayButton: Rickard's orange button image in the rounded shape with a dark border, a warm pulsing glow, a shine
            // that sweeps after a release and every 4 s, and a spring on press (MenuButtonFx.cs).
            void BuildPlay(float x)
            {
                float w = PlayW, h = BrowH, r = Mathf.Min(h * 0.32f, 5 * Cw), b = 0.7f * Cw;
                var rt = UiKit.NewRect("Play", low);
                At(rt, x + w / 2, BrowBottom + h / 2, w, h);
                playScale = rt;
                var shadow = Img(rt, "Shadow", UiKit.Rounded, new Color(0, 0, 0, 0.45f));
                Round(shadow, r);
                UiKit.Stretch(shadow.rectTransform, 0, Cw, 0, -Cw);
                playGlow = Img(rt, "Glow", HomeArt.PlayGlow, Color.white);
                Size(playGlow.rectTransform, 1.08f * w, 2 * 0.95f * h); // PLAY is screen wide now: the old 1.24 w glowed out past the screen's sides
                Round(Img(rt, "Edge", UiKit.Rounded, new Color32(0x0b, 0x13, 0x30, 0xff)), r);
                var face = Img(rt, "Face", UiKit.Rounded, UiKit.Orange);
                Round(face, r - b);
                UiKit.Stretch(face.rectTransform, b, b, b, b);
                face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                var art = UiKit.NewRect("Art", face.rectTransform);
                UiKit.Stretch(art);
                var raw = art.gameObject.AddComponent<RawImage>();
                raw.texture = Picture("menu_btn_orange");
                raw.raycastTarget = false;
                innerGlow = Img(face.rectTransform, "InnerGlow", HomeArt.PlayInnerGlow, Color.white);
                Size(innerGlow.rectTransform, 2 * 0.48f * w, 2 * 0.55f * h).anchoredPosition = new Vector2(0, 0.05f * h);
                shineImg = Img(face.rectTransform, "Shine", HomeArt.Shine, Color.clear);
                shine = Size(shineImg.rectTransform, PlayInnerH * 0.9f * 0.86f, PlayInnerH * 1.4f);
                shine.localRotation = Quaternion.Euler(0, 0, -31); // the old band leans 0.3 h each way
                playDark = Img(face.rectTransform, "Pressed", null, new Color(60 / 255f, 20 / 255f, 0, 0.18f));
                UiKit.Stretch(playDark.rectTransform);
                playDark.enabled = false;

                float size = 7.6f * Cw;
                // PLAY and its ›, in one box: in the middle, or a little up when the first-win line is under it (SetPlaySub)
                homePlayLabel = Size(UiKit.NewRect("Label", rt), w, h);
                var label = Lil(homePlayLabel, "PLAY", 7.6f, Color.white);
                Edge(label, new Color32(0x8a, 0x32, 0x00, 0xff), size * 0.07f * 0.6f, new Color32(0xb0, 0x4a, 0x00, 0xff), size * 0.083f);
                float lx = -2.5f * Cw, ly = 0;
                Size(label.rectTransform, w, size * 1.4f).anchoredPosition = new Vector2(lx, ly);
                // the › after the label: a dark stroke under a white one
                float ax = lx + label.preferredWidth / 2 + 4.2f * Cw, kk = size * 0.22f;
                Chevron(homePlayLabel, ax, ly, kk, kk * 0.75f, new Color32(0x8a, 0x32, 0x00, 0xff));
                Chevron(homePlayLabel, ax, ly, kk, kk * 0.42f, Color.white);
                // the old sub line's place and look (Lilita with a dark orange edge) for "FIRST WIN TODAY +50 XP"
                homeFirstWinText = Lil(rt, "", 3.2f, Color.white);
                Edge(homeFirstWinText, new Color32(0x6e, 0x26, 0x00, 0xff), 0.3f * Cw, new Color(60 / 255f, 15 / 255f, 0, 0.85f), 0.45f * Cw);
                Size(homeFirstWinText.rectTransform, w, 5 * Cw).anchoredPosition = new Vector2(0, -0.28f * h);
                homeFirstWinText.gameObject.SetActive(false);

                var hit = rt.gameObject.AddComponent<Image>();
                hit.color = Color.clear;
                var button = rt.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => { BamAudio.Play(SfxId.UiConfirm); OnPlay(); });
                playPress = rt.gameObject.AddComponent<HomePress>();
            }

            static void Chevron(RectTransform parent, float ax, float ay, float k, float thick, Color color)
            {
                float len = k * Mathf.Sqrt(2) + thick * 0.5f;
                var a = Img(parent, "Chevron", null, color);
                Size(a.rectTransform, len, thick).anchoredPosition = new Vector2(ax, ay + k * 0.5f);
                a.rectTransform.localRotation = Quaternion.Euler(0, 0, -45);
                var c = Img(parent, "Chevron", null, color);
                Size(c.rectTransform, len, thick).anchoredPosition = new Vector2(ax, ay - k * 0.5f);
                c.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            }

            // HubRuneFrame3: a soft light on top, the worn gold edge, a thin inner line and gold studs in the corners
            static void RuneFrame(RectTransform face, float w, float h, float r)
            {
                var sheen = Img(face, "Sheen", UiKit.Rounded, new Color(1f, 236 / 255f, 190 / 255f, 0.07f));
                Round(sheen, r * 0.7f);
                sheen.rectTransform.anchorMin = sheen.rectTransform.anchorMax = sheen.rectTransform.pivot = new Vector2(0.5f, 1);
                sheen.rectTransform.anchoredPosition = new Vector2(0, -0.6f * Cw);
                sheen.rectTransform.sizeDelta = new Vector2(w - 1.6f * Cw, h * 0.38f);
                var gold = Img(face, "Gold", HomeArt.RoundRing(0.45f * Cw / r), HubGold);
                RoundRing(gold, r);
                var inner = Img(face, "Inner", HomeArt.RoundRing(0.25f * Cw / (r - Cw)), Fade(HubGold, 0.3f));
                RoundRing(inner, r - Cw);
                UiKit.Stretch(inner.rectTransform, Cw, Cw, Cw, Cw);
                float k = 0.9f * Cw, d = r * 0.55f;
                for (int i = 0; i < 4; i++)
                {
                    var stud = Img(face, "Stud", null, UiKit.Gold);
                    var sr = Size(stud.rectTransform, k * 1.414f, k * 1.414f);
                    sr.anchoredPosition = new Vector2((i % 2 == 0 ? -1 : 1) * (w / 2 - d), (i < 2 ? 1 : -1) * (h / 2 - d));
                    sr.localRotation = Quaternion.Euler(0, 0, 45);
                }
            }

            // HubModeIcon "classic": a mint robot on a dark rounded square
            internal static void RobotIcon(RectTransform face, float y, float s)
            {
                var back = new Color32(0x22, 0x2a, 0x62, 0xff);
                var sq = Img(face, "Icon", UiKit.Rounded, back);
                Round(sq, s * 0.26f);
                var box = Size(sq.rectTransform, s, s);
                box.anchoredPosition = new Vector2(0, y);
                float k = s * 0.3f;
                var head = Img(box, "Head", UiKit.Rounded, UiKit.Primary);
                Round(head, k * 0.35f);
                Size(head.rectTransform, 2 * k, 1.45f * k).anchoredPosition = new Vector2(0, -0.175f * k);
                Size(Img(box, "Stalk", null, UiKit.Primary).rectTransform, 0.16f * k, 0.55f * k).anchoredPosition = new Vector2(0, 0.825f * k);
                Size(Img(box, "Knob", UiKit.Circle, UiKit.Primary).rectTransform, 0.36f * k, 0.36f * k).anchoredPosition = new Vector2(0, 1.15f * k);
                Size(Img(box, "Eye", UiKit.Circle, back).rectTransform, 0.44f * k, 0.44f * k).anchoredPosition = new Vector2(-0.45f * k, -0.1f * k);
                Size(Img(box, "Eye", UiKit.Circle, back).rectTransform, 0.44f * k, 0.44f * k).anchoredPosition = new Vector2(0.45f * k, -0.1f * k);
            }

            // ---- the top bar: the hero's picture and name straight on the dark top (no glass pill, no gear: Settings is a tab) ----

            void BuildTopBar()
            {
                float cy = 7.5f * Cw;

                // the picture: the chosen hero's portrait in a blue-purple circle
                float ar = 5 * Cw;
                var disc = Img(top, "Avatar", HomeArt.AvatarDisc, Color.white);
                AtTop(disc.rectTransform, 9 * Cw - ar, cy - ar, 2 * ar, 2 * ar);
                var mask = Img(disc.rectTransform, "Mask", UiKit.Circle, Color.white);
                UiKit.Stretch(mask.rectTransform);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var pic = UiKit.NewRect("Portrait", mask.rectTransform);
                UiKit.Stretch(pic);
                homePortraitImg = pic.gameObject.AddComponent<RawImage>();
                homePortraitImg.raycastTarget = false;
                homePortraitImg.uvRect = new Rect(0.115f, 0.1f, 0.77f, 0.9f); // the 400x308 portraits: a square around the face

                float tx = 17.4f * Cw;
                homeName = Lil(top, "", 3.8f, HubText);
                homeName.alignment = TextAnchor.MiddleLeft;
                AtTop(homeName.rectTransform, tx, cy - 2.8f * Cw - 3 * Cw, 50 * Cw, 6 * Cw);
                homeTitle = Lil(top, "", 2.7f, UiKit.Gold);
                homeTitle.font = UiKit.BodyFont;
                homeTitle.fontStyle = FontStyle.Bold;
                homeTitle.alignment = TextAnchor.MiddleLeft;
                Edge(homeTitle, new Color(6 / 255f, 8 / 255f, 26 / 255f, 0.9f), 0.25f * Cw, new Color(0, 0, 0, 0.85f), 0.35f * Cw);
                AtTop(homeTitle.rectTransform, tx, cy + 1.1f * Cw - 2.5f * Cw, 50 * Cw, 5 * Cw);
                if (OnlineChip(top, cy)) { disc.gameObject.SetActive(false); homeName.gameObject.SetActive(false); homeTitle.gameObject.SetActive(false); } // online builds: the old profile chip
            }

            // HubTutCard (the old MenuNewPlayer.cs): after the first tutorial, "Tutorial done" and "Next: your first match" on a dark
            // mint card under the top bar; a tap or the first finished match takes it away. The old card said "+100 XP"; BAM has no
            // XP yet, so that part is left out. It sits under the gear, which the old Home did not have there.
            void BuildTutCard()
            {
                float y = 29 * Cw, h = 13 * Cw, r = 3 * Cw;
                var card = Img(top, "TutorialCard", UiKit.Rounded, new Color(10 / 255f, 40 / 255f, 34 / 255f, 0.92f));
                Round(card, r);
                card.raycastTarget = true;
                AtTop(card.rectTransform, 4 * Cw, y, 1080 - 8 * Cw, h);
                var edge = Img(card.rectTransform, "Edge", HomeArt.RoundRing(0.4f * Cw / r), UiKit.Primary);
                RoundRing(edge, r);
                var title = Lil(card.rectTransform, "Tutorial done", 4f, Color.white);
                At(title, 540 - 4 * Cw, h * (1 - 0.36f), 1000, 6 * Cw);
                var next = Lil(card.rectTransform, "Next: your first match", 3f, UiKit.Primary);
                next.font = UiKit.BodyFont;
                next.fontStyle = FontStyle.Bold;
                At(next, 540 - 4 * Cw, h * (1 - 0.72f), 1000, 5 * Cw);
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card;
                b.transition = Selectable.Transition.None;
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                b.onClick.AddListener(() => { BamAudio.Play(SfxId.UiTap); BamSettings.TutorialCard = false; homeTutCard.SetActive(false); });
                homeTutCard = card.gameObject;
                homeTutCard.SetActive(false);
            }

            // ---- per frame ----

            void Update()
            {
                float t = Time.unscaledTime, cw = Cw;
                float br = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2 / 3f);
                float s = (1 + 0.012f * Mathf.Sin(t * 0.45f)) * PartyTick();
                homeBg.rectTransform.localScale = new Vector3(s, s, 1);
                LayoutSky(s);

                rune.alpha = 0.45f + 0.25f * br;
                float rx = 0.4f * HeroH * 0.93f, ry = 0.1f * HeroH * 0.93f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI * 2 / 12 + t * 0.25f;
                    marks[i].anchoredPosition = new Vector2(Mathf.Cos(a) * rx, -Mathf.Sin(a) * ry);
                }

                aura.alpha = 0.45f + 0.3f * br;
                for (int i = 0; i < 7; i++)
                {
                    float per = 3.2f + (i * 0.61f) % 1.4f, k = ((t + i * 1.37f) % per) / per;
                    float sx = ((i * 0.383f) % 1f) * 2 - 1;
                    var p = new Vector2(540 + sx * HeroH * 0.26f + Mathf.Sin((t + i) * 1.3f) * 0.8f * cw, FeetY + cw + k * HeroH * 0.65f);
                    float f = Mathf.Sin(k * Mathf.PI);
                    moteCore[i].rectTransform.anchoredPosition = p;
                    moteGlow[i].rectTransform.anchoredPosition = p;
                    moteCore[i].color = new Color(190 / 255f, 1f, 230 / 255f, 0.9f * 0.7f * f);
                    moteGlow[i].color = new Color(UiKit.Primary.r, UiKit.Primary.g, UiKit.Primary.b, 0.8f * 0.25f * f);
                }

                // PLAY
                bool dn = playPress.Down;
                if (playWasDown && !dn) { shineAt = t; springV += 1.6f; } // released: a kick, so it overshoots a little
                playWasDown = dn;
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f), target = dn ? 0.94f : 1f;
                springV += ((target - spring) * 520f - springV * 18f) * dt;
                spring += springV * dt;
                float sc = Mathf.Clamp(spring, 0.85f, 1.12f);
                playScale.localScale = new Vector3(sc, sc, 1);
                float bp = 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI * 2 / 2.4f);
                playGlow.color = new Color(1, 1, 1, 0.5f + 0.3f * bp);
                innerGlow.color = new Color(1, 1, 1, 0.55f + 0.25f * bp);
                playDark.enabled = dn;
                float since = t - shineAt, st = since < 0.45f ? since / 0.45f : (t % 4f < 0.45f ? (t % 4f) / 0.45f : -1);
                if (st < 0) shineImg.enabled = false;
                else
                {
                    shineImg.enabled = true;
                    float iw = PlayW - 1.4f * Cw, bw = PlayInnerH * 0.9f;
                    shine.anchoredPosition = new Vector2(-iw / 2 - bw * 1.5f + (iw + bw * 3) * st, 0);
                    shineImg.color = new Color(1, 1, 1, (since < 0.45f ? 0.55f : 0.28f) * (1 - Mathf.Abs(st - 0.5f) * 1.2f));
                }

                modesFace.anchoredPosition = new Vector2(0, modesPress.Down ? -0.5f * Cw : 0);
            }

            // ---- small helpers, in design units ----

            static Image Img(Transform parent, string name, Sprite sprite, Color color)
            {
                var img = UiKit.Fill(parent, color, sprite);
                img.name = name;
                return img;
            }

            // Centre (x, y) in the group, y up; the pivot (px, py) picks which point of the rect sits there.
            static RectTransform At(Component c, float x, float y, float w, float h, float px = 0.5f, float py = 0.5f)
            {
                var r = (RectTransform)c.transform;
                r.anchorMin = r.anchorMax = Vector2.zero;
                r.pivot = new Vector2(px, py);
                r.anchoredPosition = new Vector2(x, y);
                r.sizeDelta = new Vector2(w, h);
                return r;
            }

            // Top-left corner at (x, y) measured down from the group's top, as in the old code.
            static RectTransform AtTop(RectTransform r, float x, float y, float w, float h)
            {
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
                r.anchoredPosition = new Vector2(x, -y);
                r.sizeDelta = new Vector2(w, h);
                return r;
            }

            // Centred on the parent, of this size.
            static RectTransform Size(RectTransform r, float w, float h)
            {
                r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(w, h);
                return r;
            }

            // UiKit.Rounded has 28 px corners at 100 px per unit: this gives corners of radius r.
            static void Round(Image img, float r) => img.pixelsPerUnitMultiplier = 28f / Mathf.Max(1f, r);
            static void RoundRing(Image img, float r) { img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = HomeArt.RingRadius / Mathf.Max(1f, r); }

            // An ellipse outline of uniform width (HomeArt.EllipseRing), rx wide each way and a quarter as tall.
            static Image Ellipse(RectTransform parent, float rx, float stroke, Color color, float y)
            {
                var sprite = HomeArt.EllipseRing(stroke / rx, out float f);
                var img = Img(parent, "Ellipse", sprite, color);
                float w = 2 * rx * f;
                Size(img.rectTransform, w, w * sprite.rect.height / sprite.rect.width).anchoredPosition = new Vector2(0, y);
                return img;
            }

            // Rickard's ring picture (home_ring, O-040, 2026-10-31) instead of the two drawn mint ellipses: its back or front half,
            // the bright outer line on rx. False when the picture is missing: the drawn ring then.
            static Sprite ringSprite;
            static bool RingPicture(RectTransform parent, float rx, bool back)
            {
                if (ringSprite == null)
                {
                    var tex = Picture("home_ring", true);
                    if (tex == null) return false;
                    ringSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                }
                var img = Img(parent, "RingPicture", ringSprite, Color.white);
                float w = 2 * rx * 1.1f; // the picture's bright edge sits a little inside its glow
                Size(img.rectTransform, w, w * ringSprite.rect.height / ringSprite.rect.width);
                Half(img, back);
                return true;
            }

            // The back (upper) or front (lower) half of a ring, so the hero stands inside it.
            static void Half(Image img, bool back)
            {
                img.type = Image.Type.Filled;
                img.fillMethod = Image.FillMethod.Vertical;
                img.fillOrigin = back ? (int)Image.OriginVertical.Top : (int)Image.OriginVertical.Bottom;
                img.fillAmount = 0.5f;
            }

            static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);

            static Text Lil(Transform parent, string text, float sizeCw, Color color)
            {
                var t = UiKit.Label(parent, text, Mathf.RoundToInt(sizeCw * Cw), TextAnchor.MiddleCenter, color);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                return t;
            }

            // The old Text(..., stroke, sw, shadow, sh): a dark edge round the letters and a shadow under them.
            static void Edge(Text t, Color stroke, float width, Color shadow, float drop)
            {
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = stroke;
                o.effectDistance = new Vector2(width, -width);
                var s = t.gameObject.AddComponent<Shadow>();
                s.effectColor = shadow;
                s.effectDistance = new Vector2(0, -drop);
            }
        }

        /// <summary>HubGear: eight rounded teeth round a ring with a dark hole, radius r, centred in the parent.</summary>
        static void Gear(RectTransform parent, float r, Color color, Color hole)
        {
            for (int i = 0; i < 8; i++)
            {
                var tooth = UiKit.Fill(parent, color, UiKit.Rounded);
                tooth.pixelsPerUnitMultiplier = 28f / Mathf.Max(1f, r * 0.08f);
                var tr = tooth.rectTransform;
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
                float a = i * Mathf.PI / 4;
                tr.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (r * 0.775f);
                tr.sizeDelta = new Vector2(r * 0.4f, r * 0.45f);
                tr.localRotation = Quaternion.Euler(0, 0, -i * 45f);
            }
            var body = UiKit.Fill(parent, color, UiKit.Circle);
            UiKit.Place(body, 0.5f, 0.5f, 0, 0, r * 1.4f, r * 1.4f);
            var h = UiKit.Fill(parent, hole, UiKit.Circle);
            UiKit.Place(h, 0.5f, 0.5f, 0, 0, r * 0.6f, r * 0.6f);
        }
    }

    /// <summary>Held down and still over the button: the old Down(key), for PLAY's spring and MODES sinking.</summary>
    sealed class HomePress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler
    {
        bool held, over;
        public bool Down => held && over;
        public void OnPointerDown(PointerEventData e) { held = true; over = true; }
        public void OnPointerUp(PointerEventData e) => held = false;
        public void OnPointerEnter(PointerEventData e) => over = true;
        public void OnPointerExit(PointerEventData e) => over = false;
        void OnDisable() { held = false; over = false; }
    }

    /// <summary>The soft shapes the old code drew with gradients and strokes, made once as small white textures.</summary>
    static partial class HomeArt
    {
        delegate Color Paint(float x, float y);

        static Sprite Make(string name, int w, int h, Paint paint, Vector4 border = default)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "BAM.UI." + name };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = paint(x + 0.5f, y + 0.5f);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        static readonly Dictionary<int, float> ellipseScale = new Dictionary<int, float>();

        /// <summary>
        /// An ellipse outline four times as wide as tall whose line is the same width all round; stroke is the line's width
        /// over the half width. f: the sprite's width over the ellipse's own width (the line pokes out half its width).
        /// </summary>
        public static Sprite EllipseRing(float stroke, out float f)
        {
            int key = 1000000 + Mathf.RoundToInt(stroke * 10000);
            if (cache.TryGetValue(key, out var s)) { f = ellipseScale[key]; return s; }
            const int W = 512;
            float a = (W / 2f - 2f) / (1 + stroke / 2), b = a / 4, line = stroke * a, pad = line / 2 + 2;
            int H = Mathf.CeilToInt(2 * (b + pad) / 2) * 2;
            float cx = W / 2f, cy = H / 2f;
            s = Make("EllipseRing", W, H, (x, y) =>
            {
                float dx = x - cx, dy = y - cy;
                float g = Mathf.Sqrt(dx * dx / (a * a) + dy * dy / (b * b));
                float grad = Mathf.Sqrt(dx * dx / (a * a * a * a) + dy * dy / (b * b * b * b)) / Mathf.Max(g, 1e-4f);
                float d = (g - 1) / Mathf.Max(grad, 1e-6f);
                return White(Mathf.Clamp01(line / 2 - Mathf.Abs(d) + 0.5f));
            });
            f = W / 2f / a;
            cache[key] = s;
            ellipseScale[key] = f;
            return s;
        }

        public const float RingRadius = 32f;

        /// <summary>A 9-sliced rounded outline with corners of RingRadius px; stroke is the line over the corner radius.</summary>
        public static Sprite RoundRing(float stroke)
        {
            int key = 2000000 + Mathf.RoundToInt(stroke * 10000);
            if (cache.TryGetValue(key, out var s)) return s;
            float R = RingRadius, line = Mathf.Max(1f, stroke * R);
            int n = (int)(2 * R + 2);
            float half = n / 2f;
            s = Make("RoundRing", n, n, (x, y) =>
            {
                float qx = Mathf.Abs(x - half) - (half - R), qy = Mathf.Abs(y - half) - (half - R);
                float d = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - R;
                return White(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(line + d + 0.5f));
            }, new Vector4(R + 1, R + 1, R + 1, R + 1));
            cache[key] = s;
            return s;
        }

        /// <summary>A circle outline filling its rect; stroke is the line over the radius.</summary>
        public static Sprite CircleRing(float stroke)
        {
            int key = 3000000 + Mathf.RoundToInt(stroke * 10000);
            if (cache.TryGetValue(key, out var s)) return s;
            const int N = 128;
            float R = N / 2f, line = stroke * R;
            s = Make("CircleRing", N, N, (x, y) =>
            {
                float d = new Vector2(x - R, y - R).magnitude - R;
                return White(Mathf.Clamp01(0.5f - d) * Mathf.Clamp01(line + d + 0.5f));
            });
            cache[key] = s;
            return s;
        }

        static Sprite spot, aura, ringShadow, contact, playGlow, playInner, shine, diamond, avatar, roof;

        // HubSpot3: rgba(255,226,160) 0 at the top, 0.07 at 60 %, 0.16 at the feet; 0.12 of the bottom's width at the top.
        public static Sprite Spot => spot != null ? spot : spot = Make("Spot", 128, 128, (x, y) =>
        {
            float v = y / 128f, hw = Mathf.Lerp(1f, 0.12f / 0.55f, v) * 64f;
            float a = v < 0.4f ? Mathf.Lerp(0.16f, 0.07f, v / 0.4f) : Mathf.Lerp(0.07f, 0f, (v - 0.4f) / 0.6f);
            return new Color(1f, 226 / 255f, 160 / 255f, a * Mathf.Clamp01(hw - Mathf.Abs(x - 64f) + 0.5f));
        });

        public static Sprite AuraGlow => aura != null ? aura : aura = Radial("Aura",
            (0f, new Color(0.24f, 0.9f, 0.69f, 0.45f)), (0.55f, new Color(0.24f, 0.9f, 0.69f, 0.14f)), (1f, new Color(0.24f, 0.9f, 0.69f, 0f)));
        public static Sprite RingShadow => ringShadow != null ? ringShadow : ringShadow = Radial("RingShadow",
            (0f, new Color(0, 0, 0, 0.55f)), (0.7f, new Color(0, 0, 0, 0.15f)), (1f, new Color(0, 0, 0, 0)));
        public static Sprite ContactShadow => contact != null ? contact : contact = Radial("Contact",
            (0f, new Color(0, 0, 0, 0.7f)), (0.6f, new Color(0, 0, 0, 0.35f)), (1f, new Color(0, 0, 0, 0)));
        public static Sprite PlayGlow => playGlow != null ? playGlow : playGlow = Radial("PlayGlow",
            (0f, new Color(1f, 140 / 255f, 50 / 255f, 0.45f)), (0.7f, new Color(1f, 120 / 255f, 40 / 255f, 0.12f)), (1f, new Color(1f, 110 / 255f, 30 / 255f, 0f)));
        public static Sprite PlayInnerGlow => playInner != null ? playInner : playInner = Radial("PlayInner",
            (0f, new Color(1f, 236 / 255f, 170 / 255f, 0.55f)), (0.6f, new Color(1f, 190 / 255f, 90 / 255f, 0.18f)), (1f, new Color(1f, 160 / 255f, 60 / 255f, 0f)));

        // FxShineBand: white, clear at the edges and 0.75 in the middle
        public static Sprite Shine => shine != null ? shine : shine = Make("Shine", 64, 4, (x, y) => White(0.75f * (1 - Mathf.Abs(x / 32f - 1))));

        public static Sprite Diamond => diamond != null ? diamond : diamond = Make("Diamond", 48, 40, (x, y) =>
        {
            float d = (Mathf.Abs(x - 24) / 24f + Mathf.Abs(y - 20) / 20f - 1) / Mathf.Sqrt(1 / 576f + 1 / 400f);
            return White(Mathf.Clamp01(0.5f - d));
        });

        // the top bar's picture: a circle from #4c6bff at the top to #7a3cff at the bottom
        public static Sprite AvatarDisc => avatar != null ? avatar : avatar = Make("Avatar", 128, 128, (x, y) =>
        {
            float d = new Vector2(x - 64, y - 64).magnitude - 64;
            var c = Color.Lerp(new Color32(0x7a, 0x3c, 0xff, 0xff), new Color32(0x4c, 0x6b, 0xff, 0xff), y / 128f);
            c.a = Mathf.Clamp01(0.5f - d);
            return c;
        });

        // the house's roof: a triangle 2.2 times as wide as tall
        public static Sprite Roof => roof != null ? roof : roof = Make("Roof", 88, 40, (x, y) =>
        {
            float slope = 40f / 44f, d = (40f * (1 - Mathf.Abs(x - 44f) / 44f) - y) / Mathf.Sqrt(1 + slope * slope);
            return White(Mathf.Clamp01(d + 0.5f) * Mathf.Clamp01(y + 0.5f));
        });

        static Sprite Radial(string name, params (float at, Color color)[] stops)
        {
            return Make(name, 128, 128, (x, y) =>
            {
                float r = new Vector2(x - 64, y - 64).magnitude / 64f;
                if (r >= 1) return Color.clear;
                for (int i = 1; i < stops.Length; i++)
                    if (r <= stops[i].at)
                        return Color.Lerp(stops[i - 1].color, stops[i].color, (r - stops[i - 1].at) / (stops[i].at - stops[i - 1].at));
                return stops[stops.Length - 1].color;
            });
        }

        static Color White(float a) => new Color(1, 1, 1, a);
    }
}
