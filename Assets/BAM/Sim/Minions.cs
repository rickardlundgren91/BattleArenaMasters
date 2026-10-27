using System;

namespace BAM.Sim
{
    // Lane minions (the old game's SimMinionAI.cs, SimCreepLeash.cs and the aiMinion wrapper in SimAI.cs): a small state machine
    // with stable targets and steering, like MLBB minions (march in a wave, stop and fight, spread round the target, go back to
    // the lane).
    //   Advance    follows the lane waypoints towards the enemy tower
    //   Approach   walks to its own attack spot round the target (a bearing fixed when the target was picked plus a per-minion
    //              offset, so five melee minions fan out round one enemy instead of all aiming at the same point)
    //   Attack     stands still and hits; enters at 92 % of reach and leaves above 100 % (no flicker at the border)
    //   Reposition a short sidestep after the minion made no progress for 0.45 s (blocked by friends, a corner)
    //   Return     more than 1.3 tiles off the lane with nothing to fight: walks back until 0.8 tiles from it, then Advance
    // The 0.2 s target scan is a moment inside these states, not a state of its own (that would break their hysteresis).
    // Targets are kept for MnLock s and replaced only by something clearly better (score < MnSwitch x the old one).
    // Score = distance, +MnHeroPen for a hero (minions prefer minions) unless that hero hit a friendly hero in the last MnAggroT s,
    // -MnSiegeTower for a siege minion looking at a tower. The attack itself (Combat.UpdateAttack) uses MbTgt when it is in reach.
    // The leash: a minion never walks after a hero in grass, or after anything more than MinionMaxToLane from the lane (a target
    // already in reach is still hit).
    public sealed partial class Match
    {
        internal const float MnScan = 0.2f;
        const float MnLock = 0.9f, MnSwitch = 0.7f, MnSmooth = 10f, MnSepW = 0.9f, MnAggroT = 2f;
        const float MnHeroPen = 0.6f, MnSiegeTower = 1.5f;
        const float MnEnter = 0.92f, MnLeave = 1.0f, MnSpot = 0.8f; // the attack spot is at 80 % of reach

        void AiMinion(Unit m, float dt)
        {
            m.AiT -= dt;
            if (m.TutStatic)
            {
                // the guided match's practice target stands on its own spot
                if (SimMath.Hypot(m.SpawnX - m.X, m.SpawnY - m.Y) > 0.1f) MoveUnit(m, m.SpawnX - m.X, m.SpawnY - m.Y, dt);
                return;
            }
            AiMinionLane(m, dt);
        }

        /// <summary>May minion m walk after enemy u? Not after a hero in a bush, not after anything far from the lane.</summary>
        bool CreepMayChase(Unit m, Unit u)
        {
            if (u.Kind == UnitKind.Hero && Map.InGrass(u.X, u.Y)) return false;
            return Map.LaneDist(u.X, u.Y) <= Rules.MinionMaxToLane;
        }

        bool InReach(Unit m, Unit u) => SimMath.Dist(m, u) <= m.Range + u.R && Map.LosClear(m, u);

        /// <summary>Did enemy hero h hurt one of m's living heroes in the last MnAggroT s?</summary>
        bool MnHeroAggro(Unit m, Unit h)
        {
            if (h.HeroIndex < 0) return false; // a clone's hits count for Fenn, never for the clone
            for (int i = 0; i < Units.Count; i++)
            {
                var o = Units[i];
                if (o.Kind == UnitKind.Hero && o.Alive && o.Team == m.Team && Time - o.LastHitBy[h.HeroIndex] < MnAggroT) return true;
            }
            return false;
        }

        float MnScore(Unit m, Unit t)
        {
            float s = SimMath.Dist(m, t);
            if (t.Kind == UnitKind.Hero && !MnHeroAggro(m, t)) s += MnHeroPen;
            if (t.Kind == UnitKind.Tower && m.Minion == MinionKind.Siege) s -= MnSiegeTower;
            return s;
        }

        bool MnValid(Unit m, Unit t)
        {
            if (t == null || !t.Alive || t.Has(St.Untargetable) || t.Team == m.Team || !IsVisibleTo(t, m.Team)) return false;
            float d = SimMath.Dist(m, t);
            if (d > Rules.MinionVision * 1.25f + t.R) return false;
            if (d <= m.Range + t.R && Map.LosClear(m, t)) return true;
            return CreepMayChase(m, t);
        }

        void MnAcquire(Unit m)
        {
            var c = EnemiesInRange(m, Rules.MinionVision, Pick.Any, false, aiBuf);
            Unit best = null; float bs = float.MaxValue;
            for (int i = 0; i < c.Count; i++)
            {
                var u = c[i];
                if (!InReach(m, u) && !CreepMayChase(m, u)) continue;
                float s = MnScore(m, u);
                if (s < bs) { bs = s; best = u; }
            }
            var cur = m.MbTgt;
            if (MnValid(m, cur) && (best == null || best == cur || Time < m.MbLockUntil || bs >= MnScore(m, cur) * MnSwitch)) return; // keep it
            if (best == cur) return;
            m.MbTgt = best; m.MbLockUntil = Time + MnLock; m.MbGx = float.NaN; m.Path.Clear();
            if (best != null) m.MbAng = SimMath.Atan2(m.Y - best.Y, m.X - best.X) + ((m.Serial * 7) % 9 - 4) * 0.22f; // its own spot round the target
        }

        void AiMinionLane(Unit m, float dt)
        {
            if (m.MbTgt != null && !m.MbTgt.Alive) m.MbTgt = null;
            m.MbScanT -= dt;
            if (m.MbScanT <= 0) { m.MbScanT = MnScan; MnAcquire(m); }
            var t = m.MbTgt;
            bool hasGoal = false; float gx = 0, gy = 0, sp = SpeedOf(m);

            // progress since the last tick: stuck detection (only while it was trying to walk)
            if (!float.IsNaN(m.MbPx) && (m.MbState == MinionState.Advance || m.MbState == MinionState.Approach || m.MbState == MinionState.Return))
            {
                if (SimMath.Hypot(m.X - m.MbPx, m.Y - m.MbPy) < 0.3f * sp * dt) m.MbStuckT += dt; else m.MbStuckT = Math.Max(0, m.MbStuckT - 2 * dt);
                if (m.MbStuckT > 0.45f)
                {
                    m.MbStuckT = 0; m.MbSideT = 0.7f;
                    m.MbSideSign = m.MbSideSign == 0 ? (m.Serial % 2 == 0 ? 1 : -1) : -m.MbSideSign;
                    m.Path.Clear(); m.MbGx = float.NaN;
                }
            }
            m.MbPx = m.X; m.MbPy = m.Y;

            if (t != null)
            {
                float reach = m.Range + t.R, d = SimMath.Dist(m, t); bool los = Map.LosClear(m, t);
                if (m.MbState == MinionState.Attack ? (d <= reach * MnLeave && los) : (d <= reach * MnEnter && los)) m.MbState = MinionState.Attack;
                else
                {
                    m.MbState = m.MbSideT > 0 ? MinionState.Reposition : MinionState.Approach;
                    float sx = t.X + SimMath.Cos(m.MbAng) * reach * MnSpot, sy = t.Y + SimMath.Sin(m.MbAng) * reach * MnSpot;
                    hasGoal = true;
                    if (los && Map.Free(sx, sy, m.R) && Map.LaneDist(sx, sy) <= 1.3f) { gx = sx; gy = sy; } // the spot stays on the road
                    else { gx = t.X; gy = t.Y; }
                }
            }
            else
            {
                float ld = Map.LaneDist(m.X, m.Y);
                m.MbState = m.MbState == MinionState.Return ? (ld > 0.8f ? MinionState.Return : MinionState.Advance) : (ld > 1.3f ? MinionState.Return : MinionState.Advance);
                if (m.MbSideT > 0) m.MbState = MinionState.Reposition;
                int n = Map.LaneX.Length;
                if (m.Wp < n)
                {
                    int wi = m.Team == Teams.Player ? m.Wp : n - 1 - m.Wp; // team 1 walks the lane the other way
                    if (SimMath.Hypot(Map.LaneX[wi] - m.X, Map.LaneY[wi] - m.Y) < 0.8f) m.Wp++;
                }
                hasGoal = true;
                if (m.Wp < n) { int wi = m.Team == Teams.Player ? m.Wp : n - 1 - m.Wp; gx = Map.LaneX[wi]; gy = Map.LaneY[wi]; }
                else { var tw = Towers[Teams.Other(m.Team)]; gx = tw.X; gy = tw.Y; }
            }
            if (m.MbSideT > 0) m.MbSideT -= dt;

            float k = 1 - SimMath.Exp(-dt * MnSmooth);
            if (m.MbState == MinionState.Attack || !hasGoal)
            {
                if (m.MbState == MinionState.Attack && !(m.AimT > Time)) m.Facing = SimMath.Atan2(t.Y - m.Y, t.X - m.X);
                m.MbDx -= m.MbDx * k; m.MbDy -= m.MbDy * k; return; // stands still while it fights
            }

            // a path to the goal, made again twice a second or when the goal has moved more than a tile
            // (MbGx = NaN means no path yet; a failed search leaves it NaN so it is tried again next tick, as in the old game)
            if (m.AiT <= 0 || float.IsNaN(m.MbGx) || SimMath.Hypot(gx - m.MbGx, gy - m.MbGy) > 1f)
            {
                m.AiT = 0.5f + Rng.Range(0, 0.15f);
                bool ok = Map.FindPath(m.X, m.Y, gx, gy, Time, m.Path);
                if (ok) { m.MbGx = gx; m.MbGy = gy; } else m.MbGx = float.NaN;
            }
            float tx = gx, ty = gy;
            if (m.Path.Has)
            {
                float nx = m.Path.X[m.Path.Index], ny = m.Path.Y[m.Path.Index];
                if (SimMath.Hypot(nx - m.X, ny - m.Y) < 0.25f) { m.Path.Index++; if (m.Path.Has) { nx = m.Path.X[m.Path.Index]; ny = m.Path.Y[m.Path.Index]; } }
                tx = nx; ty = ny;
            }
            float dx = tx - m.X, dy = ty - m.Y, dl = SimMath.Hypot(dx, dy);
            if (dl < 0.05f) { m.MbDx -= m.MbDx * k; m.MbDy -= m.MbDy * k; return; }
            dx /= dl; dy /= dl;

            // keep apart from friends (minions spread, and make room for friendly heroes); enemies are fought, not avoided.
            // Fenn's wolf passes through bodies, so nobody steers round it.
            float sepX = 0, sepY = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                var o = Units[i];
                if (o == m || !o.Alive || o.Kind == UnitKind.Tower || o.Kind == UnitKind.Wolf || o.Team != m.Team) continue;
                float ox = m.X - o.X, oy = m.Y - o.Y, dd = SimMath.Hypot(ox, oy);
                bool hero = o.Kind == UnitKind.Hero;
                float rad = hero ? m.R + o.R + 0.7f : (m.R + o.R) * 1.6f + 0.1f;
                if (dd >= rad || dd < 1e-4f) continue;
                float w = (rad - dd) / rad * (hero ? 1.5f : 1f); sepX += ox / dd * w; sepY += oy / dd * w;
            }
            // towers are obstacles too: slide round them instead of walking into Separate's push-out (the spawn sits behind the own tower)
            bool nearTower = false;
            for (int ti = 0; ti < 2; ti++)
            {
                var o = Towers[ti];
                if (o == null || !o.Alive || o == t) continue;
                float ox = m.X - o.X, oy = m.Y - o.Y, dd = SimMath.Hypot(ox, oy), rad = o.R + m.R + 0.45f;
                if (dd >= rad || dd < 1e-4f) continue;
                nearTower = true;
                float w = (rad - dd) / rad * 2f, rx = ox / dd, ry = oy / dd, tx2 = -ry, ty2 = rx;
                if (m.MbTowerSide == 0) // picked once, so a minion straight behind the tower does not flip sides
                {
                    float side = tx2 * dx + ty2 * dy;
                    m.MbTowerSide = side > 0.05f ? 1 : side < -0.05f ? -1 : (m.Serial % 2 == 0 ? 1 : -1);
                }
                tx2 *= m.MbTowerSide; ty2 *= m.MbTowerSide;
                dx += rx * w * 0.5f + tx2 * w * 2.2f; dy += ry * w * 0.5f + ty2 * w * 2.2f;
            }
            if (!nearTower) m.MbTowerSide = 0;
            dx += sepX * MnSepW; dy += sepY * MnSepW;
            if (t != null) // fighting keeps to the road
            {
                float ld = Map.LaneDist(m.X, m.Y);
                if (ld > 0.9f)
                {
                    Map.LaneNearest(m.X, m.Y, out float lx, out float ly);
                    float pl = SimMath.Hypot(lx - m.X, ly - m.Y);
                    if (pl > 1e-3f) { float w = Math.Min(1f, (ld - 0.9f) * 1.2f); dx += (lx - m.X) / pl * w; dy += (ly - m.Y) / pl * w; }
                }
            }
            if (m.MbSideT > 0)
            {
                float a = m.MbSideSign * 1.05f, c = SimMath.Cos(a), s = SimMath.Sin(a);
                float rx = dx * c - dy * s, ry = dx * s + dy * c; dx = rx; dy = ry;
            }
            float l = SimMath.Hypot(dx, dy);
            if (l > 1e-4f) { dx /= l; dy /= l; }
            m.MbDx += (dx - m.MbDx) * k; m.MbDy += (dy - m.MbDy) * k;
            if (SimMath.Hypot(m.MbDx, m.MbDy) > 0.05f) MoveUnit(m, m.MbDx, m.MbDy, dt);
        }
    }
}
