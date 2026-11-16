using System;

namespace BAM.Sim
{
    // The training island, the guided match's first half (the old game's SimTutorialIsland.cs, Tutorial 2.0): Tyra stands on a
    // round stone platform and learns one thing at a time, each by doing it (tutorial review 2026-11-15: the first minion fell
    // without a touch and the grass step had no enemy in it): 1 walk to a ring, 2 bring a minion inside her white ring (she
    // attacks by herself), 3 find the minion that hides in the grass, then hide there herself, 4 Focus: a hero hits a random
    // enemy in range, 5 so making one enemy the only one in range hits it every time; then a boost pad at the end of the bridge
    // throws her towards the battle, the picture fades and the game starts the arena half (Tutorial.cs). The towers are not
    // part of it. Every finished lesson fills one of the guide card's dots (Tutorial.Lesson).
    public sealed partial class Match
    {
        struct IslandStepDef
        {
            public string Text;              // '|' marks the highlighted words; the card breaks it into its two rows by itself (Hud.LineText)
            public string Text2;             // the line of the step's second part (Tutorial.Part 1): the grass step's "hide yourself", the two Focus steps' "this is what happens here"
            public bool HasTarget; public float TargetX, TargetY;
            public float Time;               // > 0: the step ends after this long
            public int Spawn0, SpawnN;       // the practice minions it brings (spawner ids Spawn0 .. Spawn0 + SpawnN - 1)
            public bool Kill, Pad, Tip;
            public bool Focus;               // the Focus lesson: the three stand still, Tyra hits them at random from the ring
            public bool Grass;               // the grass lesson: find the minion hidden in the grass, then stand hidden in it
            public byte Point;               // 0 none, 1 the first living minion she can see, 2 the leader
        }

        const float IslandStartX = 6.5f, IslandStartY = 14.8f, IslandDiscX = 6.5f, IslandDiscY = 11f, IslandWalkR = 5.25f;
        const int IslandW = 13, IslandH = 50, IslandBridgeX0 = 5; // the bridge is 3 tiles wide from this column
        const float IslandNeckY = 16.5f;   // until the bridge opens Tyra cannot walk past the platform's edge
        // The island is framed on one screen until then. 11.4 -> 12.8 (tutorial review 2026-11-15): the platform sits higher on
        // the screen, under the guide card at its top and clear of the joystick, instead of behind a card across its far edge.
        const float IslandCamY = 12.8f;
        // Rickard 2026-10-26 (a shorter, Brawl Stars-like tutorial): half the pause between steps. 0.5 -> 0.65 s 2026-11-15: the
        // pause now shows the praise for the finished lesson ("NICE!"), which needs a moment to be read
        const float IslandFirstPause = 1.8f, IslandStepPause = 0.65f, IslandFadeOut = 0.6f;
        const int IslandPadTx = IslandBridgeX0 + 1, IslandPadTy = 19;
        // the old gamedata pad: a push of (0, 3.6) at speed 12, flown at half speed for its length + 0.8
        const float IslandJumpDist = Tutorial.PadThrow, IslandJumpSpeed = 6f;
        // Lesson 1's ring: left of the platform's middle, 3.4 tiles from where Tyra starts. From it the first minion (far edge)
        // is about 6 tiles away, out of her reach of 4.4 + its radius, so she has to walk it into her white ring herself (it
        // used to walk past her in the middle and fall without a touch).
        const float IslandFirstX = 4.1f, IslandFirstY = 12.4f;
        // The grass lesson: a minion stands hidden in the bottom right tuft (tile 9, 14), seen only from Rules.GrassSight (2.5)
        // tiles; two of Tyra's hits take it down, then she stands in the grass herself until nobody sees her for IslandHideTime s.
        const float IslandGrassX = 9.5f, IslandGrassY = 14.5f, IslandHideTime = 0.6f, IslandHiddenHp = 60f;
        const int IslandHiddenId = 4;

        // Rickard 2026-10-25: the old Focus steps, said plainly (the rule is the same).
        // Rickard 2026-10-26: one short line per step, and the old "random enemy" and "only target" steps are one: the three
        // come in, the leader is pointed out, and Tyra takes it down from the ring where it is the only one in her reach.
        // The ring is left of the leader's spot (4.5, 13), out of reach of the other two (8.5, 12) and (8.5, 13.5).
        // Tutorial review 2026-11-15: every step now needs the player to do it, and the lines say what to do and why.
        static readonly IslandStepDef[] IslandSteps =
        {
            new IslandStepDef { Text = "Welcome! Drag the |joystick| to walk into the |gold ring|.", HasTarget = true, TargetX = IslandFirstX, TargetY = IslandFirstY },
            new IslandStepDef { Text = "Get the |minion| in your white ring. You |attack by yourself|!", Spawn0 = 0, SpawnN = 1, Kill = true, Point = 1 },
            new IslandStepDef { Text = "Enemies hide in |grass|! Walk close to find this one.", Text2 = "Now hide in the |grass|: enemies can't see |you| there.", Spawn0 = IslandHiddenId, SpawnN = 1, Grass = true, Point = 1, HasTarget = true, TargetX = IslandGrassX, TargetY = IslandGrassY },
            // Rickard 2026-11-15: Focus back, with the dots, explained and shown before the "only target" step. The three come in
            // and cannot fall; from the ring all three are in Tyra's reach (3 dots) and her hits land on them at random until she
            // has stood there FocusShow s. Then they are healed and cannot be hurt until she stands in the gold ring with only the
            // leader in reach (he wrote the same evening that they were shot down on the way, so the leader fell to one shot).
            // Each of the two has a line for getting there and one for what happens there (Part), so no line is longer than two
            // rows of the card and each says what the player sees at that moment.
            new IslandStepDef { Text = "Three enemies! Stand in the |gold ring|.", Text2 = "You hit a |random| enemy in range. |FOCUS| counts them.", Spawn0 = 1, SpawnN = 3, Focus = true, HasTarget = true, TargetX = IslandDiscX, TargetY = IslandDiscY },
            new IslandStepDef { Text = "Want to hit only the |leader|? Stand in the |gold ring|.", Text2 = "Only |one| enemy in range: |every| hit lands on him!", Kill = true, Point = 2, HasTarget = true, TargetX = 2f, TargetY = 12.5f },
            new IslandStepDef { Text = "You're ready! Hop on the |boost pad| to fly to the |arena|.", HasTarget = true, TargetX = IslandBridgeX0 + 1.5f, TargetY = 19.5f, Pad = true, Tip = true },
        };

        // the practice minions' spawners (old gamedata tutorial.spawners 0-3, and 4 for the hidden one): they come in over the
        // platform's far edge (row 6) and walk down to their own spot (the old lane point, which the old tutStatic walk read as
        // (wp.x + 0.5, wp.y + 1)); the hidden one is in its tuft from the start
        // 2026-11-15: the other two stand right of the disc's middle (8.5, 12) and (8.5, 13.5), far from the gold ring: the old
        // (6.5, 14) was 4.74 tiles from it, just past Tyra's reach of 4.4 + 0.25 since her range went up that day. The first
        // minion stops at the far edge (6.5, 7) instead of walking down to (6.5, 14) past Tyra.
        static readonly float[] IslandSpawnX = { 6, 5, 8, 8, 9 }, IslandWpX = { 6, 4, 8, 8, 9 }, IslandWpY = { 6, 12, 11, 12.5f, 13.5f };
        const float FocusShow = 5f;    // the Focus lesson: seconds with all three in reach after they walked in (about five of Tyra's hits)
        const float FocusFloor = 0.5f; // nor do they drop below half health while she shows it
        const float IslandSpawnRow = 6, IslandInvulnerable = 3; // the three walk in to their spots first (was 8 s under the old 10 s step)

        static Arena IslandMap()
        {
            var tiles = new byte[IslandW * IslandH];
            for (int ty = 0; ty < IslandH; ty++)
                for (int tx = 0; tx < IslandW; tx++)
                {
                    byte t;
                    if (ty < 17)
                    {
                        float dx = tx + 0.5f - IslandDiscX, dy = ty + 0.5f - IslandDiscY, d = SimMath.Hypot(dx, dy);
                        bool neck = ty == 16 && tx >= IslandBridgeX0 && tx <= IslandBridgeX0 + 2;
                        t = d > IslandWalkR && !neck ? Arena.Void : d > 4.0f && Math.Abs(dx) > 2.6f && Math.Abs(dy) > 2.6f ? Arena.Grass : Arena.Ground;
                    }
                    // rows 17-19: the bridge, with the boost pad on its last row. Nothing below it: the old "Tutorial" map's lane
                    // (rows 22-49) was never walked and showed as a blank stone slab under the bridge once it opened (tutorial
                    // review 2026-11-15); the pad's flight goes out over the open sky into the fade
                    else t = ty <= IslandPadTy && tx >= IslandBridgeX0 && tx <= IslandBridgeX0 + 2 ? Arena.Ground : Arena.Void;
                    tiles[ty * IslandW + tx] = t;
                }
            // the old map's lane (team 0 walks it upwards); no lane minions come on the island, only Tyra's practice ones
            var a = new Arena(IslandW, IslandH, tiles, new[] { 6.5f, 6.5f, 6.5f }, new[] { 42.5f, 35.5f, 29.5f });
            for (int i = 0; i < 3; i++) { a.SpawnX[0][i] = IslandStartX; a.SpawnY[0][i] = IslandStartY; a.SpawnX[1][i] = IslandDiscX; a.SpawnY[1][i] = IslandSpawnRow + 0.5f; }
            // the towers are not part of the island: out of the picture (the old y = -25)
            for (int t = 0; t < 2; t++) { a.TowerX[t] = IslandDiscX; a.TowerY[t] = -25; }
            a.MinionX[0] = 6.5f; a.MinionY[0] = 45.5f; a.MinionX[1] = 6.5f; a.MinionY[1] = 26.5f;
            return a;
        }

        void StartIsland()
        {
            NoStars = true;
            var tu = Tut = new Tutorial { Island = true, Step = Tutorial.IslandWalk, Tower = Towers[Teams.Enemy], Pause = IslandFirstPause, Lessons = Tutorial.AllLessons };
            tu.CamHold = true; tu.CamX = IslandDiscX; tu.CamY = IslandCamY;
            tu.PadX = IslandPadTx + 0.5f; tu.PadY = IslandPadTy + 0.5f;
            State = MatchState.Play; Time = 0;
            for (int t = 0; t < 2; t++) AddStatus(Towers[t], St.Disarm, 1e9f);
            // Tyra plays it (Rickard 2026-10-26) and speaks the lines; as strong as the old onboarding match made the hero
            var p = NewHero(HeroId.Tyra, Teams.Player, IslandStartX, IslandStartY);
            tu.Speaker = TutSpeaker(p); // the hero you play is the guide (Rickard 2026-10-29)
            p.IsPlayer = true; p.DmgMult = 2; p.TakenMult = 0.5f; p.Charge = 0; Player = p;
            RefreshIsland();
        }

        /// <summary>The guide's name on the card (the HUD shows the hero's portrait beside it).</summary>
        static string TutSpeaker(Unit p) => HeroCatalog.Get(p.Hero).Name.ToUpperInvariant();

        /// <summary>The line to show now, null = none: LineMarked keeps the '|' marks round the words to highlight, Line is the
        /// same text without them.</summary>
        void TutSay(string marked)
        {
            var tu = Tut;
            if (ReferenceEquals(marked, tu.LineMarked)) return;
            tu.LineMarked = marked; tu.Line = marked?.Replace("|", "");
        }

        /// <summary>A lesson is done: one more of the guide card's dots fills, with a word of praise (the HUD).</summary>
        void TutLessonDone() { var tu = Tut; if (tu.Lesson < tu.Lessons) tu.Lesson++; tu.LessonAt = Time; }

        void IslandSpawn(int id)
        {
            bool hidden = id == IslandHiddenId;
            float sx = IslandWpX[id] + 0.5f, sy = IslandWpY[id] + 1f; // its own spot
            var m = NewMinion(MinionKind.Melee, Teams.Enemy, hidden ? sx : IslandSpawnX[id] + 0.5f, hidden ? sy : IslandSpawnRow + 0.5f);
            m.SpawnX = sx; m.SpawnY = sy;
            m.DmgOverride = 0; m.TutStatic = true; // it swings (every attack shows) but does no harm
            // the very first and the leader fall fast, the hidden one to two hits
            m.MaxHp = m.Hp = hidden ? IslandHiddenHp : id <= 1 ? Rules.TutorialMinionHp * 0.5f : Rules.TutorialMinionHp;
            if (id >= 1 && !hidden) { AddStatus(m, St.Invulnerable, IslandInvulnerable); m.TutUnkillable = true; }
            if (id == 1) Tut.Leader = m;
            Tut.Minions.Add(m);
        }

        void EnterIslandStep()
        {
            var tu = Tut; ref readonly var st = ref IslandSteps[tu.Step]; tu.T = 0; tu.Part = 0; tu.Hid = 0;
            for (int i = 0; i < st.SpawnN; i++) IslandSpawn(st.Spawn0 + i);
            if (st.Pad) { tu.BridgeOpen = true; tu.PadOn = true; tu.CamHold = false; }
            if (tu.Step == Tutorial.IslandTarget)
                for (int i = 0; i < tu.Minions.Count; i++)
                {
                    // full health again, and no harm until Tyra stands in the gold ring (IslandGuard)
                    var m = tu.Minions[i]; if (!m.Alive) continue;
                    m.Hp = m.MaxHp; m.RemoveAll(St.Invulnerable); AddStatus(m, St.Invulnerable, 1e9f);
                }
        }

        bool IslandMinionLeft()
        {
            var l = Tut.Minions;
            for (int i = 0; i < l.Count; i++) if (l[i].Alive) return true;
            return false;
        }

        void UpdateIsland(float dt)
        {
            var tu = Tut; var p = Player;
            if (tu.Jumping)
            {
                // the pad's flight: out over the open sky, Tyra does nothing else (St.Casting), the picture fades
                float step = Math.Min(tu.JumpLeft, IslandJumpSpeed * dt);
                p.Y += step; tu.JumpLeft -= step; p.Facing = SimMath.PI / 2;
                tu.Leave += dt; tu.Fade = Math.Min(1, tu.Leave / IslandFadeOut);
                if (tu.Leave >= IslandFadeOut) tu.Done = true;
                RefreshIsland();
                return;
            }
            p.Charge = 0; // no special on the island
            if (!tu.BridgeOpen && p.Y > IslandNeckY) p.Y = IslandNeckY;
            // the leader takes the other two down with it
            if (tu.Leader != null && !tu.Leader.Alive)
                for (int i = 0; i < tu.Minions.Count; i++) { var m = tu.Minions[i]; if (m.Alive && m != tu.Leader) Kill(m, p); }
            if (tu.Pause > 0)
            {
                tu.Pause -= dt;
                if (tu.Pause <= 0) EnterIslandStep();
                RefreshIsland();
                return;
            }
            ref readonly var st = ref IslandSteps[tu.Step];
            tu.T += dt;
            if (st.Pad)
            {
                if ((int)Math.Floor(p.X) == IslandPadTx && (int)Math.Floor(p.Y) == IslandPadTy)
                {
                    tu.Jumping = true; tu.JumpLeft = IslandJumpDist;
                    AddStatus(p, St.Casting, IslandJumpDist / IslandJumpSpeed + 0.1f);
                }
                RefreshIsland();
                return; // the pad step ends with the jump
            }
            bool done;
            if (st.Focus)
            {
                // the clock runs only while all three stand on their spots in her reach (three dots), so her hits land on them
                bool show = FocusTargets(p) >= 3;
                for (int i = 0; i < tu.Minions.Count && show; i++) if (tu.Minions[i].Alive && tu.Minions[i].Has(St.Invulnerable)) show = false;
                if (!show) tu.T = Math.Max(0, tu.T - dt);
                tu.Part = show ? 1 : 0; // "stand in the ring" until then, "you hit a random enemy" while it shows
                for (int i = 0; i < tu.Minions.Count; i++) { var m = tu.Minions[i]; if (m.Alive && m.Hp < m.MaxHp * FocusFloor) m.Hp = m.MaxHp * FocusFloor; }
                done = tu.T >= FocusShow;
            }
            else if (st.Grass)
            {
                // part 0: the hidden minion is found and taken down. Part 1: Tyra stands in grass (any tuft) herself, seen by
                // nobody: her own last shot keeps her lit for Rules.RevealTime first, so the crossed-out eye comes up before it ends
                if (IslandMinionLeft()) done = false;
                else
                {
                    tu.Part = 1;
                    tu.Hid = Map.InGrass(p.X, p.Y) && HiddenFromFoes(p) ? tu.Hid + dt : 0;
                    done = tu.Hid >= IslandHideTime;
                }
            }
            else if (st.Time > 0) done = tu.T >= st.Time;
            else if (st.Kill) done = !IslandMinionLeft();
            else done = st.HasTarget && SimMath.Hypot(p.X - st.TargetX, p.Y - st.TargetY) < 0.9f;
            if (tu.Step == Tutorial.IslandTarget) { IslandGuard(p); tu.Part = IslandOnlyLeader(p) ? 1 : 0; }
            if (done) { tu.Step++; tu.T = 0; tu.Part = 0; tu.Pause = IslandStepPause; TutLessonDone(); } // every step but the pad is a lesson
            RefreshIsland();
        }

        /// <summary>Focus: how many enemies are in the hero's auto attack reach, the same list the attack picks from at random
        /// (Combat.cs UpdateAttack). The HUD's FOCUS dots count the same (HudFocus.cs).</summary>
        public int FocusTargets(Unit p) => p == null || !p.Alive ? 0 : EnemiesInRange(p, p.Range, Pick.Any, true, tmpBuf).Count;

        // the "only target" step: Tyra stands in the gold ring with only the leader in her reach
        bool IslandOnlyLeader(Unit p)
        {
            var tu = Tut; ref readonly var st = ref IslandSteps[tu.Step];
            if (tu.Leader == null || !tu.Leader.Alive || SimMath.Hypot(p.X - st.TargetX, p.Y - st.TargetY) > 0.9f) return false;
            var l = EnemiesInRange(p, p.Range, Pick.Any, true, tmpBuf);
            return l.Count == 1 && l[0] == tu.Leader;
        }

        // the three cannot be hurt until she does
        void IslandGuard(Unit p)
        {
            var tu = Tut;
            if (tu.Leader == null || !tu.Leader.Alive || !tu.Leader.Has(St.Invulnerable) || !IslandOnlyLeader(p)) return;
            for (int i = 0; i < tu.Minions.Count; i++) { tu.Minions[i].RemoveAll(St.Invulnerable); tu.Minions[i].TutUnkillable = false; }
        }

        /// <summary>What the screen shows now: nothing between steps (the pause) or during the jump.</summary>
        void RefreshIsland()
        {
            var tu = Tut;
            ref readonly var st = ref IslandSteps[tu.Step];
            bool show = tu.Pause <= 0 && !tu.Jumping && State == MatchState.Play;
            TutSay(show ? (tu.Part == 1 && st.Text2 != null ? st.Text2 : st.Text) : null);
            tu.LineTip = st.Tip;
            tu.RingOn = show && st.HasTarget; tu.RingX = st.TargetX; tu.RingY = st.TargetY;
            Unit at = null;
            if (show && st.Point == 1)
            {
                // the minion to take down, once Tyra can see it (the one in the grass shows itself only up close)
                for (int i = 0; i < tu.Minions.Count; i++) { var m = tu.Minions[i]; if (m.Alive && IsVisibleTo(m, Teams.Player)) { at = m; break; } }
            }
            else if (show && st.Point == 2 && tu.Leader != null && tu.Leader.Alive) at = tu.Leader;
            tu.PointAt = at;
            // the ring's own arrow: not while another thing is to be looked at (a minion with the arrow over it; the FOCUS chip,
            // lit under the guide card in the Focus step's second part)
            tu.ArrowOnRing = tu.RingOn && st.Point != 2 && at == null && !(st.Focus && tu.Part == 1);
        }
    }
}
