using System;

namespace BAM.Sim
{
    /// <summary>Hilde, the siege support (Simon's Hilde 2.0 from XYZ main, handed over 2026-10-28; numbers locked by Simon, XYZ
    /// SimHeroes6.cs). Auto attack: a short cannon shell. Tap, Powder Burst: a small quick keg at a random enemy hero or minion in
    /// reach (no hero first): 0.4 s fuse, radius 1, 90 % on heroes, 150 % on minions, never the tower, heroes knocked 0.75 tiles away
    /// from its middle. Drag, Powder Keg (Hilde 2.0 Breach Charge): the big keg placed where she aims: 1 s fuse, radius 1.5, 130 % on
    /// heroes, 200 % on minions and the tower (the only form that hurts it), every hero it hits knocked 1.5 tiles the way she aimed.
    /// Passive, Wall Crash: a keg knockback that ends in a wall hurts 7 % of max health (the old wallSlam) and stuns that hero 0.4 s,
    /// one stun per keg. BAM rule (Rickard 2026-10-28, "attacker ska inte gå genom väggar"): a keg is thrown over walls like Blast's
    /// bomb, but its blast only reaches enemies with a clear line from it.</summary>
    public static class HildeS
    {
        /// <summary>Both kegs reach her attack range + 1 (XYZ kegReach).</summary>
        public const float Reach = 3.2f + 1;
        public const float KegDelay = 1, KegRadius = 1.5f, KegHeroCoeff = 1.3f, KegStructureCoeff = 2, KegKnock = 1.5f;
        public const float BurstDelay = 0.4f, BurstRadius = 1.0f, BurstHeroCoeff = 0.9f, BurstStructureCoeff = 1.5f, BurstKnock = 0.75f;
        public const float CastTime = 0.3f, KnockSpeed = 9;
        /// <summary>Wall Crash: the stun, the wall's damage as a share of max health, and how much slide must be left for a crash.</summary>
        public const float WallStun = 0.4f, WallHurt = 0.07f, WallMinLeft = 0.15f;
    }

    /// <summary>One keg blast's Wall Crash, shared by every hero it knocked: at most one stun per blast.</summary>
    internal sealed partial class KegCrash { internal Unit By; internal bool Stunned; }

    public sealed partial class Match
    {
        bool CastHilde(Unit h, float dmg)
        {
            float x, y; Unit t = null; bool drag = Aimed(h, out float aa, out float ak);
            if (drag) AimPoint(h, aa, ak, HildeS.Reach, out x, out y); // at least 1.5 tiles out, so even a very short drag has its direction
            else
            {
                var l = EnemiesInRange(h, HildeS.Reach, Pick.HeroOrMinion, false, spBuf); // thrown in an arc: no clear line needed
                if (l.Count == 0) return false;
                t = PickRandom(l);
                LeadAim(h, t, Flight.Const, HildeS.BurstDelay * 0.5f, 0, out x, out y);
            }
            var k = NewProj(ProjMode.Keg, h, x, y);
            k.Kind = drag ? ProjKind.HildeKeg : ProjKind.HildeBurst; k.Dmg = dmg; k.Big = drag;
            k.Dur = drag ? HildeS.KegDelay : HildeS.BurstDelay; k.T = -k.Dur; k.Radius = drag ? HildeS.KegRadius : HildeS.BurstRadius;
            k.Ang = drag ? aa : 0; // Breach Charge: the aim is kept on the keg at the cast, so nobody moving during the fuse changes it
            h.Facing = SimMath.Atan2(y - h.Y, x - h.X); h.AimT = Time + HildeS.CastTime + 0.1f;
            AddStatus(h, St.Casting, HildeS.CastTime); AddStatus(h, St.Disarm, HildeS.CastTime);
            castX = x; castY = y; castTgt = t;
            return true;
        }

        /// <summary>The keg goes off: every enemy in its radius with a clear line from it takes the hit (the tower only from the
        /// big keg); every enemy hero hit and not CC immune slides away. After Hilde's death it still goes off (as passive damage).</summary>
        void KegBlast(Proj p)
        {
            var h = p.Src;
            bool big = p.Kind == ProjKind.HildeKeg;
            var dk = h.Alive ? DmgKind.Special : DmgKind.Passive;
            float heroC = big ? HildeS.KegHeroCoeff : HildeS.BurstHeroCoeff, otherC = big ? HildeS.KegStructureCoeff : HildeS.BurstStructureCoeff;
            float knock = big ? HildeS.KegKnock : HildeS.BurstKnock;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Team == p.Team || u.Has(St.Untargetable) || (u.Kind == UnitKind.Tower && !big)) continue;
                if (SimMath.Dist(u, p.X, p.Y) > p.Radius + u.R || !Map.LosClear(p.X, p.Y, u.X, u.Y)) continue;
                areaBuf.Add(u);
            }
            KegCrash crash = null;
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                bool hero = u.Kind == UnitKind.Hero, free = CcImmune(u);
                SpecialHit(h, u, p.Dmg * (hero ? heroC : otherC), p.Kind, big, dk);
                if (!hero || !u.Alive || free || KnockImmune(u)) continue;
                float dx, dy;
                if (big) { dx = SimMath.Cos(p.Ang); dy = SimMath.Sin(p.Ang); }
                else
                {   // away from the blast's middle (from Hilde when it went off right on top of the hero)
                    dx = u.X - p.X; dy = u.Y - p.Y; float d = SimMath.Hypot(dx, dy);
                    if (d < 0.05f) { dx = u.X - h.X; dy = u.Y - h.Y; d = SimMath.Hypot(dx, dy); }
                    if (d < 0.05f) { dx = 1; dy = 0; d = 1; }
                    dx /= d; dy /= d;
                }
                InterruptSpecial(u); // a knockback cuts a special under way
                if (crash == null) crash = new KegCrash { By = h };
                float time = knock / HildeS.KnockSpeed; u.KnockFreeAt = Time + Rules.KnockCooldown;
                u.KnockVX = dx * HildeS.KnockSpeed; u.KnockVY = dy * HildeS.KnockSpeed; u.KnockT = time; u.KnockKeg = crash; u.KnockRoll = null;
                Emit(EventType.Knock, h, u, time, (byte)p.Kind, big, u.X + dx * knock, u.Y + dy * knock);
            }
            Emit(EventType.Explosion, h, null, p.Radius, (byte)p.Kind, big, p.X, p.Y, dk);
        }

        /// <summary>A keg slide stopped at (nx, ny) with left tiles still to go: with more than 0.15 left the wall hurts (7 % of max
        /// health, not a direct hit) and, against a real wall (not the map's edge) when this keg has not stunned anyone yet, Wall
        /// Crash stuns the hero. A CC-immune hero (Moa's shield, as it was before the wall's damage) is not stunned and does not use
        /// up the keg's stun.</summary>
        void KegWallHit(Unit u, float nx, float ny, float left)
        {
            var c = u.KnockKeg; var h = c.By;
            if (left <= HildeS.WallMinLeft || !u.Alive || u.Kind == UnitKind.Tower) return;
            bool free = CcImmune(u);
            DealDamage(h, u, u.MaxHp * HildeS.WallHurt, h.Alive ? DmgKind.Special : DmgKind.Passive, DmgFlags.NotDirect);
            if (c.Stunned || free || !u.Alive || u.Kind != UnitKind.Hero || u.IsClone || u.Team == h.Team || u.Has(St.Untargetable) || !WallNear(nx, ny, u.R)) return;
            if (AddStatus(u, St.Stun, HildeS.WallStun, 0, 0, h) < 0) return;
            c.Stunned = true;
            float d = SimMath.Hypot(u.KnockVX, u.KnockVY), wx = d > 0 ? u.KnockVX / d : 0, wy = d > 0 ? u.KnockVY / d : 0;
            Emit(EventType.WallCrash, h, u, HildeS.WallStun, (byte)ProjKind.HildeKeg, true, u.X + wx * u.R, u.Y + wy * u.R);
        }

        /// <summary>A wall tile (not the map's edge or the void) touches a body of radius r at (x, y).</summary>
        bool WallNear(float x, float y, float r)
        {
            int x0 = (int)Math.Floor(x - r), x1 = (int)Math.Floor(x + r), y0 = (int)Math.Floor(y - r), y1 = (int)Math.Floor(y + r);
            for (int ty = y0; ty <= y1; ty++)
                for (int tx = x0; tx <= x1; tx++)
                {
                    if (Map.Tile(tx, ty) != Arena.Wall) continue;
                    float cx = SimMath.Clamp(x, tx, tx + 1), cy = SimMath.Clamp(y, ty, ty + 1);
                    if ((cx - x) * (cx - x) + (cy - y) * (cy - y) < r * r) return true;
                }
            return false;
        }
    }
}
