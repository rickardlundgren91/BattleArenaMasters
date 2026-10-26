using BAM.Art;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Game
{
    /// <summary>
    /// The special's aim on the ground while it is dragged, in the MLBB skill-indicator style (Rickard 2026-10-26: Tyra's aim was a
    /// flat "Paint" square): a soft range circle that brightens towards a glowing rim, a translucent arrow whose edges glow, with a
    /// fill that sweeps from the hero to the tip again and again, and for area specials a soft target circle with a pulse.
    /// Cancelling dims it all to a quiet grey (old HudAim AIM_CANCEL); a special that is not ready shows red.
    /// The gradients are small textures made once; the material is a copy of the art's own aim material, so its transparent
    /// shader variant is the one already in the build.
    /// </summary>
    sealed class AimIndicator
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap");
        static Texture2D stripTex, headTex, discTex;
        static Mesh quadZ, quadC;

        readonly GameObject root;
        readonly Part range, target, pulse, body, sweep, head;
        float sweepT;

        static readonly Color Ok = new Color(0.85f, 0.95f, 1f, 1f), Bad = new Color(1f, 0.4f, 0.4f, 1f), Grey = new Color(0.78f, 0.78f, 0.82f, 0.55f);

        sealed class Part
        {
            public Transform T;
            public MeshRenderer R;
            public Material M;
            public Color Shown = new Color(-1f, 0f, 0f, 0f);

            public void SetColor(Color c)
            {
                if (c == Shown) return;
                Shown = c;
                M.SetColor(BaseColor, c);
            }
        }

        public AimIndicator(AimView source)
        {
            MakeAssets();
            Material src = null;
            var rs = source.GetComponentsInChildren<MeshRenderer>(true);
            if (rs.Length > 0) src = rs[0].sharedMaterial;
            root = new GameObject("BAM.AimIndicator");
            range = MakePart("Range", quadC, discTex, src, 3004);
            target = MakePart("Target", quadC, discTex, src, 3008);
            pulse = MakePart("Pulse", quadC, discTex, src, 3009);
            body = MakePart("Body", quadZ, stripTex, src, 3008);
            sweep = MakePart("Sweep", quadZ, stripTex, src, 3009);
            head = MakePart("Head", quadZ, headTex, src, 3010);
            Hide();
        }

        Part MakePart(string name, Mesh mesh, Texture2D tex, Material src, int queue)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = src != null ? new Material(src) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetTexture(BaseMap, tex);
            mat.mainTexture = tex;
            mat.renderQueue = queue;
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.enabled = false;
            return new Part { T = go.transform, R = r, M = mat };
        }

        public void Destroy()
        {
            if (root == null) return;
            foreach (var p in new[] { range, target, pulse, body, sweep, head }) Object.Destroy(p.M);
            Object.Destroy(root);
        }

        public void Hide()
        {
            range.R.enabled = target.R.enabled = pulse.R.enabled = body.R.enabled = sweep.R.enabled = head.R.enabled = false;
        }

        static Color Tint(bool ready, bool cancel) => cancel ? Grey : ready ? Ok : Bad;

        /// <summary>The reach of the special: a soft circle round the hero.</summary>
        public void Range(Vector3 hero, float reach, bool ready, bool cancel)
        {
            Place(range, hero, Quaternion.identity, new Vector3(reach * 2f, 1f, reach * 2f), 0.04f);
            Color c = Tint(ready, cancel);
            range.SetColor(new Color(c.r, c.g, c.b, cancel ? 0.35f : 0.75f));
        }

        /// <summary>An arrow from the hero, length long and width wide (the hit width), with its glowing edges and the sweep.</summary>
        public void Line(Vector3 from, Vector3 dir, float length, float width, bool ready, bool cancel, float dt)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector3.forward;
            dir.Normalize();
            width = Mathf.Max(0.3f, width);
            float headLen = Mathf.Min(length * 0.3f, Mathf.Max(0.45f, width * 0.9f));
            float bodyLen = Mathf.Max(0.05f, length - headLen);
            var q = Quaternion.LookRotation(dir);
            Color c = Tint(ready, cancel);

            Place(body, from, q, new Vector3(width, 1f, bodyLen), 0.06f);
            body.SetColor(new Color(c.r, c.g, c.b, cancel ? 0.5f : 0.95f));
            Place(head, from + dir * bodyLen, q, new Vector3(width * 1.8f, 1f, headLen), 0.061f);
            head.SetColor(new Color(c.r, c.g, c.b, cancel ? 0.5f : 1f));

            // the fill runs from the hero to the tip every 0.7 s and fades as it gets there
            sweepT = cancel ? 0f : (sweepT + dt / 0.7f) % 1f;
            float k = 1f - (1f - sweepT) * (1f - sweepT);
            Place(sweep, from, q, new Vector3(width * 0.92f, 1f, Mathf.Max(0.02f, bodyLen * k)), 0.062f);
            sweep.SetColor(new Color(c.r, c.g, c.b, cancel ? 0f : 0.55f * (1f - sweepT * 0.7f)));
            sweep.R.enabled = !cancel;
        }

        /// <summary>The spot an area special lands on: a soft circle with a ring that pulses inside it.</summary>
        public void Circle(Vector3 center, float radius, bool ready, bool cancel, float dt)
        {
            Color c = Tint(ready, cancel);
            Place(target, center, Quaternion.identity, new Vector3(radius * 2f, 1f, radius * 2f), 0.06f);
            target.SetColor(new Color(c.r, c.g, c.b, cancel ? 0.5f : 1f));
            sweepT = cancel ? 0f : (sweepT + dt / 0.8f) % 1f;
            float pr = radius * (0.25f + 0.75f * sweepT);
            Place(pulse, center, Quaternion.identity, new Vector3(pr * 2f, 1f, pr * 2f), 0.062f);
            pulse.SetColor(new Color(c.r, c.g, c.b, cancel ? 0f : 0.6f * (1f - sweepT)));
            pulse.R.enabled = !cancel;
        }

        static void Place(Part p, Vector3 at, Quaternion q, Vector3 scale, float y)
        {
            p.T.SetPositionAndRotation(new Vector3(at.x, y, at.z), q);
            p.T.localScale = scale;
            p.R.enabled = true;
        }

        // ---------------------------------------------------------------- the textures and meshes, made once

        static void MakeAssets()
        {
            if (stripTex != null) return;
            // the arrow's body: across, a faint fill that grows towards the sides and a bright glowing edge; along, it fades in
            // from the hero so it seems to come out of the feet
            stripTex = NewTex("BAM.Aim.Strip", 64, 64);
            var px = new Color32[64 * 64];
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / 32f - 1f), v = (y + 0.5f) / 64f;
                    float a = Mathf.Max(0.16f + 0.2f * u * u, Edge(u, 0.86f, 0.97f));
                    a *= Mathf.SmoothStep(0f, 1f, v / 0.12f);
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            stripTex.SetPixels32(px); stripTex.Apply(false, true);

            // the arrow head: a triangle (base at v = 0, tip at v = 1) with a faint fill and glowing edges
            headTex = NewTex("BAM.Aim.Head", 64, 64);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float u = (x + 0.5f) / 64f, v = (y + 0.5f) / 64f;
                    float half = 0.5f * (1f - v), du = Mathf.Abs(u - 0.5f);
                    float a = 0f;
                    if (du <= half && v < 0.995f)
                    {
                        // distance to the nearest side, in texture units, scaled so the glow looks even
                        float side = (half - du) * 0.9f, dEdge = side; // the sides only: no bright bar where it meets the body
                        a = Mathf.Max(0.3f, 1f - Mathf.SmoothStep(0f, 0.07f, dEdge) * 0.7f);
                        a *= Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, side / 0.012f));
                    }
                    px[y * 64 + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            headTex.SetPixels32(px); headTex.Apply(false, true);

            // a soft disc: nearly clear in the middle, a fill that brightens towards the rim, then a crisp glowing rim
            const int n = 128;
            discTex = NewTex("BAM.Aim.Disc", n, n);
            var dp = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / (n * 0.5f) - 1f, dy = (y + 0.5f) / (n * 0.5f) - 1f, d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = d > 1f ? 0f : Mathf.Max(0.04f + 0.24f * d * d * d, Edge(d, 0.9f, 0.975f));
                    dp[y * n + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Clamp01(a)));
                }
            discTex.SetPixels32(dp); discTex.Apply(false, true);

            quadZ = Quad("BAM.Aim.QuadZ", -0.5f, 0.5f, 0f, 1f);
            quadC = Quad("BAM.Aim.QuadC", -0.5f, 0.5f, -0.5f, 0.5f);
        }

        // a bright band that peaks at mid and falls off softly inside (the glow) and sharply outside
        static float Edge(float t, float from, float mid)
        {
            if (t < from) return 0.35f * Mathf.SmoothStep(0f, 1f, (t - (from - 0.14f)) / 0.14f); // the soft inner glow
            if (t <= mid) return Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, (t - from) / (mid - from)));
            return Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, (t - mid) / (1f - mid + 1e-4f)));
        }

        static Texture2D NewTex(string name, int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };

        static Mesh Quad(string name, float x0, float x1, float z0, float z1)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            m.vertices = new[] { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) };
            m.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 }; // clockwise seen from above
            m.RecalculateBounds();
            return m;
        }
    }
}
