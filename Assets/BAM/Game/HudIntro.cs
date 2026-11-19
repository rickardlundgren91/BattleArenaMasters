using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The HUD while the match intro plays (ABC2 View3DIntro.cs DrawIntro): no controls, thin black cinematic bars that open
    /// with the sweep, TAP TO SKIP, and over each hero of the player's team a dark plate with its name (the player's in mint)
    /// and its role as a pill in the role's colour. A tap anywhere makes the intro finish fast.
    /// </summary>
    sealed partial class Hud
    {
        /// <summary>Set by the game flow before Begin.</summary>
        public MatchIntro Intro;

        static readonly Color PlateColor = new Color(7f / 255f, 11f / 255f, 22f / 255f, 0.9f), Mint = Hex(0x8dffd6), PillInk = Hex(0x0a0d18);
        static readonly Color TeamAlly = Hex(0x3d9bff), TeamFoe = Hex(0xff4040), PillDark = new Color(20f / 255f, 28f / 255f, 52f / 255f, 1f);
        const float TagHeight = 2.15f; // world height of the tag's foot, just above the heads

        RectTransform introRoot, barTop, barBottom;
        Text skipText;
        readonly IntroTag[] tags = new IntroTag[3];
        bool introShown;

        sealed class IntroTag
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Plate, Edge, Notch, Pill;
            public Text Name, Role;
        }

        static Color RoleColor(string r) =>
            r == "Tank" ? Hex(0x7ab8ff) : r == "Fighter" ? Hex(0xffa25e) : r == "Assassin" ? Hex(0xff6a85) : r == "Mage" ? Hex(0xc79bff)
            : r == "Marksman" ? Hex(0xffd65e) : r == "Support" ? Hex(0x6fe3b4) : Hex(0xcfd6e6);

        void BeginIntro()
        {
            introShown = false;
            bool on = Intro != null && Intro.Active;
            for (int i = 0; i < tags.Length; i++)
            {
                var tag = tags[i];
                bool use = on && i < Intro.HeroCount;
                tag.Root.gameObject.SetActive(use);
                if (!use) continue;
                var u = Intro.Hero(i);
                var info = HeroCatalog.Get(u.Hero);
                Color rc = RoleColor(info.Role);
                tag.Name.text = info.Name;
                tag.Name.color = u == m.Player ? Mint : Color.white;
                tag.Role.text = info.Role.ToUpperInvariant();
                // the card's edge in the TEAM's colour (blue ours, red theirs, as the health bars), the role only as the pill's word
                // in its colour on the dark plate: an Assassin's pink edge made our own Tikta read as an enemy (UI/UX review
                // 2026-11-19 nr 8)
                Color tc = u.Team == m.Player.Team ? TeamAlly : TeamFoe;
                tag.Edge.color = tc; tag.Notch.color = tc;
                tag.Pill.color = PillDark; tag.Role.color = rc;
            }
            ShowIntro(on);
        }

        void ShowIntro(bool on)
        {
            if (introShown == on && introRoot.gameObject.activeSelf == on) return;
            introShown = on;
            introRoot.gameObject.SetActive(on);
            // the normal HUD comes back as the intro lands
            if (safe.gameObject.activeSelf == on)
            {
                if (on) CancelTouches();
                safe.gameObject.SetActive(!on);
            }
        }

        /// <summary>Every frame; true while the intro owns the screen.</summary>
        bool IntroFrame(bool hidden)
        {
            bool on = Intro != null && Intro.Active;
            ShowIntro(on);
            if (!on) return false;
            var ptr = Pointer.current;
            var kb = Keyboard.current;
            // not while paused: a tap on the pause menu must not skip it behind the menu
            if (!hidden && ((ptr != null && ptr.press.wasPressedThisFrame) || (kb != null && kb.spaceKey.wasPressedThisFrame))) Intro.Skip();

            float u = Intro.Progress, h = canvasRect.rect.height;
            float bar = h * 0.075f * (1f - Mathf.SmoothStep(0f, 1f, u));
            barTop.sizeDelta = new Vector2(0f, bar);
            barBottom.sizeDelta = new Vector2(0f, bar);
            var sc = skipText.color; sc.a = 0.75f * (1f - Mathf.Clamp01(u * 2f)); skipText.color = sc;
            skipText.rectTransform.anchoredPosition = new Vector2(-50f, bar + 30f);

            float a = Intro.LabelAlpha;
            for (int i = 0; i < Intro.HeroCount; i++)
            {
                var tag = tags[i];
                var hero = Intro.Hero(i);
                Vector3 wp = views.DrawnAt(hero);
                wp.y = TagHeight;
                Vector3 sp = cam.Camera.WorldToScreenPoint(wp);
                bool show = a > 0.01f && sp.z > 0f && hero.Alive;
                tag.Group.alpha = show ? a : 0f;
                if (!show) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 local);
                tag.Root.anchoredPosition = local + new Vector2(0f, 18f);
            }
            return true;
        }

        void BuildIntro()
        {
            introRoot = UiKit.NewRect("Intro", canvasRect);
            UiKit.Stretch(introRoot);
            barTop = Bar(introRoot, 1f);
            barBottom = Bar(introRoot, 0f);
            skipText = UiKit.Label(introRoot, "TAP TO SKIP", 36, TextAnchor.LowerRight, Color.white);
            UiKit.Place(skipText, 1f, 0f, -50f, 100f, 500f, 50f);
            skipText.rectTransform.pivot = new Vector2(1f, 0f);
            for (int i = 0; i < tags.Length; i++) tags[i] = MakeTag(introRoot);
            introRoot.gameObject.SetActive(false);
        }

        static RectTransform Bar(RectTransform parent, float y)
        {
            var img = UiKit.Fill(parent, Color.black);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y); rt.pivot = new Vector2(0.5f, y);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 0f);
            return rt;
        }

        // a dark plate with the name, the role pill under it and a small notch pointing down at the hero
        static IntroTag MakeTag(RectTransform parent)
        {
            const float w = 300f, h = 146f;
            var t = new IntroTag();
            t.Root = UiKit.NewRect("Tag", parent);
            t.Root.anchorMin = t.Root.anchorMax = new Vector2(0.5f, 0.5f);
            t.Root.pivot = new Vector2(0.5f, 0f);
            t.Root.sizeDelta = new Vector2(w, h + 18f);
            t.Group = t.Root.gameObject.AddComponent<CanvasGroup>();
            t.Group.blocksRaycasts = false; t.Group.interactable = false;
            var notch = UiKit.NewRect("Notch", t.Root);
            notch.anchorMin = notch.anchorMax = new Vector2(0.5f, 0f);
            notch.pivot = new Vector2(0.5f, 0.5f);
            notch.sizeDelta = new Vector2(24f, 24f);
            notch.anchoredPosition = new Vector2(0f, 18f);
            notch.localRotation = Quaternion.Euler(0f, 0f, 45f);
            t.Notch = notch.gameObject.AddComponent<Image>();
            t.Notch.raycastTarget = false;
            var box = UiKit.NewRect("Plate", t.Root);
            box.anchorMin = new Vector2(0f, 0f); box.anchorMax = new Vector2(1f, 1f);
            box.offsetMin = new Vector2(0f, 18f); box.offsetMax = Vector2.zero;
            t.Edge = UiKit.Fill(box, Color.white, UiKit.Rounded);
            t.Edge.pixelsPerUnitMultiplier = 1.6f;
            t.Plate = UiKit.Fill(box, PlateColor, UiKit.Rounded);
            t.Plate.pixelsPerUnitMultiplier = 1.7f;
            UiKit.Stretch(t.Plate.rectTransform, 5, 5, 5, 5);
            t.Name = UiKit.Label(box, "", 54, TextAnchor.MiddleCenter, Color.white);
            UiKit.Place(t.Name, 0.5f, 1f, 0f, -8f, w, 66f);
            var o = t.Name.gameObject.AddComponent<Outline>();
            o.effectColor = Color.black; o.effectDistance = new Vector2(3f, -3f);
            var pill = UiKit.NewRect("Pill", box);
            UiKit.Place(pill, 0.5f, 0f, 0f, 14f, 210f, 50f);
            t.Pill = pill.gameObject.AddComponent<Image>();
            t.Pill.sprite = UiKit.Rounded;
            t.Pill.type = Image.Type.Sliced;
            t.Pill.raycastTarget = false;
            t.Role = UiKit.Label(pill, "", 34, TextAnchor.MiddleCenter, PillInk);
            UiKit.Stretch(t.Role.rectTransform);
            t.Root.gameObject.SetActive(false);
            return t;
        }
    }
}
