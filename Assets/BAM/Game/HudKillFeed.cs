using BAM.Sim;
using BAM.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.Game
{
    /// <summary>
    /// The kill feed (Rickard 2026-10-27 "vi saknar kill feed"). The old game's feed was text rows ("Fenn killed Knut" with tags)
    /// that Rickard took out on 2026-10-20, so this one follows MLBB instead: picture only, no words. Under the centre of the
    /// glass strip, newest on top, at most three rows, each 4 s (the old FeedRow t = 4): a glass plate tinted in the killer's
    /// team colour, the killer's round portrait, the old game's crossed swords (menu_nav_battleIcon) and the victim's portrait,
    /// dimmed. Edges in team colour, gold on the player as in the strip. A death to the tower or a minion shows the swords and
    /// the victim alone. Built from the sim's Death events, so every phone in an online match shows the same feed.
    /// </summary>
    sealed partial class Hud
    {
        const int FeedRows = 3;
        const float FeedLife = 4f, FeedFade = 0.5f, FeedPop = 0.22f;

        sealed class FeedSide
        {
            public RectTransform Root;
            public RawImage Pic;
            public Image Edge, Dark;
        }

        sealed class FeedRow
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Back, Tint, Gold;
            public RectTransform Swords;
            public FeedSide Killer, Victim;
            public bool HasKiller;
            public float Born = -99f, Y;
        }

        readonly FeedRow[] feed = new FeedRow[FeedRows];
        readonly System.Collections.Generic.Dictionary<HeroId, Texture2D> feedPics = new System.Collections.Generic.Dictionary<HeroId, Texture2D>();
        float fD, fPad, fGap, fSw, fH, fTop, fStep, fW;

        void BuildKillFeed()
        {
            Sprite swords = null;
            var tex = Resources.Load<Texture2D>("BAM/Hud/menu_nav_battleIcon_inactive");
            if (tex != null) swords = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            else Debug.LogError("[BAM.Game] HUD picture missing: Resources/BAM/Hud/menu_nav_battleIcon_inactive");
            for (int i = 0; i < feed.Length; i++)
            {
                var r = new FeedRow();
                r.Root = TopRect("KillFeed");
                r.Root.pivot = new Vector2(0.5f, 1f);
                r.Group = r.Root.gameObject.AddComponent<CanvasGroup>();
                r.Group.blocksRaycasts = false; r.Group.interactable = false;
                r.Back = UiKit.Fill(r.Root, GlassFill, UiKit.Rounded);
                r.Tint = UiKit.Fill(r.Root, Color.clear, UiKit.Rounded);
                Edge(UiKit.Fill(r.Root, GlassEdge, UiKit.Ring));
                r.Gold = UiKit.Fill(r.Root, GlassGold, UiKit.Ring);
                r.Killer = FeedPortrait(r.Root);
                r.Victim = FeedPortrait(r.Root);
                var sw = UiKit.Fill(r.Root, Color.white, swords);
                sw.preserveAspect = true;
                r.Swords = sw.rectTransform;
                r.Swords.anchorMin = r.Swords.anchorMax = new Vector2(0f, 0.5f);
                r.Swords.pivot = new Vector2(0.5f, 0.5f);
                var sh = sw.gameObject.AddComponent<Shadow>(); // the hudText shadow, so the white swords read on light ground
                sh.effectColor = TextShadow;
                sh.effectDistance = new Vector2(0f, -2f);
                r.Root.gameObject.SetActive(false);
                feed[i] = r;
            }
        }

        FeedSide FeedPortrait(RectTransform parent)
        {
            var s = new FeedSide();
            s.Root = UiKit.NewRect("Portrait", parent);
            s.Root.anchorMin = s.Root.anchorMax = new Vector2(0f, 0.5f);
            s.Root.pivot = new Vector2(0.5f, 0.5f);
            var mask = UiKit.Fill(s.Root, PortraitBack, UiKit.Circle);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var pic = UiKit.NewRect("Pic", mask.transform);
            UiKit.Stretch(pic);
            s.Pic = pic.gameObject.AddComponent<RawImage>();
            s.Pic.raycastTarget = false;
            s.Pic.uvRect = new Rect(0.115f, 0f, 0.77f, 1f); // as the strip's portraits: the 400x308 renders, the sides cut
            s.Dark = UiKit.Fill(mask.transform, new Color(4f / 255f, 6f / 255f, 20f / 255f, 0.5f));
            s.Edge = UiKit.Fill(s.Root, MateEdge, UiKit.CircleRing);
            return s;
        }

        void BeginKillFeed()
        {
            for (int i = 0; i < feed.Length; i++) { feed[i].Born = -99f; feed[i].Root.gameObject.SetActive(false); }
        }

        void OnKillFeed(SimEvent e)
        {
            if (m.HeroTest) return;
            var victim = e.B;
            var killer = e.A;
            if (killer != null && killer.IsClone && killer.Owner != null) killer = killer.Owner;
            bool hasKiller = killer != null && killer.IsRealHero;
            // the oldest row (or a free one) becomes the newest
            FeedRow r = feed[0];
            for (int i = 0; i < feed.Length; i++)
            {
                if (!feed[i].Root.gameObject.activeSelf) { r = feed[i]; break; }
                if (feed[i].Born < r.Born) r = feed[i];
            }
            r.HasKiller = hasKiller;
            int killerTeam = Teams.Other(victim.Team);
            Color kc = killerTeam == m.Player.Team ? TopOwn : TopFoe;
            r.Tint.color = new Color(kc.r, kc.g, kc.b, 0.3f);
            SetFeedSide(r.Killer, hasKiller ? killer : null);
            SetFeedSide(r.Victim, victim);
            r.Victim.Dark.enabled = true;
            r.Born = Time.unscaledTime;
            r.Y = fTop - fStep * 0.6f; // it drops in from just above its place
            r.Root.SetAsLastSibling();
            r.Root.gameObject.SetActive(true);
            LayoutFeedRow(r);
        }

        void SetFeedSide(FeedSide s, Unit u)
        {
            s.Root.gameObject.SetActive(u != null);
            if (u == null) return;
            if (!feedPics.TryGetValue(u.Hero, out var tex))
                feedPics[u.Hero] = tex = Resources.Load<Texture2D>("BAM/Menu/portrait_" + HeroCatalog.FileName(HeroCatalog.Get(u.Hero).Name));
            s.Pic.texture = tex;
            s.Dark.enabled = false;
            s.Edge.color = u == m.Player ? PlayerEdge : u.Team == m.Player.Team ? TopOwn : TopFoe;
        }

        // sizes in fractions of the frame's width, as the strip: called from LayoutGlass
        void LayoutKillFeed(float W, float H)
        {
            fW = W;
            fD = W * 0.072f; fPad = W * 0.01f; fGap = W * 0.012f; fSw = fD * 0.78f;
            fH = fD + fPad * 2f;
            fTop = gM + gH + H * 0.014f;
            fStep = fH + W * 0.01f;
            for (int i = 0; i < feed.Length; i++) if (feed[i].Root.gameObject.activeSelf) LayoutFeedRow(feed[i]);
        }

        void LayoutFeedRow(FeedRow r)
        {
            if (fH <= 0f) return;
            float w = fPad * 2f + fSw + fGap + fD + (r.HasKiller ? fD + fGap : fPad * 0.5f);
            r.Root.sizeDelta = new Vector2(w, fH);
            Corners(r.Back, fH / 2f);
            Corners(r.Tint, fH / 2f);
            for (int i = 0; i < r.Root.childCount; i++)
            {
                var img = r.Root.GetChild(i).GetComponent<Image>();
                if (img != null && img.color == GlassEdge) RoundRing(img, fH / 2f, 1.5f);
            }
            RoundRing(r.Gold, fH / 2f, 1.5f);
            float x = fPad;
            if (r.HasKiller) { FeedSideAt(r.Killer, x + fD / 2f); x += fD + fGap; }
            else x += fPad * 0.5f;
            r.Swords.sizeDelta = new Vector2(fSw, fSw);
            r.Swords.anchoredPosition = new Vector2(x + fSw / 2f, 0f);
            x += fSw + fGap;
            FeedSideAt(r.Victim, x + fD / 2f);
        }

        void FeedSideAt(FeedSide s, float x)
        {
            s.Root.sizeDelta = new Vector2(fD, fD);
            s.Root.anchoredPosition = new Vector2(x, 0f);
        }

        void KillFeedFrame(float dt)
        {
            float now = Time.unscaledTime;
            // the live rows, newest first, each sliding to its slot
            for (int i = 0; i < feed.Length; i++)
                if (feed[i].Root.gameObject.activeSelf && now - feed[i].Born >= FeedLife) feed[i].Root.gameObject.SetActive(false);
            int slot = 0;
            for (int n = 0; n < feed.Length; n++)
            {
                FeedRow r = null;
                for (int i = 0; i < feed.Length; i++) if (feed[i].Root.gameObject.activeSelf && FeedRank(feed[i]) == n) { r = feed[i]; break; }
                if (r == null) continue;
                float age = now - r.Born, target = fTop + slot * fStep;
                r.Y = Mathf.Lerp(r.Y, target, 1f - Mathf.Exp(-dt * 16f));
                r.Root.anchoredPosition = new Vector2(fW / 2f, -r.Y);
                // a small spring in (as the damage numbers), then a fade over the last half second
                float pop = 1f;
                if (age < FeedPop) { float k = age / FeedPop; pop = 1f + 0.18f * Mathf.Cos(k * Mathf.PI * 1.5f) * (1f - k); }
                r.Root.localScale = Vector3.one * pop;
                r.Group.alpha = age < 0.08f ? age / 0.08f : Mathf.Clamp01((FeedLife - age) / FeedFade);
                slot++;
            }
        }

        // 0 for the newest live row, 1 for the next...
        int FeedRank(FeedRow r)
        {
            int rank = 0;
            for (int i = 0; i < feed.Length; i++) if (feed[i] != r && feed[i].Root.gameObject.activeSelf && feed[i].Born > r.Born) rank++;
            return rank;
        }
    }
}
