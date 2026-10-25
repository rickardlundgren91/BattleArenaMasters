using UnityEngine;

namespace BAM.Art
{
    // Every opaque mesh in the game is coloured through one 16x16 palette texture: a vertex picks its colour with its UV.
    // That lets one URP Simple Lit material (no custom shader, no vertex colours) draw the whole arena and all units,
    // keeps the SRP batcher happy and makes a later swap to real textures a material change only.
    static class Pal
    {
        public const int Size = 16;

        // Index = cell in the texture (row-major from the bottom). Only append; meshes are cached by index.
        public const byte White = 0, Black = 1, Shadow = 2, Ink = 3,
            Ground = 4, GroundB = 5, Road = 6, RoadEdge = 7, Grass = 8, GrassDark = 9, GrassLight = 10,
            Stone = 11, StoneTop = 12, StoneDark = 13, Cliff = 14, CliffDark = 15, Void = 16, Rock = 17, RockLight = 18,
            Skin = 19, SkinDark = 20, Leather = 21, LeatherDark = 22, Metal = 23, MetalDark = 24, Gold = 25, Wood = 26,
            WoodDark = 27, Cloth = 28, Beard = 29, Forest = 30, ForestDark = 31, Tan = 32, Antler = 33, Honey = 34, HoneyDark = 35,
            BeeBlack = 36, Wing = 37, Walrus = 38, WalrusDark = 39, Tusk = 40, Navy = 41, Teal = 42, TealDark = 43, Violet = 44,
            Pink = 45, Goblin = 46, GoblinDark = 47, Glove = 48, Bomb = 49, Fuse = 50, Fire = 51, FireCore = 52, Goggle = 53,
            Ghost = 54, GhostDark = 55, GhostEye = 56, Eye = 57, Pupil = 58, Steel = 59, Hair = 60, Cream = 61,
            // Team colours: main, light, dark for team 0 (blue) and team 1 (red). Keep the three of a team together.
            Blue = 64, BlueLight = 65, BlueDark = 66, Red = 67, RedLight = 68, RedDark = 69,
            BarBack = 70, BarPlayer = 71, BarAlly = 72, BarEnemy = 73, BarShield = 74, BarTrail = 75, BarFrame = 76,
            PlayerRing = 77, Stun = 78, Slow = 79, Root = 80, Burn = 81, Poison = 82, ShieldFx = 83, Haste = 84,
            AimOk = 85, AimBad = 86, Range = 87, ShadowSoft = 88, WhiteSoft = 89, WhiteMid = 90;

        static readonly Color32[] colors = new Color32[Size * Size];
        static Texture2D tex;

        static Pal()
        {
            Set(White, 0xffffff); Set(Black, 0x111111); Set(Shadow, 0x000000, 110); Set(Ink, 0x22252e);
            Set(Ground, 0xd8bf8a); Set(GroundB, 0xcfb47c); Set(Road, 0xece0bd); Set(RoadEdge, 0xbfa676);
            Set(Grass, 0x47b54a); Set(GrassDark, 0x2c7d33); Set(GrassLight, 0x7fd65a);
            Set(Stone, 0x8d93a6); Set(StoneTop, 0xb9bfcf); Set(StoneDark, 0x666c80); Set(Cliff, 0x7a5a3c); Set(CliffDark, 0x4a3524);
            Set(Void, 0x1c2440); Set(Rock, 0x9a8f86); Set(RockLight, 0xc4bab0);
            Set(Skin, 0xf2c39b); Set(SkinDark, 0xd39a72); Set(Leather, 0x9a6232); Set(LeatherDark, 0x6b3f1d);
            Set(Metal, 0xc8ced8); Set(MetalDark, 0x6f7785); Set(Gold, 0xffc23a); Set(Wood, 0xa8743f); Set(WoodDark, 0x6e4724);
            Set(Cloth, 0xe9e2cf); Set(Beard, 0xd9732c); Set(Forest, 0x3f8f4f); Set(ForestDark, 0x2b5e37); Set(Tan, 0xc9a26b);
            Set(Antler, 0xe8d8b8); Set(Honey, 0xffc93c); Set(HoneyDark, 0xe39b12); Set(BeeBlack, 0x3a2f2a); Set(Wing, 0xd9f3ff);
            Set(Walrus, 0xa77d63); Set(WalrusDark, 0x7d5944); Set(Tusk, 0xfff6e0); Set(Navy, 0x3b4766);
            Set(Teal, 0x2ad1b5); Set(TealDark, 0x13806f); Set(Violet, 0x8a5cff); Set(Pink, 0xff6fb5);
            Set(Goblin, 0x8ccf4d); Set(GoblinDark, 0x5c9a2c); Set(Glove, 0xff9a2e); Set(Bomb, 0x2c2c38); Set(Fuse, 0xfff07a);
            Set(Fire, 0xff7a1a); Set(FireCore, 0xffe066); Set(Goggle, 0x9ff0ff);
            Set(Ghost, 0x9fe8ff); Set(GhostDark, 0x7a8cff); Set(GhostEye, 0xffffff); Set(Eye, 0xffffff); Set(Pupil, 0x15151a);
            Set(Steel, 0xe3e9f2); Set(Hair, 0x5a3a24); Set(Cream, 0xfff1d6);
            Set(Blue, 0x2f7bff); Set(BlueLight, 0x8cc0ff); Set(BlueDark, 0x1c4fb8);
            Set(Red, 0xff3d3d); Set(RedLight, 0xff9a8f); Set(RedDark, 0xb31d24);
            Set(BarBack, 0x1a1c24); Set(BarPlayer, 0x53e05a); Set(BarAlly, 0x3d9bff); Set(BarEnemy, 0xff4040);
            Set(BarShield, 0xfff2b0); Set(BarTrail, 0xffffff); Set(BarFrame, 0x0b0c10);
            Set(PlayerRing, 0x6dff7a); Set(Stun, 0xffe14a); Set(Slow, 0x7fd8ff); Set(Root, 0x6fbf3a); Set(Burn, 0xff7a1a);
            Set(Poison, 0x9cff3a); Set(ShieldFx, 0xffd860, 85); Set(Haste, 0xffffff);
            Set(AimOk, 0xffffff); Set(AimBad, 0xff3030); Set(Range, 0xffffff); Set(ShadowSoft, 0x000000, 70);
            Set(WhiteSoft, 0xffffff, 70); Set(WhiteMid, 0xffffff, 150);
        }

        static void Set(int i, int rgb, byte a = 255) =>
            colors[i] = new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, a);

        public static Color Get(int i) => colors[i];

        /// <summary>UV of the centre of a palette cell.</summary>
        public static Vector2 UV(int i) => new Vector2(((i % Size) + 0.5f) / Size, ((i / Size) + 0.5f) / Size);

        public static byte Team(int team) => team == 1 ? Red : Blue;
        public static byte TeamLight(int team) => team == 1 ? RedLight : BlueLight;
        public static byte TeamDark(int team) => team == 1 ? RedDark : BlueDark;

        public static Texture2D Texture
        {
            get
            {
                if (tex != null) return tex;
                // Point filter and no mips: a vertex UV always lands on exactly one cell, at every distance.
                tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false)
                {
                    name = "BAM Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave,
                };
                tex.SetPixels32(colors);
                tex.Apply(false, true);
                return tex;
            }
        }

        static Texture2D dot;
        /// <summary>A soft round dot for particles (white, alpha falls off to the edge).</summary>
        public static Texture2D Dot
        {
            get
            {
                if (dot != null) return dot;
                const int n = 32;
                dot = new Texture2D(n, n, TextureFormat.RGBA32, true, false) { name = "BAM Dot", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                dot.SetPixels32(px);
                dot.Apply(true, true);
                return dot;
            }
        }
    }
}
