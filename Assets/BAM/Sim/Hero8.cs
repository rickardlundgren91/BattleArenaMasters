using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Four heroes of BAM's own after tester Øli's ideas (Discord #heroes 2026-11-05: a werewolf, a wizard in a trance, a druid who turns
    // into a flock of crows, a witch who throws plants that slow and poison). Rickard 2026-11-09: "vi bygger alla fyra", kit approved on
    // a card the same day (doc "Nya egna hjältar – förslag"). Their own names, looks and numbers: nothing from the old game's heroes.
    // In HeroCatalog.Coming (Preview, PRACTICE > HERO TEST only) with stand-in bodies until Rickard's own models are in.

    /// <summary>Ragn, the werewolf blacksmith (hero 12, Fighter). Special, Moon Leap: he leaps up to LeapReach tiles onto a random enemy
    /// in reach (walls and bases stop the leap). Landing, the nearest enemy hero or minion within LandRadius takes LandCoeff of his
    /// damage, a Stun and a Shove (a knockback); Ragn gets a shield of ShieldPct of his max health for ShieldTime s either way.
    /// Passive, Wolf Run: after CalmTime s out of combat he runs RunBonus faster, until the next hit given or taken.</summary>
    public static class RagnS
    {
        public const float LeapReach = 5, LeapSpeed = 12, MinLeapTime = 0.2f, LandRadius = 1.2f, LandCoeff = 1.6f, Stun = 0.8f, Shove = 2;
        public const float ShieldPct = 0.15f, ShieldTime = 3;
        public const float CalmTime = 3, RunBonus = 0.15f;
    }

    /// <summary>Nässla, the swamp witch (hero 13, Mage/Support). Special, Thorn Thicket: a seed pod on a random enemy's spot in her
    /// range grows into a bramble of Radius for Duration s. Every Tick s enemies in it are slowed by Slow (Nässlas do not stack); every
    /// second (at 0.5, 1.5, 2.5 s) they take a poison tick (PoisonCoeff of her damage in all, split over the ticks) and her ally heroes
    /// in it get HealPct of their max health back (Passive, Healing Sap). It ends early when she dies.</summary>
    public static class NasslaS
    {
        public const float Reach = 3.6f, Radius = 2, Duration = 3, Tick = 0.5f, Slow = -0.35f, SlowTime = 0.6f, PoisonCoeff = 1.2f, HealPct = 0.02f;
        public const int PoisonTicks = 3;
    }

    /// <summary>Skrall, the storm monk (hero 14, Mage). Passive, Trance: every full second he stands still with nobody in his reach (so
    /// never in the middle of a fight) he gathers a step, at most Steps; his next auto attack spends them all for StepBonus more damage per step, and with all three it also
    /// jumps to another enemy within JumpRange of the target for JumpCoeff of that hit. Special, Thunder Judgement (tap only): three
    /// strikes on random enemies in his range (one enemy alone may get more than one), each after a red circle of Warn s (Gap s
    /// apart): every enemy within StrikeRadius takes StrikeCoeff of his damage, heroes a Stun.</summary>
    public static class SkrallS
    {
        public const int Steps = 3, Strikes = 3;
        public const float StepTime = 1, StepBonus = 0.4f, JumpRange = 2, JumpCoeff = 0.5f;
        public const float Warn = 0.4f, Gap = 0.15f, StrikeRadius = 0.8f, StrikeCoeff = 0.8f, Stun = 0.3f;
    }

    /// <summary>Ravna, the crow druid (hero 15, Assassin). Special, Crow Flock: she turns into a flock of crows and flies FlockReach
    /// tiles straight at a random enemy in that reach (walls and bases stop it) at FlockSpeed; while a flock she cannot be hit and
    /// passes through bodies, and every enemy hero or minion she flies through (within HitRadius) takes FlockCoeff of her damage
    /// once. Passive, Crow's Eye: an enemy hero her attack hurts is seen by everyone for MarkTime s, in grass too.</summary>
    public static class RavnaS
    {
        public const float FlockReach = 5.5f, FlockSpeed = 5.5f, HitRadius = 0.6f, FlockCoeff = 1.0f, MarkTime = 3;
    }

    public sealed partial class Unit
    {
        /// <summary>Ragn: while his Moon Leap flies (RagnLeapEnd >= 0), where it left from, where it lands and when it started.</summary>
        public float RagnLeapEnd = -1; internal float RagnFromX, RagnFromY, RagnToX, RagnToY, RagnLeapStart;
        /// <summary>Skrall: Trance steps gathered (0-3) and how long he has stood still towards the next.</summary>
        public int TranceSteps; internal float TranceStill;
        /// <summary>Ravna: while her Crow Flock flies (RavnaFlockEnd >= 0), its direction, and whom it has torn at.</summary>
        public float RavnaFlockEnd = -1; internal float RavnaAng;
        internal readonly List<Unit> FlockHit = new List<Unit>(8);
        public bool RagnLeaping => RagnLeapEnd >= 0;
        public bool RavnaFlying => RavnaFlockEnd >= 0;
    }

    public sealed partial class Match
    {
        // ---------- Ragn ----------
        /// <summary>Where Ragn's leap aimed at (ang, k) comes down: k of LeapReach the aimed way, cut where a wall or a base stops him.</summary>
        public void RagnLandPoint(Unit h, float ang, float k, out float x, out float y)
        {
            AimPoint(h, ang, k, RagnS.LeapReach, out float gx, out float gy, 0f);
            float ga = SimMath.Atan2(gy - h.Y, gx - h.X), want = SimMath.Hypot(gx - h.X, gy - h.Y);
            float c = SimMath.Cos(ga), s = SimMath.Sin(ga), step = 0.1f, d = 0;
            while (d < want - 1e-4f)
            {
                float nd = Math.Min(want, d + step);
                if (!Map.Free(h.X + c * nd, h.Y + s * nd, h.R) || TowerAt(h.X + c * nd, h.Y + s * nd, h.R)) break;
                d = nd;
            }
            x = h.X + c * d; y = h.Y + s * d;
        }

        bool CastRagn(Unit h)
        {
            if (h.RagnLeaping || !Aimed(h, out float aa, out float ak)) return false; // a leap needs a spot (the tap gives its target's)
            RagnLandPoint(h, aa, ak, out float x, out float y);
            float d = SimMath.Hypot(x - h.X, y - h.Y), t = Math.Max(RagnS.MinLeapTime, d / RagnS.LeapSpeed);
            h.RagnFromX = h.X; h.RagnFromY = h.Y; h.RagnToX = x; h.RagnToY = y; h.RagnLeapStart = Time; h.RagnLeapEnd = Time + t;
            if (d > 1e-3f) h.Facing = SimMath.Atan2(y - h.Y, x - h.X);
            h.AimT = Time + t + 0.2f;
            AddStatus(h, St.Casting, t); AddStatus(h, St.Disarm, t);
            castX = x; castY = y;
            return true;
        }

        /// <summary>Every tick for a living Ragn: the leap's flight and landing.</summary>
        void RagnTick(Unit h)
        {
            if (!h.RagnLeaping) return;
            float span = Math.Max(1e-3f, h.RagnLeapEnd - h.RagnLeapStart), k = SimMath.Clamp((Time - h.RagnLeapStart) / span, 0, 1);
            h.X = SimMath.Lerp(h.RagnFromX, h.RagnToX, k); h.Y = SimMath.Lerp(h.RagnFromY, h.RagnToY, k); h.MovedAt = Time;
            if (k < 1) return;
            h.RagnLeapEnd = -1;
            Unstick(h);
            RagnLand(h);
        }

        void RagnLand(Unit h)
        {
            Unit best = null; float bd = float.MaxValue;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(h, u)) continue;
                float d = SimMath.Dist(u, h);
                if (d > RagnS.LandRadius + u.R || !Map.LosClear(h, u)) continue;
                if (d < bd || (d == bd && u.Serial < best.Serial)) { bd = d; best = u; }
            }
            float sh = h.MaxHp * RagnS.ShieldPct;
            AddStatus(h, St.Shield, RagnS.ShieldTime, 0, sh, h);
            Emit(EventType.RagnLand, h, best, sh, (byte)HeroId.Ragn, best != null, h.X, h.Y);
            if (best == null) return;
            bool free = CcImmune(best);
            SpecialHit(h, best, h.StatDmg() * RagnS.LandCoeff, ProjKind.RagnLeap, true);
            if (!best.Alive || free) return;
            float ang = bd > 0.05f ? SimMath.Atan2(best.Y - h.Y, best.X - h.X) : SimMath.Atan2(h.RagnToY - h.RagnFromY, h.RagnToX - h.RagnFromX);
            InterruptSpecial(best);
            Knock(h, best, ang, RagnS.Shove, RagnS.Stun, null, ProjKind.RagnLeap);
        }

        /// <summary>Wolf Run: Ragn out of combat for CalmTime s runs RunBonus faster (Movement.SpeedOf).</summary>
        public bool RagnRunning(Unit u) => u.Kind == UnitKind.Hero && u.Hero == HeroId.Ragn && !u.IsClone && u.Alive && Time - u.LastCombat >= RagnS.CalmTime;

        // ---------- Nässla ----------
        bool CastNassla(Unit h, float dmg)
        {
            if (!Aimed(h, out float aa, out float ak)) return false; // the thicket needs a spot (the tap gives its target's)
            LandPoint(h, aa, ak, NasslaS.Reach, out float x, out float y);
            var p = NewProj(ProjMode.Bramble, h, x, y);
            p.Kind = ProjKind.NasslaBramble; p.Radius = NasslaS.Radius; p.Dur = NasslaS.Duration; p.Dmg = dmg * NasslaS.PoisonCoeff / NasslaS.PoisonTicks;
            h.Facing = SimMath.Atan2(y - h.Y, x - h.X); h.AimT = Time + 0.4f;
            AddStatus(h, St.Casting, 0.25f); AddStatus(h, St.Disarm, 0.25f);
            BrambleUpdate(p); // the slow is on at once
            castX = x; castY = y;
            return true;
        }

        /// <summary>The thicket: the slow every Tick s on every enemy inside (clear line from its middle), the poison and the sap on
        /// the odd ticks (about once a second, as every damage-over-time number shows). Ends after Duration or with Nässla.</summary>
        void BrambleUpdate(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || p.T >= NasslaS.Duration - 1e-4f) { p.Done = true; return; }
            int due = (int)Math.Floor((p.T + 1e-4f) / NasslaS.Tick) + 1; // tick 1 at the cast, then every 0.5 s
            while (p.Ticks < due)
            {
                p.Ticks++;
                bool sap = p.Ticks % 2 == 0; // 0.5, 1.5, 2.5 s
                areaBuf.Clear();
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Kind == UnitKind.Tower || u.Has(St.Untargetable)) continue;
                    if (SimMath.Dist(u, p.X, p.Y) > p.Radius + u.R || !Map.LosClear(p.X, p.Y, u.X, u.Y)) continue;
                    areaBuf.Add(u);
                }
                for (int i = 0; i < areaBuf.Count; i++)
                {
                    var u = areaBuf[i];
                    if (!u.Alive) continue;
                    if (u.Team != h.Team)
                    {
                        AddSlowOnce(u, St.SlowPct, NasslaS.SlowTime, NasslaS.Slow, h);
                        if (sap) SpecialHit(h, u, p.Dmg, ProjKind.NasslaBramble, false, DmgKind.Special, DmgFlags.NotDirect);
                    }
                    else if (sap && u.Kind == UnitKind.Hero && !u.IsClone) Heal(u, u.MaxHp * NasslaS.HealPct, h);
                }
            }
        }

        // ---------- Skrall ----------
        /// <summary>Every tick for a living Skrall: Trance gathers a step for every full second he stands still without attacking.</summary>
        void SkrallTick(Unit h, float dt)
        {
            // still: he did not move this tick, is not casting, and has nobody in his reach to fight (so a fight never feeds it)
            bool still = Time - h.MovedAt > dt * 0.5f && !h.Has(St.Casting) && EnemiesInRange(h, h.Range, Pick.Any, true, spBuf).Count == 0;
            if (!still || h.TranceSteps >= SkrallS.Steps) { h.TranceStill = 0; return; }
            h.TranceStill += dt;
            if (h.TranceStill < SkrallS.StepTime - 1e-4f) return;
            h.TranceStill = 0; h.TranceSteps++;
            Emit(EventType.TranceStep, h, null, h.TranceSteps, (byte)HeroId.Skrall, h.TranceSteps >= SkrallS.Steps, h.X, h.Y);
        }

        /// <summary>An auto attack spends the Trance: the damage share it adds, and the steps (for the jump on the hit).</summary>
        float TranceSpend(Unit h, out int steps)
        {
            steps = h.TranceSteps; h.TranceSteps = 0; h.TranceStill = 0;
            return 1 + SkrallS.StepBonus * steps;
        }

        /// <summary>A full Trance's hit jumps on: another enemy hero or minion within JumpRange of the target, with a clear line.</summary>
        void TranceJump(Unit h, Unit t, float dealt0)
        {
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u == t || !Hurtable(h, u) || SimMath.Dist(u, t) > SkrallS.JumpRange + u.R || !IsVisibleTo(u, h.Team) || !Map.LosClear(t, u)) continue;
                areaBuf.Add(u);
            }
            if (areaBuf.Count == 0) return;
            var o = PickRandom(areaBuf);
            SpecialHit(h, o, dealt0 * SkrallS.JumpCoeff, ProjKind.SkrallBolt, true, DmgKind.Passive);
        }

        bool CastSkrall(Unit h, float dmg)
        {
            var l = EnemiesInRange(h, h.Range, Pick.HeroOrMinion, true, spBuf);
            if (l.Count == 0) return false;
            Unit first = null;
            for (int i = 0; i < SkrallS.Strikes; i++)
            {
                Unit t;
                if (l.Count > 0) { int j = Rng.Int(l.Count); t = l[j]; l.RemoveAt(j); } // three different enemies while there are
                else t = PickRandom(areaBuf);
                if (i == 0) { first = t; areaBuf.Clear(); }
                areaBuf.Add(t);
                float wait = SkrallS.Warn + SkrallS.Gap * i;
                var p = NewProj(ProjMode.Strike, h, t.X, t.Y);
                p.Kind = ProjKind.SkrallStrike; p.T = -wait; p.Dur = wait; p.Radius = SkrallS.StrikeRadius; p.Dmg = dmg * SkrallS.StrikeCoeff;
                WarnOf(WarnCircle(h, t.X, t.Y, SkrallS.StrikeRadius, wait), p);
            }
            h.Facing = SimMath.Atan2(first.Y - h.Y, first.X - h.X); h.AimT = Time + 0.4f;
            AddStatus(h, St.Casting, 0.3f); AddStatus(h, St.Disarm, 0.3f);
            castX = first.X; castY = first.Y; castTgt = first;
            return true;
        }

        /// <summary>One strike comes down: every enemy hero and minion (not towers) within its radius, heroes stunned (Moa's shield
        /// blocks it). After Skrall's death it still comes down (as passive damage).</summary>
        void SkrallStrike(Proj p)
        {
            var h = p.Src; var dk = h.Alive ? DmgKind.Special : DmgKind.Passive;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (Hurtable(h, u) && SimMath.Dist(u, p.X, p.Y) <= p.Radius + u.R) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                SpecialHit(h, u, p.Dmg, p.Kind, true, dk);
                if (u.Alive && u.Kind == UnitKind.Hero) AddStatus(u, St.Stun, SkrallS.Stun, 0, 0, h);
            }
            Emit(EventType.Explosion, h, null, p.Radius, (byte)p.Kind, true, p.X, p.Y, dk);
        }

        // ---------- Ravna ----------
        /// <summary>How far her flock flies from (x, y) along ang before a wall or a base stops it (also the red warning's length).</summary>
        public float FlockReach(Unit h, float ang) => TowerReach(h.X, h.Y, ang, Map.LaneReach(h.X, h.Y, ang, RavnaS.FlockReach), h.R);

        bool CastRavna(Unit h)
        {
            if (h.RavnaFlying || !Aimed(h, out float aa, out _)) return false; // the flock needs a way (the tap gives its target's)
            float len = FlockReach(h, aa), t = Math.Max(0.1f, len / RavnaS.FlockSpeed);
            h.RavnaAng = aa; h.RavnaFlockEnd = Time + t; h.FlockHit.Clear();
            h.Facing = aa; h.AimT = Time + t + 0.1f;
            AddStatus(h, St.Untargetable, t); AddStatus(h, St.Casting, t); AddStatus(h, St.Disarm, t);
            castX = h.X + SimMath.Cos(aa) * len; castY = h.Y + SimMath.Sin(aa) * len;
            RavnaTears(h);
            return true;
        }

        /// <summary>Every tick for a living Ravna: the flock flies on (stopping at a wall or a base) and tears at whom it passes.</summary>
        void RavnaTick(Unit h, float dt)
        {
            if (!h.RavnaFlying) return;
            float c = SimMath.Cos(h.RavnaAng), s = SimMath.Sin(h.RavnaAng), step = RavnaS.FlockSpeed * dt;
            float nx = h.X + c * step, ny = h.Y + s * step;
            bool stop = !Map.Free(nx, ny, h.R) || TowerAt(nx, ny, h.R);
            if (!stop) { h.X = nx; h.Y = ny; h.MovedAt = Time; RavnaTears(h); }
            if (stop || Time >= h.RavnaFlockEnd - 1e-4f) RavnaLand(h);
        }

        void RavnaTears(Unit h)
        {
            float dmg = h.StatDmg() * RavnaS.FlockCoeff;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(h, u) || h.FlockHit.Contains(u) || SimMath.Dist(u, h) > RavnaS.HitRadius + u.R) continue;
                h.FlockHit.Add(u);
                SpecialHit(h, u, dmg, ProjKind.RavnaFlock, true);
            }
        }

        void RavnaLand(Unit h)
        {
            h.RavnaFlockEnd = -1; h.FlockHit.Clear();
            h.RemoveAll(St.Untargetable); h.RemoveAll(St.Casting); h.RemoveAll(St.Disarm);
            Unstick(h);
            Emit(EventType.FlockEnd, h, null, 0, (byte)HeroId.Ravna, false, h.X, h.Y);
        }

        /// <summary>Crow's Eye: an enemy hero her auto attack hurt stays seen for MarkTime s, in grass too (RevealT).</summary>
        static void CrowsEye(Unit h, Unit t, float dealt)
        {
            if (t.Kind != UnitKind.Hero || t.IsClone || dealt <= 0) return;
            t.RevealT = Math.Max(t.RevealT, RavnaS.MarkTime);
        }

        // ---------- all four ----------
        /// <summary>Every tick for a living real hero of the four.</summary>
        void Hero8Tick(Unit h, float dt)
        {
            switch (h.Hero)
            {
                case HeroId.Ragn: RagnTick(h); break;
                case HeroId.Skrall: SkrallTick(h, dt); break;
                case HeroId.Ravna: RavnaTick(h, dt); break;
            }
        }

        /// <summary>A death ends a leap, a flock and the Trance.</summary>
        static void Hero8Reset(Unit h)
        {
            h.RagnLeapEnd = -1; h.RavnaFlockEnd = -1; h.FlockHit.Clear(); h.TranceSteps = 0; h.TranceStill = 0;
        }
    }
}
