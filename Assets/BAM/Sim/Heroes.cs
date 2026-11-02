using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // The seven heroes' specials and passives (the old game's SimHeroes6, SimSpecials2 for Tyra's bolt, SimDualSpecials for the
    // tap and drag forms, SimMoaInstinct, SimTelegraph). Fenn's wolf and clone are the decided new versions (Docs/DECISIONS.md).
    // Every special has two forms: a tap (Unit.AimAng NaN) picks its own target, a drag casts the aimed form.

    /// <summary>Bram, Hammer Throw. Tap: the hammer homes in on a random enemy hero or minion in reach and bounces on to up to
    /// two more enemies nearby (each bounce weaker). Drag: Anvil Drop. Passive: Backlash (both Hero5.cs).</summary>
    public static class BramS
    {
        public const float Reach = 5, Speed = 13, DamageCoeff = 1.3f, BounceRange = 2.5f, BounceCoeff = 0.7f, LineHitbox = 0.2f;
        public const int Bounces = 2;
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
        /// <summary>Shadow Escape (Fenn 2.0, Simon's Skuggflykt, XYZ #607/#609): an enemy hero's auto attack hitting his decoy speeds
        /// Fenn up 20 % for 1 s (the decoy is gone at that hit, so once per decoy).</summary>
        public const float EscapeBonus = 0.2f, EscapeTime = 1;
        /// <summary>The clone's damage as a share of Fenn's: none, it is a decoy (the old illusion dealt all of it).</summary>
        public const float CloneDamage = 0;
    }

    /// <summary>Tyra, Pinpoint Bolt (the drag): a short draw, then a heavy bolt that flies on until it hits something: the first
    /// enemy it meets, an enemy tower or a wall (or it leaves the arena). The tap is Arrow Volley and the passive Featherstep (Hero5.cs).</summary>
    public static class TyraS
    {
        /// <summary>Her bolt flies past minions and Fenn's wolf and hits only heroes (decoys too), as Knut's hook (Rickard 2026-11-02 "tyras
        /// pil ska likt knut gå genom creeps ändå men ändå kunna sikta på dom"); towers and walls still stop it. false = the old rule.</summary>
        public const bool BoltPassesMinions = true;
        /// <summary>The bolt's old length: 2 x (her attack range 4 + 0.5), as the old boltLen. Since 2026-10-30 (Rickard: "tyras pil ska
        /// fortsätta tills den träffar något") only how far the bots look for a bolt target and fear hers; the bolt flies Flight.</summary>
        public const float Length = 2 * (4f + 0.5f);
        /// <summary>How far the bolt flies at most: longer than the arena's diagonal, so in practice to the first hit, wall or the edge.</summary>
        public const float Flight = 40;
        public const float Windup = 0.35f, DamageCoeff = 1.15f, Speed = 18, Hitbox = 0.35f;
        /// <summary>One-tap special (Rules.OneTapSpecial, 2026-11-01): how far her tap looks for the random enemy to shoot at.</summary>
        public const float TapTarget = 7;
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
        /// <summary>The honey field's shield with the one-tap special (Match.OneTap): 12 %, it is no longer placed over several
        /// allies on purpose (Rickard 2026-11-01); AreaShieldPercent (6 %) is the old drag's.</summary>
        public const float FieldShieldOneTap = 0.10f; // 12 -> 10 % (Rickard 2026-11-02 "nerfa skölden lite men inte i tid"; Hard bots Moa 61 -> 55 %)
        /// <summary>How long the one-tap field's shield lasts: 5 s (Rickard 2026-11-02 "minst 5"; AreaDuration 3 s is the old drag's).</summary>
        public const float FieldShieldTimeOneTap = 5;
        public const float SpeedBonus = 0.1f, SpeedDuration = 1.5f;
        public const float QueenRange = 4, QueenThreshold = 0.25f, QueenShieldPercent = 0.08f, QueenDuration = 2, QueenCooldown = 22;
    }

    /// <summary>Knut, Anchor Throw. Tap: Anchor Slam round him (Hero5.cs). Drag: the anchor on a chain, thrown the full
    /// HookDistance the aimed way; it flies past minions and Fenn's wolf (HookPassesMinions) and pulls the first enemy hero it touches
    /// (a decoy too, never a tower) to him and stuns it for the pull plus 0.4 s. Passive: his auto attacks slow (Defs).</summary>
    public static class KnutS
    {
        /// <summary>The anchor flies out at one steady OutSpeed (a harpoon, as Pudge's hook; Rickard 2026-11-02 "ja" to the hook
        /// feel list). It was 14 tiles/s slowing to a stop over 1.2 s. HookDistance = his auto attack range, the white ring round him
        /// (Defs 2.3; Rickard 2026-11-02 "samma som hans range", after the hook began to fly past minions: 6.5 tiles reached too far).</summary>
        public const float HookRadius = 0.4f, HookDistance = 2.3f, OutSpeed = 15, OutTime = HookDistance / OutSpeed, Release = 0.5f;
        /// <summary>The haul back is a yank: a catch hangs PullPause on the taut chain, then the reel starts at PullStart tiles/s and
        /// speeds up by PullAccel to PullMax (6 tiles in ~0.37 s after the pause, about the old flat 15 tiles/s; an empty
        /// anchor comes back the same way without the pause). See Reeled / PullTime.</summary>
        public const float PullPause = 0.08f, PullStart = 8, PullMax = 22, PullAccel = 60;
        public const float StunAfter = 0.4f, DamageCoeff = 1, CastTime = 0.9f;
        const float AccelTime = (PullMax - PullStart) / PullAccel, AccelDist = PullStart * AccelTime + 0.5f * PullAccel * AccelTime * AccelTime;
        /// <summary>How far the reel has hauled r s after it started (after the pause).</summary>
        public static float Reeled(float r) => r <= 0 ? 0 : r < AccelTime ? PullStart * r + 0.5f * PullAccel * r * r : AccelDist + PullMax * (r - AccelTime);
        /// <summary>How long the reel takes to haul d tiles (the inverse of Reeled).</summary>
        public static float PullTime(float d) => d <= 0 ? 0 : d <= AccelDist ? (SimMath.Sqrt(PullStart * PullStart + 2 * PullAccel * d) - PullStart) / PullAccel : AccelTime + (d - AccelDist) / PullMax;
        /// <summary>The hook flies past minions and Fenn's wolf and catches only heroes (Rickard 2026-11-02, "man kan ju inte träffa
        /// minions så då kan man sikta på heroes bakom"), as Pudge's hook feels; the one-tap aims it only at heroes (OneTap.cs).
        /// false = the old rule: the first enemy of any kind it touches.</summary>
        public const bool HookPassesMinions = true;
        /// <summary>Anchor Slam (the tap since 2026-10-28, Rickard's idea "en slow aoe och den andra hooken"): after a short wind-up
        /// where he stands, every enemy hero and minion round him with a clear line takes the hit and a 40 % slow.</summary>
        public const float SlamRadius = 1.8f, SlamWindup = 0.3f, SlamCoeff = 1.0f, SlamSlow = 0.4f, SlamSlowTime = 1.5f;
    }

    /// <summary>Tikta, Needle Storm. Tap: four needles, one every 0.2 s, each at a random enemy in reach. Drag: all four along
    /// one line, each stops in the first enemy. Needles poison heroes. Passive: her auto attacks hit harder on targets under 40 %
    /// health.</summary>
    public static class TiktaS
    {
        public const float Reach = 2.9f, Interval = 0.2f, DamageCoeff = 1.5f, Speed = 16, LineReach = 5, LineHitbox = 0.2f; // needles 1.2 -> 1.5 (Rickard 2026-11-02 "buffa henne lite": one-tap Tikta 43 % on Hard bots)
        public const int Count = 4;
        public const float PoisonPerSecond = 0.1f, PoisonDuration = 2;
    }

    /// <summary>Blast, Supernova. Tap: a strip of fire for 2 s at a random enemy in reach that hurts enemies in it every 0.5 s.
    /// Drag: a bomb thrown (over walls) onto the aimed spot that explodes 0.5 s later (swapped by Rickard 2026-10-30). Passive: everything she hits burns for 15 % more over 2 s.</summary>
    public static class BlastS
    {
        public const float Reach = 4.5f, Delay = 0.5f, Radius = 1.5f, DamageCoeff = 1.2f; // 1.45 -> 1.2 when the bomb became the aimed drag (2026-10-30: Hard 58.6 -> 53.1 %)
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
        /// <summary>Ties warning w to its shot or drop: w then ends when that does, not when the caster dies.</summary>
        static Proj WarnOf(Proj w, Proj of) { w.Of = of; w.OfSerial = of.Serial; return w; }

        /// <summary>Tikta's drag warning and Moa's honey field do not give away a caster hidden in grass: a team that cannot see her
        /// neither sees them drawn nor reads them (bots). Her needles still show once thrown (TIKTA-05, Rickard 2026-10-29 "Dölj tills kast").</summary>
        public bool CastHiddenFrom(Proj p, int team)
        {
            var s = p.Src;
            if (s == null || !s.Alive || s.Team == team || s.Kind != UnitKind.Hero) return false;
            bool hides = (p.Mode == ProjMode.WarnLine && s.Hero == HeroId.Tikta) || (p.Mode == ProjMode.HoneyField && s.Hero == HeroId.Moa);
            return hides && !IsVisibleTo(s, team);
        }

        void WarnUpdate(Proj p)
        {
            var s = p.Src;
            if (p.Of != null)
            {   // tied to its shot or drop: it shows as long as that is on its way, even after the caster dies (the anvil still lands)
                if (p.T >= p.Dur || p.Of.Serial != p.OfSerial || p.Of.Done) { p.Done = true; return; }
            }
            else if (p.T >= p.Dur || s == null || !s.Alive || (p.Tied && !(s.Kind == UnitKind.Wolf && s.Wolf == WolfMode.Fly))) { p.Done = true; return; }
            if (s == null) return;
            // Tikta's drag: her needles still in the hand leave from where she is then, so the strip moves with her (TIKTA-01)
            if (p.Mode == ProjMode.WarnLine && s.Kind == UnitKind.Hero && s.Hero == HeroId.Tikta && NeedleInHand(s)) { p.X = s.X; p.Y = s.Y; }
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
            if (t == null) { p.Done = true; return; }
            if (!p.Fizzle && (!t.Alive || t.Has(St.Untargetable))) { p.Fizzle = true; p.OX = t.X; p.OY = t.Y; } // keeps the spot it flew at
            float step = p.Speed * dt;
            if (p.Fizzle) { FlyToSpotAndPuff(p, step); return; } // its target fell or slipped away: no bounce either (BRAM-08)
            float d = SimMath.Hypot(t.X - p.X, t.Y - p.Y);
            p.Ang = SimMath.Atan2(t.Y - p.Y, t.X - p.X);
            if (!Map.LosClear(p.X, p.Y, t.X, t.Y)) { p.Done = true; Blocked(p, p.X, p.Y); return; } // a wall between: the hammer drops
            if (d > step + t.R * 0.5f) { p.X += (t.X - p.X) / d * step; p.Y += (t.Y - p.Y) / d * step; return; }
            p.AddHit(t);
            SpecialHit(p.Src, t, p.Dmg, p.Kind, true);
            // the bounce: up to Bounces more, to an enemy hero or minion within BounceRange of t with a clear line from t, that
            // Bram's team can see (Rickard 2026-10-29, BRAM-05: a hero hidden in the grass stays hidden)
            areaBuf.Clear();
            if (p.HitCount <= BramS.Bounces)
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Team == p.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || p.HasHit(u) || !IsVisibleTo(u, p.Team)) continue;
                    if (SimMath.Dist(u, t) > BramS.BounceRange + u.R || !Map.LosClear(t.X, t.Y, u.X, u.Y)) continue;
                    areaBuf.Add(u);
                }
            if (areaBuf.Count == 0) { p.Done = true; return; }
            p.Tgt = PickRandom(areaBuf); p.Dmg *= BramS.BounceCoeff; p.X = t.X; p.Y = t.Y;
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
                h.Facing = aa; w.Facing = aa; w.Wolf = WolfMode.Fly; w.WolfGx = gx; w.WolfGy = gy; w.WolfOx = wx; w.WolfOy = wy;
                // red warning over the whole flight (it can turn onto a hero anywhere along it) and the landing spot
                WarnLine(w, ga, gd, FennS.HomeRadius, gd / FennS.ThrowSpeed + 0.05f, true);
                WarnCircle(w, gx, gy, FennS.PounceRadius, gd / FennS.ThrowSpeed + 0.05f, true);
                castX = gx; castY = gy;
            }
            else PackHunt(w); // the tap: the wolf picks its first prey at once (Fenn 2.0, Hero5.cs)
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
                var prey = w.WolfPrey; // Pack Hunt's first prey (Hero5.cs), held while it is valid
                if (prey != null && prey.Alive && !prey.Has(St.Untargetable) && IsVisibleTo(prey, w.Team) && SimMath.Dist(o, prey) <= FennS.Leash) t = prey;
                else
                {
                    w.WolfPrey = null; // then the called wolf's old rule: the nearest enemy near it, within the leash
                    t = Nearest(EnemiesInRange(w, FennS.Sight, Pick.Any, false, aiBuf), w.X, w.Y);
                    if (t != null && SimMath.Dist(o, t) > FennS.Leash) t = null;
                }
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
            // it lets go of a prey that died, vanished or left the red strip (a dash or blink), and flies on to the red circle
            if (prey != null && (!prey.Alive || prey.Has(St.Untargetable) || !InWolfStrip(w, prey))) prey = w.WolfPrey = null;
            if (prey == null)
            {
                float bd = float.MaxValue;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Team == w.Team || u.Kind != UnitKind.Hero || u.Has(St.Untargetable) || !IsVisibleTo(u, w.Team)) continue;
                    float d = SimMath.Dist(w, u) - u.R;
                    if (d <= FennS.HomeRadius && d < bd && InWolfStrip(w, u) && Map.LosClear(w, u)) { bd = d; prey = u; }
                }
                w.WolfPrey = prey;
            }
            float gx = prey != null ? prey.X : w.WolfGx, gy = prey != null ? prey.Y : w.WolfGy;
            float dist = SimMath.Hypot(gx - w.X, gy - w.Y), stop = prey != null ? w.R + prey.R + 0.1f : 0, step = FennS.ThrowSpeed * dt;
            if (dist > 1e-4f) w.Facing = SimMath.Atan2(gy - w.Y, gx - w.X);
            bool last = dist - stop <= step;
            float k = last ? (dist > 1e-4f ? Math.Max(0, dist - stop) / dist : 0) : step / dist;
            float nx = w.X + (gx - w.X) * k, ny = w.Y + (gy - w.Y) * k;
            // never past the 6-tile throw (Rickard 2026-10-25): at the edge it lands where it is
            float ox = nx - w.WolfOx, oy = ny - w.WolfOy, od = SimMath.Hypot(ox, oy);
            if (od > FennS.ThrowReach) { nx = w.WolfOx + ox * (FennS.ThrowReach / od); ny = w.WolfOy + oy * (FennS.ThrowReach / od); last = true; }
            bool wall = !Map.Free(nx, ny, w.R) || TowerAt(nx, ny, w.R); // a wall or a base stops the flight: it lands where it is (the old rush, wall = true)
            if (!wall) { w.X = nx; w.Y = ny; w.WalkT += dist * k; w.MovedAt = Time; }
            if (last || wall) WolfPounce(w, prey);
        }

        /// <summary>Is u inside the thrown wolf's red strip: from where it left to where it lands, HomeRadius + u's radius to the side
        /// (so it never turns onto a hero behind Fenn or off to the side of what the warning showed).</summary>
        bool InWolfStrip(Unit w, Unit u)
        {
            float lx = w.WolfGx - w.WolfOx, ly = w.WolfGy - w.WolfOy, len = SimMath.Hypot(lx, ly);
            float ux = u.X - w.WolfOx, uy = u.Y - w.WolfOy;
            if (len < 1e-4f) return SimMath.Hypot(ux, uy) <= FennS.HomeRadius + u.R; // a throw stopped at once: the landing circle
            float along = (ux * lx + uy * ly) / len, side = Math.Abs(ux * ly - uy * lx) / len;
            return along >= -u.R && along <= len + FennS.HomeRadius + u.R && side <= FennS.HomeRadius + u.R;
        }

        /// <summary>How far the thrown wolf flies from (x, y) along ang (map space) before a wall stops it, at most want: the
        /// steps it really flies (the old rushStop), so the aim preview and the red circle can show where it lands.</summary>
        public float WolfReach(float x, float y, float ang, float want)
        {
            float c = SimMath.Cos(ang), s = SimMath.Sin(ang), step = FennS.ThrowSpeed * Rules.Tick, d = 0;
            while (d < want - 1e-4f)
            {
                float nd = Math.Min(want, d + step);
                if (!Map.Free(x + c * nd, y + s * nd, Defs.Wolf.Radius) || TowerAt(x + c * nd, y + s * nd, Defs.Wolf.Radius)) break; // a wall or a base
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
            float len = Map.EdgeReach(h.X, h.Y, ang, TyraS.Flight); // it flies on to the arena's edge if nothing stops it first
            castX = h.X + SimMath.Cos(ang) * len; castY = h.Y + SimMath.Sin(ang) * len;
            h.Facing = ang;
            var p = NewProj(ProjMode.Bolt, h, h.X, h.Y);
            p.Kind = ProjKind.TyraBolt; p.T = -TyraS.Windup; p.Ang = ang; p.Travel = len; p.Dmg = dmg * TyraS.DamageCoeff; p.Speed = TyraS.Speed; p.Radius = TyraS.Hitbox;
            // as long as the bolt really flies (to the first wall) and only while it does (TYRA-02)
            float reach = BoltReach(h, ang);
            WarnOf(WarnLine(h, ang, reach, TyraS.Hitbox, TyraS.Windup + reach / TyraS.Speed), p);
            AddStatus(h, St.Casting, TyraS.Windup); AddStatus(h, St.Disarm, TyraS.Windup); Channel(h, TyraS.Windup);
            return true;
        }

        /// <summary>How far Tyra's bolt aimed at ang flies with no hero or minion in the way: to the first wall, tower or the
        /// arena's edge (what the aim and the red warning draw).</summary>
        internal float BoltReach(Unit h, float ang)
            => TowerReach(h.X, h.Y, ang, Map.ShotReach(h.X, h.Y, ang, Map.EdgeReach(h.X, h.Y, ang, TyraS.Flight)), TyraS.Hitbox);

        /// <summary>How far a straight shot from (x, y) along ang, half wide, flies before it touches a living tower, at most len. Both
        /// bases stop every hero's shot as a wall does, with no damage (specials never hurt towers): the enemy tower used to stop only
        /// Tyra's bolt, while Tikta's needles, Knut's hook, Bonk's boulder and Fenn's wolf flew through (tester 2026-11-02, Rickard
        /// "alla heroes ska stanna mot basen, inte orättvist"), and the team's own tower stops its shots too (Rickard 2026-11-02 "ja").</summary>
        internal float TowerReach(float x, float y, float ang, float len, float half)
        {
            float ca = SimMath.Cos(ang), sa = SimMath.Sin(ang);
            for (int t = 0; t < Towers.Length; t++)
            {
                var u = Towers[t];
                if (u == null || !u.Alive) continue;
                float dx = u.X - x, dy = u.Y - y, along = dx * ca + dy * sa, side = Math.Abs(dy * ca - dx * sa), rr = u.R + half;
                if (along <= 0 || side >= rr) continue;
                len = Math.Min(len, Math.Max(0, along - (float)Math.Sqrt(rr * rr - side * side)));
            }
            return len;
        }

        /// <summary>Does a body of radius r at (x, y) touch a living tower, either team's (where Fenn's thrown wolf stops)?</summary>
        internal bool TowerAt(float x, float y, float r)
        {
            for (int t = 0; t < Towers.Length; t++)
            {
                var u = Towers[t];
                if (u != null && u.Alive && SimMath.Hypot(u.X - x, u.Y - y) < u.R + r) return true;
            }
            return false;
        }

        /// <summary>Does a tower (either team's) stand on the straight line from h to u before the shot (half wide) reaches u's body? A tapped
        /// straight special never picks a target hidden behind the base, so the tap is not spent on the tower.</summary>
        bool TowerInWay(Unit h, Unit u, float half)
        {
            float dx = u.X - h.X, dy = u.Y - h.Y, d = SimMath.Hypot(dx, dy);
            if (d < 1e-4f) return false;
            float need = Math.Max(0, d - u.R - half);
            return TowerReach(h.X, h.Y, SimMath.Atan2(dy, dx), need, half) < need - 1e-4f;
        }

        /// <summary>Tyra's bolt flies straight and stops in the first enemy, tower of either team (no damage: specials never hurt towers) or wall
        /// it meets.</summary>
        void BoltUpdate(Proj p, float dt)
        {
            // the wall first, then the enemies, as the volley's arrows (precision TYRA-11); the last step stops on the reach
            float ox = p.X, oy = p.Y, step = Math.Max(0, Math.Min(p.Speed * dt, p.Travel - p.D)), ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang);
            p.X += ca * step; p.Y += sa * step; p.D += step;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            // the guided match's special lesson ("TAP it to fire a bolt!") has only minions to shoot at and is passed by a bolt that
            // hits: there the bolt stops at minions, as before protocol 37 (no player passed the lesson on 0.3.3, 2026-11-02)
            bool pastMinions = TyraS.BoltPassesMinions && !(Tut != null && !Tut.Island && Tut.Step == Tutorial.StepSpecial);
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                bool tower = u.Kind == UnitKind.Tower && u.Alive; // either team's base stops it (TowerReach)
                if (!tower && (!Hurtable(p.Src, u) || u.Team == p.Team || (pastMinions && u.Kind != UnitKind.Hero)) || SimMath.Hypot(u.X - p.X, u.Y - p.Y) > p.Radius + u.R) continue; // past minions (BoltPassesMinions)
                p.Done = true;
                if (tower) Blocked(p, p.X, p.Y); else SpecialHit(p.Src, u, p.Dmg, p.Kind, true);
                return;
            }
            if (p.D >= p.Travel - 1e-5f) p.Done = true;
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
                MoaShield(h, u, OneTap ? MoaS.FieldShieldOneTap : MoaS.AreaShieldPercent, OneTap ? MoaS.FieldShieldTimeOneTap : MoaS.AreaDuration);
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

        /// <summary>The anchor flies out at OutSpeed, catches the first enemy hero it touches (past minions and wolves:
        /// KnutS.HookPassesMinions; a hero under
        /// Moa's shield only takes the hit) and comes back, pulling its catch to Knut; the catch is stunned for the pull and 0.4 s more.</summary>
        void HookUpdate(Proj p, float dt)
        {
            var h = p.Src;
            if (!h.Alive) { p.Done = true; return; }
            float ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang), back = KnutS.Release + h.R;
            if (p.Out)
            {
                float sp = KnutS.OutSpeed;
                float ox = h.X + ca * p.D, oy = h.Y + sa * p.D, od = p.D;
                // never past HookDistance, so it reaches exactly as far as the aim shows (it overshot up to 0.1 on its last tick)
                float cap = TowerReach(h.X, h.Y, p.Ang, KnutS.HookDistance, KnutS.HookRadius); // a base (either team's) stops it as a wall
                p.D = Math.Min(p.D + sp * dt, cap); p.X = h.X + ca * p.D; p.Y = h.Y + sa * p.D;
                bool tower = cap < KnutS.HookDistance - 1e-4f && p.D >= cap - 1e-4f;
                // a wall on this tick's path (swept as the other shots, so it never slips a corner): the anchor stops before it
                bool wall = Map.ShotBlocked(ox, oy, p.X, p.Y);
                if (wall) { p.D = od; p.X = ox; p.Y = oy; }
                Unit hit = null;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (u.Alive && u.Team != h.Team && u.Kind != UnitKind.Tower && (u.Kind == UnitKind.Hero || !KnutS.HookPassesMinions) && !u.Has(St.Untargetable) && SimMath.Hypot(u.X - p.X, u.Y - p.Y) <= KnutS.HookRadius + u.R) { hit = u; break; }
                }
                if (hit != null)
                {
                    p.Out = false; h.RemoveAll(St.Channel); p.Dur = p.T; p.Travel = p.D; // the reel counts from here (Proj.Dur / Travel are free on a hook)
                    bool free = CcImmune(hit);
                    if (!free) p.Caught = hit;
                    SpecialHit(h, hit, p.Dmg, p.Kind, true);
                    if (!free)
                    {
                        Emit(EventType.Pull, h, hit, 0, (byte)p.Kind, false, hit.X, hit.Y);
                        float pull = KnutS.PullPause + KnutS.PullTime(Math.Max(0, p.D - back));
                        if (hit.Alive) AddStatus(hit, St.Stun, pull + KnutS.StunAfter, 0, 0, h);
                        AddStatus(h, St.Casting, pull); // he stands till the catch is in, so it never trails a walking Knut
                    }
                }
                else if (p.D >= KnutS.HookDistance - 1e-4f || wall || tower)
                {
                    p.Out = false; h.RemoveAll(St.Channel); p.Dur = p.T; p.Travel = p.D;
                    if (wall || tower) Blocked(p, p.X, p.Y);
                }
            }
            else
            {
                var c = p.Caught;
                p.D = p.Travel - KnutS.Reeled(p.T - p.Dur - (c != null ? KnutS.PullPause : 0)); // a catch hangs on the taut chain first
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
                float wr = TowerReach(h.X, h.Y, aa, NeedleReach, TiktaS.LineHitbox); // the warning ends on a base too
                WarnLine(h, aa, wr, 0.25f, (TiktaS.Count - 1) * TiktaS.Interval + NeedleReach / TiktaS.Speed);
                Channel(h, (TiktaS.Count - 1) * TiktaS.Interval);
                castX = h.X + SimMath.Cos(aa) * wr; castY = h.Y + SimMath.Sin(aa) * wr;
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

        /// <summary>A homing shot whose target fell or turned untargetable flies on to the spot it was aimed at (OX, OY) and puffs out
        /// there, no hit, so it is seen to land instead of vanishing in mid-air (TIKTA-07, BRAM-08).</summary>
        void FlyToSpotAndPuff(Proj p, float step)
        {
            float ox = p.X, oy = p.Y, dl = SimMath.Hypot(p.OX - p.X, p.OY - p.Y), k = dl <= step ? 1 : step / dl;
            p.X += (p.OX - p.X) * k; p.Y += (p.OY - p.Y) * k;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); }
            else if (k >= 1) { p.Done = true; Blocked(p, p.X, p.Y); }
        }

        void NeedleUpdate(Proj p, float dt)
        {
            var t = p.Tgt;
            if (t == null) { p.Done = true; return; }
            if (!p.Fizzle && (!t.Alive || t.Has(St.Untargetable))) { p.Fizzle = true; p.OX = t.X; p.OY = t.Y; } // keeps the spot it flew at
            float step = p.Speed * dt;
            if (p.Fizzle) { FlyToSpotAndPuff(p, step); return; } // its target fell or slipped away (TIKTA-07)
            float d = SimMath.Hypot(t.X - p.X, t.Y - p.Y);
            if (!Map.LosClear(p.X, p.Y, t.X, t.Y)) { p.Done = true; Blocked(p, p.X, p.Y); return; } // the target ducked behind a wall
            if (d <= step + t.R * 0.5f) { p.Done = true; SpecialHit(p.Src, t, p.Dmg + TiktaMarkBonus(p.Src, t, p), p.Kind, true); NeedlePoison(p.Src, t); return; } // + Deathmark in the same hit
            p.Ang = SimMath.Atan2(t.Y - p.Y, t.X - p.X);
            p.X += (t.X - p.X) / d * step; p.Y += (t.Y - p.Y) / d * step;
        }

        /// <summary>A dragged needle, swept (Tikta 2.0): the whole path flown this tick, up to the reach, is tested, so a big tick
        /// never carries it past an enemy; the first enemy along it is hit if no wall stands between, else it stops at the wall.</summary>
        void NeedleLineUpdate(Proj p, float dt)
        {
            if (p.D == 0 && p.Src != null && p.Src.Alive) { p.X = p.Src.X; p.Y = p.Src.Y; } // leaves from where she is now, as her warning (TIKTA-01)
            float ox = p.X, oy = p.Y, step = p.Speed * dt, len = Math.Max(0, Math.Min(step, NeedleReach - p.D));
            float tc = TowerReach(ox, oy, p.Ang, len, TiktaS.LineHitbox); // a base (either team's) stops it as a wall (TowerReach)
            var u = SweptHit(p.Src, p.Team, ox, oy, p.Ang, tc, TiktaS.LineHitbox, out float at); // nobody behind the base
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
            float move = Math.Max(0, Math.Min(step, NeedleReach - p.D)); // the last step ends at the reach, not past it (TIKTA-04)
            bool tower = tc < move - 1e-5f;
            if (tower) move = tc;
            p.X += SimMath.Cos(p.Ang) * move; p.Y += SimMath.Sin(p.Ang) * move; p.D += move;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y))
            {   // the spark where the line first meets the wall, not at the start of the step (TIKTA-04)
                float lo = 0, hi = move;
                for (int k = 0; k < 8; k++) { float mid = (lo + hi) * 0.5f; if (Map.ShotBlocked(ox, oy, ox + SimMath.Cos(p.Ang) * mid, oy + SimMath.Sin(p.Ang) * mid)) hi = mid; else lo = mid; }
                p.X = ox + SimMath.Cos(p.Ang) * lo; p.Y = oy + SimMath.Sin(p.Ang) * lo;
                p.Done = true; Blocked(p, p.X, p.Y); return;
            }
            if (tower) { p.Done = true; Blocked(p, p.X, p.Y); return; } // a puff on the base's face
            if (p.D >= NeedleReach - 1e-4f) p.Done = true;
        }

        /// <summary>Tikta still holds a needle of her drag, or it leaves this tick (from where she is then).</summary>
        bool NeedleInHand(Unit h)
        {
            for (int i = 0; i < Projs.Count; i++) { var q = Projs[i]; if (q.Src == h && !q.Done && q.Mode == ProjMode.NeedleLine && q.D <= q.Speed * Rules.Tick * 1.001f) return true; } // in the hand or leaving this tick
            return false;
        }

        /// <summary>The longest time one of Tikta's dragged needles already flying still needs to its reach (0 if none).</summary>
        float NeedleFlightLeft(Unit h)
        {
            float left = 0;
            for (int i = 0; i < Projs.Count; i++) { var q = Projs[i]; if (q.Src == h && !q.Done && q.Mode == ProjMode.NeedleLine && q.T >= 0) left = Math.Max(left, (NeedleReach - q.D) / q.Speed); }
            return left;
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
            // Rickard 2026-10-30: a tap lays the fire strip, a drag throws the bomb where it is aimed (the old game had them the other way)
            float x, y; Unit t = null;
            if (!Aimed(h, out float aa, out float ak))
            {   // Inferno Lane: a strip of fire at a random enemy hero or minion in reach with a clear line, cut at the first wall
                var l = EnemiesInRange(h, BlastS.LaneLength, Pick.HeroOrMinion, true, spBuf); // no hero first (Simon's tap rule)
                if (l.Count == 0) return false;
                t = PickRandom(l);
                float la = SimMath.Atan2(t.Y - h.Y, t.X - h.X), len = Map.LaneReach(h.X, h.Y, la, BlastS.LaneLength);
                var p = NewProj(ProjMode.FireLane, h, h.X, h.Y);
                p.Kind = ProjKind.BlastFire; p.Ang = la; p.D = len; p.Dmg = dmg * BlastS.LaneCoeff; p.Radius = BlastS.LaneHalfWidth; p.Dur = BlastS.LaneDuration;
                h.Facing = la; h.AimT = Time + 0.3f; AddStatus(h, St.Casting, 0.25f); AddStatus(h, St.Disarm, 0.25f);
                castX = h.X + SimMath.Cos(la) * len; castY = h.Y + SimMath.Sin(la) * len; castTgt = t;
                return true;
            }
            // Supernova: thrown in an arc (no clear line needed) onto the aimed spot, off a wall tile
            BombPoint(h, aa, ak, out x, out y);
            h.Facing = SimMath.Atan2(y - h.Y, x - h.X); AddStatus(h, St.Casting, 0.25f); AddStatus(h, St.Disarm, 0.25f);
            var b = NewProj(ProjMode.Bomb, h, x, y);
            b.Kind = ProjKind.BlastBomb; b.T = -BlastS.Delay; b.Radius = BlastS.Radius; b.Dur = BlastS.Delay; b.Dmg = dmg * BlastS.DamageCoeff;
            castX = x; castY = y; castTgt = t;
            return true;
        }

        /// <summary>Where a dragged Supernova lands: off a wall tile like Bram's anvil, so the blast is not buried in the wall.</summary>
        public void BombPoint(Unit h, float ang, float k, out float x, out float y) => LandPoint(h, ang, k, BlastS.Reach, out x, out y);

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
