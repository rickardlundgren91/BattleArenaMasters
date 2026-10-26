using System;

namespace BAM.Sim
{
    /// <summary>
    /// The guided first match (the old game's "first battle", SimLoop.cs, played there with Tyra): one walk up the lane of the
    /// real arena, with Fenn as on the island (Rickard 2026-10-26: the tutorial goes on after the island, no switch of hero). Five
    /// moments, one short line at a time: 1 move, 2 auto attack a harmless practice target, 3 its hits charge the real special and
    /// the special bursts the target, 4 (new, Rickard 2026-10-26) Knut, who walked to and fro in front of the tower until then,
    /// comes down the lane and fights back weakly until Fenn takes him down, 5 break the enemy tower, which falls to exactly
    /// TowerHits of Fenn's auto attacks. No waves, no timers, no failure. The match screen reads this.
    /// </summary>
    public sealed class Tutorial
    {
        public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepFight = 3, StepTower = 4;
        /// <summary>The opening: the camera rests on the enemy tower for OpenHold s, then glides down the lane to Fenn by OpeningTime.</summary>
        public const float OpenHold = 1.3f, OpeningTime = 2.4f;
        public const int TowerHits = 6;
        /// <summary>Moment 1's ring on the lane (Fenn walks to it, or any 2.5 tiles).</summary>
        public const float MarkX = 7.5f, MarkY = 16.2f;
        /// <summary>The practice target bursts this long after the special hit it (the special's own effect shows first).</summary>
        public const float BurstDelay = 0.15f;

        /// <summary>The moment the player is in.</summary>
        public int Step;
        /// <summary>The one hint to show now, null = none.</summary>
        public string Line;
        /// <summary>The first 2.4 s: the camera looks at the enemy tower.</summary>
        public bool Opening;
        public Unit Dummy, Guard, Tower;
        /// <summary>The practice target was last hit at this time (the screen shakes it).</summary>
        public float HitAt = -99;
        /// <summary>The enemy tower fell: the guided match is complete.</summary>
        public bool Done;
        /// <summary>When the tower fell (Match.Time), for the end screen.</summary>
        public float DoneAt = -99;
        /// <summary>Fenn's confirmed hits (a rise of the real charge) at these match times, newest last: "+ CHARGE" flies from him to the button.</summary>
        public readonly float[] ChargeTimes = new float[8];
        public int ChargeCount;
        /// <summary>Confirmed hits on the tower so far (its bar is TowerHits blocks) and when the last one landed.</summary>
        public int TowerHitsTaken => TowerHitCount;
        public float LastTowerHit => TowerHitAt;

        internal float T, Moved, Lx, Ly, Shown, LastCharge, BurstAt = -99, TowerHitAt = -99, GuardDir = 1, KnutDownAt = -1;
        internal int Gained, TowerHitCount;
        internal bool Burst;
        /// <summary>The special hit the practice target (it bursts a moment later).</summary>
        public bool BurstDone => Burst;
        /// <summary>How long the current line has been up (the screen fades it in over its first 0.3 s).</summary>
        public float LineShown => Shown;

        // ---------- the training island (the first half, Fenn; TutorialIsland.cs). Unused in the arena half. ----------
        public const int IslandWalk = 0, IslandMinion = 1, IslandGrass = 2, IslandTarget = 3, IslandPad = 4;
        /// <summary>This is the island half: Step is one of the Island* steps, Done means the boost pad threw Fenn and the
        /// picture has faded to black, so the game starts the arena half (a new Match with Tutorial and without Island).</summary>
        public bool Island;
        /// <summary>The line with the words to highlight between '|' marks (Line is the same text without them); '\n' breaks
        /// lines. Tip: highlight in the calm colour (teal) instead of red. Speaker: who says it ("FENN SAYS"), null = nobody.</summary>
        public string LineMarked, Speaker;
        public bool LineTip;
        /// <summary>The camera frames the whole island at (CamX, CamY) until the bridge opens; then it follows Fenn.</summary>
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
        /// <summary>The pad threw Fenn: he flies (St.Casting) and the picture fades to black, Fade 0..1.</summary>
        public bool Jumping;
        public float Fade;
        /// <summary>The leader of the three (taking it down takes down the other two).</summary>
        public Unit Leader;

        internal float Pause, Leave, JumpLeft;
        internal readonly System.Collections.Generic.List<Unit> Minions = new System.Collections.Generic.List<Unit>(8);
    }

    public sealed partial class Match
    {
        // positions: the old ones mirrored onto our map (y -> 35 - y); the enemy tower is at (7.5, 5.5)
        const float TutStartX = 7.5f, TutStartY = 19f, TutMarkX = Tutorial.MarkX, TutMarkY = Tutorial.MarkY, TutDummyX = 7.5f, TutDummyY = 12f;
        // Knut walks calmly side to side 2 tiles in front of the tower, right of the lane, so he never stands in Fenn's way to it
        const float TutGuardY = 7.5f, TutGuardX0 = 9.0f, TutGuardX1 = 10.3f, TutGuardPace = 0.5f;
        const float TutMoveDist = 2.5f;   // tiles walked (any way) that end moment 1
        const float TutLineMin = 1.3f;    // the least time a line stays up
        const float TutRefill = 0.35f;    // the practice target refills below this share of its health, so it lasts until the special
        const float TutAfterBurst = 0.7f; // then moment 4
        // moment 4: Knut comes down the lane and fights back, weakly (Fenn takes about 5 hits to beat him, he barely scratches Fenn)
        const float TutKnutHp = 260f, TutKnutDmg = 0.4f, TutKnutPace = 0.8f, TutAfterFight = 0.8f, TutPlayerFloor = 0.4f;

        const string LineMove = "Move with the joystick!", LineAuto = "Get close. Fenn attacks automatically!",
            LineSpecial = "SPECIAL READY! Tap to use it!", LineFight = "An enemy hero! Take Knut down!", LineTower = "Towers shoot! Destroy this one!";

        void StartTutorial()
        {
            NoStars = true;
            var tu = Tut = new Tutorial { Step = Tutorial.StepMove, Tower = Towers[Teams.Enemy] };
            State = MatchState.Play; Time = 0;
            // the towers never shoot in the guided match (their health and damage are untouched)
            for (int t = 0; t < 2; t++) AddStatus(Towers[t], St.Disarm, 1e9f);

            // Fenn, made stronger as the old onboarding match did (FirstOnboardingMatch)
            var p = NewHero(HeroId.Fenn, Teams.Player, TutStartX, TutStartY);
            p.IsPlayer = true; p.DmgMult = 2; p.TakenMult = 0.5f; p.Charge = 0; Player = p;
            tu.Lx = p.X; tu.Ly = p.Y;

            // the practice target: a melee minion inside (the screen draws it as a target board) that stands still, never attacks and
            // never dies until the special; no target until moment 2
            var d = NewMinion(MinionKind.Melee, Teams.Enemy, TutDummyX, TutDummyY);
            d.SpawnX = TutDummyX; d.SpawnY = TutDummyY;
            d.DmgOverride = 0; d.TutStatic = true; d.TutUnkillable = true;
            AddStatus(d, St.Disarm, 1e9f); AddStatus(d, St.Untargetable, 1e9f);
            d.MaxHp = d.Hp = Rules.TutorialMinionHp;
            tu.Dummy = d;

            // the guard: no bot, no attacks, no special, never in a target list until moment 4 (StartFight)
            var k = NewHero(HeroId.Knut, Teams.Enemy, TutGuardX0, TutGuardY);
            k.Dummy = true;
            AddStatus(k, St.Disarm, 1e9f); AddStatus(k, St.Untargetable, 1e9f);
            tu.Guard = k;
            RefreshTutorial();
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
                case Tutorial.StepFight: return tu.Guard != null && tu.Guard.Alive ? LineFight : null;
                default: return LineTower;
            }
        }

        void RefreshTutorial()
        {
            Tut.Opening = Tut.Step == Tutorial.StepMove && Time < Tutorial.OpeningTime;
            Tut.Line = TutLineNow();
        }

        void TutNext(int step) { var tu = Tut; tu.Step = step; tu.T = 0; tu.Shown = 0; }

        void UpdateTutorial(float dt)
        {
            if (Tut.Island) { UpdateIsland(dt); return; }
            var tu = Tut; var p = Player;
            tu.T += dt;
            RefreshTutorial();
            // a confirmed hit is a rise of the real charge (no charge for misses, wall-blocked or dropped shots, or the special)
            if (p.Charge > tu.LastCharge)
            {
                tu.Gained++;
                if (tu.ChargeCount == tu.ChargeTimes.Length) { Array.Copy(tu.ChargeTimes, 1, tu.ChargeTimes, 0, tu.ChargeCount - 1); tu.ChargeCount--; }
                tu.ChargeTimes[tu.ChargeCount++] = Time;
            }
            tu.LastCharge = p.Charge;
            var d = tu.Dummy;
            if (d != null && d.Alive)
            {
                if (d.LastCombat > tu.HitAt && d.LastCombat > 0) tu.HitAt = d.LastCombat;
                if (tu.Burst) { if (Time - tu.BurstAt >= Tutorial.BurstDelay) { d.TutUnkillable = false; Kill(d, p); } }
                else if (d.Hp < d.MaxHp * TutRefill) d.Hp = d.MaxHp; // refilled quietly (no heal numbers)
            }
            var k = tu.Guard;
            if (k != null && k.Alive && tu.Step < Tutorial.StepFight)
            {
                if (k.X >= TutGuardX1) tu.GuardDir = -1; else if (k.X <= TutGuardX0) tu.GuardDir = 1;
                MoveUnit(k, tu.GuardDir * TutGuardPace, 0, dt);
                k.Y = TutGuardY;
            }
            else if (k != null && k.Alive)
            {
                // the fight: Knut walks at Fenn until Fenn is in his reach, then stands and swings
                float dx = p.X - k.X, dy = p.Y - k.Y, dist = SimMath.Hypot(dx, dy);
                if (p.Alive && dist > (k.Range + p.R) * 0.9f) MoveUnit(k, dx / dist * TutKnutPace, dy / dist * TutKnutPace, dt);
                // Fenn cannot lose the guided match
                if (p.Alive && p.Hp < p.MaxHp * TutPlayerFloor) p.Hp = p.MaxHp * TutPlayerFloor;
            }
            else if (k != null) k.RespawnT = 1e9f; // Knut stays down
            if (!tu.Opening) tu.Moved += SimMath.Hypot(p.X - tu.Lx, p.Y - tu.Ly);
            tu.Lx = p.X; tu.Ly = p.Y;
            if (tu.Line != null) tu.Shown += dt;
            switch (tu.Step)
            {
                case Tutorial.StepMove:
                    if (!tu.Opening && (tu.Moved >= TutMoveDist || SimMath.Hypot(p.X - TutMarkX, p.Y - TutMarkY) < 0.9f))
                    {
                        TutNext(Tutorial.StepAuto); tu.Gained = 0;
                        if (d != null) d.RemoveAll(St.Untargetable);
                    }
                    break;
                case Tutorial.StepAuto: if (tu.Gained > 0 && tu.Shown >= TutLineMin) TutNext(Tutorial.StepSpecial); break; // once Fenn has hit and the line could be read
                case Tutorial.StepSpecial: if (tu.Burst && Time - tu.BurstAt >= TutAfterBurst) { TutNext(Tutorial.StepFight); StartFight(); } break;
                case Tutorial.StepFight:
                    if (k == null || !k.Alive) { if (tu.KnutDownAt < 0) tu.KnutDownAt = Time; if (Time - tu.KnutDownAt >= TutAfterFight) TutNext(Tutorial.StepTower); }
                    break;
            }
            RefreshTutorial();
        }

        // moment 4: Knut becomes a real (weak) enemy: a target for Fenn, and his own swings hurt Fenn a little
        void StartFight()
        {
            var k = Tut.Guard;
            if (k == null || !k.Alive) return;
            k.RemoveAll(St.Disarm); k.RemoveAll(St.Untargetable);
            k.MaxHp = k.Hp = TutKnutHp; k.DmgMult = TutKnutDmg;
            Tut.KnutDownAt = -1;
        }

        /// <summary>DealDamage, for the guided match's enemy tower and practice target only. The tower: only Fenn's own auto attacks
        /// in moment 4 hurt it, each taking the same share of what is left, so the TowerHits-th hit always breaks it (regen between
        /// hits changes nothing); everything else deals 0. The target: Fenn's special makes it burst (UpdateTutorial).</summary>
        float TutDamage(Unit src, Unit tgt, float amount, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island) return amount;
            if (tgt == tu.Dummy)
            {
                // Fenn's special is his wolf: its bites (or the pounce of a thrown one) count as the special
                bool special = src == p ? kind == DmgKind.Special : src != null && src.Kind == UnitKind.Wolf && src.Owner == p;
                if (special && tu.Step >= Tutorial.StepSpecial && !tu.Burst) { tu.Burst = true; tu.BurstAt = Time; }
                return amount;
            }
            if (tu.Step != Tutorial.StepTower || kind != DmgKind.Auto || src == null || src != p) return 0;
            tu.TowerHitCount++; tu.TowerHitAt = Time;
            if (tu.TowerHitCount >= Tutorial.TowerHits) { tu.Done = true; tu.DoneAt = Time; tu.Line = null; return tgt.Hp; } // the tower falls and the match ends (team 0 wins)
            return tgt.Hp / (Tutorial.TowerHits - tu.TowerHitCount + 1);
        }
    }
}
