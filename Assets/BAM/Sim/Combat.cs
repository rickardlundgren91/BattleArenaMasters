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
        NotDirect = 4, // from a hero but not a direct hit (Inferno Lane's ticks): no Queen's Instinct (Fenn's clone still comes, as the old Phantom Step)
        NoIgnite = 8,  // Blast's Chain Reaction hits: they light no burn
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

        /// <summary>A unit knocked back less than Rules.KnockCooldown s ago is not knocked again (it still takes the hit).</summary>
        public bool KnockImmune(Unit u) => Time < u.KnockFreeAt || u.KnockPad && u.KnockT > 0; // a hero in a jump pad's throw flies on

        /// <summary>Adds a status; control on a CC-immune hero is ignored (-1). A stun or root on a hero cuts its special.</summary>
        internal int AddStatus(Unit u, St type, float dur, float v = 0, float amount = 0, Unit by = null, bool queen = false)
        {
            if (IsControl(type) && CcImmune(u)) return -1;
            int i = u.AddRaw(type, dur, v, amount, by, queen);
            if ((type == St.Stun || type == St.Root) && dur > 0 && u.Kind == UnitKind.Hero) InterruptSpecial(u, type == St.Stun);
            return i;
        }

        /// <summary>Knut's slows do not stack between Knuts (three Knuts slow like one, 2026-10-30 balance): the same slow from
        /// another hero of the same kind is refreshed instead of added.</summary>
        internal void AddSlowOnce(Unit u, St type, float dur, float v, Unit by)
        {
            if (CcImmune(u)) return;
            for (int i = 0; i < u.StCount; i++)
            {
                ref var s = ref u.Sts[i];
                if (s.Type == type && s.V == v && s.By != null && s.By.Kind == UnitKind.Hero && s.By.Hero == by.Hero) { if (dur > s.T) s.T = dur; s.By = by; return; }
            }
            u.AddRaw(type, dur, v, 0, by, false);
        }

        /// <summary>A short cast state that a stun or root can cut (Tyra's draw, Tikta's storm, Knut's throw).</summary>
        internal void Channel(Unit h, float dur)
        {
            h.RemoveAll(St.Channel);
            if (dur > 0) AddStatus(h, St.Channel, dur, dur);
        }

        /// <summary>A stun or root on a hero cancels what of its special is still under way; a stun also cancels an auto attack
        /// still winding up (Rickard 2026-10-29: "en stun ska avbryta ett slag som redan laddas", all heroes; BONK-01/CHAINMAP-03).
        /// A root does not: a rooted hero still attacks.</summary>
        internal void InterruptSpecial(Unit u, bool stun = false)
        {
            if (u.Kind != UnitKind.Hero || !u.Alive) return;
            bool any = false, cut = false;
            if (u.Needles > 0) { u.Needles = 0; any = true; }
            if (CancelTelegraph(u)) any = cut = true; // a tapped special still in its warning (Telegraph.cs): no cast, the warning goes
            for (int i = 0; i < Projs.Count; i++)
            {
                var p = Projs[i];
                if (p.Src != u || p.Done) continue;
                if (p.T < 0 && (p.Mode == ProjMode.Bolt || p.Mode == ProjMode.NeedleLine || p.Mode == ProjMode.Boulder || p.Mode == ProjMode.Slam || p.Mode == ProjMode.Volley || p.Mode == ProjMode.AnchorSlam || p.Mode == ProjMode.Worldbreaker || p.Mode == ProjMode.Quake)) { p.Done = true; any = cut = true; } // still in the hand
                else if (p.Mode == ProjMode.Hook && p.Out && p.Caught == null) { p.Out = false; p.Dur = p.T; p.Travel = p.D; any = true; } // the reel counts from here (HookUpdate)          // the anchor comes back
                else if (stun && p.T < 0 && (p.Mode == ProjMode.Auto || p.Mode == ProjMode.Smash)) { p.Done = true; any = true; } // the swing never lands
            }
            // the red warning goes with a shot that never left the hand; a shot already flying keeps its warning
            // (Tikta's one strip warns for all four needles: it stays while a needle of hers still flies, then goes, TIKTA-03)
            float flying = cut && u.Hero == HeroId.Tikta ? NeedleFlightLeft(u) : 0;
            if (cut) for (int i = 0; i < Projs.Count; i++) { var p = Projs[i]; if (p.Src == u && !p.Done && p.IsWarning) { if (flying > 0) p.Dur = Math.Min(p.Dur, p.T + flying); else p.Done = true; } }
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
            if (att != null && !tgt.IsClone) att.RevealT = Rules.RevealTime; // hitting Fenn's decoy reveals nobody
            // being hit in grass reveals nobody: only your own hits and an enemy within 2.5 tiles do, so a stray splash or a
            // bot's next shot cannot keep a hero who is not fighting lit for ever (tester MysOxen 2026-11-04). A hit outside the
            // grass still trails you 2 s into it.
            if (tgt.Kind != UnitKind.Hero || !Map.InGrass(tgt.X, tgt.Y)) tgt.RevealT = Rules.RevealTime;
            bool burning = (flags & DmgFlags.Burning) != 0;
            if (!burning)
            {
                amount *= tgt.TakenMult;
                if (tgt.TutUnkillable && amount >= tgt.Hp) amount = Math.Max(0, tgt.Hp - 1);
                if (tgt.Kind == UnitKind.Tower && Tut == null) amount *= TowerLateTaken() * TowerNeedsWave(att, tgt);
                if (tgt.Kind == UnitKind.Tower && src != null && src.IsMinionLike) amount *= PacingMinionTowerMult(); // the old wolf counted as a minion
                if (tgt.Kind == UnitKind.Tower && src != null && src.Kind == UnitKind.Wolf) amount *= Rules.WolfVsTower; // half (tester 2026-11-03 via Discord, Rickard ok)
                if (Tut != null) TutSawHit(src, tgt, kind); // the guided match's special lessons (Tutorial.cs)
                if (Tut != null && (tgt == Tut.Tower || tgt == Tut.Dummy))
                {
                    amount = TutDamage(src, tgt, amount, kind);
                    if (amount <= 0) return 0; // a hit the tutorial zeroes is no fight: it neither stops regen nor counts as combat
                }
            }
            if (tgt.IsClone) { amount = Math.Max(amount, tgt.Hp); ShadowEscape(src, tgt, kind); } // Fenn's decoy is gone at the first damage of any kind (the old illusion took double)
            tgt.LastCombat = Time;
            // any fight stops regen, minions too (Rickard 2026-11-03: "man måste vara utanför combat helt för att regga", as in ABC;
            // from protocol 18 to 46 only hero and tower fights did). A Fenn decoy's 0-damage hits stay no fight.
            if (att == null || !att.IsClone) tgt.LastFight = Time;
            if (src != null && !tgt.IsClone)
            {
                src.LastCombat = Time;
                src.LastFight = Time; // hitting anything stops the regen (Rickard 2026-11-03)
                if (src.Kind == UnitKind.Hero && att.HeroIndex >= 0) tgt.LastHitBy[att.HeroIndex] = Time;
            }
            if (src != null && src.Kind == UnitKind.Hero && tgt.Kind == UnitKind.Hero && !tgt.IsClone)
            {
                var t = Towers[tgt.Team]; // a hero hitting a hero under its tower draws the tower's fire
                if (t != null && t.Alive && SimMath.Dist(t, src) <= t.UDef.Range + src.R && !TowerLocked(t)) t.Target = src;
            }
            for (int i = 0; i < tgt.StCount && amount > 0; i++)
            {
                ref var s = ref tgt.Sts[i];
                if (s.Type != St.Shield || s.Amount <= 0) continue;
                float a = Math.Min(s.Amount, amount); s.Amount -= a; amount -= a;
            }
            if (amount <= 0) return 0;
            if (tgt.Kind == UnitKind.Hero && tgt.Hero == HeroId.Oaken) amount = StoredImpact(src, tgt, amount, kind, flags); // Hero6.cs
            amount = QueenInstinct(src, tgt, amount, kind, flags);
            if (amount <= 0) return 0;
            float taken = Math.Min(amount, tgt.Hp);
            tgt.Hp -= amount;
            if (taken > 0 && tgt.Kind == UnitKind.Hero && tgt.Hero == HeroId.Oaken) tgt.OakenLastLoss = Time;
            Emit(EventType.Damage, att, tgt, taken, 0, false, 0, 0, kind);
            if (src != null && src.Kind == UnitKind.Hero && !src.IsClone && src.Hero == HeroId.Blast && !burning && (flags & DmgFlags.NoIgnite) == 0) Ignite(src, tgt, amount);
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
            var etw = Towers[Teams.Other(u.Team)];
            if (etw != null && etw.Alive && SimMath.Dist(u, etw) < etw.UDef.Range + u.R + 0.5f) u.TowerDeaths++;
            if (u.SpecialReady) u.ReadyDeaths++;
            u.Deaths++; u.RespawnT = RespawnTime(u); u.DiedAt = Time;
            u.ClearStatuses(); u.Needles = 0; u.ClonePending = false; u.KnockT = 0; u.KnockPad = false; u.KnockKeg = null; u.KnockRoll = null; u.KnockFreeAt = -99;
            u.SpellChanEnd = -1; u.RevealT = 0; // a recall channel ends with the hero, and a revive starts unrevealed
            TiktaOnDeath(u); // the Deathmark she put on someone goes with her
            KuggReset(u); // his Anchor ends with him
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

        /// <summary>The wait before a dead hero comes back: 8 s, +1 s per earlier death, and up to 20 s more as the match clock runs
        /// (LateRamp), never more than Rules.RespawnMax (15 s).</summary>
        public float RespawnTime(Unit u) => Math.Min(Rules.RespawnMax, Rules.RespawnBase + Rules.RespawnPerDeath * (u.Deaths - 1) + Rules.RespawnLateMax * LateRamp());

        /// <summary>0 until 1:30 on the match clock, then straight up to 1 at 6:00 (no steps); always 0 in the tutorial and HERO TEST.</summary>
        internal float LateRamp() => Tut != null || Config.HeroTest ? 0
            : Math.Max(0, Math.Min(1, (Time - Rules.LateStart) / (Rules.LateFull - Rules.LateStart)));

        void Revive(Unit u)
        {
            u.Alive = true; u.Hp = u.MaxHp; u.X = u.PX = u.SpawnX; u.Y = u.PY = u.SpawnY;
            if (!Config.HeroTest) AddStatus(u, St.Invulnerable, Rules.RespawnInvulnerable); // HERO TEST: hittable at once (old herotestTick)
            u.AtkTimer = 0; u.RespawnT = 0; u.MoInit = false; u.SpellChanEnd = -1; u.TeleAt = -1;
            if (u.Bot != null) u.Bot.Reset(true);
            Emit(EventType.Respawn, u, null);
        }

        // ---------- towers ----------
        /// <summary>Has enemy hero e attacked one of tower tw's heroes in the last 2 s? Only attacking counts: being hit by a
        /// defender is no reason for the tower to turn (TOWER RULE, Rickard 2026-11-05; tester Øli 2026-11-06). The tower itself
        /// switches at the hit (DealDamage, attacker in its reach); the bots read this to weigh a tower's fire.</summary>
        internal bool TowerAggro(Unit tw, Unit e)
        {
            if (e.Kind != UnitKind.Hero || e.Team == tw.Team || e.HeroIndex < 0) return false;
            for (int i = 0; i < Units.Count; i++)
            {
                var a = Units[i];
                if (a.Kind == UnitKind.Hero && a.Team == tw.Team && a.Alive && Time - a.LastHitBy[e.HeroIndex] < Rules.TowerAggroTime) return true;
            }
            return false;
        }
        /// <summary>Tower aggro holds (Rickard 2026-11-02 "man ska inte kunna tappa aggro från tornet om man har fått det förrän man
        /// går ut ur range"): once a tower aims at an enemy hero it stays on that hero while the hero lives and stands in its reach,
        /// even in grass, behind a wall, or when another hero starts a fight under it. Only leaving the reach or dying ends it.</summary>
        internal bool TowerLocked(Unit tw)
        {
            var k = tw.Target;
            return k != null && k.Kind == UnitKind.Hero && k.Team != tw.Team && k.Alive && SimMath.Dist(tw, k) <= tw.Range + k.R;
        }
        /// <summary>Heroes take more from tower shots as the match clock runs: x1 until 1:30, straight up to x2 at 6:00 (LateRamp).</summary>
        internal float TowerLateMult(Unit h) => 1 + (Rules.TowerLateMax - 1) * LateRamp();
        /// <summary>From level 10 (2:24) towers take more damage, +50 % a minute, at most x2 (Rules.TowerTakenMax, reached at 4:24).</summary>
        internal float TowerLateTaken() => Math.Min(Rules.TowerTakenMax, 1 + Rules.TowerTakenPerMinute * Math.Max(0, Time - (Rules.TowerTakenLevel - 1) * Rules.SecondsPerLevel) / 60);
        /// <summary>Backdoor protection (Rickard 2026-10-28, closed-test feedback): a hero hitting a tower without one of its own
        /// minions in the tower's range deals half damage, the whole match (it was half of the late bonus only).</summary>
        float TowerNeedsWave(Unit att, Unit tw)
        {
            if (att == null || att.Kind != UnitKind.Hero) return 1;
            for (int i = 0; i < Units.Count; i++)
            {
                var m = Units[i];
                if (m.IsMinionLike && m.Alive && m.Team == att.Team && SimMath.Dist(m, tw) <= tw.UDef.Range + m.R) return 1;
            }
            return 0.5f;
        }
        float TowerCoeff(Unit tw, Unit tgt) => tgt.Kind != UnitKind.Hero || Tut != null ? 1
            : Rules.TowerVsHero * TowerLateMult(tgt) * (tgt == tw.TowerFocus ? 1 + Math.Min(Rules.TowerRampMax, Rules.TowerRamp * tw.TowerStreak) : 1);

        // ---------- auto attacks ----------
        void UpdateAttack(Unit u, float dt)
        {
            if (!u.Alive || (u.Kind == UnitKind.Tower && State != MatchState.Play)) return;
            if (u.Has(St.Stun) || u.Has(St.Disarm)) return;
            float cd = u.AttackCd;
            u.AtkTimer += dt;
            if (u.AtkTimer < cd) return;
            var targets = EnemiesInRange(u, u.Range, Pick.Any, true, atkBuf);
            Unit tgt;
            if (u.Kind == UnitKind.Tower)
            {
                var k = u.Target;
                bool locked = TowerLocked(u);
                if (k != null && !locked && (!k.Alive || SimMath.Dist(u, k) > u.Range + k.R || !IsVisibleTo(k, u.Team) || !Map.LosClear(u, k))) u.Target = null;
                // TOWER RULE (Rickard 2026-11-05): the tower keeps its target until it dies or leaves the reach; the one switch is to
                // an enemy hero who attacks one of its heroes from inside the reach, made at the hit (DealDamage). Until protocol 58 a
                // 2 s fight memory also turned it here, to a hero who had only been hit by a defender or had hit one from outside the
                // reach, so a hero stepping back in took the tower off his living minion (tester Øli 2026-11-06).
                if (u.Target == null)
                {
                    Unit m = null; float bd = float.MaxValue;
                    for (int i = 0; i < targets.Count; i++) { var t = targets[i]; if (!t.IsMinionLike) continue; float d = SimMath.Dist(u, t); if (d < bd) { bd = d; m = t; } }
                    u.Target = m ?? Nearest(targets, u.X, u.Y);
                }
                tgt = u.Target;
                // a locked hero behind a wall or untargetable for a moment keeps the aggro: the tower holds its shot instead of
                // turning to someone else (heroes in its reach are always seen, grass or not; Rickard 2026-11-02)
                if (locked && tgt != null && (!Map.LosClear(u, tgt) || tgt.Has(St.Untargetable))) tgt = null;
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
            // a hero's ready swing waits Rules.AutoHold s for a target before it runs out (ABC feel, tester 2026-11-02: an enemy
            // just slipping out or stepping in at the ring's edge still gets the shot); others hold it ready. The hold counts from
            // when the charge ring is full (MatchRings: full on the blow, a wind-up after the rules are ready), not from when the
            // rules are ready: Bonk's 0.5 s wind-up made it run out 0.2 s BEFORE his ring was full (tester 2026-11-04 via Discord:
            // "it seems to target about 0.5 sec before the circle is filled up")
            else if (u.Kind == UnitKind.Hero) { if (u.AtkTimer >= cd + u.HDef.SpawnDelay + Rules.AutoHold) u.AtkTimer = 0; }
            else u.AtkTimer = cd;
        }

        void AutoAttack(Unit u, Unit tgt)
        {
            bool hero = u.Kind == UnitKind.Hero;
            float spawnDelay, projSpeed, projTime, dmg, coeff = 1; ProjKind kind; bool big = false;
            if (hero)
            {
                var d = u.HDef; spawnDelay = d.SpawnDelay; projSpeed = d.ProjSpeed; projTime = d.ProjTime; kind = d.Attack;
                if (u.Hero == HeroId.Kugg && u.KuggAnchored) { kind = ProjKind.KuggShell; projSpeed = KuggS.ShellSpeed; projTime = 0; coeff *= KuggS.ShotCoeff; big = true; } // Anchor (Hero7.cs)
                dmg = u.StatDmg();
                coeff *= HeroAttackCoeff(u, tgt, ref big);
                if (AmbushReady(u)) { coeff *= 1 + Rules.AmbushBonus; big = true; Emit(EventType.Ambush, u, tgt); }
            }
            else
            {
                var d = u.UDef; spawnDelay = d.SpawnDelay; projSpeed = d.ProjSpeed; projTime = d.ProjTime; kind = d.Attack; dmg = u.DefDmg;
                coeff = UnitAutoCoeff(u, tgt);
            }
            u.Facing = SimMath.Atan2(tgt.Y - u.Y, tgt.X - u.X); u.AimT = Time + spawnDelay + 0.1f;
            float travel = projTime != 0 ? projTime : 0.1f;
            if (hero && u.Hero == HeroId.Tyra) travel = SimMath.Clamp(SimMath.Dist(u, tgt) / 15f, 0.08f, projTime); // about 15 tiles/s, between 0.08 and 0.25 s of flight (measured 10-16 tiles/s with the tick rounding, precision TYRA-11)
            if (hero && u.Hero == HeroId.Bonk) { BonkSwing(u, tgt, dmg * coeff, big); return; } // a slow slam on the ground in front (Hero3.cs)
            var p = NewProj(ProjMode.Auto, u, u.X, u.Y);
            p.Kind = kind; p.Tgt = tgt; p.Dmg = dmg * coeff; p.T = -spawnDelay; p.Speed = projSpeed; p.Travel = travel; p.Big = big; p.OnHit = hero;
            Emit(EventType.AttackStart, u, tgt, spawnDelay, (byte)kind, big);
            if (hero && u.Hero == HeroId.Kugg) KuggFired(u); // an anchored shot spends one (Hero7.cs)
        }

        /// <summary>What a tower's or minion's shot is worth against tgt (tower ramp and late bonus, a siege minion's x2 on towers).</summary>
        float UnitAutoCoeff(Unit u, Unit tgt)
        {
            float coeff = 1;
            if (u.Kind == UnitKind.Tower) coeff = TowerCoeff(u, tgt);
            if (u.Kind == UnitKind.Minion && u.Minion == MinionKind.Siege && tgt.Kind == UnitKind.Tower) coeff *= Rules.SiegeVsTower;
            return coeff;
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
                emitShot = p.IsShot ? p.Serial : 0;
                if (p.Mode == ProjMode.Auto && p.Tgt != null && !p.Tgt.Alive && !p.Fizzle) { RetargetAuto(p); if (p.Done) continue; }
                p.T += dt;
                if (p.T < 0)
                {
                    if (p.IsShot && p.Src != null)
                    {
                        if (!p.Src.Alive) { p.Done = true; continue; } // a shot still in the hand dies with its shooter
                        p.X = p.Src.X; p.Y = p.Src.Y; // still in the shooter's hand
                    }
                    continue;
                }
                switch (p.Mode)
                {
                    case ProjMode.Auto: UpdateAuto(p, dt); break;
                    case ProjMode.Hammer: HammerUpdate(p, dt); break;
                    case ProjMode.Bolt: BoltUpdate(p, dt); break;
                    case ProjMode.Hook: HookUpdate(p, dt); break;
                    case ProjMode.Needle: NeedleUpdate(p, dt); break;
                    case ProjMode.NeedleLine: NeedleLineUpdate(p, dt); break;
                    case ProjMode.Bomb: p.Done = true; BombBlast(p); break;
                    case ProjMode.FireLane: FireLaneUpdate(p); break;
                    case ProjMode.HoneyField: HoneyFieldUpdate(p); break;
                    case ProjMode.WarnLine: case ProjMode.WarnCircle: WarnUpdate(p); break;
                    case ProjMode.ChainBlast: p.Done = true; ChainBlast(p); break;
                    case ProjMode.Slam: p.Done = true; BonkSlam(p); break;
                    case ProjMode.Boulder: BoulderUpdate(p, dt); break;
                    case ProjMode.Smash: p.Done = true; BonkSmash(p); break;
                    case ProjMode.Keg: p.Done = true; KegBlast(p); break;
                    case ProjMode.Drop: p.Done = true; AnvilDrop(p); break;
                    case ProjMode.AnchorSlam: p.Done = true; AnchorSlam(p); break;
                    case ProjMode.Worldbreaker: p.Done = true; OakenSlam(p); break;
                    case ProjMode.Quake: p.Done = true; OakenQuake(p); break;
                    case ProjMode.Volley: VolleyUpdate(p, dt); break;
                    case ProjMode.Echo: EchoUpdate(p, dt); break;
                    case ProjMode.FeatherTrail: FeatherTrailUpdate(p); break;
                }
            }
            emitShot = 0;
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
            float ox = p.X, oy = p.Y;
            float tx = p.Fizzle ? p.FX : tgt.X, ty = p.Fizzle ? p.FY : tgt.Y; // a fizzling shot flies to its spot and hits nothing there
            if (p.Speed > 0)
            {
                float d = SimMath.Hypot(tx - p.X, ty - p.Y), step = p.Speed * Rules.Tick;
                if (d <= step || d < 0.05f)
                {
                    p.Done = true;
                    if (Map.ShotBlocked(ox, oy, tx, ty)) Blocked(p, ox, oy);
                    else if (p.Fizzle) { p.X = tx; p.Y = ty; }
                    else HitWith(p);
                }
                else
                {
                    p.X += (tx - p.X) / d * step; p.Y += (ty - p.Y) / d * step;
                    if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); }
                }
            }
            else
            {
                float k = SimMath.Clamp(p.T / p.Travel, 0, 1);
                p.X = SimMath.Lerp(p.OX, tx, k); p.Y = SimMath.Lerp(p.OY, ty, k);
                if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); }
                else if (p.T >= p.Travel) { p.Done = true; if (!p.Fizzle) HitWith(p); }
            }
            if (!tgt.Alive && !p.Done && !p.Fizzle) p.Done = true;
        }

        /// <summary>Rickard 2026-10-29 ("skottet ska inte hoppa, man kan blinka sig ur skottläget"): a hero that blinks or recalls
        /// leaves the homing shots on their way to it, autos and homing specials alike, in the hand or flying: they fly on to the
        /// spot it left and end there with no hit, instead of jumping after it.</summary>
        internal void LoseShots(Unit h)
        {
            for (int i = 0; i < Projs.Count; i++)
            {
                var p = Projs[i];
                if (p.Done || p.Fizzle || p.Tgt != h || (p.Mode != ProjMode.Auto && p.Mode != ProjMode.Needle && p.Mode != ProjMode.Hammer)) continue;
                p.Fizzle = true;
                if (p.Mode == ProjMode.Auto) { p.FX = h.X; p.FY = h.Y; }
                else { p.OX = h.X; p.OY = h.Y; } // a needle or hammer keeps its spot there and puffs out on it (Heroes.FlyToSpotAndPuff)
            }
        }

        /// <summary>A shot stopped by a wall still shows something where it stopped.</summary>
        void Blocked(Proj p, float x, float y) => Emit(EventType.Blocked, p.Src, p.Tgt, 0, (byte)p.Kind, false, x, y);

        /// <summary>An auto attack whose target died before it landed goes to another enemy in range (a hero picks at random,
        /// others the nearest); with none the swing is dropped.</summary>
        const float RetargetTime = 1f / 20f; // s per tile: a retargeted timed shot flies at most 20 tiles a second

        void RetargetAuto(Proj p)
        {
            var u = p.Src;
            if (u == null || !u.Alive) { if (p.Launched && p.T >= 0) { p.Fizzle = true; p.FX = p.Tgt.X; p.FY = p.Tgt.Y; } return; } // nobody to pick a new target: one in flight lands on the spot, as below
            var list = EnemiesInRange(u, u.Range, Pick.Any, true, atkBuf);
            if (list.Count > 0)
            {
                p.Tgt = u.Kind == UnitKind.Hero ? PickRandom(list) : Nearest(list, u.X, u.Y);
                if (u.Kind != UnitKind.Hero) p.Dmg = u.DefDmg * UnitAutoCoeff(u, p.Tgt); // priced for the new target, not the dead one
                u.Facing = SimMath.Atan2(p.Tgt.Y - u.Y, p.Tgt.X - u.X);
                // a timed shot already flying goes on from where it is, at least RetargetTime per tile, instead of jumping onto the
                // line from its start to the new target (up to 4.4 tiles in one tick, precision CHAINMAP-05)
                if (p.Launched && p.Speed <= 0 && p.T > 0)
                {
                    float d = SimMath.Hypot(p.Tgt.X - p.X, p.Tgt.Y - p.Y);
                    p.OX = p.X; p.OY = p.Y; p.Travel = Math.Max(p.Travel - p.T, d * RetargetTime); p.T = 0;
                }
                return;
            }
            // nobody to go to: a shot still in the hand ends; one already flying lands on the fallen target's spot with no hit, so it
            // is seen to land instead of vanishing in the air (precision CHAINMAP-09)
            if (p.Launched && p.T >= 0) { p.Fizzle = true; p.FX = p.Tgt.X; p.FY = p.Tgt.Y; return; }
            p.Done = true;
        }

        void HitWith(Proj p)
        {
            var s = p.Src; var t = p.Tgt;
            if (!t.Alive) return;
            float dealt = DealDamage(s, t, p.Dmg, DmgKind.Auto);
            Emit(EventType.Hit, s, t, dealt, (byte)p.Kind, p.Big, t.X, t.Y, DmgKind.Auto); // always: every attack shows where it landed
            if (!p.OnHit || (!s.Alive && !s.IsClone)) return;
            if (s.Kind == UnitKind.Hero && !s.IsClone) OnHeroHit(s, t, p, dealt);
        }

        void OnHeroHit(Unit h, Unit t, Proj p, float dealt)
        {
            if (t.IsClone) return; // Fenn's decoy gives no charge and no on-hit passives (old phantomHit)
            if (dealt <= 0 && t.Has(St.Invulnerable)) return; // nor does a hit on respawn protection (precision CHAINMAP-06; a shield still charges)
            if (h.Hero == HeroId.Tikta) TiktaTryMark(h, t, dealt);
            if (!h.KuggRooted) h.Charge = Math.Min(h.HDef.MaxCharge, h.Charge + 1); // Kugg gathers no charge while anchored (ROI17's stocks)
            if (h.HDef.OnHitSlow != 0 && (t.Kind == UnitKind.Hero || t.IsMinionLike)) AddSlowOnce(t, St.SlowFlat, h.HDef.OnHitSlowTime, h.HDef.OnHitSlow, h);
            if (h.Hero == HeroId.Bram) BramBacklash(h, t);
            if (h.Hero == HeroId.Tyra) Featherstep(h, t, dealt); // Hero5.cs
            if (h.Hero == HeroId.Oaken) OakenRelease(h, t); // Hero6.cs
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
            float k = tier.AimLead * Rng.Range(0.8f, 1.15f) * (tier.V3 ? 0.7f : 1) * BotBrain.LeadSeen(this, h, t); // no lead on a walk it has not watched
            float n = tier.AimNoise * (1 - 0.25f * h.Bot.Skill) * (tier.V3 ? 1.2f : 1);
            if (h.Bot.Fumble()) { k = 0; n *= 1.5f; } // a human miss: aimed where the target is (BotHuman.cs)
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
            BotBrain.TrackSight(this, u);
        }
    }
}
