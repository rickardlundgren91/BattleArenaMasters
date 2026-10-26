using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>Status markers on one unit. Each marker is a child renderer made the first time it is needed and then only toggled.</summary>
    sealed class StatusFx
    {
        const int Stun = 0, Slow = 1, Root = 2, Burn = 3, Poison = 4, Shield = 5, Haste = 6, Illusion = 7, Count = 8;

        readonly Transform parent;
        readonly float radius, height;
        readonly Transform[] t = new Transform[Count];
        readonly MeshRenderer[] r = new MeshRenderer[Count];
        StatusFlags flags;
        bool visible = true;
        float emitTimer;

        public StatusFx(Transform parent, float radius, float height)
        {
            this.parent = parent;
            this.radius = radius;
            this.height = height;
        }

        public bool Visible
        {
            set
            {
                if (visible == value) return;
                visible = value;
                Refresh();
            }
        }

        public void Set(StatusFlags f)
        {
            if (f == flags) return;
            flags = f;
            Refresh();
        }

        static bool Has(StatusFlags f, StatusFlags bit) => (f & bit) != 0;

        void Refresh()
        {
            Show(Stun, Has(flags, StatusFlags.Stunned));
            Show(Slow, Has(flags, StatusFlags.Slowed));
            Show(Root, Has(flags, StatusFlags.Rooted));
            Show(Burn, Has(flags, StatusFlags.Burning));
            Show(Poison, Has(flags, StatusFlags.Poisoned));
            Show(Shield, Has(flags, StatusFlags.Shielded));
            Show(Haste, Has(flags, StatusFlags.Hasted));
            Show(Illusion, Has(flags, StatusFlags.Illusion));
        }

        void Show(int i, bool on)
        {
            on &= visible;
            if (r[i] == null)
            {
                if (!on) return;
                Make(i);
            }
            if (r[i].enabled != on) r[i].enabled = on;
        }

        void Make(int i)
        {
            Mesh mesh; Material mat; Vector3 pos; float s;
            float rr = Mathf.Max(radius, 0.25f) * 1.3f;
            switch (i)
            {
                case Stun: mesh = Shapes.StunStars(); mat = ArtMaterials.Unlit; pos = new Vector3(0f, height + 0.12f, 0f); s = Mathf.Max(1f, radius * 2.5f); break;
                case Slow: mesh = Shapes.SlowRing(); mat = ArtMaterials.UnlitFade; pos = new Vector3(0f, 0.03f, 0f); s = rr; break;
                case Root: mesh = Shapes.RootVines(); mat = ArtMaterials.Lit; pos = Vector3.zero; s = rr; break;
                case Burn: mesh = Shapes.Flames(); mat = ArtMaterials.UnlitAdd; pos = Vector3.zero; s = rr * 0.8f; break;
                case Poison: mesh = Shapes.PoisonBubbles(); mat = ArtMaterials.Unlit; pos = new Vector3(0.18f, height + 0.05f, 0f); s = 1f; break;
                case Shield: mesh = Shapes.ShieldBubble(); mat = ArtMaterials.UnlitFade; pos = new Vector3(0f, height * 0.5f, 0f); s = Mathf.Max(radius * 1.6f, height * 0.62f); break;
                case Haste: mesh = Shapes.HasteLines(); mat = ArtMaterials.UnlitAdd; pos = Vector3.zero; s = Mathf.Max(1f, radius * 2.2f); break;
                default: mesh = Shapes.RingMesh(0.85f, Pal.Violet); mat = ArtMaterials.UnlitAdd; pos = new Vector3(0f, 0.04f, 0f); s = rr * 1.15f; break;
            }
            var go = new GameObject("Status" + i);
            t[i] = go.transform;
            t[i].SetParent(parent, false);
            t[i].localPosition = pos;
            t[i].localScale = new Vector3(s, s, s);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            r[i] = go.AddComponent<MeshRenderer>();
            r[i].sharedMaterial = mat;
            r[i].shadowCastingMode = ShadowCastingMode.Off;
            r[i].receiveShadows = false;
        }

        public void Tick(float dt, float time)
        {
            if (flags == StatusFlags.None || !visible) return;
            if (r[Stun] != null && r[Stun].enabled) t[Stun].localRotation = Quaternion.Euler(0f, time * 240f, 0f);
            if (r[Burn] != null && r[Burn].enabled)
            {
                float f = 0.85f + Mathf.Sin(time * 23f) * 0.1f + Mathf.Sin(time * 37f) * 0.06f;
                Vector3 s = t[Burn].localScale;
                t[Burn].localScale = new Vector3(s.x, s.x * f, s.x);
                t[Burn].localRotation = Quaternion.Euler(0f, time * 90f, 0f);
            }
            if (r[Poison] != null && r[Poison].enabled) t[Poison].localPosition = new Vector3(0.18f, height + 0.05f + Mathf.Sin(time * 4f) * 0.05f, 0f);
            if (r[Shield] != null && r[Shield].enabled)
            {
                float k = 1f + Mathf.Sin(time * 5f) * 0.03f;
                float b = Mathf.Max(radius * 1.6f, height * 0.62f) * k;
                t[Shield].localScale = new Vector3(b, b, b);
            }
            if (r[Illusion] != null && r[Illusion].enabled) t[Illusion].localRotation = Quaternion.Euler(0f, time * 60f, 0f);
            if (r[Root] != null && r[Root].enabled) t[Root].localRotation = Quaternion.Euler(0f, Mathf.Sin(time * 3f) * 6f, 0f);

            // a few world particles for the statuses that should feel alive
            emitTimer -= dt;
            if (emitTimer > 0f) return;
            emitTimer = Fx.Amount >= 1f ? 0.12f : 0.25f;
            Vector3 p = parent.position;
            if (Has(flags, StatusFlags.Burning)) Fx.Flames(p + new Vector3(0f, height * 0.4f, 0f), 2, Pal.Get(Pal.Fire), 0.3f, 0.3f, 0.5f, 1.2f, radius * 0.6f);
            if (Has(flags, StatusFlags.Poisoned)) Fx.Bubbles(p + new Vector3(0f, height * 0.6f, 0f), 1, Pal.Get(Pal.Poison), 0.14f, 0.2f, 0.6f, 0.6f, radius * 0.5f);
            if (Has(flags, StatusFlags.Hasted)) Fx.Glow(p + new Vector3(0f, 0.2f, 0f), 1, Pal.Get(Pal.White), 0.16f, 0.1f, 0.35f, 0.2f, radius * 0.6f);
            if (Has(flags, StatusFlags.Slowed)) Fx.Glow(p + new Vector3(0f, 0.3f, 0f), 1, Pal.Get(Pal.Slow), 0.12f, 0.2f, 0.5f, -0.2f, radius * 0.8f);
        }
    }
}
