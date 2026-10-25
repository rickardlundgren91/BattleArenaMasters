using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    [Flags]
    internal enum DmgFlags : byte
    {
        None = 0,
        Burning = 1,   // a burn tick: its amount was finished when the burn was lit, so it skips the multipliers
        DotTick = 2,   // a poison tick
        NotDirect = 4, // from a hero but not a direct hit (Inferno Lane's ticks): no Queen's Instinct, no clone
    }

    // Damage, death, auto attacks and shots (the old game's Sim.dealDamage, kill and SimCombat).
    public sealed partial class Match
    {
        // ---------- statuses ----------
        static bool IsControl(St t) => t == St.Stun || t == St.Root || t == St.SlowFlat || t == St.SlowPct;

        /// <summary>A hero under one of Moa's shields (not Queen's Instinct) shrugs off stuns, roots and slows.</summary>
        public bool CcImmune(Unit u)
        {
            if (u.Kind != UnitKind.Hero) return false;
            for (int i = 0; i < u.StCount; i++)
            {
                ref var s = ref u.Sts[i];
                if (s.Type == St.Shield && s.Amount > 0 && !s.Queen && s.By != null && s.By.Kind == UnitKind.Hero && s.By.Hero == HeroId.Moa) return true;
            }
            return false;
        }

        /// <summary>Adds a status; control on a CC-immune hero is ignored (-1). A stun or root on a hero cuts its special.</summary>
        internal int AddStatus(Unit u, St type, float dur, float v = 0, float amount = 0, Unit by = null, bool queen = false)
        {
            if (IsControl(type) && CcImmune(u)) return -1;
            int i = u.AddRaw(type, dur, v, amount, by, queen);
            if ((type == St.Stun || type == St.Root) && dur > 0 && u.Kind == UnitKind.Hero) InterruptSpecial(u);
            return i;
        }

        /// <summary>A short cast state that a stun or root can cut (Tyra's draw, Tikta's storm, Knut's throw).</summary>
        internal void Channel(Unit h, float dur)
        {
            h.RemoveAll(St.Channel);
            if (dur > 0) AddStatus(h, St.Channel, dur, dur);
        }

        /// <summary>A stun or root on a hero cancels what of its special is still under way.</summary>
        internal void InterruptSpecial(Unit u)
        {
            if (u.Kind != UnitKind.Hero || !u.Alive) return;
            bool any = false;
            if (u.Needles > 0) { u.Needles = 0; any = true; }
            for (int i = 0; i < Projs.Count; i++)
            {
                var p = Projs[i];
                if (p.Src != u || p.Done) continue;
                if (p.T < 0 && (p.Mode == ProjMode.Bolt || p.Mode == ProjMode.NeedleLine)) { p.Done = true; any = true; } // still in the hand
                else if (p.Mode == ProjMode.Hook && p.Out && p.Caught == null) { p.Out = false; any = true; }          // the anchor comes back
                else if (p.IsWarning) p.Done = true;                                                                    // its red warning goes with it
            }
            if (!any) { u.RemoveAll(St.Channel); return; }
            u.RemoveAll(St.Casting); u.RemoveAll(St.Channel);
            Emit(EventType.Interrupt, u, null);
        }

        public static bool CanAct(Unit u) => !u.Has(St.Stun);

        // ---------- damage ----------
        /// <summary>Deals amount to tgt from src (src can be null). Returns the health actually taken.</summary>
        internal float DealDamage(Unit src, Unit tgt, float amount, DmgKind kind, DmgFlags flags = DmgFlags.None)
        {
            if (!tgt.Alive || amount <= 0) return 0;
            if (tgt.Has(St.Invulnerable) || tgt.Has(St.Untargetable)) return 0;
            var att = src != null ? (src.Owner ?? src) : null;
            if (tgt.Kind == UnitKind.Hero) tgt.LastHurt = Time;
            if (att != null) att.RevealT = Rules.RevealTime;
            tgt.RevealT = Rules.RevealTime;
            bool burning = (flags & DmgFlags.Burning) != 0;
            if (!burning)
            {
                amount *= tgt.TakenMult;
                if (tgt.TutUnkillable && amount >= tgt.Hp) amount = Math.Max(0, tgt.Hp - 1);
                if (tgt.IsClone) amount *= 2;
                if (tgt.Kind == UnitKind.Tower && Tut == null) amount *= TowerLateTaken() * TowerNeedsWave(att, tgt);
                if (tgt.Kind == UnitKind.Tower && src != null && src.IsMinionLike) amount *= PacingMinionTowerMult(); // the old wolf counted as a minion
                if (Tut != null && (tgt == Tut.Tower || tgt == Tut.Dummy)) amount = TutDamage(src, tgt, amount, kind);
            }
            tgt.LastCombat = Time;
            if (src != null)
            {
                src.LastCombat = Time;
                if (src.Kind == UnitKind.Hero && att.HeroIndex >= 0) tgt.LastHitBy[att.HeroIndex] = Time;
            }
            if (src != null && src.Kind == UnitKind.Hero && tgt.Kind == UnitKind.Hero)
            {
                var t = Towers[tgt.Team]; // a hero hitting a hero under its tower draws the tower's fire
                if (t != null && t.Alive && SimMath.Dist(t, src) <= t.UDef.Range + src.R) t.Target = src;
            }
            for (int i = 0; i < tgt.StCount && amount > 0; i++)
            {
                ref var s = ref tgt.Sts[i];
                if (s.Type != St.Shield || s.Amount <= 0) continue;
                float a = Math.Min(s.Amount, amount); s.Amount -= a; amount -= a;
            }
            if (amount <= 0) return 0;
            amount = QueenInstinct(src, tgt, amount, kind, flags);
            if (amount <= 0) return 0;
            float taken = Math.Min(amount, tgt.Hp);
            tgt.Hp -= amount;
            Emit(EventType.Damage, att, tgt, taken, 0, false, 0, 0, kind);
            if (src != null && src.Kind == UnitKind.Hero && !src.IsClone && src.Hero == HeroId.Blast && !burning) Ignite(src, tgt, amount);
            if (tgt.Hp <= 0) Kill(tgt, src);
            else CloneOnHurt(tgt, src, kind, flags);
            return taken;
        }

        internal void Heal(Unit u, float amt, Unit by)
        {
            if (!u.Alive || amt <= 0) return;
            float before = u.Hp; u.Hp = Math.Min(u.MaxHp, u.Hp + amt);
            if (u.Hp - before > 0) Emit(EventType.Heal, by, u, u.Hp - before);
        }

        // ---------- death ----------
        void Kill(Unit u, Unit src)
        {
            u.Alive = false; u.Hp = 0;
            var killer = src != null ? (src.Owner ?? src) : null;
            if (u.Kind == UnitKind.Tower)
            {
                if (u.Team == 0) Map.TowerTile0 = -1; else Map.TowerTile1 = -1;
                Emit(EventType.TowerDown, u, null);
                EndMatch(Teams.Other(u.Team));
                return;
            }
            if (u.IsClone) { Emit(EventType.CloneEnd, u, u.Owner); return; }
            if (u.IsMinionLike) { u.DeadT = 0.6f; Emit(EventType.Death, killer, u); return; }

            // a hero
            u.Deaths++; u.RespawnT = RespawnTime(u); u.DiedAt = Time;
            u.ClearStatuses(); u.Needles = 0; u.ClonePending = false;
            int kt = Teams.Other(u.Team);
            Kills[kt]++;
            u.Streak = 0;
            // the team stars: the killers' team +1 (at most 3), the dead hero's team loses all; every hero's pips mirror its team's
            Stars[kt] = Math.Min(Defs.MaxStars, Stars[kt] + 1); Stars[u.Team] = 0;
            for (int i = 0; i < Units.Count; i++)
            {
                var h = Units[i]; if (h.Kind != UnitKind.Hero) continue;
                h.Pips = Stars[h.Team];
                float old = h.MaxHp; h.MaxHp = h.StatHp();
                if (h.Alive && old > 0) h.Hp = Math.Min(h.MaxHp, h.Hp * h.MaxHp / old);
            }
            for (int i = 0; i < Heroes.Count; i++)
            {
                var a = Heroes[i];
                if (a != killer && a.Team != u.Team && Time - u.LastHitBy[i] < Rules.AssistWindow) a.Assists++;
            }
            int streak = 0;
            if (killer != null && killer.Kind == UnitKind.Hero) { killer.Kills++; killer.Streak++; streak = killer.Streak; }
            for (int i = 0; i < u.LastHitBy.Length; i++) u.LastHitBy[i] = -99;
            Emit(EventType.Death, killer, u, streak);
        }

        /// <summary>The wait before a dead hero comes back: 8 s, +1 s per earlier death, and from level 10 more with the level and
        /// the match clock (at most 20 s more).</summary>
        public float RespawnTime(Unit u)
        {
            float t = Rules.RespawnBase + Rules.RespawnPerDeath * (u.Deaths - 1);
            if (u.Level < Rules.RespawnLateLevel) return t;
            float since = Math.Max(0, Time - (Rules.RespawnLateLevel - 1) * Rules.SecondsPerLevel);
            float extra = Rules.RespawnLatePerLevel * (u.Level - (Rules.RespawnLateLevel - 1)) + Rules.RespawnLatePerMinute * since / 60f;
            return t + Math.Min(Rules.RespawnLateMax, extra);
        }

        void Revive(Unit u)
        {
            u.Alive = true; u.Hp = u.MaxHp; u.X = u.PX = u.SpawnX; u.Y = u.PY = u.SpawnY;
            AddStatus(u, St.Invulnerable, Rules.RespawnInvulnerable);
            u.AtkTimer = 0; u.RespawnT = 0; u.MoInit = false;
            Emit(EventType.Respawn, u, null);
        }

        // ---------- towers ----------
        /// <summary>Is enemy hero e fighting one of tower tw's heroes right now (hit one, or was hit by one, in the last 2 s)?</summary>
        internal bool TowerAggro(Unit tw, Unit e)
        {
            if (e.Kind != UnitKind.Hero || e.Team == tw.Team) return false;
            for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Team == tw.Team && Time - e.LastHitBy[i] < Rules.TowerAggroTime) return true;
            if (e.HeroIndex < 0) return false;
            for (int i = 0; i < Units.Count; i++)
            {
                var a = Units[i];
                if (a.Kind == UnitKind.Hero && a.Team == tw.Team && a.Alive && Time - a.LastHitBy[e.HeroIndex] < Rules.TowerAggroTime) return true;
            }
            return false;
        }
        /// <summary>From level 10 heroes take more from tower shots, growing with the match clock.</summary>
        internal float TowerLateMult(Unit h)
        {
            if (h.Level < Rules.TowerLateLevel) return 1;
            float since = Math.Max(0, Time - (Rules.TowerLateLevel - 1) * Rules.SecondsPerLevel);
            return Math.Min(Rules.TowerLateMax, 1 + Rules.TowerLateBase + Rules.TowerLatePerLevel * (h.Level - Rules.TowerLateLevel) + Rules.TowerLatePerMinute * since / 60);
        }
        /// <summary>From level 8 towers take more damage, growing with the match clock (at most x5).</summary>
        internal float TowerLateTaken() => Math.Min(Rules.TowerTakenMax, 1 + Rules.TowerTakenPerMinute * Math.Max(0, Time - (Rules.TowerTakenLevel - 1) * Rules.SecondsPerLevel) / 60);
        /// <summary>A hero hitting a tower without one of its own minions in the tower's range gets only half of the late bonus.</summary>
        float TowerNeedsWave(Unit att, Unit tw)
        {
            if (att == null || att.Kind != UnitKind.Hero) return 1;
            float late = TowerLateTaken(); if (late <= 1) return 1;
            for (int i = 0; i < Units.Count; i++)
            {
                var m = Units[i];
                if (m.IsMinionLike && m.Alive && m.Team == att.Team && SimMath.Dist(m, tw) <= tw.UDef.Range + m.R) return 1;
            }
            return (1 + (late - 1) * 0.5f) / late;
        }
        float TowerCoeff(Unit tw, Unit tgt) => tgt.Kind != UnitKind.Hero || Tut != null ? 1
            : TowerLateMult(tgt) * (tgt == tw.TowerFocus ? 1 + Math.Min(Rules.TowerRampMax, Rules.TowerRamp * tw.TowerStreak) : 1);

        // ---------- auto attacks ----------
        void UpdateAttack(Unit u, float dt)
        {
            if (!u.Alive || (u.Kind == UnitKind.Tower && State != MatchState.Play)) return;
            if (u.Has(St.Stun) || u.Has(St.Disarm)) return;
            float cd = u.AttackCd;
            u.AtkTimer += dt;
            if (u.AtkTimer < cd) return;
            var targets = EnemiesInRange(u, u.Range, Pick.Any, u.Kind != UnitKind.Wolf, atkBuf); // the spectral wolf bites through walls
            Unit tgt;
            if (u.Kind == UnitKind.Tower)
            {
                var k = u.Target;
                if (k != null && (!k.Alive || SimMath.Dist(u, k) > u.Range + k.R || !IsVisibleTo(k, u.Team) || !Map.LosClear(u, k))) u.Target = null;
                // minions first, but an enemy hero fighting one of the tower's heroes is shot instead
                if (!(u.Target != null && TowerAggro(u, u.Target)))
                {
                    Unit fighter = null; float bd = float.MaxValue;
                    for (int i = 0; i < targets.Count; i++) { var t = targets[i]; if (!TowerAggro(u, t)) continue; float d = SimMath.Dist(u, t); if (d < bd) { bd = d; fighter = t; } }
                    if (fighter != null) u.Target = fighter;
                }
                if (u.Target == null)
                {
                    Unit m = null; float bd = float.MaxValue;
                    for (int i = 0; i < targets.Count; i++) { var t = targets[i]; if (!t.IsMinionLike) continue; float d = SimMath.Dist(u, t); if (d < bd) { bd = d; m = t; } }
                    u.Target = m ?? Nearest(targets, u.X, u.Y);
                }
                tgt = u.Target;
                // shots in a row at the same hero hit harder; it starts over on another target or after 2.5 s without a shot
                if (tgt != null)
                {
                    if (tgt.Kind == UnitKind.Hero && tgt == u.TowerFocus && Time - u.TowerShotT < Rules.TowerRampReset) u.TowerStreak++; else u.TowerStreak = 0;
                    u.TowerFocus = tgt; u.TowerShotT = Time;
                }
                else if (Time - u.TowerShotT >= Rules.TowerRampReset) { u.TowerFocus = null; u.TowerStreak = 0; }
            }
            else if (u.Kind == UnitKind.Minion) tgt = u.MbTgt != null && targets.Contains(u.MbTgt) ? u.MbTgt : Nearest(targets, u.X, u.Y);
            else if (u.Kind == UnitKind.Wolf) tgt = u.WolfPrey != null && targets.Contains(u.WolfPrey) ? u.WolfPrey : Nearest(targets, u.X, u.Y);
            else tgt = targets.Count > 0 ? PickRandom(targets) : null; // D1: a hero's auto attack picks a random target in range
            if (tgt != null)
            {
                AutoAttack(u, tgt); u.AtkTimer = 0;
                if (u.Kind != UnitKind.Tower) { u.Target = tgt; u.TargetT = Time; }
            }
            else if (u.Kind == UnitKind.Hero) u.AtkTimer = 0; // a hero's swing runs out; others hold it ready
            else u.AtkTimer = cd;
        }

        void AutoAttack(Unit u, Unit tgt)
        {
            bool hero = u.Kind == UnitKind.Hero;
            float spawnDelay, projSpeed, projTime, dmg, coeff = 1; ProjKind kind; bool big = false;
            if (hero)
            {
                var d = u.HDef; spawnDelay = d.SpawnDelay; projSpeed = d.ProjSpeed; projTime = d.ProjTime; kind = d.Attack;
                dmg = u.StatDmg();
                coeff *= HeroAttackCoeff(u, tgt, ref big);
                if (AmbushReady(u)) { coeff *= 1 + Rules.AmbushBonus; big = true; Emit(EventType.Ambush, u, tgt); }
            }
            else
            {
                var d = u.UDef; spawnDelay = d.SpawnDelay; projSpeed = d.ProjSpeed; projTime = d.ProjTime; kind = d.Attack; dmg = u.DefDmg;
                if (u.Kind == UnitKind.Tower) coeff = TowerCoeff(u, tgt);
                if (u.Kind == UnitKind.Minion && u.Minion == MinionKind.Siege && tgt.Kind == UnitKind.Tower) coeff *= Rules.SiegeVsTower;
            }
            u.Facing = SimMath.Atan2(tgt.Y - u.Y, tgt.X - u.X); u.AimT = Time + spawnDelay + 0.1f;
            float travel = projTime != 0 ? projTime : 0.1f;
            if (hero && u.Hero == HeroId.Tyra) travel = SimMath.Clamp(SimMath.Dist(u, tgt) / 15f, 0.08f, projTime); // her arrow flies at a steady 15 tiles/s
            var p = NewProj(ProjMode.Auto, u, u.X, u.Y);
            p.Kind = kind; p.Tgt = tgt; p.Dmg = dmg * coeff; p.T = -spawnDelay; p.Speed = projSpeed; p.Travel = travel; p.Big = big; p.OnHit = hero;
            p.NoWall = u.Kind == UnitKind.Wolf;
            Emit(EventType.AttackStart, u, tgt, spawnDelay, (byte)kind, big);
        }

        bool AmbushReady(Unit u) => u.Kind == UnitKind.Hero && !u.IsClone && Tut == null && Map.InGrass(u.X, u.Y) && HiddenFromFoes(u);

        // ---------- shots ----------
        void UpdateProjectiles(float dt)
        {
            projBuf.Clear(); projBuf.AddRange(Projs);
            for (int i = 0; i < projBuf.Count; i++)
            {
                var p = projBuf[i];
                if (p.Done) continue;
                if (p.Mode == ProjMode.Auto && p.Tgt != null && !p.Tgt.Alive) { RetargetAuto(p); if (p.Done) continue; }
                p.T += dt;
                if (p.T < 0)
                {
                    if (p.IsShot && p.Src != null) { p.X = p.Src.X; p.Y = p.Src.Y; } // still in the shooter's hand
                    continue;
                }
                switch (p.Mode)
                {
                    case ProjMode.Auto: UpdateAuto(p, dt); break;
                    case ProjMode.Hammer: HammerUpdate(p, dt); break;
                    case ProjMode.HammerLine: HammerLineUpdate(p, dt); break;
                    case ProjMode.Bolt: BoltUpdate(p, dt); break;
                    case ProjMode.Hook: HookUpdate(p, dt); break;
                    case ProjMode.Needle: NeedleUpdate(p, dt); break;
                    case ProjMode.NeedleLine: NeedleLineUpdate(p, dt); break;
                    case ProjMode.Bomb: p.Done = true; BombBlast(p); break;
                    case ProjMode.FireLane: FireLaneUpdate(p); break;
                    case ProjMode.HoneyField: HoneyFieldUpdate(p); break;
                    case ProjMode.WarnLine: case ProjMode.WarnCircle: WarnUpdate(p); break;
                }
            }
            int w = 0;
            for (int i = 0; i < Projs.Count; i++)
            {
                var p = Projs[i];
                if (p.Done) projPool.Add(p); else Projs[w++] = p;
            }
            if (w < Projs.Count) Projs.RemoveRange(w, Projs.Count - w);
        }

        void UpdateAuto(Proj p, float dt)
        {
            var tgt = p.Tgt;
            if (!p.Launched) { p.Launched = true; p.OX = p.Src.X; p.OY = p.Src.Y; } // the shot leaves where the shooter stood at the release
            float ox = p.X, oy = p.Y; bool los = !p.NoWall;
            if (p.Speed > 0)
            {
                float d = SimMath.Hypot(tgt.X - p.X, tgt.Y - p.Y), step = p.Speed * Rules.Tick;
                if (d <= step || d < 0.05f)
                {
                    p.Done = true;
                    if (!los || !Map.ShotBlocked(ox, oy, tgt.X, tgt.Y)) HitWith(p); else Blocked(p, ox, oy);
                }
                else
                {
                    p.X += (tgt.X - p.X) / d * step; p.Y += (tgt.Y - p.Y) / d * step;
                    if (los && Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); }
                }
            }
            else
            {
                float k = SimMath.Clamp(p.T / p.Travel, 0, 1);
                p.X = SimMath.Lerp(p.OX, tgt.X, k); p.Y = SimMath.Lerp(p.OY, tgt.Y, k);
                if (los && Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); }
                else if (p.T >= p.Travel) { p.Done = true; HitWith(p); }
            }
            if (!tgt.Alive && !p.Done) p.Done = true;
        }

        /// <summary>A shot stopped by a wall still shows something where it stopped.</summary>
        void Blocked(Proj p, float x, float y) => Emit(EventType.Blocked, p.Src, p.Tgt, 0, (byte)p.Kind, false, x, y);

        /// <summary>An auto attack whose target died before it landed goes to another enemy in range (a hero picks at random,
        /// others the nearest); with none the swing is dropped.</summary>
        void RetargetAuto(Proj p)
        {
            var u = p.Src;
            if (u == null || !u.Alive) return; // the shot just ends
            var list = EnemiesInRange(u, u.Range, Pick.Any, u.Kind != UnitKind.Wolf, atkBuf);
            if (list.Count > 0)
            {
                p.Tgt = u.Kind == UnitKind.Hero ? PickRandom(list) : Nearest(list, u.X, u.Y);
                u.Facing = SimMath.Atan2(p.Tgt.Y - u.Y, p.Tgt.X - u.X);
                return;
            }
            p.Done = true;
        }

        void HitWith(Proj p)
        {
            var s = p.Src; var t = p.Tgt;
            if (!t.Alive) return;
            float dealt = DealDamage(s, t, p.Dmg, DmgKind.Auto);
            Emit(EventType.Hit, s, t, dealt, (byte)p.Kind, p.Big, t.X, t.Y, DmgKind.Auto); // always: every attack shows where it landed
            if (!p.OnHit || (!s.Alive && !s.IsClone)) return;
            if (s.Kind == UnitKind.Hero && !s.IsClone) OnHeroHit(s, t, p);
        }

        void OnHeroHit(Unit h, Unit t, Proj p)
        {
            h.Charge = Math.Min(h.HDef.MaxCharge, h.Charge + 1);
            if (h.HDef.OnHitSlow != 0 && (t.Kind == UnitKind.Hero || t.IsMinionLike)) AddStatus(t, St.SlowFlat, h.HDef.OnHitSlowTime, h.HDef.OnHitSlow, 0, h);
            if (h.Hero == HeroId.Bram) BramPassive(h, t, p);
        }

        // ---------- what the bots know ----------
        internal enum Flight : byte { Const, Linear, Hook }
        static float FlightTime(Flight f, float a, float b, float d)
        {
            switch (f)
            {
                case Flight.Linear: return a + d / b;
                case Flight.Hook: return a * (1 - SimMath.Sqrt(Math.Max(0, 1 - Math.Min(d, b) / b)));
                default: return a;
            }
        }

        /// <summary>Where a tap special aims at t: a bot leads a moving hero by its tier's share of the motion and misses by its
        /// noise; the player's tap (and a target that is not a hero) gets the exact spot.</summary>
        internal void LeadAim(Unit h, Unit t, Flight f, float a, float b, out float ax, out float ay)
        {
            if (h.Bot == null || t.Kind != UnitKind.Hero || t.IsClone) { ax = t.X; ay = t.Y; return; }
            var tier = h.Bot.Tier;
            float k = tier.AimLead * Rng.Range(0.8f, 1.15f) * (tier.V3 ? 0.7f : 1);
            float n = tier.AimNoise * (1 - 0.25f * h.Bot.Skill) * (tier.V3 ? 1.2f : 1);
            float ft = FlightTime(f, a, b, SimMath.Dist(h, t));
            for (int i = 0; i < 2; i++) ft = FlightTime(f, a, b, SimMath.Hypot(t.X + t.MoVx * ft * k - h.X, t.Y + t.MoVy * ft * k - h.Y));
            ax = t.X + t.MoVx * ft * k + Rng.Range(-n, n) * 0.5f;
            ay = t.Y + t.MoVy * ft * k + Rng.Range(-n, n) * 0.5f;
        }

        /// <summary>The smoothed motion of a hero (for lead aim) and where each enemy team last saw it.</summary>
        void TrackMotion(Unit u, float dt)
        {
            if (!u.MoInit) { u.MoX = u.X; u.MoY = u.Y; u.MoVx = u.MoVy = 0; u.MoInit = true; }
            float k = Math.Min(1, dt / 0.25f), vx = (u.X - u.MoX) / dt, vy = (u.Y - u.MoY) / dt;
            if (SimMath.Hypot(vx, vy) > 9) { vx = 0; vy = 0; }
            u.MoVx += (vx - u.MoVx) * k; u.MoVy += (vy - u.MoVy) * k;
            u.MoX = u.X; u.MoY = u.Y;
            for (int tm = 0; tm < 2; tm++)
                if (tm != u.Team && IsVisibleTo(u, tm)) { u.LastSeenX[tm] = u.X; u.LastSeenY[tm] = u.Y; u.LastSeenT[tm] = Time; }
        }
    }
}
