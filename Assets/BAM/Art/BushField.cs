using System.Collections.Generic;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// The old game's living bushes (View3D bushes + SyncBushBend): every bush tile within 1.6 tiles of the player's hero turns
    /// 40% see-through, and every tile leans away from the closest hero within 1 tile of its middle (up to 16 degrees) and springs
    /// back when they leave. Only the picture: who is hidden in the grass is still the game layer's call (SetVisibility).
    /// Reads the heroes' positions from their views, so the game layer needs to tell it nothing.
    /// </summary>
    sealed class BushField : MonoBehaviour
    {
        sealed class Tile { public Renderer r; public Transform t; public float x, z; public Vector2 lean; public bool faded; }

        readonly List<Tile> tiles = new List<Tile>(128);
        Material mat, faded;
        float pollenT;
        uint seed = 12345u;

        internal void Init(Transform holder, Material normal, Material see)
        {
            mat = normal; faded = see;
            for (int i = 0; i < holder.childCount; i++)
            {
                var c = holder.GetChild(i);
                var p = c.localPosition;
                tiles.Add(new Tile { r = c.GetComponent<Renderer>(), t = c, x = p.x, z = p.z });
            }
        }

        void LateUpdate()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime);
            var heroes = BodyView.Heroes;
            // the player's hero, alive (the old game also faded round the teammate the camera follows while the player is dead)
            bool hasP = false; float px = 0f, pz = 0f;
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i];
                if (!h.IsPlayer || h.IsDead) continue;
                var hp = transform.InverseTransformPoint(h.transform.position);
                hasP = true; px = hp.x; pz = hp.z;
                break;
            }
            Pollen(dt, hasP, px, pz);
            for (int k = 0; k < tiles.Count; k++)
            {
                var b = tiles[k];
                bool near = hasP && (px - b.x) * (px - b.x) + (pz - b.z) * (pz - b.z) < 1.6f * 1.6f;
                if (near != b.faded) { b.faded = near; b.r.sharedMaterial = near ? faded : mat; }

                Vector2 want = Vector2.zero; float best = 0f;
                for (int i = 0; i < heroes.Count; i++)
                {
                    var h = heroes[i];
                    if (!h.Shown) continue; // a hidden enemy must not bend the bush it hides in
                    var hp = transform.InverseTransformPoint(h.transform.position);
                    float dx = b.x - hp.x, dz = b.z - hp.z, d2 = dx * dx + dz * dz;
                    if (d2 >= 1f) continue;
                    float d = Mathf.Sqrt(d2), w = 1f - d;
                    if (w > best) { best = w; want = d < 0.01f ? new Vector2(0f, w) : new Vector2(dx, dz) * (w / d); }
                }
                if (best <= 0f && b.lean == Vector2.zero) continue;
                b.lean = Vector2.Lerp(b.lean, want, Mathf.Clamp01(dt * (best > 0f ? 10f : 4f)));
                float m = b.lean.magnitude;
                if (m < 0.004f) { b.lean = Vector2.zero; b.t.localRotation = Quaternion.identity; continue; }
                // topple toward +lean, about the tile's middle on the ground (the tile's own origin)
                var axis = new Vector3(b.lean.y, 0f, -b.lean.x) / m; // up x lean
                b.t.localRotation = Quaternion.AngleAxis(16f * m, axis);
            }
        }

        // the old game's floating pollen over the arena (View3DWorld.SyncAmbience): a few pale specks drift up round the player
        // (or the bushes' middle while there is none), so the arena never looks frozen
        void Pollen(float dt, bool hasP, float px, float pz)
        {
            pollenT -= dt;
            if (pollenT > 0f || tiles.Count == 0) return;
            pollenT = ArtKit.High ? 0.22f : 0.5f;
            if (!hasP) { var c = tiles[tiles.Count / 2]; px = c.x; pz = c.z; }
            var p = transform.TransformPoint(new Vector3(px + (Rnd() - 0.5f) * 12f, 0.3f + Rnd() * 1.2f, pz + (Rnd() - 0.5f) * 14f));
            bool petal = Rnd() < 0.3f;
            var col = petal ? new Color32(255, 214, 230, 200) : new Color32(255, 246, 190, 210);
            Fx.Glow(p, 1, col, petal ? 0.1f : 0.07f, 0.12f, 3.2f, 0.18f, 0.2f);
        }

        float Rnd() { seed = seed * 1664525u + 1013904223u; return (seed >> 8) * (1f / 16777216f); }
    }
}
