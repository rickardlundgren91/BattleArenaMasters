using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>Small shared meshes: ground decals, status markers, effect rings, projectiles. Built once and cached.</summary>
    static class Shapes
    {
        static readonly MeshBuilder b = new MeshBuilder();
        static readonly Dictionary<int, Mesh> cache = new Dictionary<int, Mesh>();

        static bool Cached(int key, out Mesh m) => cache.TryGetValue(key, out m);
        static Mesh Store(int key, string name) { Mesh m = b.ToMesh(name); cache[key] = m; return m; }

        /// <summary>Soft shadow plus a ring in the marker's colour, radius 1 (scaled by the unit).</summary>
        public static Mesh Decal(UnitMarker marker)
        {
            int key = 100 + (int)marker;
            if (Cached(key, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.ShadowSoft).Disc(Vector3.zero, 0.85f, 20);
            b.Color(Pal.Shadow).Disc(new Vector3(0f, 0.002f, 0f), 0.6f, 20);
            // only the player's own hero gets a ring (old View3DMeMark); allies, enemies and towers just the soft shadow, as the old
            // SyncGround (its team rings were removed 2026-10-19)
            // Rickard 2026-10-30 (MLBB): weaker and thinner, half see-through and a third as wide as before (0.2)
            byte c = marker == UnitMarker.Player ? Pal.PlayerRingSoft : (byte)255;
            if (c != 255)
            {
                float w = marker == UnitMarker.Player ? 0.07f : 0.13f;
                b.Color(c).Ring(new Vector3(0f, 0.004f, 0f), 1f - w, 1f, 28);
                if (marker == UnitMarker.Player) // a small arrow in front: which way the player's hero faces
                {
                    b.M = Matrix4x4.TRS(new Vector3(0f, 0.004f, 1.12f), Quaternion.identity, Vector3.one);
                    b.Triangle(new Vector3(-0.14f, 0f, 0f), new Vector3(0f, 0f, 0.16f), new Vector3(0.14f, 0f, 0f));
                    b.Reset();
                }
            }
            return Store(key, "Decal " + marker);
        }

        public static Mesh Disc(byte color, int seg = 32)
        {
            int key = 200 + color;
            if (Cached(key, out Mesh m)) return m;
            b.Clear();
            b.Color(color).Disc(Vector3.zero, 1f, seg);
            return Store(key, "Disc");
        }

        public static Mesh RingMesh(float inner, byte color, int seg = 40)
        {
            int key = 1000 + color * 128 + Mathf.RoundToInt(inner * 100f);
            if (Cached(key, out Mesh m)) return m;
            b.Clear();
            b.Color(color).Ring(Vector3.zero, inner, 1f, seg);
            return Store(key, "Ring");
        }

        /// <summary>Filled circle (soft) with a bright rim. Used by zones and the aim circle; tinted by the material.</summary>
        public static Mesh ZoneCircle()
        {
            if (Cached(300, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.WhiteSoft).Disc(Vector3.zero, 0.94f, 40);
            b.Color(Pal.White).Ring(new Vector3(0f, 0.002f, 0f), 0.92f, 1f, 40);
            return Store(300, "Zone Circle");
        }

        /// <summary>A zone circle cut to how far it reaches each way (rim: points in the local xz plane, any order, at radius 1 where
        /// nothing cuts it): soft fill with a bright edge like ZoneCircle, or only the fill. Not cached: the caller destroys it.</summary>
        public static Mesh ReachFan(Vector3[] rim, bool edge)
        {
            System.Array.Sort(rim, (p, q) => Mathf.Atan2(p.z, p.x).CompareTo(Mathf.Atan2(q.z, q.x)));
            b.Clear();
            b.Color(edge ? Pal.WhiteSoft : Pal.WhiteMid);
            float fill = edge ? 0.94f : 1f;
            for (int i = 0; i < rim.Length; i++) b.Triangle(Vector3.zero, rim[(i + 1) % rim.Length] * fill, rim[i] * fill);
            if (edge)
            {
                b.Color(Pal.White);
                Vector3 up = new Vector3(0f, 0.002f, 0f);
                for (int i = 0; i < rim.Length; i++)
                {
                    Vector3 p = rim[i], q = rim[(i + 1) % rim.Length];
                    b.Face(p * 0.92f + up, q * 0.92f + up, q + up, p + up);
                }
            }
            return b.ToMesh("Zone Reach");
        }

        /// <summary>Strip from z = 0 to z = 1, x from -1 to 1, soft fill with bright edges.</summary>
        public static Mesh ZoneStrip()
        {
            if (Cached(301, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.WhiteSoft).Face(new Vector3(-0.94f, 0f, 0f), new Vector3(-0.94f, 0f, 1f), new Vector3(0.94f, 0f, 1f), new Vector3(0.94f, 0f, 0f));
            b.Color(Pal.White);
            b.Face(new Vector3(-1f, 0.002f, 0f), new Vector3(-1f, 0.002f, 1f), new Vector3(-0.92f, 0.002f, 1f), new Vector3(-0.92f, 0.002f, 0f));
            b.Face(new Vector3(0.92f, 0.002f, 0f), new Vector3(0.92f, 0.002f, 1f), new Vector3(1f, 0.002f, 1f), new Vector3(1f, 0.002f, 0f));
            return Store(301, "Zone Strip");
        }

        /// <summary>Half of ZoneCircle on the +z side (radius 1): ZoneStrip's round end, so a strip hit as a capsule is drawn as one.</summary>
        public static Mesh ZoneCap()
        {
            if (Cached(305, out Mesh m)) return m;
            b.Clear();
            const int Seg = 20;
            for (int i = 0; i < Seg; i++)
            {
                float a0 = Mathf.PI * (1f - (float)i / Seg), a1 = Mathf.PI * (1f - (float)(i + 1) / Seg);
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)), up = new Vector3(0f, 0.002f, 0f);
                b.Color(Pal.WhiteSoft).Triangle(Vector3.zero, d0 * 0.94f, d1 * 0.94f);
                b.Color(Pal.White).Triangle(d0 * 0.92f + up, d0 + up, d1 + up);
                b.Triangle(d0 * 0.92f + up, d1 + up, d1 * 0.92f + up);
            }
            return Store(305, "Zone Cap");
        }

        /// <summary>Tyra's Arrow Volley on the ground (precision TYRA-09): a soft fan of radius 1 from the origin along +z, +-half
        /// radians, with a bright line along each of the n arrows' paths. Scaled by her reach and turned the volley's way.</summary>
        public static Mesh VolleyFan(float half, int n)
        {
            if (Cached(304, out Mesh m)) return m; // one volley shape (TyraS's numbers never change in a match)
            b.Clear();
            const int seg = 12;
            b.Color(Pal.WhiteSoft);
            for (int i = 0; i < seg; i++)
            {
                float a0 = -half + 2f * half * i / seg, a1 = -half + 2f * half * (i + 1) / seg;
                b.Triangle(Vector3.zero, new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)));
            }
            b.Color(Pal.White);
            const float w = 0.012f; // half width of an arrow's line, of the radius (0.05 tile at her 4.0)
            for (int i = 0; i < n; i++)
            {
                float a = n == 1 ? 0f : -half + 2f * half * i / (n - 1);
                Vector3 d = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)), side = new Vector3(d.z, 0f, -d.x) * w, up = new Vector3(0f, 0.002f, 0f);
                b.Face(up - side, up - side + d, up + side + d, up + side);
            }
            return Store(304, "Volley Fan");
        }

        /// <summary>Aim line body: x -0.5..0.5, z 0..1.</summary>
        public static Mesh AimBody()
        {
            if (Cached(302, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.WhiteMid).Face(new Vector3(-0.5f, 0f, 0f), new Vector3(-0.5f, 0f, 1f), new Vector3(0.5f, 0f, 1f), new Vector3(0.5f, 0f, 0f));
            return Store(302, "Aim Body");
        }

        /// <summary>Arrow head: base at z = 0 (width 1), tip at z = 1.</summary>
        public static Mesh AimHead()
        {
            if (Cached(303, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.White).Triangle(new Vector3(-0.5f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(0.5f, 0f, 0f));
            return Store(303, "Aim Head");
        }

        /// <summary>Three stars circling over the head (stun).</summary>
        public static Mesh StunStars()
        {
            if (Cached(400, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.Stun);
            var star = new Vector2[10];
            for (int k = 0; k < 3; k++)
            {
                float a = k * Mathf.PI * 2f / 3f;
                b.M = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.25f, 0f, Mathf.Sin(a) * 0.25f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg + 90f, 0f), Vector3.one);
                // a five-pointed star as five convex kites
                for (int i = 0; i < 5; i++)
                {
                    float a0 = (i * 72f + 90f) * Mathf.Deg2Rad, a1 = (i * 72f + 126f) * Mathf.Deg2Rad, a2 = (i * 72f + 54f) * Mathf.Deg2Rad;
                    star[0] = Vector2.zero;
                    star[1] = new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * 0.04f;
                    star[2] = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * 0.1f;
                    star[3] = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * 0.04f;
                    b.Extrude(new[] { star[0], star[1], star[2], star[3] }, 0.025f);
                }
            }
            b.Reset();
            return Store(400, "Stun Stars");
        }

        /// <summary>Icy ring with frost shards at the feet (slow).</summary>
        public static Mesh SlowRing()
        {
            if (Cached(402, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.Slow).Ring(Vector3.zero, 0.75f, 1f, 24);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                b.M = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.88f, 0f, Mathf.Sin(a) * 0.88f), Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 15f), Vector3.one);
                b.Cylinder(Vector3.zero, 0.09f, 0f, 0.3f, 4, false);
            }
            b.Reset();
            return Store(402, "Slow");
        }

        /// <summary>Vines and thorns closing around the feet (root).</summary>
        public static Mesh RootVines()
        {
            if (Cached(403, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.GrassDark).Torus(new Vector3(0f, 0.06f, 0f), 0.8f, 0.07f, 16, 4);
            b.Color(Pal.Root);
            for (int i = 0; i < 7; i++)
            {
                float a = i * Mathf.PI * 2f / 7f;
                b.M = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.8f, 0f, Mathf.Sin(a) * 0.8f), Quaternion.Euler(Mathf.Sin(a) * 25f, 0f, -Mathf.Cos(a) * 25f), Vector3.one);
                b.Cylinder(Vector3.zero, 0.08f, 0f, 0.45f, 5, false);
            }
            b.Reset();
            return Store(403, "Root");
        }

        /// <summary>Flame tongues around the body (burn). Drawn additive.</summary>
        public static Mesh Flames()
        {
            if (Cached(404, out Mesh m)) return m;
            b.Clear();
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                b.Color(i % 2 == 0 ? Pal.Fire : Pal.FireCore);
                b.M = Matrix4x4.TRS(new Vector3(Mathf.Cos(a) * 0.6f, 0.1f, Mathf.Sin(a) * 0.6f), Quaternion.identity, Vector3.one);
                b.Cylinder(Vector3.zero, 0.16f, 0f, 0.55f + (i % 2) * 0.2f, 6, false);
            }
            b.Reset();
            return Store(404, "Flames");
        }

        /// <summary>Little poison bubbles over the head.</summary>
        public static Mesh PoisonBubbles()
        {
            if (Cached(405, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.Poison);
            b.Sphere(new Vector3(-0.12f, 0f, 0f), 0.06f, 8, 5);
            b.Sphere(new Vector3(0.1f, 0.08f, 0.03f), 0.045f, 8, 5);
            b.Sphere(new Vector3(0.02f, 0.18f, -0.04f), 0.035f, 8, 5);
            b.Color(Pal.Ink).Box(new Vector3(0f, -0.07f, 0f), new Vector3(0.03f, 0.03f, 0.03f));
            return Store(405, "Poison");
        }

        /// <summary>Speed lines behind the unit (haste). Drawn additive.</summary>
        public static Mesh HasteLines()
        {
            if (Cached(406, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.Haste);
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * 0.35f, y = 0.35f + (i % 2) * 0.25f;
                b.Beam(new Vector3(x, y, -0.45f), new Vector3(x, y, -1.1f + i * 0.1f), 0.03f, 0.03f);
            }
            return Store(406, "Haste");
        }

        /// <summary>Crescent for melee swings: an arc of quads in the xz plane, opening forward.</summary>
        public static Mesh Slash()
        {
            if (Cached(407, out Mesh m)) return m;
            b.Clear();
            const int seg = 10;
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.Lerp(-70f, 70f, i / (float)seg) * Mathf.Deg2Rad, a1 = Mathf.Lerp(-70f, 70f, (i + 1) / (float)seg) * Mathf.Deg2Rad;
                float w0 = Mathf.Sin(i / (float)seg * Mathf.PI) * 0.18f + 0.02f, w1 = Mathf.Sin((i + 1) / (float)seg * Mathf.PI) * 0.18f + 0.02f;
                Vector3 o0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), o1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                b.Color(Pal.White).Face(o0 * (0.5f - w0), o0 * 0.5f, o1 * 0.5f, o1 * (0.5f - w1));
            }
            return Store(407, "Slash");
        }

        public static Mesh Cube()
        {
            if (Cached(408, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.White).Box(Vector3.zero, Vector3.one);
            return Store(408, "Cube");
        }

        public static Mesh Ball()
        {
            if (Cached(409, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.White).Sphere(Vector3.zero, 1f, 14, 9);
            return Store(409, "Ball");
        }

        /// <summary>A chain of links along z from 0 to 1 (scaled to the tether length).</summary>
        public static Mesh Chain() => Chain(16);

        /// <summary>Knut's chain, one unit long (scaled to its length) with this many links: the view picks the count by the
        /// length, so every link stays ChainLink long instead of stretching (2026-11-02).</summary>
        public static Mesh Chain(int links)
        {
            links = Mathf.Clamp(links, 2, 64);
            int key = links == 16 ? 410 : 90000 + links;
            if (Cached(key, out Mesh m)) return m;
            b.Clear();
            b.Color(Pal.MetalDark);
            for (int i = 0; i < links; i++)
            {
                float z = (i + 0.5f) / links;
                if (i % 2 == 0) b.Box(new Vector3(0f, 0f, z), new Vector3(0.07f, 0.025f, 1f / links * 1.1f));
                else b.Box(new Vector3(0f, 0f, z), new Vector3(0.025f, 0.07f, 1f / links * 1.1f));
            }
            return Store(key, "Chain");
        }

        // ---------------------------------------------------------------- projectiles (forward = +z, centred)

        public static Mesh Projectile(ProjKind kind, int team)
        {
            int key = 2000 + (int)kind * 4 + team;
            if (Cached(key, out Mesh m)) return m;
            b.Clear();
            switch (kind)
            {
                case ProjKind.BramHammer:
                case ProjKind.BramHammerSpecial:
                case ProjKind.BramBolt: // his thrown Hammer Line (2026-11-12: "kasta en hammare och inte en laserstråle"), the biggest
                {
                    float s = kind == ProjKind.BramBolt ? 1.7f : kind == ProjKind.BramHammerSpecial ? 1.35f : 1f;
                    b.Color(Pal.Wood).Box(new Vector3(0f, -0.12f * s, 0f), new Vector3(0.05f, 0.3f, 0.05f) * s);
                    b.Color(Pal.Metal).Box(new Vector3(0f, 0.08f * s, 0f), new Vector3(0.17f, 0.17f, 0.3f) * s, Pal.Steel);
                    b.Color(kind != ProjKind.BramHammer ? Pal.Team(team) : Pal.MetalDark).Box(new Vector3(0f, 0.08f * s, 0.16f * s), new Vector3(0.19f, 0.19f, 0.03f) * s);
                    b.Box(new Vector3(0f, 0.08f * s, -0.16f * s), new Vector3(0.19f, 0.19f, 0.03f) * s);
                    break;
                }
                case ProjKind.TyraArrow:
                case ProjKind.TyraVolley:
                case ProjKind.RangedMinionShot:
                {
                    float s = kind == ProjKind.TyraVolley ? 1.15f : kind == ProjKind.TyraArrow ? 1f : 0.75f;
                    b.Color(Pal.Wood).Beam(new Vector3(0f, 0f, -0.3f * s), new Vector3(0f, 0f, 0.22f * s), 0.025f, 0.025f);
                    b.Color(kind == ProjKind.TyraVolley ? Pal.Gold : Pal.Metal).At(new Vector3(0f, 0f, 0.2f * s), new Vector3(90f, 0f, 0f)).Cylinder(Vector3.zero, 0.04f, 0f, 0.1f, 5);
                    b.Reset();
                    b.Color(Pal.TeamLight(team)).Box(new Vector3(0f, 0.025f, -0.26f * s), new Vector3(0.01f, 0.05f, 0.1f));
                    b.Box(new Vector3(0.025f, 0f, -0.26f * s), new Vector3(0.05f, 0.01f, 0.1f));
                    break;
                }
                case ProjKind.TyraBolt:
                    b.Color(Pal.Steel).Beam(new Vector3(0f, 0f, -0.42f), new Vector3(0f, 0f, 0.3f), 0.045f, 0.045f);
                    b.Color(Pal.Goggle).Sphere(new Vector3(0f, 0f, 0.3f), new Vector3(0.09f, 0.09f, 0.16f), 10, 6);
                    b.Color(Pal.TeamLight(team)).Box(new Vector3(0f, 0.05f, -0.36f), new Vector3(0.015f, 0.1f, 0.16f));
                    b.Box(new Vector3(0.05f, 0f, -0.36f), new Vector3(0.1f, 0.015f, 0.16f));
                    break;
                case ProjKind.MoaBolt:
                    b.Color(Pal.Honey).Sphere(Vector3.zero, 0.13f, 12, 8);
                    b.Color(Pal.HoneyDark).Sphere(new Vector3(0f, 0f, -0.12f), new Vector3(0.08f, 0.08f, 0.12f), 10, 6);
                    b.Color(Pal.Cream).Sphere(new Vector3(0.04f, 0.05f, 0.06f), 0.035f, 6, 4);
                    break;
                case ProjKind.KnutAnchor:
                    b.Color(Pal.MetalDark).Box(new Vector3(0f, 0f, -0.05f), new Vector3(0.07f, 0.07f, 0.45f));
                    b.Box(new Vector3(0f, 0f, -0.22f), new Vector3(0.26f, 0.06f, 0.06f));
                    b.Color(Pal.Metal).Beam(new Vector3(0f, 0f, 0.18f), new Vector3(0.17f, 0f, 0.05f), 0.06f, 0.06f);
                    b.Beam(new Vector3(0f, 0f, 0.18f), new Vector3(-0.17f, 0f, 0.05f), 0.06f, 0.06f);
                    b.Color(Pal.Steel).Box(new Vector3(0.19f, 0f, 0.1f), new Vector3(0.08f, 0.04f, 0.12f));
                    b.Box(new Vector3(-0.19f, 0f, 0.1f), new Vector3(0.08f, 0.04f, 0.12f));
                    b.Color(Pal.Team(team)).Box(new Vector3(0f, 0f, 0.05f), new Vector3(0.09f, 0.09f, 0.07f));
                    break;
                case ProjKind.TiktaNeedle:
                case ProjKind.TiktaNeedleSpecial:
                {
                    float s = kind == ProjKind.TiktaNeedleSpecial ? 1.3f : 1f;
                    b.Color(Pal.Steel).Beam(new Vector3(0f, 0f, -0.2f * s), new Vector3(0f, 0f, 0.18f * s), 0.025f * s, 0.025f * s);
                    b.Color(kind == ProjKind.TiktaNeedleSpecial ? Pal.Teal : Pal.Pink).Beam(new Vector3(0f, 0f, 0.18f * s), new Vector3(0f, 0f, 0.26f * s), 0.018f * s, 0.018f * s);
                    b.Color(Pal.TeamLight(team)).Box(new Vector3(0f, 0f, -0.2f * s), new Vector3(0.05f, 0.05f, 0.03f));
                    break;
                }
                case ProjKind.BlastFire:
                    b.Color(Pal.FireCore).Sphere(Vector3.zero, 0.11f, 10, 6);
                    b.Color(Pal.Fire).Sphere(new Vector3(0f, 0f, -0.1f), new Vector3(0.12f, 0.12f, 0.2f), 10, 6);
                    break;
                case ProjKind.BlastBomb:
                case ProjKind.HildeKeg: // her keg's fallback without the model
                case ProjKind.HildeBurst:
                    b.Color(Pal.Bomb).Sphere(Vector3.zero, 0.17f, 12, 8);
                    b.Color(Pal.Cream).Sphere(new Vector3(-0.05f, 0.03f, 0.15f), 0.035f, 6, 4);
                    b.Sphere(new Vector3(0.05f, 0.03f, 0.15f), 0.035f, 6, 4);
                    b.Color(Pal.Team(team)).Torus(Vector3.zero, 0.17f, 0.022f, 14, 4);
                    b.Color(Pal.MetalDark).Cylinder(new Vector3(0f, 0.15f, 0f), 0.05f, 0.05f, 0.05f, 8);
                    b.Color(Pal.Fuse).Beam(new Vector3(0f, 0.2f, 0f), new Vector3(0.03f, 0.28f, -0.03f), 0.025f, 0.025f);
                    break;
                case ProjKind.SiegeShell:
                case ProjKind.HildeShell: // her cannon's shell
                    b.Color(Pal.Bomb).Sphere(Vector3.zero, 0.12f, 10, 6);
                    b.Color(Pal.Team(team)).Torus(Vector3.zero, 0.12f, 0.018f, 12, 4);
                    break;
                case ProjKind.KuggShot: // a stand-in until Rickard's own picture (O-063, tx_fx_kugg_shot)
                case ProjKind.KuggShell:
                    b.Color(Pal.MetalDark).Sphere(Vector3.zero, kind == ProjKind.KuggShell ? 0.15f : 0.11f, 10, 6);
                    b.Color(Pal.TeamLight(team)).Torus(Vector3.zero, kind == ProjKind.KuggShell ? 0.15f : 0.11f, 0.02f, 12, 4);
                    break;
                case ProjKind.KuggSpark: // a stand-in until O-064 (tx_fx_kugg_spark)
                    b.Color(Pal.TeamLight(team)).Sphere(Vector3.zero, 0.07f, 8, 5);
                    break;
                case ProjKind.NasslaThorn: // a stand-in until Rickard's own picture: a green thorn
                    b.Color(Pal.TeamLight(team)).Sphere(Vector3.zero, 0.06f, 8, 5);
                    b.Color(Pal.TeamLight(team)).Beam(new Vector3(0f, 0f, -0.12f), new Vector3(0f, 0f, 0.12f), 0.03f, 0.005f);
                    break;
                case ProjKind.SkrallBolt: // a stand-in until Rickard's own picture: a small bright spark (the flight is near-instant)
                    b.Color(Pal.White).Sphere(Vector3.zero, 0.08f, 8, 5);
                    b.Color(Pal.TeamLight(team)).Torus(Vector3.zero, 0.09f, 0.015f, 10, 4);
                    break;
                case ProjKind.TowerShot:
                    b.Color(Pal.TeamLight(team)).Sphere(Vector3.zero, 0.16f, 12, 8);
                    b.Color(Pal.White).Sphere(new Vector3(0f, 0f, 0.03f), 0.09f, 8, 5);
                    break;
                default: // melee kinds: the crescent is drawn by the view, this is only a fallback
                    b.Color(Pal.White).Sphere(Vector3.zero, 0.05f, 6, 4);
                    break;
            }
            return Store(key, "Proj " + kind);
        }
    }
}
