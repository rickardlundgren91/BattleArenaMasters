using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// The static map in two meshes: everything solid (ground, road, walls, cliffs, rocks, bushes, tower bases) in one
    /// lit palette mesh, the soft ground marks (shadows under walls, lane dashes) in one transparent mesh.
    /// </summary>
    static class Arena
    {
        const byte TVoid = 0, TGround = 1, TGrass = 2, TWall = 3;
        const float WallH = 1.0f, CliffDepth = 2.6f, BushH = 0.62f;

        // Rickard's painted textures (Resources/BAMArt/Arena) and how many units one repeat covers. Missing file = palette colour.
        sealed class Layer
        {
            public readonly MeshBuilder B = new MeshBuilder();
            public Texture2D T;
            public string Name;
        }

        static Layer Load(string name, float repeat)
        {
            var l = new Layer { Name = name, T = Resources.Load<Texture2D>("BAMArt/Arena/" + name) };
            l.B.WorldUV = repeat;
            return l;
        }

        static float waterY;

        public static GameObject Build(ArenaInfo info)
        {
            var root = new GameObject("Arena");
            var solid = new MeshBuilder();
            var marks = new MeshBuilder();
            Layer ground = Load("T_BAM_Ground_Cobble", 6f), road = Load("T_BAM_Ground_Road2", 4f), grass = Load("T_BAM_Ground_Grass", 6f),
                wall = Load("T_BAM_Block", 2f), kerb = Load("T_BAM_Kerb", 1f), water = Load("T_BAM_Water", 6f), plinth = Load("T_BAM_Plinth", 3f);
            int w = info.Width, h = info.Height;
            float laneX = w * 0.5f;
            var rnd = new System.Random(1234);
            bool moat = water.T != null;
            waterY = moat ? -0.55f : -CliffDepth;

            // water (or an abyss) all around and below, so the camera never sees the empty background at the map edge;
            // the island floats over its own cloud sea instead
            if (!info.Island) Pick(water, solid, Pal.Void).Face(new Vector3(-40f, waterY, -40f), new Vector3(-40f, waterY, h + 40f), new Vector3(w + 40f, waterY, h + 40f), new Vector3(w + 40f, waterY, -40f));

            for (int ty = 0; ty < h; ty++)
                for (int tx = 0; tx < w; tx++)
                {
                    int code = info.Tile(tx, ty);
                    if (code == TVoid) continue;
                    if (info.Island)
                    {
                        // the painted platform covers rows 0-16 (only its bushes come from here); Island draws the bridge and lane
                        if (ty < Island.PlatformRows && code == TGrass) Bush(solid, info.TileCenter(tx, ty), tx, ty, rnd);
                        continue;
                    }
                    Vector3 c = info.TileCenter(tx, ty);
                    float x0 = c.x - 0.5f, x1 = c.x + 0.5f, z0 = c.z - 0.5f, z1 = c.z + 0.5f;
                    bool inRock = InRock(info, c);

                    if (code == TWall && !inRock)
                    {
                        // stone block, a lighter cap and a dark seam so neighbours read as one wall
                        MeshBuilder wb = Pick(wall, solid, Pal.Stone);
                        wb.Box(new Vector3(c.x, WallH * 0.45f, c.z), new Vector3(1f, WallH * 0.9f, 1f), wall.T != null ? -1 : Pal.StoneDark);
                        Pick(wall, solid, Pal.StoneTop).Box(new Vector3(c.x, WallH * 0.95f, c.z), new Vector3(0.96f, WallH * 0.1f, 0.96f), wall.T != null ? -1 : Pal.StoneTop);
                        if (wall.T == null && ((tx + ty) & 1) == 0) solid.Color(Pal.StoneDark).Box(new Vector3(c.x, WallH * 0.45f, c.z), new Vector3(1.01f, 0.04f, 1.01f));
                        // contact shadow on the ground around the block
                        marks.Color(Pal.ShadowSoft).Face(new Vector3(x0 - 0.25f, 0.012f, z0 - 0.25f), new Vector3(x0 - 0.25f, 0.012f, z1 + 0.25f), new Vector3(x1 + 0.25f, 0.012f, z1 + 0.25f), new Vector3(x1 + 0.25f, 0.012f, z0 - 0.25f));
                    }
                    else
                    {
                        float dLane = Mathf.Abs(c.x - laneX);
                        MeshBuilder gb;
                        if (code == TGrass) gb = Pick(grass, solid, Pal.GrassDark);
                        else if (dLane < 1.6f) gb = Pick(road, solid, Pal.Road);
                        else if (dLane < 2.1f && road.T == null) gb = solid.Color(Pal.RoadEdge);
                        else gb = Pick(ground, solid, ((tx + ty) & 1) == 0 ? Pal.Ground : Pal.GroundB);
                        gb.Face(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0));
                        if (code == TGrass) Bush(solid, c, tx, ty, rnd);
                        else if (ground.T == null && dLane >= 2.1f && rnd.NextDouble() < 0.18) Pebble(solid, c, rnd);
                    }

                    // cliffs where the map ends or a void tile begins
                    if (Void(info, tx, ty - 1)) Cliff(solid, kerb, new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1));
                    if (Void(info, tx, ty + 1)) Cliff(solid, kerb, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
                    if (Void(info, tx - 1, ty)) Cliff(solid, kerb, new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0));
                    if (Void(info, tx + 1, ty)) Cliff(solid, kerb, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
                }

            // lane dashes down the road between the towers
            if (info.Towers != null && info.Towers.Length >= 2)
            {
                float za = Mathf.Min(info.Towers[0].z, info.Towers[1].z) + 1.6f, zb = Mathf.Max(info.Towers[0].z, info.Towers[1].z) - 1.6f;
                for (float z = za; z < zb; z += 1.2f)
                    marks.Color(Pal.WhiteSoft).Face(new Vector3(laneX - 0.06f, 0.011f, z), new Vector3(laneX - 0.06f, 0.011f, z + 0.6f), new Vector3(laneX + 0.06f, 0.011f, z + 0.6f), new Vector3(laneX + 0.06f, 0.011f, z));
            }

            if (info.Rocks != null)
                for (int i = 0; i < info.Rocks.Length; i++)
                {
                    Vector3 r = info.Rocks[i];
                    float rad = Mathf.Max(0.3f, r.z);
                    Vector3 p = new Vector3(r.x, 0f, r.y);
                    solid.Color(Pal.Rock).Boulder(p + new Vector3(0f, rad * 0.45f, 0f), new Vector3(rad * 0.95f, rad * 0.75f, rad * 1.05f), 77 + i);
                    solid.Color(Pal.RockLight).Boulder(p + new Vector3(rad * 0.45f, rad * 0.25f, rad * 0.5f), new Vector3(rad * 0.4f, rad * 0.35f, rad * 0.4f), 91 + i);
                    solid.Color(Pal.StoneDark).Boulder(p + new Vector3(-rad * 0.5f, rad * 0.15f, -rad * 0.4f), new Vector3(rad * 0.3f, rad * 0.25f, rad * 0.3f), 103 + i);
                    marks.Color(Pal.Shadow).Disc(new Vector3(p.x, 0.012f, p.z), rad * 1.15f, 20);
                }

            if (info.Towers != null)
                for (int t = 0; t < info.Towers.Length; t++)
                {
                    Vector3 p = info.Towers[t];
                    p.y = 0f;
                    Pick(plinth, solid, Pal.StoneDark).Cylinder(p, 1.45f, 1.35f, 0.12f, 16, true, plinth.T != null ? -1 : Pal.StoneTop);
                    solid.Color(Pal.Team(t)).Cylinder(p + new Vector3(0f, 0.12f, 0f), 1.3f, 1.3f, 0.03f, 16, true, Pal.Team(t));
                    Pick(plinth, solid, Pal.StoneTop).Cylinder(p + new Vector3(0f, 0.12f, 0f), 1.12f, 1.12f, 0.035f, 16, true, plinth.T != null ? -1 : Pal.StoneTop);
                }

            Add(root, "Solid", solid.ToMesh("Arena"), ArtMaterials.Lit, true);
            foreach (Layer l in new[] { ground, road, grass, wall, kerb, water, plinth })
            {
                if (l.T == null || l.B.VertexCount == 0) continue;
                var m = ArtMaterials.Instance(ArtMaterials.Lit);
                m.SetTexture(ArtMaterials.BaseMap, l.T);
                m.mainTexture = l.T;
                Add(root, l.Name, l.B.ToMesh(l.Name), m, l != water);
            }
            Add(root, "Marks", marks.ToMesh("Arena Marks"), MarksMaterial(), false);
            if (info.Island) Island.Build(root, info);
            return root;
        }

        /// <summary>The textured layer when its picture loaded, else the palette mesh in the fallback colour.</summary>
        static MeshBuilder Pick(Layer l, MeshBuilder solid, byte fallback) => l.T != null ? l.B : solid.Color(fallback);

        static Material marksMat;
        static Material MarksMaterial()
        {
            if (marksMat != null) return marksMat;
            marksMat = ArtMaterials.Instance(ArtMaterials.UnlitFade);
            marksMat.renderQueue = 2950; // before every unit ring, zone and effect
            return marksMat;
        }

        internal static GameObject Add(GameObject root, string name, Mesh mesh, Material mat, bool lit)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.receiveShadows = lit;
            r.shadowCastingMode = lit && ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (lit) ArtKit.RegisterCaster(r);
            go.isStatic = true;
            return go;
        }

        static bool Void(ArenaInfo a, int tx, int ty) => tx < 0 || ty < 0 || tx >= a.Width || ty >= a.Height || a.Tile(tx, ty) == TVoid;

        static bool InRock(ArenaInfo a, Vector3 c)
        {
            if (a.Rocks == null) return false;
            for (int i = 0; i < a.Rocks.Length; i++)
            {
                float dx = c.x - a.Rocks[i].x, dz = c.z - a.Rocks[i].y, r = a.Rocks[i].z + 0.35f;
                if (dx * dx + dz * dz <= r * r) return true;
            }
            return false;
        }

        // Edge from a to b, ordered so the face looks outward (see the callers).
        static void Cliff(MeshBuilder m, Layer kerb, Vector3 a, Vector3 b)
        {
            Vector3 down = Vector3.down;
            float bottom = -waterY + 0.1f;
            MeshBuilder top = Pick(kerb, m, Pal.Cliff);
            top.Face(a, b, b + down * 0.5f, a + down * 0.5f);
            m.Color(Pal.CliffDark).Face(a + down * 0.5f, b + down * 0.5f, b + down * Mathf.Max(bottom, 0.6f), a + down * Mathf.Max(bottom, 0.6f));
            // a low curb of stone along the edge: you see where the arena stops
            Vector3 mid = (a + b) * 0.5f, along = (b - a).normalized, outward = new Vector3(along.z, 0f, -along.x);
            MeshBuilder cb = Pick(kerb, m, Pal.StoneTop);
            cb.At(mid - outward * 0.07f + Vector3.up * 0.05f, new Vector3(0f, Mathf.Atan2(along.x, along.z) * Mathf.Rad2Deg, 0f));
            cb.Box(Vector3.zero, new Vector3(0.14f, 0.1f, 1.0f), kerb.T != null ? -1 : Pal.StoneTop);
            cb.Reset();
        }

        // A round hedge clump per grass tile. Clumps overlap their neighbours, so a grass area reads as one bush.
        internal static void Bush(MeshBuilder m, Vector3 c, int tx, int ty, System.Random rnd)
        {
            for (int i = 0; i < 3; i++)
            {
                float ox = (float)rnd.NextDouble() * 0.5f - 0.25f, oz = (float)rnd.NextDouble() * 0.5f - 0.25f;
                float r = 0.42f + (float)rnd.NextDouble() * 0.12f;
                m.Color(i == 0 ? Pal.Grass : i == 1 ? Pal.GrassLight : Pal.Grass);
                m.Sphere(c + new Vector3(ox, BushH * 0.55f, oz), new Vector3(r, BushH * 0.6f, r), 8, 5);
            }
            // a few leaf tips sticking up break the dome shape
            for (int i = 0; i < 2; i++)
            {
                float ox = (float)rnd.NextDouble() * 0.6f - 0.3f, oz = (float)rnd.NextDouble() * 0.6f - 0.3f;
                m.Color(Pal.GrassLight).At(c + new Vector3(ox, BushH * 0.8f, oz), new Vector3((float)rnd.NextDouble() * 30f - 15f, 0f, (float)rnd.NextDouble() * 30f - 15f));
                m.Cylinder(Vector3.zero, 0.08f, 0f, 0.28f, 4, false);
                m.Reset();
            }
        }

        static void Pebble(MeshBuilder m, Vector3 c, System.Random rnd)
        {
            float ox = (float)rnd.NextDouble() * 0.7f - 0.35f, oz = (float)rnd.NextDouble() * 0.7f - 0.35f, r = 0.05f + (float)rnd.NextDouble() * 0.06f;
            m.Color(Pal.RoadEdge).Sphere(c + new Vector3(ox, r * 0.3f, oz), new Vector3(r * 1.3f, r * 0.6f, r), 6, 3);
        }
    }
}
