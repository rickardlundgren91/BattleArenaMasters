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
        static bool skirted; // Rickard's cliff picture hangs on the edge: the old code-drawn cliff faces would sit in the same plane

        public static GameObject Build(ArenaInfo info)
        {
            var root = new GameObject("Arena");
            owned = root.AddComponent<ArenaOwned>();
            var solid = new MeshBuilder();
            var marks = new MeshBuilder();
            Layer ground = Load("T_BAM_Ground_Cobble", 6f), road = Load("T_BAM_Ground_Road2", 4f), grass = Load("T_BAM_Ground_Grass", 6f),
                wall = Load("T_BAM_Block", 2f), kerb = Load("T_BAM_Kerb", 1f), water = WaterLayer(), plinth = Load("T_BAM_Plinth", 3f);
            int w = info.Width, h = info.Height;
            float laneX = w * 0.5f;
            var rnd = new System.Random(1234);
            bool moat = water.T != null;
            waterY = moat ? -0.55f : -CliffDepth;
            // Rickard's cliff (O-055): the map stands on a rock cliff, so the water lies a cliff's height down (the bottom quarter of
            // the screen under our base was flat water with repeated rings, 3 Nov videos)
            Texture2D cliffPic = info.Island ? null : Resources.Load<Texture2D>("BAMArt/Arena/tx_arena_cliff");
            if (cliffPic != null) waterY = -CliffSkirtH;
            skirted = cliffPic != null;

            // water (or an abyss) all around and below, so the camera never sees the empty background at the map edge;
            // the island floats over its own cloud sea instead
            if (!info.Island) Pick(water, solid, Pal.Void).Face(new Vector3(-40f, waterY, -40f), new Vector3(-40f, waterY, h + 40f), new Vector3(w + 40f, waterY, h + 40f), new Vector3(w + 40f, waterY, -40f));

            // the BAM arena's own ground picture (grass, cobbled road between the towers, worn earth along it, tall grass under the
            // bushes), baked offline exactly like the old game painted it (bam-work/art/ground/bake.py); only for that map's size
            Texture2D painted = !info.Island && w == 15 && h == 35 ? Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_ArenaGround_v1_2") : null;
            if (painted != null) PaintedGround(root, painted, w, h);
            if (painted != null) TowerRunes.Build(root);
            if (painted != null) RoadRunes.Build(root);
            Mesh pillarM = Props.Mesh("wallkit_pillar"), beamM = Props.Mesh("wallkit_straight");
            bool kit = pillarM != null && beamM != null && !info.Island;
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
                        // Rickard's Meshy wall kit stands here (WallKit below); only the contact shadow on the ground, which the BAM
                        // arena's painted ground already carries, soft (bam-work/art/karta/detaljer/bake_skuggor.py, 2026-11-02)
                        if (painted == null) marks.Color(Pal.ShadowSoft).Face(new Vector3(x0 - 0.2f, 0.012f, z0 - 0.2f), new Vector3(x0 - 0.2f, 0.012f, z1 + 0.2f), new Vector3(x1 + 0.2f, 0.012f, z1 + 0.2f), new Vector3(x1 + 0.2f, 0.012f, z0 - 0.2f));
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
                        if (code == TGrass) { } // the old game's leaves stand here (LeafBushes below)
                        else if (ground.T == null && dLane >= 2.1f && rnd.NextDouble() < 0.18) Pebble(solid, c, rnd);
                    }

                    // cliffs where the map ends or a void tile begins
                    if (Void(info, tx, ty - 1)) Cliff(solid, kerb, new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), ty == 0);
                    if (Void(info, tx, ty + 1)) Cliff(solid, kerb, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), ty == h - 1);
                    if (Void(info, tx - 1, ty)) Cliff(solid, kerb, new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0), tx == 0);
                    if (Void(info, tx + 1, ty)) Cliff(solid, kerb, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), tx == w - 1);
                }

            if (info.Rocks != null)
                for (int i = 0; i < info.Rocks.Length; i++)
                {
                    Vector3 r = info.Rocks[i];
                    float rad = Mathf.Max(0.3f, r.z);
                    Vector3 p = new Vector3(r.x, 0f, r.y);
                    // Rickard's mossy boulder picture stands here (RockPictures below) and carries its own shadow: the round code shadow
                    // under it showed as a dark circle round the stone (Rickard 2026-10-31 "stenarna har mörka cirklar runt sig")
                    if (rockPic != null) continue;
                    marks.Color(Pal.Shadow).Disc(new Vector3(p.x, 0.012f, p.z), rad * 1.15f, 20);
                    solid.Color(Pal.Rock).Boulder(p + new Vector3(0f, rad * 0.45f, 0f), new Vector3(rad * 0.95f, rad * 0.75f, rad * 1.05f), 77 + i);
                    solid.Color(Pal.RockLight).Boulder(p + new Vector3(rad * 0.45f, rad * 0.25f, rad * 0.5f), new Vector3(rad * 0.4f, rad * 0.35f, rad * 0.4f), 91 + i);
                    solid.Color(Pal.StoneDark).Boulder(p + new Vector3(-rad * 0.5f, rad * 0.15f, -rad * 0.4f), new Vector3(rad * 0.3f, rad * 0.25f, rad * 0.3f), 103 + i);
                }

            // tower plinth with the team ring: only under the old code tower; Rickard's Tripo tower (bam_tower_grass) stands on its
            // own stone foot straight on the plaza (Rickard 2026-11-02 "bort med blåa cirkeln")
            if (info.Towers != null && Props.Mesh("bam_tower_grass") == null)
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
            if (!info.Island) LeafBushes(root, info);
            // no stone wall round the floor any more (Rickard 2026-11-04: "vi kanske inte behöver den där väggen"): the cliff and its
            // low kerb mark the edge, and the rules never let anyone off the map, so the wall was only looks
            const bool edgeWall = false;
            Mesh edgeM = info.Island || !edgeWall ? null : Props.Mesh("bam_boundary_wall");
            if (edgeM != null) BoundaryWall(root, info, edgeM);
            if (cliffPic != null) CliffSkirt(root, info, cliffPic, edgeM != null ? edgeM.bounds.size.z * EdgeScale - 0.07f : 0f);
            if (rockPic != null && info.Rocks != null) RockPictures(root, info, rockPic);
            Add(root, "Marks", marks.ToMesh("Arena Marks"), MarksMaterial(), false);
            if (info.Island) Island.Build(root, info);
            // the old game had no sun: its arena was lit by the warm ambient alone, so walls, rocks and bushes keep their dark greens
            // (Rickard 2026-10-26, side by side: "det skiljer mycket"). The sun only lights the units, not this layer.
            SetLayer(root.transform, ArtKit.ArenaLayer);
            owned = null;
            return root;
        }

        // Rickard's wall kit (Tripo ruin wall + pillar, 2026-10-31), placed like the old game (View3DWallModels.cs, "pillar + beam"):
        // a pillar on every node of the wall grid (end, corner, tee, single tile) and the straight piece repeated about every two
        // tiles between them. No pillars inside a run, so it reads as one long wall (Rickard's picture), slimmer pillars and a
        // thicker wall so its crenellated top shows from above. Each model is merged into one mesh, so the walls cost two draw calls.
        // pillar top is only 0.64 wide in the model, so 1.6 makes it ~1 tile, clearly wider and taller than the 0.77-tile wall like his reference picture (Rickard 2026-11-01: "hörnbitarna är pyttesmå")
        const float KitPillarXZ = 1.6f, KitPillarY = 1.0f, KitWallH = 0.8f, KitWallT = 1.4f, KitWallLen = 1.9f;
        // tiles one straight piece covers: Rickard's 2026-11-01 Tripo wall is 8 blocks long, so ~7 tiles keeps its blocks square from above
        const float KitSegLen = 7f;
        // the straight piece is 8 blocks; a run too short for all 8 gets a piece cut down to fewer whole blocks instead of 8 squeezed
        // ones (Rickard 2026-11-01: the 3-tile wall under the tower showed thin slats). The cut ends sit inside the pillars.
        const int KitBlocks = 8;
        static readonly System.Collections.Generic.Dictionary<int, Mesh> cutBeams = new System.Collections.Generic.Dictionary<int, Mesh>();

        static Mesh CutBeam(Mesh full, int blocks)
        {
            if (blocks >= KitBlocks) return full;
            if (cutBeams.TryGetValue(blocks, out Mesh have) && have != null) return have;
            Vector3[] v = full.vertices; Vector3[] n = full.normals; Vector2[] uv = full.uv; Color[] col = full.colors;
            int[] tri = full.triangles;
            float x0 = full.bounds.min.x, bw = full.bounds.size.x / KitBlocks, x1 = x0 + blocks * bw, shift = x0 + blocks * bw * 0.5f;
            var map = new int[v.Length];
            for (int i = 0; i < map.Length; i++) map[i] = -1;
            var nv = new System.Collections.Generic.List<Vector3>(); var nn = new System.Collections.Generic.List<Vector3>();
            var nuv = new System.Collections.Generic.List<Vector2>(); var ncol = new System.Collections.Generic.List<Color>();
            var nt = new System.Collections.Generic.List<int>();
            for (int t = 0; t < tri.Length; t += 3)
            {
                float cx = (v[tri[t]].x + v[tri[t + 1]].x + v[tri[t + 2]].x) / 3f;
                if (cx < x0 || cx > x1) continue;
                for (int j = 0; j < 3; j++)
                {
                    int a = tri[t + j];
                    if (map[a] < 0)
                    {
                        map[a] = nv.Count;
                        nv.Add(new Vector3(v[a].x - shift, v[a].y, v[a].z));
                        if (n.Length == v.Length) nn.Add(n[a]);
                        if (uv.Length == v.Length) nuv.Add(uv[a]);
                        if (col.Length == v.Length) ncol.Add(col[a]);
                    }
                    nt.Add(map[a]);
                }
            }
            var m = new Mesh { name = full.name + "_" + blocks };
            m.SetVertices(nv);
            if (nn.Count == nv.Count) m.SetNormals(nn);
            if (nuv.Count == nv.Count) m.SetUVs(0, nuv);
            if (ncol.Count == nv.Count) m.SetColors(ncol);
            m.SetTriangles(nt, 0);
            if (nn.Count != nv.Count) m.RecalculateNormals();
            m.RecalculateBounds();
            cutBeams[blocks] = m;
            return m;
        }

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
            void Beam(float x, float y, bool alongRows, float len, int blocks) => beams.Add(new CombineInstance
            {
                mesh = CutBeam(beamM, blocks),
                transform = Matrix4x4.TRS(W(x, y), Quaternion.Euler(0f, alongRows ? 90f : 0f, 0f), new Vector3(len / (KitWallLen * blocks / KitBlocks), KitWallH, KitWallT)),
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
                        int k = Mathf.Max(1, Mathf.RoundToInt(len / KitSegLen));
                        float g = (float)len / k;
                        int blocks = Mathf.Clamp(Mathf.RoundToInt(g * KitBlocks / KitSegLen), 1, KitBlocks);
                        for (int i = 0; i < k; i++)
                        {
                            float c = (i + 0.5f) * g;
                            Beam(x + 0.5f + dx * c, y + 0.5f + dy * c, dir == 1, g + 0.04f, blocks);
                        }
                    }
                }
            KitPiece(root, "Wall pillars", pillars, "wallkit_pillar");
            KitPiece(root, "Wall pieces", beams, "wallkit_straight");
        }

        // the new water (O-057, 2026-11-04, painted dark teal, no rings) wins over the old one
        static Layer WaterLayer()
        {
            var l = Load("T_BAM_Water_v2", 6f);
            return l.T != null ? l : Load("T_BAM_Water", 6f);
        }

        // Rickard's cliff picture (tx_arena_cliff, O-055, 2026-11-04): grass along the top, plain rock under it, seamless left to right,
        // 1024 x 872. Hung round the outside of the boundary wall from the floor down to the water, one picture high, CliffSkirtW
        // wide per repeat, so the camera sees a cliff under the base instead of open water. Only looks (no fog, glow or particles:
        // the map's no-go rules). The faces look outward; the camera behind the bottom base sees the south one, the online top team's
        // turned camera the north one.
        const float CliffSkirtW = 8f, CliffSkirtH = CliffSkirtW * 872f / 1024f;

        static void CliffSkirt(GameObject root, ArenaInfo info, Texture2D pic, float rim)
        {
            float w = info.Width, h = info.Height;
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var n = new System.Collections.Generic.List<Vector3>();
            var tri = new System.Collections.Generic.List<int>();
            // left and right as seen from outside, looking at the face
            void Face(Vector3 left, Vector3 right)
            {
                Vector3 along = right - left; along.y = 0f;
                float len = along.magnitude;
                Vector3 outN = Vector3.Cross(Vector3.up, along / len); // outward, for the left-to-right order seen from outside
                int i0 = v.Count;
                v.Add(left); v.Add(right); v.Add(right + Vector3.down * CliffSkirtH); v.Add(left + Vector3.down * CliffSkirtH);
                float u0 = (left.x * 0.37f + left.z * 0.61f) / CliffSkirtW, u1 = u0 + len / CliffSkirtW; // each side starts elsewhere in the picture
                uv.Add(new Vector2(u0, 1f)); uv.Add(new Vector2(u1, 1f)); uv.Add(new Vector2(u1, 0f)); uv.Add(new Vector2(u0, 0f));
                for (int k = 0; k < 4; k++) n.Add(outN);
                tri.Add(i0); tri.Add(i0 + 1); tri.Add(i0 + 2); tri.Add(i0); tri.Add(i0 + 2); tri.Add(i0 + 3);
            }
            float y = 0f;
            Face(new Vector3(-rim, y, -rim), new Vector3(w + rim, y, -rim));         // south, seen from -z
            Face(new Vector3(w + rim, y, h + rim), new Vector3(-rim, y, h + rim));   // north, seen from +z
            Face(new Vector3(-rim, y, h + rim), new Vector3(-rim, y, -rim));         // west, seen from -x
            Face(new Vector3(w + rim, y, -rim), new Vector3(w + rim, y, h + rim));   // east, seen from +x
            var mesh = new Mesh { name = "Cliff skirt" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetNormals(n); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            var mat = ArtMaterials.Instance(ArtMaterials.LitClip); // the grass tips along the top are cut out by the picture's alpha
            mat.SetTexture(ArtMaterials.BaseMap, pic);
            mat.mainTexture = pic;
            Add(root, "Cliff skirt", mesh, mat, false, false);
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

        // The old game's bush (View3DMapArena.ArenaBush, Rickard 2026-10-17: "the leaves built in code looked good, so they are the
        // bush"), drawn in the painted ground grass's style (Rickard 2026-10-31: "stilen gräset är gjort", same greens): on every
        // grass tile five tufts of 5-7 pointed blades fanned from one root, leaning away from the camera so on screen they rise long
        // and upward; tiles on an area's edge lean their outer blades outward, so the outline is ragged. Two greens, each blade dark
        // at the root and light at the tip (a bilinear 4x2 ramp texture, as the old palette). About as many triangles as the old
        // 24 leaves per tile. One mesh per tile,
        // one shared material (SRP batcher), so BushField can fade the tiles round the player and lean them away from heroes.
        static Texture2D leafRamp;
        static Material leafMat, leafFaded;

        static void LeafBushes(GameObject root, ArenaInfo info)
        {
            int w = info.Width, h = info.Height;
            var v = new System.Collections.Generic.List<Vector3>(16384);
            var n = new System.Collections.Generic.List<Vector3>(16384);
            var uv = new System.Collections.Generic.List<Vector2>(16384);
            var t = new System.Collections.Generic.List<int>(24576);
            bool G(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && info.Tile(x, y) == TGrass;
            float away = info.Turned ? -1f : 1f; // +z leads away from the camera, -z when it looks from the other end (online, top team)
            GameObject holder = null;
            // PERF-01: 124 tiles drawn one by one cost about 120 draw calls. Each 3 x 2 block of tiles is also merged into one
            // still mesh that is drawn while no tile in it fades or leans; BushField swaps a block to its own tiles only then.
            var chunkOf = new System.Collections.Generic.Dictionary<int, int>();
            var chunkV = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
            var chunkN = new System.Collections.Generic.List<System.Collections.Generic.List<Vector3>>();
            var chunkUv = new System.Collections.Generic.List<System.Collections.Generic.List<Vector2>>();
            var chunkT = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
            var tileChunk = new System.Collections.Generic.List<int>(128);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!G(x, y)) continue;
                    v.Clear(); n.Clear(); uv.Clear(); t.Clear();
                    var mid0 = info.TileCenter(x, y); // each tile is its own piece round its middle, so it can fade and lean alone
                    // the old map's rows grew with z; here row y lies at z = h - 1 - y, so the old row y - 1 (toward -z) is y + 1
                    float ex = (G(x - 1, y) ? 0 : -1) + (G(x + 1, y) ? 0 : 1), ez = (G(x, y + 1) ? 0 : -1) + (G(x, y - 1) ? 0 : 1);
                    // tufts like the painted ground grass (T_BAM_Ground_Grass): five clumps per tile (four jittered quarters and one in
                    // the middle), each a fan of 5-7 blades from one root, so dark gaps open between the clumps
                    for (int q = 0; q < 5; q++)
                    {
                        int qi = q == 4 ? 2 : q & 1, qj = q == 4 ? 2 : q >> 1;
                        int hx = x * 3 + qi, hy = y * 3 + qj;
                        float u = q == 4 ? 0.35f + Hash(hx, hy, 32) * 0.3f : (qi + 0.2f + Hash(hx, hy, 32) * 0.6f) / 2f;
                        float vv = q == 4 ? 0.35f + Hash(hx, hy, 33) * 0.3f : (qj + 0.2f + Hash(hx, hy, 33) * 0.6f) / 2f;
                        var foot = new Vector3(x + u, 0f, h - 1 - y + vv) - mid0;
                        int row = Hash(hx, hy, 37) < 0.35f ? 1 : 0;
                        int blades = 5 + (int)(Hash(hx, hy, 40) * 2.99f);
                        float a0 = Hash(hx, hy, 34) * 6.283f, tall = 0.8f + Hash(hx, hy, 35) * 0.35f;
                        float edgeW = Mathf.Clamp01(Mathf.Max(ex * (u - 0.5f), ez * (vv - 0.5f)) * 2.2f); // near an open side: lean out
                        for (int b = 0; b < blades; b++)
                        {
                            int bx = hx * 7 + b;
                            float a = a0 + b * 6.283f / blades + (Hash(bx, hy, 41) - 0.5f) * 0.6f;
                            // fanned round the root, but leaning away from the camera like the old leaves, and out at an open side
                            // (online on the top team the camera looks from the north, COORDS-01: away from it is -z there)
                            var dir = (new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a) + 0.9f * away) + new Vector3(ex, 0f, ez) * (edgeW * 1.2f)).normalized;
                            float len = tall * (0.65f + Hash(bx, hy, 42) * 0.45f), wd = 0.075f + Hash(bx, hy, 43) * 0.03f, up = 0.55f + Hash(bx, hy, 44) * 0.35f;
                            Blade(v, n, uv, t, foot + dir * 0.04f, dir, len, wd, up, row, Hash(bx, hy, 45) < 0.7f, away);
                        }
                    }
                    if (v.Count == 0) continue;
                    var mesh = new Mesh { name = "Bush tile" };
                    mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(true);
                    if (holder == null) { holder = new GameObject("Bushes"); holder.transform.SetParent(root.transform, false); }
                    EnsureLeafMats();
                    var go = Add(holder, "Bush", mesh, leafMat, true, false); // receive shadows, cast none (thousands of leaves in the shadow pass)
                    go.isStatic = false; // it leans
                    go.transform.localPosition = mid0;

                    int key = (y / 2) * 64 + x / 3;
                    if (!chunkOf.TryGetValue(key, out int c))
                    {
                        c = chunkV.Count; chunkOf[key] = c;
                        chunkV.Add(new System.Collections.Generic.List<Vector3>(2048)); chunkN.Add(new System.Collections.Generic.List<Vector3>(2048));
                        chunkUv.Add(new System.Collections.Generic.List<Vector2>(2048)); chunkT.Add(new System.Collections.Generic.List<int>(3072));
                    }
                    int b0 = chunkV[c].Count;
                    for (int i = 0; i < v.Count; i++) chunkV[c].Add(v[i] + mid0); // the tile's own leaves, where the tile stands
                    chunkN[c].AddRange(n); chunkUv[c].AddRange(uv);
                    for (int i = 0; i < t.Count; i++) chunkT[c].Add(t[i] + b0);
                    tileChunk.Add(c);
                }
            if (holder == null) return;
            var chunkHolder = new GameObject("Bush blocks");
            chunkHolder.transform.SetParent(root.transform, false);
            var blocks = new Renderer[chunkV.Count];
            for (int c = 0; c < chunkV.Count; c++)
            {
                var mesh = new Mesh { name = "Bush block" };
                mesh.SetVertices(chunkV[c]); mesh.SetNormals(chunkN[c]); mesh.SetUVs(0, chunkUv[c]); mesh.SetTriangles(chunkT[c], 0);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);
                blocks[c] = Add(chunkHolder, "Bush block", mesh, leafMat, true, false).GetComponent<Renderer>();
            }
            holder.AddComponent<BushField>().Init(holder.transform, leafMat, leafFaded, blocks, tileChunk);
        }

        static void EnsureLeafMats()
        {
            if (leafMat == null)
            {
                leafRamp = new Texture2D(4, 2, TextureFormat.RGBA32, false, false)
                {
                    name = "BAM leaf ramp", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
                };
                leafRamp.SetPixels32(new[]
                {
                    new Color32(0x1c, 0x4a, 0x2c, 255), new Color32(0x2c, 0x7a, 0x44, 255), new Color32(0x4a, 0xa4, 0x5a, 255), new Color32(0x9b, 0xd8, 0x73, 255), // leaves
                    new Color32(0x18, 0x44, 0x38, 255), new Color32(0x2a, 0x76, 0x60, 255), new Color32(0x48, 0xa0, 0x7c, 255), new Color32(0xa6, 0xe0, 0xa0, 255), // a cooler green
                });
                leafRamp.Apply(false, true);
                leafMat = ArtMaterials.Instance(ArtMaterials.Lit);
                leafMat.name = "BAM leaves";
                leafMat.SetTexture(ArtMaterials.BaseMap, leafRamp);
                leafMat.mainTexture = leafRamp;
                if (leafMat.HasProperty(CullId)) leafMat.SetFloat(CullId, 0f); // both sides, as the old game
                Matte(leafMat); // the old bush was URP Lit with smoothness 0: no sheen, or the leaves read mint instead of green
                // the old game's see-through bush round the player: the same leaves at 40% (View3D.Faded)
                leafFaded = ArtMaterials.Instance(ArtMaterials.LitFade);
                leafFaded.name = "BAM leaves (faded)";
                leafFaded.SetTexture(ArtMaterials.BaseMap, leafRamp);
                leafFaded.mainTexture = leafRamp;
                leafFaded.SetColor(ArtMaterials.BaseColor, new Color(1f, 1f, 1f, 0.4f));
                Matte(leafFaded);
                if (leafFaded.HasProperty(CullId)) leafFaded.SetFloat(CullId, 0f);
            }
        }

        static readonly int CullId = Shader.PropertyToID("_Cull");

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }
        static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness"), SpecColorId = Shader.PropertyToID("_SpecColor");

        static void Matte(Material m)
        {
            if (m.HasProperty(SmoothnessId)) m.SetFloat(SmoothnessId, 0f);
            if (m.HasProperty(SpecColorId)) m.SetColor(SpecColorId, Color.black);
        }

        // one grass blade from its root p, rising and bending out along dir: four rings of two points, widest a third of the way up.
        // Painted in flat tone steps as the ground grass: dark root, then mid, then a light tip on most blades
        static void Blade(System.Collections.Generic.List<Vector3> v, System.Collections.Generic.List<Vector3> n, System.Collections.Generic.List<Vector2> uv,
            System.Collections.Generic.List<int> t, Vector3 p, Vector3 dir, float len, float wd, float up, int row, bool light, float away)
        {
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            Vector3 ctrl = p + Vector3.up * (len * up) + dir * (len * 0.2f), end = p + Vector3.up * (len * up * 0.85f) + dir * (len * 0.65f);
            Vector3 Pt(float k) => (1 - k) * (1 - k) * p + 2 * (1 - k) * k * ctrl + k * k * end;
            int pl = -1, pr = -1;
            for (int i = 0; i < 4; i++)
            {
                float k = BladeT[i];
                Vector3 c = Pt(k), tan = (Pt(Mathf.Min(1f, k + 0.05f)) - Pt(Mathf.Max(0f, k - 0.05f))).normalized;
                Vector3 nr = Vector3.Cross(side, tan).normalized;
                if (nr.z * away > 0f) nr = -nr;             // the face toward the camera
                nr = (nr + Vector3.up * 0.6f).normalized;   // and a little up, so the light reads soft
                int cell = light ? BladeLight[i] : BladeDim[i];
                var cuv = new Vector2((cell + 0.5f) / 4f, (row + 0.5f) / 2f);
                int l = v.Count;
                v.Add(c - side * (wd * BladeW[i])); n.Add(nr); uv.Add(cuv);
                v.Add(c + side * (wd * BladeW[i])); n.Add(nr); uv.Add(cuv);
                if (pl >= 0) { t.Add(pl); t.Add(pr); t.Add(l + 1); t.Add(pl); t.Add(l + 1); t.Add(l); }
                pl = l; pr = l + 1;
            }
        }

        static readonly float[] BladeT = { 0f, 0.4f, 0.75f, 1f }, BladeW = { 0.5f, 1f, 0.6f, 0f };
        static readonly int[] BladeLight = { 0, 2, 3, 3 }, BladeDim = { 0, 1, 2, 2 };

        // the old game's hash (View3DMapStyle.Hash): the same leaves in the same places
        static float Hash(int x, int y, int k)
        {
            uint hh = (uint)(x * 374761393 + y * 668265263 + k * 1274126177);
            hh = (hh ^ (hh >> 13)) * 1274126177; hh ^= hh >> 16;
            return (hh & 0xffffff) / (float)0x1000000;
        }

        // The middle boulders: Rickard's painted mossy boulder, as in the old game (RuinBill): a hero behind it is hidden, one in front
        // is drawn over it. About 2.4 tiles wide, the rules' boulder plus a hero's radius. "Behind" means north of the boulder's
        // middle: the picture's lower part lies flat on the ground from the foot line to the middle and only the rest stands upright
        // there, the same picture on screen (the camera is orthographic). Standing upright at the foot line, it hid a hero beside the
        // boulder's southern half (Rickard 2026-10-29: the hero stood "inside" the rock).
        static void RockPictures(GameObject root, ArenaInfo info, Texture2D tex)
        {
            const float CY = 0.5724f; // cos of the camera's elevation: a vertical edge shows this much shorter on screen
            const float SY = 0.82f;   // sin of the camera's elevation: ground depth shows this much shorter on screen
            var v = new System.Collections.Generic.List<Vector3>();
            var uv = new System.Collections.Generic.List<Vector2>();
            var t = new System.Collections.Generic.List<int>();
            float ar = (float)tex.width / tex.height;
            for (int i = 0; i < info.Rocks.Length; i++)
            {
                Vector3 r = info.Rocks[i];
                // sized so the painted rock ends where the rules stop a hero (Rickard 2026-10-26: at 2.6 a hero walking into its side went in
                // and vanished behind it): the picture's rock fills ~95% of its width, the rules' edge is RockR + the hero's radius
                float rad = Mathf.Max(0.3f, r.z), sh = rad * 2.05f, sw = sh * ar;
                // the foot on the boulder's side nearest the camera: turned online (COORDS-01) that is the north side, and the
                // picture faces the other way (half flipped keeps the winding toward the camera and the picture unmirrored)
                float near = info.Turned ? -1f : 1f, depth = rad * 1.35f;
                var foot = new Vector3(r.x, 0.02f, r.y - near * depth);
                var mid = new Vector3(r.x, 0.02f, r.y);
                // the flat part shows depth x SY on screen; the upright part the rest of the picture's sh
                float split = Mathf.Min(0.95f, depth * SY / sh);
                Vector3 up = new Vector3(0f, (sh - split * sh) / CY, 0f), half = new Vector3(near * sw * 0.5f, 0f, 0f);
                int b = v.Count;
                v.Add(foot - half); v.Add(mid - half); v.Add(mid - half + up); v.Add(foot + half); v.Add(mid + half); v.Add(mid + half + up);
                uv.Add(new Vector2(0f, 0f)); uv.Add(new Vector2(0f, split)); uv.Add(new Vector2(0f, 1f));
                uv.Add(new Vector2(1f, 0f)); uv.Add(new Vector2(1f, split)); uv.Add(new Vector2(1f, 1f));
                t.Add(b); t.Add(b + 1); t.Add(b + 4); t.Add(b); t.Add(b + 4); t.Add(b + 3);         // flat on the ground
                t.Add(b + 1); t.Add(b + 2); t.Add(b + 5); t.Add(b + 1); t.Add(b + 5); t.Add(b + 4); // upright at the middle
            }
            var mesh = new Mesh { name = "Arena rocks" };
            mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetTriangles(t, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = new Vector3(0f, 0.82f, info.Turned ? 0.5724f : -0.5724f); // lit like it faces the camera
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

        static ArenaOwned owned; // the arena being built: keeps every mesh and material made for it, freed with it

        static bool Shared(Material m) => m == leafMat || m == leafFaded || m == marksMat || m == ArtMaterials.Lit || m == ArtMaterials.LitFade
            || m == ArtMaterials.LitClip || m == ArtMaterials.Unlit || m == ArtMaterials.UnlitFade || m == ArtMaterials.UnlitAdd;

        internal static GameObject Add(GameObject root, string name, Mesh mesh, Material mat, bool lit, bool cast = true)
        {
            if (owned != null)
            {
                if (mesh != null) owned.Items.Add(mesh);
                if (mat != null && !Shared(mat)) owned.Items.Add(mat);
            }
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

        // Edge from a to b, ordered so the face looks outward (see the callers). outer: the map's own boundary (not a void tile inside).
        static void Cliff(MeshBuilder m, Layer kerb, Vector3 a, Vector3 b, bool outer)
        {
            Vector3 down = Vector3.down;
            float bottom = -waterY + 0.1f;
            // with the cliff picture (CliffSkirt) hung exactly on this plane since the boundary wall went (d5954de), these two faces
            // z-fought with it and the edge flickered (Rickard's video 2026-11-04 16:37); the picture is the cliff now
            if (!(skirted && outer))
            {
                MeshBuilder top = Pick(kerb, m, Pal.Cliff);
                top.Face(a, b, b + down * 0.5f, a + down * 0.5f);
                m.Color(Pal.CliffDark).Face(a + down * 0.5f, b + down * 0.5f, b + down * Mathf.Max(bottom, 0.6f), a + down * Mathf.Max(bottom, 0.6f));
            }
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

    /// <summary>On the arena root: the meshes and materials Arena and Island made for this one arena. Destroying a GameObject
    /// leaves them alive (they are DontSave), so every match and the tutorial's bridge rebuild would keep the old arena's.</summary>
    /// <summary>
    /// The carved runes round each tower's plaza light up briefly in the tower's team colour when that tower fires, and only
    /// then (Rickard 2026-11-01: glowing runes all the time distracted from the fight). Two additive quads over the painted
    /// ground, one per plaza, with the runes cut out of the ground picture (bam-work/art/karta/detaljer/make_runmask.py);
    /// each is switched off while dark, so it costs nothing between shots.
    /// </summary>
    sealed class TowerRunes : MonoBehaviour
    {
        static TowerRunes live;
        static readonly string[] Pics = { "T_BAM_TowerRunes_Top", "T_BAM_TowerRunes_Bottom" };
        // each picture is 448 px (7 tiles at 64 px a tile) round its plaza's centre in the 960 x 2240 ground picture: (480, 361) and (480, 1889)
        static readonly Vector2[] Centre = { new Vector2(7.5f, 35f - 361f / 64f), new Vector2(7.5f, 35f - 1889f / 64f) };
        const float Size = 7f, Peak = 0.8f, Rise = 0.06f, Fade = 0.55f;
        readonly Renderer[] quad = new Renderer[2];
        readonly Material[] mat = new Material[2];
        readonly float[] age = { 99f, 99f };
        readonly Color[] tint = new Color[2];

        internal static void Build(GameObject root)
        {
            TowerRunes tr = null;
            for (int i = 0; i < 2; i++)
            {
                Texture2D t = Resources.Load<Texture2D>("BAMArt/Arena/" + Pics[i]);
                if (t == null) continue;
                if (tr == null) tr = root.AddComponent<TowerRunes>();
                Vector2 c = Centre[i];
                float s = Size * 0.5f, y = 0.014f;
                var mesh = new Mesh { name = Pics[i] };
                mesh.vertices = new[] { new Vector3(c.x - s, y, c.y - s), new Vector3(c.x - s, y, c.y + s), new Vector3(c.x + s, y, c.y + s), new Vector3(c.x + s, y, c.y - s) };
                mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                var m = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                m.SetTexture(ArtMaterials.BaseMap, t);
                m.mainTexture = t;
                m.SetColor(ArtMaterials.BaseColor, Color.black);
                GameObject go = Arena.Add(root, Pics[i], mesh, m, false);
                go.isStatic = false;
                tr.quad[i] = go.GetComponent<Renderer>();
                tr.quad[i].enabled = false;
                tr.mat[i] = m;
            }
            if (tr != null) { tr.enabled = false; live = tr; }
        }

        /// <summary>The tower at <paramref name="at"/> fired: its plaza's runes flash in <paramref name="c"/>.</summary>
        internal static void Pulse(Vector3 at, Color c)
        {
            if (live == null) return;
            int i = Mathf.Abs(at.z - Centre[0].y) <= Mathf.Abs(at.z - Centre[1].y) ? 0 : 1;
            if (live.quad[i] == null) return;
            live.age[i] = 0f;
            live.tint[i] = c;
            live.quad[i].enabled = true;
            live.enabled = true;
        }

        void Update()
        {
            bool any = false;
            for (int i = 0; i < 2; i++)
            {
                if (quad[i] == null || !quad[i].enabled) continue;
                age[i] += Time.deltaTime;
                if (age[i] >= Rise + Fade) { quad[i].enabled = false; continue; }
                float k = age[i] < Rise ? age[i] / Rise : 1f - (age[i] - Rise) / Fade;
                k = Mathf.Clamp01(k) * Peak;
                Color c = tint[i];
                mat[i].SetColor(ArtMaterials.BaseColor, new Color(c.r * k, c.g * k, c.b * k, 1f));
                any = true;
            }
            if (!any) enabled = false;
        }

        void OnDestroy() { if (live == this) live = null; }
    }

    /// <summary>
    /// "Show the way" at the start of a match (Rickard 2026-11-02): the carved runes down the middle of the road light up in a
    /// wave from the player's base to the enemy's, their plazas flash, the runes stay softly lit and go out when the first
    /// minions come. The road is cut in 20 one-tile slices (additive quads over the painted ground, the runes cut out of the
    /// ground picture by bam-work/art/karta/detaljer/make_vagrunor.py), each switched off while dark.
    /// </summary>
    sealed class RoadRunes : MonoBehaviour
    {
        static RoadRunes live;
        const string Pic = "T_BAM_RoadRunes";
        // the picture is the strip x 448-512, y 480-1760 px of the 960 x 2240 ground picture: tiles x 7-8, rows 7.5-27.5
        const int N = 20;
        const float X0 = 7f, X1 = 8f, ZTop = 27.5f;
        const float Speed = 14f;                        // slices (tiles) a second the light runs along the road
        const float Front = 0.95f, Hold = 0.5f;         // brightness as the light passes, then while it waits for the minions
        const float Rise = 0.1f, Settle = 0.5f, Out = 0.9f, MaxOn = 8f;
        readonly Renderer[] quad = new Renderer[N];
        readonly Material[] mat = new Material[N];
        float t = -1f, outT = -1f;
        bool fromTop, arrived;
        Color own, foe;
        Vector3 to;

        internal static void Build(GameObject root)
        {
            Texture2D tex = Resources.Load<Texture2D>("BAMArt/Arena/" + Pic);
            if (tex == null) return;
            var rr = root.AddComponent<RoadRunes>();
            const float y = 0.015f;
            for (int i = 0; i < N; i++)
            {
                float z1 = ZTop - i, z0 = z1 - 1f, v1 = 1f - (float)i / N, v0 = 1f - (float)(i + 1) / N;
                var mesh = new Mesh { name = Pic + "_" + i };
                mesh.vertices = new[] { new Vector3(X0, y, z0), new Vector3(X0, y, z1), new Vector3(X1, y, z1), new Vector3(X1, y, z0) };
                mesh.uv = new[] { new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(1f, v1), new Vector2(1f, v0) };
                mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                mesh.RecalculateBounds();
                var m = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                m.SetTexture(ArtMaterials.BaseMap, tex);
                m.mainTexture = tex;
                m.SetColor(ArtMaterials.BaseColor, Color.black);
                GameObject go = Arena.Add(root, Pic + "_" + i, mesh, m, false);
                go.isStatic = false;
                rr.quad[i] = go.GetComponent<Renderer>();
                rr.quad[i].enabled = false;
                rr.mat[i] = m;
            }
            rr.enabled = false;
            live = rr;
        }

        /// <summary>Run the light from the tower at <paramref name="from"/> to the one at <paramref name="target"/>.</summary>
        internal static void Show(Vector3 from, Vector3 target, Color ownColour, Color foeColour)
        {
            if (live == null) return;
            live.fromTop = from.z > ZTop - N * 0.5f;
            live.to = target;
            live.own = ownColour;
            live.foe = foeColour;
            live.t = 0f;
            live.outT = -1f;
            live.arrived = false;
            live.enabled = true;
            TowerRunes.Pulse(from, ownColour);
        }

        /// <summary>The first minions came: the road goes dark.</summary>
        internal static void Hide()
        {
            if (live == null || live.t < 0f || live.outT >= 0f) return;
            live.outT = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            if (outT < 0f && t > MaxOn) outT = 0f;
            if (outT >= 0f) outT += dt;
            float fade = outT < 0f ? 1f : 1f - outT / Out;
            if (!arrived && t >= N / Speed) { arrived = true; TowerRunes.Pulse(to, foe); }
            if (fade <= 0f)
            {
                for (int i = 0; i < N; i++) if (quad[i] != null) quad[i].enabled = false;
                t = -1f;
                enabled = false;
                return;
            }
            for (int i = 0; i < N; i++)
            {
                if (quad[i] == null) continue;
                int k = fromTop ? i : N - 1 - i;          // the slice's place along the run
                float since = t - (k + 0.5f) / Speed;     // how long ago the light reached it
                if (since < 0f) { quad[i].enabled = false; continue; }
                float b = since < Rise ? since / Rise * Front : Mathf.Lerp(Front, Hold, Mathf.Clamp01((since - Rise) / Settle));
                b *= fade;
                quad[i].enabled = true;
                mat[i].SetColor(ArtMaterials.BaseColor, new Color(own.r * b, own.g * b, own.b * b, 1f));
            }
        }

        void OnDestroy() { if (live == this) live = null; }
    }

    sealed class ArenaOwned : MonoBehaviour
    {
        internal readonly System.Collections.Generic.List<Object> Items = new System.Collections.Generic.List<Object>(128);

        void OnDestroy()
        {
            for (int i = 0; i < Items.Count; i++) if (Items[i] != null) Destroy(Items[i]);
            Items.Clear();
        }
    }
}
