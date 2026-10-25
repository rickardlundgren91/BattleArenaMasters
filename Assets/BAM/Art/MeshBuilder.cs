using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// Builds low-poly meshes from simple shapes. Every shape is placed through the current matrix (<see cref="M"/>),
    /// coloured by a palette index and tied to one bone (rigid skinning), so a whole character is one mesh and one draw call.
    /// Only used while building (boot, match start), never per frame.
    /// </summary>
    sealed class MeshBuilder
    {
        readonly List<Vector3> v = new List<Vector3>(1024);
        readonly List<Vector3> n = new List<Vector3>(1024);
        readonly List<Vector2> uv = new List<Vector2>(1024);
        readonly List<int> bones = new List<int>(1024);
        readonly List<int> tri = new List<int>(2048);
        public Matrix4x4 M = Matrix4x4.identity;
        public int Bone;
        Vector2 col;

        public int VertexCount => v.Count;
        public void Clear() { v.Clear(); n.Clear(); uv.Clear(); bones.Clear(); tri.Clear(); M = Matrix4x4.identity; Bone = 0; }

        public MeshBuilder Color(byte c) { col = Pal.UV(c); return this; }
        public MeshBuilder At(Vector3 pos) { M = Matrix4x4.Translate(pos); return this; }
        public MeshBuilder At(Vector3 pos, Vector3 euler) { M = Matrix4x4.TRS(pos, Quaternion.Euler(euler), Vector3.one); return this; }
        public MeshBuilder Reset() { M = Matrix4x4.identity; return this; }

        int Vert(Vector3 p, Vector3 normal)
        {
            v.Add(M.MultiplyPoint3x4(p));
            n.Add(M.MultiplyVector(normal).normalized);
            uv.Add(col);
            bones.Add(Bone);
            return v.Count - 1;
        }

        void Tri(int a, int b, int c) { tri.Add(a); tri.Add(b); tri.Add(c); }
        void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

        /// <summary>Flat-shaded quad, corners clockwise seen from the front (Unity's winding).</summary>
        public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = Vert(a, nn); Vert(b, nn); Vert(c, nn); Vert(d, nn);
            Quad(i, i + 1, i + 2, i + 3);
        }

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 nn = Vector3.Cross(b - a, c - a).normalized;
            int i = Vert(a, nn); Vert(b, nn); Vert(c, nn);
            Tri(i, i + 1, i + 2);
        }

        /// <summary>Axis-aligned box (in the current matrix) around center. topColor &lt; 0 = same colour on all faces.</summary>
        public void Box(Vector3 c, Vector3 size, int topColor = -1)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = c + new Vector3(-h.x, -h.y, -h.z), p100 = c + new Vector3(h.x, -h.y, -h.z), p010 = c + new Vector3(-h.x, h.y, -h.z), p110 = c + new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = c + new Vector3(-h.x, -h.y, h.z), p101 = c + new Vector3(h.x, -h.y, h.z), p011 = c + new Vector3(-h.x, h.y, h.z), p111 = c + new Vector3(h.x, h.y, h.z);
            Vector2 keep = col;
            Face(p000, p010, p110, p100); // -z
            Face(p101, p111, p011, p001); // +z
            Face(p001, p011, p010, p000); // -x
            Face(p100, p110, p111, p101); // +x
            Face(p001, p000, p100, p101); // -y
            if (topColor >= 0) col = Pal.UV(topColor);
            Face(p010, p011, p111, p110); // +y
            col = keep;
        }

        /// <summary>Cylinder or cone frustum standing on its base centre, along local +y. Smooth sides, flat caps.</summary>
        public void Cylinder(Vector3 baseCenter, float rBottom, float rTop, float height, int seg, bool caps = true, int capColor = -1)
        {
            int start = v.Count;
            float slope = (rBottom - rTop) / Mathf.Max(height, 1e-4f);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                Vector3 nn = new Vector3(cs, slope, sn).normalized;
                Vert(baseCenter + new Vector3(cs * rBottom, 0f, sn * rBottom), nn);
                Vert(baseCenter + new Vector3(cs * rTop, height, sn * rTop), nn);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                Quad(a, a + 1, a + 3, a + 2);
            }
            if (!caps) return;
            Vector2 keep = col;
            if (capColor >= 0) col = Pal.UV(capColor);
            if (rTop > 1e-4f) Disc(baseCenter + Vector3.up * height, rTop, seg, true);
            col = keep;
            if (rBottom > 1e-4f) Disc(baseCenter, rBottom, seg, false);
        }

        /// <summary>Flat disc in the local xz plane, facing +y (up = true) or -y.</summary>
        public void Disc(Vector3 c, float r, int seg, bool up = true)
        {
            Vector3 nn = up ? Vector3.up : Vector3.down;
            int ci = Vert(c, nn);
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vert(c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r), nn);
            }
            for (int i = 0; i < seg; i++)
            {
                if (up) Tri(ci, ci + 2 + i, ci + 1 + i);
                else Tri(ci, ci + 1 + i, ci + 2 + i);
            }
        }

        /// <summary>Flat ring in the local xz plane facing up.</summary>
        public void Ring(Vector3 c, float rIn, float rOut, int seg)
        {
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                Vert(c + new Vector3(cs * rIn, 0f, sn * rIn), Vector3.up);
                Vert(c + new Vector3(cs * rOut, 0f, sn * rOut), Vector3.up);
            }
            for (int i = 0; i < seg; i++)
            {
                int a = start + i * 2;
                Quad(a, a + 2, a + 3, a + 1);
            }
        }

        /// <summary>Ellipsoid (radius per axis). Smooth shaded, low poly.</summary>
        public void Sphere(Vector3 c, Vector3 r, int seg = 10, int rings = 6)
        {
            int start = v.Count;
            for (int y = 0; y <= rings; y++)
            {
                float t = y * Mathf.PI / rings;
                float sy = Mathf.Cos(t), sr = Mathf.Sin(t);
                for (int i = 0; i <= seg; i++)
                {
                    float a = i * Mathf.PI * 2f / seg;
                    Vector3 unit = new Vector3(Mathf.Cos(a) * sr, sy, Mathf.Sin(a) * sr);
                    Vector3 nn = new Vector3(unit.x / r.x, unit.y / r.y, unit.z / r.z);
                    Vert(c + Vector3.Scale(unit, r), nn);
                }
            }
            int row = seg + 1;
            for (int y = 0; y < rings; y++)
                for (int i = 0; i < seg; i++)
                {
                    int a = start + y * row + i;
                    Quad(a, a + 1, a + row + 1, a + row);
                }
        }

        public void Sphere(Vector3 c, float r, int seg = 10, int rings = 6) => Sphere(c, new Vector3(r, r, r), seg, rings);

        /// <summary>Capsule-ish limb from a to b (a sphere at each end and a tube between). Good for arms and legs.</summary>
        public void Limb(Vector3 a, Vector3 b, float ra, float rb, int seg = 8)
        {
            Matrix4x4 keep = M;
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion q = len > 1e-5f ? Quaternion.FromToRotation(Vector3.up, d / len) : Quaternion.identity;
            M = keep * Matrix4x4.TRS(a, q, Vector3.one);
            Cylinder(Vector3.zero, ra, rb, len, seg, false);
            M = keep;
            Sphere(a, ra, seg, 4);
            Sphere(b, rb, seg, 4);
        }

        /// <summary>Box stretched between two points (planks, blades, shafts).</summary>
        public void Beam(Vector3 a, Vector3 b, float w, float h)
        {
            Matrix4x4 keep = M;
            Vector3 d = b - a;
            float len = d.magnitude;
            Quaternion q = len > 1e-5f ? Quaternion.LookRotation(d / len, Mathf.Abs(Vector3.Dot(d / len, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up) : Quaternion.identity;
            M = keep * Matrix4x4.TRS((a + b) * 0.5f, q, Vector3.one);
            Box(Vector3.zero, new Vector3(w, h, len));
            M = keep;
        }

        /// <summary>Convex polygon (xy outline, counter-clockwise) extruded along z by depth, centred on z = 0.</summary>
        public void Extrude(Vector2[] outline, float depth, int sideColor = -1)
        {
            float h = depth * 0.5f;
            int count = outline.Length;
            // front (+z faces toward +z) and back
            int f = v.Count;
            for (int i = 0; i < count; i++) Vert(new Vector3(outline[i].x, outline[i].y, h), Vector3.forward);
            for (int i = 1; i < count - 1; i++) Tri(f, f + i, f + i + 1);
            int bk = v.Count;
            for (int i = 0; i < count; i++) Vert(new Vector3(outline[i].x, outline[i].y, -h), Vector3.back);
            for (int i = 1; i < count - 1; i++) Tri(bk, bk + i + 1, bk + i);
            Vector2 keep = col;
            if (sideColor >= 0) col = Pal.UV(sideColor);
            for (int i = 0; i < count; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % count];
                Face(new Vector3(p.x, p.y, -h), new Vector3(q.x, q.y, -h), new Vector3(q.x, q.y, h), new Vector3(p.x, p.y, h));
            }
            col = keep;
        }

        /// <summary>Torus around local y (rings, anchor eyes, bands).</summary>
        public void Torus(Vector3 c, float R, float r, int seg = 12, int tube = 6)
        {
            int start = v.Count;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int j = 0; j <= tube; j++)
                {
                    float b = j * Mathf.PI * 2f / tube;
                    Vector3 nn = dir * Mathf.Cos(b) + Vector3.up * Mathf.Sin(b);
                    Vert(c + dir * R + nn * r, nn);
                }
            }
            int row = tube + 1;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tube; j++)
                {
                    int a = start + i * row + j;
                    Quad(a, a + 1, a + row + 1, a + row);
                }
        }

        /// <summary>Lumpy low-poly boulder: an icosahedron-like sphere with jittered radius (deterministic by seed).</summary>
        public void Boulder(Vector3 c, Vector3 r, int seed)
        {
            const int seg = 9, rings = 5;
            var rnd = new System.Random(seed);
            var ring = new Vector3[(rings + 1) * (seg + 1)];
            for (int y = 0; y <= rings; y++)
            {
                float t = y * Mathf.PI / rings;
                for (int i = 0; i <= seg; i++)
                {
                    float a = (i % seg) * Mathf.PI * 2f / seg + y * 0.35f;
                    float j = (y == 0 || y == rings) ? 1f : 0.82f + (float)rnd.NextDouble() * 0.3f;
                    ring[y * (seg + 1) + i] = c + Vector3.Scale(new Vector3(Mathf.Cos(a) * Mathf.Sin(t), Mathf.Cos(t), Mathf.Sin(a) * Mathf.Sin(t)) * j, r);
                }
                // close the seam exactly
                ring[y * (seg + 1) + seg] = ring[y * (seg + 1)];
            }
            for (int y = 0; y < rings; y++)
                for (int i = 0; i < seg; i++)
                {
                    Vector3 a = ring[y * (seg + 1) + i], b = ring[y * (seg + 1) + i + 1], cc = ring[(y + 1) * (seg + 1) + i + 1], d = ring[(y + 1) * (seg + 1) + i];
                    // flat faces read as chiselled stone
                    if (y == 0) Triangle(a, cc, d);
                    else if (y == rings - 1) Triangle(a, b, d);
                    else { Triangle(a, b, cc); Triangle(a, cc, d); }
                }
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Mesh with rigid one-bone-per-vertex skinning. pivots = each bone's model-space position (bones start unrotated).</summary>
        public Mesh ToSkinnedMesh(string name, Vector3[] pivots)
        {
            Mesh m = ToMesh(name);
            var w = new BoneWeight[bones.Count];
            for (int i = 0; i < w.Length; i++) w[i] = new BoneWeight { boneIndex0 = bones[i], weight0 = 1f };
            m.boneWeights = w;
            var bp = new Matrix4x4[pivots.Length];
            for (int i = 0; i < bp.Length; i++) bp[i] = Matrix4x4.Translate(-pivots[i]);
            m.bindposes = bp;
            return m;
        }
    }
}
