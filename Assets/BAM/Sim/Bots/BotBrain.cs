using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    /// <summary>
    /// A bot playing one hero: the old game's bot brain (ABC2 SimAI.cs aiHero with SimTeamplay, SimBotDrag, SimSpecials2,
    /// SimHeroes6 newHeroSpecialWanted), ported line by line with every old switch at its default. Bronze plays the V2 brain,
    /// the other tiers V2 + V3. Items, buff spots, the Arena Core, signals, the RL brain and the heroes not in BAM are left out.
    /// Every random number comes from Match.Rng; Think allocates nothing.
    /// </summary>
    public sealed partial class BotBrain
    {
        public readonly Unit H;
        public readonly BotTier Tier;
        /// <summary>This bot's own skill, -1..1 (people in one rank differ too); set at its first decision.</summary>
        public float Skill;
        /// <summary>What it is doing (for tests and debugging).</summary>
        public BotMode Mode;

        readonly Match M;
        readonly int tierI;
        readonly bool v3;
        readonly float[] tk;
        readonly float[] react;

        // ---------- the old AI object ----------
        float t, afk = float.NaN, bold = float.NaN, spT = float.NaN, decideIv, lastThinkT = -99;
        bool retreat, danger, panicked, misjudge, overchase, urgent, chip, following;
        float retreatT = -99, ttk = 99, mistakeT = -1, lateRetreat, commitT, commitHp, strafeT, strafe;
        float mdx, mdy, mT = -99;
        float emoteT, spWantT, spHoldT, spHoldL; int seenKills;
        Unit focus, kite;
        Unit leadE; float leadT = -1e9f, leadAt = -1e9f;
        Unit susU; float susT = float.NaN, susJ;
        readonly float[] soakRT = new float[8], soakRJ = new float[8];
        bool hasSeen; Unit seenU; float seenX, seenY, seenT;
        bool hasBlocked; float blockedX, blockedY, blockedT;
        bool hasStuck; float stuckX, stuckY, stuckT;
        Unit chaseU, dropU; float chaseX, chaseY, chaseGap, chaseT, chaseLastT = -99, dropT;
        // the goal (old Goal): a spot, a distance to stop at from the followed unit, and where that unit was when the spot was set
        bool hasGoal; float gX, gY, gStop, gFx, gFy; bool gHasF; Unit gFollow;
        readonly PathBuf path = new PathBuf();

        // reused lists (no garbage per tick)
        readonly List<Unit> bufMates = new List<Unit>(8), bufAllies = new List<Unit>(8), bufPushers = new List<Unit>(8), bufFoes = new List<Unit>(32), bufHeroes = new List<Unit>(8);
        // grass tile centres
        readonly float[] bushX, bushY; readonly int bushN;

        public BotBrain(Unit h, BotTier tier, Match m)
        {
            H = h; Tier = tier; M = m;
            tierI = BotTables.Index(tier); v3 = tier.V3; tk = BotTables.Knobs(v3); react = tier.React;
            for (int i = 0; i < soakRT.Length; i++) soakRT[i] = float.NaN;
            int n = 0;
            for (int y = 0; y < m.Map.H; y++) for (int x = 0; x < m.Map.W; x++) if (m.Map.Tile(x, y) == Arena.Grass) n++;
            bushX = new float[n]; bushY = new float[n];
            for (int y = 0; y < m.Map.H; y++) for (int x = 0; x < m.Map.W; x++) if (m.Map.Tile(x, y) == Arena.Grass) { bushX[bushN] = x + 0.5f; bushY[bushN++] = y + 0.5f; }
        }

        float TK(int i) => tk[i];
        static float TK3(int i) => BotTables.Tk3Def[i];
        float Srnd() => M.Rng.Float();
        float Srand(float a, float b) => M.Rng.Range(a, b);
        float PickReact() => react[M.Rng.Int(react.Length)];
        bool Safety => Tier.Safety > 0;
        int Fwd => H.Team == Teams.Enemy ? 1 : -1; // the enemy team sits at the top and walks towards +y
        int StarDiff => M.NoStars ? 0 : M.Stars[H.Team] - M.Stars[Teams.Other(H.Team)];
        bool ProtectLead => !M.NoStars && M.Stars[H.Team] >= Defs.MaxStars;
        /// <summary>When `by` (a real hero) last hurt victim; never for a clone or anything else.</summary>
        static float HitAt(Unit victim, Unit by) => by != null && by.HeroIndex >= 0 ? victim.LastHitBy[by.HeroIndex] : -1e9f;

        float Temperament()
        {
            if (float.IsNaN(bold)) { bold = Srand(-1, 1); if (TK(17) > 0) Skill = Srand(-1, 1) * TK(17); }
            return SimMath.Clamp(bold + SimMath.Clamp((H.Kills - H.Deaths) * 0.1f, -0.5f, 0.5f), -1, 1);
        }

        /// <summary>After a respawn or a recall the bot starts over at its base: no old path, goal, kite or stuck spot.</summary>
        public void Reset(bool died)
        {
            path.Clear(); hasGoal = false; gFollow = null; gHasF = false; kite = null;
            hasStuck = false; hasBlocked = false; t = 0;
            if (died) { retreat = false; danger = false; focus = null; }
        }

        void SetGoal(float x, float y, float stop = 0, Unit follow = null) { hasGoal = true; gX = x; gY = y; gStop = stop; gFollow = follow; gHasF = false; }

        // ---------- the think tick (old aiHero) ----------
        public void Think(float dt)
        {
            var h = H; t -= dt;
            bool resumed = Now - lastThinkT > 2.5f * dt; lastThinkT = Now; // the last ticks were skipped (stun, cast, recall, death)
            if (float.IsNaN(afk)) afk = BotTables.AfkMax * (float)Math.Pow(Srnd(), BotTables.AfkExp);
            if (Now < afk) return;
            var myTower = TowerOf(h.Team); var enemyTower = TowerOf(Teams.Other(h.Team));
            int fwd = Fwd;
            bool safety = Safety;
            if (t <= 0) Decide(myTower, enemyTower, fwd, safety);
            if (!float.IsNaN(spT))
            {
                spT -= dt;
                if (spT <= 0)
                {
                    spT = float.NaN;
                    bool dz = danger || retreat && ttk < 3;
                    if (h.SpecialReady && (SpecialWanted(dz) || DragPlan(dz, out _, out _)) && !TeamSpecialHold(dz)) BotCast(dz);
                }
            }
            if (h.Has(St.Casting) || !h.Alive) return;
            if (DodgeVector(out float dvx, out float dvy)) { BotMove(dvx, dvy, dt, 0); path.Clear(); t = Math.Min(t, 0.15f); return; }
            if (emoteT > Now && !retreat && !danger) { float side = (int)(Now * 6) % 2 == 0 ? 1 : -1; M.MoveUnit(h, side, 0, dt); return; }
            if (kite != null && kite.Alive && Dist(h, kite) < kite.Range + kite.R + 0.9f)
            {
                var k = kite; float ax = h.X - k.X, ay = h.Y - k.Y - fwd * 0.5f;
                if (M.Map.Free(h.X + ax * 0.2f, h.Y + ay * 0.2f, h.R)) { BotMove(ax, ay, dt, 18); return; }
            }
            if (hasGoal && gStop > 0)
            {
                var f = gFollow != null && gFollow.Alive ? gFollow : null;
                if (f != null && Dist(h, f) <= gStop)
                {
                    if (f.Kind == UnitKind.Hero && safety && h.Hero != HeroId.Tyra) // Tyra stands still to keep her passive (dodging comes first)
                    {
                        if (!(strafeT > 0)) { strafeT = Srand(0.6f, 1.4f); strafe = strafe == 1 ? -1 : 1; }
                        strafeT -= dt;
                        float d = Dist(h, f); if (d == 0) d = 1;
                        float ux = (h.X - f.X) / d, uy = (h.Y - f.Y) / d, pull = (gStop - 0.35f - d) * 1.5f;
                        float vx = -uy * strafe + ux * pull, vy = ux * strafe + uy * pull;
                        if (Walkable(h, h.X + vx * 0.3f, h.Y + vy * 0.3f) && !NearEnemyTowerAt(h.X + vx * 0.5f, h.Y + vy * 0.5f, h.Team, 0)) { BotMove(vx, vy, dt, 18); h.Facing = SimMath.Atan2(f.Y - h.Y, f.X - h.X); }
                        else strafe = -strafe;
                    }
                    return;
                }
                if (f != null && f.Kind == UnitKind.Hero && (!path.Has || !gHasF || Hyp(gFx - f.X, gFy - f.Y) > 1.0f))
                {
                    float d = Dist(h, f); if (d == 0) d = 1; float keep = gStop - 0.1f;
                    gX = f.X + (h.X - f.X) / d * keep; gY = f.Y + (h.Y - f.Y) / d * keep; gFx = f.X; gFy = f.Y; gHasF = true;
                    M.Map.FindPath(h.X, h.Y, gX, gY, Now, path);
                }
            }
            // a bot that could not think (stunned, casting, recalling, dead) or is rooted could not move: that is not being stuck
            // (Rickard 2026-10-27: bots no longer stand still after Fenn's root)
            if (!hasStuck || resumed || h.Has(St.Root)) { hasStuck = true; stuckX = h.X; stuckY = h.Y; stuckT = Now; }
            if (!path.Has || Hyp(h.X - stuckX, h.Y - stuckY) > Math.Max(0.1f, SpeedOf(h) * 0.2f)) { stuckX = h.X; stuckY = h.Y; stuckT = Now; }
            else if (Now - stuckT > 0.6f)
            {
                hasBlocked = true; blockedT = Now + 1.5f;
                if (hasGoal) { blockedX = gX; blockedY = gY; } else { blockedX = path.X[path.Count - 1]; blockedY = path.Y[path.Count - 1]; }
                path.Clear(); hasGoal = false; t = Math.Min(t, 0.2f); stuckT = Now; return;
            }
            if (path.Has)
            {
                for (int k = 0; k < 4 && path.Count - path.Index > 1 && ClearWalk(h, path.X[path.Index + 1], path.Y[path.Index + 1]); k++) path.Index++;
                float nx = path.X[path.Index], ny = path.Y[path.Index], dx = nx - h.X, dy = ny - h.Y, d = Hyp(dx, dy);
                bool last = path.Count - path.Index == 1;
                if (d < (last ? 0.05f : 0.25f)) path.Index++;
                else { float step = SpeedOf(h) * dt, k = last && d < step ? d / step : 1; BotMove(dx / d * k, dy / d * k, dt, last && d < 0.4f ? 0 : 18); }
            }
        }

        /// <summary>A thumb on a joystick cannot turn at once: the walking direction turns at most `turn` rad/s (0 = no limit).</summary>
        void BotMove(float dx, float dy, float dt, float turn)
        {
            var h = H; float m = Hyp(dx, dy);
            turn *= TK(7);
            if (m == 0 || turn <= 0 || Now - mT > 0.25f) { M.MoveUnit(h, dx, dy, dt); if (m > 0) { mdx = dx / m; mdy = dy / m; mT = Now; } return; }
            float ux = dx / m, uy = dy / m, a = SimMath.Atan2(mdx * uy - mdy * ux, mdx * ux + mdy * uy), lim = turn * dt;
            if (a > lim || a < -lim) { float r = a > 0 ? lim : -lim, c = SimMath.Cos(r), s = SimMath.Sin(r); ux = mdx * c - mdy * s; uy = mdx * s + mdy * c; }
            mdx = ux; mdy = uy; mT = Now; float mag = Math.Min(1, m);
            M.MoveUnit(h, ux * mag, uy * mag, dt);
        }

        // ---------- a decision (the old aiHero's "ai.t <= 0" block) ----------
        void Decide(Unit myTower, Unit enemyTower, int fwd, bool safety)
        {
            var h = H; float now = Now;
            bool calm = !h.InCombat(now) && !AnyFoeHeroSeenWithin(h, 8);
            t = Math.Max(0.1f, (PickReact() + Srand(-BotTables.ReactionOffset, BotTables.ReactionOffset)) * (calm ? 1.4f : h.InCombat(now) ? 0.75f : 1) * (1 - 0.15f * Skill)) * (v3 ? TK3(0) : 1) * HumanReaction();
            decideIv = t;
            Temperament();
            if (!(mistakeT > now))
            {
                float mc = BotTables.Mistake[tierI]; mistakeT = now + Srand(6, 14); bool plat = tierI == 3;
                misjudge = plat && Srnd() < mc;
                overchase = !plat && safety && Srnd() < mc * (1 + bold * 0.5f);
                lateRetreat = !plat && Srnd() < mc * (1 + bold * 0.5f) ? 0.06f : 0;
            }
            float hpR = HpR(h), myDps = DpsOf(h);
            if (hpR > 0.4f) panicked = false;
            else if (hpR < 0.15f && !panicked && h.SpecialReady && h.InCombat(now)) { panicked = true; if (Srnd() < BotTables.PanicSpecial[tierI]) BotCast(true); }
            ttk = TtkAt(h);
            if (h.Kills != seenKills)
            {
                seenKills = h.Kills;
                if (!AnyFoeHeroSeenWithin(h, 7) && hpR > 0.3f && Srnd() < TK(18)) emoteT = now + Srand(0.6f, 1.2f); // a little dance after a kill
            }
            bool allyMinionsNear = false;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.IsMinionLike && u.Team == h.Team && Dist(u, h) < 4) { allyMinionsNear = true; break; } }
            bool towerOnMe = enemyTower != null && enemyTower.Alive && enemyTower.Target == h && Dist(h, enemyTower) < enemyTower.UDef.Range + h.R + 0.5f;
            float tank = TowerTank(enemyTower, h.Team);
            var matesNear = MatesWithin(h, 8, bufMates);
            int foesNear = CountSeenNear(Teams.Other(h.Team), h.Team, null, h.X, h.Y, 8);
            bool fightNear = false;
            for (int i = 0; i < matesNear.Count; i++) { var a = matesNear[i]; if (now - a.LastCombat < 1 && Dist(a, h) < 6) { fightNear = true; break; } }
            var fo = focus != null && focus.Alive && focus.Kind == UnitKind.Hero && Sees(focus) ? focus : null;
            bool winning = fo != null && (fo.Hp + ShieldOf(fo)) / Math.Max(1, TeamDpsOn(h, myDps, matesNear, fo)) < ttk * TK(15);
            float line = Tier.LowHealth - lateRetreat - Temperament() * 0.04f;
            if (Tier.LowHealth > 0 && safety)
                line = SimMath.Clamp(line - (fightNear ? 0.12f : 0) - (winning ? 0.15f : 0) - (h.SpecialReady ? 0.05f : 0) + (foesNear > matesNear.Count + 1 ? 0.1f : 0)
                    + (ttk < 2.5f ? 0.1f : 0) + (h.Has(St.SlowPct) && h.InCombat(now) ? 0.12f : 0), 0.12f, 0.6f);
            line += TK(0);
            bool wasRetreat = retreat;
            if (Tier.LowHealth > 0 && hpR < line
                || (!allyMinionsNear && hpR < Tier.NoMinionLowHealth && fo != null && !winning && foesNear >= matesNear.Count + 1)
                || (towerOnMe && hpR < Tier.TargetedByTowerLowHealth && !(chip && hpR > 0.3f))) retreat = true;
            if (towerOnMe && (h.Hp + ShieldOf(h)) / TowerDpsOn(enemyTower, h) < BotTables.TowerBotLeaveS) retreat = true; // the tower's ramp would kill us soon
            if (retreat && !wasRetreat) retreatT = now;
            bool locked = retreat && now - retreatT < 2;
            if (retreat && !locked && hpR >= Math.Min(Tier.HighHealth + TK(4), Math.Max(line + 0.25f, Tier.LowHealth + 0.05f) + TK(4))) retreat = false;
            if (retreat && safety && ttk > 2)
                for (int i = 0; i < us.Count; i++)
                {
                    var u = us[i];
                    if (u.Kind != UnitKind.Hero || !u.Alive || u.Team == h.Team || u.IsClone || !Sees(u) || Dist(u, h) > h.Range + u.R + 0.5f) continue;
                    float killT = (u.Hp + ShieldOf(u)) / Math.Max(1, TeamDpsOn(h, myDps, matesNear, u));
                    int helpers = CountSeenNear(u.Team, h.Team, u, u.X, u.Y, 7);
                    if (killT < 1.2f && ttk > killT * 2 && (hpR > 0.35f ? helpers <= 1 : helpers == 0 && Unaccounted(h, u) == 0)) { retreat = false; break; }
                }
            if (retreat && !locked && safety && hpR > 0.6f && !h.InCombat(now))
                for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a.Kind == UnitKind.Hero && a.Alive && a != h && a.Team == h.Team && !a.IsClone && now - a.LastCombat < 1 && Dist(a, h) < 8) { retreat = false; break; } }
            bool finish = TowerFinish(enemyTower, hpR, myDps); if (finish) retreat = false;
            TeamStay(hpR); // don't leave the human alone in a fight
            danger = !retreat && !finish && !(v3 && TK3(33) > 0 && InvulT(h) > 1.5f) && ttk < Tier.TtkDanger * TK(5) && !(commitT > now && ttk > 1.2f);
            var target = retreat ? null : PickHeroTarget(enemyTower, myDps);
            bool towerUp = enemyTower != null && enemyTower.Alive;
            var pushers = bufPushers; pushers.Clear();
            if (towerUp) for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a.Kind == UnitKind.Hero && a.Alive && a.Team == h.Team && !a.IsClone && (a == h || Dist(a, enemyTower) < enemyTower.UDef.Range + 2.5f) && HpR(a) > 0.4f) pushers.Add(a); }
            float guardDps = 0;
            if (towerUp)
            {
                guardDps = TowerDpsOn(enemyTower, h);
                for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.IsMinionLike && u.Team != h.Team && Dist(u, enemyTower) < enemyTower.UDef.Range + 3) guardDps += DpsOf(u) * 0.5f; }
            }
            float pushDps = 0, pushMinHp = float.PositiveInfinity;
            for (int i = 0; i < pushers.Count; i++) { var o = pushers[i]; pushDps += DpsOf(o); pushMinHp = Math.Min(pushMinHp, o.Hp + ShieldOf(o)); }
            float towerTtk = towerUp ? enemyTower.Hp / pushDps : 99;
            bool soloTower = towerUp && pushMinHp / guardDps > towerTtk * 1.15f;
            bool towerOnMinion = towerUp && enemyTower.Target != null && enemyTower.Target.IsMinionLike && enemyTower.Target.Alive && enemyTower.Target.Hp > 25;
            int defenders = 0, foesAlive = 0; bool lurking = false, guards = false, foeDead = false;
            if (towerUp)
                for (int i = 0; i < us.Count; i++)
                {
                    var u = us[i];
                    if (u.Team == h.Team) continue;
                    if (u.Kind == UnitKind.Hero)
                    {
                        if (!u.Alive) { foeDead = true; continue; }
                        foesAlive++;
                        float tr = enemyTower.UDef.Range + 3;
                        if (Sees(u) ? Dist(u, enemyTower) < tr : RemNear(u, h.Team, enemyTower.X, enemyTower.Y, tr)) defenders++;
                        if (!lurking && !u.IsClone && (Sees(u) ? Dist(u, enemyTower) < 12 || Dist(u, h) < 8 : RemOrUnknownNear(u, h.Team, enemyTower.X, enemyTower.Y, 14))) lurking = true;
                    }
                    else if (u.IsMinionLike && u.Alive && Dist(u, enemyTower) < enemyTower.UDef.Range + 6) guards = true;
                }
            chip = towerUp && safety && tank == 0 && !(towerOnMe && enemyTower.TowerStreak >= 2) && defenders == 0 && !lurking && !guards && enemyTower.Hp > enemyTower.MaxHp * 0.3f && hpR > (chip ? 0.3f : 0.6f) && !retreat;
            bool shouldTower = towerUp && (tank >= 25 || towerOnMinion || soloTower || chip || !safety && hpR > 0.5f && !towerOnMe || defenders == 0 && hpR > 0.6f && !towerOnMe && tank > 0
                || hpR > 0.5f && tank > 0 && foesAlive < pushers.Count
                || hpR > 0.5f && tank > 0 && foeDead
                || hpR > 0.6f && tank >= 10 && StarDiff >= 2);
            if (!shouldTower && !retreat && PushWindow(enemyTower, hpR)) shouldTower = true;
            if (!shouldTower && !retreat && TeamTower(enemyTower, hpR)) shouldTower = true; // hit the tower with the human
            if (shouldTower && towerUp && tierI == 0 && !BronzeMinionsAtTower(enemyTower)) shouldTower = false; // beginners' tier: no tower rush without own minions
            if (finish) { shouldTower = true; target = null; }
            if (target != null && shouldTower && !urgent && Dist(h, enemyTower) < Dist(h, target) + 4) target = null;

            hasGoal = false; kite = null;
            if (retreat) GoalRetreat(myTower, enemyTower, fwd);
            else if (danger)
            {
                Mode = BotMode.Danger; focus = null;
                if (SafestSpot(out float sx, out float sy)) SetGoal(sx, sy); else SetGoal(myTower.X, myTower.Y - fwd);
            }
            else if (towerOnMe && tank < 25 && !soloTower && !chip && !finish && !(target != null && (target.Hp + ShieldOf(target)) / myDps < 1.5f))
            {
                Mode = BotMode.LeaveTower; focus = null;
                float a = SimMath.Atan2(h.Y - enemyTower.Y, h.X - enemyTower.X), r = enemyTower.UDef.Range + h.R + 0.8f;
                SetGoal(enemyTower.X + SimMath.Cos(a) * r, enemyTower.Y + SimMath.Sin(a) * r);
                if (!M.Map.Free(gX, gY, h.R)) SetGoal(h.X, enemyTower.Y - fwd * r);
            }
            else if (safety && hpR > BotTables.SoakMinHp && BotTables.Soak[tierI] > 0 && SoakTarget() is Unit sa)
            {
                Mode = BotMode.Soak; Unit e = null;
                for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.Team != h.Team && u.Kind == UnitKind.Hero && Sees(u) && (e == null || Dist(u, sa) < Dist(e, sa))) e = u; }
                focus = e;
                if (e != null) SetGoal(sa.X + (e.X - sa.X) * 0.25f, sa.Y + (e.Y - sa.Y) * 0.25f); else SetGoal(sa.X, sa.Y);
            }
            else if (target != null) GoalHero(target, enemyTower, safety);
            else if (hasSeen && seenU.Alive && !Sees(seenU) && !retreat && safety && WantsBushChase(enemyTower))
            {
                Mode = BotMode.BushChase; focus = null; SetGoal(seenX, seenY);
                if (Hyp(h.X - seenX, h.Y - seenY) < 1.2f) hasSeen = false;
            }
            else GoalLane(enemyTower, fwd, shouldTower, tank, towerUp, safety);
            if (v3 && target == null && !finish && (Mode == BotMode.Lane || Mode == BotMode.Group || Mode == BotMode.Support)) V3Goal(hpR, myTower, fwd);
            if (target == null && !finish && !retreat && (Mode == BotMode.Lane || Mode == BotMode.Group)) TeamFollow(hpR, myTower, fwd);
            else following = false;
            if (hasGoal && hasBlocked && now < blockedT && Hyp(gX - blockedX, gY - blockedY) < 1.5f) hasGoal = false;
            if (!hasGoal || !M.Map.FindPath(h.X, h.Y, gX, gY, now, path)) path.Clear();
            if (!h.SpecialReady) spWantT = 0;
            if (h.SpecialReady)
            {
                bool dz = danger || retreat && ttk < 3;
                if ((SpecialWanted(dz) || DragPlan(dz, out _, out _)) && !TeamSpecialHold(dz)) { if (float.IsNaN(spT)) spT = PickReact() * 0.5f * TK(11) * HumanReaction(); }
                else spT = float.NaN;
            }
        }

        bool AnyFoeHeroSeenWithin(Unit h, float r)
        {
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && u.Team != h.Team && Sees(u) && Dist(u, h) < r) return true; }
            return false;
        }

        static float InvulT(Unit u) { float t = 0; for (int i = 0; i < u.StCount; i++) if (u.Sts[i].Type == St.Invulnerable && u.Sts[i].T > t) t = u.Sts[i].T; return t; }

        bool BronzeMinionsAtTower(Unit tower)
        {
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.IsMinionLike && u.Team == H.Team && Dist(u, tower) < tower.UDef.Range + 3) return true; }
            return false;
        }

        // ---------- the goals ----------
        void GoalRetreat(Unit myTower, Unit enemyTower, int fwd)
        {
            var h = H; Mode = BotMode.Retreat; focus = null;
            bool threat = false; // (the old game: a minion's range was undefined there, so minions never count)
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (!u.Alive || u.Team == h.Team || u.IsClone || !Sees(u)) continue;
                if (u.Kind == UnitKind.Hero ? Dist(u, h) < u.Range + SpeedOf(u) * 1.5f + 2 : u.Kind == UnitKind.Tower && Dist(u, h) < u.UDef.Range + 1) { threat = true; break; }
            }
            var fightAt = Nearest(MatesWithin(h, float.MaxValue, bufMates), h);
            float clear = Math.Max(h.Range, 3.5f) + 1;
            float bx = 0, by = 0;
            bool bush = fightAt != null && Dist(fightAt, h) < 9 && BushSpot(fightAt.X, fightAt.Y, 6, enemyTower, clear, out bx, out by);
            if (!bush) bush = BushSpot(h.X, h.Y, 7, enemyTower, clear, out bx, out by);
            if (bush && Now - h.LastCombat > 0.8f && (M.Map.InGrass(h.X, h.Y) && Hyp(h.X - bx, h.Y - by) < 1.2f || !threat || Hyp(h.X - bx, h.Y - by) < 3)) SetGoal(bx, by, 0.3f);
            else if (threat || Now - h.LastCombat < 0.5f)
            {
                float d = Dist(h, myTower); if (d == 0) d = 1;
                float backX = myTower.X, backY = myTower.Y - fwd * (myTower.R + h.R + 0.6f);
                bool backOk = Walkable(h, backX, backY);
                if (d > 5.5f) SetGoal(h.X + (myTower.X - h.X) / d * 4, h.Y + (myTower.Y - h.Y) / d * 4);
                else if (backOk && Hyp(h.X - backX, h.Y - backY) > 0.4f) SetGoal(backX, backY);
                if (hasGoal && !Walkable(h, gX, gY)) { if (backOk) SetGoal(backX, backY); else hasGoal = false; }
            }
        }

        void GoalHero(Unit target, Unit enemyTower, bool safety)
        {
            var h = H; float now = Now;
            if (focus != target) { commitT = now + (ttk < 3 ? 0.7f : StarDiff >= 2 ? 1.8f : 1.2f) * Srand(0.85f, 1.15f) * TK(9); commitHp = h.Hp; }
            else if (commitT > now && commitHp - h.Hp > h.MaxHp * 0.25f)
                for (int i = 0; i < M.Heroes.Count; i++) { var k = M.Heroes[i]; if (k != target && now - h.LastHitBy[i] < 1) { commitT = 0; break; } }
            ChaseTrack(target);
            Mode = Outranges(h, target) && safety ? BotMode.Harass : BotMode.Hero; focus = target;
            hasSeen = true; seenU = target; seenX = target.X; seenY = target.Y; seenT = now;
            float d = Dist(h, target); if (d == 0) d = 1;
            float keep = Mode == BotMode.Harass ? HarassKeep(h, target) : h.Range * 0.85f + target.R;
            SetGoal(target.X + (h.X - target.X) / d * keep, target.Y + (h.Y - target.Y) / d * keep, keep + 0.1f, target);
            if (v3 && TK3(30) > 0 && V3CleanSpot(target, keep, out float cx, out float cy)) SetGoal(cx, cy);
            if (d < h.Range + target.R + 3 && !M.Map.LosClear(h, target)) SetGoal(target.X, target.Y, 0.3f, target); // a wall in the way: walk round it
            if (safety && (BotTables.Ranged(h.Hero) && target.Range < 2.6f || Outranges(h, target)) && d < target.Range + target.R + h.R + 0.4f) kite = target;
            else if (safety && d > h.Range + 1.5f && d < 9 && !M.Map.InGrass(h.X, h.Y) && gStop != 0.3f)
            {
                if (BushSpot(target.X, target.Y, h.Range + 1, enemyTower, 0, out float bx, out float by) && Hyp(bx - target.X, by - target.Y) < d - 1 && Hyp(h.X - bx, h.Y - by) < d) SetGoal(bx, by, 0.3f);
            }
        }

        void GoalLane(Unit enemyTower, int fwd, bool shouldTower, float tank, bool towerUp, bool safety)
        {
            var h = H; focus = null;
            var fl = LaneTarget(h.Team);
            int si = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a == h) break; if (a.Kind == UnitKind.Hero && a.Team == h.Team) si++; }
            float slot = (si - 1) * 0.9f;
            Mode = shouldTower ? BotMode.Tower : BotMode.Lane;
            float tr = enemyTower.UDef.Range;
            if (shouldTower) SetGoal(enemyTower.X + slot * 0.6f, enemyTower.Y - fwd * (h.Range * 0.85f + enemyTower.R), h.Range * 0.85f + enemyTower.R, enemyTower);
            else if (fl != null && fl.Kind != UnitKind.Tower)
            {
                float y = fl.Y - fwd * (BotTables.Ranged(h.Hero) ? 1.6f : 0.8f);
                if (towerUp && Math.Abs(y - enemyTower.Y) < tr + 0.6f && tank < 25) y = enemyTower.Y - fwd * (tr + 0.8f);
                SetGoal(fl.X + slot, y);
            }
            else if (towerUp) SetGoal(enemyTower.X + slot, enemyTower.Y - fwd * (tr + 1.2f));
            if (shouldTower && v3 && TK3(43) > 0.5f && V3CleanSpot(enemyTower, h.Range * 0.85f + enemyTower.R, out float cx, out float cy)) SetGoal(cx, cy); // hit the tower, not the minions around it
            if (shouldTower && Dist(h, enemyTower) < h.Range + enemyTower.R + 3 && !M.Map.LosClear(h, enemyTower)) SetGoal(enemyTower.X, enemyTower.Y, enemyTower.R + h.R + 0.3f, enemyTower);
            if (!shouldTower && hasGoal && safety)
            {
                var mates = MatesWithin(h, float.MaxValue, bufMates);
                if (BotTables.Tank(h.Hero) && fl != null && fl.Kind != UnitKind.Tower) gY += fwd * 0.5f;
                int foesAhead = 0;
                for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && u.Team != h.Team && Sees(u) && Hyp(u.X - gX, u.Y - gY) < 8) foesAhead++; }
                var mate = Nearest(mates, h);
                if (mate != null && foesAhead >= 2 && Dist(mate, h) > 6) { Mode = BotMode.Group; SetGoal(mate.X, mate.Y - fwd * 1.5f); }
            }
            if (!shouldTower && hasGoal && safety && Mode == BotMode.Lane && BushSpot(gX, gY, Math.Max(1.5f, h.Range - 0.5f), enemyTower, 0, out float ax, out float ay)) SetGoal(ax, ay, 0.3f);
            // people don't stand perfectly still while they wait at the wave: shuffle a little around the spot
            if (TK(8) > 0 && Mode == BotMode.Lane && hasGoal && gStop == 0 && Hyp(h.X - gX, h.Y - gY) < 1.2f)
            {
                float jx = gX + Srand(-0.6f, 0.6f), jy = gY + Srand(-0.5f, 0.5f);
                if (Walkable(h, jx, jy) && !NearEnemyTowerAt(jx, jy, h.Team, 0.3f)) { gX = jx; gY = jy; }
            }
        }

        /// <summary>The team's minion furthest up the lane, else the enemy tower.</summary>
        Unit LaneTarget(int team)
        {
            int dir = team == Teams.Enemy ? 1 : -1; Unit best = null;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var b = us[i]; if (b.Alive && b.IsMinionLike && b.Team == team && (best == null || b.Y * dir > best.Y * dir)) best = b; }
            return best ?? TowerOf(Teams.Other(team));
        }

        /// <summary>V3: defend our tower, hold near it while outnumbered, or step out to regenerate.</summary>
        void V3Goal(float hpR, Unit myTower, int fwd)
        {
            var h = H;
            if (myTower == null || !myTower.Alive) return;
            float tr = myTower.UDef.Range; var us = M.Units;
            if (TK3(2) > 0)
            {
                int eh = 0, em = 0; float cx = 0, cy = 0;
                for (int i = 0; i < us.Count; i++)
                {
                    var u = us[i];
                    if (u.Alive && u.Team != h.Team && u.Kind != UnitKind.Tower && !u.IsClone && Dist(u, myTower) < tr + 3 && (u.Kind != UnitKind.Hero || Sees(u))) { if (u.Kind == UnitKind.Hero) eh++; else em++; cx += u.X; cy += u.Y; }
                }
                if (eh >= 1 && (eh * 2 + em * 0.5f) * TK3(2) >= 2 && Dist(h, myTower) < 30)
                {
                    int n = eh + em; cx /= n; cy /= n; float d = Hyp(cx - myTower.X, cy - myTower.Y); if (d < 0.1f) d = 0.1f;
                    float x = myTower.X + (cx - myTower.X) / d * Math.Min(d, 1.5f), y = myTower.Y + (cy - myTower.Y) / d * Math.Min(d, 1.5f);
                    if (Walkable(h, x, y)) { Mode = BotMode.Defend; SetGoal(x, y); return; }
                }
            }
            if (TK3(7) > 0)
            {
                int numbers = 0; bool foeClose = false;
                for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && !u.IsClone) numbers += u.Team == h.Team ? 1 : -1; }
                for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && u.Team != h.Team && !u.IsClone && (Sees(u) ? Dist(u, myTower) < tr + 12 : RemNear(u, h.Team, myTower.X, myTower.Y, tr + 12))) { foeClose = true; break; } }
                if (foeClose && numbers < 0 && numbers * TK3(7) <= -1 && TeamLeader() == null) // with a human leader the bot follows instead
                {
                    float x = myTower.X + Srand(-1, 1), y = myTower.Y + fwd * (tr - 1);
                    if (Walkable(h, x, y)) { Mode = BotMode.Hold; SetGoal(x, y); return; }
                }
            }
            if (hpR < TK3(1) * 0.75f)
            {
                if (AnyFoeHeroSeenWithin(h, 9)) return;
                float d = Dist(h, myTower); if (d < 4) return;
                float x = h.X + (myTower.X - h.X) / d * 3, y = h.Y + (myTower.Y - h.Y) / d * 3;
                if (Walkable(h, x, y)) { Mode = BotMode.Regen; SetGoal(x, y); }
            }
        }

        // V3: auto attacks hit a random enemy in range, so stand where the target is the only enemy in reach
        int V3Clutter(Unit tg, float x, float y)
        {
            var h = H; int n = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Alive && u != tg && u.Team != h.Team && Hyp(u.X - x, u.Y - y) <= h.Range + u.R + 0.15f && !u.Has(St.Untargetable) && Sees(u) && M.Map.LosClear(x, y, u.X, u.Y)) n++;
            }
            return n;
        }
        bool V3CleanSpot(Unit tg, float keep, out float bx, out float by)
        {
            var h = H; bx = by = 0;
            float d = Dist(h, tg); bool wide = TK3(36) > 0.5f;
            if (d > h.Range + tg.R + (wide ? 4 : 2.5f)) return false;
            int nowN = V3Clutter(tg, h.X, h.Y); if (nowN == 0) return false;
            ThreatBegin();
            float rr = Math.Min(keep, h.Range + tg.R - 0.15f), w = 2 * TK3(30), baseA = SimMath.Atan2(h.Y - tg.Y, h.X - tg.X);
            float sNow = nowN * w + 3 * TK3(37) / Math.Max(0.3f, V3TtkAt(h.X, h.Y)) + (d > h.Range + tg.R ? 1 : 0);
            bool found = false; float bs = sNow - 0.5f;
            for (int k = wide ? -15 : -4; k <= (wide ? 16 : 4); k++)
            {
                float rk = wide && (k & 1) != 0 ? Math.Max(0.6f, rr - 0.7f) : rr, a = baseA + (wide ? k / 2 : k) * SimMath.PI / 8;
                float px = tg.X + SimMath.Cos(a) * rk, py = tg.Y + SimMath.Sin(a) * rk;
                if (!Walkable(h, px, py) || NearEnemyTowerAt(px, py, h.Team, 0.3f) && !NearEnemyTower(h, 0.3f) || !M.Map.LosClear(px, py, tg.X, tg.Y)) continue;
                float sc = V3Clutter(tg, px, py) * w + 3 * TK3(37) / Math.Max(0.3f, V3TtkAt(px, py)) + Hyp(px - h.X, py - h.Y) * 0.3f;
                if (sc < bs) { bs = sc; bx = px; by = py; found = true; }
            }
            return found;
        }
    }
}
