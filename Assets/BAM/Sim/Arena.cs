using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    /// <summary>A walking path on the tile grid: tile centres, the last point being the goal itself. One per unit, reused.</summary>
    public sealed class PathBuf
    {
        public const int Cap = 160;
        public readonly float[] X = new float[Cap], Y = new float[Cap];
        public int Count, Index;
        public bool Has => Index < Count;
        public void Clear() { Count = 0; Index = 0; }
    }

    /// <summary>
    /// The map BAM_OriginalArena_v1_2 (the old game's arena): 15 x 35 tiles, mirrored around the middle row. Tile (tx, ty)
    /// has ty counted from the top row (the enemy's side). Team 0 lives at the bottom (large y), team 1 at the top.
    /// </summary>
    public sealed class Arena
    {
        /// <summary>The BAM arena's size; another map (the guided match's island) may have its own.</summary>
        public const int ArenaW = 15, ArenaH = 35;
        public const byte Void = 0, Ground = 1, Grass = 2, Wall = 3;
        public readonly int W, H;

        // the top half down to the middle row (17); the rows below it are these mirrored. 0 hero spawn, 1 tower, 2 minion spawn
        static readonly string[] Top =
        {
            "...............", // 0
            "......000......", // 1
            "...............", // 2
            "g###.......###g", // 3
            "ggg#...2...#ggg", // 4
            "ggg#...1...#ggg", // 5
            "ggg#.......#ggg", // 6
            "ggg#.......#ggg", // 7
            "ggg#.......#ggg", // 8
            "ggg#.......#ggg", // 9
            "ggg#.......#ggg", // 10
            "ggg.........ggg", // 11
            "ggg.........ggg", // 12
            "ggg.........ggg", // 13
            "...............", // 14
            "...............", // 15
            "...##.....##...", // 16
            "...##.....##...", // 17 the middle row
        };

        /// <summary>Row 0 is the top. Void / Ground / Grass / Wall.</summary>
        public readonly byte[] Tiles;

        /// <summary>The two round middle boulders: capsules (x0, y0)-(x1, y1) with radius r. Their tiles stay walls for sight
        /// lines, paths and shots, but bodies only bump into the capsule, so units walk round the stone and not the square.</summary>
        public readonly float[] RockX0 = { 4, 11 }, RockY0 = { 17, 17 }, RockX1 = { 4, 11 }, RockY1 = { 18, 18 }, RockR = { 1, 1 };
        readonly bool[] rockTile;

        /// <summary>Hero spawn slots (x left to right), tower and minion spawn per team, as tile centres.</summary>
        public readonly float[][] SpawnX = { new float[3], new float[3] }, SpawnY = { new float[3], new float[3] };
        public readonly float[] TowerX = new float[2], TowerY = new float[2], MinionX = new float[2], MinionY = new float[2];

        /// <summary>The lane's waypoints (tile centres) from team 0's side to team 1's.</summary>
        public readonly float[] LaneX = { 7.5f, 7.5f, 7.5f }, LaneY = { 28.5f, 17.5f, 6.5f };

        /// <summary>The tiles of living towers block paths (Match keeps these up to date); -1 = none.</summary>
        internal int TowerTile0 = -1, TowerTile1 = -1;

        /// <summary>The BAM arena (BAM_OriginalArena_v1_2).</summary>
        public Arena()
        {
            W = ArenaW; H = ArenaH;
            Tiles = new byte[W * H]; rockTile = new bool[W * H];
            prev = new int[W * H]; queue = new int[W * H]; seen = new int[W * H]; back = new int[W * H];
            int n0 = 0, n1 = 0;
            for (int y = 0; y < H; y++)
            {
                string row = y < Top.Length ? Top[y] : Top[H - 1 - y];
                int team = y < H / 2 ? 1 : 0;
                for (int x = 0; x < W; x++)
                {
                    char c = row[x];
                    Tiles[y * W + x] = c == 'g' ? Grass : c == '#' ? Wall : Ground;
                    float cx = x + 0.5f, cy = y + 0.5f;
                    if (c == '0') { if (team == 1) { SpawnX[1][n1] = cx; SpawnY[1][n1++] = cy; } else { SpawnX[0][n0] = cx; SpawnY[0][n0++] = cy; } }
                    else if (c == '1') { TowerX[team] = cx; TowerY[team] = cy; }
                    else if (c == '2') { MinionX[team] = cx; MinionY[team] = cy; }
                }
            }
            // the boulders' tiles: walls within 1.5 rows of the middle and 4.5 columns of the centre (as the old FindRocks)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    if (Tiles[y * W + x] == Wall && Math.Abs(y + 0.5f - H / 2f) <= 1.5f && Math.Abs(x + 0.5f - W / 2f) < 4.5f) rockTile[y * W + x] = true;
        }

        /// <summary>Another map (the guided match's island): w x h tiles in rows from the top, its own lane, no boulders.
        /// The caller fills SpawnX/SpawnY, TowerX/TowerY and MinionX/MinionY.</summary>
        public Arena(int w, int h, byte[] tiles, float[] laneX, float[] laneY)
        {
            W = w; H = h; Tiles = tiles; rockTile = new bool[w * h];
            prev = new int[w * h]; queue = new int[w * h]; seen = new int[w * h]; back = new int[w * h];
            LaneX = laneX; LaneY = laneY;
            RockX0 = RockY0 = RockX1 = RockY1 = RockR = new float[0];
        }

        public byte Tile(int tx, int ty) => tx < 0 || ty < 0 || tx >= W || ty >= H ? Void : Tiles[ty * W + tx];
        public byte TileAt(float x, float y) => Tile((int)Math.Floor(x), (int)Math.Floor(y));
        public bool Walkable(int tx, int ty) { byte t = Tile(tx, ty); return t == Ground || t == Grass; }
        public bool InGrass(float x, float y) => TileAt(x, y) == Grass;

        bool HitsRock(float x, float y, float r)
        {
            for (int i = 0; i < RockR.Length; i++)
            {
                float dx = RockX1[i] - RockX0[i], dy = RockY1[i] - RockY0[i], l2 = dx * dx + dy * dy;
                float t = l2 > 0 ? SimMath.Clamp(((x - RockX0[i]) * dx + (y - RockY0[i]) * dy) / l2, 0, 1) : 0;
                float px = RockX0[i] + dx * t - x, py = RockY0[i] + dy * t - y, rr = RockR[i] + r;
                if (px * px + py * py < rr * rr) return true;
            }
            return false;
        }

        /// <summary>Can a body of radius r stand at (x, y)? Walls, the map's edge and the boulders' capsules block.</summary>
        public bool Free(float x, float y, float r)
        {
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Floor(x + r), y0 = (int)Math.Floor(y - r), y1 = (int)Math.Floor(y + r);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                {
                    if (Walkable(tx, ty)) continue;
                    if (tx >= 0 && ty >= 0 && tx < W && ty < H && rockTile[ty * W + tx]) { if (HitsRock(x, y, r)) return false; continue; }
                    float cx = SimMath.Clamp(x, tx, tx + 1), cy = SimMath.Clamp(y, ty, ty + 1);
                    if ((cx - x) * (cx - x) + (cy - y) * (cy - y) < r * r) return false;
                }
            return true;
        }

        const float LosStep = 0.1f, LosHalf = 0.1f;

        /// <summary>Does a point stop a sight line or a shot? A wall tile does, but a boulder's tiles only inside its capsule
        /// (the same shape bodies collide with), so a body standing in a tile corner next to a boulder can still be seen and shot.</summary>
        public bool WallAt(float x, float y)
        {
            int tx = (int)Math.Floor(x), ty = (int)Math.Floor(y);
            if (Tile(tx, ty) != Wall) return false;
            return !rockTile[ty * W + tx] || HitsRock(x, y, 0);
        }

        /// <summary>No wall (see WallAt) on the line a-b, sampled every 0.1 tile on its centre and both edges (half wide). The ends are not tested.</summary>
        public bool LineClear(float ax, float ay, float bx, float by, float half = LosHalf)
        {
            float dx = bx - ax, dy = by - ay, d = (float)Math.Sqrt(dx * dx + dy * dy);
            if (d < 1e-4f) return true;
            float px = -dy / d * half, py = dx / d * half;
            int n = (int)Math.Ceiling(d / LosStep);
            for (int i = 1; i < n; i++)
            {
                float k = (float)i / n, x = ax + dx * k, y = ay + dy * k;
                if (WallAt(x, y) || WallAt(x + px, y + py) || WallAt(x - px, y - py)) return false;
            }
            return true;
        }
        public bool LosClear(float ax, float ay, float bx, float by) => LineClear(ax, ay, bx, by, LosHalf);
        public bool LosClear(Unit a, Unit b) => LineClear(a.X, a.Y, b.X, b.Y, LosHalf);
        /// <summary>A shot moving from a to b this tick hits a wall (on the way, or b is in a wall tile).</summary>
        public bool ShotBlocked(float ax, float ay, float bx, float by) => !LineClear(ax, ay, bx, by, LosHalf) || WallAt(bx, by);

        /// <summary>How far a strip from (x, y) along ang reaches before a wall cuts the sight line (Inferno Lane, the quake): found in
        /// 0.3 tile steps up to the reach itself, then narrowed to 0.01 tile, so it ends at the wall face (BLAST-03: it used to stop
        /// anywhere from 0.3 short of the face to 0.1 inside it, and could skip the last step). At least 0.3, as before.</summary>
        public float LaneReach(float x, float y, float ang, float reach)
        {
            float ca = SimMath.Cos(ang), sa = SimMath.Sin(ang);
            return Math.Max(Math.Min(0.3f, reach), Reach(x, y, ca, sa, reach, false));
        }

        /// <summary>How far a straight shot from (x, y) along ang flies before a wall stops it (ShotBlocked), at most reach, to 0.01 tile:
        /// what the aim and the red warning draw for a bolt, hook, needle or boulder (AIM-01).</summary>
        public float ShotReach(float x, float y, float ang, float reach) => Reach(x, y, SimMath.Cos(ang), SimMath.Sin(ang), reach, true);

        // the last free distance along (ca, sa): 0.3 tile steps (the last one exactly at reach), then five halvings between free and blocked
        float Reach(float x, float y, float ca, float sa, float reach, bool shot)
        {
            float free = 0;
            for (int i = 1; free < reach; i++)
            {
                float d = Math.Min(reach, i * 0.3f);
                if (!Cut(x, y, x + ca * d, y + sa * d, shot)) { free = d; continue; }
                float lo = free, hi = d;
                for (int k = 0; k < 5; k++)
                {
                    float mid = (lo + hi) * 0.5f;
                    if (Cut(x, y, x + ca * mid, y + sa * mid, shot)) hi = mid; else lo = mid;
                }
                return lo;
            }
            return reach;
        }

        bool Cut(float ax, float ay, float bx, float by, bool shot) => shot ? ShotBlocked(ax, ay, bx, by) : !LosClear(ax, ay, bx, by);

        /// <summary>How far (x, y) is from the lane's line (the waypoints' polyline).</summary>
        public float LaneDist(float x, float y)
        {
            float best = SimMath.Hypot(LaneX[0] - x, LaneY[0] - y);
            for (int i = 1; i < LaneX.Length; i++)
            {
                float d = SimMath.PtSeg(x, y, LaneX[i - 1], LaneY[i - 1], LaneX[i], LaneY[i]);
                if (d < best) best = d;
            }
            return best;
        }
        /// <summary>The point of the lane's line nearest to (x, y).</summary>
        public void LaneNearest(float x, float y, out float bx, out float by)
        {
            bx = LaneX[0]; by = LaneY[0]; float best = SimMath.Hypot(bx - x, by - y);
            for (int i = 1; i < LaneX.Length; i++)
            {
                float ax = LaneX[i - 1], ay = LaneY[i - 1], dx = LaneX[i] - ax, dy = LaneY[i] - ay, l2 = dx * dx + dy * dy;
                float k = l2 > 0 ? SimMath.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1) : 0;
                float px = ax + dx * k, py = ay + dy * k, d = SimMath.Hypot(px - x, py - y);
                if (d < best) { best = d; bx = px; by = py; }
            }
        }

        // ---------- paths (breadth-first on the tile grid, 8 directions, no corner cutting) ----------
        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 }, DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        readonly int[] prev, queue, seen, back;
        int gen;
        // a search that just failed is not run again for 0.3 s (units that cannot reach their goal used to search every frame)
        readonly Dictionary<long, float> failedAt = new Dictionary<long, float>();

        /// <summary>Fills path with tile centres from (fx, fy) to (gx, gy), the last point being the goal (or the nearest open
        /// tile's centre when the goal is in a wall). False (and an empty path) when there is no way.</summary>
        public bool FindPath(float fx, float fy, float gx, float gy, float now, PathBuf path)
        {
            path.Clear();
            int sx = (int)Math.Floor(fx), sy = (int)Math.Floor(fy), tx = (int)Math.Floor(gx), ty = (int)Math.Floor(gy);
            bool moved = false;
            if (!Walkable(tx, ty))
            {
                int bx = -1, by = -1; float bd = 1e9f;
                for (int y = Math.Max(0, ty - 3); y <= Math.Min(H - 1, ty + 3); y++)
                    for (int x = Math.Max(0, tx - 3); x <= Math.Min(W - 1, tx + 3); x++)
                        if (Walkable(x, y)) { float d = (x - tx) * (x - tx) + (y - ty) * (y - ty); if (d < bd) { bd = d; bx = x; by = y; } }
                if (bx < 0) return false;
                tx = bx; ty = by; moved = true;
            }
            float ex = moved ? tx + 0.5f : gx, ey = moved ? ty + 0.5f : gy;
            if (sx == tx && sy == ty) { path.X[0] = ex; path.Y[0] = ey; path.Count = 1; return true; }
            if (sx < 0 || sy < 0 || sx >= W || sy >= H) return false;
            long key = ((long)(sy * W + sx) << 32) | (uint)(ty * W + tx);
            if (failedAt.TryGetValue(key, out float ft) && now - ft < 0.3f && now >= ft) return false;
            if (++gen == int.MaxValue) { Array.Clear(seen, 0, seen.Length); gen = 1; }
            int start = sy * W + sx, goal = ty * W + tx, qn = 0, head = 0;
            queue[qn++] = start; prev[start] = start; seen[start] = gen;
            while (head < qn)
            {
                int c = queue[head++]; if (c == goal) break;
                int cx = c % W, cy = c / W;
                for (int k = 0; k < 8; k++)
                {
                    int dx = DX[k], dy = DY[k], nx = cx + dx, ny = cy + dy;
                    if (nx < 0 || ny < 0 || nx >= W || ny >= H) continue;
                    int n = ny * W + nx;
                    if (seen[n] == gen || !Walkable(nx, ny)) continue;
                    if (n != goal && (n == TowerTile0 || n == TowerTile1)) continue; // a living tower blocks its tile
                    if (dx != 0 && dy != 0 && (!Walkable(cx + dx, cy) || !Walkable(cx, cy + dy))) continue;
                    prev[n] = c; seen[n] = gen; queue[qn++] = n;
                }
            }
            if (seen[goal] != gen) { failedAt[key] = now; return false; }
            failedAt.Remove(key);
            int len = 0, cc = goal;
            while (cc != start && len < back.Length) { back[len++] = cc; cc = prev[cc]; }
            int take = Math.Min(len, PathBuf.Cap); // always all of it on this small map
            for (int i = 0; i < take; i++) { int t = back[len - 1 - i]; path.X[i] = t % W + 0.5f; path.Y[i] = t / W + 0.5f; }
            if (take == len) { path.X[take - 1] = ex; path.Y[take - 1] = ey; }
            path.Count = take;
            return true;
        }

        internal void ResetPathCache() => failedAt.Clear();
    }
}
