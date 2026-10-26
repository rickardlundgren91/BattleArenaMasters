using System.Collections.Generic;
using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The marks the old game draws by the heroes' health bars (ABC2 Hud.cs drawHero): the name over the player's own hero only
    /// (white with a dark edge), a thin crossed-out eye over a hero of the player's team while the enemy cannot see it (fading in
    /// and out over about 0.15 s), a small pulsing diamond by another hero's bar while its special is ready
    /// (HudTelegraph.cs drawCharged), and the interrupt window as a bar under the health bar (drawChannel). Placed each frame over the art's 3D bars.
    /// </summary>
    sealed partial class Hud
    {
        static readonly Color EyeStroke = Hex(0xdfe9e6), EyeEdge = Hex(0x08101a);

        sealed class Overhead
        {
            public RectTransform Root;
            public Text Name;
            public Image Eye, Diamond, ChanBack, ChanFill;
            public float EyeA;
        }

        readonly Overhead[] overheads = new Overhead[8];
        RectTransform overRoot;

        void BuildOverhead()
        {
            overRoot = UiKit.NewRect("Overhead", canvasRect);
            UiKit.Stretch(overRoot);
            overRoot.SetAsFirstSibling(); // under the HUD
            for (int i = 0; i < overheads.Length; i++)
            {
                var o = new Overhead();
                o.Root = UiKit.NewRect("Hero", overRoot);
                o.Root.anchorMin = o.Root.anchorMax = new Vector2(0.5f, 0.5f);
                o.Root.sizeDelta = Vector2.zero;
                o.Name = UiKit.Label(o.Root, "", 26, TextAnchor.LowerCenter, Color.white);
                o.Name.rectTransform.anchorMin = o.Name.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                o.Name.rectTransform.pivot = new Vector2(0.5f, 0f);
                o.Name.rectTransform.sizeDelta = new Vector2(300f, 40f);
                o.Name.horizontalOverflow = HorizontalWrapMode.Overflow;
                o.Name.raycastTarget = false;
                var edge = o.Name.gameObject.AddComponent<Outline>();
                edge.effectColor = new Color(8f / 255f, 10f / 255f, 24f / 255f, 0.9f);
                edge.effectDistance = new Vector2(1.5f, -1.5f);
                var eye = UiKit.NewRect("Eye", o.Root);
                eye.anchorMin = eye.anchorMax = eye.pivot = new Vector2(0.5f, 0.5f);
                o.Eye = eye.gameObject.AddComponent<Image>();
                o.Eye.sprite = EyeSprite;
                o.Eye.raycastTarget = false;
                var dia = UiKit.NewRect("Charged", o.Root);
                dia.anchorMin = dia.anchorMax = dia.pivot = new Vector2(0.5f, 0.5f);
                dia.localRotation = Quaternion.Euler(0f, 0f, 45f);
                o.Diamond = dia.gameObject.AddComponent<Image>();
                o.Diamond.raycastTarget = false;
                var dEdge = o.Diamond.gameObject.AddComponent<Outline>();
                dEdge.effectColor = new Color(0.07f, 0.05f, 0.09f, 0.9f);
                dEdge.effectDistance = new Vector2(1.5f, -1.5f);
                o.ChanBack = Plain(o.Root, "ChannelBack", new Color(8f / 255f, 10f / 255f, 24f / 255f, 0.85f));
                o.ChanFill = Plain(o.ChanBack.rectTransform, "Fill", Color.white);
                o.ChanFill.rectTransform.anchorMin = o.ChanFill.rectTransform.anchorMax = o.ChanFill.rectTransform.pivot = new Vector2(0f, 0.5f);
                o.Root.gameObject.SetActive(false);
                overheads[i] = o;
            }
        }

        void BeginOverhead()
        {
            for (int i = 0; i < overheads.Length; i++) { overheads[i].EyeA = 0f; overheads[i].Root.gameObject.SetActive(false); }
        }

        void OverheadFrame(bool hidden)
        {
            bool on = !hidden && !(m.Tut != null && m.Tut.Island) && m.State != MatchState.Over;
            float ts = canvasRect.rect.width / 11.5f; // one tile in canvas units (the camera frames 11.5 tiles across)
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < overheads.Length; i++)
            {
                var o = overheads[i];
                Unit u = i < m.Heroes.Count ? m.Heroes[i] : null;
                Vector3 bar = Vector3.zero; float bw = 0f;
                bool show = on && u != null && u.Alive && views.BarOf(u, out bar, out bw);
                if (o.Root.gameObject.activeSelf != show) o.Root.gameObject.SetActive(show);
                if (!show) { o.EyeA = 0f; continue; }
                Vector3 sp = cam.Camera.WorldToScreenPoint(bar);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out Vector2 at);
                o.Root.anchoredPosition = at;
                float size = ts * 1.05f;

                // the name: over your own hero only (K3 clutter budget 2026-10-19), just above its bar
                bool mine = u == m.Player;
                if (o.Name.gameObject.activeSelf != mine) o.Name.gameObject.SetActive(mine);
                if (mine)
                {
                    string nm = HeroCatalog.Get(u.Hero).Name;
                    if (!ReferenceEquals(o.Name.text, nm)) o.Name.text = nm;
                    o.Name.fontSize = Mathf.RoundToInt(ts * 0.27f);
                    o.Name.rectTransform.anchoredPosition = new Vector2(0f, size * 0.12f);
                }

                // the crossed-out eye while the enemy cannot see this hero of yours
                bool hiddenEye = u.Team == m.Player.Team && m.HiddenFromFoes(u);
                o.EyeA = hiddenEye ? Mathf.Min(1f, o.EyeA + dt * 7f) : Mathf.Max(0f, o.EyeA - dt * 7f);
                bool eyeOn = o.EyeA > 0.01f;
                if (o.Eye.enabled != eyeOn) o.Eye.enabled = eyeOn;
                if (eyeOn)
                {
                    float r = ts * 0.22f, pop = 0.8f + 0.2f * o.EyeA;
                    o.Eye.rectTransform.sizeDelta = new Vector2(r * 3.2f * pop, r * 3.2f * pop);
                    o.Eye.rectTransform.anchoredPosition = new Vector2(0f, size * 0.17f + ts * 0.5f);
                    o.Eye.color = new Color(1f, 1f, 1f, o.EyeA);
                }

                // another hero whose special is ready (drawCharged): a pulsing diamond by the right end of its bar, red on an
                // enemy (it can fire now), mint on a teammate (who can follow up); none on the player's own hero
                bool charged = !mine && u.SpecialReady && !u.IsClone;
                if (o.Diamond.enabled != charged) o.Diamond.enabled = charged;
                if (charged)
                {
                    bool ally = u.Team == m.Player.Team;
                    float r = ts * 0.17f * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 9f) + 0.25f), side = r * 1.414f;
                    o.Diamond.rectTransform.sizeDelta = new Vector2(side, side);
                    o.Diamond.rectTransform.anchoredPosition = new Vector2(bw * ts / 2f + 8f, 0f);
                    o.Diamond.color = ally ? new Color(0.25f, 0.95f, 0.7f, 0.95f) : new Color(1f, 0.16f, 0.2f, 0.95f);
                    var ol = o.Diamond.GetComponent<Outline>();
                    ol.effectColor = ally ? new Color(0.85f, 1f, 0.93f, 0.9f) : new Color(1f, 0.85f, 0.85f, 0.9f);
                }

                // the interrupt window (drawChannel): a small bar under the health bar empties as it closes, orange and
                // blinking on an enemy (stun it now), pale on your own team
                float chT = 0f, chV = 0f;
                for (int s = 0; s < u.StatusCount; s++)
                {
                    var st = u.GetStatus(s);
                    if (st.Type == St.Channel && st.T > 0f) { chT = st.T; chV = st.V; break; }
                }
                bool chan = chV > 0f;
                if (o.ChanBack.enabled != chan) { o.ChanBack.enabled = chan; o.ChanFill.enabled = chan; }
                if (chan)
                {
                    bool foe = u.Team != m.Player.Team;
                    float k = Mathf.Clamp01(chT / chV), cw = bw * ts * 0.8f;
                    o.ChanBack.rectTransform.sizeDelta = new Vector2(cw + 2f, 6f);
                    o.ChanBack.rectTransform.anchoredPosition = new Vector2(0f, -ts * 0.12f - 6f);
                    o.ChanFill.rectTransform.anchoredPosition = new Vector2(1f, 0f);
                    o.ChanFill.rectTransform.sizeDelta = new Vector2(cw * k, 4f);
                    o.ChanFill.color = foe ? new Color(1f, 0.62f, 0.15f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 30f)) : new Color(1f, 1f, 1f, 0.7f);
                }
            }
        }

        static Image Plain(RectTransform parent, string name, Color color)
        {
            var rt = UiKit.NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color; img.raycastTarget = false; img.enabled = false;
            return img;
        }

        // The old drawHiddenEye: an almond outline, the pupil ring and a slash, thin pale strokes over a faint dark edge.
        static Sprite eyeSprite;
        static Sprite EyeSprite
        {
            get
            {
                if (eyeSprite != null) return eyeSprite;
                const int n = 96;
                float c = n / 2f, r = n / 3.2f, w = r * 1.25f, h = r * 0.62f;
                var lines = new List<Vector2[]>();
                lines.Add(Quad(new Vector2(c - w, c), new Vector2(c, c + h * 2f), new Vector2(c + w, c)));
                lines.Add(Quad(new Vector2(c + w, c), new Vector2(c, c - h * 2f), new Vector2(c - w, c)));
                var ring = new Vector2[33];
                for (int i = 0; i < ring.Length; i++) { float a = i * Mathf.PI * 2f / 32f; ring[i] = new Vector2(c + Mathf.Cos(a) * h * 0.55f, c + Mathf.Sin(a) * h * 0.55f); }
                lines.Add(ring);
                lines.Add(new[] { new Vector2(c - w * 0.8f, c - h * 1.3f), new Vector2(c + w * 0.8f, c + h * 1.3f) });
                float dark = Mathf.Max(2.2f, r * 0.2f) * 0.5f, pale = Mathf.Max(1.1f, r * 0.09f) * 0.5f;
                var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "BAM.Hud.Eye", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f);
                        float d = float.MaxValue;
                        foreach (var l in lines)
                            for (int i = 1; i < l.Length; i++) d = Mathf.Min(d, SegDist(p, l[i - 1], l[i]));
                        float aPale = Mathf.Clamp01(pale + 0.5f - d) * 0.8f, aDark = Mathf.Clamp01(dark + 0.5f - d) * 0.35f;
                        float a = aPale + aDark * (1f - aPale);
                        Color col = a > 0f ? Color.Lerp(EyeEdge, EyeStroke, aPale / Mathf.Max(1e-4f, a)) : Color.clear;
                        col.a = a;
                        px[y * n + x] = col;
                    }
                tex.SetPixels32(px);
                tex.Apply(false, true);
                eyeSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
                return eyeSprite;
            }
        }

        static Vector2[] Quad(Vector2 a, Vector2 ctl, Vector2 b)
        {
            var pts = new Vector2[17];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / 16f, u = 1f - t;
                pts[i] = u * u * a + 2f * u * t * ctl + t * t * b;
            }
            return pts;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (a + ab * t - p).magnitude;
        }
    }
}
