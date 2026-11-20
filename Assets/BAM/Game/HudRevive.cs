using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// Revive shown to everyone (Rickard 2026-11-20: "något som indikerar att om man dör med ress item så väntar en ress så att de
    /// andra spelarna vet och det inte blir en överraskning varje gång"). Display only, no rule changes:
    /// - while a hero's Revive is ready (Match.ReviveArmed), a small phoenix-orange rising-arrow badge sits by the right end of its
    ///   health bar, on allies and enemies alike (past an enemy's red special diamond when that shows);
    /// - when such a hero falls (Unit.ReviveComing), a bigger badge stays over the body with a ring that fills over the
    ///   ReviveDelay wait and the word REVIVE under it, while SpellFx.ReviveWait puts a ring and embers on the ground.
    /// A Fenn clone wears its owner's badge, so the decoy is not given away.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color ReviveOrange = Hex(0xffa070), ReviveDisc = new Color(0.16f, 0.07f, 0.04f, 0.85f);

        sealed class ReviveWait
        {
            public RectTransform Root;
            public Image Disc, Ring, Icon;
            public Text Word;
        }

        readonly ReviveWait[] reviveWaits = new ReviveWait[6];

        void BuildReviveMark(Overhead o)
        {
            var rt = UiKit.NewRect("Revive", o.Root);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            o.ReviveBack = rt.gameObject.AddComponent<Image>();
            o.ReviveBack.sprite = UiKit.Circle; o.ReviveBack.color = ReviveDisc; o.ReviveBack.raycastTarget = false; o.ReviveBack.enabled = false;
            var ic = UiKit.NewRect("Icon", rt);
            UiKit.Stretch(ic);
            ic.localScale = new Vector3(0.78f, 0.78f, 1f);
            o.ReviveIcon = ic.gameObject.AddComponent<Image>();
            o.ReviveIcon.sprite = SpellGlyphSprite(SpellId.Revive, out _); o.ReviveIcon.preserveAspect = true;
            o.ReviveIcon.color = ReviveOrange; o.ReviveIcon.raycastTarget = false;
        }

        /// <summary>The ready badge by the bar; charged = the enemy's red special diamond is showing on the same side.</summary>
        void ReviveMarkFrame(Overhead o, Unit who, float ts, float bw, bool charged)
        {
            bool show = m.ReviveArmed(who);
            if (o.ReviveBack.enabled != show) o.ReviveBack.enabled = show;
            if (!show) return;
            float r = ts * 0.15f * (1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f));
            float x = bw * ts / 2f + 8f + (charged ? ts * 0.34f : 0f) + r;
            o.ReviveBack.rectTransform.sizeDelta = new Vector2(2f * r, 2f * r);
            o.ReviveBack.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        void BuildReviveWaits()
        {
            for (int i = 0; i < reviveWaits.Length; i++)
            {
                var w = new ReviveWait();
                w.Root = UiKit.NewRect("ReviveWait", overRoot);
                w.Root.anchorMin = w.Root.anchorMax = w.Root.pivot = new Vector2(0.5f, 0.5f);
                w.Disc = w.Root.gameObject.AddComponent<Image>();
                w.Disc.sprite = UiKit.Circle; w.Disc.color = ReviveDisc; w.Disc.raycastTarget = false;
                var ring = UiKit.NewRect("Ring", w.Root);
                UiKit.Stretch(ring);
                w.Ring = ring.gameObject.AddComponent<Image>();
                w.Ring.sprite = UiKit.CircleRing; w.Ring.type = Image.Type.Filled; w.Ring.fillMethod = Image.FillMethod.Radial360;
                w.Ring.fillOrigin = (int)Image.Origin360.Top; w.Ring.fillClockwise = true;
                w.Ring.color = ReviveOrange; w.Ring.raycastTarget = false;
                var ic = UiKit.NewRect("Icon", w.Root);
                UiKit.Stretch(ic);
                ic.localScale = new Vector3(0.7f, 0.7f, 1f);
                w.Icon = ic.gameObject.AddComponent<Image>();
                w.Icon.sprite = SpellGlyphSprite(SpellId.Revive, out _); w.Icon.preserveAspect = true;
                w.Icon.color = ReviveOrange; w.Icon.raycastTarget = false;
                w.Word = UiKit.Label(w.Root, "REVIVE", 20, TextAnchor.UpperCenter, ReviveOrange);
                w.Word.rectTransform.anchorMin = w.Word.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                w.Word.rectTransform.pivot = new Vector2(0.5f, 1f);
                w.Word.horizontalOverflow = HorizontalWrapMode.Overflow;
                w.Word.fontStyle = FontStyle.Bold;
                w.Word.raycastTarget = false;
                var edge = w.Word.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(8f / 255f, 10f / 255f, 24f / 255f, 0.9f);
                edge.effectDistance = new Vector2(1.5f, -1.5f);
                w.Root.gameObject.SetActive(false);
                reviveWaits[i] = w;
            }
        }

        void HideReviveWaits()
        {
            for (int i = 0; i < reviveWaits.Length; i++) if (reviveWaits[i] != null) reviveWaits[i].Root.gameObject.SetActive(false);
        }

        /// <summary>The badge over each fallen hero that Revive will stand up, with the wait filling round it.</summary>
        void ReviveWaitsFrame(bool on, float ts)
        {
            int n = 0;
            if (on)
                for (int i = 0; i < m.Heroes.Count && n < reviveWaits.Length; i++)
                {
                    var u = m.Heroes[i];
                    if (u == null || !u.ReviveComing) continue;
                    var w = reviveWaits[n++];
                    if (!w.Root.gameObject.activeSelf) w.Root.gameObject.SetActive(true);
                    Vector3 sp = cam.Camera.WorldToScreenPoint(Coords.Of(u) + Vector3.up * 1.6f);
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 at);
                    float k = 1f - Mathf.Clamp01(u.RespawnT / Mathf.Max(0.01f, SpellDefs.ReviveDelay));
                    float r = ts * 0.3f * (1f + 0.08f * Mathf.Sin(Time.unscaledTime * 10f));
                    w.Root.anchoredPosition = at;
                    w.Root.sizeDelta = new Vector2(2f * r, 2f * r);
                    w.Ring.fillAmount = k;
                    int font = Mathf.Max(8, Mathf.RoundToInt(ts * 0.2f));
                    if (w.Word.fontSize != font) w.Word.fontSize = font;
                    w.Word.rectTransform.sizeDelta = new Vector2(ts * 3f, ts * 0.4f);
                    w.Word.rectTransform.anchoredPosition = new Vector2(0f, -ts * 0.04f);
                }
            for (int i = n; i < reviveWaits.Length; i++)
                if (reviveWaits[i].Root.gameObject.activeSelf) reviveWaits[i].Root.gameObject.SetActive(false);
        }
    }
}
