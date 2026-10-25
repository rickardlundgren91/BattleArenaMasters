using System;

namespace BAM.Sim
{
    /// <summary>
    /// The guided first match (the old game's "first battle", SimLoop.cs): one walk up the lane of the real arena with Tyra. Four
    /// moments, one short line at a time: 1 move, 2 auto attack a harmless practice target, 3 its hits charge the real special and
    /// the special bursts the target, 4 break the enemy tower, which falls to exactly TowerHits of Tyra's auto attacks while Knut
    /// walks to and fro in front of it (harmless, never a target). No waves, no timers, no failure. The match screen reads this.
    /// </summary>
    public sealed class Tutorial
    {
        public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepTower = 3;
        /// <summary>The opening: the camera rests on the enemy tower for OpenHold s, then glides down the lane to Tyra by OpeningTime.</summary>
        public const float OpenHold = 1.3f, OpeningTime = 2.4f;
        public const int TowerHits = 6;
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

        internal float T, Moved, Lx, Ly, Shown, LastCharge, BurstAt = -99, TowerHitAt = -99, GuardDir = 1;
        internal int Gained, TowerHitCount;
        internal bool Burst;

        // ---------- the training island (the first half, Fenn; TutorialIsland.cs). Unused in the arena half. ----------
        public const int IslandWalk = 0, IslandMinion = 1, IslandGrass = 2, IslandRandom = 3, IslandOnly = 4, IslandPad = 5;
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
        const float TutStartX = 7.5f, TutStartY = 19f, TutMarkX = 7.5f, TutMarkY = 16.2f, TutDummyX = 7.5f, TutDummyY = 12f;
        // Knut walks calmly side to side 2 tiles in front of the tower, right of the lane, so he never crosses Tyra's shots at it
        const float TutGuardY = 7.5f, TutGuardX0 = 9.0f, TutGuardX1 = 10.3f, TutGuardPace = 0.5f;
        const float TutMoveDist = 2.5f;   // tiles walked (any way) that end moment 1
        const float TutLineMin = 1.3f;    // the least time a line stays up
        const float TutRefill = 0.35f;    // the practice target refills below this share of its health, so it lasts until the special
        const float TutAfterBurst = 0.7f; // then moment 4

        const string LineMove = "Move with the joystick!", LineAuto = "Get close. Tyra attacks automatically!",
            LineSpecial = "SPECIAL READY! Tap to use it!", LineTower = "Towers shoot! Destroy this one!";

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

            // the practice target: a melee minion inside (the screen draws it as a target board) that stands still, never attacks and
            // never dies until the special; no target until moment 2
            var d = NewMinion(MinionKind.Melee, Teams.Enemy, TutDummyX, TutDummyY);
            d.SpawnX = TutDummyX; d.SpawnY = TutDummyY;
            d.DmgOverride = 0; d.TutStatic = true; d.TutUnkillable = true;
            AddStatus(d, St.Disarm, 1e9f); AddStatus(d, St.Untargetable, 1e9f);
            d.MaxHp = d.Hp = Rules.TutorialMinionHp;
            tu.Dummy = d;

            // the guard: no bot, no attacks, no special, never in a target list
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
            if (p.Charge > tu.LastCharge) tu.Gained++;
            tu.LastCharge = p.Charge;
            var d = tu.Dummy;
            if (d != null && d.Alive)
            {
                if (d.LastCombat > tu.HitAt && d.LastCombat > 0) tu.HitAt = d.LastCombat;
                if (tu.Burst) { if (Time - tu.BurstAt >= Tutorial.BurstDelay) { d.TutUnkillable = false; Kill(d, p); } }
                else if (d.Hp < d.MaxHp * TutRefill) d.Hp = d.MaxHp; // refilled quietly (no heal numbers)
            }
            var k = tu.Guard;
            if (k != null && k.Alive)
            {
                if (k.X >= TutGuardX1) tu.GuardDir = -1; else if (k.X <= TutGuardX0) tu.GuardDir = 1;
                MoveUnit(k, tu.GuardDir * TutGuardPace, 0, dt);
                k.Y = TutGuardY;
            }
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
                case Tutorial.StepAuto: if (tu.Gained > 0 && tu.Shown >= TutLineMin) TutNext(Tutorial.StepSpecial); break; // once Tyra has hit and the line could be read
                case Tutorial.StepSpecial: if (tu.Burst && Time - tu.BurstAt >= TutAfterBurst) TutNext(Tutorial.StepTower); break;
            }
            RefreshTutorial();
        }

        /// <summary>DealDamage, for the guided match's enemy tower and practice target only. The tower: only Tyra's own auto attacks
        /// in moment 4 hurt it, each taking the same share of what is left, so the TowerHits-th hit always breaks it (regen between
        /// hits changes nothing); everything else deals 0. The target: Tyra's special makes it burst (UpdateTutorial).</summary>
        float TutDamage(Unit src, Unit tgt, float amount, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island) return amount;
            if (tgt == tu.Dummy)
            {
                if (kind == DmgKind.Special && src == p && tu.Step >= Tutorial.StepAuto && !tu.Burst) { tu.Burst = true; tu.BurstAt = Time; }
                return amount;
            }
            if (tu.Step != Tutorial.StepTower || kind != DmgKind.Auto || src == null || src != p) return 0;
            tu.TowerHitCount++; tu.TowerHitAt = Time;
            if (tu.TowerHitCount >= Tutorial.TowerHits) { tu.Done = true; tu.Line = null; return tgt.Hp; } // the tower falls and the match ends (team 0 wins)
            return tgt.Hp / (Tutorial.TowerHits - tu.TowerHitCount + 1);
        }
    }
}
