using System;

namespace BAM.Sim
{
    /// <summary>
    /// The guided first match's arena half: a real small first battle with Tyra, as on the island (Rickard 2026-10-29: "andra delen
    /// är inte bra"; he chose a real fight over the old walk past target boards). The boost pad throws Tyra onto the lane of the real
    /// arena, two of her minions walk with her and a small enemy wave comes down. Five lessons, each done by the player, one short
    /// line at a time on the same guide card as the island's (tutorial review 2026-11-15: the lines did not match what happened,
    /// Knut, the heal and the tower asked nothing of the player and the tower never shot):
    /// 1 follow the minions into the wave (Tyra attacks by herself; her hits charge the special), 2 TAP the special when it is
    /// ready (an auto-aimed bolt), 3 Knut comes down the lane, throws his anchor and pulls Tyra in: BLINK gets her away (with the
    /// joystick at rest it dashes away from him), 4 the special is full again: it finishes Knut (he cannot fall to anything else);
    /// a moment of the real regen follows, 5 the enemy tower shoots from now on, at the first enemy inside its ring: her minions
    /// go in first, and only while it is busy with them can Tyra hurt it; it falls to exactly TowerHits of her auto attacks.
    /// The old drag game (Rules.OneTapSpecial off) keeps its aimed bolt at Knut between 2 and 3. No failure: Tyra never drops
    /// below 40 %. The match screen reads this.
    /// </summary>
    public sealed partial class Tutorial
    {
        public const int StepMove = 0, StepAuto = 1, StepSpecial = 2, StepThrow = 3, StepBlink = 4, StepFight = 5, StepHeal = 6, StepTower = 7;
        /// <summary>The opening: DESTROY THE ENEMY TOWER over Tyra as she lands (no look at the tower first since 2026-10-29).</summary>
        public const float OpenHold = 0f, OpeningTime = 1.8f;
        public const int TowerHits = 6;
        public const float BurstDelay = 0.15f;
        /// <summary>The whole guided match's lessons, one dot each on the guide card: the island's five (walk, attack, grass, Focus,
        /// one target) and the arena's five (charge, special, Blink, Knut, the tower).</summary>
        public const int IslandLessons = 5, ArenaLessons = 5, AllLessons = IslandLessons + ArenaLessons;
        /// <summary>StepTower's line: wait for the minions, go (the tower is busy with them), or it shoots you.</summary>
        public const int TowerWait = 0, TowerGo = 1, TowerYou = 2;

        /// <summary>The moment the player is in.</summary>
        public int Step;
        /// <summary>The one hint to show now, null = none (the plain text; LineMarked has the words to highlight).</summary>
        public string Line;
        /// <summary>The first OpeningTime s: the title over Tyra's landing.</summary>
        public bool Opening;
        /// <summary>Dummy: no practice target any more (null; kept for the screens). Guard: Knut. Goal: what the gold trail leads
        /// to now (the nearest enemy minion, Knut, the tower), null = nothing (or the ring, when RingOn).</summary>
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
        /// <summary>Lessons done so far in the whole guided match, of Lessons (the arena half starts at IslandLessons): the guide
        /// card's dots. LessonAt: when the last one was done (Match.Time of this half), for the card's word of praise.</summary>
        public int Lesson, Lessons;
        public float LessonAt = -99;
        /// <summary>StepTower: which of its three lines is up (TowerWait, TowerGo, TowerYou).</summary>
        public int TowerSay;
        /// <summary>StepBlink: Knut's anchor has done its part and the line asks for BLINK now (the arrow over its button).</summary>
        public bool BlinkAsked => Hooked;
        /// <summary>The island's Focus step shows its point now (all three in reach, the line names FOCUS): the arrow over the dots.</summary>
        public bool FocusShown => Island && Step == IslandFocus && Part == 1 && Line != null;

        internal float T, Shown, LastCharge, BurstAt = -99, TowerHitAt = -99, GuardDir = 1, KnutDownAt = -1, BlinkAt = -1, PressAt = -99, HookAt = -1, WaveAt = -99;
        internal float Hid, HelpAt = -1, OnYouT, TowerSayT;
        internal int Gained, TowerHitCount, Part;
        internal bool Burst, PressAimed, Hooked, Pulled, Helpers;

        // ---------- the training island (the first half, Tyra; TutorialIsland.cs). Unused in the arena half but where noted. ----------
        public const int IslandWalk = 0, IslandMinion = 1, IslandGrass = 2, IslandFocus = 3, IslandTarget = 4, IslandPad = 5;
        /// <summary>This is the island half: Step is one of the Island* steps, Done means the boost pad threw Tyra and the
        /// picture has faded, so the game starts the arena half (a new Match with Tutorial and without Island).</summary>
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
        /// <summary>The pad threw Tyra: she flies (St.Casting) and the picture fades, Fade 0..1.</summary>
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
        const float TutHookAfter = 0.6f;  // old drag game: Knut throws his anchor this long after Tyra's aimed bolt hit him
        // one tap: Knut walks down the lane and throws his anchor from this far (it flies 7), or after TutHookWait s at the latest
        const float TutHookFrom = 4.2f, TutHookWait = 4f;
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
            LineSpecial = "|SPECIAL| is ready: |tap| it!", LineSpecialNear = "|SPECIAL| is ready! Get an enemy in your white ring.", LineThrow = "An enemy hero! |DRAG| SPECIAL to aim at Knut!",
            LineKnut = "An enemy hero: |Knut|! Watch out for his hook.",
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

            // two of her minions land with her and walk the lane; the first enemy wave is already on its way
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX - 0.8f, TutStartY - 0.6f);
            TutMinion(MinionKind.Melee, Teams.Player, TutStartX + 0.8f, TutStartY - 0.6f);
            TutWave(true);

            // Knut: no bot, no attacks, never a target until his moment
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
                // one tap: Knut is on his way down (the line stays through his throw, until BLINK's own line)
                case Tutorial.StepThrow: return !tu.Burst ? LineThrow : OneTap && knut ? LineKnut : null;
                // "He pulled you in!" only when the anchor really caught Tyra; it can miss, or Knut may not throw at all
                // (bug review 2026-11-02, nr 19)
                case Tutorial.StepBlink:
                    if (!tu.Hooked) return OneTap && knut ? LineKnut : null;
                    return p.SpellAt <= Time ? (tu.Pulled ? LineBlink : LineBlinkMissed) : null;
                // the goal of the moment, also while Knut is a step out of reach (the trail leads to him; the HUD's arrow on SPECIAL
                // waits for a target)
                case Tutorial.StepFight: return knut && p.SpecialReady ? LineFight : null;
                case Tutorial.StepHeal: return LineHeal;
                default: return tu.TowerSay == Tutorial.TowerYou ? LineTowerYou : tu.TowerSay == Tutorial.TowerGo ? LineTowerGo : LineTowerWait;
            }
        }

        void RefreshTutorial()
        {
            var tu = Tut;
            tu.Opening = tu.Step == Tutorial.StepMove && Time < Tutorial.OpeningTime;
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
                if (p.Alive && dist > TutKnutStop(k, p)) MoveUnit(k, dx / dist * TutKnutPace, dy / dist * TutKnutPace, dt);
                else if (dist > 0.01f) k.Facing = SimMath.Atan2(dy, dx);
            }
            else if (k != null && !k.Alive) k.RespawnT = 1e9f; // Knut stays down

            if (tu.Line != null) tu.Shown += dt;
            // the gold trail's landmark: the wave until the tap is learned, Knut when he is to be finished, the tower when it is
            // busy with the minions (the ring leads the rest of the tower moment, RefreshTutorial)
            tu.Goal = tu.Step <= Tutorial.StepSpecial ? TutNearestFoeMinion(p)
                : tu.Step == Tutorial.StepThrow && !tu.Burst || tu.Step == Tutorial.StepFight ? (k != null && k.Alive ? k : null)
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
                            TutEndWave(true);
                            TutNext(Tutorial.StepThrow);
                            StartFight();
                            // one-tap special (Rules.OneTapSpecial, 2026-11-01): no drag to teach, so the aimed bolt at Knut (where
                            // new players stalled) counts as done at once and Knut comes on to his hook. The old drag game gets its
                            // special back full for the aimed bolt.
                            if (OneTap) { tu.Burst = true; tu.BurstAt = Time; } else p.Charge = p.HDef.MaxCharge;
                        }
                    }
                    break;
                case Tutorial.StepThrow:
                    // old drag game: the charge comes back full until an aimed bolt hits Knut, a moment after the last press so a
                    // bolt in the air lands first
                    if (!tu.Burst) { if (!p.SpecialReady && Time - tu.PressAt >= TutRefillAfter) p.Charge = p.HDef.MaxCharge; }
                    // one tap: Knut walks down until his anchor has a short, clear way to Tyra (it used to wait until he stood
                    // beside her, about 6 s with no line: tutorial review 2026-11-15)
                    else if (Time - tu.BurstAt >= TutHookAfter && (!OneTap || k == null || !k.Alive || SimMath.Dist(k, p) <= TutHookFrom || tu.T > TutHookWait))
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
            // Tyra's minions hold where they stand until the push on the tower (they still swing at Knut when he comes close):
            // left to walk, they stood at the enemy tower long before her, and "let your minions go first" had nothing to show
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (u.Alive && u.Kind == UnitKind.Minion && u.Team == Teams.Player) { u.TutStatic = true; u.SpawnX = u.X; u.SpawnY = u.Y; }
            }
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
            // the tap lesson: any bolt that hits; the old drag game's aimed bolt: only a bolt Tyra aimed, and only on Knut
            bool tap = tu.Step == Tutorial.StepSpecial;
            if (tap || tu.Step == Tutorial.StepThrow && tu.PressAimed && tgt == tu.Guard) { tu.Burst = true; tu.BurstAt = Time; }
            if (tap) TutLessonDone();
        }

        /// <summary>DealDamage, for the guided match's enemy tower and Knut. The tower: only Tyra's own auto attacks in the last
        /// moment hurt it, each taking the same share of what is left, so the TowerHits-th hit always breaks it (regen between hits
        /// changes nothing); everything else (her minions' swings too) deals 0. Knut: in the fight moment Tyra's special takes all
        /// that is left of him; every other hit is the real one (and stops at his floor, UpdateTutorial).</summary>
        float TutDamage(Unit src, Unit tgt, float amount, DmgKind kind)
        {
            var tu = Tut; var p = Player;
            if (tu.Island) return amount;
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
