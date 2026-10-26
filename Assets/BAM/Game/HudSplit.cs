using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The SPECIAL button split down the middle (the old Hud.cs 743-796, the WoW wheel; Rickard 2026-10-26 "ikonen är delad på
    /// mitten och fylls nerifrån och upp för att visa att man kan klicka eller dra"): the left half is the tap, the right half the
    /// drag. The charge ring runs up both sides from the bottom and meets at the top, a gold level rises in each half over the
    /// icon, both halves are grey until the special is ready, a faint line parts them and TAP / DRAG sit under each half.
    /// With its own pictures (Resources/BAM/Menu/special_&lt;hero&gt;_tap and _drag) the tap picture fills the left half and the drag
    /// picture the right; without them the hero's one special icon covers the disc as before.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color SplitGrey = new Color(70f / 255f, 74f / 255f, 92f / 255f, 0.78f),
            LevelCharging = new Color(1f, 180f / 255f, 50f / 255f, 0.3f), LevelReady = new Color(1f, 196f / 255f, 64f / 255f, 0.38f);

        Image specialFillRight, splitGrey, tapIcon, dragIcon;
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

            // the two pictures, each centred in its half (old: 0.7 R across, 0.44 R off the middle, 0.12 R up)
            tapIcon = HalfIcon("Tap", -1f);
            dragIcon = HalfIcon("Drag", 1f);

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
                var t = UiKit.Label(specialRoot, h == 0 ? "TAP" : "DRAG", Mathf.RoundToInt(R * 0.26f), TextAnchor.MiddleCenter,
                    new Color(1f, 1f, 1f, 0.8f));
                UiKit.Place(t, 0.5f, 0.5f, (h == 0 ? -1f : 1f) * R * 0.45f, -R * 0.63f, R, R * 0.4f);
            }
        }

        Image HalfIcon(string name, float side)
        {
            const float R = SpecialButton.Radius;
            var rt = UiKit.NewRect(name + "Icon", specialFace.transform);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.one * 0.7f * R;
            rt.anchoredPosition = new Vector2(side * R * 0.44f, R * 0.12f);
            var img = rt.gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        // at the start of a match: the hero's own tap and drag pictures, if both are there
        bool BeginSplit(string hero)
        {
            var tap = Resources.Load<Texture2D>("BAM/Menu/special_" + hero + "_tap");
            var drag = Resources.Load<Texture2D>("BAM/Menu/special_" + hero + "_drag");
            bool both = tap != null && drag != null && tap != drag;
            SetIcon(tapIcon, both ? tap : null);
            SetIcon(dragIcon, both ? drag : null);
            return both;
        }

        static void SetIcon(Image img, Texture2D tex)
        {
            if (img.sprite != null) Object.Destroy(img.sprite); // the last match's sprite (the texture stays)
            img.sprite = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f) : null;
            img.enabled = tex != null;
        }

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
                splitLevel[h].anchorMax = new Vector2(h == 0 ? 0.5f : 1f, Mathf.Clamp01(frac));
            }
        }
    }
}
