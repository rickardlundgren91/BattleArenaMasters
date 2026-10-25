using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Walking and bumping (the old game's SimCombat speedOf, moveUnit and separate).
    public sealed partial class Match
    {
        /// <summary>Two minions keep at least this far apart (for two small ones; it grows with their radii), so one model
        /// never hides another on screen.</summary>
        const float MinionGap = 0.45f;
        readonly List<Unit> mobBuf = new List<Unit>(96);

        /// <summary>Tiles per second now: the base plus the flat changes, times one plus the percent changes, between 0.5 and 6.</summary>
        public float SpeedOf(Unit u)
        {
            if (u.Kind == UnitKind.Tower) return 0;
            float flat = 0, pct = 0;
            for (int i = 0; i < u.StCount; i++)
            {
                ref var s = ref u.Sts[i];
                if (s.Type == St.SlowFlat || s.Type == St.SpeedFlat) flat += s.V;
                else if (s.Type == St.SlowPct || s.Type == St.SpeedPct) pct += s.V;
            }
            return SimMath.Clamp((u.BaseSpeed + flat) * (1 + pct), 0.5f, 6);
        }

        /// <summary>Walks u one tick along (dx, dy): a longer vector is cut to full speed, a shorter one walks slower. Slides
        /// along walls; a root holds it still.</summary>
        internal void MoveUnit(Unit u, float dx, float dy, float dt)
        {
            if (dx == 0 && dy == 0) return;
            if (u.Has(St.Root)) return;
            float sp = SpeedOf(u), m = SimMath.Hypot(dx, dy);
            if (m > 1) { dx /= m; dy /= m; }
            float ox = u.X, oy = u.Y, nx = ox + dx * sp * dt, ny = oy + dy * sp * dt;
            if (MoveOk(u, nx, ny)) { u.X = nx; u.Y = ny; }
            else if (MoveOk(u, nx, oy)) u.X = nx;
            else if (MoveOk(u, ox, ny)) u.Y = ny;
            float moved = SimMath.Hypot(u.X - ox, u.Y - oy);
            if (moved > 0)
            {
                if (!(u.AimT > Time)) u.Facing = SimMath.Atan2(dy, dx);
                u.WalkT += moved; u.MovedAt = Time;
            }
        }

        /// <summary>Fenn's spectral wolf passes walls and bodies and only stays inside the map.</summary>
        bool MoveOk(Unit u, float x, float y) => u.Kind == UnitKind.Wolf
            ? x > 0.3f && y > 0.3f && x < Map.W - 0.3f && y < Map.H - 0.3f
            : Map.Free(x, y, u.R);

        /// <summary>Moves u to the nearest free spot (rings of 0.5 tiles, 16 directions) when it ended up inside a wall.</summary>
        internal void Unstick(Unit u)
        {
            if (Map.Free(u.X, u.Y, u.R)) return;
            for (float r = 0.5f; r < 6; r += 0.5f)
                for (int a = 0; a < 16; a++)
                {
                    float x = u.X + SimMath.Cos(a / 16f * SimMath.PI * 2) * r, y = u.Y + SimMath.Sin(a / 16f * SimMath.PI * 2) * r;
                    if (Map.Free(x, y, u.R)) { u.X = x; u.Y = y; return; }
                }
        }

        /// <summary>Bodies push apart once per tick. Towers push everyone out of their body; heroes (and Fenn's clone) walk
        /// through heroes; minions yield to heroes and push each other softly; the spectral wolf touches nobody.</summary>
        void Separate()
        {
            for (int t = 0; t < 2; t++)
            {
                var tw = Towers[t];
                if (tw == null || !tw.Alive) continue;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Kind == UnitKind.Tower || u.Kind == UnitKind.Wolf) continue;
                    float d = SimMath.Dist(tw, u), min = tw.R + u.R;
                    if (d >= min) continue;
                    float nx = (u.X - tw.X) / (d != 0 ? d : 1), ny = (u.Y - tw.Y) / (d != 0 ? d : 1), side = u.X >= tw.X ? 1 : -1;
                    if (u.Kind == UnitKind.Minion && Math.Abs(u.X - tw.X) < 0.08f) side = u.Serial % 2 == 0 ? 1 : -1; // straight behind a tower: one fixed side
                    nx += -ny * side * 0.6f;
                    float m = SimMath.Hypot(nx, ny); if (m == 0) m = 1;
                    float px = u.X + nx / m * (min - d + 0.01f), py = u.Y + ny / m * (min - d + 0.01f);
                    if (Map.Free(px, py, u.R)) { u.X = px; u.Y = py; }
                }
            }
            mobBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.Kind != UnitKind.Tower && u.Kind != UnitKind.Wolf) mobBuf.Add(u);
            }
            for (int i = 0; i < mobBuf.Count; i++)
                for (int j = i + 1; j < mobBuf.Count; j++)
                {
                    var a = mobBuf[i]; var b = mobBuf[j];
                    if (a.Kind == UnitKind.Hero && b.Kind == UnitKind.Hero) continue;
                    bool am = a.Kind == UnitKind.Minion, bm = b.Kind == UnitKind.Minion;
                    float d = SimMath.Dist(a, b), min = (a.R + b.R) * 0.9f;
                    if (am && bm) min = Math.Max(min, MinionGap * (a.R + b.R) / 0.29f);
                    if (!(d < min && d > 1e-4f)) continue;
                    float push = (min - d) / 2, nx = (b.X - a.X) / d, ny = (b.Y - a.Y) / d, wa = 1, wb = 1;
                    if (am && bm) push *= a.Team == b.Team ? 0.5f : 0.7f; // minions correct only part of the overlap per tick
                    else if (am) { wa = 2; wb = 0; }                       // a minion makes all the room for a hero
                    else if (bm) { wa = 0; wb = 2; }
                    if (wa > 0 && Map.Free(a.X - nx * push * wa, a.Y - ny * push * wa, a.R)) { a.X -= nx * push * wa; a.Y -= ny * push * wa; }
                    if (wb > 0 && Map.Free(b.X + nx * push * wb, b.Y + ny * push * wb, b.R)) { b.X += nx * push * wb; b.Y += ny * push * wb; }
                }
        }
    }
}
