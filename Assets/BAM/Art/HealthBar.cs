using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// World-space health bar: one small dynamic mesh (frame, health, shield, damage trail, level badge with 7-segment
    /// digits), one draw call. Vertices are rewritten only when a value changes or the trail is catching up.
    /// </summary>
    sealed class HealthBar
    {
        const int Quads = 20, QFrame = 0, QHp = 1, QShield = 2, QTrail = 3, QEmpty = 4, QBadge = 5, QDigits = 6;
        const float H = 0.12f, Border = 0.025f, Badge = 0.22f;

        public readonly Transform T;
        readonly MeshRenderer r;
        readonly Mesh mesh;
        readonly Vector3[] v = new Vector3[Quads * 4];
        readonly Vector2[] uv = new Vector2[Quads * 4];
        readonly float w;
        readonly bool showLevel;
        float hp = 1f, shield, trail = 1f, trailWait;
        int level = 1;
        byte fill = Pal.BarAlly, badge = Pal.BlueDark;
        bool dirty = true, uvDirty = true;

        // 7-segment masks for 0..9: bits a b c d e f g
        static readonly byte[] digitMask = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };

        public HealthBar(Transform parent, float width, float height, bool level)
        {
            w = width;
            showLevel = level;
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
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(w + 1f, 1f, 1f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = ArtMaterials.Unlit;
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

        /// <summary>Back to full with no trail (respawn, pooled reuse).</summary>
        public void Reset() { hp = 1f; shield = 0f; trail = 1f; dirty = true; }

        public void SetLevel(int lvl)
        {
            lvl = Mathf.Clamp(lvl, 0, 99);
            if (lvl == level) return;
            level = lvl;
            dirty = true;
        }

        public void SetColors(byte fillColor, byte badgeColor)
        {
            if (fill == fillColor && badge == badgeColor) return;
            fill = fillColor; badge = badgeColor;
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
            if (dirty) { Rebuild(); mesh.vertices = v; dirty = false; }
            if (uvDirty) { RebuildUV(); mesh.uv = uv; uvDirty = false; }
        }

        void Rebuild()
        {
            float x0 = -w * 0.5f, x1 = w * 0.5f, y0 = -H * 0.5f, y1 = H * 0.5f;
            Quad(QFrame, x0 - Border, y0 - Border, x1 + Border, y1 + Border, 0.01f);
            float s0 = Mathf.Min(hp, 1f - shield);           // shield sits after the health, or at the right end when full
            float hpEnd = x0 + w * hp;
            float shA = x0 + w * Mathf.Max(hp, s0), shB = x0 + w * Mathf.Min(1f, Mathf.Max(hp, s0) + shield);
            float hpShown = Mathf.Min(hpEnd, shA);
            Quad(QHp, x0, y0, hpShown, y1, 0f);
            Quad(QShield, shA, y0, shB, y1, 0f);
            float trA = Mathf.Max(shB, hpEnd), trB = Mathf.Max(trA, x0 + w * trail);
            Quad(QTrail, trA, y0, trB, y1, 0f);
            Quad(QEmpty, trB, y0, x1, y1, 0f);
            if (showLevel)
            {
                float bx1 = x0 - Border - 0.01f, bx0 = bx1 - Badge;
                Quad(QBadge, bx0, -Badge * 0.5f, bx1, Badge * 0.5f, 0.005f);
                float cx = (bx0 + bx1) * 0.5f;
                if (level >= 10)
                {
                    Digit(QDigits, level / 10, cx - 0.045f);
                    Digit(QDigits + 7, level % 10, cx + 0.045f);
                }
                else
                {
                    Digit(QDigits, level, cx);
                    Digit(QDigits + 7, -1, cx);
                }
            }
            else
            {
                Quad(QBadge, 0f, 0f, 0f, 0f, 0f);
                Digit(QDigits, -1, 0f);
                Digit(QDigits + 7, -1, 0f);
            }
        }

        void Digit(int q, int d, float cx)
        {
            const float dw = 0.07f, dh = 0.13f, t = 0.022f;
            int mask = d < 0 ? 0 : digitMask[d];
            float l = cx - dw * 0.5f, rr = cx + dw * 0.5f, top = dh * 0.5f, bot = -dh * 0.5f;
            // a b c d e f g
            Seg(q + 0, mask & 0x01, l, top - t, rr, top);
            Seg(q + 1, mask & 0x02, rr - t, 0f, rr, top);
            Seg(q + 2, mask & 0x04, rr - t, bot, rr, 0f);
            Seg(q + 3, mask & 0x08, l, bot, rr, bot + t);
            Seg(q + 4, mask & 0x10, l, bot, l + t, 0f);
            Seg(q + 5, mask & 0x20, l, 0f, l + t, top);
            Seg(q + 6, mask & 0x40, l, -t * 0.5f, rr, t * 0.5f);
        }

        void Seg(int q, int on, float a, float b, float c, float d)
        {
            if (on != 0) Quad(q, a, b, c, d, -0.005f);
            else Quad(q, 0f, 0f, 0f, 0f, 0f);
        }

        // Faces -z: the bar's transform copies the camera rotation, so -z looks at the camera.
        void Quad(int q, float xa, float ya, float xb, float yb, float z)
        {
            if (xb < xa) xb = xa;
            int i = q * 4;
            v[i] = new Vector3(xa, ya, z);
            v[i + 1] = new Vector3(xa, yb, z);
            v[i + 2] = new Vector3(xb, yb, z);
            v[i + 3] = new Vector3(xb, ya, z);
        }

        void RebuildUV()
        {
            Col(QFrame, Pal.BarFrame);
            Col(QHp, fill);
            Col(QShield, Pal.BarShield);
            Col(QTrail, Pal.BarTrail);
            Col(QEmpty, Pal.BarBack);
            Col(QBadge, badge);
            for (int i = 0; i < 14; i++) Col(QDigits + i, Pal.White);
        }

        void Col(int q, byte c)
        {
            Vector2 p = Pal.UV(c);
            int i = q * 4;
            uv[i] = p; uv[i + 1] = p; uv[i + 2] = p; uv[i + 3] = p;
        }
    }
}
