using System;

namespace BAM.Sim
{
    // The training island, the guided match's first half (the old game's SimTutorialIsland.cs, Tutorial 2.0): Fenn stands on a
    // round stone platform and learns to walk, to auto attack, to hide in grass and that a hero hits a random enemy in range (so
    // making one enemy the only one in range hits it every time); then a boost pad at the end of the bridge throws him towards
    // the battle, the picture fades to black and the game starts the arena half (Tutorial.cs). The towers are not part of it.
    public sealed partial class Match
    {
        struct IslandStepDef
        {
            public string Text;              // '|' marks the highlighted words, '\n' breaks lines
            public bool HasTarget; public float TargetX, TargetY;
            public float Time;               // > 0: the step ends after this long
            public int Spawn0, SpawnN;       // the practice minions it brings (spawner ids Spawn0 .. Spawn0 + SpawnN - 1)
            public bool Kill, Pad, Tip;
            public byte Point;               // 0 none, 1 the first living minion, 2 the leader
        }

        const float IslandStartX = 6.5f, IslandStartY = 14.8f, IslandDiscX = 6.5f, IslandDiscY = 11f, IslandWalkR = 5.25f;
        const int IslandW = 13, IslandH = 50, IslandBridgeX0 = 5; // the bridge is 3 tiles wide from this column
        const float IslandNeckY = 16.5f;   // until the bridge opens Fenn cannot walk past the platform's edge
        const float IslandCamY = 11.4f;    // the island is framed on one screen until then
        // Rickard 2026-10-26 (a shorter, Brawl Stars-like tutorial): half the pause between steps
        const float IslandFirstPause = 1.8f, IslandStepPause = 0.5f, IslandFadeOut = 0.6f;
        const int IslandPadTx = IslandBridgeX0 + 1, IslandPadTy = 19;
        // the old gamedata pad: a push of (0, 3.6) at speed 12, flown at half speed for its length + 0.8
        const float IslandJumpDist = 3.6f + 0.8f, IslandJumpSpeed = 6f;

        // Rickard 2026-10-25: the old Focus steps, said plainly (the new HUD has no Focus counter; the rule is the same).
        // Rickard 2026-10-26: one short line per step, and the old "random enemy" and "only target" steps are one: the three
        // come in, the leader is pointed out, and Fenn takes it down from the ring where it is the only one in his reach.
        // The ring is left of the leader's spot (4.5, 13), out of reach of the other two (6.5, 14) and (7.5, 13); the old ring
        // at (4.5, 13) had all three within Fenn's reach of 3 tiles.
        static readonly IslandStepDef[] IslandSteps =
        {
            new IslandStepDef { Text = "Move here with the joystick!", HasTarget = true, TargetX = IslandDiscX, TargetY = IslandDiscY },
            new IslandStepDef { Text = "You attack automatically! Take down the |Minion|!", Spawn0 = 0, SpawnN = 1, Kill = true, Point = 1 },
            new IslandStepDef { Text = "Hide in the |grass|! Enemies only see you up close.", HasTarget = true, TargetX = 9.5f, TargetY = 14f },
            new IslandStepDef { Text = "You hit a |random enemy| in reach. Make the leader your |only target|!", Spawn0 = 1, SpawnN = 3, Kill = true, Point = 2, HasTarget = true, TargetX = 2f, TargetY = 12.5f },
            new IslandStepDef { Text = "Hop on the |boost pad| to reach the battle!", HasTarget = true, TargetX = IslandBridgeX0 + 1.5f, TargetY = 19.5f, Pad = true, Tip = true },
        };
        static readonly string[] IslandPlain = MakePlain();
        static string[] MakePlain()
        {
            var r = new string[IslandSteps.Length];
            for (int i = 0; i < r.Length; i++) r[i] = IslandSteps[i].Text.Replace("|", "");
            return r;
        }

        // the practice minions' spawners (old gamedata tutorial.spawners 0-3): they come in over the platform's far edge (row 6)
        // and walk down to their own spot (the old lane point, which the old tutStatic walk read as (wp.x + 0.5, wp.y + 1))
        static readonly float[] IslandSpawnX = { 6, 5, 6, 7 }, IslandWpX = { 6, 4, 6, 7 }, IslandWpY = { 13, 12, 13, 12 };
        const float IslandSpawnRow = 6, IslandInvulnerable = 3; // the three walk in to their spots first (was 8 s under the old 10 s step)

        // rows 17-49: the bridge and the old "Tutorial" map's lane below it (shown only once the bridge opens; walkable only
        // for the pad's flight). Rows 0-16 are the round platform, made in IslandMap. ' ' void, '.' ground, 'g' grass, '#' wall
        static readonly string[] IslandLower =
        {
            "     ...     ", "     ...     ", "     ...     ", "             ", "             ", // 17-21
            "  .........  ", "  .........  ", "  .........  ", "  .........  ", "  .... ....  ", // 22-26
            "  .........  ", "ggg.......ggg", "gg#.......ggg", "ggg.......#gg", "ggg.......#gg", // 27-31
            "ggg.......#gg", "g...........g", "g...........g", "g...........g", "g...........g", // 32-36
            "g...........g", "g...........g", "gg#.......ggg", "gg#.......ggg", "gg#.......ggg", // 37-41
            "ggg.......#gg", "ggg.......ggg", "  .........  ", "  .... ....  ", "  .........  ", // 42-46
            "  .........  ", "  .........  ", "  .........  ",                                   // 47-49
        };

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
                    else
                    {
                        char c = IslandLower[ty - 17][tx];
                        t = c == ' ' ? Arena.Void : c == 'g' ? Arena.Grass : c == '#' ? Arena.Wall : Arena.Ground;
                    }
                    tiles[ty * IslandW + tx] = t;
                }
            // the old map's lane (team 0 walks it upwards); no lane minions come on the island, only Fenn's practice ones
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
            var tu = Tut = new Tutorial { Island = true, Step = Tutorial.IslandWalk, Tower = Towers[Teams.Enemy], Pause = IslandFirstPause, Speaker = "FENN SAYS" };
            tu.CamHold = true; tu.CamX = IslandDiscX; tu.CamY = IslandCamY;
            tu.PadX = IslandPadTx + 0.5f; tu.PadY = IslandPadTy + 0.5f;
            State = MatchState.Play; Time = 0;
            for (int t = 0; t < 2; t++) AddStatus(Towers[t], St.Disarm, 1e9f);
            // Fenn guides; as strong as the old onboarding match made him (FirstOnboardingMatch)
            var p = NewHero(HeroId.Fenn, Teams.Player, IslandStartX, IslandStartY);
            p.IsPlayer = true; p.DmgMult = 2; p.TakenMult = 0.5f; p.Charge = 0; Player = p;
            RefreshIsland();
        }

        void IslandSpawn(int id)
        {
            var m = NewMinion(MinionKind.Melee, Teams.Enemy, IslandSpawnX[id] + 0.5f, IslandSpawnRow + 0.5f);
            m.SpawnX = IslandWpX[id] + 0.5f; m.SpawnY = IslandWpY[id] + 1f;
            m.DmgOverride = 0; m.TutStatic = true; // it swings (every attack shows) but does no harm
            m.MaxHp = m.Hp = id <= 1 ? Rules.TutorialMinionHp * 0.5f : Rules.TutorialMinionHp; // the very first and the leader fall fast
            if (id >= 1) AddStatus(m, St.Invulnerable, IslandInvulnerable);
            if (id == 1) Tut.Leader = m;
            Tut.Minions.Add(m);
        }

        void EnterIslandStep()
        {
            var tu = Tut; ref readonly var st = ref IslandSteps[tu.Step]; tu.T = 0;
            for (int i = 0; i < st.SpawnN; i++) IslandSpawn(st.Spawn0 + i);
            if (st.Pad) { tu.BridgeOpen = true; tu.PadOn = true; tu.CamHold = false; }
        }

        void UpdateIsland(float dt)
        {
            var tu = Tut; var p = Player;
            if (tu.Jumping)
            {
                // the pad's flight: over the void and walls, Fenn does nothing else (St.Casting), the picture fades to black
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
            if (st.Time > 0) done = tu.T >= st.Time;
            else if (st.Kill) { done = true; for (int i = 0; i < tu.Minions.Count; i++) if (tu.Minions[i].Alive) { done = false; break; } }
            else done = st.HasTarget && SimMath.Hypot(p.X - st.TargetX, p.Y - st.TargetY) < 0.9f;
            if (done) { tu.Step++; tu.T = 0; tu.Pause = IslandStepPause; }
            RefreshIsland();
        }

        /// <summary>What the screen shows now: nothing between steps (the pause) or during the jump.</summary>
        void RefreshIsland()
        {
            var tu = Tut;
            ref readonly var st = ref IslandSteps[tu.Step];
            bool show = tu.Pause <= 0 && !tu.Jumping && State == MatchState.Play;
            tu.Line = show ? IslandPlain[tu.Step] : null;
            tu.LineMarked = show ? st.Text : null;
            tu.LineTip = st.Tip;
            tu.RingOn = show && st.HasTarget; tu.RingX = st.TargetX; tu.RingY = st.TargetY;
            tu.ArrowOnRing = tu.RingOn && st.Point != 2;
            Unit at = null;
            if (show && st.Point == 1) { for (int i = 0; i < tu.Minions.Count; i++) if (tu.Minions[i].Alive) { at = tu.Minions[i]; break; } }
            else if (show && st.Point == 2 && tu.Leader != null && tu.Leader.Alive) at = tu.Leader;
            tu.PointAt = at;
        }
    }
}
