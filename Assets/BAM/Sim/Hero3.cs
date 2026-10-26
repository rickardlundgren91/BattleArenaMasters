using System;

namespace BAM.Sim
{
    /// <summary>Bonk, the caveman (BAM's own hero #8, Rickard 2026-10-26; never call him by the trademarked name he was asked for
    /// as). Melee bruiser with a stone club. Auto attack: a club swing (D1: a random target in range, as every hero). Tap, Ground
    /// Slam: a short wind-up where he stands, then every enemy hero and minion round him (clear line, not towers) takes a hit and a
    /// short stun. Drag, Boulder Toss: after a short wind-up a boulder rolls along the aimed line, stops in the first enemy hero or
    /// minion (or a wall), hits it and knocks it back along the line (walls stop the slide) with a short stun. Passive, Heavy Club:
    /// every 4th swing is a power hit.</summary>
    public static class BonkS
    {
        public const float SlamRadius = 2.0f, SlamWindup = 0.3f, SlamCoeff = 1.4f, SlamStun = 0.6f;
        public const float BoulderReach = 5.5f, BoulderWindup = 0.25f, BoulderSpeed = 11, BoulderHitbox = 0.55f, BoulderCoeff = 1.3f;
        public const float KnockDist = 1.5f, KnockTime = 0.2f, KnockStun = 0.4f;
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
                float len = Map.LaneReach(h.X, h.Y, aa, BonkS.BoulderReach);
                WarnLine(h, aa, len, BonkS.BoulderHitbox, BonkS.BoulderWindup + len / BonkS.BoulderSpeed);
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
            u.KnockVX = SimMath.Cos(ang) * sp; u.KnockVY = SimMath.Sin(ang) * sp; u.KnockT = BonkS.KnockTime;
            AddStatus(u, St.Stun, BonkS.KnockStun, 0, 0, src);
            Emit(EventType.Knock, src, u, BonkS.KnockTime, (byte)ProjKind.BonkBoulder, true, u.X + u.KnockVX * BonkS.KnockTime, u.Y + u.KnockVY * BonkS.KnockTime);
        }

        /// <summary>One tick of a knockback slide: it moves while the way is free and ends at the first wall.</summary>
        void KnockStep(Unit u, float dt)
        {
            float t = Math.Min(dt, u.KnockT); u.KnockT -= dt;
            float nx = u.X + u.KnockVX * t, ny = u.Y + u.KnockVY * t;
            if (Map.Free(nx, ny, u.R)) { u.X = nx; u.Y = ny; }
            else u.KnockT = 0;
        }
    }
}
