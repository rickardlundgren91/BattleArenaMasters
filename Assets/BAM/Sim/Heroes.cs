using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // The seven heroes' specials and passives (the old game's SimHeroes6, SimSpecials2 for Tyra's bolt, SimDualSpecials for the
    // tap and drag forms, SimMoaInstinct, SimTelegraph). Fenn's wolf and clone are the decided new versions (Docs/DECISIONS.md).
    // Every special has two forms: a tap (Unit.AimAng NaN) picks its own target, a drag casts the aimed form.

    /// <summary>Bram, Hammer Throw. Tap: the hammer homes in on a random enemy hero in reach and bounces on to up to two more
    /// enemies nearby (each bounce weaker). Drag: a straight throw through every enemy on the line. Passive: every 4th auto attack
    /// also hurts everyone around its target.</summary>
    public static class BramS
    {
        public const float Reach = 5, Speed = 13, DamageCoeff = 1.3f, BounceRange = 2.5f, BounceCoeff = 0.7f, LineReach = 5, LineHitbox = 0.2f;
        public const int Bounces = 2;
        public const int PassiveEvery = 4;
        public const float PassiveRadius = 1.5f, PassiveCoeff = 0.5f;
        /// <summary>Anvil Drop (the drag since 2026-10-28, XYZ 2026-10-25): lands DropDelay s later on the aimed point within Reach,
        /// hurts and stuns every enemy hero and minion within DropRadius with a clear line from it.</summary>
        public const float DropDelay = 0.6f, DropRadius = 1.1f, DropCoeff = 1.0f, DropStun = 1.5f;
        /// <summary>Backlash (Simon's Återslaget, the passive since 2026-10-28): every 4th auto attack an echo flies from the target
        /// back towards Bram, up to 2.25 tiles, and hits the first other enemy hero or minion for 65 % of his damage.</summary>
        public const int EchoEvery = 4;
        public const float EchoDistance = 2.25f, EchoSpeed = 14, EchoHitbox = 0.35f, EchoCoeff = 0.65f;
    }

    /// <summary>Fenn, Call of the Pack. Tap: a wolf that follows him and bites enemies near him. Drag: the wolf is thrown up to
    /// 6 tiles, homes in on an enemy hero it passes and pounces (a bite and a short root), then hunts on. Walls stop the wolf:
    /// it walks round them, a throw ends at the first one and it bites only with a clear line (Rickard 2026-10-25, after it won
    /// 90 % going through them); it still passes through bodies. Passive (the clone): when an enemy hero hits Fenn, a copy of
    /// him with his health and level appears for 3 s, runs at the nearest enemy and swings at it, but it is a decoy: it deals no
    /// damage and is gone at the first hit it takes (Rickard 2026-10-25; the old illusion's full fight made Fenn win 83 % against
    /// Hard bots, the decoy 62 %, the old game's Fenn 61 %). At most once per 18 s.</summary>
    public static class FennS
    {
        /// <summary>The thrown wolf turns onto an enemy hero within HomeRadius of its flight: as wide as the red warning over
        /// its path (the old rush's warning was PounceRadius wide too).</summary>
        public const float ThrowReach = 6, ThrowSpeed = 11, HomeRadius = 1, PounceRadius = 1, RootDuration = 0.8f;
        public const float HpPercent = 0.3f, Duration = 4, DamageCoeff = 0.4f, Leash = 6, Sight = 3.5f, FollowGap = 1.2f;
        public const float CloneCooldown = 18, CloneLife = 3, CloneSight = 5;
        /// <summary>The clone's damage as a share of Fenn's: none, it is a decoy (the old illusion dealt all of it).</summary>
        public const float CloneDamage = 0;
    }

    /// <summary>Tyra, Pinpoint Bolt (the drag): a short draw, then a heavy bolt that stops in the first enemy it meets. The tap is
    /// Arrow Volley and the passive Featherstep (Hero5.cs).</summary>
    public static class TyraS
    {
        /// <summary>The bolt's length: 2 x (her attack range 4 + 0.5), as the old boltLen; also the tap's reach.</summary>
        public const float Length = 2 * (4f + 0.5f);
        public const float Windup = 0.35f, DamageCoeff = 1.15f, Speed = 18, Hitbox = 0.35f;
        /// <summary>Arrow Volley (the tap since 2026-10-28, XYZ 2026-10-25): after a short draw, five arrows fanned +-HalfAngle round a
        /// random enemy hero or minion within her attack range; they fly as far as her range, through enemies, and the volley hurts
        /// each enemy once (never towers).</summary>
        public const int VolleyArrows = 5;
        public const float VolleyWindup = 0.25f, VolleyHalfAngle = 0.35f, VolleyCoeff = 0.3f, VolleySpeed = 18, VolleyHitbox = 0.3f;
        /// <summary>Featherstep (Simon, the passive since 2026-10-28): her auto attack hurting an enemy hero speeds her up 12 % for
        /// 1.5 s, once every 10 s.</summary>
        public const float FeatherBonus = 0.12f, FeatherTime = 1.5f, FeatherCooldown = 10;
    }

    /// <summary>Moa, Honey Rush. Tap: a shield on the ally hero with the lowest health share in reach (Moa included). Drag: a
    /// honey field for 2 s that shields each ally hero in it once. Her shields block stuns, roots and slows and speed the holder
    /// up. Passive (Queen's Instinct): an ally hero near her that a direct enemy hero hit would leave under 25 % health gets a
    /// small shield inside that hit (every 22 s).</summary>
    public static class MoaS
    {
        public const float Range = 5, ShieldPercent = 0.18f, Duration = 3, AreaRadius = 1.8f, AreaShieldPercent = 0.06f, AreaDuration = 3, FieldDuration = 2;
        public const float SpeedBonus = 0.1f, SpeedDuration = 1.5f;
        public const float QueenRange = 4, QueenThreshold = 0.25f, QueenShieldPercent = 0.08f, QueenDuration = 2, QueenCooldown = 22;
    }

    /// <summary>Knut, Anchor Throw: an anchor on a chain that pulls the first enemy it hits to him and stuns it (the stun
    /// lasts the pull plus 0.4 s). Tap: at a random enemy hero or minion in reach, no hero first (Rickard 2026-10-27; towers are
    /// never a special's target). Drag: the aimed way. Passive: his auto attacks slow (Defs).</summary>
    public static class KnutS
    {
        public const float HookRadius = 0.4f, HookDistance = 6.5f, TapReach = 3.5f, OutSpeed = 14, OutTime = 1.2f, Release = 0.5f;
        public const float PullSpeed = 15, StunAfter = 0.4f, DamageCoeff = 1, CastTime = 0.9f;
        /// <summary>Anchor Slam (the tap since 2026-10-28, Rickard's idea "en slow aoe och den andra hooken"): after a short wind-up
        /// where he stands, every enemy hero and minion round him with a clear line takes the hit and a 40 % slow.</summary>
        public const float SlamRadius = 1.8f, SlamWindup = 0.3f, SlamCoeff = 1.0f, SlamSlow = 0.4f, SlamSlowTime = 1.5f;
    }

    /// <summary>Tikta, Needle Storm. Tap: four needles, one every 0.2 s, each at a random enemy in reach. Drag: all four along
    /// one line, each stops in the first enemy. Needles poison heroes. Passive: her auto attacks hit harder on targets under 40 %
    /// health.</summary>
    public static class TiktaS
    {
        public const float Reach = 2.9f, Interval = 0.2f, DamageCoeff = 1.2f, Speed = 16, LineReach = 5, LineHitbox = 0.2f;
        public const int Count = 4;
        public const float PoisonPerSecond = 0.1f, PoisonDuration = 2;
    }

    /// <summary>Blast, Supernova. Tap: a bomb thrown (over walls) at a random enemy in reach that explodes 0.5 s later. Drag:
    /// a strip of fire for 2 s that hurts enemies in it every 0.5 s. Passive: everything she hits burns for 15 % more over 2 s.</summary>
    public static class BlastS
    {
        public const float Reach = 4.5f, Delay = 0.5f, Radius = 1.5f, DamageCoeff = 1.45f;
        public const float LaneLength = 4.5f, LaneHalfWidth = 0.45f, LaneDuration = 2, LaneTick = 0.5f, LaneCoeff = 0.35f;
        public const float BurnPercent = 0.15f, BurnDuration = 2, BurnTick = 0.25f;
    }

    public sealed partial class Match
    {
        readonly List<Unit> spBuf = new List<Unit>(48), areaBuf = new List<Unit>(48);
        // where the cast being made aims and at whom (the Cast event)
        float castX, castY;
        Unit castTgt;

        // ---------- casting ----------
        /// <summary>Casts h's special: aimAngle NaN = a tap, else a drag the way aimAngle points (map space) with length share aimK.
        /// False (and nothing spent) when it cannot fire now: no charge, stunned, or a tap with nobody in reach.</summary>
        internal bool TryCastSpecial(Unit h, float aimAngle, float aimK)
        {
            if (!h.Alive || h.Kind != UnitKind.Hero || h.IsClone || !h.SpecialReady || h.Has(St.Stun) || State != MatchState.Play) return false;
            h.AimAng = aimAngle; h.AimK = SimMath.Clamp(float.IsNaN(aimK) ? 0 : aimK, 0, 1);
            bool drag = !float.IsNaN(aimAngle);
            castX = h.X; castY = h.Y; castTgt = null;
            float dmg = h.StatDmg();
            bool used;
            switch (h.Hero)
            {
                case HeroId.Bram: used = CastBram(h, dmg); break;
                case HeroId.Fenn: used = CastFenn(h); break;
                case HeroId.Tyra: used = CastTyra(h, dmg); break;
                case HeroId.Moa: used = CastMoa(h); break;
                case HeroId.Knut: used = CastKnut(h, dmg); break;
                case HeroId.Tikta: used = CastTikta(h); break;
                case HeroId.Blast: used = CastBlast(h, dmg); break;
                case HeroId.Bonk: used = CastBonk(h, dmg); break;
                case HeroId.Hilde: used = CastHilde(h, dmg); break;
                case HeroId.Oaken: used = CastOaken(h, dmg); break;
                default: used = false; break;
            }
            h.AimAng = float.NaN;
            if (!used) return false;
            h.Charge = 0;
            int other = Teams.Other(h.Team);
            if (IsVisibleTo(h, other)) h.CastSeen[other] = Time; // the bots' memory of a spent special
            Emit(EventType.Cast, h, castTgt, 0, (byte)h.Hero, drag, castX, castY);
            return true;
        }

        bool Aimed(Unit h, out float ang, out float k)
        {
            ang = h.AimAng; k = h.AimK;
            return !float.IsNaN(ang);
        }

        /// <summary>A ground special's landing point: aimK of the reach the aimed way, at least minDist tiles out, inside the map.</summary>
        void AimPoint(Unit h, float ang, float k, float reach, out float x, out float y, float minDist = 1.5f)
        {
            float d = Math.Min(reach, Math.Max(minDist, k * reach));
            x = SimMath.Clamp(h.X + SimMath.Cos(ang) * d, 0.5f, Map.W - 0.5f);
            y = SimMath.Clamp(h.Y + SimMath.Sin(ang) * d, 0.5f, Map.H - 0.5f);
        }

        /// <summary>A random enemy hero in reach with a clear line, else a random enemy of the fallback kind; null if none.</summary>
        Unit PickInReach(Unit h, float reach, Pick fallback, bool los)
        {
            var l = EnemiesInRange(h, reach, Pick.Heroes, los, spBuf);
            if (l.Count == 0) l = EnemiesInRange(h, reach, fallback, los, spBuf);
            return l.Count > 0 ? PickRandom(l) : null;
        }

        /// <summary>A special lands on tgt: the damage and the Hit event (every hit shows).</summary>
        float SpecialHit(Unit src, Unit tgt, float dmg, ProjKind kind, bool big, DmgKind dk = DmgKind.Special, DmgFlags flags = DmgFlags.None)
        {
            float dealt = DealDamage(src, tgt, dmg, dk, flags);
            Emit(EventType.Hit, src, tgt, dealt, (byte)kind, big, tgt.X, tgt.Y, dk);
            return dealt;
        }

        static bool Hurtable(Unit h, Unit u) => u.Alive && u.Team != h.Team && u.Kind != UnitKind.Tower && !u.Has(St.Untargetable);

        // ---------- red warnings (the old SimTelegraph): pictures for players, read by the bots to dodge ----------
        /// <summary>A warning strip from src's spot the way ang points, len long and half wide; tied: it goes when the move it
        /// belongs to ends (Fenn's thrown wolf).</summary>
        Proj WarnLine(Unit src, float ang, float len, float half, float dur, bool tied = false)
        {
            var p = NewProj(ProjMode.WarnLine, src, src.X, src.Y);
            p.Ang = ang; p.D = len; p.Radius = half; p.Dur = Math.Max(0.15f, dur); p.Tied = tied;
            return p;
        }
        Proj WarnCircle(Unit src, float x, float y, float r, float dur, bool tied = false)
        {
            var p = NewProj(ProjMode.WarnCircle, src, x, y);
            p.Radius = r; p.Dur = Math.Max(0.15f, dur); p.Tied = tied;
            return p;
        }
        void WarnUpdate(Proj p)
        {
            var s = p.Src;
            if (p.T >= p.Dur || s == null || !s.Alive || (p.Tied && !(s.Kind == UnitKind.Wolf && s.Wolf == WolfMode.Fly))) p.Done = true;
        }

        // ---------- Bram ----------
        bool CastBram(Unit h, float dmg)
        {
            if (Aimed(h, out float aa, out float ak)) return CastAnvilDrop(h, dmg, aa, ak); // Hero5.cs
            var l = EnemiesInRange(h, BramS.Reach, Pick.HeroOrMinion, true, spBuf); // any enemy hero or minion, no hero first (Simon's tap rule)
            if (l.Count == 0) return false;
            var t = PickRandom(l);
            h.Facing = SimMath.Atan2(t.Y - h.Y, t.X - h.X);
            AddStatus(h, St.Casting, 0.2f);
            float d = Math.Min(BramS.Reach, SimMath.Dist(h, t));
            WarnLine(h, h.Facing, d, 0.15f, d / BramS.Speed + 0.1f);
            var hm = NewProj(ProjMode.Hammer, h, h.X, h.Y);
            hm.Kind = ProjKind.BramHammerSpecial; hm.Tgt = t; hm.Ang = h.Facing; hm.Speed = BramS.Speed; hm.Dmg = dmg * BramS.DamageCoeff; hm.Radius = BramS.LineHitbox;
            castX = t.X; castY = t.Y; castTgt = t;
            return true;
        }

        /// <summary>The tapped hammer homes in on its target, hits, then bounces to a random enemy hero or minion near it.</summary>
        void HammerUpdate(Proj p, float dt)
        {
            var t = p.Tgt;
            if (t == null || !t.Alive || t.Has(St.Untargetable)) { p.Done = true; return; }
            float d = SimMath.Hypot(t.X - p.X, t.Y - p.Y), step = p.Speed * dt;
            p.Ang = SimMath.Atan2(t.Y - p.Y, t.X - p.X);
            if (!Map.LosClear(p.X, p.Y, t.X, t.Y)) { p.Done = true; Blocked(p, p.X, p.Y); return; } // a wall between: the hammer drops
            if (d > step + t.R * 0.5f) { p.X += (t.X - p.X) / d * step; p.Y += (t.Y - p.Y) / d * step; return; }
            p.AddHit(t);
            SpecialHit(p.Src, t, p.Dmg, p.Kind, true);
            // the bounce: up to Bounces more, to an enemy hero or minion within BounceRange of t with a clear line from t
            areaBuf.Clear();
            if (p.HitCount <= BramS.Bounces)
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Team == p.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || p.HasHit(u)) continue;
                    if (SimMath.Dist(u, t) > BramS.BounceRange + u.R || !Map.LosClear(t.X, t.Y, u.X, u.Y)) continue;
                    areaBuf.Add(u);
                }
            if (areaBuf.Count == 0) { p.Done = true; return; }
            p.Tgt = PickRandom(areaBuf); p.Dmg *= BramS.BounceCoeff; p.X = t.X; p.Y = t.Y;
        }

        /// <summary>The old dragged throw (until 2026-10-28, now Anvil Drop): straight, through every enemy on the line, stops at a
        /// wall or after LineReach. Nothing casts it any more.</summary>
        void HammerLineUpdate(Proj p, float dt)
        {
            float ox = p.X, oy = p.Y, step = p.Speed * dt;
            p.X += SimMath.Cos(p.Ang) * step; p.Y += SimMath.Sin(p.Ang) * step; p.D += step;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            if (p.D > BramS.LineReach) { p.Done = true; return; }
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(p.Src, u) || u.Team == p.Team || p.HasHit(u) || SimMath.Dist(u, p.X, p.Y) > u.R + BramS.LineHitbox) continue;
                p.AddHit(u);
                SpecialHit(p.Src, u, p.Dmg, p.Kind, true);
            }
        }

        /// <summary>The old passive Anvil Strike (until 2026-10-28, now Backlash, Hero5.cs): every 4th auto attack also hit everyone
        /// (not towers) around its target for half its damage. Nothing calls it any more.</summary>
        void BramPassive(Unit h, Unit t, Proj p)
        {
            if (++h.PassHits < BramS.PassiveEvery) return;
            h.PassHits = 0;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u != t && Hurtable(h, u) && SimMath.Dist(u, t) <= BramS.PassiveRadius + u.R) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++) if (areaBuf[i].Alive) SpecialHit(h, areaBuf[i], p.Dmg * BramS.PassiveCoeff, ProjKind.BramHammer, false, DmgKind.Passive);
            Emit(EventType.Explosion, h, t, BramS.PassiveRadius, (byte)ProjKind.BramHammer, false, t.X, t.Y, DmgKind.Passive);
        }

        // ---------- Fenn ----------
        bool CastFenn(Unit h)
        {
            // one wolf at a time: a new one replaces the old
            for (int i = 0; i < Units.Count; i++)
            {
                var o = Units[i];
                if (o.Alive && o.Kind == UnitKind.Wolf && o.Owner == h) { o.Alive = false; o.Hp = 0; o.DeadT = 0.6f; Emit(EventType.Death, null, o); }
            }
            bool drag = Aimed(h, out float aa, out float ak);
            float a = h.Facing + SimMath.PI / 2, wx = h.X + SimMath.Cos(a) * 0.6f, wy = h.Y + SimMath.Sin(a) * 0.6f;
            if (drag || !Map.Free(wx, wy, Defs.Wolf.Radius)) { wx = h.X; wy = h.Y; }
            var w = NewUnit(UnitKind.Wolf, h.Team, wx, wy);
            w.UDef = Defs.Wolf; w.R = Defs.Wolf.Radius; w.Owner = h; w.LifeT = FennS.Duration;
            w.MaxHp = h.MaxHp * FennS.HpPercent; w.Hp = w.MaxHp; w.DmgOverride = h.StatDmg() * FennS.DamageCoeff; w.Facing = h.Facing;
            w.Wolf = WolfMode.Follow;
            AddStatus(h, St.Casting, 0.25f);
            castX = wx; castY = wy;
            if (drag)
            {
                AimPoint(h, aa, ak, FennS.ThrowReach, out float gx, out float gy);
                // the throw ends where a wall would stop the wolf, so the red circle shows where it really lands
                float ga = SimMath.Atan2(gy - wy, gx - wx), gd = WolfReach(wx, wy, ga, SimMath.Hypot(gx - wx, gy - wy));
                gx = wx + SimMath.Cos(ga) * gd; gy = wy + SimMath.Sin(ga) * gd;
                h.Facing = aa; w.Facing = aa; w.Wolf = WolfMode.Fly; w.WolfGx = gx; w.WolfGy = gy;
                // red warning over the whole flight (it can turn onto a hero anywhere along it) and the landing spot
                WarnLine(w, ga, gd, FennS.HomeRadius, gd / FennS.ThrowSpeed + 0.05f, true);
                WarnCircle(w, gx, gy, FennS.PounceRadius, gd / FennS.ThrowSpeed + 0.05f, true);
                castX = gx; castY = gy;
            }
            Emit(EventType.WolfSummon, w, h, 0, 0, drag, wx, wy);
            return true;
        }

        /// <summary>The wolf's brain once it is on the ground (a thrown wolf flies in WolfFly first, stunned or not): after a pounce
        /// it hunts its prey, else any enemy near it; a called wolf bites the nearest enemy near Fenn, else stays at his side. It
        /// walks round walls on a path, as the old aiWolf.</summary>
        void AiWolf(Unit w, float dt)
        {
            var o = w.Owner;
            w.AiT -= dt;
            Unit t = null;
            if (w.Wolf == WolfMode.Free)
            {
                var prey = w.WolfPrey;
                if (prey != null && prey.Alive && !prey.Has(St.Untargetable) && IsVisibleTo(prey, w.Team)) t = prey;
                else
                {
                    w.WolfPrey = null;
                    t = Nearest(EnemiesInRange(w, FennS.Sight, Pick.Heroes, false, aiBuf), w.X, w.Y);
                    if (t == null) t = Nearest(EnemiesInRange(w, FennS.Sight, Pick.Any, false, aiBuf), w.X, w.Y);
                    if (t == null) w.Wolf = WolfMode.Follow; // nothing left to hunt: back to Fenn
                }
            }
            if (w.Wolf == WolfMode.Follow)
            {
                t = Nearest(EnemiesInRange(w, FennS.Sight, Pick.Any, false, aiBuf), w.X, w.Y);
                if (t != null && SimMath.Dist(o, t) > FennS.Leash) t = null;
            }
            // its bite needs a clear line, so a target behind a wall is walked round to
            if (t != null) { if (SimMath.Dist(w, t) > w.Range + t.R || !Map.LosClear(w, t)) { WolfWalk(w, t.X, t.Y, dt); return; } }
            else if (SimMath.Dist(w, o) > FennS.FollowGap) { WolfWalk(w, o.X, o.Y, dt); return; }
            w.AiT = 0; w.Path.Clear(); // standing: the next goal gets a fresh path
        }

        /// <summary>One step towards (gx, gy) along a path round the walls, made again every 0.3 s (the old aiWolf); straight at
        /// the goal when there is no path.</summary>
        void WolfWalk(Unit w, float gx, float gy, float dt)
        {
            if (w.AiT <= 0) { w.AiT = 0.3f; Map.FindPath(w.X, w.Y, gx, gy, Time, w.Path); }
            if (w.Path.Has)
            {
                float nx = w.Path.X[w.Path.Index], ny = w.Path.Y[w.Path.Index];
                if (SimMath.Hypot(nx - w.X, ny - w.Y) < 0.25f) w.Path.Index++;
                else MoveUnit(w, nx - w.X, ny - w.Y, dt);
                return;
            }
            MoveUnit(w, gx - w.X, gy - w.Y, dt);
        }

        void WolfFly(Unit w, float dt)
        {
            var prey = w.WolfPrey;
            if (prey != null && (!prey.Alive || prey.Has(St.Untargetable))) prey = w.WolfPrey = null;
            if (prey == null)
            {
                float bd = float.MaxValue;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Team == w.Team || u.Kind != UnitKind.Hero || u.Has(St.Untargetable) || !IsVisibleTo(u, w.Team)) continue;
                    float d = SimMath.Dist(w, u) - u.R;
                    if (d <= FennS.HomeRadius && d < bd && Map.LosClear(w, u)) { bd = d; prey = u; }
                }
                w.WolfPrey = prey;
            }
            float gx = prey != null ? prey.X : w.WolfGx, gy = prey != null ? prey.Y : w.WolfGy;
            float dist = SimMath.Hypot(gx - w.X, gy - w.Y), stop = prey != null ? w.R + prey.R + 0.1f : 0, step = FennS.ThrowSpeed * dt;
            if (dist > 1e-4f) w.Facing = SimMath.Atan2(gy - w.Y, gx - w.X);
            bool last = dist - stop <= step;
            float k = last ? (dist > 1e-4f ? Math.Max(0, dist - stop) / dist : 0) : step / dist;
            float nx = w.X + (gx - w.X) * k, ny = w.Y + (gy - w.Y) * k;
            bool wall = !Map.Free(nx, ny, w.R); // a wall stops the flight: it lands where it is (the old rush, wall = true)
            if (!wall) { w.X = nx; w.Y = ny; w.WalkT += dist * k; w.MovedAt = Time; }
            if (last || wall) WolfPounce(w, prey);
        }

        /// <summary>How far the thrown wolf flies from (x, y) along ang (map space) before a wall stops it, at most want: the
        /// steps it really flies (the old rushStop), so the aim preview and the red circle can show where it lands.</summary>
        public float WolfReach(float x, float y, float ang, float want)
        {
            float c = SimMath.Cos(ang), s = SimMath.Sin(ang), step = FennS.ThrowSpeed * Rules.Tick, d = 0;
            while (d < want - 1e-4f)
            {
                float nd = Math.Min(want, d + step);
                if (!Map.Free(x + c * nd, y + s * nd, Defs.Wolf.Radius)) break;
                d = nd;
            }
            return d;
        }

        /// <summary>The thrown wolf lands: a bite from Fenn on its prey (else the enemy next to it, heroes first) and a short root.
        /// A landing on nothing shows no bite.</summary>
        void WolfPounce(Unit w, Unit prey)
        {
            var best = prey != null && prey.Alive && SimMath.Dist(w, prey) - prey.R <= FennS.PounceRadius && Map.LosClear(w, prey) ? prey : PounceTarget(w.Team, w.X, w.Y, FennS.PounceRadius);
            w.Wolf = WolfMode.Free;
            w.WolfPrey = best != null && best.Kind == UnitKind.Hero ? best : null;
            Emit(EventType.WolfPounce, w, best, FennS.PounceRadius, 0, false, w.X, w.Y);
            if (best == null) return;
            SpecialHit(w.Owner, best, w.DmgOverride, ProjKind.WolfBite, true);
            Emit(EventType.AttackStart, w, best, 0, (byte)ProjKind.WolfBite, true); // the bite itself shows on the wolf
            if (best.Alive) AddStatus(best, St.Root, FennS.RootDuration, 0, 0, w);
        }

        /// <summary>The enemy (not a tower) within radius of the spot, measured to its edge, with no wall in between: a hero before
        /// anything else, then the nearest. The aim preview rings the same one.</summary>
        public Unit PounceTarget(int team, float x, float y, float radius)
        {
            Unit best = null; float bd = 99;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Team == team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable)) continue;
                float d = SimMath.Hypot(u.X - x, u.Y - y) - u.R;
                if (d > radius || !Map.LosClear(x, y, u.X, u.Y)) continue;
                bool uh = u.Kind == UnitKind.Hero;
                bool better = best == null || (uh != (best.Kind == UnitKind.Hero) ? uh : d < bd);
                if (better) { best = u; bd = d; }
            }
            return best;
        }

        // Fenn's clone (the old Omega/Thief illusion): set up inside the damage, made at the end of the tick
        void CloneOnHurt(Unit tgt, Unit src, DmgKind kind, DmgFlags flags)
        {
            if (tgt.Kind != UnitKind.Hero || tgt.IsClone || tgt.Hero != HeroId.Fenn || !tgt.Alive) return;
            if (src == null || src.Kind != UnitKind.Hero || src.IsClone || src.Team == tgt.Team) return; // an enemy hero's own hit
            if ((kind != DmgKind.Auto && kind != DmgKind.Special) || (flags & (DmgFlags.Burning | DmgFlags.DotTick)) != 0) return;
            if (Time < tgt.CloneReadyAt || tgt.ClonePending) return;
            for (int i = 0; i < Units.Count; i++) { var c = Units[i]; if (c.IsClone && c.Alive && c.Owner == tgt) return; }
            tgt.CloneReadyAt = Time + FennS.CloneCooldown;
            tgt.ClonePending = true;
        }

        /// <summary>The clones of this tick's hits join the units now (never inside a loop over the units).</summary>
        void FlushClones()
        {
            for (int i = 0, n = Units.Count; i < n; i++)
            {
                var h = Units[i];
                if (!h.ClonePending) continue;
                h.ClonePending = false;
                if (!h.Alive || h.Kind != UnitKind.Hero) continue;
                var c = NewUnit(UnitKind.Hero, h.Team, h.X + Rng.Range(-0.3f, 0.3f), h.Y + Rng.Range(-0.3f, 0.3f));
                c.Hero = h.Hero; c.HDef = h.HDef; c.R = h.R; c.IsClone = true; c.Owner = h; c.Level = h.Level; c.Pips = 0; // as the old illusion: no stars until the next hero death
                c.MaxHp = h.MaxHp; c.Hp = h.Hp; c.LifeT = FennS.CloneLife; c.Charge = 0; c.Facing = h.Facing; c.DmgMult = h.DmgMult * FennS.CloneDamage;
                Unstick(c);
                c.PX = c.X; c.PY = c.Y; c.SpawnX = c.X; c.SpawnY = c.Y;
                Emit(EventType.CloneSpawn, c, h, 0, 0, false, c.X, c.Y);
            }
        }

        void EndClone(Unit c)
        {
            c.Alive = false; c.Hp = 0;
            Emit(EventType.CloneEnd, c, c.Owner, 0, 0, false, c.X, c.Y);
        }

        /// <summary>The clone runs at the nearest enemy within 5 tiles (its harmless swings do the rest), else stays by Fenn.</summary>
        void AiClone(Unit c, float dt)
        {
            var t = Nearest(EnemiesInRange(c, FennS.CloneSight, Pick.Any, false, aiBuf), c.X, c.Y);
            if (t != null) { if (SimMath.Dist(c, t) > c.Range * 0.8f) MoveUnit(c, t.X - c.X, t.Y - c.Y, dt); }
            else { var o = c.Owner; if (SimMath.Dist(c, o) > 1) MoveUnit(c, o.X - c.X, o.Y - c.Y, dt); }
        }

        // ---------- Tyra ----------
        bool CastTyra(Unit h, float dmg)
        {
            if (!Aimed(h, out float ang, out _)) return CastVolley(h, dmg); // the tap: Arrow Volley (Hero5.cs)
            castX = h.X + SimMath.Cos(ang) * TyraS.Length; castY = h.Y + SimMath.Sin(ang) * TyraS.Length;
            h.Facing = ang;
            var p = NewProj(ProjMode.Bolt, h, h.X, h.Y);
            p.Kind = ProjKind.TyraBolt; p.T = -TyraS.Windup; p.Ang = ang; p.Travel = TyraS.Length; p.Dmg = dmg * TyraS.DamageCoeff; p.Speed = TyraS.Speed; p.Radius = TyraS.Hitbox;
            WarnLine(h, ang, TyraS.Length, TyraS.Hitbox, TyraS.Windup + TyraS.Length / TyraS.Speed);
            AddStatus(h, St.Casting, TyraS.Windup); AddStatus(h, St.Disarm, TyraS.Windup); Channel(h, TyraS.Windup);
            return true;
        }

        /// <summary>Tyra's bolt flies straight and stops in the first enemy (or wall) it meets.</summary>
        void BoltUpdate(Proj p, float dt)
        {
            float step = p.Speed * dt, ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang);
            p.X += ca * step; p.Y += sa * step; p.D += step;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(p.Src, u) || u.Team == p.Team || SimMath.Hypot(u.X - p.X, u.Y - p.Y) > p.Radius + u.R) continue;
                p.Done = true;
                SpecialHit(p.Src, u, p.Dmg, p.Kind, true);
                return;
            }
            if (Map.ShotBlocked(p.X - ca * step, p.Y - sa * step, p.X, p.Y)) { p.Done = true; Blocked(p, p.X - ca * step, p.Y - sa * step); }
            else if (p.D >= p.Travel) p.Done = true;
        }

        /// <summary>Bonk's every 4th swing is a power hit (big = the hit is drawn bigger). Tikta's Final Sting is gone (Deathmark,
        /// Hero2.cs), and Tyra's Steady Hand too (Featherstep, Hero5.cs).</summary>
        float HeroAttackCoeff(Unit u, Unit tgt, ref bool big)
        {
            if (u.Hero == HeroId.Bonk && !u.IsClone && ++u.PassHits >= BonkS.PowerEvery) { u.PassHits = 0; big = true; return 1 + BonkS.PowerBonus; }
            return 1;
        }

        // ---------- Moa ----------
        bool CastMoa(Unit h)
        {
            if (Aimed(h, out float aa, out float ak))
            {
                AimPoint(h, aa, ak, MoaS.Range, out float px, out float py, 0f); // no minimum: Moa can drop the field on herself (Rickard 2026-10-26)
                var f = NewProj(ProjMode.HoneyField, h, px, py);
                f.Kind = ProjKind.MoaBolt; f.Radius = MoaS.AreaRadius; f.Dur = MoaS.FieldDuration;
                HoneyFieldUpdate(f); // allies already inside get their shield in the cast's own tick
                h.Facing = aa; AddStatus(h, St.Casting, 0.3f);
                castX = px; castY = py;
                return true;
            }
            var a = MostHurtAlly(h, MoaS.Range);
            if (a == null) return false; // Moa herself counts, so she always has one while alive
            MoaShield(h, a, MoaS.ShieldPercent, MoaS.Duration);
            AddStatus(h, St.Casting, 0.3f);
            castX = a.X; castY = a.Y; castTgt = a;
            return true;
        }

        void MoaShield(Unit moa, Unit a, float pct, float dur)
        {
            float amount = a.MaxHp * pct;
            AddStatus(a, St.Shield, dur, 0, amount, moa);
            AddStatus(a, St.SpeedPct, MoaS.SpeedDuration, MoaS.SpeedBonus, 0, moa);
            Emit(EventType.Shield, moa, a, amount, 0, false, a.X, a.Y);
        }

        /// <summary>The living ally hero (Moa included, no clones) within range with a clear line and the lowest health share.</summary>
        public Unit MostHurtAlly(Unit h, float range)
        {
            Unit best = null;
            for (int i = 0; i < Units.Count; i++)
            {
                var a = Units[i];
                if (a.Kind != UnitKind.Hero || !a.Alive || a.Team != h.Team || a.IsClone || SimMath.Dist(a, h) > range + a.R) continue;
                if (a != h && !Map.LosClear(h, a)) continue;
                if (best == null || a.Hp / a.MaxHp < best.Hp / best.MaxHp) best = a;
            }
            return best;
        }

        /// <summary>Each living ally hero inside gets the field's shield once; the field ends after 2 s or when Moa dies.</summary>
        void HoneyFieldUpdate(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || p.T >= MoaS.FieldDuration) { p.Done = true; return; }
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Kind != UnitKind.Hero || !u.Alive || u.Team != h.Team || u.IsClone || p.HasHit(u) || SimMath.Dist(u, p.X, p.Y) > MoaS.AreaRadius + u.R) continue;
                p.AddHit(u);
                MoaShield(h, u, MoaS.AreaShieldPercent, MoaS.AreaDuration);
            }
        }

        /// <summary>Queen's Instinct, inside DealDamage after the shields: a direct hit from an enemy hero that would leave an ally
        /// hero near a ready Moa strictly under 25 % health gives it a small shield that soaks the rest of this hit first.
        /// Returns what is left of amount.</summary>
        float QueenInstinct(Unit src, Unit tgt, float amount, DmgKind kind, DmgFlags flags)
        {
            if (tgt.Kind != UnitKind.Hero || tgt.IsClone || !tgt.Alive || amount <= 0) return amount;
            if ((flags & (DmgFlags.Burning | DmgFlags.DotTick | DmgFlags.NotDirect)) != 0 || (kind != DmgKind.Auto && kind != DmgKind.Special)) return amount;
            if (src == null || src.Kind != UnitKind.Hero || src.IsClone || src.Owner != null || src.Team == tgt.Team) return amount;
            Unit moa = null; float bd = float.MaxValue;
            for (int i = 0; i < Units.Count; i++)
            {
                var m = Units[i];
                if (m == tgt || m.Kind != UnitKind.Hero || m.Hero != HeroId.Moa || m.IsClone || !m.Alive || m.Team != tgt.Team) continue;
                if (Time < m.QueenReadyAt) continue;
                float d = SimMath.Dist(m, tgt);
                if (d > MoaS.QueenRange) continue;
                if (d < bd || (d == bd && m.Serial < moa.Serial)) { bd = d; moa = m; }
            }
            if (moa == null || !(tgt.Hp - amount < tgt.MaxHp * MoaS.QueenThreshold)) return amount;
            moa.QueenReadyAt = Time + MoaS.QueenCooldown;
            float sh = tgt.MaxHp * MoaS.QueenShieldPercent;
            int si = AddStatus(tgt, St.Shield, MoaS.QueenDuration, 0, sh, moa, true);
            Emit(EventType.Shield, moa, tgt, sh, 0, true, tgt.X, tgt.Y);
            if (si < 0) return amount;
            float a = Math.Min(tgt.Sts[si].Amount, amount);
            tgt.Sts[si].Amount -= a;
            return amount - a;
        }

        // ---------- Knut ----------
        bool CastKnut(Unit h, float dmg)
        {
            if (!Aimed(h, out float ang, out _)) return CastAnchorSlam(h, dmg); // the tap since 2026-10-28 (Hero5.cs); the hook is the drag
            castX = h.X + SimMath.Cos(ang) * KnutS.HookDistance; castY = h.Y + SimMath.Sin(ang) * KnutS.HookDistance;
            var p = NewProj(ProjMode.Hook, h, h.X, h.Y);
            p.Kind = ProjKind.KnutAnchor; p.Ang = ang; p.Out = true; p.D = 0; p.Dmg = dmg * KnutS.DamageCoeff; p.Radius = KnutS.HookRadius;
            AddStatus(h, St.Casting, KnutS.CastTime); h.Facing = ang; Channel(h, KnutS.OutTime); // the cut window: until the anchor stops going out
            return true;
        }

        /// <summary>The anchor flies out slowing to a stop, catches the first enemy hero or minion it touches (a hero under
        /// Moa's shield only takes the hit) and comes back, pulling its catch to Knut; the catch is stunned for the pull and 0.4 s more.</summary>
        void HookUpdate(Proj p, float dt)
        {
            var h = p.Src;
            if (!h.Alive) { p.Done = true; return; }
            float ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang), back = KnutS.Release + h.R;
            if (p.Out)
            {
                float k = SimMath.Clamp(p.T / KnutS.OutTime, 0, 1), sp = SimMath.Lerp(KnutS.OutSpeed, 0, k);
                p.D += sp * dt; p.X = h.X + ca * p.D; p.Y = h.Y + sa * p.D;
                Unit hit = null;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (u.Alive && u.Team != h.Team && u.Kind != UnitKind.Tower && !u.Has(St.Untargetable) && SimMath.Hypot(u.X - p.X, u.Y - p.Y) <= KnutS.HookRadius + u.R) { hit = u; break; }
                }
                if (hit != null)
                {
                    p.Out = false; h.RemoveAll(St.Channel);
                    bool free = CcImmune(hit);
                    if (!free) p.Caught = hit;
                    SpecialHit(h, hit, p.Dmg, p.Kind, true);
                    if (!free)
                    {
                        Emit(EventType.Pull, h, hit, 0, (byte)p.Kind, false, hit.X, hit.Y);
                        if (hit.Alive) AddStatus(hit, St.Stun, Math.Max(0, p.D - back) / KnutS.PullSpeed + KnutS.StunAfter, 0, 0, h);
                    }
                }
                else if (p.D >= KnutS.HookDistance || k >= 1 || Map.WallAt(p.X, p.Y))
                {
                    p.Out = false; h.RemoveAll(St.Channel);
                    if (Map.WallAt(p.X, p.Y)) Blocked(p, p.X, p.Y);
                }
            }
            else
            {
                p.D -= KnutS.PullSpeed * dt;
                var c = p.Caught;
                if (c != null && c.Alive) { float cd = Math.Max(back, p.D); c.X = h.X + ca * cd; c.Y = h.Y + sa * cd; c.MovedAt = Time; }
                p.X = h.X + ca * Math.Max(0, p.D); p.Y = h.Y + sa * Math.Max(0, p.D);
                if (p.D <= back)
                {
                    p.Done = true;
                    if (c != null && c.Alive) Unstick(c); // a pull along a wall's edge never leaves the catch stuck in it
                }
            }
        }

        // ---------- Tikta ----------
        bool CastTikta(Unit h)
        {
            float dmg = h.StatDmg() * TiktaS.DamageCoeff;
            if (Aimed(h, out float aa, out _))
            {   // all four needles along one line, each stops in the first enemy (or wall) it meets
                h.Facing = aa; h.AimT = Time + 0.8f;
                int cast = ++h.TiktaCastId; // the four share one cast: at most one Deathmark bonus (Hero2.cs)
                for (int i = 0; i < TiktaS.Count; i++)
                {
                    var p = NewProj(ProjMode.NeedleLine, h, h.X, h.Y);
                    p.Kind = ProjKind.TiktaNeedleSpecial; p.T = -i * TiktaS.Interval; p.Ang = aa; p.Speed = TiktaS.Speed; p.Dmg = dmg; p.Radius = TiktaS.LineHitbox; p.TiktaCast = cast;
                }
                WarnLine(h, aa, TiktaS.LineReach, 0.25f, (TiktaS.Count - 1) * TiktaS.Interval + TiktaS.LineReach / TiktaS.Speed);
                Channel(h, (TiktaS.Count - 1) * TiktaS.Interval);
                castX = h.X + SimMath.Cos(aa) * TiktaS.LineReach; castY = h.Y + SimMath.Sin(aa) * TiktaS.LineReach;
                return true;
            }
            if (EnemiesInRange(h, TiktaS.Reach, Pick.HeroOrMinion, true, spBuf).Count == 0) return false; // any enemy, no hero first (Rickard 2026-10-27)
            h.Needles = TiktaS.Count; h.NeedleT = 0; h.NeedleEnd = Time + TiktaS.Count * TiktaS.Interval + 0.1f;
            ++h.TiktaCastId; // the storm's four needles share one cast id
            Channel(h, (TiktaS.Count - 1) * TiktaS.Interval + 0.05f);
            UpdateSpecial(h, 0); // the first needle flies at once
            return true;
        }

        /// <summary>Per tick for every living real hero: Tikta's storm sends its next needle at a random enemy hero or minion in
        /// reach, no hero first (none: that needle is lost); a stun ends it.</summary>
        void UpdateSpecial(Unit h, float dt)
        {
            if (h.Needles <= 0) return;
            if (h.Has(St.Stun) || Time > h.NeedleEnd) { h.Needles = 0; return; }
            h.NeedleT -= dt;
            if (h.NeedleT > 0) return;
            h.Needles--; h.NeedleT += TiktaS.Interval;
            var l = EnemiesInRange(h, TiktaS.Reach, Pick.HeroOrMinion, true, spBuf);
            if (l.Count == 0) return;
            var t = PickRandom(l);
            h.Facing = SimMath.Atan2(t.Y - h.Y, t.X - h.X); h.AimT = Time + 0.15f;
            var p = NewProj(ProjMode.Needle, h, h.X, h.Y);
            p.Kind = ProjKind.TiktaNeedleSpecial; p.Tgt = t; p.Ang = h.Facing; p.Speed = TiktaS.Speed; p.Dmg = h.StatDmg() * TiktaS.DamageCoeff; p.Radius = TiktaS.LineHitbox; p.TiktaCast = h.TiktaCastId;
            Emit(EventType.AttackStart, h, t, 0, (byte)ProjKind.TiktaNeedleSpecial, true); // each needle shows a throw
        }

        void NeedleUpdate(Proj p, float dt)
        {
            var t = p.Tgt;
            if (t == null || !t.Alive || t.Has(St.Untargetable)) { p.Done = true; return; }
            float d = SimMath.Hypot(t.X - p.X, t.Y - p.Y), step = p.Speed * dt;
            if (!Map.LosClear(p.X, p.Y, t.X, t.Y)) { p.Done = true; Blocked(p, p.X, p.Y); return; } // the target ducked behind a wall
            if (d <= step + t.R * 0.5f) { p.Done = true; SpecialHit(p.Src, t, p.Dmg + TiktaMarkBonus(p.Src, t, p), p.Kind, true); NeedlePoison(p.Src, t); return; } // + Deathmark in the same hit
            p.Ang = SimMath.Atan2(t.Y - p.Y, t.X - p.X);
            p.X += (t.X - p.X) / d * step; p.Y += (t.Y - p.Y) / d * step;
        }

        /// <summary>A dragged needle, swept (Tikta 2.0): the whole path flown this tick, up to the reach, is tested, so a big tick
        /// never carries it past an enemy; the first enemy along it is hit if no wall stands between, else it stops at the wall.</summary>
        void NeedleLineUpdate(Proj p, float dt)
        {
            float ox = p.X, oy = p.Y, step = p.Speed * dt, len = Math.Max(0, Math.Min(step, TiktaS.LineReach - p.D));
            var u = SweptHit(p.Src, p.Team, ox, oy, p.Ang, len, TiktaS.LineHitbox, out float at);
            if (u != null)
            {
                float hx = ox + SimMath.Cos(p.Ang) * at, hy = oy + SimMath.Sin(p.Ang) * at;
                if (!Map.ShotBlocked(ox, oy, hx, hy))
                {
                    p.X = hx; p.Y = hy; p.D += at; p.Done = true;
                    SpecialHit(p.Src, u, p.Dmg + TiktaMarkBonus(p.Src, u, p), p.Kind, true);
                    NeedlePoison(p.Src, u);
                    return;
                }
            }
            p.X += SimMath.Cos(p.Ang) * step; p.Y += SimMath.Sin(p.Ang) * step; p.D += step;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            if (p.D >= TiktaS.LineReach) p.Done = true;
        }

        /// <summary>A needle poisons an enemy hero: 10 % of Tikta's damage per second for 2 s, Strongest Wins (Hero2.cs).</summary>
        void NeedlePoison(Unit src, Unit u) => TiktaPoison(src, u, src.StatDmg() * TiktaS.PoisonPerSecond, TiktaS.PoisonDuration);

        /// <summary>Poison ticks: each poison gathers V per second and deals it every half second (and what is left at its end).</summary>
        void DotTick(Unit u, float dt)
        {
            for (int i = 0; i < u.StCount; i++)
            {
                if (u.Sts[i].Type != St.Poison || u.Sts[i].By == null) continue;
                float v = u.Sts[i].V, t = u.Sts[i].T;
                u.Sts[i].Amount += v * Math.Min(dt, Math.Max(0, t));
                if (u.Sts[i].Amount < v * 0.5f && t > dt) continue;
                float a = u.Sts[i].Amount; u.Sts[i].Amount = 0;
                if (a <= 0 || !u.Alive) continue;
                DealDamage(u.Sts[i].By, u, a, DmgKind.Special, DmgFlags.DotTick);
                if (!u.Alive) return;
            }
        }

        // ---------- Blast ----------
        bool CastBlast(Unit h, float dmg)
        {
            if (Aimed(h, out float aa, out _))
            {   // Inferno Lane: a strip of fire along the aimed line, cut at the first wall
                float len = Map.LaneReach(h.X, h.Y, aa, BlastS.LaneLength);
                var p = NewProj(ProjMode.FireLane, h, h.X, h.Y);
                p.Kind = ProjKind.BlastFire; p.Ang = aa; p.D = len; p.Dmg = dmg * BlastS.LaneCoeff; p.Radius = BlastS.LaneHalfWidth; p.Dur = BlastS.LaneDuration;
                h.Facing = aa; h.AimT = Time + 0.3f; AddStatus(h, St.Casting, 0.25f); AddStatus(h, St.Disarm, 0.25f);
                castX = h.X + SimMath.Cos(aa) * len; castY = h.Y + SimMath.Sin(aa) * len;
                return true;
            }
            // Supernova: thrown in an arc (no clear line needed) at a random enemy hero or minion in reach, no hero first (Simon's tap rule)
            var l = EnemiesInRange(h, BlastS.Reach, Pick.HeroOrMinion, false, spBuf);
            if (l.Count == 0) return false;
            var t = PickRandom(l);
            LeadAim(h, t, Flight.Const, BlastS.Delay * 0.5f, 0, out float ax, out float ay);
            h.Facing = SimMath.Atan2(ay - h.Y, ax - h.X); AddStatus(h, St.Casting, 0.25f); AddStatus(h, St.Disarm, 0.25f);
            var b = NewProj(ProjMode.Bomb, h, ax, ay);
            b.Kind = ProjKind.BlastBomb; b.T = -BlastS.Delay; b.Radius = BlastS.Radius; b.Dur = BlastS.Delay; b.Dmg = dmg * BlastS.DamageCoeff;
            castX = ax; castY = ay; castTgt = t;
            return true;
        }

        /// <summary>The bomb goes off: every enemy (not towers) within its radius. After Blast's death it still goes off (as passive
        /// damage).</summary>
        void BombBlast(Proj p)
        {
            var h = p.Src;
            var dk = h.Alive ? DmgKind.Special : DmgKind.Passive;
            ChainOnImpact(h, p, BlastS.Radius); // Blast 2.0, before the bomb's own damage lights anything (Hero2.cs)
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.Team != p.Team && u.Kind != UnitKind.Tower && !u.Has(St.Untargetable) && SimMath.Dist(u, p.X, p.Y) <= BlastS.Radius + u.R
                    && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u); // thrown over walls, but the blast stops at them (Rickard 2026-10-28)
            }
            for (int i = 0; i < areaBuf.Count; i++) if (areaBuf[i].Alive) SpecialHit(h, areaBuf[i], p.Dmg, p.Kind, true, dk);
            Emit(EventType.Explosion, h, null, BlastS.Radius, (byte)p.Kind, true, p.X, p.Y, dk);
        }

        /// <summary>The fire strip: one hit on each enemy in it every 0.5 s (4 in all, the first 0.5 s after the cast); it ends
        /// after 2 s or when Blast dies. Not a direct hit (no Queen's Instinct).</summary>
        void FireLaneUpdate(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive) { p.Done = true; return; }
            int max = (int)Math.Round(BlastS.LaneDuration / BlastS.LaneTick), due = Math.Min(max, (int)Math.Floor((p.T + 1e-4f) / BlastS.LaneTick));
            while (p.Ticks < due)
            {
                p.Ticks++;
                float ex = p.X + SimMath.Cos(p.Ang) * p.D, ey = p.Y + SimMath.Sin(p.Ang) * p.D;
                areaBuf.Clear();
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (Hurtable(h, u) && SimMath.PtSeg(u.X, u.Y, p.X, p.Y, ex, ey) <= BlastS.LaneHalfWidth + u.R) areaBuf.Add(u);
                }
                for (int i = 0; i < areaBuf.Count; i++) if (areaBuf[i].Alive) SpecialHit(h, areaBuf[i], p.Dmg, p.Kind, false, DmgKind.Special, DmgFlags.NotDirect);
            }
            if (p.T >= BlastS.LaneDuration - 1e-4f) p.Done = true;
        }

        /// <summary>Everything Blast hits burns: 15 % of the hit more over 2 s. One burn per target; a new hit adds to it and
        /// restarts its time.</summary>
        void Ignite(Unit src, Unit tgt, float amount)
        {
            int i = tgt.Find(St.Burn);
            bool fresh = i < 0;
            if (fresh) { i = AddStatus(tgt, St.Burn, BlastS.BurnDuration); if (i < 0) return; }
            tgt.Sts[i].Amount += amount * BlastS.BurnPercent; tgt.Sts[i].T = BlastS.BurnDuration; tgt.Sts[i].By = src;
            if (fresh) Emit(EventType.Ignite, src, tgt);
        }

        /// <summary>The burn's damage, a little every 0.25 s (before the statuses count down); its last tick takes what is left.</summary>
        void BurnTick(Unit u, float dt)
        {
            int i = u.Find(St.Burn);
            if (i < 0 || u.Sts[i].Amount <= 0 || u.Sts[i].By == null) return;
            u.Sts[i].V += dt;
            float t = u.Sts[i].T, v = u.Sts[i].V;
            if (v < BlastS.BurnTick && t > dt) return;
            float left = t + v - dt, part = t <= dt || left <= 0 ? u.Sts[i].Amount : u.Sts[i].Amount * Math.Min(1, v / left);
            u.Sts[i].Amount -= part; u.Sts[i].V = 0;
            DealDamage(u.Sts[i].By, u, part, DmgKind.Passive, DmgFlags.Burning);
        }
    }
}
