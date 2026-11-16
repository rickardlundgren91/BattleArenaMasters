using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// The guided match's round training island (the old game's View3DMapTutorial.cs), drawn with Rickard's own painted
    /// island textures (Resources/BAMArt/Island, Gemini 2026-10-18): the platform picture on one round mesh (rows 0-16),
    /// its painted cliff under the rim, the bridge (rows 17-19; a lane below it too if the map has one) with the same cliff
    /// under every edge that faces the void, the boost pad on the bridge's last row (the game's own jump pad, Arena.JumpPad),
    /// two braziers and small floating islets, in the sky of Home's own painting (tut_sky, a picture held behind everything;
    /// a plain cloud sea far below stands in without it).
    /// Built once by Arena.Build when ArenaInfo.Island is set; only the bridge's rise and the sky move afterwards.
    /// </summary>
    static class Island
    {
        public const int PlatformRows = 17;
        const int BridgeX0 = 5, BridgeRows = 3;           // the bridge: columns 5-7, rows 17-19 (Sim TutorialIsland.cs)
        const float MidY = 11f;                           // the platform's middle row (its middle stone lies at x 6.5)
        // where the platform picture lies, in tiles (0.9 times the old first island, so it all fits one screen)
        const float DiscX0 = 0.65f, DiscY0 = 5.91f, DiscW = 11.52f, DiscH = 10.98f;
        const float CliffDepth = 3.2f, CloudY = -14f;
        const float SkyAspect = 768f / 960f;              // tut_sky.jpg's own shape (Unity imports it square, so it is not asked)
        internal const float RiseDepth = 3f;
        static readonly Color CloudSea = new Color32(0xe4, 0xf0, 0xfa, 0xff);

        // The platform's outline: 96 points (u, v counted down the picture) where tut_disc.png's alpha ends, 3 px inside,
        // read once from the picture offline (bam-work/art/island/rim.py) so the game never reads pixels.
        static readonly float[] Rim =
        {
            0.9938f, 0.5f, 0.9902f, 0.5337f, 0.987f, 0.5673f, 0.9806f, 0.6004f, 0.9733f, 0.6331f, 0.9628f, 0.6649f, 0.9446f, 0.6934f, 0.9339f, 0.7246f,
            0.9189f, 0.7539f, 0.9022f, 0.7822f, 0.8818f, 0.8076f, 0.8628f, 0.834f, 0.8315f, 0.848f, 0.8132f, 0.8749f, 0.7892f, 0.8956f, 0.7653f, 0.9168f,
            0.7362f, 0.9296f, 0.7079f, 0.9426f, 0.6799f, 0.9559f, 0.6495f, 0.9623f, 0.6216f, 0.9766f, 0.5927f, 0.9891f, 0.5613f, 0.9892f, 0.5308f, 0.9937f,
            0.5f, 0.9921f, 0.4693f, 0.9911f, 0.4393f, 0.984f, 0.4095f, 0.9775f, 0.3787f, 0.9754f, 0.3469f, 0.9735f, 0.3182f, 0.9607f, 0.2877f, 0.952f,
            0.2588f, 0.9387f, 0.2319f, 0.9212f, 0.2093f, 0.8977f, 0.1835f, 0.8789f, 0.1597f, 0.8573f, 0.1485f, 0.8236f, 0.1281f, 0.7996f, 0.1092f, 0.7741f,
            0.093f, 0.7467f, 0.0796f, 0.7177f, 0.0623f, 0.6903f, 0.0514f, 0.6599f, 0.0424f, 0.6287f, 0.0317f, 0.5978f, 0.0204f, 0.5663f, 0.0185f, 0.5331f,
            0.005f, 0.5f, 0.016f, 0.4667f, 0.0204f, 0.4337f, 0.017f, 0.3991f, 0.0291f, 0.3675f, 0.0419f, 0.3367f, 0.0519f, 0.3051f, 0.0729f, 0.2789f,
            0.0821f, 0.2467f, 0.1019f, 0.2208f, 0.1212f, 0.1948f, 0.1438f, 0.1721f, 0.1641f, 0.1474f, 0.1843f, 0.1221f, 0.2108f, 0.1044f, 0.2382f, 0.0886f,
            0.2656f, 0.0738f, 0.2916f, 0.0563f, 0.3168f, 0.0356f, 0.3469f, 0.0265f, 0.3771f, 0.0183f, 0.4076f, 0.0122f, 0.4387f, 0.0108f, 0.4692f, 0.0063f,
            0.5f, 0.0039f, 0.5308f, 0.0063f, 0.5618f, 0.0069f, 0.5924f, 0.0122f, 0.6223f, 0.0208f, 0.6519f, 0.0303f, 0.6808f, 0.0417f, 0.714f, 0.0445f,
            0.7406f, 0.0624f, 0.7695f, 0.0766f, 0.796f, 0.095f, 0.8223f, 0.1142f, 0.8491f, 0.1335f, 0.8684f, 0.1608f, 0.8917f, 0.1844f, 0.9074f, 0.2142f,
            0.9189f, 0.2461f, 0.9361f, 0.2742f, 0.9515f, 0.3036f, 0.9652f, 0.3342f, 0.9842f, 0.3638f, 0.9892f, 0.3978f, 0.9908f, 0.4322f, 0.9914f, 0.4662f
        };

        public static void Build(GameObject root, ArenaInfo info)
        {
            Texture2D disc = Tex("tut_disc"), cliffDisc = Tex("tut_cliff_disc"), cliffLane = Tex("tut_cliff_lane"),
                stone = Tex("tut_stone"), moss = Tex("tut_moss"), bridge = Tex("tut_bridge");
            if (disc != null) disc.wrapMode = TextureWrapMode.Clamp;
            // the cliff pictures repeat sideways; their bottom is cut out (stalactites), so no wrap downwards
            if (cliffDisc != null) { cliffDisc.wrapModeU = TextureWrapMode.Repeat; cliffDisc.wrapModeV = TextureWrapMode.Clamp; }
            if (cliffLane != null) { cliffLane.wrapModeU = TextureWrapMode.Repeat; cliffLane.wrapModeV = TextureWrapMode.Clamp; }

            Sky(root, CloudPlane(root, info));
            Platform(root, info, disc, cliffDisc);
            Props(root, info);
            Lane(root, info, cliffLane, stone, moss, bridge);
        }

        static Texture2D Tex(string name)
        {
            var t = Resources.Load<Texture2D>("BAMArt/Island/" + name);
            if (t == null) Debug.LogError("[BAM.Art] Island texture Resources/BAMArt/Island/" + name + " is missing");
            return t;
        }

        static Material Textured(Material baseMat, Texture2D t)
        {
            var m = ArtMaterials.Instance(baseMat);
            m.SetTexture(ArtMaterials.BaseMap, t);
            m.mainTexture = t;
            return m;
        }

        static float Z(ArenaInfo info, float row) => info.Height - row;

        // ------------------------------------------------------------------ sky

        // The plain cloud sea far below: all there was round the island until 2026-11-16 (a white screen with an island on it).
        // Now it only stands in where the sky picture cannot be shown (IslandSky).
        static GameObject CloudPlane(GameObject root, ArenaInfo info)
        {
            var mb = new MeshBuilder();
            float w = info.Width, h = info.Height;
            mb.Color(Pal.White).Face(new Vector3(-80f, CloudY, -80f), new Vector3(-80f, CloudY, h + 80f), new Vector3(w + 80f, CloudY, h + 80f), new Vector3(w + 80f, CloudY, -80f));
            var m = Textured(ArtMaterials.Unlit, Texture2D.whiteTexture);
            m.SetColor(ArtMaterials.BaseColor, CloudSea);
            return Arena.Add(root, "Cloud sea", mb.ToMesh("Island cloud sea"), m, false);
        }

        // The sky all round the island: Home's own painting, blurred (tut_sky.jpg, cut from Rickard's home_bg; Rickard
        // 2026-11-16: "Ja, Home-himlen (suddig)"), so the island floats in the same sky the game opens on and the first
        // start's picture of Home fades into it. One picture filling the view, far behind everything, held to the camera.
        static void Sky(GameObject root, GameObject clouds)
        {
            var tex = Resources.Load<Texture2D>("BAMArt/Island/tut_sky"); // without it the cloud sea stays, as before
            if (tex == null) return;
            tex.wrapMode = TextureWrapMode.Clamp;
            // a unit square facing the camera it is turned with (IslandSky)
            var v = new List<Vector3> { new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f) };
            var n = new List<Vector3> { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            var uv = new List<Vector2> { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            var t = new List<int>(6);
            Quad(t, 0, 1, 2, 3, false);
            var go = Arena.Add(root, "Sky", Build("Island sky", v, n, uv, t), Textured(ArtMaterials.Unlit, tex), false);
            Moving(go);
            var sky = go.AddComponent<IslandSky>();
            sky.Picture = go.GetComponent<MeshRenderer>();
            sky.Clouds = clouds.GetComponent<MeshRenderer>();
            sky.Aspect = SkyAspect;
            sky.Picture.enabled = false; // until its first frame has placed it
        }

        // ------------------------------------------------------------------ the round platform

        static void Platform(GameObject root, ArenaInfo info, Texture2D disc, Texture2D cliff)
        {
            int seg = Rim.Length / 2;
            var v = new List<Vector3>(seg + 1);
            var uv = new List<Vector2>(seg + 1);
            var n = new List<Vector3>(seg + 1);
            var t = new List<int>(seg * 3);
            Vector3 mid = PicturePoint(info, 0.5f, 0.5f);
            v.Add(mid); uv.Add(new Vector2(0.5f, 0.5f)); n.Add(Vector3.up);
            for (int i = 0; i < seg; i++)
            {
                float u = Rim[i * 2], d = Rim[i * 2 + 1];
                v.Add(PicturePoint(info, u, d)); uv.Add(new Vector2(u, 1f - d)); n.Add(Vector3.up);
            }
            // pick the winding that faces up (the picture's y runs down while world z runs up, so work it out)
            bool flip = Vector3.Cross(v[1] - v[0], v[2] - v[0]).y < 0f;
            for (int i = 0; i < seg; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % seg;
                t.Add(0); t.Add(flip ? b : a); t.Add(flip ? a : b);
            }
            if (disc != null)
            {
                var mesh = Build("Island platform", v, n, uv, t);
                Arena.Add(root, "Platform", mesh, Textured(ArtMaterials.Unlit, disc), false);
            }
            else Debug.LogError("[BAM.Art] Island platform picture missing: the island has no floor");

            if (cliff == null) return;
            // the rock face under the rim, tapering in a little, repeating about once per CliffDepth of rim
            float len = 0f;
            for (int i = 0; i < seg; i++) len += Vector3.Distance(v[1 + i], v[1 + (i + 1) % seg]);
            float reps = Mathf.Max(1f, Mathf.Round(len / CliffDepth)), run = 0f;
            var cv = new List<Vector3>((seg + 1) * 2);
            var cu = new List<Vector2>((seg + 1) * 2);
            var ct = new List<int>(seg * 6);
            for (int i = 0; i <= seg; i++)
            {
                Vector3 top = v[1 + i % seg];
                Vector3 bot = new Vector3(mid.x + (top.x - mid.x) * 0.86f, -CliffDepth, mid.z + (top.z - mid.z) * 0.86f);
                if (i > 0) run += Vector3.Distance(top, v[1 + (i - 1) % seg]);
                float uu = run / len * reps;
                cv.Add(top); cu.Add(new Vector2(uu, 1f));
                cv.Add(bot); cu.Add(new Vector2(uu, 0f));
            }
            Vector3 out0 = cv[0] - mid; out0.y = 0f;
            bool cflip = Vector3.Dot(Vector3.Cross(cv[1] - cv[0], cv[2] - cv[0]), out0) < 0f;
            for (int i = 0; i < seg; i++)
            {
                int b0 = i * 2;
                Quad(ct, b0, b0 + 1, b0 + 3, b0 + 2, cflip);
            }
            var cm = Build("Island platform cliff", cv, null, cu, ct);
            Arena.Add(root, "Platform cliff", cm, Textured(ArtMaterials.LitClip, cliff), true);
        }

        /// <summary>A point of the platform picture (u across, d down the picture) on the map, at ground height.</summary>
        static Vector3 PicturePoint(ArenaInfo info, float u, float d) => new Vector3(DiscX0 + u * DiscW, 0f, Z(info, DiscY0 + d * DiscH));

        // ------------------------------------------------------------------ props on and around the platform

        static void Props(GameObject root, ArenaInfo info)
        {
            var solid = new MeshBuilder();
            var glow = new MeshBuilder();
            float zMid = Z(info, MidY);

            // (no glowing circle drawn round the middle stone any more: the first lesson's gold ring lies left of it since
            // 2026-11-16, and a second, green ring beside the one to walk into only confused)

            // two braziers flanking the middle
            for (int i = 0; i < 2; i++)
            {
                var p = new Vector3(i == 0 ? 1.5f : 11.5f, 0f, zMid);
                solid.Color(Pal.StoneDark).Cylinder(p, 0.34f, 0.3f, 0.5f, 6, true, Pal.Stone);
                solid.Color(Pal.Stone).Cylinder(p + Vector3.up * 0.5f, 0.3f, 0.42f, 0.18f, 8, true, Pal.CliffDark);
                glow.Color(Pal.Fire).Cylinder(p + Vector3.up * 0.62f, 0.26f, 0f, 0.6f, 5, false);
                glow.Color(Pal.FireCore).Cylinder(p + new Vector3(0.06f, 0.64f, 0.04f), 0.14f, 0f, 0.4f, 4, false);
            }

            // small grassy islets floating around, lower than the floor so they never hide it
            var rnd = new System.Random(31);
            float w = info.Width, h = info.Height;
            for (int i = 0; i < 16; i++)
            {
                float r1 = (float)rnd.NextDouble(), r2 = (float)rnd.NextDouble(), r3 = (float)rnd.NextDouble(), r4 = (float)rnd.NextDouble();
                float x = i % 2 == 1 ? w + 3.5f + r1 * 8f : -4.5f - r1 * 8f;
                float row = -4f + i / 16f * (h + 8f) + r2 * 2f;
                float rad = 0.9f + r3 * 1.6f, top = -0.8f - r4 * 2.2f, hh = 1.2f + rad * 1.1f;
                var p = new Vector3(x, top - hh, Z(info, row));
                solid.Color(Pal.Rock).Cylinder(p, rad * 0.35f, rad, hh, 7, true, Pal.Grass);
                solid.Color(Pal.GrassDark).Cylinder(p + Vector3.up * (hh - 0.12f), rad * 1.02f, rad * 1.04f, 0.14f, 7, false);
            }

            Arena.Add(root, "Island props", solid.ToMesh("Island props"), ArtMaterials.Lit, true);
            Arena.Add(root, "Island glow", glow.ToMesh("Island glow"), ArtMaterials.UnlitAdd, false);
        }

        // ------------------------------------------------------------------ the bridge and the lane (rows 17+)

        static void Lane(GameObject root, ArenaInfo info, Texture2D cliff, Texture2D stone, Texture2D moss, Texture2D bridge)
        {
            int w = info.Width, h = info.Height;
            var lane = new GameObject("Island lane");
            lane.transform.SetParent(root.transform, false);

            var stoneB = new MeshBuilder { WorldUV = 4f };    // about one flagstone per tile
            var mossB = new MeshBuilder { WorldUV = 3.5f };
            var solid = new MeshBuilder();                     // curbs, bushes, the boost pad (palette)
            var padMarks = new MeshBuilder();                  // the pad's soft shadow on the bridge
            var block = Resources.Load<Texture2D>("BAMArt/Arena/T_BAM_Block");
            var wallB = new MeshBuilder { WorldUV = 2f };
            var bv = new List<Vector3>(); var bn = new List<Vector3>(); var bu = new List<Vector2>(); var bt = new List<int>();
            var cv = new List<Vector3>(); var cu = new List<Vector2>(); var ct = new List<int>();
            var rnd = new System.Random(4321);
            bool any = false;

            for (int ty = PlatformRows; ty < h; ty++)
                for (int tx = 0; tx < w; tx++)
                {
                    int code = info.Tile(tx, ty);
                    if (code == 0) continue;
                    any = true;
                    Vector3 c = info.TileCenter(tx, ty);
                    float x0 = c.x - 0.5f, x1 = c.x + 0.5f, z0 = c.z - 0.5f, z1 = c.z + 0.5f;
                    var a = new Vector3(x0, 0f, z0); var b = new Vector3(x0, 0f, z1); var cc = new Vector3(x1, 0f, z1); var d = new Vector3(x1, 0f, z0);

                    bool onBridge = ty < PlatformRows + BridgeRows && tx >= BridgeX0 && tx < BridgeX0 + 3;
                    if (onBridge && bridge != null)
                    {
                        // the bridge picture spans its three columns and repeats every 3.3 rows down the bridge
                        int i = bv.Count;
                        bv.Add(a); bv.Add(b); bv.Add(cc); bv.Add(d);
                        for (int k = 0; k < 4; k++)
                        {
                            Vector3 q = bv[i + k];
                            bn.Add(Vector3.up);
                            bu.Add(new Vector2((q.x - BridgeX0) / 3f, 1f - (info.Height - q.z - PlatformRows) / 3.3f));
                        }
                        Quad(bt, i, i + 1, i + 2, i + 3, false);
                    }
                    else if (code == 2 && moss != null) mossB.Face(a, b, cc, d);
                    else if (stone != null) stoneB.Face(a, b, cc, d);
                    else solid.Color(Pal.StoneTop).Face(a, b, cc, d);

                    if (code == 2) Arena.Bush(solid, c, tx, ty, rnd);
                    else if (code == 3)
                    {
                        // Rickard's arena block for the practice walls (the same as in the arena)
                        MeshBuilder wb = block != null ? wallB : solid.Color(Pal.Stone);
                        wb.Box(new Vector3(c.x, 0.45f, c.z), new Vector3(1f, 0.9f, 1f), block != null ? -1 : Pal.StoneTop);
                    }

                    // the painted cliff and a low curb under every edge that faces the void
                    if (Void(info, tx, ty - 1)) Edge(cv, cu, ct, solid, new Vector3(x0, 0f, z1), new Vector3(x1, 0f, z1), Vector3.forward);
                    if (Void(info, tx, ty + 1)) Edge(cv, cu, ct, solid, new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), Vector3.back);
                    if (Void(info, tx - 1, ty)) Edge(cv, cu, ct, solid, new Vector3(x0, 0f, z0), new Vector3(x0, 0f, z1), Vector3.left);
                    if (Void(info, tx + 1, ty)) Edge(cv, cu, ct, solid, new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), Vector3.right);
                }
            if (!any) { Object.Destroy(lane); return; }

            // The boost pad (Rickard 2026-11-16, of the line "Hop on the boost pad": "Ingen jump pad"; only a gold ring lay
            // there): the game's own jump pad, the one the second map has, so the first pad a player meets looks like every
            // later one. It stands on the bridge and rises with it; its chevrons point the way it throws, out over the sky.
            if (info.Pads != null)
                for (int i = 0; i < info.Pads.Length; i++) Arena.JumpPad(solid, padMarks, info.Pads[i], info.PadTo[i], false);

            if (stoneB.VertexCount > 0) Moving(Arena.Add(lane, "Lane stone", stoneB.ToMesh("Island lane stone"), Textured(ArtMaterials.Unlit, stone), false));
            if (mossB.VertexCount > 0) Moving(Arena.Add(lane, "Lane moss", mossB.ToMesh("Island lane moss"), Textured(ArtMaterials.Unlit, moss), false));
            if (bv.Count > 0) Moving(Arena.Add(lane, "Bridge", Build("Island bridge", bv, bn, bu, bt), Textured(ArtMaterials.Unlit, bridge), false));
            if (wallB.VertexCount > 0) Moving(Arena.Add(lane, "Lane walls", wallB.ToMesh("Island lane walls"), Textured(ArtMaterials.Lit, block), true));
            if (solid.VertexCount > 0) Moving(Arena.Add(lane, "Lane solid", solid.ToMesh("Island lane solid"), ArtMaterials.Lit, true));
            if (padMarks.VertexCount > 0) Moving(Arena.Add(lane, "Lane marks", padMarks.ToMesh("Island lane marks"), Arena.MarksMaterial(), false));
            if (cv.Count > 0 && cliff != null) Moving(Arena.Add(lane, "Lane cliff", Build("Island lane cliff", cv, null, cu, ct), Textured(ArtMaterials.LitClip, cliff), true));

            if (info.LaneRises)
            {
                lane.transform.localPosition = new Vector3(0f, -RiseDepth, 0f);
                lane.AddComponent<IslandLaneRise>();
            }
        }

        // One edge of a lane tile from a to b that faces the void (outward): the cliff picture hanging CliffDepth down
        // and a low stone curb on top of the edge.
        static void Edge(List<Vector3> v, List<Vector2> uv, List<int> t, MeshBuilder solid, Vector3 a, Vector3 b, Vector3 outward)
        {
            int i = v.Count;
            float ua = (a.x + a.z) / CliffDepth, ub = (b.x + b.z) / CliffDepth;
            v.Add(a); uv.Add(new Vector2(ua, 1f));
            v.Add(a + Vector3.down * CliffDepth); uv.Add(new Vector2(ua, 0f));
            v.Add(b + Vector3.down * CliffDepth); uv.Add(new Vector2(ub, 0f));
            v.Add(b); uv.Add(new Vector2(ub, 1f));
            bool flip = Vector3.Dot(Vector3.Cross(v[i + 1] - v[i], v[i + 2] - v[i]), outward) < 0f;
            Quad(t, i, i + 1, i + 2, i + 3, flip);

            const float cw = 0.16f, chh = 0.16f;
            Vector3 mid = (a + b) * 0.5f - outward * (cw * 0.5f) + Vector3.up * (chh * 0.5f);
            bool alongX = Mathf.Abs(b.x - a.x) > 0.5f;
            solid.Color(Pal.Stone).Box(mid, alongX ? new Vector3(1f, chh, cw) : new Vector3(cw, chh, 1f), Pal.StoneTop);
        }

        static bool Void(ArenaInfo a, int tx, int ty) => tx < 0 || ty < 0 || tx >= a.Width || ty >= a.Height || a.Tile(tx, ty) == 0;

        static void Moving(GameObject go) => go.isStatic = false;

        static void Quad(List<int> t, int a, int b, int c, int d, bool flip)
        {
            if (!flip) { t.Add(a); t.Add(b); t.Add(c); t.Add(a); t.Add(c); t.Add(d); }
            else { t.Add(a); t.Add(c); t.Add(b); t.Add(a); t.Add(d); t.Add(c); }
        }

        static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            if (n != null) m.SetNormals(n); else m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }

    /// <summary>The island's sky picture, held to the match's camera: every frame it is put far behind everything, turned with
    /// the camera and sized to cover the view (its sides cut on a phone, its top and bottom on a wide screen), so the sky
    /// stands still while the island moves and shakes in front of it. The match's camera looks straight (orthographic); for
    /// any other camera (the Art Preview's) the picture hides and the plain cloud sea shows instead.</summary>
    sealed class IslandSky : MonoBehaviour
    {
        public MeshRenderer Picture, Clouds;
        public float Aspect = 1f; // the picture's width over its height
        Camera cam;

        void LateUpdate() // after GameFlow.Update has placed and shaken the camera
        {
            if (cam == null) cam = Camera.main;
            bool on = cam != null && cam.orthographic;
            if (Picture.enabled != on) Picture.enabled = on;
            if (Clouds != null && Clouds.enabled == on) Clouds.enabled = !on;
            if (!on) return;
            float vh = 2f * cam.orthographicSize, vw = vh * cam.aspect;
            float w = Mathf.Max(vw, vh * Aspect) * 1.02f; // a hair over, so no edge ever shows
            var t = cam.transform;
            transform.SetPositionAndRotation(t.position + t.forward * (cam.farClipPlane * 0.9f), t.rotation);
            transform.localScale = new Vector3(w, w / Aspect, 1f);
        }
    }

    /// <summary>The island's bridge (and lane) rising into place (smoothstep, 0.9 s) after the bridge opens.</summary>
    sealed class IslandLaneRise : MonoBehaviour
    {
        const float Time01 = 0.9f;
        float k;

        void Update()
        {
            k = Mathf.Min(1f, k + Mathf.Min(Time.unscaledDeltaTime, 0.1f) / Time01);
            float e = k * k * (3f - 2f * k);
            transform.localPosition = new Vector3(0f, (1f - e) * -Island.RiseDepth, 0f);
            if (k >= 1f) enabled = false;
        }
    }
}
