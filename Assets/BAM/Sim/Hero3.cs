using System;

namespace BAM.Sim
{
    /// <summary>Bonk, the caveman (BAM's own hero #8, Rickard 2026-10-26; never call him by the trademarked name he was asked for
    /// as). Melee bruiser with a huge stone club. Auto attack (Rickard 2026-10-26 "extremt långsamt, böja sig bakåt"): the target is
    /// a random enemy in range (D1, as every hero); he leans back for a long wind-up, then smashes the club into the ground in front
    /// of him, toward that target: every enemy in the circle there takes the hit, the target only if it is still inside. Tap, Ground
    /// Slam: a short wind-up where he stands, then every enemy hero and minion round him (clear line, not towers) takes a hit and a
    /// short stun. Drag, Boulder Toss: after a short wind-up a boulder rolls along the aimed line, stops in the first enemy hero or
    /// minion (or a wall), hits it and knocks it back along the line (walls stop the slide) with a short stun. Passive, Heavy Club:
    /// every 4th swing is a power hit.</summary>
    public static class BonkS
    {
        public const float SlamRadius = 2.0f, SlamWindup = 0.3f, SlamCoeff = 1.4f, SlamStun = 0.6f;
        public const float BoulderReach = 5.5f, BoulderWindup = 0.25f, BoulderSpeed = 11, BoulderHitbox = 0.55f, BoulderCoeff = 1.3f;
        public const float KnockDist = 1.5f, KnockTime = 0.2f, KnockStun = 0.4f;
        /// <summary>The slam auto attack: the wind-up (the HeroDef's SpawnDelay), the circle's centre this far in front of him
        /// toward the target, and its radius. Towers are hit only as the picked target.</summary>
        public const float SmashAhead = 1.2f, SmashRadius = 1.3f, SmashFollow = 0.5f; // the club follows its target at most this far (Rickard 2026-10-28)
        public const int PowerEvery = 4;
        public const float PowerBonus = 0.6f;
    }

    public sealed partial class Match
    {
        bool CastBonk(Unit h, float dmg)
        {
            if (Aimed(h, out float aa, out _))
            {   // Boulder Toss: rolls straight along the aim after the wind-up, a red line warns the way
                var p = NewProj(ProjMode.Boulder, h, h.X, h.Y);
                p.Kind = ProjKind.BonkBoulder; p.T = -BonkS.BoulderWindup; p.Ang = aa; p.Speed = BonkS.BoulderSpeed; p.Dmg = dmg * BonkS.BoulderCoeff; p.Radius = BonkS.BoulderHitbox;
                float len = Map.ShotReach(h.X, h.Y, aa, BonkS.BoulderReach); // where the boulder really stops, as its aim shows (BONK-05)
                WarnOf(WarnLine(h, aa, len, BonkS.BoulderHitbox, BonkS.BoulderWindup + len / BonkS.BoulderSpeed), p);
                h.Facing = aa; h.AimT = Time + BonkS.BoulderWindup + 0.2f;
                AddStatus(h, St.Casting, BonkS.BoulderWindup); Channel(h, BonkS.BoulderWindup);
                castX = h.X + SimMath.Cos(aa) * len; castY = h.Y + SimMath.Sin(aa) * len;
                return true;
            }
            // Ground Slam: only with an enemy hero or minion in reach
            if (EnemiesInRange(h, BonkS.SlamRadius, Pick.HeroOrMinion, true, spBuf).Count == 0) return false;
            var s = NewProj(ProjMode.Slam, h, h.X, h.Y);
            s.Kind = ProjKind.BonkSlam; s.T = -BonkS.SlamWindup; s.Dur = BonkS.SlamWindup; s.Radius = BonkS.SlamRadius; s.Dmg = dmg * BonkS.SlamCoeff;
            WarnCircle(h, h.X, h.Y, BonkS.SlamRadius, BonkS.SlamWindup);
            AddStatus(h, St.Casting, BonkS.SlamWindup); Channel(h, BonkS.SlamWindup);
            return true;
        }

        /// <summary>Bonk's auto attack starts: a circle in front of him toward tgt (closer when tgt is closer), landing after the
        /// wind-up; BonkSmash moves it onto tgt where it is then.</summary>
        void BonkSwing(Unit u, Unit tgt, float dmg, bool big)
        {
            float d = SimMath.Dist(u, tgt), ahead = Math.Min(BonkS.SmashAhead, d), ca = SimMath.Cos(u.Facing), sa = SimMath.Sin(u.Facing);
            var p = NewProj(ProjMode.Smash, u, u.X + ca * ahead, u.Y + sa * ahead);
            p.Kind = ProjKind.BonkClub; p.Tgt = tgt; p.Dmg = dmg; p.T = -u.HDef.SpawnDelay; p.Dur = u.HDef.SpawnDelay; p.Radius = BonkS.SmashRadius;
            p.Big = big; p.OnHit = true; p.Ang = u.Facing; p.OX = u.X; p.OY = u.Y; // where he stood: the circle walks with him (BONK-02)
            Emit(EventType.AttackStart, u, tgt, u.HDef.SpawnDelay, (byte)ProjKind.BonkClub, big);
        }

        /// <summary>The club lands: every enemy hero and minion in the circle with a clear line (and the target tower), one auto
        /// attack hit each; the special charges once per slam. A dead Bonk's slam never lands.</summary>
        void BonkSmash(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive) return;
            // he may walk during the wind-up: the circle keeps its place in front of him, so the club lands where his arms reach,
            // never left behind where the swing began (precision audit BONK-02, 2026-10-29: it landed up to 2.95 tiles from him)
            p.X += h.X - p.OX; p.Y += h.Y - p.OY;
            // the club follows its target through the wind-up, at most SmashFollow tiles (closed-test feedback; Rickard chose 0.5 on
            // a card 2026-10-28): a target that barely moved is hit, one that ran away can still dodge the giant's slow swing. It
            // follows the point SmashAhead in front of him on the line to the target, so a sidestep is followed and a straight
            // retreat is not (BONK-10: measured, kept as Rickard chose)
            var t = p.Tgt;
            if (t != null && t.Alive && !t.Has(St.Untargetable))
            {
                float d = SimMath.Dist(h, t);
                if (d <= h.Range + t.R && d > 1e-4f && Map.LosClear(h.X, h.Y, t.X, t.Y))
                {
                    float ahead = Math.Min(BonkS.SmashAhead, d);
                    float nx = h.X + (t.X - h.X) / d * ahead, ny = h.Y + (t.Y - h.Y) / d * ahead, mv = SimMath.Hypot(nx - p.X, ny - p.Y);
                    if (mv > BonkS.SmashFollow) { nx = p.X + (nx - p.X) / mv * BonkS.SmashFollow; ny = p.Y + (ny - p.Y) / mv * BonkS.SmashFollow; }
                    p.X = nx; p.Y = ny;
                    p.Ang = h.Facing = SimMath.Atan2(t.Y - h.Y, t.X - h.X);
                }
            }
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Team == p.Team || u.Has(St.Untargetable) || (u.Kind == UnitKind.Tower && u != p.Tgt)) continue;
                if (SimMath.Dist(u, p.X, p.Y) > p.Radius + u.R || !Map.LosClear(p.X, p.Y, u.X, u.Y)) continue;
                areaBuf.Add(u);
            }
            Unit first = null; float firstDealt = 0;
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                float dealt = DealDamage(h, u, p.Dmg, DmgKind.Auto);
                Emit(EventType.Hit, h, u, dealt, (byte)ProjKind.BonkClub, p.Big, u.X, u.Y, DmgKind.Auto);
                if (first == null || u == p.Tgt) { first = u; firstDealt = dealt; }
            }
            Emit(EventType.Explosion, h, p.Tgt, p.Radius, (byte)ProjKind.BonkClub, p.Big, p.X, p.Y, DmgKind.Auto); // cracks and dust
            if (first != null && h.Alive) OnHeroHit(h, first, p, firstDealt);
        }

        /// <summary>The slam lands where Bonk stood (he cannot walk during the wind-up): every enemy hero and minion within its
        /// radius with a clear line takes the hit and a stun (Moa's shield blocks the stun).</summary>
        void BonkSlam(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || h.Has(St.Stun)) return;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (Hurtable(h, u) && u.Team != p.Team && SimMath.Dist(u, p.X, p.Y) <= BonkS.SlamRadius + u.R && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                SpecialHit(h, u, p.Dmg, ProjKind.BonkSlam, true);
                if (u.Alive) AddStatus(u, St.Stun, BonkS.SlamStun, 0, 0, h);
            }
            Emit(EventType.Explosion, h, null, BonkS.SlamRadius, (byte)ProjKind.BonkSlam, true, p.X, p.Y);
        }

        /// <summary>The boulder, swept like Tikta's line needles: the first enemy along this tick's path is hit if no wall stands
        /// between; else it stops at a wall or after its reach.</summary>
        void BoulderUpdate(Proj p, float dt)
        {
            float ox = p.X, oy = p.Y, step = p.Speed * dt, len = Math.Max(0, Math.Min(step, BonkS.BoulderReach - p.D));
            var u = SweptHit(p.Src, p.Team, ox, oy, p.Ang, len, BonkS.BoulderHitbox, out float at);
            if (u != null)
            {
                float hx = ox + SimMath.Cos(p.Ang) * at, hy = oy + SimMath.Sin(p.Ang) * at;
                if (!Map.ShotBlocked(ox, oy, hx, hy))
                {
                    p.X = hx; p.Y = hy; p.D += at; p.Done = true;
                    bool free = CcImmune(u);
                    SpecialHit(p.Src, u, p.Dmg, p.Kind, true);
                    if (u.Alive && !free) Knock(p.Src, u, p.Ang);
                    return;
                }
            }
            p.X += SimMath.Cos(p.Ang) * step; p.Y += SimMath.Sin(p.Ang) * step; p.D += step;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            if (p.D >= BonkS.BoulderReach) p.Done = true;
        }

        /// <summary>u slides KnockDist the way ang points over KnockTime, stunned a little longer; walls stop the slide.</summary>
        void Knock(Unit src, Unit u, float ang)
        {
            float sp = BonkS.KnockDist / BonkS.KnockTime;
            u.KnockVX = SimMath.Cos(ang) * sp; u.KnockVY = SimMath.Sin(ang) * sp; u.KnockT = BonkS.KnockTime; u.KnockKeg = null;
            AddStatus(u, St.Stun, BonkS.KnockStun, 0, 0, src);
            Emit(EventType.Knock, src, u, BonkS.KnockTime, (byte)ProjKind.BonkBoulder, true, u.X + u.KnockVX * BonkS.KnockTime, u.Y + u.KnockVY * BonkS.KnockTime);
        }

        /// <summary>One tick of a knockback slide: it moves while the way is free and ends at the first wall.</summary>
        void KnockStep(Unit u, float dt)
        {
            float left = u.KnockT * SimMath.Hypot(u.KnockVX, u.KnockVY); // tiles of the slide still to go (Hilde's Wall Crash)
            float t = Math.Min(dt, u.KnockT); u.KnockT -= dt;
            float nx = u.X + u.KnockVX * t, ny = u.Y + u.KnockVY * t;
            if (Map.Free(nx, ny, u.R)) { u.X = nx; u.Y = ny; u.MovedAt = Time; }
            else { if (u.KnockKeg != null) KegWallHit(u, nx, ny, left); u.KnockT = 0; }
            if (u.KnockT <= 0) u.KnockKeg = null;
        }
    }
}
