using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The SPECIAL button's two-sided charge (the old Hud.cs 743-796, the WoW wheel; Rickard 2026-10-26 "ikonen är delad på
    /// mitten och fylls nerifrån och upp för att visa att man kan klicka eller dra"): the charge ring runs up both sides from the
    /// bottom and meets at the top, a gold level rises in each half over the icon, and both halves are grey until the special is
    /// ready. The tap and drag pictures that once filled the two halves (Resources/BAM/Menu/special_&lt;hero&gt;_tap and _drag,
    /// bam-work/art/special-icons) went out with the one-tap special (Rules.OneTapSpecial): the hero's one special icon covers
    /// the disc (Hud.Begin), and the 18 pictures left Resources (full review 2026-11-09 nr 85).
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color SplitGrey = new Color(70f / 255f, 74f / 255f, 92f / 255f, 0.45f),
            LevelCharging = new Color(1f, 180f / 255f, 50f / 255f, 0.3f), LevelReady = new Color(1f, 196f / 255f, 64f / 255f, 0.38f);

        const int TapDragHideAfter = 10;
        Image specialFillRight, splitGrey;
        RectTransform[] splitLevel = new RectTransform[2];
        Image[] splitLevelImg = new Image[2];

        // after the icon, before the gold rim (the old draw order: icon, grey halves, gold level, line and labels, rim)
        void BuildSplit(RectTransform fillLeft)
        {
            const float R = SpecialButton.Radius;
            // the charge ring as two arcs from the bottom: the left one clockwise, the right one the other way
            specialFill.fillOrigin = (int)Image.Origin360.Bottom;
            specialFill.fillClockwise = true;
            specialFillRight = Object.Instantiate(specialFill.gameObject, fillLeft.parent).GetComponent<Image>();
            specialFillRight.name = "ChargeRight";
            specialFillRight.transform.SetSiblingIndex(fillLeft.GetSiblingIndex() + 1);
            specialFillRight.fillClockwise = false;

            // grey over both halves while not ready (old: the whole radius, over the ring too)
            splitGrey = Circle(specialRoot, "Grey", 2f * R, SplitGrey).GetComponent<Image>();

            // the gold level, one per half, over the grey and clipped round to the icon's disc (old: 0.87 R)
            var levelMask = Circle(specialRoot, "Level", 2f * 0.87f * R, Color.white);
            levelMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            for (int h = 0; h < 2; h++)
            {
                var rt = UiKit.NewRect(h == 0 ? "LevelTap" : "LevelDrag", levelMask);
                rt.anchorMin = new Vector2(h == 0 ? 0f : 0.5f, 0f);
                rt.anchorMax = new Vector2(h == 0 ? 0.5f : 1f, 0f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                splitLevelImg[h] = rt.gameObject.AddComponent<Image>();
                splitLevelImg[h].raycastTarget = false;
                splitLevel[h] = rt;
            }

            if (Rules.OneTapSpecial) return; // one special: no middle line, no TAP / DRAG labels
            // the line between the halves
            var line = UiKit.NewRect("Line", specialRoot);
            line.anchorMin = line.anchorMax = line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(Mathf.Max(1f, R * 0.03f), 2f * 0.78f * R);
            var li = line.gameObject.AddComponent<Image>();
            li.color = new Color(1f, 1f, 1f, 0.25f);
            li.raycastTarget = false;

            // which half is which
            for (int h = 0; h < 2; h++)
            {
                // new players only: after TapDragHideAfter counted matches the labels are gone (Rickard 2026-10-31)
                if (OnlineHooks.Matches != null && OnlineHooks.Matches() >= TapDragHideAfter) break;
                var t = UiKit.Label(specialRoot, h == 0 ? "TAP" : "DRAG", Mathf.RoundToInt(R * 0.26f), TextAnchor.MiddleCenter,
                    new Color(1f, 1f, 1f, 0.8f));
                UiKit.Place(t, 0.5f, 0.5f, (h == 0 ? -1f : 1f) * R * 0.45f, -R * 0.63f, R, R * 0.4f);
                UiKit.AddOutline(t, 2);
            }
        }

        // The charge ring fills by ANGLE up both sides from the bottom, so a level that rose by plain height ran ahead of the ring
        // (at a third charged the level stood at a third of the height, the ring tips only at a quarter; Rickard 2026-11-03 "dom
        // fylls inte jämt yttre och inre lagret"). This is the height where the ring's tips are, so the level and the veil meet
        // them exactly; both circles share the centre, so the same fraction fits the inner disc.
        static float RingHeight(float frac) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * Mathf.Clamp01(frac));

        // every frame, from Special()
        void Split(float frac, bool ready)
        {
            specialFill.fillAmount = specialFillRight.fillAmount = 0.5f * frac;
            specialFillRight.color = specialFill.color;
            splitGrey.enabled = !ready;
            for (int h = 0; h < 2; h++)
            {
                bool on = frac > 0.001f;
                splitLevelImg[h].enabled = on;
                if (!on) continue;
                splitLevelImg[h].color = ready ? LevelReady : LevelCharging;
                splitLevel[h].anchorMax = new Vector2(h == 0 ? 0.5f : 1f, RingHeight(frac));
            }
        }
    }
}
