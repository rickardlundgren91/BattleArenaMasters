using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// Hold to read (the old HudHold.cs, Rickard 2026-10-14; asked for again 2026-10-26 "hålla inne för att läsa om skill och
    /// passiv"): keep a finger on the SPECIAL button for 0.4 s and a thin white ring fills round it, then a small see-through card
    /// in the top right corner says what the special does, the hero's passive and how it charges. Letting go after the card has
    /// shown casts nothing (Rickard 2026-10-26); a short tap casts as always, a drag aims and shows no card. Not in the tutorial.
    /// </summary>
    sealed partial class Hud
    {
        const float HoldTime = 0.4f, HoldFade = 0.12f;
        static readonly Color HoldBack = new Color(12f / 255f, 16f / 255f, 44f / 255f, 0.72f), HoldGold = Hex(0xffc94a),
            HoldDesc = Hex(0xe6e9ff), HoldBlue = Hex(0x9fd8ff), HoldDesc2 = Hex(0xc9cfee), HoldFoot = Hex(0x8f96c4);

        RectTransform holdCard;
        CanvasGroup holdLook;
        Image holdRing;
        Text holdTag, holdName, holdDesc, holdName2, holdDesc2, holdFoot;
        float holdLaidW = -1f;
        HeroId holdLaidHero = (HeroId)255;
        bool holdLaidReady;

        void BuildHold()
        {
            // the ring that fills while the finger stays: just outside the button (old: r * 1.12, 3 px)
            holdRing = UiKit.Fill(specialRoot, new Color(1f, 1f, 1f, 0.85f), HoldRingSprite);
            holdRing.raycastTarget = false;
            holdRing.type = Image.Type.Filled;
            holdRing.fillMethod = Image.FillMethod.Radial360;
            holdRing.fillOrigin = (int)Image.Origin360.Top;
            holdRing.fillClockwise = true;
            var rr = holdRing.rectTransform;
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.sizeDelta = Vector2.one * (2f * 1.12f * SpecialButton.Radius + 8f);
            holdRing.enabled = false;

            holdCard = UiKit.NewRect("HoldCard", safe);
            holdCard.anchorMin = holdCard.anchorMax = new Vector2(0f, 1f);
            holdCard.pivot = new Vector2(0f, 1f);
            holdLook = holdCard.gameObject.AddComponent<CanvasGroup>();
            holdLook.blocksRaycasts = false; holdLook.interactable = false;
            var back = UiKit.Fill(holdCard, HoldBack, UiKit.Rounded);
            back.raycastTarget = false;
            var edge = UiKit.Fill(holdCard, HoldGold, UiKit.Ring); // the gold edge (UiKit.Ring: the same corner as Rounded)
            edge.type = Image.Type.Sliced;
            edge.raycastTarget = false;
            holdTag = HoldText(UiKit.Font, HoldGold);
            holdName = HoldText(UiKit.Font, Color.white);
            holdDesc = HoldText(UiKit.BodyFont, HoldDesc);
            holdName2 = HoldText(UiKit.Font, HoldBlue);
            holdDesc2 = HoldText(UiKit.BodyFont, HoldDesc2);
            holdFoot = HoldText(UiKit.Font, HoldFoot);
            holdCard.gameObject.SetActive(false);
        }

        Text HoldText(Font font, Color color)
        {
            var t = UiKit.Label(holdCard, "", 20, TextAnchor.UpperLeft, color);
            t.font = font;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var r = t.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            return t;
        }

        void HoldFrame(bool hidden)
        {
            var p = m.Player;
            bool on = !hidden && m.Tut == null && p != null && special.Pressed && !special.Dragging && m.State != MatchState.Over;
            float el = on ? Time.unscaledTime - special.PressedAt : 0f;
            bool ring = on && el < HoldTime;
            if (holdRing.enabled != ring) holdRing.enabled = ring;
            if (ring) holdRing.fillAmount = Mathf.Clamp01(el / HoldTime);
            bool card = on && el >= HoldTime;
            if (holdCard.gameObject.activeSelf != card) holdCard.gameObject.SetActive(card);
            if (!card) return;
            float k = Mathf.Clamp01((el - HoldTime) / HoldFade);
            holdLook.alpha = k;
            LayoutHold(p);
            float H = safe.rect.height;
            holdCard.anchoredPosition = new Vector2(holdCard.anchoredPosition.x, -(H * 0.095f - (1f - k) * H * 0.01f));
        }

        // the card's text and size, rebuilt only when the hero, its readiness or the screen width changes
        void LayoutHold(Unit p)
        {
            float W = safe.rect.width;
            bool ready = p.Alive && p.SpecialReady;
            if (Mathf.Abs(W - holdLaidW) < 0.5f && p.Hero == holdLaidHero && ready == holdLaidReady) return;
            holdLaidW = W; holdLaidHero = p.Hero; holdLaidReady = ready;
            var info = HeroCatalog.Get(p.Hero);
            float pad = W * 0.025f, cw = W * 0.6f, tw = cw - pad * 2f;
            int nf = Mathf.RoundToInt(W * 0.038f), df = Mathf.RoundToInt(W * 0.029f), tf = Mathf.RoundToInt(W * 0.024f);
            float lh = df * 1.25f;
            string foot = "Charges with " + (p.HDef != null ? p.HDef.MaxCharge : 0) + " hits" + (ready ? " · READY" : "");

            float y = pad;
            y = Put(holdTag, "SPECIAL", tf, pad, y, tw) + tf * 0.4f;
            y = Put(holdName, info.SpecialName, nf, pad, y, tw) + nf * 0.2f;
            y = Put(holdDesc, info.SpecialDesc, df, pad, y, tw, lh);
            y += df * 0.6f;
            y = Put(holdName2, "Passive: " + info.PassiveName, df, pad, y, tw) + df * 0.35f;
            y = Put(holdDesc2, info.PassiveDesc, df, pad, y, tw, lh);
            y += df * 0.5f;
            y = Put(holdFoot, foot, df, pad, y, tw) + pad * 0.8f;

            holdCard.sizeDelta = new Vector2(cw, y);
            holdCard.anchoredPosition = new Vector2(W * 0.97f - cw, holdCard.anchoredPosition.y);
        }

        // one block of text at (x, y) from the card's top, wrapped to w; returns the y under it
        static float Put(Text t, string text, int size, float x, float y, float w, float lineHeight = 0f)
        {
            t.text = text ?? "";
            t.fontSize = Mathf.Max(10, size);
            if (lineHeight > 0f) t.lineSpacing = lineHeight / (size * 1.15f);
            var r = t.rectTransform;
            r.sizeDelta = new Vector2(w, 10f);
            r.anchoredPosition = new Vector2(x, -y);
            float h = t.preferredHeight;
            r.sizeDelta = new Vector2(w, h);
            return y + h;
        }

        static Sprite holdRingSprite;

        // a thin ring across the whole sprite (radial fill runs round it)
        static Sprite HoldRingSprite
        {
            get
            {
                if (holdRingSprite != null) return holdRingSprite;
                const int n = 256;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.HoldRing", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                float h = n / 2f, w = 3.5f;
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = new Vector2(x + 0.5f - h, y + 0.5f - h).magnitude, a = Mathf.Clamp01(w / 2f + 0.5f - Mathf.Abs(d - (h - w)));
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(255f * a));
                    }
                tex.SetPixels32(px); tex.Apply(false, true);
                holdRingSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return holdRingSprite;
            }
        }
    }
}
