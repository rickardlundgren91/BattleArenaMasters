using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// World-space health bar in the MLBB style (Rickard 2026-10-30 "gör healthbars mer som i mlbb"): a thin dark frame, the fill
    /// shaded with a light top edge, a dark notch every 100 HP (a wider one every 500) on hero bars, the white damage trail, the
    /// shield after the health, and under a hero's bar a thin orange row for the special's charge (where MLBB has mana; it blinks when ready).
    /// One small dynamic mesh, one draw call, drawn over everything (ArtMaterials.Overlay), so a body or a raised weapon never
    /// cuts through it. The quads are written back to front: with no depth test the triangle order is the layering.
    /// Vertices are rewritten only when a value changes or the trail is catching up.
    /// When the special is ready the row turns bright gold and a narrow white shine sweeps along it every SweepEvery seconds
    /// (tester 2026-11-02 "the small yellow bar could glow when a hero's ultimate is ready"; Rickard found a soft halo ugly, "glöden
    /// e ful", so it is a crisp sweep in the bar's own flat style). Shown on every hero, enemies too, so a ready enemy special can be seen.
    /// </summary>
    sealed class HealthBar
    {
        const int QFrame = 0, QEnEdge = 1, QBack = 2, QTrail = 3, QHpLo = 4, QHpMid = 5, QHpHi = 6, QShLo = 7, QShMid = 8, QShHi = 9, QShEdge = 10, QEnBack = 11, QEn = 12, QShineWide = 13, QShine = 14, QTicks = 15,
            MaxTicks = 24, Quads = QTicks + MaxTicks;
        const float Border = 0.035f, EnergyH = 0.045f, EnergyGap = 0.015f, TickStep = 100f;
        // the ready sweep: SweepTime to cross the row, then a rest; the shine is a white core inside a wider pale gold band
        const float SweepTime = 0.45f, SweepEvery = 1.6f, ShineWide = 0.16f, ShineCore = 0.05f;
        float shineAt = -1f; // where the shine's centre is along the row, 0..1; below 0 = none
        // and a gold edge round the row that blinks between dark and light gold (Rickard 2026-11-02 picked "D": the sweep plus the
        // blinking gold edge); it lies over the black frame round the row only
        const float EdgeRate = 7f;
        bool edgeLit;

        public readonly Transform T;
        readonly MeshRenderer r;
        readonly Mesh mesh;
        readonly Vector3[] v = new Vector3[Quads * 4];
        readonly Vector2[] uv = new Vector2[Quads * 4];
        readonly float w, h;
        float hp = 1f, shield, trail = 1f, trailWait, maxHp, energy = -1f;
        bool ready;
        byte fill = Pal.BarAlly;
        bool dirty = true, uvDirty = true;

        /// <param name="thick">Bar height in tiles: heroes 0.13, minions thinner.</param>
        public HealthBar(Transform parent, float width, float height, float thick)
        {
            w = width;
            h = thick;
            var go = new GameObject("Bar");
            T = go.transform;
            T.SetParent(parent, false);
            T.localPosition = new Vector3(0f, height, 0f);
            mesh = new Mesh { name = "Bar", hideFlags = HideFlags.DontSave };
            mesh.MarkDynamic();
            var tri = new int[Quads * 6];
            for (int q = 0; q < Quads; q++)
            {
                int i = q * 4, t = q * 6;
                tri[t] = i; tri[t + 1] = i + 1; tri[t + 2] = i + 2; tri[t + 3] = i; tri[t + 4] = i + 2; tri[t + 5] = i + 3;
            }
            Rebuild();
            RebuildUV();
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tri;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(w + 1f, 1f, 1f)); // MatchViews.BarOf reads the width back as size.x - 1
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ArtMaterials.Overlay;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        public bool Visible { set { if (r.enabled != value) r.enabled = value; } }

        public void Set(float hp01, float shield01)
        {
            hp01 = Mathf.Clamp01(hp01);
            shield01 = Mathf.Clamp01(shield01);
            if (hp01 == hp && shield01 == shield) return;
            if (hp01 < hp) { trail = Mathf.Max(trail, hp); trailWait = 0.35f; }
            else trail = hp01;
            hp = hp01;
            shield = shield01;
            dirty = true;
        }

        /// <summary>Max health, for the notches every 100 HP; 0 = no notches (minions, towers).</summary>
        public void SetMaxHp(float max)
        {
            if (Mathf.Abs(max - maxHp) < 0.5f) return;
            maxHp = max;
            dirty = true;
        }

        /// <summary>The special's charge row under the bar: 0..1, below 0 = no row (not a hero).</summary>
        public void SetEnergy(float k01, bool isReady)
        {
            k01 = k01 < 0f ? -1f : Mathf.Clamp01(k01);
            if (Mathf.Abs(k01 - energy) > 0.001f) { energy = k01; dirty = true; }
            if (ready != isReady) { ready = isReady; uvDirty = true; dirty = true; }
        }

        /// <summary>Back to full with no trail (respawn, pooled reuse).</summary>
        public void Reset() { hp = 1f; shield = 0f; trail = 1f; dirty = true; }

        public void SetColors(byte fillColor)
        {
            if (fill == fillColor) return;
            fill = fillColor;
            uvDirty = true;
        }

        public void Tick(float dt, Quaternion camRot)
        {
            T.rotation = camRot;
            if (trail > hp)
            {
                if (trailWait > 0f) trailWait -= dt;
                else { trail = Mathf.Max(hp, trail - dt * 0.9f); dirty = true; }
            }
            float at = -1f;
            if (ready && energy >= 0f)
            {
                float ph = Time.unscaledTime % SweepEvery;
                if (ph < SweepTime) at = Mathf.Lerp(-ShineWide, 1f + ShineWide, ph / SweepTime);
            }
            if (at != shineAt) { shineAt = at; dirty = true; }
            if (ready && energy >= 0f)
            {
                bool lit = Mathf.Sin(Time.unscaledTime * EdgeRate) > -0.2f;
                if (lit != edgeLit) { edgeLit = lit; uvDirty = true; }
            }
            if (dirty) { Rebuild(); mesh.vertices = v; dirty = false; }
            if (uvDirty) { RebuildUV(); mesh.uv = uv; uvDirty = false; }
        }

        void Rebuild()
        {
            float x0 = -w * 0.5f, x1 = w * 0.5f, y1 = h * 0.5f, y0 = -h * 0.5f;
            bool en = energy >= 0f;
            float ey1 = y0 - EnergyGap, ey0 = ey1 - EnergyH;
            Quad(QFrame, x0 - Border, (en ? ey0 : y0) - Border, x1 + Border, y1 + Border);
            if (en && ready) Quad(QEnEdge, x0 - Border, ey0 - Border, x1 + Border, y0); // the frame's part round the row, up to the health
            else Quad(QEnEdge, 0f, 0f, 0f, 0f);
            Quad(QBack, x0, y0, x1, y1);
            float s0 = Mathf.Min(hp, 1f - shield);           // shield sits after the health, or at the right end when full
            float hpEnd = x0 + w * hp;
            float shA = x0 + w * s0, shB = x0 + w * Mathf.Min(1f, s0 + shield); // health is cut where the shield starts
            float hpShown = Mathf.Min(hpEnd, shA);
            float trA = Mathf.Max(shB, hpEnd), trB = Mathf.Max(trA, x0 + w * trail);
            Quad(QTrail, trA, y0, trB, y1);
            Quad(QHpLo, x0, y0, hpShown, y1);
            Quad(QHpMid, x0, y0 + h * 0.45f, hpShown, y1);
            Quad(QHpHi, x0, y1 - h * 0.18f, hpShown, y1);
            Quad(QShLo, shA, y0, shB, y1);                   // the shield is shaded like the health: gold with a light top edge (Rickard 2026-10-31)
            Quad(QShMid, shA, y0 + h * 0.45f, shB, y1);
            Quad(QShHi, shA, y1 - h * 0.18f, shB, y1);
            if (shB > shA) Quad(QShEdge, shA - 0.007f, y0, shA + 0.007f, y1); // a dark line where the shield starts, so it reads as its own block
            else Quad(QShEdge, 0f, 0f, 0f, 0f);
            if (en)
            {
                Quad(QEnBack, x0, ey0, x1, ey1);
                Quad(QEn, x0, ey0, x0 + w * energy, ey1);
            }
            else { Quad(QEnBack, 0f, 0f, 0f, 0f); Quad(QEn, 0f, 0f, 0f, 0f); }
            if (en && shineAt >= -ShineWide)
            {
                float c = x0 + w * shineAt;
                Quad(QShineWide, Mathf.Clamp(c - w * ShineWide * 0.5f, x0, x1), ey0, Mathf.Clamp(c + w * ShineWide * 0.5f, x0, x1), ey1);
                Quad(QShine, Mathf.Clamp(c - w * ShineCore * 0.5f, x0, x1), ey0, Mathf.Clamp(c + w * ShineCore * 0.5f, x0, x1), ey1);
            }
            else { Quad(QShineWide, 0f, 0f, 0f, 0f); Quad(QShine, 0f, 0f, 0f, 0f); }
            // a notch every 100 HP, every 500 a wider one; a very big bar notches every 200, 300... so it never runs out of quads
            float step = TickStep;
            if (maxHp > 0f) while (maxHp / step > MaxTicks + 1) step += TickStep;
            for (int i = 0; i < MaxTicks; i++)
            {
                float at = (i + 1) * step;
                if (maxHp <= 0f || at >= maxHp - 1f) { Quad(QTicks + i, 0f, 0f, 0f, 0f); continue; }
                bool big = Mathf.RoundToInt(at) % 500 == 0;
                float x = x0 + w * at / maxHp, tw = big ? 0.026f : 0.014f;
                Quad(QTicks + i, x - tw * 0.5f, y0, x + tw * 0.5f, y1);
            }
        }

        // Faces -z: the bar's transform copies the camera rotation, so -z looks at the camera.
        void Quad(int q, float xa, float ya, float xb, float yb)
        {
            if (xb < xa) xb = xa;
            int i = q * 4;
            v[i] = new Vector3(xa, ya, 0f);
            v[i + 1] = new Vector3(xa, yb, 0f);
            v[i + 2] = new Vector3(xb, yb, 0f);
            v[i + 3] = new Vector3(xb, ya, 0f);
        }

        void RebuildUV()
        {
            Col(QFrame, Pal.BarFrame);
            Col(QEnEdge, edgeLit ? Pal.BarEnergyReady : Pal.BarShieldLo);
            Col(QBack, Pal.BarBack);
            Col(QTrail, Pal.BarTrail);
            Col(QHpLo, Pal.BarLo(fill));
            Col(QHpMid, fill);
            Col(QHpHi, Pal.BarHi(fill));
            Col(QShLo, Pal.BarShieldLo);
            Col(QShMid, Pal.BarShield);
            Col(QShHi, Pal.BarShieldHi);
            Col(QShEdge, Pal.BarFrame);
            Col(QEnBack, Pal.BarBack);
            Col(QEn, ready ? Pal.Gold : Pal.BarEnergy);
            Col(QShineWide, Pal.BarEnergyReady);
            Col(QShine, Pal.White);
            for (int i = 0; i < MaxTicks; i++) Col(QTicks + i, Pal.BarFrame);
        }

        void Col(int q, byte c)
        {
            Vector2 p = Pal.UV(c);
            int i = q * 4;
            uv[i] = p; uv[i + 1] = p; uv[i + 2] = p; uv[i + 3] = p;
        }
    }
}
