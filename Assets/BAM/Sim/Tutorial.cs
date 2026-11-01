using System;

namespace BAM.Sim
{
    /// <summary>
    /// The guided first match's arena half: a real small first battle with Tyra, as on the island (Rickard 2026-10-29: "andra delen
    /// är inte bra"; he chose a real fight over the old walk past target boards). The boost pad throws Tyra onto the lane of the real
    /// arena, two of her minions walk with her and a small enemy wave comes down. One short line only when it is needed:
    /// 1 walk up the lane with the minions, 2 fight the wave (Tyra attacks automatically; her hits charge the special), 3 TAP the
    /// special when it is ready (an auto-aimed bolt), 4 Knut comes down the lane: DRAG the special to aim a bolt at him (the charge is
    /// full for it and comes back until an aimed bolt hits him), 5 he throws his anchor and pulls Tyra in: tap BLINK to get away,
    /// 6 take him down (he fights for real; Tyra cannot drop below 40 %), 7 step out of the fight and heal (the real regen),
    /// 8 break the enemy tower together with a new pair of minions; it falls to exactly TowerHits of Tyra's auto attacks.
    /// The towers never shoot. No failure. The match screen reads this.
    /// </summary>
    public sealed partial class Tutorial
    {
        public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepThrow = 3, StepBlink = 4, StepFight = 5, StepHeal = 6, StepTower = 7;
        /// <summary>The opening: DESTROY THE ENEMY TOWER over Tyra as she lands (no look at the tower first since 2026-10-29).</summary>
        public const float OpenHold = 0f, OpeningTime = 1.8f;
        public const int TowerHits = 6;
        /// <summary>Moment 1's ring on the lane (Tyra walks to it, or any 2.5 tiles).</summary>
        public const float MarkX = 7.5f, MarkY = 16.2f;
        public const float BurstDelay = 0.15f;

        /// <summary>The moment the player is in.</summary>
        public int Step;
        /// <summary>The one hint to show now, null = none.</summary>
        public string Line;
        /// <summary>The first OpeningTime s: the title over Tyra's landing.</summary>
        public bool Opening;
        /// <summary>Dummy: no practice target any more (null; kept for the screens). Guard: Knut. Goal: what the gold trail leads
        /// to now (the nearest enemy minion, Knut, the tower), null = nothing.</summary>
        public Unit Dummy, Guard, Tower, Goal;
        public float HitAt = -99;
        /// <summary>The battle spell (Blink) is on: from moment 5 (Match.SpellsOn).</summary>
        public bool SpellOn;
        /// <summary>The enemy tower fell: the guided match is complete.</summary>
        public bool Done;
        /// <summary>When the tower fell (Match.Time), for the end screen.</summary>
        public float DoneAt = -99;
        /// <summary>Tyra's confirmed hits (a rise of the real charge) at these match times, newest last: "+ CHARGE" flies from her to the button.</summary>
        public readonly float[] ChargeTimes = new float[8];
        public int ChargeCount;
        /// <summary>Confirmed hits on the tower so far (its bar is TowerHits blocks) and when the last one landed.</summary>
        public int TowerHitsTaken => TowerHitCount;
        public float LastTowerHit => TowerHitAt;
        /// <summary>The current moment's special lesson is done (the tap hit something; the aimed bolt hit Knut).</summary>
        public bool BurstDone => Burst;
        /// <summary>How long the current line has been up.</summary>
        public float LineShown => Shown;

        internal float T, Moved, Lx, Ly, Shown, LastCharge, BurstAt = -99, TowerHitAt = -99, GuardDir = 1, KnutDownAt = -1, BlinkAt = -1, PressAt = -99, HookAt = -1, WaveAt = -99;
        internal int Gained, TowerHitCount;
        internal bool Burst, PressAimed, Hooked, Helpers;

        // ---------- the training island (the first half, Tyra; TutorialIsland.cs). Unused in the arena half. ----------
        public const int IslandWalk = 0, IslandMinion = 1, IslandGrass = 2, IslandTarget = 3, IslandPad = 4;
        /// <summary>This is the island half: Step is one of the Island* steps, Done means the boost pad threw Tyra and the
        /// picture has faded to black, so the game starts the arena half (a new Match with Tutorial and without Island).</summary>
        public bool Island;
        /// <summary>The line with the words to highlight between '|' marks (Line is the same text without them); '\n' breaks
        /// lines. Tip: highlight in the calm colour (teal) instead of red. Speaker: who says it ("FENN SAYS"), null = nobody.</summary>
        public string LineMarked, Speaker;
        public bool LineTip;
        /// <summary>The camera frames the whole island at (CamX, CamY) until the bridge opens; then it follows Tyra.</summary>
        public bool CamHold;
        public float CamX, CamY;
        /// <summary>A pulsing ring on the ground at (RingX, RingY), with a bouncing arrow over it when ArrowOnRing.</summary>
        public bool RingOn, ArrowOnRing;
        public float RingX, RingY;
        /// <summary>A bouncing arrow over this unit (the minion to take down, the leader to single out); null = none.</summary>
        public Unit PointAt;
        /// <summary>The bridge and the lane beyond it are open (shown) from the last step on.</summary>
        public bool BridgeOpen;
        /// <summary>The boost pad, shown at its tile centre once the bridge opens.</summary>
        public bool PadOn;
        public float PadX, PadY;
        /// <summary>The pad threw Tyra: she flies (St.Casting) and the picture fades to black, Fade 0..1.</summary>
        public bool Jumping;
        public float Fade;
        /// <summary>The leader of the three (taking it down takes down the other two).</summary>
        public Unit Leader;

        internal float Pause, Leave, JumpLeft;
        internal readonly System.Collections.Generic.List<Unit> Minions = new System.Collections.Generic.List<Unit>(8);
    }

    public sealed partial class Match
    {
        // positions on our map (the enemy tower at (7.5, 5.5), the lane straight down x = 7.5)
        const float TutStartX = 7.5f, TutStartY = 19f, TutMarkX = Tutorial.MarkX, TutMarkY = Tutorial.MarkY;
        // Knut walks calmly side to side 2 tiles in front of the tower, right of the lane, until he joins the fight
        const float TutGuardY = 7.5f, TutGuardX0 = 9.0f, TutGuardX1 = 10.3f, TutGuardPace = 0.5f;
        const float TutMoveDist = 2.5f;   // tiles walked (any way) that end moment 1
        const float TutLineMin = 1.3f;    // the least time a line stays up
        const float TutAfter = 0.7f;      // a pause after a lesson is done, before the next line
        const float TutRefillAfter = 1.2f; // moment 4: the charge refills this long after the last press
        // the enemy wave: it comes from just in front of the enemy tower, a new pair when none is left before the tap is learned
        const float TutWaveY = 9.5f, TutWaveGap = 1.5f;
        // Knut fights for real (long enough that Tyra feels it and has something to heal); Tyra never drops below 40 %
        const float TutKnutHp = 380f, TutKnutDmg = 1.6f, TutKnutPace = 0.85f, TutAfterFight = 0.8f, TutPlayerFloor = 0.4f;
        const float TutHookAfter = 0.6f;  // moment 5: Knut throws his anchor this long after Tyra's aimed bolt hit him
        // moment 7 ends when Tyra has healed back to this share (from 40 % about 8 s of the real regen)
        const float TutHealed = 0.95f;

        const string LineMove = "Walk up the lane with your minions!", LineAuto = "Fight the wave! Tyra attacks automatically.",
            LineSpecial = "SPECIAL READY! TAP it to fire a bolt!", LineThrow = "An enemy hero! DRAG SPECIAL to aim a bolt at Knut!",
            LineBlink = "He pulled you in! Tap BLINK to dash away!", LineFight = "Now take Knut down!",
            LineHeal = "Step out of the fight to heal up!", LineTower = "Destroy the tower with your minions!";

        void StartTutorial()
        {
            NoStars = true;
            var tu = Tut = new Tutorial { Step = Tutorial.StepMove, Tower = Towers[Teams.Enemy] };
            State = MatchState.Play; Time = 0;
            // the towers never shoot in the guided match (their health and damage are untouched)
            for (int t = 0; t < 2; t++) AddStatus(Towers[t], St.Disarm, 1e9f);

            // Tyra, made stronger as the old onboarding match did (FirstOnboardingMatch)
            var p = NewHero(HeroId.Tyra, Teams.Player, TutStartX, TutStartY);
            p.IsPlayer = true; p.DmgMult = 2; p.TakenMult = 0.5f; p.Charge = 0; Player = p;
            tu.Lx = p.X; tu.Ly = p.Y;

            // two of her minions land with her and walk the lane; the first enemy wave is already on its way
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX - 0.8f, TutStartY - 0.6f);
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX + 0.8f, TutStartY - 0.6f);
            TutWave(true);

            // Knut: no bot, no attacks, never a target until moment 4
            var k = NewHero(HeroId.Knut, Teams.Enemy, TutGuardX0, TutGuardY);
            k.Dummy = true;
            AddStatus(k, St.Disarm, 1e9f); AddStatus(k, St.Untargetable, 1e9f);
            tu.Guard = k;
            RefreshTutorial();
        }

        // a lane minion with its next waypoint ahead of it (the lane's points are at y 28.5, 17.5 and 6.5)
        Unit TutMinion(MinionKind kind, int team, float x, float y)
        {
            var m = NewMinion(kind, team, x, y);
            if (team == Teams.Player) m.Wp = y > Map.LaneY[1] + 0.8f ? 1 : 2;
            else m.Wp = y < Map.LaneY[1] - 0.8f ? 1 : 2;
            return m;
        }

        // the enemy wave: two melee minions and (the first time) a ranged one behind them
        void TutWave(bool first)
        {
            TutMinion(MinionKind.Melee, Teams.Enemy, 7.5f - 0.6f, TutWaveY);
            TutMinion(MinionKind.Melee, Teams.Enemy, 7.5f + 0.6f, TutWaveY);
            if (first) TutMinion(MinionKind.Ranged, Teams.Enemy, 7.5f, TutWaveY - TutWaveGap);
            Tut.WaveAt = Time;
        }

        bool TutEnemyMinions()
        {
            for (int i = 0; i < Units.Count; i++) { var u = Units[i]; if (u.Alive && u.IsMinionLike && u.Team == Teams.Enemy) return true; }
            return false;
        }

        Unit TutNearestFoeMinion(Unit p)
        {
            Unit best = null; float bd = 1e9f;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || !u.IsMinionLike || u.Team != Teams.Enemy) continue;
                float d = SimMath.Hypot(u.X - p.X, u.Y - p.Y);
                if (d < bd) { bd = d; best = u; }
            }
            return best;
        }

        string TutLineNow()
        {
            var tu = Tut;
            if (State != MatchState.Play || tu.Done) return null;
            switch (tu.Step)
            {
                case Tutorial.StepMove: return tu.Opening ? null : LineMove;
                case Tutorial.StepAuto: return LineAuto;
                // no line until the special is ready: the screen flies "+ CHARGE" into the button instead
                case Tutorial.StepSpecial: return !tu.Burst && Player.SpecialReady ? LineSpecial : null;
                case Tutorial.StepThrow: return !tu.Burst ? LineThrow : null;
                case Tutorial.StepBlink: return tu.Hooked && Player.SpellAt <= Time ? LineBlink : null;
                case Tutorial.StepFight: return tu.Guard != null && tu.Guard.Alive ? LineFight : null;
                case Tutorial.StepHeal: return LineHeal;
                default: return LineTower;
            }
        }

        void RefreshTutorial()
        {
            Tut.Opening = Tut.Step == Tutorial.StepMove && Time < Tutorial.OpeningTime;
            Tut.Line = TutLineNow();
        }

        void TutNext(int step) { var tu = Tut; tu.Step = step; tu.T = 0; tu.Shown = 0; tu.Burst = false; }

        void UpdateTutorial(float dt)
        {
            if (Tut.Island) { UpdateIsland(dt); return; }
            var tu = Tut; var p = Player;
            tu.T += dt;
            RefreshTutorial();
            // the special pressed this tick (PlayerAct casts it later in this tick): a drag carries an angle, a tap does not
            if (specialBuf > 0) { tu.PressAt = Time; tu.PressAimed = !float.IsNaN(bufAng); }
            // a confirmed hit is a rise of the real charge
            if (p.Charge > tu.LastCharge)
            {
                tu.Gained++;
                if (tu.ChargeCount == tu.ChargeTimes.Length) { Array.Copy(tu.ChargeTimes, 1, tu.ChargeTimes, 0, tu.ChargeCount - 1); tu.ChargeCount--; }
                tu.ChargeTimes[tu.ChargeCount++] = Time;
            }
            tu.LastCharge = p.Charge;
            // Tyra cannot lose the guided match
            if (p.Alive && p.Hp < p.MaxHp * TutPlayerFloor) p.Hp = p.MaxHp * TutPlayerFloor;

            var k = tu.Guard;
            if (k != null && k.Alive && tu.Step < Tutorial.StepThrow)
            {
                if (k.X >= TutGuardX1) tu.GuardDir = -1; else if (k.X <= TutGuardX0) tu.GuardDir = 1;
                MoveUnit(k, tu.GuardDir * TutGuardPace, 0, dt);
                k.Y = TutGuardY;
            }
            else if (k != null && k.Alive && !k.Has(St.Casting) && !k.Has(St.Stun))
            {
                // Knut's simple fight: walk at Tyra until she is in his reach, then stand and swing (the real auto attack)
                float dx = p.X - k.X, dy = p.Y - k.Y, dist = SimMath.Hypot(dx, dy);
                if (p.Alive && dist > (k.Range + p.R) * 0.9f) MoveUnit(k, dx / dist * TutKnutPace, dy / dist * TutKnutPace, dt);
                else if (dist > 0.01f) k.Facing = SimMath.Atan2(dy, dx);
            }
            else if (k != null && !k.Alive) k.RespawnT = 1e9f; // Knut stays down

            if (!tu.Opening) tu.Moved += SimMath.Hypot(p.X - tu.Lx, p.Y - tu.Ly);
            tu.Lx = p.X; tu.Ly = p.Y;
            if (tu.Line != null) tu.Shown += dt;
            // the gold trail's landmark
            tu.Goal = tu.Step == Tutorial.StepAuto || tu.Step == Tutorial.StepSpecial ? TutNearestFoeMinion(p)
                : tu.Step >= Tutorial.StepThrow && tu.Step <= Tutorial.StepFight && k != null && k.Alive && tu.Step != Tutorial.StepBlink ? k
                : tu.Step == Tutorial.StepTower ? tu.Tower : null;

            switch (tu.Step)
            {
                case Tutorial.StepMove:
                    if (!tu.Opening && (tu.Moved >= TutMoveDist || SimMath.Hypot(p.X - TutMarkX, p.Y - TutMarkY) < 0.9f)) TutNext(Tutorial.StepAuto);
                    break;
                case Tutorial.StepAuto:
                    if (p.SpecialReady && tu.Shown >= TutLineMin) TutNext(Tutorial.StepSpecial);
                    else if (!TutEnemyMinions() && Time - tu.WaveAt > 2f) TutWave(false); // more to hit until the special is charged
                    break;
                case Tutorial.StepSpecial:
                    if (!tu.Burst && !TutEnemyMinions() && Time - tu.WaveAt > 2f) TutWave(false); // something for the bolt
                    if (tu.Burst && Time - tu.BurstAt >= TutAfter)
                    {
                        TutNext(Tutorial.StepThrow);
                        StartFight();
                        p.Charge = p.HDef.MaxCharge;
                        // one-tap special (Rules.OneTapSpecial, 2026-11-01): no drag to teach, so moment 4 (the aimed bolt at Knut, where
                        // new players stalled) counts as done at once and Knut comes on to his hook (moment 5) with Tyra's special full
                        if (OneTap) { tu.Burst = true; tu.BurstAt = Time; }
                    }
                    break;
                case Tutorial.StepThrow:
                    // the charge comes back full until an aimed bolt hits Knut, a moment after the last press so a bolt in the air
                    // lands first
                    if (!tu.Burst) { if (!p.SpecialReady && Time - tu.PressAt >= TutRefillAfter) p.Charge = p.HDef.MaxCharge; }
                    else if (Time - tu.BurstAt >= TutHookAfter && (!OneTap || k == null || !k.Alive || SimMath.Dist(k, p) <= KnutS.HookDistance - 1f || tu.T > 5f)) // one tap: wait for Knut to come within his hook
                    {
                        TutNext(Tutorial.StepBlink);
                        tu.SpellOn = true; p.Spell = SpellId.Blink; p.SpellAt = Time; p.SpellChanEnd = -1;
                    }
                    break;
                case Tutorial.StepBlink:
                    // Knut throws his anchor at Tyra (his real special): once it has pulled her in, BLINK gets her away
                    if (!tu.Hooked && k != null && k.Alive)
                    {
                        if (tu.HookAt < 0 && !k.Has(St.Casting))
                        {
                            k.Charge = k.HDef.MaxCharge;
                            if (TryCastSpecial(k, SimMath.Atan2(p.Y - k.Y, p.X - k.X), 1)) tu.HookAt = Time;
                        }
                        if (tu.HookAt >= 0 && Time - tu.HookAt >= KnutS.CastTime + 0.4f) tu.Hooked = true; // pulled or missed: on with the lesson
                        if (tu.HookAt < 0 && tu.T > 3f) tu.Hooked = true; // could not throw (too far, a wall): teach the blink anyway
                    }
                    else if (k == null || !k.Alive) tu.Hooked = true;
                    if (tu.Hooked && p.SpellAt > Time) { if (tu.BlinkAt < 0) tu.BlinkAt = Time; if (Time - tu.BlinkAt >= TutAfter) TutNext(Tutorial.StepFight); }
                    break;
                case Tutorial.StepFight:
                    if (k == null || !k.Alive)
                    {
                        if (tu.KnutDownAt < 0) tu.KnutDownAt = Time;
                        if (Time - tu.KnutDownAt >= TutAfterFight) TutNext(p.Hp >= p.MaxHp * TutHealed ? Tutorial.StepTower : Tutorial.StepHeal);
                    }
                    break;
                case Tutorial.StepHeal: if (p.Hp >= p.MaxHp * TutHealed && tu.Shown >= TutLineMin) TutNext(Tutorial.StepTower); break;
            }
            // the push: a fresh pair of minions walks to the tower with Tyra
            if (tu.Step == Tutorial.StepTower && !tu.Helpers)
            {
                tu.Helpers = true;
                TutMinion(MinionKind.Melee, Teams.Player, p.X - 0.8f, p.Y + 0.8f);
                TutMinion(MinionKind.Melee, Teams.Player, p.X + 0.8f, p.Y + 0.8f);
            }
            RefreshTutorial();
        }

        // Knut becomes a real enemy: a target for Tyra, and his own swings hurt
        void StartFight()
        {
            var k = Tut.Guard;
            if (k == null || !k.Alive) return;
            k.RemoveAll(St.Disarm); k.RemoveAll(St.Untargetable);
            k.MaxHp = k.Hp = TutKnutHp; k.DmgMult = TutKnutDmg; k.Charge = 0;
            Tut.KnutDownAt = -1;
        }

        /// <summary>Every hit in the arena half (DealDamage, before the target takes it): the lessons that need a special to land.</summary>
        void TutSawHit(Unit src, Unit tgt, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island || tu.Burst || src != p || kind != DmgKind.Special || tgt.Team == p.Team) return;
            // moment 3: any bolt that hits; moment 4: only a bolt Tyra aimed, and only on Knut
            bool ok = tu.Step == Tutorial.StepSpecial || tu.Step == Tutorial.StepThrow && tu.PressAimed && tgt == tu.Guard;
            if (ok) { tu.Burst = true; tu.BurstAt = Time; }
        }

        /// <summary>DealDamage, for the guided match's enemy tower only: only Tyra's own auto attacks in the last moment hurt it,
        /// each taking the same share of what is left, so the TowerHits-th hit always breaks it (regen between hits changes nothing);
        /// everything else (her minions' swings too) deals 0.</summary>
        float TutDamage(Unit src, Unit tgt, float amount, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island || tgt != tu.Tower) return amount;
            if (tu.Step != Tutorial.StepTower || kind != DmgKind.Auto || src == null || src != p) return 0;
            tu.TowerHitCount++; tu.TowerHitAt = Time;
            if (tu.TowerHitCount >= Tutorial.TowerHits) { tu.Done = true; tu.DoneAt = Time; tu.Line = null; return tgt.Hp; } // the tower falls and the match ends (team 0 wins)
            return tgt.Hp / (Tutorial.TowerHits - tu.TowerHitCount + 1);
        }
    }
}
