using UnityEngine;
using UnityEngine.UI;
using BAM.Sim;
using BAM.UI;

namespace BAM.Game
{
    // FOCUS (Rickard 2026-11-15: "lägg in focus igen med prickarna", so new players understand the targeting): the old game's
    // HudFocus.cs chip under SPECIAL. Focus = how many enemies are in your auto attack reach; your hero hits one of them at random,
    // so fewer targets means more hits where you want them. One dot per target (at most 5): teal at 1-2, orange at 3+, hidden at 0.
    // Picture only, no rule change. The island's Focus step explains it (TutorialIsland.cs) with a gold arrow on the chip; the
    // first three real matches show one short line next to it for 10 s.
    sealed partial class Hud
    {
        const int FocusDotsMax = 5;
        const float FocusH = 60f, FocusLabelW = 150f, FocusDot = 24f, FocusGap = 36f, FocusEdge = 3f;
        RectTransform focusRoot, focusArrow;
        CanvasGroup focusArrowGroup;
        Image focusEdge, focusBack;
        readonly Image[] focusDots = new Image[FocusDotsMax];
        Text focusLabel, focusHint;
        int focusShown = -1;
        bool focusHintOn;
        float focusHintAt;

        void BuildFocus()
        {
            focusRoot = UiKit.NewRect("Focus", safe);
            focusRoot.anchorMin = focusRoot.anchorMax = focusRoot.pivot = new Vector2(0.5f, 0.5f);
            focusEdge = UiKit.Fill(focusRoot, TopOwn, UiKit.Rounded);
            focusEdge.type = Image.Type.Sliced; focusEdge.pixelsPerUnitMultiplier = 1.5f; focusEdge.raycastTarget = false;
            focusBack = UiKit.Fill(focusRoot, GlassFill, UiKit.Rounded);
            focusBack.type = Image.Type.Sliced; focusBack.pixelsPerUnitMultiplier = 1.6f; focusBack.raycastTarget = false;
            UiKit.Stretch(focusBack.rectTransform, FocusEdge, FocusEdge, FocusEdge, FocusEdge);
            focusLabel = UiKit.Label(focusRoot, "FOCUS", 32, TextAnchor.MiddleLeft, LevelCream);
            focusLabel.raycastTarget = false;
            UiKit.AddOutline(focusLabel, 2);
            UiKit.Place(focusLabel, 0f, 0.5f, FocusH * 0.4f, 0f, FocusLabelW, FocusH);
            for (int i = 0; i < FocusDotsMax; i++)
            {
                var d = UiKit.NewRect("Dot", focusRoot);
                d.anchorMin = d.anchorMax = d.pivot = new Vector2(0f, 0.5f);
                d.sizeDelta = new Vector2(FocusDot, FocusDot);
                d.anchoredPosition = new Vector2(FocusLabelW + FocusGap * i + (FocusGap - FocusDot) * 0.5f, 0f);
                focusDots[i] = d.gameObject.AddComponent<Image>();
                focusDots[i].sprite = UiKit.Circle;
                focusDots[i].raycastTarget = false;
            }
            focusHint = UiKit.Label(safe, "Fewer enemies in range = your hits\nland where you want them", 30, TextAnchor.MiddleRight, LevelCream);
            focusHint.raycastTarget = false;
            focusHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            focusHint.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.AddOutline(focusHint, 2);
            focusArrow = MakeChevron(safe);
            focusArrowGroup = focusArrow.gameObject.AddComponent<CanvasGroup>(); focusArrowGroup.blocksRaycasts = false;
            focusRoot.gameObject.SetActive(false);
            focusHint.gameObject.SetActive(false);
        }

        void BeginFocus()
        {
            focusShown = -1;
            focusRoot.gameObject.SetActive(false);
            focusHint.gameObject.SetActive(false);
            focusArrow.gameObject.SetActive(false);
            // the line beside the chip: the first three real matches only (the old game's abc2-focushint)
            focusHintOn = false; focusHintAt = -1f;
            if (!tutorial && !heroTest)
            {
                int seen = PlayerPrefs.GetInt("bam-focushint", 0);
                focusHintOn = seen < 3;
                if (focusHintOn) { PlayerPrefs.SetInt("bam-focushint", seen + 1); PlayerPrefs.Save(); }
            }
        }

        void FocusFrame(bool hidden)
        {
            var p = m.Player;
            int n = !hidden && !heroTest && m.State == MatchState.Play && p != null && p.Alive ? m.FocusTargets(p) : 0;
            bool show = n > 0;
            if (focusRoot.gameObject.activeSelf != show) focusRoot.gameObject.SetActive(show);
            // the island's Focus step: the gold arrow points at the dots while Tyra shows it
            bool arrow = !hidden && island && m.Tut != null && m.Tut.Step == Tutorial.IslandFocus && m.Tut.Line != null && show;
            Vector2 c = Vector2.zero;
            float w = 0f;
            if (show || arrow)
            {
                // centred under SPECIAL (its spot on the island too, where the button is hidden), a small gap under its rim
                Vector3 sc = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
                int shown = Mathf.Min(n, FocusDotsMax);
                w = FocusLabelW + FocusGap * shown + FocusH * 0.5f;
                float y = sc.y - SpecialButton.Radius - SafeW * 0.012f - FocusH * 0.5f;
                // never off the screen's edge or under its bottom
                var sr = safe.rect;
                float x = Mathf.Clamp(sc.x, sr.xMin + w * 0.5f + 8f, sr.xMax - w * 0.5f - 8f);
                y = Mathf.Max(y, sr.yMin + FocusH * 0.5f + 6f);
                c = new Vector2(x, y);
            }
            Arrow(focusArrow, focusArrowGroup, arrow, c + new Vector2(0f, FocusH * 0.5f), FocusH);
            if (show)
            {
                focusRoot.localPosition = c;
                focusRoot.sizeDelta = new Vector2(w, FocusH);
                if (n != focusShown)
                {
                    focusShown = n;
                    Color col = n >= 3 ? TopFoe : TopOwn;
                    focusEdge.color = col;
                    for (int i = 0; i < FocusDotsMax; i++) { focusDots[i].enabled = i < n; focusDots[i].color = col; }
                }
            }
            // the hint: 10 s from the first moment the chip shows in this match, fading over its last 1.5 s
            bool hint = focusHintOn && show;
            if (hint && focusHintAt < 0f) focusHintAt = Time.unscaledTime;
            float age = focusHintAt < 0f ? 0f : Time.unscaledTime - focusHintAt;
            if (focusHintAt >= 0f && age > 10f) focusHintOn = hint = false;
            if (focusHint.gameObject.activeSelf != hint) focusHint.gameObject.SetActive(hint);
            if (hint)
            {
                // beside the chip on the screen's inner side (left of it for the right hand), right-aligned towards it
                var hr = focusHint.rectTransform;
                bool right = InnerSide > 0f;
                hr.anchorMin = hr.anchorMax = new Vector2(0.5f, 0.5f);
                hr.pivot = new Vector2(right ? 0f : 1f, 0.5f);
                hr.sizeDelta = new Vector2(620f, 90f);
                focusHint.alignment = right ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                hr.localPosition = new Vector2(c.x + InnerSide * (w * 0.5f + 14f), c.y);
                var col = focusHint.color; col.a = Mathf.Clamp01((10f - age) / 1.5f); focusHint.color = col;
            }
        }
    }
}
