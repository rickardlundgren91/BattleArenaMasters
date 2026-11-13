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
        readonly Ring[] range = new Ring[3], charge = new Ring[1], reach = new Ring[1], target = new Ring[3], tower = new Ring[2];
        // Brawl Stars ideas 2 and 3 (Rickard 2026-10-26): a red ring under each enemy the dragged special would hit, and an orange
        // ring at the hero's feet while its special is ready
        readonly Ring[] hits = new Ring[8], ready = new Ring[2];
        // the battle spell's blink: where it lands while it is dragged (old SpellAimNow: a 0.45 ring on the ground)
        readonly Ring[] blink = new Ring[2];
        // the tutorial's goal spot (where to walk). Was the old HudTutorialIsland.drawIslandWorld pair of thin white rings growing
        // from it; Rickard 2026-11-02 "ringarna som visar vart man ska gå måste bli tydligare": now a gold spot the size of the
        // arrival area (GoalR, the rules' 0.9 tiles), filled faintly, with a thick gold edge on a dark outline and a gold ripple
        // that keeps leaving it. Gold to match the guide arrow over it.
        readonly Ring[] goal = new Ring[6];
        public const float GoalR = 0.9f;
        /// <summary>The gold target ring's outer edge round a tower: just outside its model, not its hit edge. Rickard's Tripo tower
        /// is about 3 tiles across (Models.BuildTowerProp), so its stone foot hid the first ring at 1.15 (Rickard 2026-11-03 "märkte
        /// ingenting som visade att jag attackerade tornet").</summary>
        const float TowerRingR = 1.75f;
        bool goalOn;
        Vector3 goalAt;
        bool blinkOn;
        Vector2 blinkAt;
        Color blinkColor;
        static readonly Color RingEdge = new Color(0.03f, 0.05f, 0.1f, 0.4f);
        static readonly Color HitRed = new Color(1f, 0.2f, 0.25f);
        bool aimOn;
        AimInfo aimInfo;
        readonly System.Collections.Generic.List<Unit> aimHits = new System.Collections.Generic.List<Unit>(8);
        // Moa: the ally her tap would shield now (MOA-02); Bonk: his club's ground circle while it winds up (BONK-07)
        readonly Ring[] moaPick = new Ring[1], smash = new Ring[4];
        static readonly Color Honey = new Color(1f, 0.8f, 0.25f);
        Match m;
        MatchViews views;
        Unit autoTarget;
        float autoAt = -99f, swingAt = -99f;

        public void Begin(Match match, MatchViews matchViews)
        {
            m = match; views = matchViews;
            autoTarget = null; autoAt = swingAt = -99f; blowAt = -99f;
            Make(range, true); Make(charge); Make(reach); Make(target); Make(tower); Make(hits); Make(ready); Make(blink); Make(goal); Make(moaPick); Make(smash);
            blinkOn = false;
            aimOn = false;
        }

        public void End()
        {
            Free(range); Free(charge); Free(reach); Free(target); Free(tower); Free(hits); Free(ready); Free(blink); Free(goal); Free(moaPick); Free(smash);
            m = null; views = null; autoTarget = null;
        }

        /// <summary>overTerrain: the ring ignores depth and is drawn over the tower and walls too. The hero's white range ring was
        /// cut by the Tripo tower's stone foot and the walls (Rickard 2026-11-03 "heroens range ring den vita ritas under tornet så
        /// ska det inte va"). Health bars (Overlay, queue 4000), effects (3010+) and the HUD canvas still draw over it.</summary>
        static void Make(Ring[] rings, bool overTerrain = false)
        {
            for (int i = 0; i < rings.Length; i++)
            {
                var v = ArtKit.CreateAim();
                MeshRenderer rr = null;
                foreach (var r in v.GetComponentsInChildren<MeshRenderer>(true)) if (r.gameObject.name == "Range") rr = r;
                // the aim's own private range material (AimLook.rangeMat, destroyed with the aim), so only this ring changes. URP's Unlit
                // has no _ZTest, so setting it did nothing and the tower still hid the ring: the overlay shader instead (bug review 2026-11-03)
                if (overTerrain && rr != null) ArtKit.DrawOverAll(rr.sharedMaterial);
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

        /// <summary>The special being dragged: what the rules would cover if let go now (Sim Aim.cs). Set every frame by the HUD;
        /// off when the finger is not aiming.</summary>
        public void SetAim(bool on, in AimInfo a)
        {
            aimOn = on; aimInfo = a;
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
                if (e.Type == EventType.AttackStart && e.A == m.Player && e.B != null) { autoTarget = e.B; autoAt = Time.time; }
            }
            // the range ring flashes on the blow itself, when the charge ring is full (below), not at the start of the wind-up
            bool winding = Winding(m.Player, out _);
            if (wasWinding && !winding) { swingAt = Time.time; blowAt = m.Time; }
            wasWinding = winding;
        }
        bool wasWinding;
        float blowAt = -99f; // the rules' time of the player's last blow (its swing's release)

        // Rickard 2026-10-30 ("uppladdning före", all heroes): the charge ring is full on the blow, the frame the swing lands, not
        // on the rules' attack timer, which starts the wind-up (Bonk's 0.7 s swing began only once the ring was full). It counts
        // the time to the next blow: from the last blow (the timer at the wind-up) up to the attack cooldown, and through a swing
        // that is winding up, on to its release. The swing itself and the timer are the rules' own; only the ring is drawn later.
        float Charge(Unit p)
        {
            float cd = Mathf.Max(0.05f, p.AttackCd);
            if (Winding(p, out float left)) return Mathf.Clamp01(1f - left / cd);
            // between blows: the time since the last one over the cooldown, so it grows out to the range in one go and is full (gone)
            // while the attack is ready; AtkTimer starts over at every wind-up and when nobody is in reach, so it stopped short of
            // the range (Rickard 2026-10-30: "timercirkeln går inte hela vägen ut till gubbens range").
            // With nobody in reach it keeps pulsing in the attack's rhythm, one grow-out per cooldown, though no blow comes (Rickard
            // 2026-10-30: "Utan target ska den pulsera men inte slå"); the old one stood still, full. Drawn only: the rules' timer is
            // untouched, and a target stepping in still starts the swing at once.
            // Since 2026-11-04 it follows the rules' own timer (AtkTimer counts from the last wind-up's start): 0 on the blow, full one
            // cooldown later, as the rules' wait for a target ends (Combat: cd + wind-up + AutoHold), then over again. The old
            // pulse from the last blow drifted off the rules, so an enemy stepping in as the ring filled got no swing (tester
            // 2026-11-04 via Discord on Bonk: "it seems to target about 0.5 sec before the circle is filled up").
            return Mathf.Clamp01((p.AtkTimer - p.HDef.SpawnDelay) / cd);
        }

        // the player's auto attack still in the hand: how long until it is released (Bonk's club lands then; a shot leaves)
        bool Winding(Unit p, out float left)
        {
            left = 0f;
            if (p == null) return false;
            var ps = m.Projs;
            for (int i = 0; i < ps.Count; i++)
            {
                var q = ps[i];
                if (q.Src != p || q.Done || q.T >= 0f || (q.Mode != ProjMode.Auto && q.Mode != ProjMode.Smash)) continue;
                left = -q.T;
                return true;
            }
            return false;
        }

        /// <param name="px">World units per screen pixel at the focus (the old rings' widths are in pixels).</param>
        public void Frame(bool hidden, float px)
        {
            HideAll();
            if (goalOn && !hidden)
            {
                float now = Time.unscaledTime, beat = 0.5f + 0.5f * Mathf.Sin(now * 5f);
                float gw = 5f * px, ow = 1.5f * px, er = GoalR + 0.04f * beat;
                // the area itself, faintly filled and breathing
                Show(goal[0], goalAt, er, new Color(Gold.r, Gold.g, Gold.b, 0.18f + 0.12f * beat), er);
                // its edge: a dark outline either side of a thick gold band, so it reads on the light stone and on the grass
                Color dark = new Color(Dark.r, Dark.g, Dark.b, 0.55f);
                Show(goal[1], goalAt, er + ow, dark, ow);
                Show(goal[2], goalAt, er, new Color(Gold.r, Gold.g, Gold.b, 1f), gw);
                Show(goal[5], goalAt, er - gw, dark, ow);
                // two gold ripples leaving the edge outward, fading as they go
                for (int i = 0; i < 2; i++)
                {
                    float gk = (now / 1.1f + 0.5f * i) % 1f;
                    Show(goal[3 + i], goalAt, er + 0.75f * gk, new Color(Gold.r, Gold.g, Gold.b, 0.85f * (1f - gk)), (3.5f - 1.5f * gk) * px);
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
            // Bonk's power hit / Bram's Backlash is the next swing: the ring goes gold, thicker and breathing (tester 2026-11-03 via
            // Discord: "a visual indicator showing when his next attack will be empowered"); no new ring on the ground
            bool power = p.NextHitSpecial;
            float pb = power ? 0.5f + 0.5f * Mathf.Sin(Time.time * 6f) : 0f;
            float ew = (2f + 1.5f * hit + (power ? 1.5f + pb : 0f)) * px, dw = 1f * px;
            Color rc = power ? new Color(Gold.r, Gold.g, Gold.b, 0.75f + 0.25f * Mathf.Max(hit, pb)) : new Color(1f, 1f, 1f, 0.55f + 0.4f * hit);
            Show(range[0], at, r + ew / 2f, rc, ew);
            Show(range[1], at, r + ew / 2f + dw, RingEdge, dw);
            Show(range[2], at, r - ew / 2f, RingEdge, dw);

            // the attack charge grows out to the range until the next swing (none while stunned or disarmed)
            bool blocked = p.Has(St.Stun) || p.Has(St.Disarm);
            float k = blocked ? 0f : Charge(p);
            if (k > 0.02f && k < 1f)
            {
                float cw = 1.5f * px;
                Show(charge[0], at, r * k + cw / 2f, new Color(1f, 1f, 1f, 0.15f + 0.3f * k), cw);
            }

            // (the special's own reach, a third white ring while it was ready, is gone: Rickard 2026-10-30 "det är för många cirklar";
            // the drag's aim still shows its reach)

            // a thick gold ring on who the last auto attack picked: held, then fading over the last 0.4 s
            float age = Time.time - autoAt;
            // a tower is held longer (Bonk swings every 2.6 s) and ringed outside its model: at its hit edge (R 0.55) the 1-tile-wide
            // tower body hid the ring (tester 2026-11-03 via Discord: "some kind of visual indicator when you're targeting the enemy tower")
            bool atTower = autoTarget != null && autoTarget.Kind == UnitKind.Tower;
            float hold = atTower ? 3f : 1.1f;
            if (autoTarget != null && autoTarget.Alive && age >= 0f && age < hold && m.IsVisibleTo(autoTarget, p.Team))
            {
                float fa = age < hold - 0.4f ? 1f : 1f - (age - (hold - 0.4f)) / 0.4f, pop = Mathf.Max(0f, 1f - age / 0.15f);
                // the gold band's OUTER edge is the target's hit edge (Unit.R), like ABC's red disc under its target (Rickard's ABC
                // videos 2026-11-02): the range ring meeting the gold ring = in reach (it sat 0.22 tiles outside before)
                float gw = (atTower ? 5f : 3.5f) * px, rr = (atTower ? TowerRingR + 0.06f * Mathf.Sin(Time.time * 5f) : autoTarget.R) + 0.12f * pop; // outer edge; Show draws the band inward from it
                Vector3 tp = views.DrawnAt(autoTarget); tp.y = 0f;
                Show(target[1], tp, rr, new Color(Gold.r, Gold.g, Gold.b, fa), 2f * gw);
                Show(target[0], tp, rr - 2f * gw, new Color(Dark.r, Dark.g, Dark.b, 0.55f * fa), 1.5f * px); // dark edge inside the gold
                // (its thinner pulsing inner ring is gone: two gold rings on one target, Rickard 2026-10-31)
            }

            // (the orange "special ready" ring at the feet is gone: Rickard 2026-10-31, the HP bar already shows a ready special)

            // the dragged special's hit preview: who the rules would hit if let go now (Sim AimTargets: walls, line of sight, only the
            // first one a bolt, hook, needle or boulder meets; Moa's field: the allies it shields, in honey) (AIM-03, AIM-04)
            if (aimOn)
            {
                m.AimTargets(p, aimInfo, aimHits);
                Color hc = aimInfo.Allies ? Honey : HitRed;
                for (int i = 0; i < aimHits.Count && i < hits.Length; i++)
                {
                    var u = aimHits[i];
                    Vector3 up = views.DrawnAt(u); up.y = 0f;
                    Show(hits[i], up, u.R + 0.25f, new Color(hc.r, hc.g, hc.b, 0.9f), 3f * px);
                }
            }
            else if (p.SpecialReady && p.Hero == HeroId.Moa && !m.OneTap) // the one-tap special picks a random ally in her ring (Sim OneTap.TapAlly), not this one (bug review 2026-11-02, nr 5)
            {
                // Moa's tap shields the ally hero (or herself) with the lowest health share in reach: show who that is now (MOA-02)
                var pick = m.MostHurtAlly(p, MoaS.Range);
                // only on an ally: on Moa herself the honey ring is gone (Rickard 2026-10-31, the HP bar shows the shield and the ready special)
                if (pick != null && pick != p)
                {
                    Vector3 up = views.DrawnAt(pick); up.y = 0f;
                    Show(moaPick[0], up, pick.R + 0.22f, new Color(Honey.r, Honey.g, Honey.b, 0.75f), 2.5f * px);
                }
            }

            // Bonk's club lands on a ground circle after its wind-up: show it faintly (the enemy's red, ours white), as the bots
            // already read it (BONK-07)
            {
                int n = 0;
                var ps = m.Projs;
                for (int i = 0; i < ps.Count && n < smash.Length; i++)
                {
                    var q = ps[i];
                    if (q.Done || q.Mode != ProjMode.Smash || q.T >= 0f || q.Src == null || !m.IsVisibleTo(q.Src, p.Team)) continue;
                    bool foe = q.Team != p.Team;
                    float sk = Mathf.Clamp01(1f + q.T / Mathf.Max(0.05f, q.Dur)); // 0 at the swing's start, 1 as it lands
                    Color sc = foe ? HitRed : Color.white;
                    m.ClubSpot(q, out float qx, out float qy); // where it lands, not where the swing began (tester 2026-11-04)
                    Show(smash[n++], Coords.World(qx, qy), q.Radius, new Color(sc.r, sc.g, sc.b, (foe ? 0.35f : 0.22f) + 0.35f * sk), 2f * px);
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
                    // the old game (Hud.cs, Rickard 2026-10-14) also filled the whole reach faint red while it shot at you: the area turns
                    // red, not only its edge (Rickard 2026-10-30 "tornets area blir inte röd")
                    if (onMe) Show(tower[1], tc, tr, new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.13f), tr);
                    Show(tower[0], tc, tr + w / 2f, onMe ? new Color(TowerRed.r, TowerRed.g, TowerRed.b, 0.75f) : new Color(1f, 1f, 1f, 0.25f), w);
                }
            }
        }

        void HideAll()
        {
            Hide(range); Hide(charge); Hide(reach); Hide(target); Hide(tower); Hide(hits); Hide(ready); Hide(blink); Hide(goal); Hide(moaPick); Hide(smash);
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
                inner = Mathf.Round(inner * 2000f) / 2000f; // the vertices move only when it really changes
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

        // The band's topology (normals, palette UV, colours, triangles) never changes, only its inner edge moves, and that every
        // frame for the pulsing range ring, the target pop and the goal ripples: a mesh built once gets new vertex positions only,
        // not Clear plus every array and the bounds each time (full review 2026-11-09 nr 90). The outer edge is always at 1, so the
        // bounds hold; the picture is the same to the vertex.
        static void BuildRing(Mesh mesh, float inner)
        {
            for (int i = 0; i <= RingSeg; i++)
            {
                float a = i * Mathf.PI * 2f / RingSeg, c = Mathf.Cos(a), s = Mathf.Sin(a);
                ringV[2 * i] = new Vector3(c * inner, 0f, s * inner);
                ringV[2 * i + 1] = new Vector3(c, 0f, s);
            }
            if (mesh.vertexCount == ringV.Length) { mesh.vertices = ringV; return; }
            for (int i = 0; i <= RingSeg; i++)
            {
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
