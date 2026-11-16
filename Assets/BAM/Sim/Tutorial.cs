using System;

namespace BAM.Sim
{
    /// <summary>
    /// The guided first match's arena half: a real small first battle with Tyra, as on the island (Rickard 2026-10-29: "andra delen
    /// är inte bra"; he chose a real fight over the old walk past target boards). The boost pad throws Tyra onto the lane of the real
    /// arena (she comes down out of the air for the first LandTime s, Tutorial.Air), two of her minions walk with her and a small
    /// enemy wave comes down. Six lessons, each done by the player, one short
    /// line at a time on the same guide card as the island's (tutorial review 2026-11-15: the lines did not match what happened,
    /// Knut, the heal and the tower asked nothing of the player and the tower never shot):
    /// 1 follow the minions into the wave (Tyra attacks by herself; her hits charge the special), 2 TAP the special when it is
    /// ready (an auto-aimed bolt), 3 Fenn steps out of the side grass, most often outside her white ring, where no tap reaches:
    /// a DRAG of the special aims it by hand, as far as it flies (Rickard 2026-11-16: "Sen har vi inte lärt spelaren att man kan
    /// dra skillen. Gör så att fenn dyker upp från buskarna och man får lära sig på honom"), 4 Knut comes down the lane, throws
    /// his anchor and pulls Tyra in: BLINK gets her away (with the joystick at rest it dashes away from him), 5 the special is
    /// full again: it finishes Knut (he cannot fall to anything else); a moment of the real regen follows, 6 the enemy tower
    /// shoots from now on, at the first enemy inside its ring: her minions go in first, and only while it is busy with them can
    /// Tyra hurt it; it falls to exactly TowerHits of her auto attacks.
    /// No failure: Tyra never drops below 40 %, and for a player who never makes the drag the game makes the shot in the end
    /// (TutDragMercy). The match screen reads this.
    /// </summary>
    public sealed partial class Tutorial
    {
        public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepThrow = 3, StepBlink = 4, StepFight = 5, StepHeal = 6, StepTower = 7;
        /// <summary>The opening: Tyra comes down from the island's boost pad for LandTime s (Air), then DESTROY THE ENEMY TOWER
        /// stands over her until OpeningTime (no look at the tower first since 2026-10-29).</summary>
        public const float OpenHold = 0f, LandTime = 0.55f, OpeningTime = LandTime + 1.8f;
        public const int TowerHits = 6;
        public const float BurstDelay = 0.15f;
        /// <summary>The whole guided match's lessons: the island's five (walk, attack, grass, Focus, one target) and the arena's
        /// six (charge, the tapped special, the dragged special, Blink, Knut, the tower).</summary>
        public const int IslandLessons = 5, ArenaLessons = 6, AllLessons = IslandLessons + ArenaLessons;
        /// <summary>StepTower's line: wait for the minions, go (the tower is busy with them), or it shoots you.</summary>
        public const int TowerWait = 0, TowerGo = 1, TowerYou = 2;

        /// <summary>The moment the player is in.</summary>
        public int Step;
        /// <summary>The one hint to show now, null = none (the plain text; LineMarked has the words to highlight).</summary>
        public string Line;
        /// <summary>The first OpeningTime s: Tyra's landing, then the title over her.</summary>
        public bool Opening;
        /// <summary>Dummy: no practice target any more (null; kept for the screens). Guard: Knut. Hider: Fenn, who waits hidden in
        /// the side grass until the drag lesson (StepThrow). Goal: what the gold trail leads to now (the nearest enemy minion,
        /// Knut, the tower), null = nothing (or the ring, when RingOn).</summary>
        public Unit Dummy, Guard, Hider, Tower, Goal;
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
        /// <summary>The current moment's special lesson is done (the tap hit something; the aimed bolt hit Fenn).</summary>
        public bool BurstDone => Burst;
        /// <summary>The drag lesson asks for the drag now: Fenn stands out of the grass and the special is ready. The screen then
        /// shows the drag over the SPECIAL button and lights the button though nobody may be in the ring; a tap fires nothing.</summary>
        public bool DragAsked;
        /// <summary>A drag aims the special from the drag lesson on (Match.DragAimOn); the lessons before it know the tap alone.</summary>
        public bool DragOn;
        /// <summary>Lessons done so far in the whole guided match, of Lessons (the arena half starts at IslandLessons): which
        /// word of praise the guide card says, and the ding for it. (The card showed them as a row of dots until 2026-11-16.)
        /// LessonAt: when the last one was done (Match.Time of this half), for the card's word of praise.</summary>
        public int Lesson, Lessons;
        public float LessonAt = -99;
        /// <summary>StepTower: which of its three lines is up (TowerWait, TowerGo, TowerYou).</summary>
        public int TowerSay;
        /// <summary>StepBlink: Knut's anchor has done its part and the line asks for BLINK now (the arrow over its button).</summary>
        public bool BlinkAsked => Hooked;
        /// <summary>The island's Focus step shows its point now (all three in reach, the line names FOCUS): the screen brings
        /// the FOCUS chip up, big under the guide card, and lights a gold ring round it (HudFocus.cs).</summary>
        public bool FocusShown => Island && Step == IslandFocus && Part == 1 && Line != null;

        internal float T, Shown, LastCharge, BurstAt = -99, TowerHitAt = -99, GuardDir = 1, KnutDownAt = -1, BlinkAt = -1, PressAt = -99, HookAt = -1, WaveAt = -99;
        internal float Hid, HelpAt = -1, OnYouT, TowerSayT;
        internal int Gained, TowerHitCount, Part;
        internal bool Burst, PressAimed, Hooked, Pulled, Helpers;
        // the drag lesson: where Fenn stands for it and the corner he walks round on his way out, whether he stands there, how long
        // the drag has been asked for, the taps it got instead, whether an aimed bolt missed him, and whether the game had to show it
        internal float HiderX, HiderY, HiderViaX, HiderViaY, DragT;
        internal int DragTaps;
        internal bool HiderVia, HiderOut, DragMissed, DragShown;

        // ---------- the training island (the first half, Tyra; TutorialIsland.cs). Unused in the arena half but where noted. ----------
        public const int IslandWalk = 0, IslandMinion = 1, IslandGrass = 2, IslandFocus = 3, IslandTarget = 4, IslandPad = 5;
        /// <summary>This is the island half: Step is one of the Island* steps, Done means the boost pad has thrown Tyra to the
        /// top of her jump, so the game cuts to the arena half (a new Match with Tutorial and without Island), where she lands.</summary>
        public bool Island;
        /// <summary>Both halves: the line with the words to highlight between '|' marks (Line is the same text without them); '\n'
        /// breaks lines. Tip: highlight in the calm colour (teal) instead of red. Speaker: the guide's name on the card ("TYRA",
        /// beside her portrait), null = nobody.</summary>
        public string LineMarked, Speaker;
        public bool LineTip;
        /// <summary>The camera frames the whole island at (CamX, CamY) until the bridge opens; then it follows Tyra.</summary>
        public bool CamHold;
        public float CamX, CamY;
        /// <summary>Both halves: a pulsing gold ring on the ground at (RingX, RingY), with a bouncing arrow over it when ArrowOnRing
        /// (the arena: where to wait outside the tower's reach).</summary>
        public bool RingOn, ArrowOnRing;
        public float RingX, RingY;
        /// <summary>A bouncing arrow over this unit (the minion to take down, the leader to single out); null = none.</summary>
        public Unit PointAt;
        /// <summary>The bridge is open (shown) from the last step on.</summary>
        public bool BridgeOpen;
        /// <summary>The boost pad, shown at its tile centre once the bridge opens.</summary>
        public bool PadOn;
        public float PadX, PadY;
        /// <summary>The pad has thrown Tyra: the island half's last JumpTime s, in which she does nothing else (St.Casting).
        /// Rickard 2026-11-16: "nu fadear bara bilden när man går på jump pad, jag vill att man ska hoppa och att det klipps
        /// till att man går in i nästa scen": no fade any more (it hid the whole flight); the throw is seen, and at its top the
        /// game cuts to the arena half, which begins with her coming down.</summary>
        public bool Jumping;
        public const float JumpTime = 0.55f;
        /// <summary>Both halves: how high the pad's throw has Tyra now, 0..1 of its full height. The island: up from the pad,
        /// fast at first and slowing to the top (1 when the half is Done). The arena: 1 at its start, slow at first and fast
        /// into the ground, 0 from LandTime on. One throw's arc, cut in two at its top. PAir is the tick before, for the picture
        /// between ticks. Only the picture lifts her (MatchViews, as Unit.JumpLift for the arena's pads): the rules know no heights.</summary>
        public float Air, PAir;
        /// <summary>The leader of the three (taking it down takes down the other two).</summary>
        public Unit Leader;

        internal float Pause, Leave;
        internal readonly System.Collections.Generic.List<Unit> Minions = new System.Collections.Generic.List<Unit>(8);
    }

    public sealed partial class Match
    {
        // positions on our map (the enemy tower at (7.5, 5.5), the lane straight down x = 7.5)
        const float TutStartX = 7.5f, TutStartY = 19f;
        // Knut walks calmly side to side 2 tiles in front of the tower, right of the lane, until he joins the fight
        const float TutGuardY = 7.5f, TutGuardX0 = 9.0f, TutGuardX1 = 10.3f, TutGuardPace = 0.5f;
        const float TutAfter = 0.7f;      // a pause after a lesson is done, before the next line (the card praises it meanwhile)
        const float TutRefillAfter = 1.2f; // the charge refills this long after the last press until a bolt lands where the lesson needs it
        // the enemy wave: it comes from just in front of the enemy tower, a new pair when none is left before the tap is learned
        const float TutWaveY = 9.5f, TutWaveGap = 1.5f;
        // once the tapped bolt has landed, what is left of the wave falls to one hit each; after this long the rest goes anyway,
        // so Knut always comes down an empty lane (his lesson's special must not pick a minion instead of him)
        const float TutClearMax = 3f;
        // Knut fights for real (long enough that Tyra feels it and has something to heal); Tyra never drops below 40 %
        const float TutKnutHp = 380f, TutKnutDmg = 1.6f, TutKnutPace = 0.85f, TutAfterFight = 0.8f, TutPlayerFloor = 0.4f;
        // nor does Knut drop below 40 % before the fight moment: his hook and the BLINK come first (full review 2026-11-13 nr 111);
        // in the fight moment only Tyra's special finishes him, her other hits stop at 15 %
        const float TutKnutFloor = 0.4f, TutKnutLast = 0.15f;
        const float TutHookAfter = 0.6f;  // Knut is on his way at the earliest this long after the aimed bolt took Fenn
        // Knut walks down the lane and throws his anchor from this far (it flies 7), or after TutHookWait s at the latest
        const float TutHookFrom = 4.2f, TutHookWait = 4f;
        // The drag lesson. Fenn waits hidden deep in the left side tuft (rows 11-13; seen only from Rules.GrassSight tiles), walks
        // out round the wall's end (TutHiderVia) and stands in front of it, up the lane from where the wave was fought: from the
        // lane's middle below row 14 that is outside Tyra's reach (4.4 + his 0.4), where a tap has nobody to pick and only the
        // drag gets there. With Tyra on the map's right side he comes out of the right tuft, mirrored, so he is on her screen
        // (11 tiles across). Her other hits never take him below TutHiderFloor; her own aimed bolt takes all of him.
        const float TutHideX = 1.3f, TutHideY = 12.3f, TutHiderViaX = 4.5f, TutHiderViaY = 11.5f, TutHiderX = 4.7f, TutHiderY = 10f;
        const float TutHiderRightFrom = 8.9f, TutHiderPace = 1f, TutHiderFloor = 0.5f;
        // his walk out takes about 2 s; held up (Tyra herself in his way), he counts as out where he stands after this long
        const float TutHiderWalkMax = 3.5f;
        // the line turns to how a drag is made after this long with no aimed bolt (or at the first tap), and after TutDragMercy s
        // of asking the game makes the shot itself, so that nobody is left standing at this lesson (new players stalled at the old
        // drag step, 2026-11-01); that lesson then counts as done without a word of praise
        const float TutDragSlow = 10f, TutDragMercy = 30f;
        /// <summary>Where the guided match's Knut stops walking at Tyra and stands to swing: his reach, a little inside.</summary>
        static float TutKnutStop(Unit k, Unit p) => (k.Range + p.R) * 0.9f;
        // after Knut: no heal moment when Tyra is this whole; else it lasts until she is back at TutHealedEnough (at least
        // TutHealMin s, so a tick or two of the real regen show), at most TutHealMax s
        const float TutHealed = 0.95f, TutHealedEnough = 0.8f, TutHealMin = 2.5f, TutHealMax = 5.5f;
        // the tower moment: her minions take 40 % of the tower's shots (two of them last about 20 s in its ring); a new pair comes
        // TutHelpWait s after the last one fell, from down the lane; the gold ring to wait in lies TutWaitOut tiles outside the
        // tower's reach; a line stays TutTowerHold s before the next takes its place, the first one TutTowerIntro s; a player who
        // stays under fire TutTowerMercy s with her minions in the ring too is let go
        const float TutHelperTaken = 0.4f, TutHelpWait = 1.5f, TutWaitOut = 1.1f, TutTowerHold = 0.3f, TutTowerIntro = 2.5f, TutTowerMercy = 10f;

        // every line fits two rows of the guide card, which breaks it at a sentence's end when both rows fit and else where the
        // rows are most even (Hud.LineText; measured with the card's font, bam-work/tutorial/README.md)
        const string LineMove = "Follow your |minions|! Every hit |charges| your SPECIAL.",
            LineSpecial = "|SPECIAL| is ready: |tap| it!", LineSpecialNear = "|SPECIAL| is ready! Get an enemy in your white ring.",
            LineDrag = "|Fenn| was hiding in the grass! |Drag| SPECIAL to aim at him.",
            LineDragHow = "Press and |hold| SPECIAL. |Slide| toward Fenn and let go!",
            LineDragMiss = "Missed! Slide again. Let go when the |line| is on Fenn.",
            LineKnut = "Now |Knut| is coming! Watch out for his hook.",
            LineBlink = "He pulled you in! Steer away and tap |BLINK| to dash.", LineBlinkMissed = "Steer away and tap |BLINK| to dash!",
            LineFight = "Now finish him with your |SPECIAL|!",
            LineHeal = "Knut is down! You |heal| when you stop fighting.",
            LineTowerWait = "Wait here! Towers shoot the |first| enemy in their ring.",
            LineTowerGo = "It shoots your |minions| now: |attack the tower|!",
            LineTowerYou = "It's shooting |you|! Step back out of the red ring.";

        void StartTutorial()
        {
            NoStars = true;
            var tu = Tut = new Tutorial { Step = Tutorial.StepMove, Tower = Towers[Teams.Enemy], Lesson = Tutorial.IslandLessons, Lessons = Tutorial.AllLessons };
            State = MatchState.Play; Time = 0;
            // the towers do not shoot until the tower moment (their health and damage are untouched), and until then nobody can aim
            // at the enemy tower: Tyra's shots there counted for nothing and her minions stood hitting it through Knut's lesson
            for (int t = 0; t < 2; t++) AddStatus(Towers[t], St.Disarm, 1e9f);
            AddStatus(tu.Tower, St.Untargetable, 1e9f);
            // and the player's own tower cannot fall: a new player who wanders off (the side grass) while the first enemy waves
            // walk down must not lose the guided match and get it again at every start (bug review 2026-11-02, nr 1)
            Towers[Teams.Player].TutUnkillable = true;

            // Tyra, made stronger as the old onboarding match did (FirstOnboardingMatch)
            var p = NewHero(HeroId.Tyra, Teams.Player, TutStartX, TutStartY);
            p.IsPlayer = true; p.DmgMult = 2; p.TakenMult = 0.5f; p.Charge = 0; Player = p;
            tu.Speaker = TutSpeaker(p);
            // she comes down out of the air, from the top of the island's boost pad's throw, and can do nothing until she is down
            // (UpdateTutorial lifts the status the tick she lands; its own length is only a net)
            tu.Air = tu.PAir = 1; AddStatus(p, St.Casting, Tutorial.LandTime + 1);

            // two of her minions land with her and walk the lane; the first enemy wave is already on its way
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX - 0.8f, TutStartY - 0.6f);
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX + 0.8f, TutStartY - 0.6f);
            TutWave(true);

            // Knut: no bot, no attacks, never a target until his moment
            var k = NewHero(HeroId.Knut, Teams.Enemy, TutGuardX0, TutGuardY);
            k.Dummy = true;
            AddStatus(k, St.Disarm, 1e9f); AddStatus(k, St.Untargetable, 1e9f);
            tu.Guard = k;
            // Fenn: hidden in the side grass until the drag lesson, the same way (no bot, no attacks, never a target before it)
            var f = NewHero(HeroId.Fenn, Teams.Enemy, TutHideX, TutHideY);
            f.Dummy = true; f.Facing = 0;
            AddStatus(f, St.Disarm, 1e9f); AddStatus(f, St.Untargetable, 1e9f);
            tu.Hider = f;
            RefreshTutorial();
        }

        // a lane minion with its next waypoint ahead of it (the lane's points are at y 28.5, 17.5 and 6.5)
        Unit TutMinion(MinionKind kind, int team, float x, float y)
        {
            var m = NewMinion(kind, team, x, y);
            TutLaneOn(m);
            return m;
        }

        void TutLaneOn(Unit m)
        {
            if (m.Team == Teams.Player) m.Wp = m.Y > Map.LaneY[1] + 0.8f ? 1 : 2;
            else m.Wp = m.Y < Map.LaneY[1] - 0.8f ? 1 : 2;
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

        // the tapped bolt has landed: what is left of the wave falls to the next hit it takes (all = at once), so the lane is
        // empty when Knut comes
        void TutEndWave(bool all)
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || !u.IsMinionLike || u.Team != Teams.Enemy) continue;
                if (all) Kill(u, Player); else if (u.Hp > 1) u.Hp = 1;
            }
        }

        string TutLineNow()
        {
            var tu = Tut; var p = Player; var k = tu.Guard;
            if (State != MatchState.Play || tu.Done) return null;
            bool knut = k != null && k.Alive;
            switch (tu.Step)
            {
                // one line from the landing until the special is ready: the walk and the first fight are one lesson
                case Tutorial.StepMove: return tu.Opening ? null : LineMove;
                case Tutorial.StepAuto: return LineMove;
                // one tap: "tap it" only while the tap has a target, never over a grey button (a tap there only shakes the button);
                // with nobody in reach the line says so and the gold trail leads to the minions (bug review 2026-11-02, nr 13)
                case Tutorial.StepSpecial: return tu.Burst || !p.SpecialReady ? null : !OneTap || TapHasTarget(p) ? LineSpecial : LineSpecialNear;
                // the drag lesson, from the moment Fenn is seen: what to do, then how a drag is made (after a tap, or a while with
                // no aimed bolt), or that the last bolt missed him. Then Knut is on his way down (the line stays through his
                // throw, until BLINK's own line)
                case Tutorial.StepThrow:
                    if (tu.Burst) return knut && Time - tu.BurstAt >= TutAfter ? LineKnut : null; // the card praises the shot first
                    if (!TutHiderSeen()) return null;
                    return tu.DragMissed ? LineDragMiss : tu.DragTaps > 0 || tu.DragT >= TutDragSlow ? LineDragHow : LineDrag;
                // "He pulled you in!" only when the anchor really caught Tyra; it can miss, or Knut may not throw at all
                // (bug review 2026-11-02, nr 19)
                case Tutorial.StepBlink:
                    if (!tu.Hooked) return knut ? LineKnut : null;
                    return p.SpellAt <= Time ? (tu.Pulled ? LineBlink : LineBlinkMissed) : null;
                // the goal of the moment, also while Knut is a step out of reach (the trail leads to him; the HUD's arrow on SPECIAL
                // waits for a target)
                case Tutorial.StepFight: return knut && p.SpecialReady ? LineFight : null;
                case Tutorial.StepHeal: return LineHeal;
                default: return tu.TowerSay == Tutorial.TowerYou ? LineTowerYou : tu.TowerSay == Tutorial.TowerGo ? LineTowerGo : LineTowerWait;
            }
        }

        /// <summary>The drag lesson runs and Fenn is to be seen: out on his spot, or on his way to it past the grass's edge.</summary>
        bool TutHiderSeen()
        {
            var tu = Tut; var f = tu.Hider;
            if (tu.Step != Tutorial.StepThrow || tu.Burst || f == null || !f.Alive || State != MatchState.Play) return false;
            return tu.HiderOut || IsVisibleTo(f, Teams.Player);
        }

        void RefreshTutorial()
        {
            var tu = Tut;
            tu.Opening = tu.Step == Tutorial.StepMove && Time < Tutorial.OpeningTime;
            // the drag lesson: the arrow is over Fenn from the moment he is seen, and the drag is asked for once he stands there
            // and the special is ready (not while a bolt that missed him is on its way and the charge comes back)
            bool seen = TutHiderSeen();
            tu.PointAt = seen ? tu.Hider : null;
            tu.DragAsked = seen && tu.HiderOut && Player.Alive && Player.SpecialReady;
            TutSay(TutLineNow());
            // the tower moment's gold ring: where to wait for the minions, and where to step back to when the tower shoots Tyra
            bool wait = tu.Step == Tutorial.StepTower && !tu.Done && State == MatchState.Play && tu.TowerSay != Tutorial.TowerGo;
            tu.RingOn = tu.ArrowOnRing = wait;
            tu.RingX = tu.Tower.X; tu.RingY = tu.Tower.Y + tu.Tower.Range + TutWaitOut;
        }

        void TutNext(int step) { var tu = Tut; tu.Step = step; tu.T = 0; tu.Shown = 0; tu.Burst = false; }

        void UpdateTutorial(float dt)
        {
            if (Tut.Island) { UpdateIsland(dt); return; }
            var tu = Tut; var p = Player;
            tu.T += dt;
            // the landing: the second half of the pad's throw, slow off its top and fast into the ground; down, she can act
            tu.PAir = tu.Air;
            if (tu.Air > 0)
            {
                float down = Time / Tutorial.LandTime;
                tu.Air = down >= 1 ? 0 : 1 - down * down;
                if (tu.Air <= 0) p.RemoveAll(St.Casting);
            }
            RefreshTutorial();
            // the special pressed this tick (PlayerAct casts it later in this tick): a drag carries an angle, a tap does not
            if (specialBuf > 0)
            {
                bool aimed = !float.IsNaN(bufAng);
                // the drag lesson takes no tap once Fenn stands there: it fires nothing (a tap with Fenn in the ring would hit him
                // and teach the tap again), and a tap when the drag is asked for turns the line to how a drag is made
                if (!aimed && tu.Step == Tutorial.StepThrow && !tu.Burst && tu.HiderOut) { seatSpecBuf[0] = 0; if (tu.DragAsked) tu.DragTaps++; }
                else { tu.PressAt = Time; tu.PressAimed = aimed; }
            }
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
            // Knut cannot fall before his lessons are done: Tyra's double damage could take his 380 before the hook, and then the BLINK
            // line stood over an empty lane and the fight moment never came (full review 2026-11-13 nr 111). In the fight moment he
            // falls to her special alone (TutDamage), so the lesson "finish him with your SPECIAL" is the player's own doing.
            // TutUnkillable keeps one big hit from finishing him between two ticks.
            if (k != null && k.Alive)
            {
                k.TutUnkillable = true;
                float floor = tu.Step < Tutorial.StepFight ? TutKnutFloor : TutKnutLast;
                if (k.Hp < k.MaxHp * floor) k.Hp = k.MaxHp * floor;
            }
            // nor can Fenn fall to anything but her own aimed bolt: where he stands inside her ring her arrows find him by
            // themselves, and they stop at half his health (TutDamage; this floor is for what does not pass it). Down, he stays down.
            var f = tu.Hider;
            if (f != null && f.Alive)
            {
                f.TutUnkillable = true;
                if (f.Hp < f.MaxHp * TutHiderFloor) f.Hp = f.MaxHp * TutHiderFloor;
            }
            else if (f != null) f.RespawnT = 1e9f;
            // Knut keeps to his walk by the tower until the drag lesson is done
            if (k != null && k.Alive && (tu.Step < Tutorial.StepThrow || tu.Step == Tutorial.StepThrow && !tu.Burst))
            {
                if (k.X >= TutGuardX1) tu.GuardDir = -1; else if (k.X <= TutGuardX0) tu.GuardDir = 1;
                MoveUnit(k, tu.GuardDir * TutGuardPace, 0, dt);
                k.Y = TutGuardY;
            }
            else if (k != null && k.Alive && !k.Has(St.Casting) && !k.Has(St.Stun))
            {
                // Knut's simple fight: walk at Tyra until she is in his reach, then stand and swing (the real auto attack)
                float dx = p.X - k.X, dy = p.Y - k.Y, dist = SimMath.Hypot(dx, dy);
                if (p.Alive && dist > TutKnutStop(k, p)) MoveUnit(k, dx / dist * TutKnutPace, dy / dist * TutKnutPace, dt);
                else if (dist > 0.01f) k.Facing = SimMath.Atan2(dy, dx);
            }
            else if (k != null && !k.Alive) k.RespawnT = 1e9f; // Knut stays down

            if (tu.Line != null) tu.Shown += dt;
            // the gold trail's landmark: the wave until the tap is learned, Knut when he is to be finished, the tower when it is
            // busy with the minions (the ring leads the rest of the tower moment, RefreshTutorial). None to Fenn: the trail says
            // "walk here", and his lesson is the shot from where she stands (the arrow is over him, PointAt)
            tu.Goal = tu.Step <= Tutorial.StepSpecial ? TutNearestFoeMinion(p)
                : tu.Step == Tutorial.StepFight ? (k != null && k.Alive ? k : null)
                : tu.Step == Tutorial.StepTower && tu.TowerSay == Tutorial.TowerGo ? tu.Tower : null;

            switch (tu.Step)
            {
                case Tutorial.StepMove:
                    // the fight has begun: an enemy in Tyra's reach, or her first hit. No ring to walk to any more: any 2.5 tiles
                    // of walking, any way, used to end this moment
                    if (!tu.Opening && (tu.Gained > 0 || FocusTargets(p) > 0)) TutNext(Tutorial.StepAuto);
                    break;
                case Tutorial.StepAuto:
                    if (p.SpecialReady) { TutLessonDone(); TutNext(Tutorial.StepSpecial); }
                    else if (!TutEnemyMinions() && Time - tu.WaveAt > 2f) TutWave(false); // more to hit until the special is charged
                    break;
                case Tutorial.StepSpecial:
                    if (!tu.Burst)
                    {
                        if (!TutEnemyMinions() && Time - tu.WaveAt > 2f) TutWave(false); // something for the bolt
                        // a bolt that hit nothing (its minion died in the wind-up, it flew on into a wall): the charge comes back a
                        // moment after the press instead of ~11 s of autos with no line (bug review 2026-11-02, nr 3)
                        if (!p.SpecialReady && Time - tu.PressAt >= TutRefillAfter) p.Charge = p.HDef.MaxCharge;
                    }
                    else
                    {
                        TutEndWave(false);
                        if (Time - tu.BurstAt >= TutAfter && (!TutEnemyMinions() || Time - tu.BurstAt >= TutClearMax))
                        {
                            // the lane is empty: Fenn steps out of the side grass for the drag lesson (the one-tap game of
                            // 2026-11-01 had none: the old aimed bolt at Knut, far up by the tower with no sign of how a drag is
                            // made, was where new players stalled, so it was passed over; Rickard 2026-11-16 asked for it back,
                            // on Fenn)
                            TutEndWave(true);
                            TutNext(Tutorial.StepThrow);
                            TutHoldMinions();
                            // only where a drag aims at all (Rules.DragAimSpecial; the old tap + drag game always did): else the
                            // lesson counts as done by itself and Knut comes at once, as from 2026-11-01 until this lesson came
                            if (!OneTap || DragAim) TutHiderStart();
                            else { tu.Burst = true; tu.BurstAt = Time; if (tu.Lesson < tu.Lessons) tu.Lesson++; }
                        }
                    }
                    break;
                case Tutorial.StepThrow:
                    if (!tu.Burst) TutDragStep(dt);
                    else
                    {
                        // Fenn is down: Knut becomes a real enemy and walks down until his anchor has a short, clear way to Tyra
                        // (it used to wait until he stood beside her, about 6 s with no line: tutorial review 2026-11-15)
                        if (k != null && k.Alive && k.Has(St.Untargetable)) StartFight();
                        if (Time - tu.BurstAt >= TutHookAfter && (k == null || !k.Alive || SimMath.Dist(k, p) <= TutHookFrom || Time - tu.BurstAt > TutHookWait))
                        {
                            TutNext(Tutorial.StepBlink);
                            tu.SpellOn = true; p.Spell = SpellId.Blink; p.SpellAt = Time; p.SpellChanEnd = -1;
                        }
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
                        if (tu.HookAt >= 0 && !tu.Pulled) // did the anchor catch Tyra? (the line says "He pulled you in!" only then)
                            for (int i = 0; i < Projs.Count; i++)
                                if (Projs[i].Mode == ProjMode.Hook && Projs[i].Src == k && Projs[i].Caught == p) { tu.Pulled = true; break; }
                        // pulled or missed: on with the lesson, once Tyra can act again (the catch's stun outlasts a long haul)
                        if (tu.HookAt >= 0 && Time - tu.HookAt >= KnutS.CastTime + 0.4f && !p.Has(St.Stun)) tu.Hooked = true;
                        if (tu.HookAt < 0 && tu.T > 3f) tu.Hooked = true; // could not throw (too far, a wall): teach the blink anyway
                    }
                    else if (k == null || !k.Alive) tu.Hooked = true;
                    if (tu.Hooked && p.SpellAt > Time)
                    {
                        if (tu.BlinkAt < 0) { tu.BlinkAt = Time; TutLessonDone(); }
                        if (Time - tu.BlinkAt >= TutAfter) { TutNext(Tutorial.StepFight); p.Charge = p.HDef.MaxCharge; } // the special that finishes him
                    }
                    break;
                case Tutorial.StepFight:
                    if (k == null || !k.Alive)
                    {
                        if (tu.KnutDownAt < 0) { tu.KnutDownAt = Time; TutLessonDone(); }
                        if (Time - tu.KnutDownAt >= TutAfterFight) TutNext(p.Hp >= p.MaxHp * TutHealed ? Tutorial.StepTower : Tutorial.StepHeal);
                    }
                    // a bolt that missed him: the special is back a moment after the press
                    else if (!p.SpecialReady && Time - tu.PressAt >= TutRefillAfter) p.Charge = p.HDef.MaxCharge;
                    break;
                case Tutorial.StepHeal:
                    if (p.Hp >= p.MaxHp * TutHealedEnough && tu.T >= TutHealMin || tu.T >= TutHealMax) TutNext(Tutorial.StepTower);
                    break;
            }
            if (tu.Step == Tutorial.StepTower && !tu.Done) TutTowerStep(dt);
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
            TutHoldMinions();
        }

        // Tyra's minions hold where they stand from the drag lesson until the push on the tower (they still swing at what comes
        // close): left to walk, they stood at the enemy tower long before her, and "let your minions go first" had nothing to show
        void TutHoldMinions()
        {
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.Kind == UnitKind.Minion && u.Team == Teams.Player && !u.TutStatic) { u.TutStatic = true; u.SpawnX = u.X; u.SpawnY = u.Y; }
            }
        }

        // The drag lesson begins: where Fenn will stand, and a drag aims the special from now on. He comes out of the left tuft,
        // or, with Tyra far to the right (he would be off her screen), out of the right one: he waits there unseen, so the move
        // across is never seen either.
        void TutHiderStart()
        {
            var tu = Tut; var f = tu.Hider; var p = Player;
            tu.DragOn = true;
            if (f == null || !f.Alive) return;
            bool right = p.X > TutHiderRightFrom && !IsVisibleTo(f, Teams.Player);
            if (right) { f.X = f.PX = Map.W - f.X; f.Facing = SimMath.PI; }
            bool onRight = f.X > Map.W * 0.5f;
            tu.HiderViaX = onRight ? Map.W - TutHiderViaX : TutHiderViaX; tu.HiderViaY = TutHiderViaY;
            tu.HiderX = onRight ? Map.W - TutHiderX : TutHiderX; tu.HiderY = TutHiderY;
        }

        /// <summary>The drag lesson, every tick until Tyra's aimed bolt has hit Fenn: he walks out of the grass to his spot, becomes
        /// a target there and her special is full; it comes back a moment after every bolt that missed him.</summary>
        void TutDragStep(float dt)
        {
            var tu = Tut; var p = Player; var f = tu.Hider;
            if (!tu.DragOn) TutHiderStart(); // the step was entered some other way than from the tap lesson (a test)
            if (f == null || !f.Alive) { tu.Burst = true; tu.BurstAt = Time; if (tu.Lesson < tu.Lessons) tu.Lesson++; return; } // nobody to learn on: on to Knut
            if (!tu.HiderOut)
            {
                // round the wall's end, then up to his spot
                float gx = tu.HiderVia ? tu.HiderX : tu.HiderViaX, gy = tu.HiderVia ? tu.HiderY : tu.HiderViaY;
                float dx = gx - f.X, dy = gy - f.Y, d = SimMath.Hypot(dx, dy);
                if (d > 0.15f && tu.T < TutHiderWalkMax) { MoveUnit(f, dx / d * TutHiderPace, dy / d * TutHiderPace, dt); return; }
                if (!tu.HiderVia) { tu.HiderVia = true; return; }
                // he stands there (or was held up: then where he got to): a target now, and her special is ready
                tu.HiderOut = true;
                f.RemoveAll(St.Untargetable);
                p.Charge = p.HDef.MaxCharge;
                return;
            }
            float fx = p.X - f.X, fy = p.Y - f.Y;
            if (fx * fx + fy * fy > 1e-4f) f.Facing = SimMath.Atan2(fy, fx); // he watches her
            // a bolt that missed him: the special is back a moment after the press, so a bolt in the air lands first
            if (!p.SpecialReady && Time - tu.PressAt >= TutRefillAfter)
            {
                if (tu.DragShown)
                {
                    // the game's own shot found no way to him (it cannot miss today: her bolt flies through everything to the
                    // island's edge, TyraS.BoltPierces): the lesson ends all the same, nobody is left standing at it
                    f.TutUnkillable = false; Kill(f, p);
                    tu.Burst = true; tu.BurstAt = Time;
                    if (tu.Lesson < tu.Lessons) tu.Lesson++;
                    return;
                }
                p.Charge = p.HDef.MaxCharge; tu.DragMissed = true;
            }
            if (!tu.DragAsked) return;
            tu.DragT += dt;
            if (tu.DragT < TutDragMercy || tu.DragShown) return;
            // nobody is left standing at this lesson: the game makes the shot itself, a drag at Fenn, and the match goes on
            float ang = SimMath.Atan2(f.Y - p.Y, f.X - p.X);
            if (OneTap ? TryAimSpecial(p, ang, 1) : TryCastSpecial(p, ang, 1)) { tu.DragShown = true; tu.PressAt = Time; tu.PressAimed = true; }
        }

        /// <summary>The tower moment (every tick of it): the enemy tower shoots for real now, at the first enemy inside its ring and
        /// at a minion before a hero (TowerPick). Tyra's minions walk in first and take its shots; only then, and only while it does
        /// not aim at her, can Tyra aim at it (it is untargetable otherwise), so the six hits that break it are all made the right
        /// way. Under fire she is told to step back out; she cannot drop below 40 % (UpdateTutorial).</summary>
        void TutTowerStep(float dt)
        {
            var tu = Tut; var p = Player; var tw = tu.Tower;
            int own = 0; bool inRing = false;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Kind != UnitKind.Minion || u.Team != Teams.Player) continue;
                own++;
                if (u.TutStatic) { u.TutStatic = false; TutLaneOn(u); } // the ones that held through Knut's lesson walk on
                u.TakenMult = TutHelperTaken;
                if (SimMath.Dist(u, tw) <= tw.Range + u.R) inRing = true;
            }
            // new minions come up the lane beside Tyra's row, never nearer the tower than 2.5 tiles outside its ring
            float hy = Math.Max(p.Y + 0.8f, tw.Y + tw.Range + 2.5f);
            if (!tu.Helpers)
            {
                // the push begins: Tyra has two minions with her (new ones for those that fell on the way)
                tu.Helpers = true;
                for (int i = own; i < 2; i++) TutMinion(MinionKind.Melee, Teams.Player, TutStartX + (i == 0 ? -0.6f : 0.6f), hy);
            }
            else if (own > 0) tu.HelpAt = -1;
            else if (tu.HelpAt < 0) tu.HelpAt = Time;
            else if (Time - tu.HelpAt >= TutHelpWait)
            {
                // the tower took them all: a new pair
                TutMinion(MinionKind.Melee, Teams.Player, TutStartX - 0.6f, hy);
                TutMinion(MinionKind.Melee, Teams.Player, TutStartX + 0.6f, hy);
                tu.HelpAt = -1;
            }
            // the tower shoots from now on, once its first line has had time to be read
            if (tu.T >= TutTowerIntro) tw.RemoveAll(St.Disarm);

            bool onYou = tw.Target == p && p.Alive && SimMath.Dist(tw, p) <= tw.Range + p.R;
            tu.OnYouT = onYou ? tu.OnYouT + dt : 0;
            if (onYou && inRing && tu.OnYouT >= TutTowerMercy) { tw.Target = null; onYou = false; tu.OnYouT = 0; } // it takes a minion next (TowerPick)
            bool open = inRing && !onYou;
            if (open) tw.RemoveAll(St.Untargetable);
            else if (!tw.Has(St.Untargetable)) AddStatus(tw, St.Untargetable, 1e9f);

            // the line follows what the tower does, a moment late so it never flickers; the first one stays long enough to be read
            int say = onYou ? Tutorial.TowerYou : inRing && tu.T >= TutTowerIntro ? Tutorial.TowerGo : Tutorial.TowerWait;
            if (say == tu.TowerSay) tu.TowerSayT = 0;
            else { tu.TowerSayT += dt; if (tu.TowerSayT >= TutTowerHold) { tu.TowerSay = say; tu.TowerSayT = 0; } }
        }

        /// <summary>The guided match's BLINK with no direction (a tap with the joystick at rest): away from Knut, not the way Tyra
        /// faces, which is at him while she shoots (tutorial review 2026-11-15: "tap BLINK to dash away" dashed her into him).</summary>
        float TutBlinkAngle(Unit h)
        {
            var k = Tut.Guard;
            if (Tut.Island || h != Player || k == null || !k.Alive) return h.Facing;
            float dx = h.X - k.X, dy = h.Y - k.Y;
            return dx * dx + dy * dy > 1e-6f ? SimMath.Atan2(dy, dx) : h.Facing;
        }

        /// <summary>Every hit in the arena half (DealDamage, before the target takes it): the lessons that need a special to land.</summary>
        void TutSawHit(Unit src, Unit tgt, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island || tu.Burst || src != p || kind != DmgKind.Special || tgt.Team == p.Team) return;
            // the tap lesson: any bolt that hits; the drag lesson: only a bolt aimed by hand, and only on Fenn
            bool tap = tu.Step == Tutorial.StepSpecial;
            if (!tap && !(tu.Step == Tutorial.StepThrow && tu.PressAimed && tgt == tu.Hider)) return;
            tu.Burst = true; tu.BurstAt = Time;
            // a lesson done, with its word of praise; the shot the game had to make itself counts without one
            if (tap || !tu.DragShown) TutLessonDone(); else if (tu.Lesson < tu.Lessons) tu.Lesson++;
        }

        /// <summary>DealDamage, for the guided match's enemy tower, Knut and Fenn. The tower: only Tyra's own auto attacks in the last
        /// moment hurt it, each taking the same share of what is left, so the TowerHits-th hit always breaks it (regen between hits
        /// changes nothing); everything else (her minions' swings too) deals 0. Knut: in the fight moment Tyra's special takes all
        /// that is left of him; every other hit is the real one (and stops at his floor, UpdateTutorial). Fenn: in the drag lesson
        /// her own aimed bolt takes all of him (TutSawHit has just counted it); every other hit takes him no lower than half.</summary>
        float TutDamage(Unit src, Unit tgt, float amount, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island) return amount;
            if (tgt == tu.Hider)
            {
                // every other hit stops at half his health (her arrows find him by themselves inside her ring): there it takes nothing
                if (tu.Step != Tutorial.StepThrow || kind != DmgKind.Special || src == null || src != p || !tu.PressAimed)
                    return Math.Min(amount, Math.Max(0, tgt.Hp - tgt.MaxHp * TutHiderFloor));
                tgt.TutUnkillable = false;
                return Math.Max(amount, tgt.Hp);
            }
            if (tgt == tu.Guard)
            {
                if (tu.Step != Tutorial.StepFight || kind != DmgKind.Special || src == null || src != p) return amount;
                tgt.TutUnkillable = false;
                if (tu.KnutDownAt < 0) { tu.KnutDownAt = Time; TutLessonDone(); } // the lesson is done as the bolt lands
                return Math.Max(amount, tgt.Hp);
            }
            if (tgt != tu.Tower) return amount;
            if (tu.Step != Tutorial.StepTower || kind != DmgKind.Auto || src == null || src != p) return 0;
            tu.TowerHitCount++; tu.TowerHitAt = Time;
            if (tu.TowerHitCount >= Tutorial.TowerHits)
            {
                // the tower falls and the match ends (team 0 wins): the last lesson is done
                tu.Done = true; tu.DoneAt = Time; TutSay(null); tu.RingOn = tu.ArrowOnRing = false; TutLessonDone();
                return tgt.Hp;
            }
            return tgt.Hp / (Tutorial.TowerHits - tu.TowerHitCount + 1);
        }
    }
}
