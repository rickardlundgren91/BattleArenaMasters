using UnityEngine;
using UnityEngine.UI;
using BAM.Sim;
using BAM.UI;

namespace BAM.Game
{
    // FOCUS (Rickard 2026-11-15: "lägg in focus igen med prickarna", so new players understand the targeting): the old game's
    // HudFocus.cs chip under SPECIAL. Focus = how many enemies are in your auto attack reach; your hero hits one of them at random,
    // so fewer targets means more hits where you want them. One dot per target (at most 5): teal at 1-2, orange at 3+, hidden at 0.
    // Picture only, no rule change. The first three real matches show one short line next to it for 10 s.
    //
    // The guided match teaches it (the island's Focus step, TutorialIsland.cs), and there the chip is made to be seen. Rickard
    // 2026-11-16: "focus är för litet i början när man är på ön, gömt nere i ett hörne där en helt ny spelare inte tittar" (on
    // the island SPECIAL is hidden, so the small chip sat alone in the bottom corner with an arrow on it) and "den kan krympa
    // i segment i senare steg":
    // - it does not show before its lesson (the first minions lit a chip nobody had explained);
    // - when the line names it, it comes up FocusTaught times its size in the middle of the screen, right under the guide
    //   card whose line names it, and a gold ring breathes round it while that line is up (no arrow: the card is over it);
    // - then it shrinks a step at a time: FocusLeader through the island's last lesson (still under the card: the count
    //   dropping to one is that lesson's point), FocusArena in its real place under SPECIAL through the arena's first
    //   lessons, and its usual size from Knut on. A change of size glides; on the island the chip pops when it comes up
    //   and when its count changes (it counts: that is the lesson), in the arena once, the first time it shows in its place.
    sealed partial class Hud
    {
        const int FocusDotsMax = 5, FocusFont = 32;
        const float FocusH = 60f, FocusLabelW = 150f, FocusDot = 24f, FocusGap = 36f, FocusEdge = 3f;
        const float FocusCorner = 28f / 1.5f, FocusInner = 28f / 1.6f; // its corners' radius: the edge's and the dark fill's inside it
        // the guided match: the chip's sizes (times the usual one), its gap under the guide card (of the screen's width, for each
        // time its usual size: the gold ring has to clear the card's shadow), how fast a change of size glides (per second,
        // eased), how long a pop lasts, and the gold ring (how far out, how much further at its widest, how wide its line)
        const float FocusTaught = 2f, FocusLeader = 1.6f, FocusArena = 1.3f, FocusUnderGap = 0.018f, FocusGlide = 9f, FocusPop = 0.3f;
        const float FocusGlowOut = 7f, FocusGlowBreath = 3f, FocusGlowLine = 5f;
        RectTransform focusRoot;
        Image focusEdge, focusBack, focusGlow;
        readonly Image[] focusDots = new Image[FocusDotsMax];
        Text focusLabel, focusHint;
        Outline focusLabelEdge;
        int focusShown = -1;
        bool focusHintOn;
        float focusHintAt;
        // the guided match: its lesson has named it; it was up last frame; it has been up in this half; the size it is drawn
        // at now and the size its pieces are laid out for; the last pop, and whether that was the chip coming up
        bool focusKnown, focusWasUp, focusCame, focusPopIn;
        float focusSize = 1f, focusLaid = -1f, focusPopAt = -99f;

        void BuildFocus()
        {
            focusRoot = UiKit.NewRect("Focus", safe);
            focusRoot.anchorMin = focusRoot.anchorMax = focusRoot.pivot = new Vector2(0.5f, 0.5f);
            focusGlow = UiKit.Fill(focusRoot, Color.clear); // behind the chip; its ring is cut in LayFocus
            focusGlow.enabled = false;
            focusEdge = UiKit.Fill(focusRoot, TopOwn, UiKit.Rounded);
            focusEdge.type = Image.Type.Sliced; focusEdge.raycastTarget = false;
            focusBack = UiKit.Fill(focusRoot, GlassFill, UiKit.Rounded);
            focusBack.type = Image.Type.Sliced; focusBack.raycastTarget = false;
            focusLabel = UiKit.Label(focusRoot, "FOCUS", FocusFont, TextAnchor.MiddleLeft, LevelCream);
            focusLabel.raycastTarget = false;
            UiKit.AddOutline(focusLabel, 2);
            focusLabelEdge = focusLabel.GetComponent<Outline>();
            for (int i = 0; i < FocusDotsMax; i++)
            {
                var d = UiKit.NewRect("Dot", focusRoot);
                d.anchorMin = d.anchorMax = d.pivot = new Vector2(0f, 0.5f);
                focusDots[i] = d.gameObject.AddComponent<Image>();
                focusDots[i].sprite = UiKit.Circle;
                focusDots[i].raycastTarget = false;
            }
            LayFocus(1f);
            focusHint = UiKit.Label(safe, "Fewer enemies in range = your hits\nland where you want them", 30, TextAnchor.MiddleRight, LevelCream);
            focusHint.raycastTarget = false;
            focusHint.horizontalOverflow = HorizontalWrapMode.Overflow;
            focusHint.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.AddOutline(focusHint, 2);
            focusRoot.gameObject.SetActive(false);
            focusHint.gameObject.SetActive(false);
        }

        // The chip's pieces at s times the usual size: laid out for real, not with a scaled transform, so the big chip's letters
        // are as sharp as the small one's. Only when the size to lay out for changes (a new lesson of the guided match).
        void LayFocus(float s)
        {
            focusLaid = s;
            Corners(focusEdge, FocusCorner * s);
            Corners(focusBack, FocusInner * s);
            float e = FocusEdge * s;
            UiKit.Stretch(focusBack.rectTransform, e, e, e, e);
            RoundRing(focusGlow, (FocusCorner + FocusGlowOut) * s, FocusGlowLine * s);
            focusLabel.fontSize = Mathf.RoundToInt(FocusFont * s);
            if (focusLabelEdge != null) focusLabelEdge.effectDistance = new Vector2(2f * s, -2f * s);
            UiKit.Place(focusLabel, 0f, 0.5f, FocusH * 0.4f * s, 0f, FocusLabelW * s, FocusH * s);
            for (int i = 0; i < FocusDotsMax; i++)
            {
                var d = focusDots[i].rectTransform;
                d.sizeDelta = new Vector2(FocusDot * s, FocusDot * s);
                d.anchoredPosition = new Vector2((FocusLabelW + FocusGap * i + (FocusGap - FocusDot) * 0.5f) * s, 0f);
            }
        }

        void BeginFocus()
        {
            focusShown = -1;
            focusKnown = focusWasUp = focusCame = false;
            focusSize = 1f; focusPopAt = -99f;
            focusRoot.localScale = Vector3.one;
            focusGlow.enabled = false;
            focusRoot.gameObject.SetActive(false);
            focusHint.gameObject.SetActive(false);
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
            var tu = m.Tut;
            int n = !hidden && !heroTest && m.State == MatchState.Play && p != null && p.Alive ? m.FocusTargets(p) : 0;
            // the island: not before its lesson has named it
            if (!focusKnown && (tu == null || !tu.Island || tu.FocusShown || tu.Step > Tutorial.IslandFocus)) focusKnown = true;
            bool show = n > 0 && focusKnown;
            if (focusRoot.gameObject.activeSelf != show) focusRoot.gameObject.SetActive(show);
            Vector2 c = Vector2.zero;
            float w = 0f, now = Time.unscaledTime;
            if (show)
            {
                // where it sits: its own place is centred under SPECIAL, a small gap under its rim; on the island (no SPECIAL
                // there) it is in the middle of the screen, right under the guide card
                var sr = safe.rect;
                bool under = tu != null && tu.Island && cardLaidW > 0f;
                Vector3 sc = safe.InverseTransformPoint(specialRoot.TransformPoint(specialRoot.rect.center));
                float top = sc.y - SpecialButton.Radius - SafeW * 0.012f;
                // the guided match's size for this lesson; in its own place never taller than the room under SPECIAL
                float size = tu == null ? 1f
                    : tu.Island ? (tu.Step <= Tutorial.IslandFocus ? FocusTaught : FocusLeader)
                    : tu.Step < Tutorial.StepBlink ? FocusArena : 1f;
                if (!under) size = Mathf.Clamp((top - sr.yMin - 6f) / FocusH, 1f, size);
                if (size != focusLaid) LayFocus(size);
                // a new lesson's size glides in; nothing glides while the chip is away
                if (!focusWasUp || Mathf.Abs(focusSize - size) < 0.004f) focusSize = size;
                else focusSize += (size - focusSize) * (1f - Mathf.Exp(-FocusGlide * Time.unscaledDeltaTime));

                int shown = Mathf.Min(n, FocusDotsMax);
                float laidW = (FocusLabelW + FocusGap * shown + FocusH * 0.5f) * size, h = FocusH * focusSize;
                w = laidW * focusSize / size;
                float x = sc.x, y = top - h * 0.5f;
                if (under)
                {
                    Vector3 cp = card.localPosition;
                    x = cp.x;
                    y = cp.y - card.sizeDelta.y * 0.5f - SafeW * FocusUnderGap * focusSize - h * 0.5f;
                }
                // never off the screen's edge or under its bottom
                x = Mathf.Clamp(x, sr.xMin + w * 0.5f + 8f, sr.xMax - w * 0.5f - 8f);
                y = Mathf.Max(y, sr.yMin + h * 0.5f + 6f);
                c = new Vector2(x, y);

                // the pops: the island's (it comes up, its count changes), and the arena's one
                bool came = !focusWasUp, counted = focusWasUp && n != focusShown;
                if (tu != null && (tu.Island ? came || counted : came && !focusCame)) { focusPopAt = now; focusPopIn = came; }
                focusCame = true;
                float pop = 1f, pk = (now - focusPopAt) / FocusPop;
                if (pk < 1f)
                {
                    float bump = Mathf.Sin(pk * Mathf.PI), left = 1f - pk;
                    pop = focusPopIn ? 1f - 0.4f * left * left * left + 0.12f * bump : 1f + 0.14f * bump;
                }

                focusRoot.localPosition = c;
                focusRoot.sizeDelta = new Vector2(laidW, FocusH * size);
                focusRoot.localScale = Vector3.one * (focusSize / size * pop);
                if (n != focusShown)
                {
                    focusShown = n;
                    Color col = n >= 3 ? TopFoe : TopOwn;
                    focusEdge.color = col;
                    for (int i = 0; i < FocusDotsMax; i++) { focusDots[i].enabled = i < n; focusDots[i].color = col; }
                }
                // the gold ring round it while the island's line names it (as SPECIAL pulses when its line asks for a tap)
                bool glow = tu != null && tu.FocusShown;
                if (focusGlow.enabled != glow) focusGlow.enabled = glow;
                if (glow)
                {
                    float k = 0.5f + 0.5f * Mathf.Sin(now * 7f), o = (FocusGlowOut + FocusGlowBreath * k) * size;
                    focusGlow.rectTransform.offsetMin = new Vector2(-o, -o);
                    focusGlow.rectTransform.offsetMax = new Vector2(o, o);
                    focusGlow.color = new Color(1f, 0.86f, 0.4f, 0.5f + 0.45f * k);
                }
            }
            focusWasUp = show;
            // the hint: 10 s from the first moment the chip shows in this match, fading over its last 1.5 s
            bool hint = focusHintOn && show;
            if (hint && focusHintAt < 0f) focusHintAt = now;
            float age = focusHintAt < 0f ? 0f : now - focusHintAt;
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
