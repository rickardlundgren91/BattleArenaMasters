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

            // the BAM arena's own ground picture (grass, cobbled road between the towers, worn earth along it, tall grass under the
            // bushes), baked offline exactly like the old game painted it (bam-work/art/ground/bake.py); only for that map's size
            Texture2D painted = !info.Island && w == 15 && h == 35 ? Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_ArenaGround_v1_2") : null;
            if (painted != null) PaintedGround(root, painted, w, h);
            Mesh pillarM = Props.Mesh("wallkit_pillar"), beamM = Props.Mesh("wallkit_straight");
            bool kit = pillarM != null && beamM != null && !info.Island;
            Mesh bushM = info.Island ? null : Props.Mesh("bam_bush");
            Texture2D rockPic = info.Island ? null : Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_Pillar_C");

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

                    if (code == TWall && !inRock && kit)
                    {
                        // Rickard's Meshy wall kit stands here (WallKit below); only the contact shadow on the ground
                        marks.Color(Pal.ShadowSoft).Face(new Vector3(x0 - 0.2f, 0.012f, z0 - 0.2f), new Vector3(x0 - 0.2f, 0.012f, z1 + 0.2f), new Vector3(x1 + 0.2f, 0.012f, z1 + 0.2f), new Vector3(x1 + 0.2f, 0.012f, z0 - 0.2f));
                    }
                    else if (code == TWall && !inRock)
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
                        if (painted == null) gb.Face(new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), new Vector3(x1, 0f, z0));
                        if (code == TGrass) { if (bushM == null) Bush(solid, c, tx, ty, rnd); }
                        else if (ground.T == null && dLane >= 2.1f && rnd.NextDouble() < 0.18) Pebble(solid, c, rnd);
                    }

                    // cliffs where the map ends or a void tile begins
                    if (Void(info, tx, ty - 1)) Cliff(solid, kerb, new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1));
                    if (Void(info, tx, ty + 1)) Cliff(solid, kerb, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0));
                    if (Void(info, tx - 1, ty)) Cliff(solid, kerb, new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0));
                    if (Void(info, tx + 1, ty)) Cliff(solid, kerb, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1));
                }

            if (info.Rocks != null)
                for (int i = 0; i < info.Rocks.Length; i++)
                {
                    Vector3 r = info.Rocks[i];
                    float rad = Mathf.Max(0.3f, r.z);
                    Vector3 p = new Vector3(r.x, 0f, r.y);
                    marks.Color(Pal.Shadow).Disc(new Vector3(p.x, 0.012f, p.z), rad * 1.15f, 20);
                    if (rockPic != null) continue; // Rickard's mossy boulder picture stands here (RockPictures below)
                    solid.Color(Pal.Rock).Boulder(p + new Vector3(0f, rad * 0.45f, 0f), new Vector3(rad * 0.95f, rad * 0.75f, rad * 1.05f), 77 + i);
                    solid.Color(Pal.RockLight).Boulder(p + new Vector3(rad * 0.45f, rad * 0.25f, rad * 0.5f), new Vector3(rad * 0.4f, rad * 0.35f, rad * 0.4f), 91 + i);
                    solid.Color(Pal.StoneDark).Boulder(p + new Vector3(-rad * 0.5f, rad * 0.15f, -rad * 0.4f), new Vector3(rad * 0.3f, rad * 0.25f, rad * 0.3f), 103 + i);
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
            if (kit) WallKit(root, info, pillarM, beamM);
            if (bushM != null) BushModels(root, info, bushM);
            Mesh edgeM = info.Island ? null : Props.Mesh("bam_boundary_wall");
            if (edgeM != null) BoundaryWall(root, info, edgeM);
            if (rockPic != null && info.Rocks != null) RockPictures(root, info, rockPic);
            Add(root, "Marks", marks.ToMesh("Arena Marks"), MarksMaterial(), false);
            if (info.Island) Island.Build(root, info);
            return root;
        }

        // Rickard's Meshy wall kit, placed like the old game (View3DWallModels.cs, "pillar + beam"): a pillar on every node of the
        // wall grid (end, corner, tee, single tile) and at even steps along a long run, the straight piece stretched between two
        // pillars. Each model is merged into one mesh, so the walls cost two draw calls.
        const float KitPillarXZ = 0.9f, KitPillarY = 0.85f, KitWallH = 0.8f, KitWallT = 1.15f, KitWallLen = 1.9f;

        static void WallKit(GameObject root, ArenaInfo info, Mesh pillarM, Mesh beamM)
        {
            int w = info.Width, h = info.Height;
            var pillars = new System.Collections.Generic.List<CombineInstance>();
            var beams = new System.Collections.Generic.List<CombineInstance>();
            bool At(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && info.Tile(x, y) == TWall && !InRock(info, info.TileCenter(x, y));
            // bit 1 = next row, 2 = +x, 4 = previous row, 8 = -x; a node is anything but a straight run's middle
            bool Node(int x, int y)
            {
                int m = (At(x, y + 1) ? 1 : 0) | (At(x + 1, y) ? 2 : 0) | (At(x, y - 1) ? 4 : 0) | (At(x - 1, y) ? 8 : 0);
                return m != 5 && m != 10;
            }
            // tile coords (row y grows toward the bottom of the screen) to the world
            Vector3 W(float x, float y) => new Vector3(x, 0f, h - y);
            void Pillar(float x, float y) => pillars.Add(new CombineInstance { mesh = pillarM, transform = Matrix4x4.TRS(W(x, y), Quaternion.identity, new Vector3(KitPillarXZ, KitPillarY, KitPillarXZ)) });
            void Beam(float x, float y, bool alongRows, float len) => beams.Add(new CombineInstance
            {
                mesh = beamM,
                transform = Matrix4x4.TRS(W(x, y), Quaternion.Euler(0f, alongRows ? 90f : 0f, 0f), new Vector3(len / KitWallLen, KitWallH, KitWallT)),
            });
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!At(x, y) || !Node(x, y)) continue;
                    Pillar(x + 0.5f, y + 0.5f);
                    // runs go +x and +y from a node; the node at the other end finds them from its own side
                    for (int dir = 0; dir < 2; dir++)
                    {
                        int dx = dir == 0 ? 1 : 0, dy = dir == 0 ? 0 : 1;
                        if (!At(x + dx, y + dy)) continue;
                        int len = 1;
                        while (!Node(x + dx * len, y + dy * len)) len++;
                        int k = Mathf.Max(1, Mathf.RoundToInt(len / 2f));
                        float g = (float)len / k;
                        for (int i = 0; i < k; i++)
                        {
                            float c = (i + 0.5f) * g;
                            Beam(x + 0.5f + dx * c, y + 0.5f + dy * c, dir == 1, g + 0.04f);
                            if (i > 0) Pillar(x + 0.5f + dx * i * g, y + 0.5f + dy * i * g);
                        }
                    }
                }
            KitPiece(root, "Wall pillars", pillars, "wallkit_pillar");
            KitPiece(root, "Wall pieces", beams, "wallkit_straight");
        }

        // Rickard's Meshy boundary wall (bam_boundary_wall: 1.9 long, 0.3 high, 0.66 deep) laid end to end round the outside of the
        // floor, each side stretched a little so the pieces fit exactly (old game: ArenaBoundaryWall). Only looks.
        const float EdgeScale = 1.3f;

        static void BoundaryWall(GameObject root, ArenaInfo info, Mesh mesh)
        {
            int w = info.Width, h = info.Height;
            var parts = new System.Collections.Generic.List<CombineInstance>();
            float mw = mesh.bounds.size.x, md = mesh.bounds.size.z * EdgeScale, off = md / 2f - 0.02f;
            void Side(Vector3 from, Vector3 dir, float len, float yaw)
            {
                int n = Mathf.Max(1, Mathf.CeilToInt(len / (mw * EdgeScale)));
                float seg = len / n, sx = seg / mw;
                for (int i = 0; i < n; i++)
                    parts.Add(new CombineInstance
                    {
                        mesh = mesh,
                        transform = Matrix4x4.TRS(from + dir * (seg * (i + 0.5f)) + Vector3.down * 0.04f, Quaternion.Euler(0f, yaw, 0f), new Vector3(sx, EdgeScale, EdgeScale)),
                    });
            }
            Side(new Vector3(-off, 0f, -off), Vector3.right, w + 2f * off, 180f);
            Side(new Vector3(-off, 0f, h + off), Vector3.right, w + 2f * off, 0f);
            Side(new Vector3(-off, 0f, 0f), Vector3.forward, h, 90f);
            Side(new Vector3(w + off, 0f, 0f), Vector3.forward, h, -90f);
            KitPiece(root, "Boundary wall", parts, "bam_boundary_wall");
        }

        // Rickard's Meshy bush (bam_bush, made for the BAM arena): one copy on every grass tile of a 1.5-tile grid, turned and sized a
        // little differently so a grass area reads as one thick hedge. Merged into one mesh (one draw call).
        static void BushModels(GameObject root, ArenaInfo info, Mesh bush)
        {
            var parts = new System.Collections.Generic.List<CombineInstance>();
            var rnd = new System.Random(4242);
            float size = Mathf.Max(bush.bounds.size.x, bush.bounds.size.z);
            const float Step = 1.5f;
            for (float y = 0.75f; y < info.Height; y += Step)
                for (float x = 0.75f; x < info.Width; x += Step)
                {
                    int tx = (int)x, ty = (int)y;
                    if (info.Tile(tx, ty) != TGrass) continue;
                    float sc = (1.9f + (float)rnd.NextDouble() * 0.35f) / size;
                    float jx = ((float)rnd.NextDouble() - 0.5f) * 0.25f, jz = ((float)rnd.NextDouble() - 0.5f) * 0.25f;
                    parts.Add(new CombineInstance
                    {
                        mesh = bush,
                        transform = Matrix4x4.TRS(new Vector3(x + jx, 0f, info.Height - y + jz), Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f),
                            new Vector3(sc, sc * (0.9f + (float)rnd.NextDouble() * 0.2f), sc)),
                    });
                }
            KitPiece(root, "Bushes", parts, "bam_bush", false); // receive shadows but cast none: hundreds of leaves in the shadow pass cost more than they show
        }

        // The middle boulders: Rickard's painted mossy boulder standing upright at its foot line, as in the old game (RuinBill): a
        // hero behind it is hidden, one in front is drawn over it. Sized like the old v1.2 boulder (about 3 tiles wide).
        static void RockPictures(GameObject root, ArenaInfo info, Texture2D tex)
        {
            const float CY = 0.5724f; // cos of the camera's elevation: a vertical edge shows this much shorter on screen
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var t = new System.Collections.Generic.List<int>();
            float ar = (float)tex.width / tex.height;
            for (int i = 0; i < info.Rocks.Length; i++)
            {
                Vector3 r = info.Rocks[i];
                float rad = Mathf.Max(0.3f, r.z), sh = rad * 2.6f, sw = sh * ar;
                var foot = new Vector3(r.x, 0.02f, r.y - rad * 1.35f);
                Vector3 up = new Vector3(0f, sh / CY, 0f), half = new Vector3(sw * 0.5f, 0f, 0f);
                int b = v.Count;
                v.Add(foot - half); v.Add(foot - half + up); v.Add(foot + half + up); v.Add(foot + half);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, 1f)); uv.Add(new Vector2(1f, 1f)); uv.Add(new Vector2(1f, 0f));
                t.Add(b); t.Add(b + 1); t.Add(b + 2); t.Add(b); t.Add(b + 2); t.Add(b + 3);
            }
            var mesh = new Mesh { name = "Arena rocks" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = new Vector3(0f, 0.82f, -0.5724f); // lit like it faces the camera
            mesh.normals = n;
            mesh.RecalculateBounds();
            var mat = ArtMaterials.Instance(ArtMaterials.LitClip);
            mat.SetTexture(ArtMaterials.BaseMap, tex);
            mat.mainTexture = tex;
            Add(root, "Rocks", mesh, mat, true);
        }

        // one quad over the whole floor, unlit like the old game's ground (the picture already carries its light and shadows)
        static void PaintedGround(GameObject root, Texture2D tex, int w, int h)
        {
            var mesh = new Mesh { name = "Arena ground" };
            mesh.vertices = new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, h), new Vector3(w, 0f, h), new Vector3(w, 0f, 0f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            var mat = ArtMaterials.Instance(ArtMaterials.Unlit);
            mat.SetTexture(ArtMaterials.BaseMap, tex);
            mat.mainTexture = tex;
            Add(root, "Ground", mesh, mat, false);
        }

        static void KitPiece(GameObject root, string name, System.Collections.Generic.List<CombineInstance> parts, string tex, bool cast = true)
        {
            if (parts.Count == 0) return;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.UploadMeshData(true);
            var mat = ArtMaterials.Instance(ArtMaterials.Lit);
            Texture2D t = Props.Texture(tex);
            if (t != null) { mat.SetTexture(ArtMaterials.BaseMap, t); mat.mainTexture = t; }
            Add(root, name, mesh, mat, true, cast);
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

        internal static GameObject Add(GameObject root, string name, Mesh mesh, Material mat, bool lit, bool cast = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.receiveShadows = lit;
            r.shadowCastingMode = lit && cast && ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (lit && cast) ArtKit.RegisterCaster(r);
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
