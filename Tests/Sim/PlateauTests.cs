using System;

namespace BAM.Sim
{
    // Plateau, the third map (Rickard 2026-11-18: "bygg the plateau från abc2"): cliff corners of void, a lane that swings round
    // the left of the middle grass field, and two jump pads that throw 3 rows over the wall bar on the cliff side.
    public sealed partial class Match
    {
        static void PlateauChecks()
        {
            var a = new Arena(MapId.Plateau);
            int voids = 0, walls = 0;
            for (int i = 0; i < a.Tiles.Length; i++) { if (a.Tiles[i] == Arena.Void) voids++; if (a.Tiles[i] == Arena.Wall) walls++; }
            Check(a.Id == MapId.Plateau && a.W == 15 && a.H == 35, "PLATEAU: 15 x 35 like the other maps");
            Check(voids > 0 && a.Tile(0, 0) == Arena.Void && a.Tile(0, 34) == Arena.Void && !a.Walkable(1, 3), $"PLATEAU: the cliff corners are void ({voids} tiles)");
            bool mirrored = true;
            for (int y = 0; y < a.H; y++) for (int x = 0; x < a.W; x++) mirrored &= a.Tile(x, y) == a.Tile(x, a.H - 1 - y);
            Check(mirrored, "PLATEAU: both halves are the same map, mirrored at the middle row");
            Check(a.TowerX[0] == 7.5f && a.TowerY[0] == 29.5f && a.TowerX[1] == 7.5f && a.TowerY[1] == 5.5f, "PLATEAU: the towers stand where Runes' do");
            Check(a.LaneX.Length == 6 && a.LaneX[2] == 2.5f && a.LaneX[3] == 2.5f, "PLATEAU: the lane swings left round the grass field");
            bool laneOk = true;
            for (int i = 0; i < a.LaneX.Length; i++) laneOk &= a.Walkable((int)a.LaneX[i], (int)a.LaneY[i]);
            Check(laneOk, "PLATEAU: every lane waypoint stands on open ground");
            var path = new PathBuf();
            Check(a.FindPath(a.MinionX[0], a.MinionY[0], a.TowerX[1], a.TowerY[1], 0, path) && path.Count > 20, "PLATEAU: a path from team 0's base to the enemy tower");
            Check(a.PadCount == 2, $"PLATEAU: two jump pads ({a.PadCount})");
            for (int i = 0; i < a.PadCount; i++)
            {
                float d = SimMath.Hypot(a.PadX[i] - a.TowerX[i == 0 ? 1 : 0], a.PadY[i] - a.TowerY[i == 0 ? 1 : 0]);
                d = Math.Min(d, Math.Min(SimMath.Hypot(a.PadX[i] - a.TowerX[0], a.PadY[i] - a.TowerY[0]), SimMath.Hypot(a.PadX[i] - a.TowerX[1], a.PadY[i] - a.TowerY[1])));
                Check(d > 5.5f, $"PLATEAU: pad {i} lies out of tower reach ({d:0.0} tiles)");
                Check(Math.Abs(a.PadToY[i] - a.PadY[i]) == Arena.PlateauPadThrow && a.Walkable((int)a.PadToX[i], (int)a.PadToY[i]), $"PLATEAU: pad {i} lands 3 rows on open ground");
                Check(!a.LineClear(a.PadX[i], a.PadY[i], a.PadToX[i], a.PadToY[i]), $"PLATEAU: pad {i} throws over the wall bar");
            }
            // the old maps are as they were
            Check(new Arena(MapId.Grove).LaneX.Length == 3 && new Arena(MapId.Runes).LaneX.Length == 3, "PLATEAU: Runes and Grove keep their straight lane");

            // whole bot matches end on Plateau, and a minion wave walks the bent lane
            int ended = 0, wentLeft = 0;
            for (int s = 1; s <= 6; s++)
            {
                var m = Create(new MatchConfig { PlayerHero = (HeroId)(s % 6), Difficulty = Difficulty.Hard, Seed = 4100 + s, PlayerIsBot = true, Map = MapId.Plateau });
                bool left = false;
                while (m.State != MatchState.Over && m.Time < 900)
                {
                    m.Step(PlayerInput.None);
                    if (!left) foreach (var u in m.Units) if (u.Kind == UnitKind.Minion && u.Alive && u.X < 4f && u.Y > 15 && u.Y < 20) { left = true; break; }
                }
                if (m.State == MatchState.Over) ended++;
                if (left) wentLeft++;
            }
            Check(ended == 6, $"PLATEAU: Hard bot matches end ({ended}/6)");
            Check(wentLeft == 6, $"PLATEAU: minions take the left way round the field ({wentLeft}/6)");
        }
    }
}
