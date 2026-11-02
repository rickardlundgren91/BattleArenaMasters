using System;

namespace BAM.Sim
{
    /// <summary>Bonk, the caveman (BAM's own hero #8, Rickard 2026-10-26; never call him by the trademarked name he was asked for
    /// as). Melee bruiser with a huge stone club. Auto attack (Rickard 2026-10-26 "extremt långsamt, böja sig bakåt"): the target is
    /// a random enemy in range (D1, as every hero); he leans back for a long wind-up, then smashes the club into the ground in front
    /// of him, toward that target: every enemy in the circle there takes the hit, the target only if it is still inside. Tap, Ground
    /// Slam: a short wind-up where he stands, then every enemy hero and minion round him (clear line, not towers) takes a hit and a
    /// short stun. Drag, Boulder Toss, bowled (Rickard 2026-10-30 "mer bowling aktig"): after a short wind-up a boulder rolls along
    /// the aimed line; the first enemy hero or minion it meets is hit and sent flying like a pin, off the side the boulder struck,
    /// with a short stun, and the boulder glances off the other way, slower, and rolls on to the next (at most two; walls stop
    /// it). A pin that slides into another enemy knocks that one on too, for less. At most three enemies per throw, each once. Passive,
    /// Heavy Club: every 4th swing is a power hit.</summary>
    public static class BonkS
    {
        public const float SlamRadius = 2.0f, SlamWindup = 0.3f, SlamCoeff = 1.4f, SlamStun = 0.6f;
        /// <summary>One-tap Bonk (Rules.OneTapSpecial): his one special is the Ground Slam in a big circle (Rickard 2026-11-02 "slår i
        /// marken i stor aoe"), not the boulder; it fires with an enemy hero or minion inside it.</summary>
        public const float SlamRadiusOneTap = 3.0f;
        /// <summary>The big slam's damage share and stun, above the old tap slam's 1.4 / 0.6 s so it feels mighty (Rickard 2026-11-02);
        /// balanced by charges and hp in Defs (Hard bots: 1.4 / 0.6 s with 3 charges and 300 hp won 90 %).</summary>
        public const float SlamCoeffOneTap = 1.6f, SlamStunOneTap = 0.8f;
        public const float BoulderReach = 5.5f, BoulderWindup = 0.25f, BoulderSpeed = 11, BoulderHitbox = 0.55f, BoulderCoeff = 1.3f;
        public const float KnockDist = 1.5f, KnockTime = 0.2f, KnockStun = 0.4f;
        /// <summary>Bowling (2026-10-30): the boulder hits at most BallPins enemies itself, and one throw at most MaxPins in all with
        /// the pins it sends into others. A pin flies off up to PinSpread radians from the boulder's way (by where it was struck,
        /// straight on a head-on hit), the boulder glances BallDeflect the other way, keeps BallSlow of its speed (less on a head-on
        /// hit, so it rolls behind its pin), NextBallCoeff of its damage, and rolls at least CarryOn tiles on (never past
        /// BoulderReach + CarryOn). A pin knocks the enemy it slides into PinKnock tiles on, for PinCoeff of the throw's damage, with no stun
        /// beyond the slide itself.</summary>
        public const int BallPins = 2, MaxPins = 3; // Rickard 2026-10-30 "lite buggad med många targets": 3 and 6 were too many
        public const float PinSpread = 1.05f, BallDeflect = 0.45f, BallSlow = 0.85f, HeadOnSlow = 0.25f, NextBallCoeff = 0.6f, CarryOn = 2.0f;
        public const float PinKnock = 1.0f, PinCoeff = 0.4f;
        /// <summary>The slam auto attack: the wind-up (the HeroDef's SpawnDelay), the circle's centre this far in front of him
        /// toward the target, and its radius. Towers are hit only as the picked target.</summary>
        public const float SmashAhead = 1.2f, SmashRadius = 1.3f, SmashFollow = 0.5f; // the club follows its target at most this far (Rickard 2026-10-28)
        public const int PowerEvery = 4;
        public const float PowerBonus = 0.6f;
    }

    /// <summary>One Boulder Toss: everyone it has hurt so far (by the boulder or by a pin it sent flying), so nobody is hit twice,
    /// and the throw's damage the pins pass on.</summary>
    internal sealed partial class BonkRoll
    {
        internal Unit By; internal float Dmg; internal int Count;
        internal readonly Unit[] Hit = new Unit[BonkS.MaxPins];
        internal bool Full => Count >= Hit.Length;
        internal bool Has(Unit u)
        {
            for (int i = 0; i < Count; i++) if (Hit[i] == u) return true;
            return false;
        }
        internal void Add(Unit u) { if (Count < Hit.Length) Hit[Count++] = u; }
    }

    public sealed partial class Match
    {
        bool CastBonk(Unit h, float dmg)
        {
            if (Aimed(h, out float aa, out _))
            {   // Boulder Toss: rolls straight along the aim after the wind-up, a red line warns the way
                var p = NewProj(ProjMode.Boulder, h, h.X, h.Y);
                p.Kind = ProjKind.BonkBoulder; p.T = -BonkS.BoulderWindup; p.Ang = aa; p.Speed = BonkS.BoulderSpeed; p.Dmg = dmg * BonkS.BoulderCoeff; p.Radius = BonkS.BoulderHitbox;
                p.Travel = BonkS.BoulderReach; p.Roll = new BonkRoll { By = h, Dmg = p.Dmg };
                float len = Map.ShotReach(h.X, h.Y, aa, BonkS.BoulderReach); // where the boulder really stops, as its aim shows (BONK-05)
                WarnOf(WarnLine(h, aa, len, BonkS.BoulderHitbox, BonkS.BoulderWindup + len / BonkS.BoulderSpeed), p);
                h.Facing = aa; h.AimT = Time + BonkS.BoulderWindup + 0.2f;
                AddStatus(h, St.Casting, BonkS.BoulderWindup); Channel(h, BonkS.BoulderWindup);
                castX = h.X + SimMath.Cos(aa) * len; castY = h.Y + SimMath.Sin(aa) * len;
                return true;
            }
            // Ground Slam: only with an enemy hero or minion in reach
            float sr = OneTap ? BonkS.SlamRadiusOneTap : BonkS.SlamRadius;
            if (EnemiesInRange(h, sr, Pick.HeroOrMinion, true, spBuf).Count == 0) return false;
            var s = NewProj(ProjMode.Slam, h, h.X, h.Y);
            s.Kind = ProjKind.BonkSlam; s.T = -BonkS.SlamWindup; s.Dur = BonkS.SlamWindup; s.Radius = sr; s.Dmg = dmg * (OneTap ? BonkS.SlamCoeffOneTap : BonkS.SlamCoeff);
            WarnCircle(h, h.X, h.Y, sr, BonkS.SlamWindup);
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
                if (Hurtable(h, u) && u.Team != p.Team && SimMath.Dist(u, p.X, p.Y) <= p.Radius + u.R && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                SpecialHit(h, u, p.Dmg, ProjKind.BonkSlam, true);
                if (u.Alive) AddStatus(u, St.Stun, OneTap ? BonkS.SlamStunOneTap : BonkS.SlamStun, 0, 0, h);
            }
            Emit(EventType.Explosion, h, null, p.Radius, /* the cast's circle: SlamRadiusOneTap with the one-tap special */ (byte)ProjKind.BonkSlam, true, p.X, p.Y);
        }

        /// <summary>The boulder, swept like Tikta's line needles: the first enemy along this tick's path not yet hit by this throw is
        /// a pin if no wall stands between, and the boulder rolls on from it with what is left of the tick; else it stops at a wall
        /// or at the end of its roll (Travel).</summary>
        void BoulderUpdate(Proj p, float dt)
        {
            float step = p.Speed * dt;
            for (int n = 0; n <= BonkS.BallPins && !p.Done; n++)
            {
                float ox = p.X, oy = p.Y, ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang), full = Math.Max(0, Math.Min(step, p.Travel - p.D));
                float len = TowerReach(ox, oy, p.Ang, full, BonkS.BoulderHitbox); // a base (either team's) stops it as a wall
                bool tower = len < full - 1e-5f;
                var u = NextPin(p, ox, oy, ca, sa, len, out float at);
                if (u != null && !Map.ShotBlocked(ox, oy, ox + ca * at, oy + sa * at))
                {
                    p.X = ox + ca * at; p.Y = oy + sa * at; p.D += at; step -= at;
                    float before = p.Speed;
                    BallHits(p, u);
                    step *= p.Speed / before; // the rest of the tick at the boulder's new speed
                    continue;
                }
                p.X += ca * len; p.Y += sa * len; p.D += len;
                if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
                if (tower) { p.Done = true; Blocked(p, p.X, p.Y); return; }
                if (p.D >= p.Travel - 1e-4f) p.Done = true;
                return;
            }
        }

        /// <summary>As SweptHit, skipping everyone this throw has already hurt: the first enemy hero or minion whose circle the
        /// boulder's path from (ox, oy) along (ca, sa) for len tiles touches.</summary>
        Unit NextPin(Proj p, float ox, float oy, float ca, float sa, float len, out float along)
        {
            along = 0; Unit best = null; float bestAt = float.MaxValue, half = BonkS.BoulderHitbox;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(p.Src, u) || u.Team == p.Team || p.Roll.Has(u)) continue;
                float dx = u.X - ox, dy = u.Y - oy, a = dx * ca + dy * sa, perp2 = dx * dx + dy * dy - a * a, rr = u.R + half;
                if (perp2 > rr * rr) continue;
                float h = SimMath.Sqrt(Math.Max(0, rr * rr - perp2)), enter = a - h;
                if (a + h < 0 || enter > len) continue;
                enter = Math.Max(0, enter);
                if (enter < bestAt) { bestAt = enter; best = u; }
            }
            along = best != null ? bestAt : 0;
            return best;
        }

        /// <summary>The boulder strikes the pin u: the hit, and u flies off the side it was struck (straight on a head-on hit) with
        /// the stun; the boulder glances the other way, slower and weaker, and rolls on, or stops in its last pin.</summary>
        void BallHits(Proj p, Unit u)
        {
            var roll = p.Roll;
            float ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang), dx = u.X - p.X, dy = u.Y - p.Y;
            float side = SimMath.Clamp((dy * ca - dx * sa) / (u.R + BonkS.BoulderHitbox), -1, 1); // + : the pin stood left of the way
            p.AddHit(u); roll.Add(u);
            bool free = CcImmune(u);
            SpecialHit(p.Src, u, p.Dmg, p.Kind, true);
            if (u.Alive && !free) Knock(p.Src, u, p.Ang + side * BonkS.PinSpread, BonkS.KnockDist, BonkS.KnockStun, roll);
            p.Ang -= side * BonkS.BallDeflect;
            p.Speed *= BonkS.BallSlow - (1 - Math.Abs(side)) * BonkS.HeadOnSlow;
            p.Dmg *= BonkS.NextBallCoeff;
            p.Travel = Math.Min(Math.Max(p.Travel, p.D + BonkS.CarryOn), BonkS.BoulderReach + BonkS.CarryOn);
            if (p.HitCount >= BonkS.BallPins || roll.Full) p.Done = true;
        }

        /// <summary>u slides dist tiles the way ang points over KnockTime, stunned stun s from its start; walls stop the slide.
        /// roll: the Boulder Toss behind it, so the sliding pin knocks on the enemies it meets.</summary>
        void Knock(Unit src, Unit u, float ang, float dist, float stun, BonkRoll roll)
        {
            if (KnockImmune(u)) { if (stun > 0) AddStatus(u, St.Stun, stun, 0, 0, src); return; } // knocked too lately: the stun alone
            u.KnockFreeAt = Time + Rules.KnockCooldown;
            float sp = dist / BonkS.KnockTime;
            u.KnockVX = SimMath.Cos(ang) * sp; u.KnockVY = SimMath.Sin(ang) * sp; u.KnockT = BonkS.KnockTime; u.KnockKeg = null; u.KnockRoll = roll;
            if (stun > 0) AddStatus(u, St.Stun, stun, 0, 0, src);
            Emit(EventType.Knock, src, u, BonkS.KnockTime, (byte)ProjKind.BonkBoulder, true, u.X + u.KnockVX * BonkS.KnockTime, u.Y + u.KnockVY * BonkS.KnockTime);
        }

        /// <summary>A pin sliding from Boulder Toss bumps into the enemies (its own team) it touches with a clear line: each not yet
        /// hurt by this throw takes PinCoeff of the throw's damage and is knocked on (no stun), away from the pin; it bumps nobody
        /// further (one step of chain, so a crowd does not scatter at once).</summary>
        void PinBumps(Unit u)
        {
            var roll = u.KnockRoll; var src = roll.By;
            for (int i = 0; i < Units.Count && !roll.Full; i++)
            {
                var o = Units[i];
                if (o == u || o.Team != u.Team || !Hurtable(src, o) || roll.Has(o)) continue;
                float dx = o.X - u.X, dy = o.Y - u.Y, rr = u.R + o.R;
                if (dx * dx + dy * dy > rr * rr || !Map.LosClear(u.X, u.Y, o.X, o.Y)) continue;
                roll.Add(o);
                bool free = CcImmune(o);
                SpecialHit(src, o, roll.Dmg * BonkS.PinCoeff, ProjKind.BonkBoulder, true);
                if (o.Alive && !free) Knock(src, o, SimMath.Atan2(dy, dx), BonkS.PinKnock, 0, null); // it slides on but bumps nobody else
            }
        }

        /// <summary>One tick of a knockback slide: it moves while the way is free and ends at the first wall.</summary>
        void KnockStep(Unit u, float dt)
        {
            float left = u.KnockT * SimMath.Hypot(u.KnockVX, u.KnockVY); // tiles of the slide still to go (Hilde's Wall Crash)
            float t = Math.Min(dt, u.KnockT); u.KnockT -= dt;
            float nx = u.X + u.KnockVX * t, ny = u.Y + u.KnockVY * t;
            if (Map.Free(nx, ny, u.R)) { u.X = nx; u.Y = ny; u.MovedAt = Time; if (u.KnockRoll != null) PinBumps(u); }
            else { if (u.KnockKeg != null) KegWallHit(u, nx, ny, left); u.KnockT = 0; }
            if (u.KnockT <= 0) { u.KnockKeg = null; u.KnockRoll = null; }
        }
    }
}
