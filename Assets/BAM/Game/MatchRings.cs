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
    /// at the player. Drawn with ArtKit's aim rings, tinted per ring, each with its own thin ring mesh: the old rings are a
    /// screen pixel or two wide (View3D.SyncRange's widths in pixels), and the aim ring's own band grows with its radius.
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
            public MeshFilter Filter;
            public Mesh Orig, Own;
            public float OwnInner = -1f;
        }

        // target: a dark edge, the gold band and a pulsing inner ring
        readonly Ring[] range = new Ring[3], charge = new Ring[1], reach = new Ring[1], target = new Ring[3], tower = new Ring[1];
        // Brawl Stars ideas 2 and 3 (Rickard 2026-10-26): a red ring under each enemy the dragged special would hit, and an orange
        // ring at the hero's feet while its special is ready
        readonly Ring[] hits = new Ring[8], ready = new Ring[2];
        // the battle spell's blink: where it lands while it is dragged (old SpellAimNow: a 0.45 ring on the ground)
        readonly Ring[] blink = new Ring[2];
        // the island tutorial's goal spot (old HudTutorialIsland.drawIslandWorld): two white rings that grow from it, 3 px
        readonly Ring[] goal = new Ring[2];
        bool goalOn;
        Vector3 goalAt;
        bool blinkOn;
        Vector2 blinkAt;
        Color blinkColor;
        static readonly Color RingEdge = new Color(0.03f, 0.05f, 0.1f, 0.4f);
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
            Make(range); Make(charge); Make(reach); Make(target); Make(tower); Make(hits); Make(ready); Make(blink); Make(goal);
            blinkOn = false;
            aimOn = false;
        }

        public void End()
        {
            Free(range); Free(charge); Free(reach); Free(target); Free(tower); Free(hits); Free(ready); Free(blink); Free(goal);
            m = null; views = null; autoTarget = null;
        }

        static void Make(Ring[] rings)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var v = ArtKit.CreateAim();
                MeshRenderer rr = null;
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true)) if (r.gameObject.name == "Range") rr = r;
                var mf = rr != null ? rr.GetComponent<MeshFilter>() : null;
                rings[i] = new Ring { View = v, Renderer = rr, Block = new MaterialPropertyBlock(), Filter = mf, Orig = mf != null ? mf.sharedMesh : null };
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
                    if (rings[i].Filter != null && rings[i].Orig != null) rings[i].Filter.sharedMesh = rings[i].Orig; // and to its own band
                    if (rings[i].Own != null) Object.Destroy(rings[i].Own);
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

        /// <summary>The blink being dragged: where it would land, in the rules' coordinates. Set every frame by the HUD.</summary>
        public void SetBlink(bool on, Vector2 at, Color color)
        {
            blinkOn = on; blinkAt = at; blinkColor = color;
        }

        /// <summary>The island tutorial's goal spot (world position), or off. Set every frame by the HUD.</summary>
        public void SetGoal(bool on, Vector3 at)
        {
            goalOn = on; goalAt = at;
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

        /// <param name="px">World units per screen pixel at the focus (the old rings' widths are in pixels).</param>
        public void Frame(bool hidden, float px)
        {
            HideAll();
            if (goalOn && !hidden)
            {
                float gk = (Time.unscaledTime / 0.9f) % 1f;
                for (int i = 0; i < goal.Length; i++)
                {
                    float gr = ((gk + 0.5f * i) % 1f) * 0.9f + 0.2f;
                    Show(goal[i], goalAt, gr + 1.5f * px, new Color(1f, 1f, 1f, 0.9f - gr * 0.8f), 3f * px);
                }
            }
            var p = m.Player;
            // the island shows the range ring too (old View3D.SyncRange ran there as well)
            if (hidden || p == null || !p.Alive || m.State == MatchState.Over) return;
            Vector3 at = views.DrawnAt(p);
            at.y = 0f;
            float r = p.Range;

            // the auto range (View3D.SyncRange's 1.2 px at 0.2, flashing on a swing). Rickard 2026-10-26 "man ser knappt cirkeln" on
            // the stone floor: stronger than the old one, 2 px white at 0.55 between two thin dark edges (as the old "you" ring is
            // edged), so it reads on light stone and dark grass alike; a swing still flashes it wider and brighter
            float hit = Mathf.Clamp01(1f - (Time.time - swingAt) / 0.25f);
            float ew = (2f + 1.5f * hit) * px, dw = 1f * px;
            Show(range[0], at, r + ew / 2f, new Color(1f, 1f, 1f, 0.55f + 0.4f * hit), ew);
            Show(range[1], at, r + ew / 2f + dw, RingEdge, dw);
            Show(range[2], at, r - ew / 2f, RingEdge, dw);

            // the attack charge grows out to the range until the next swing (none while stunned or disarmed)
            bool blocked = p.Has(St.Stun) || p.Has(St.Disarm);
            float k = blocked ? 0f : Mathf.Clamp01(p.AtkTimer / Mathf.Max(0.05f, p.AttackCd));
            if (k > 0.02f && k < 1f)
            {
                float cw = 1.5f * px;
                Show(charge[0], at, r * k + cw / 2f, new Color(1f, 1f, 1f, 0.15f + 0.3f * k), cw);
            }

            // the special's own reach while it is ready, if it differs from the auto range
            if (p.SpecialReady && p.HDef != null)
            {
                float sr = p.HDef.TapReach;
                if (sr > 0.5f && Mathf.Abs(sr - r) > 0.25f) Show(reach[0], at, sr + 0.6f * px, new Color(1f, 1f, 1f, 0.12f), 1.2f * px);
            }

            // a thick gold ring on who the last auto attack picked: held, then fading over the last 0.4 s
            float age = Time.time - autoAt;
            if (autoTarget != null && autoTarget.Alive && age >= 0f && age < 1.1f && m.IsVisibleTo(autoTarget, p.Team))
            {
                float fa = age < 0.7f ? 1f : 1f - (age - 0.7f) / 0.4f, pop = Mathf.Max(0f, 1f - age / 0.15f);
                float rr = autoTarget.R + 0.22f + 0.12f * pop;
                Vector3 tp = views.DrawnAt(autoTarget); tp.y = 0f;
                float gw = 3.5f * px, ri = rr * (0.72f + 0.06f * Mathf.Sin(age * 18f));
                Show(target[0], tp, rr + gw + 1.5f * px, new Color(Dark.r, Dark.g, Dark.b, 0.55f * fa), 2f * (gw + 1.5f * px));
                Show(target[1], tp, rr + gw, new Color(Gold.r, Gold.g, Gold.b, fa), 2f * gw);
                Show(target[2], tp, ri + 1.2f * px, new Color(Gold.r, Gold.g, Gold.b, 0.7f * fa), 2.4f * px);
            }

            // ready: an orange ring at the hero's feet, breathing
            if (p.SpecialReady)
            {
                float br = 0.5f + 0.5f * Mathf.Sin(Time.time * 5f);
                float rw = 2.5f * px, rr0 = Mathf.Max(0.55f, p.R + 0.35f) + 0.12f; // just outside the green "you" ring
                Show(ready[0], at, rr0 + rw, new Color(ReadyOrange.r, ReadyOrange.g, ReadyOrange.b, 0.65f + 0.3f * br), rw);
                Show(ready[1], at, rr0 + rw + (1f + 2f * br) * px * 2f, new Color(ReadyOrange.r, ReadyOrange.g, ReadyOrange.b, 0.35f * br), 1.5f * px);
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
                    Show(hits[n++], up, u.R + 0.25f, new Color(HitRed.r, HitRed.g, HitRed.b, 0.9f), 3f * px);
                }
            }

            if (blinkOn)
            {
                Vector3 bp = Coords.World(blinkAt.x, blinkAt.y);
                Show(blink[0], bp, 0.45f, new Color(blinkColor.r, blinkColor.g, blinkColor.b, 0.9f), 3f * px);
                Show(blink[1], bp, 0.45f * (0.55f + 0.1f * Mathf.Sin(Time.time * 10f)), new Color(blinkColor.r, blinkColor.g, blinkColor.b, 0.5f), 2f * px);
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
                    float w = (onMe ? 3f : 1.5f) * px;
                    Show(tower[0], tc, tr + w / 2f, onMe ? new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.75f) : new Color(1f, 1f, 1f, 0.25f), w);
                }
            }
        }

        void HideAll()
        {
            Hide(range); Hide(charge); Hide(reach); Hide(target); Hide(tower); Hide(hits); Hide(ready); Hide(blink); Hide(goal);
        }

        static void Hide(Ring[] rings)
        {
            for (int i = 0; i < rings.Length; i++) if (rings[i] != null) rings[i].View.Hide();
        }

        /// <summary>A ring whose outer edge is at radius and whose band is width wide (world units).</summary>
        static void Show(Ring ring, Vector3 center, float radius, Color color, float width)
        {
            if (ring == null || radius <= 0.01f) return;
            if (ring.Filter != null)
            {
                // the ring mesh runs from inner to 1 and is scaled by the radius: a fixed width needs its own inner edge
                float inner = Mathf.Clamp(1f - width / radius, 0f, 0.999f);
                inner = Mathf.Round(inner * 2000f) / 2000f; // rebuilt only when it really changes
                if (ring.Own == null || inner != ring.OwnInner)
                {
                    ring.OwnInner = inner;
                    if (ring.Own == null) ring.Own = new Mesh { name = "BAM.Ring" };
                    BuildRing(ring.Own, inner);
                }
                if (ring.Filter.sharedMesh != ring.Own) ring.Filter.sharedMesh = ring.Own;
            }
            ring.View.ShowRange(center, radius);
            if (ring.Renderer != null && color != ring.Shown)
            {
                ring.Shown = color;
                ring.Block.SetColor(BaseColor, color);
                ring.Renderer.SetPropertyBlock(ring.Block);
            }
        }

        static readonly Vector3[] ringV = new Vector3[2 * (RingSeg + 1)];
        static readonly Vector3[] ringN = new Vector3[2 * (RingSeg + 1)];
        static readonly Color[] ringC = new Color[2 * (RingSeg + 1)];
        static readonly Vector2[] ringUv = new Vector2[2 * (RingSeg + 1)];
        static int[] ringT;
        const int RingSeg = 96;
        static readonly Vector2 PaletteWhite = new Vector2(0.5f / 16f, 0.5f / 16f); // Art Pal.UV(Pal.White)

        static void BuildRing(Mesh mesh, float inner)
        {
            for (int i = 0; i <= RingSeg; i++)
            {
                float a = i * Mathf.PI * 2f / RingSeg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                ringV[2 * i] = new Vector3(c * inner, 0f, s * inner);
                ringV[2 * i + 1] = new Vector3(c, 0f, s);
                ringN[2 * i] = ringN[2 * i + 1] = Vector3.up;
                ringC[2 * i] = ringC[2 * i + 1] = Color.white;
                // the ring material colours through Art's 16x16 palette texture by UV: every vertex on cell 0 (white), so the band
                // is exactly the tint. (UVs running 0..1 across the palette made the rings a dim mix of its colours, 2026-10-26.)
                ringUv[2 * i] = ringUv[2 * i + 1] = PaletteWhite;
            }
            if (ringT == null)
            {
                ringT = new int[RingSeg * 6];
                for (int i = 0; i < RingSeg; i++)
                {
                    int v = 2 * i, t = 6 * i;
                    // facing up (clockwise seen from above, as Unity wants)
                    ringT[t] = v; ringT[t + 1] = v + 2; ringT[t + 2] = v + 1;
                    ringT[t + 3] = v + 2; ringT[t + 4] = v + 3; ringT[t + 5] = v + 1;
                }
            }
            mesh.Clear();
            mesh.vertices = ringV;
            mesh.normals = ringN;
            mesh.colors = ringC;
            mesh.uv = ringUv;
            mesh.triangles = ringT;
            mesh.RecalculateBounds();
        }
    }
}
