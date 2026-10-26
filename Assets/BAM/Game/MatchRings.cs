using BAM.Art;
using BAM.Sim;
using UnityEngine;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The old game's rings on the ground round the player's hero (ABC2 View3D.cs SyncRange, Hud.cs): the auto-attack range as a
    /// thin white ring that flashes thicker on each attack, the attack charge as a ring growing out to it until the next swing,
    /// the special's own reach while it is ready (when it differs from the auto range), a gold ring on the unit the last auto
    /// attack picked (held 1.1 s), and an enemy tower's range when the player comes near it: faint white, red while it shoots
    /// at the player. Drawn with ArtKit's aim rings, tinted per ring.
    /// </summary>
    sealed class MatchRings
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly Color Gold = new Color(1f, 0.82f, 0.2f), Dark = new Color(0.05f, 0.04f, 0.1f), TowerRed = new Color(1f, 0.24f, 0.31f);

        sealed class Ring
        {
            public AimView View;
            public MeshRenderer Renderer;
            public MaterialPropertyBlock Block;
            public Color Shown = new Color(-1f, 0f, 0f, 0f);
        }

        // range: three rings side by side make it thicker on a hit; target: an outer dark edge, three gold, one pulsing inner
        readonly Ring[] range = new Ring[3], charge = new Ring[2], reach = new Ring[1], target = new Ring[5], tower = new Ring[3];
        // Brawl Stars ideas 2 and 3 (Rickard 2026-10-26): a red ring under each enemy the dragged special would hit, and an orange
        // ring at the hero's feet while its special is ready
        readonly Ring[] hits = new Ring[8], ready = new Ring[2];
        static readonly Color ReadyOrange = new Color(1f, 0.54f, 0.12f), HitRed = new Color(1f, 0.2f, 0.25f);
        bool aimOn, aimLine;
        Vector2 aimA, aimB;
        float aimHalf;
        Match m;
        MatchViews views;
        Unit autoTarget;
        float autoAt = -99f, swingAt = -99f;

        public void Begin(Match match, MatchViews matchViews)
        {
            m = match; views = matchViews;
            autoTarget = null; autoAt = swingAt = -99f;
            Make(range); Make(charge); Make(reach); Make(target); Make(tower); Make(hits); Make(ready);
            aimOn = false;
        }

        public void End()
        {
            Free(range); Free(charge); Free(reach); Free(target); Free(tower); Free(hits); Free(ready);
            m = null; views = null; autoTarget = null;
        }

        static void Make(Ring[] rings)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var v = ArtKit.CreateAim();
                MeshRenderer rr = null;
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true)) if (r.gameObject.name == "Range") rr = r;
                rings[i] = new Ring { View = v, Renderer = rr, Block = new MaterialPropertyBlock() };
                v.Hide();
            }
        }

        static void Free(Ring[] rings)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                if (rings[i] != null && rings[i].View != null)
                {
                    rings[i].View.Hide();
                    if (rings[i].Renderer != null) rings[i].Renderer.SetPropertyBlock(null); // the pooled view goes back white
                    ArtKit.Release(rings[i].View);
                }
                rings[i] = null;
            }
        }

        /// <summary>The special being dragged, in the rules' coordinates: a line from a to b (half wide), or a circle at a (half its
        /// radius). Set every frame by the HUD; off when the finger is not aiming.</summary>
        public void SetAim(bool on, bool line, Vector2 a, Vector2 b, float half)
        {
            aimOn = on; aimLine = line; aimA = a; aimB = b; aimHalf = half;
        }

        public void AfterStep()
        {
            var ev = m.Events;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                if (e.Type == EventType.AttackStart && e.A == m.Player)
                {
                    swingAt = Time.time;
                    if (e.B != null) { autoTarget = e.B; autoAt = Time.time; }
                }
            }
        }

        public void Frame(bool hidden)
        {
            HideAll();
            var p = m.Player;
            if (hidden || p == null || !p.Alive || m.State == MatchState.Over || (m.Tut != null && m.Tut.Island)) return;
            Vector3 at = views.DrawnAt(p);
            at.y = 0f;
            float r = p.Range;

            // the auto range: thin and faint, a hit flashes it thicker and brighter for a quarter second
            float hit = Mathf.Clamp01(1f - (Time.time - swingAt) / 0.25f);
            // the old game's alphas (View3D.SyncRange): 0.2 resting, up to 0.6 on a swing. Faint, or it frames the hero.
            Show(range[0], at, r, new Color(1f, 1f, 1f, hit > 0f ? 0.2f + 0.4f * hit : 0.2f));
            if (hit > 0f)
            {
                Show(range[1], at, r * (1f + 0.02f * hit), new Color(1f, 1f, 1f, 0.35f * hit));
                Show(range[2], at, r * (1f - 0.02f * hit), new Color(1f, 1f, 1f, 0.35f * hit));
            }

            // the attack charge grows out to the range until the next swing (none while stunned or disarmed)
            bool blocked = p.Has(St.Stun) || p.Has(St.Disarm);
            float k = blocked ? 0f : Mathf.Clamp01(p.AtkTimer / Mathf.Max(0.05f, p.AttackCd));
            if (k > 0.02f && k < 1f)
            {
                Show(charge[0], at, r * k, new Color(1f, 1f, 1f, 0.15f + 0.3f * k)); // the old charge ring's own alpha
                Show(charge[1], at, r * k * 0.985f, new Color(1f, 1f, 1f, 0.1f + 0.2f * k));
            }

            // the special's own reach while it is ready, if it differs from the auto range
            if (p.SpecialReady && p.HDef != null)
            {
                float sr = p.HDef.TapReach;
                if (sr > 0.5f && Mathf.Abs(sr - r) > 0.25f) Show(reach[0], at, sr, new Color(1f, 1f, 1f, 0.12f));
            }

            // a thick gold ring on who the last auto attack picked: held, then fading over the last 0.4 s
            float age = Time.time - autoAt;
            if (autoTarget != null && autoTarget.Alive && age >= 0f && age < 1.1f && m.IsVisibleTo(autoTarget, p.Team))
            {
                float fa = age < 0.7f ? 1f : 1f - (age - 0.7f) / 0.4f, pop = Mathf.Max(0f, 1f - age / 0.15f);
                float rr = autoTarget.R + 0.22f + 0.12f * pop;
                Vector3 tp = views.DrawnAt(autoTarget); tp.y = 0f;
                Show(target[0], tp, rr * 1.12f, new Color(Dark.r, Dark.g, Dark.b, 0.55f * fa));
                Show(target[1], tp, rr * 1.07f, new Color(Gold.r, Gold.g, Gold.b, fa));
                Show(target[2], tp, rr * 1.035f, new Color(Gold.r, Gold.g, Gold.b, fa));
                Show(target[3], tp, rr, new Color(Gold.r, Gold.g, Gold.b, fa));
                Show(target[4], tp, rr * (0.72f + 0.06f * Mathf.Sin(age * 18f)), new Color(Gold.r, Gold.g, Gold.b, 0.7f * fa));
            }

            // ready: an orange ring at the hero's feet, breathing
            if (p.SpecialReady)
            {
                float br = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                Show(ready[0], at, p.R + 0.2f, new Color(ReadyOrange.r, ReadyOrange.g, ReadyOrange.b, 0.65f + 0.3f * br));
                Show(ready[1], at, (p.R + 0.2f) * (1.06f + 0.05f * br), new Color(ReadyOrange.r, ReadyOrange.g, ReadyOrange.b, 0.35f * br));
            }

            // the dragged special's hit preview: who is inside the shape now (walls are not checked: a guide, not a promise)
            if (aimOn)
            {
                int n = 0;
                var us = m.Units;
                for (int i = 0; i < us.Count && n < hits.Length; i++)
                {
                    var u = us[i];
                    if (!u.Alive || u.Team == p.Team || u.Kind == UnitKind.Tower || !m.IsVisibleTo(u, p.Team)) continue;
                    var q = new Vector2(u.X, u.Y);
                    float d;
                    if (aimLine)
                    {
                        Vector2 ab = aimB - aimA;
                        float t = Mathf.Clamp01(Vector2.Dot(q - aimA, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                        d = (aimA + ab * t - q).magnitude;
                    }
                    else d = (q - aimA).magnitude;
                    if (d > aimHalf + u.R) continue;
                    Vector3 up = views.DrawnAt(u); up.y = 0f;
                    Show(hits[n++], up, u.R + 0.25f, new Color(HitRed.r, HitRed.g, HitRed.b, 0.9f));
                }
            }

            // an enemy tower near the player: its reach, red while it shoots at the player
            var tw = m.Towers[Teams.Other(p.Team)];
            if (tw != null && tw.Alive)
            {
                float tr = tw.Range, dx = tw.X - p.X, dy = tw.Y - p.Y;
                if (dx * dx + dy * dy < (tr + 2f) * (tr + 2f))
                {
                    bool onMe = tw.Target == p;
                    Vector3 tc = Coords.Of(tw); tc.y = 0f;
                    Show(tower[0], tc, tr, onMe ? new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.75f) : new Color(1f, 1f, 1f, 0.35f));
                    if (onMe)
                    {
                        Show(tower[1], tc, tr * 0.985f, new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.75f));
                        Show(tower[2], tc, tr * 0.97f, new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.35f));
                    }
                }
            }
        }

        void HideAll()
        {
            Hide(range); Hide(charge); Hide(reach); Hide(target); Hide(tower); Hide(hits); Hide(ready);
        }

        static void Hide(Ring[] rings)
        {
            for (int i = 0; i < rings.Length; i++) if (rings[i] != null) rings[i].View.Hide();
        }

        static void Show(Ring ring, Vector3 center, float radius, Color color)
        {
            if (ring == null || radius <= 0.01f) return;
            ring.View.ShowRange(center, radius);
            if (ring.Renderer != null && color != ring.Shown)
            {
                ring.Shown = color;
                ring.Block.SetColor(BaseColor, color);
                ring.Renderer.SetPropertyBlock(ring.Block);
            }
        }
    }
}
