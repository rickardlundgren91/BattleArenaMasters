using System;

namespace BAM.Sim
{
    // The guided match after the tutorial review of 2026-11-15 (Rickard: "snygg, sammanhängande och lärorik"): every lesson is done
    // by the player. The island: the first minion must be walked into Tyra's reach, a minion hides in the grass and then Tyra does.
    // The arena: BLINK with the joystick at rest goes away from Knut, only her special finishes him, and the tower shoots: her
    // hits count only while it is busy with her minions. Since 2026-11-16 (Rickard: "Sen har vi inte lärt spelaren att man kan dra
    // skillen. Gör så att fenn dyker upp från buskarna och man får lära sig på honom") Fenn steps out of the side grass after the
    // tap lesson and falls to a dragged, aimed bolt alone. Eleven lessons in all (Tutorial.Lesson). The island ends with the
    // boost pad's throw, seen and not faded over, and the arena begins with Tyra coming down from it (Rickard 2026-11-16).
    public sealed partial class Match
    {
        static PlayerInput TutV3Go(Unit p, float gx, float gy, float stop = 0.15f)
        {
            var i = PlayerInput.None;
            if (SimMath.Hypot(gx - p.X, gy - p.Y) > stop) { i.MoveX = gx - p.X; i.MoveY = gy - p.Y; }
            return i;
        }

        // a player who does what the island's marks show: to the arrow's minion until it is in reach, else into the ring
        static PlayerInput TutV3IslandPlayer(Match m)
        {
            var tu = m.Tut; var p = m.Player;
            if (tu.Line == null) return PlayerInput.None;
            if (tu.PointAt != null && tu.Step != Tutorial.IslandTarget) return m.FocusTargets(p) == 0 ? TutV3Go(p, tu.PointAt.X, tu.PointAt.Y) : PlayerInput.None;
            return tu.RingOn ? TutV3Go(p, tu.RingX, tu.RingY) : PlayerInput.None;
        }

        // a player who does what the arena's lines say; rush = straight into the tower's ring at the tower moment, and stays;
        // drag false = never drags the special (the drag lesson is then the game's to finish)
        static PlayerInput TutV3ArenaPlayer(Match m, bool rush = false, bool blink = true, bool special = true, bool drag = true)
        {
            var tu = m.Tut; var p = m.Player; var tw = tu.Tower; var i = PlayerInput.None;
            switch (tu.Step)
            {
                case Tutorial.StepMove: case Tutorial.StepAuto: case Tutorial.StepSpecial:
                    if (tu.Goal != null && SimMath.Dist(p, tu.Goal) > 3.5f) i = TutV3Go(p, tu.Goal.X, tu.Goal.Y);
                    break;
                case Tutorial.StepFight:
                    if (tu.Goal != null && SimMath.Dist(p, tu.Goal) > 4f) i = TutV3Go(p, tu.Goal.X, tu.Goal.Y);
                    break;
                case Tutorial.StepTower:
                    if (rush) i = TutV3Go(p, tw.X, tw.Y + 3.5f);
                    else if (tu.RingOn) i = TutV3Go(p, tu.RingX, tu.RingY, 0.4f);
                    else if (tu.Goal != null && SimMath.Dist(p, tu.Goal) > 4.6f) i = TutV3Go(p, tu.Goal.X, tu.Goal.Y);
                    break;
            }
            if (special && p.SpecialReady && tu.Line != null && (tu.Step == Tutorial.StepSpecial || tu.Step == Tutorial.StepFight)) i.Special = true;
            // the drag lesson: a drag of the special, let go with the aim on Fenn
            if (drag && tu.DragAsked) { i.Special = true; i.AimAngle = SimMath.Atan2(tu.Hider.Y - p.Y, tu.Hider.X - p.X); i.AimK = 1; }
            if (blink && tu.Step == Tutorial.StepBlink && tu.Hooked && m.SpellReady(p)) { i.Spell = true; i.SpellAngle = float.NaN; } // a tap, joystick at rest
            return i;
        }

        static void TutorialV3()
        {
            TutV3Island();
            TutV3Arena();
            TutV3Drag();
            TutV3Knut();
            TutV3Tower();
        }

        static void TutV3Island()
        {
            var m = Create(new MatchConfig { Tutorial = true, Island = true, Seed = 5 });
            var tu = m.Tut; var p = m.Player;
            Check(tu != null && tu.Island && tu.Lesson == 0 && tu.Lessons == Tutorial.AllLessons, "TUT3: the island starts at lesson 0 of 11");
            Check(tu.Speaker == "TYRA", $"TUT3: Tyra is the guide (speaker '{tu.Speaker}')");
            // the map: the bridge and its pad, nothing below
            Check(m.Map.Walkable(IslandPadTx, IslandPadTy) && m.Map.Tile(IslandPadTx, IslandPadTy + 1) == Arena.Void && m.Map.Tile(6, 30) == Arena.Void, "TUT3: no ground below the bridge's pad (the old lane's slab is gone)");
            Check(m.Map.InGrass(IslandGrassX, IslandGrassY), "TUT3: the hidden minion's spot is grass");

            // lesson 1: into the ring
            float t0 = m.Time;
            while (m.Time - t0 < 20f && tu.Step == Tutorial.IslandWalk) m.Step(TutV3IslandPlayer(m));
            Check(tu.Step == Tutorial.IslandMinion && tu.Lesson == 1, $"TUT3: walking into the ring is lesson 1 (step {tu.Step}, lesson {tu.Lesson})");

            // lesson 2: the minion stops at the far edge, out of reach from the ring: nothing happens until she walks to it
            t0 = m.Time;
            while (m.Time - t0 < 6f) m.Step(PlayerInput.None);
            Unit first = tu.Minions.Count > 0 ? tu.Minions[0] : null;
            Check(first != null && first.Alive && first.Hp >= first.MaxHp && tu.Step == Tutorial.IslandMinion && m.FocusTargets(p) == 0,
                $"TUT3: the first minion is out of reach from the ring and falls to nothing while Tyra stands still (step {tu.Step}, in reach {m.FocusTargets(p)})");
            Check(tu.PointAt == first && tu.LineMarked != null && tu.LineMarked.Contains("|minion|"), "TUT3: the arrow is on the minion and the line marks it");
            t0 = m.Time;
            while (m.Time - t0 < 20f && tu.Step == Tutorial.IslandMinion) m.Step(TutV3IslandPlayer(m));
            Check(tu.Step == Tutorial.IslandGrass && tu.Lesson == 2 && first != null && !first.Alive, $"TUT3: walked into her ring, the minion falls: lesson 2 (step {tu.Step}, lesson {tu.Lesson})");

            // lesson 3: the minion in the grass is not seen from afar, the arrow is on the tuft; up close it shows and falls
            t0 = m.Time;
            while (m.Time - t0 < 3f && tu.Line == null) m.Step(PlayerInput.None);
            Unit hid = tu.Minions.Count > 1 ? tu.Minions[1] : null;
            bool far = hid != null && SimMath.Dist(p, hid) > Rules.GrassSight + 0.5f;
            Check(hid != null && hid.Alive && m.Map.InGrass(hid.X, hid.Y), "TUT3: the grass lesson's minion stands in the grass");
            Check(!far || (!m.IsVisibleTo(hid, Teams.Player) && tu.PointAt == null && tu.ArrowOnRing && m.FocusTargets(p) == 0),
                $"TUT3: from afar the minion in the grass is hidden: no arrow on it, the arrow on the tuft's ring (visible {hid != null && m.IsVisibleTo(hid, Teams.Player)})");
            bool sawIt = false, part2 = false; string line1 = tu.Line;
            t0 = m.Time;
            while (m.Time - t0 < 25f && tu.Step == Tutorial.IslandGrass)
            {
                m.Step(TutV3IslandPlayer(m));
                if (hid != null && hid.Alive && tu.PointAt == hid) sawIt = true;
                if (hid != null && !hid.Alive && tu.Line != null && tu.Line != line1) part2 = true;
            }
            Check(sawIt, "TUT3: up close the hidden minion shows and gets the arrow");
            Check(part2, "TUT3: with the minion down the line turns to hiding in the grass yourself");
            Check(tu.Step == Tutorial.IslandFocus && tu.Lesson == 3 && m.Map.InGrass(p.X, p.Y), $"TUT3: standing hidden in the grass ends lesson 3 (step {tu.Step}, lesson {tu.Lesson})");

            // lesson 4, Focus: "stand in the ring" until all three are in her reach, then the line names FOCUS (the ring's arrow
            // goes: the FOCUS chip, big under the guide card, is what to look at) while her hits land on them at random
            bool focusFirst = false, focusSecond = false, focusArrows = true; string focusLine1 = null;
            t0 = m.Time;
            while (m.Time - t0 < 40f && tu.Step == Tutorial.IslandFocus)
            {
                m.Step(TutV3IslandPlayer(m));
                if (tu.Step != Tutorial.IslandFocus || tu.Line == null) continue;
                if (focusLine1 == null) focusLine1 = tu.Line;
                if (!tu.FocusShown && tu.Line == focusLine1 && m.FocusTargets(p) < 3) focusFirst = true;
                if (tu.FocusShown) { focusSecond = tu.Line.Contains("FOCUS") && m.FocusTargets(p) >= 3; if (tu.ArrowOnRing) focusArrows = false; }
            }
            Check(focusFirst && focusLine1 != null && !focusLine1.Contains("FOCUS"), $"TUT3: the Focus lesson first sends Tyra into the ring (line '{focusLine1}')");
            Check(focusSecond && focusArrows, "TUT3: with all three in reach the line names FOCUS and the ring's arrow goes (the FOCUS chip is what to look at)");
            Check(tu.Step == Tutorial.IslandTarget && tu.Lesson == 4, $"TUT3: watching her hits spread is lesson 4 (step {tu.Step}, lesson {tu.Lesson})");

            // lesson 5, one target: the leader cannot be hurt until she stands in the gold ring with only him in reach; the line
            // says why only while she does
            bool hurtEarly = false, onlyLine = false; string targetLine1 = null;
            t0 = m.Time;
            while (m.Time - t0 < 40f && tu.Step == Tutorial.IslandTarget)
            {
                m.Step(TutV3IslandPlayer(m));
                if (tu.Step != Tutorial.IslandTarget || tu.Line == null) continue;
                if (targetLine1 == null) targetLine1 = tu.Line;
                bool inRing = SimMath.Hypot(p.X - tu.RingX, p.Y - tu.RingY) <= 0.9f;
                if (!inRing && tu.Leader != null && tu.Leader.Alive && tu.Leader.Hp < tu.Leader.MaxHp) hurtEarly = true;
                if (tu.Line != targetLine1) { onlyLine = true; if (tu.Leader != null && tu.Leader.Alive && (!inRing || m.FocusTargets(p) != 1)) hurtEarly = true; }
            }
            Check(!hurtEarly, "TUT3: the leader takes no hit before Tyra stands in the gold ring with only him in reach");
            Check(onlyLine, "TUT3: in the gold ring the line says that only one enemy is in range");
            Check(tu.Step == Tutorial.IslandPad && tu.Lesson == 5, $"TUT3: the leader down is lesson 5 (step {tu.Step}, lesson {tu.Lesson})");

            // the pad. Five lessons in all; the pad is none
            t0 = m.Time;
            while (m.Time - t0 < 90f && !tu.Jumping) m.Step(TutV3IslandPlayer(m));
            Check(tu.Jumping && !tu.Done && tu.Air == 0, $"TUT3 jump: stepping on the pad sets off its throw (jumping {tu.Jumping}, done {tu.Done}, air {tu.Air})");
            // The throw (Rickard 2026-11-16: no fade over it, a jump and a cut): Tyra goes up from the pad's middle for JumpTime s,
            // fast at first and never down again in this half, deaf to the joystick; at the top the half is done, and the game
            // cuts to the arena there
            float jumpAt = m.Time, lastAir = 0, airHalf = -1; bool fell = false, said = false;
            while (m.Time - jumpAt < 5f && !tu.Done)
            {
                m.Step(TutV3Go(p, IslandDiscX, IslandDiscY)); // steering back to the island: the throw has her
                if (tu.Air < lastAir) fell = true;
                if (tu.PAir != lastAir) fell = true; // PAir is the tick before, for the picture between ticks
                lastAir = tu.Air;
                if (airHalf < 0 && m.Time - jumpAt >= Tutorial.JumpTime * 0.5f) airHalf = tu.Air;
                if (tu.Line != null || tu.RingOn) said = true;
            }
            Check(tu.Done && tu.Lesson == Tutorial.IslandLessons, $"TUT3: the island ends on the pad with its five lessons done (done {tu.Done}, lesson {tu.Lesson}, step {tu.Step})");
            Check(!fell && tu.Air == 1 && Math.Abs(m.Time - jumpAt - Tutorial.JumpTime) < 0.03f, $"TUT3 jump: the half ends at the top of the throw, {Tutorial.JumpTime} s after the pad (fell {fell}, air {tu.Air}, {m.Time - jumpAt:0.00} s)");
            Check(airHalf > 0.7f && airHalf < 0.8f, $"TUT3 jump: fast off the pad, slowing to the top: three quarters up at half time (air {airHalf:0.00})");
            Check(SimMath.Hypot(p.X - tu.PadX, p.Y - tu.PadY) < 0.01f && !said, $"TUT3 jump: she goes up from the pad's middle, whatever the joystick says, with no line and no ring ({p.X:0.00}, {p.Y:0.00})");
            Check(m.Time < 60f, $"TUT3: a player who follows the marks is through the island in under a minute ({m.Time:0.0} s)");
        }

        // The arena half begins where the island's ended, at the top of the boost pad's throw: Tyra comes down for LandTime s,
        // slow off the top and never up again, deaf to the joystick until she is down; then the title has its time.
        static void TutV3Landing()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 5 });
            var tu = m.Tut; var p = m.Player;
            Check(tu.Air == 1 && tu.PAir == 1 && tu.Opening && !tu.Jumping, $"TUT3 landing: the arena half begins with Tyra at the top of the throw (air {tu.Air})");
            float x0 = p.X, y0 = p.Y, last = 1, airHalf = -1; bool up = false, moved = false, said = false;
            while (tu.Air > 0 && m.Time < 5f)
            {
                m.Step(TutV3Go(p, p.X + 3, p.Y));
                if (tu.Air > last || tu.PAir != last) up = true;
                last = tu.Air;
                if (tu.Air > 0 && (p.X != x0 || p.Y != y0)) moved = true;
                if (airHalf < 0 && m.Time >= Tutorial.LandTime * 0.5f) airHalf = tu.Air;
                if (tu.Line != null) said = true;
            }
            Check(!up && Math.Abs(m.Time - Tutorial.LandTime) < 0.03f, $"TUT3 landing: she is down {Tutorial.LandTime} s in, never a turn up on the way (up {up}, {m.Time:0.00} s)");
            Check(airHalf > 0.7f && airHalf < 0.8f, $"TUT3 landing: slow off the top, fast into the ground: three quarters up at half time (air {airHalf:0.00})");
            Check(!moved && !said, $"TUT3 landing: in the air she cannot be steered, and no line is up (moved {moved}, line {said})");
            for (int i = 0; i < 30; i++) m.Step(TutV3Go(p, p.X + 3, p.Y));
            Check(p.X > x0 + 0.5f && tu.Air == 0 && tu.PAir == 0, $"TUT3 landing: once down she walks ({p.X - x0:0.00} tiles in half a second)");
            Check(tu.Opening && tu.Line == null, "TUT3 landing: the title's time comes after the landing, with no line under it");
            while (m.Time < Tutorial.OpeningTime + 0.05f) m.Step(PlayerInput.None);
            Check(!tu.Opening && tu.Line != null, $"TUT3 landing: the first line comes {Tutorial.OpeningTime} s in (line '{tu.Line}')");
        }

        static void TutV3Arena()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 5 });
            var tu = m.Tut; var p = m.Player; var tw = tu.Tower;
            Check(tu.Lesson == Tutorial.IslandLessons && tu.Lessons == Tutorial.AllLessons, "TUT3: the arena half goes on from lesson 5 of 11");
            TutV3Landing();
            Check(tw.Has(St.Untargetable) && tw.Has(St.Disarm), "TUT3: before its moment the enemy tower neither shoots nor is a target");
            int towerHitsOnTyra = 0; bool knutBySpecial = false, fennByDrag = false, lineWithoutMarks = true; float minHp = p.Hp; int lessonAtTower = -1;
            while (m.State != MatchState.Over && !tu.Done && m.Time < 120f)
            {
                var k = tu.Guard;
                m.Step(TutV3ArenaPlayer(m));
                foreach (var e in m.Events)
                {
                    if (e.Type == EventType.Damage && e.B == p && e.A == tw) towerHitsOnTyra++;
                    if (e.Type == EventType.Death && e.B == k && e.A == p && tu.Step == Tutorial.StepFight) knutBySpecial = true;
                    if (e.Type == EventType.Death && e.B == tu.Hider && e.A == p && tu.Step == Tutorial.StepThrow) fennByDrag = true;
                }
                if (tu.Line != null && tu.Line.Contains("|")) lineWithoutMarks = false;
                if (tu.Step == Tutorial.StepTower && lessonAtTower < 0) lessonAtTower = tu.Lesson;
                minHp = Math.Min(minHp, p.Hp);
            }
            Check(tu.Done && m.State == MatchState.Over && m.Winner == Teams.Player, $"TUT3: a player who follows the lines breaks the tower (done {tu.Done}, step {tu.Step}, {m.Time:0.0} s)");
            Check(tu.Lesson == Tutorial.AllLessons && lessonAtTower == Tutorial.AllLessons - 1, $"TUT3: all eleven lessons are done at the end, ten when the tower moment starts ({tu.Lesson}, {lessonAtTower})");
            Check(fennByDrag, "TUT3: Fenn falls in the drag lesson");
            Check(tu.TowerHitsTaken == Tutorial.TowerHits, $"TUT3: the tower falls to exactly {Tutorial.TowerHits} of Tyra's hits ({tu.TowerHitsTaken})");
            Check(towerHitsOnTyra == 0, $"TUT3: the tower never shoots a player who lets the minions go first ({towerHitsOnTyra} shots)");
            Check(knutBySpecial, "TUT3: Knut falls in the fight moment");
            Check(lineWithoutMarks, "TUT3: Line is the plain text, without the '|' marks");
            Check(minHp >= p.MaxHp * 0.3f, $"TUT3: Tyra stays well on her feet ({minHp:0} of {p.MaxHp:0})");
            Check(m.Time < 60f, $"TUT3: a player who follows the lines is through the arena in under a minute ({m.Time:0.0} s)");
        }

        // The drag lesson: Fenn waits hidden in the side grass, steps out after the tap lesson and stands outside Tyra's ring. A
        // tap fires nothing, an aimed bolt that misses comes back, the aimed bolt that hits him is lesson 8, and only then does
        // Knut come. A player who never drags is not left standing: the game makes the shot.
        static void TutV3Drag()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 7 });
            var tu = m.Tut; var p = m.Player; var f = tu.Hider; var k = tu.Guard;
            string D(string line) => line.Replace("|", "");
            Check(f != null && f.Hero == HeroId.Fenn && f.Team == Teams.Enemy && f.Has(St.Untargetable) && f.Has(St.Disarm), "TUT3 drag: Fenn is there from the start, no target and unarmed");
            Check(m.Map.InGrass(f.X, f.Y) && !m.IsVisibleTo(f, Teams.Player), "TUT3 drag: he waits hidden in the side grass");
            Check(!m.DragAimOn && !tu.DragOn, "TUT3 drag: before the lesson a drag does not aim (the tap is taught alone)");
            bool seenEarly = false;
            while (m.Time < 60f && tu.Step < Tutorial.StepThrow) { m.Step(TutV3ArenaPlayer(m)); if (m.IsVisibleTo(f, Teams.Player)) seenEarly = true; }
            Check(tu.Step == Tutorial.StepThrow && tu.Lesson == Tutorial.IslandLessons + 2, $"TUT3 drag: the lesson follows the tap lesson (step {tu.Step}, lesson {tu.Lesson})");
            Check(!seenEarly, "TUT3 drag: a player who follows the lane does not see Fenn before his lesson");
            Check(m.DragAimOn && tu.DragOn, "TUT3 drag: from the lesson on a drag aims");

            // he walks out of the grass to his spot; no line and no arrow before he is seen, no drag asked for before he stands there
            float t0 = m.Time; bool lineUnseen = false, askedEarly = false;
            while (m.Time - t0 < 8f && !tu.HiderOut)
            {
                m.Step(PlayerInput.None);
                if ((tu.Line != null || tu.PointAt != null) && !m.IsVisibleTo(f, Teams.Player)) lineUnseen = true;
                if (tu.DragAsked && !tu.HiderOut) askedEarly = true;
            }
            Check(tu.HiderOut && m.Time - t0 < 3f, $"TUT3 drag: Fenn is out of the grass in under 3 s ({m.Time - t0:0.0} s)");
            Check(!lineUnseen && !askedEarly, "TUT3 drag: no line or arrow before Fenn is seen, no drag asked for before he stands there");
            Check(!m.Map.InGrass(f.X, f.Y) && m.IsVisibleTo(f, Teams.Player) && !f.Has(St.Untargetable) && SimMath.Hypot(f.X - TutHiderX, f.Y - TutHiderY) < 0.3f,
                $"TUT3 drag: he stands in the open in front of the wall, a target now ({f.X:0.0}, {f.Y:0.0})");
            m.Step(PlayerInput.None);
            Check(p.SpecialReady && tu.DragAsked && tu.PointAt == f && tu.Line == D(LineDrag) && tu.Goal == null, $"TUT3 drag: her special is full, the arrow is on Fenn and the line asks for the drag (line '{tu.Line}')");
            float dist = SimMath.Dist(p, f);
            Check(dist > TapReach(HeroId.Tyra) + f.R && !m.TapHasTarget(p), $"TUT3 drag: from where the tap lesson ended Fenn stands outside her ring ({dist:0.0} tiles)");
            Check(Math.Abs(f.X - p.X) < 4.3f, $"TUT3 drag: he is on her screen ({Math.Abs(f.X - p.X):0.0} tiles to the side)");
            Check(k.Has(St.Untargetable) && Math.Abs(k.Y - TutGuardY) < 0.01f, "TUT3 drag: Knut keeps to his walk by the tower");

            // a tap fires nothing; the line turns to how a drag is made
            var tap = PlayerInput.None; tap.Special = true;
            m.Step(tap);
            bool cast = false, denied = false;
            foreach (var e in m.Events) { if (e.Type == EventType.Cast && e.A == p) cast = true; if (e.Type == EventType.SpecialDenied) denied = true; }
            for (int n = 0; n < 30; n++)
            {
                m.Step(PlayerInput.None);
                foreach (var e in m.Events) { if (e.Type == EventType.Cast && e.A == p) cast = true; if (e.Type == EventType.SpecialDenied) denied = true; }
            }
            Check(!cast && !denied && p.SpecialReady && tu.DragTaps == 1 && tu.Line == D(LineDragHow), $"TUT3 drag: a tap fires nothing and the line says how to drag (taps {tu.DragTaps}, line '{tu.Line}')");

            // an aimed bolt that misses him: the special is back a moment later and the line says so; the lesson goes on
            var miss = PlayerInput.None; miss.Special = true; miss.AimAngle = SimMath.Atan2(f.Y - p.Y, f.X - p.X) + 1.2f; miss.AimK = 1;
            m.Step(miss);
            Check(!p.SpecialReady, "TUT3 drag: a drag casts with nobody in her ring");
            t0 = m.Time;
            while (m.Time - t0 < 3f && !p.SpecialReady) m.Step(PlayerInput.None);
            m.Step(PlayerInput.None);
            Check(p.SpecialReady && m.Time - t0 < TutRefillAfter + 0.2f && f.Alive && f.Hp >= f.MaxHp, $"TUT3 drag: a bolt that missed is back after a moment ({m.Time - t0:0.00} s)");
            Check(tu.Line == D(LineDragMiss) && tu.DragAsked && !tu.BurstDone && tu.Lesson == Tutorial.IslandLessons + 2, $"TUT3 drag: the line says it missed and asks again (line '{tu.Line}', lesson {tu.Lesson})");

            // close to him her arrows find him by themselves, but stop at half his health; a tap still fires nothing
            p.X = p.PX = f.X + 1.5f; p.Y = p.PY = f.Y + 2.5f;
            t0 = m.Time; cast = false; float low = f.Hp;
            while (m.Time - t0 < 6f)
            {
                var i = PlayerInput.None;
                if ((int)Math.Round(m.Time * 60) % 45 == 0) i.Special = true;
                m.Step(i);
                foreach (var e in m.Events) if (e.Type == EventType.Cast && e.A == p) cast = true;
                low = Math.Min(low, f.Hp);
            }
            Check(low < f.MaxHp && Math.Abs(low - f.MaxHp * TutHiderFloor) < 0.01f && f.Alive, $"TUT3 drag: her arrows stop at half his health ({low:0} of {f.MaxHp:0})");
            Check(!cast && !tu.BurstDone && m.TapHasTarget(p), "TUT3 drag: with Fenn inside her ring a tap still fires nothing");

            // the aimed bolt that hits: he falls, lesson 8, a word of praise before the next line
            var hit = PlayerInput.None; hit.Special = true; hit.AimAngle = SimMath.Atan2(f.Y - p.Y, f.X - p.X); hit.AimK = 1;
            m.Step(hit);
            t0 = m.Time;
            while (m.Time - t0 < 2f && f.Alive) m.Step(PlayerInput.None);
            Check(!f.Alive && tu.BurstDone && tu.Lesson == Tutorial.IslandLessons + 3 && Math.Abs(tu.LessonAt - m.Time) < 0.05f, $"TUT3 drag: the aimed bolt takes Fenn down: lesson 8 (alive {f.Alive}, lesson {tu.Lesson})");
            m.Step(PlayerInput.None);
            Check(tu.Line == null && tu.PointAt == null && !tu.DragAsked, $"TUT3 drag: no line while the card praises the shot (line '{tu.Line}')");
            m.Step(PlayerInput.None);
            Check(!k.Has(St.Untargetable) && !k.Has(St.Disarm) && k.MaxHp == TutKnutHp, "TUT3 drag: now Knut is a real enemy");
            t0 = m.Time; bool knutLine = false;
            while (m.Time - t0 < 8f && tu.Step == Tutorial.StepThrow) { m.Step(PlayerInput.None); if (tu.Line == D(LineKnut)) knutLine = true; }
            Check(knutLine && tu.Step == Tutorial.StepBlink, $"TUT3 drag: Knut's line follows and he comes down with his hook (step {tu.Step})");
            t0 = m.Time; bool back = false;
            while (m.Time - t0 < 40f && !tu.Done) { m.Step(TutV3ArenaPlayer(m)); if (f.Alive) back = true; }
            Check(!back && tu.Done && tu.Lesson == Tutorial.AllLessons, $"TUT3 drag: Fenn stays down and the match goes on to the tower (done {tu.Done}, lesson {tu.Lesson})");

            // a player who only ever taps is not left standing: after TutDragMercy s of asking the game makes the shot itself,
            // and that lesson counts as done without a word of praise
            var m2 = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 7 });
            var tu2 = m2.Tut; var f2 = tu2.Hider;
            while (m2.Time < 60f && tu2.Step < Tutorial.StepThrow) m2.Step(TutV3ArenaPlayer(m2, drag: false));
            float praised = tu2.LessonAt; float asked = -1;
            t0 = m2.Time;
            while (m2.Time - t0 < 60f && tu2.Step == Tutorial.StepThrow && !tu2.BurstDone)
            {
                var i = PlayerInput.None;
                if (tu2.DragAsked && asked < 0) asked = m2.Time;
                if (tu2.DragAsked && (int)Math.Round(m2.Time * 60) % 90 == 0) i.Special = true;
                m2.Step(i);
            }
            Check(tu2.BurstDone && tu2.DragShown && !f2.Alive && tu2.Lesson == Tutorial.IslandLessons + 3, $"TUT3 drag: the game makes the shot for a player who never drags (shown {tu2.DragShown}, lesson {tu2.Lesson})");
            Check(asked > 0 && m2.Time - asked >= TutDragMercy && m2.Time - asked < TutDragMercy + 2f, $"TUT3 drag: after {TutDragMercy:0} s of asking ({m2.Time - asked:0.0} s)");
            Check(tu2.LessonAt == praised && tu2.DragTaps > 5, $"TUT3 drag: no word of praise for the game's own shot (taps {tu2.DragTaps})");
            while (m2.Time < 150f && !tu2.Done) m2.Step(TutV3ArenaPlayer(m2, drag: false));
            Check(tu2.Done && tu2.Lesson == Tutorial.AllLessons, $"TUT3 drag: and that player gets through the whole match (done {tu2.Done}, lesson {tu2.Lesson})");

            // Tyra far to the right when the lesson starts: Fenn comes out of the right tuft, mirrored, so he is on her screen
            var m3 = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 7 });
            var tu3 = m3.Tut; var p3 = m3.Player; var f3 = tu3.Hider;
            while (m3.Time < 60f && tu3.Step < Tutorial.StepThrow)
            {
                if (tu3.Step == Tutorial.StepSpecial && tu3.BurstDone) { p3.X = p3.PX = 11f; }
                m3.Step(TutV3ArenaPlayer(m3));
            }
            t0 = m3.Time;
            while (m3.Time - t0 < 8f && !tu3.HiderOut) m3.Step(PlayerInput.None);
            Check(tu3.HiderOut && Math.Abs(f3.X - (m3.Map.W - TutHiderX)) < 0.3f && Math.Abs(f3.Y - TutHiderY) < 0.3f, $"TUT3 drag: with Tyra on the right Fenn comes out on the right ({f3.X:0.0}, {f3.Y:0.0})");
            Check(Math.Abs(f3.X - p3.X) < 4.3f && !m3.Map.InGrass(f3.X, f3.Y), $"TUT3 drag: and is on her screen there ({Math.Abs(f3.X - p3.X):0.0} tiles to the side)");
            while (m3.Time < 150f && !tu3.Done) m3.Step(TutV3ArenaPlayer(m3));
            Check(tu3.Done && !f3.Alive, "TUT3 drag: the mirrored lesson plays through as the other");

            // the rules without drag aim (Rules.DragAimSpecial off): no drag lesson, it counts as done by itself, Fenn never shows
            var m4 = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 7 });
            m4.DragAim = false;
            var tu4 = m4.Tut; bool shown4 = false, asked4 = false;
            while (m4.Time < 150f && !tu4.Done)
            {
                m4.Step(TutV3ArenaPlayer(m4));
                if (m4.IsVisibleTo(tu4.Hider, Teams.Player)) shown4 = true;
                if (tu4.DragAsked || tu4.DragOn) asked4 = true;
            }
            Check(tu4.Done && tu4.Lesson == Tutorial.AllLessons && !shown4 && !asked4 && tu4.Hider.Alive, $"TUT3 drag: without drag aim the lesson is passed over (done {tu4.Done}, lesson {tu4.Lesson}, shown {shown4})");

            // the old tap + drag game (Rules.OneTapSpecial off) has the lesson too
            var m5 = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 7 });
            m5.OneTap = false;
            var tu5 = m5.Tut; bool fenn5 = false;
            while (m5.Time < 150f && !tu5.Done)
            {
                m5.Step(TutV3ArenaPlayer(m5));
                foreach (var e in m5.Events) if (e.Type == EventType.Death && e.B == tu5.Hider && e.A == m5.Player && tu5.Step == Tutorial.StepThrow) fenn5 = true;
            }
            Check(tu5.Done && tu5.Lesson == Tutorial.AllLessons && fenn5, $"TUT3 drag: the old tap + drag game plays the lesson too (done {tu5.Done}, lesson {tu5.Lesson}, Fenn down {fenn5})");
        }

        // BLINK with the joystick at rest goes away from Knut; then only the special finishes him
        static void TutV3Knut()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 9 });
            var tu = m.Tut; var p = m.Player; var k = tu.Guard;
            while (m.Time < 60f && !(tu.Step == Tutorial.StepBlink && tu.Hooked)) m.Step(TutV3ArenaPlayer(m, blink: false));
            Check(tu.Step == Tutorial.StepBlink && tu.Hooked && tu.Line != null && tu.Line.Contains("BLINK"), $"TUT3: the BLINK line comes after Knut's hook (step {tu.Step}, line '{tu.Line}')");
            Check(!p.Has(St.Stun), "TUT3: the BLINK line waits until Tyra can act again");
            float before = SimMath.Dist(p, k);
            var b = PlayerInput.None; b.Spell = true; b.SpellAngle = float.NaN;
            m.Step(b);
            float after = SimMath.Dist(p, k);
            Check(after > before + 2.5f, $"TUT3: BLINK with the joystick at rest dashes away from Knut ({before:0.0} -> {after:0.0} tiles)");
            Check(tu.Lesson == Tutorial.IslandLessons + 4, $"TUT3: the BLINK is lesson 9 ({tu.Lesson})");

            // the fight moment: her special is full, and without it Knut does not fall
            float t0 = m.Time;
            while (m.Time - t0 < 3f && tu.Step != Tutorial.StepFight) m.Step(PlayerInput.None);
            Check(tu.Step == Tutorial.StepFight && p.SpecialReady, $"TUT3: the fight moment starts with the special ready (step {tu.Step}, charge {p.Charge})");
            t0 = m.Time; bool fightLine = false;
            while (m.Time - t0 < 15f)
            {
                m.Step(TutV3ArenaPlayer(m, special: false));
                if (tu.Line == LineFight.Replace("|", "")) fightLine = true;
            }
            Check(k.Alive && k.Hp >= k.MaxHp * TutKnutLast - 0.01f && tu.Step == Tutorial.StepFight, $"TUT3: Knut does not fall to auto attacks in the fight moment (hp {k.Hp:0} of {k.MaxHp:0}, step {tu.Step})");
            Check(fightLine, "TUT3: \"Now finish him with your SPECIAL!\" shows in the fight moment");
            Check(p.Hp >= p.MaxHp * TutPlayerFloor - 30f, $"TUT3: Tyra cannot lose the fight ({p.Hp:0} of {p.MaxHp:0})");
            t0 = m.Time;
            while (m.Time - t0 < 6f && k.Alive) m.Step(TutV3ArenaPlayer(m));
            Check(!k.Alive, $"TUT3: her special finishes Knut (hp {k.Hp:0})");
            Check(tu.Lesson == Tutorial.IslandLessons + 5, $"TUT3: Knut down is lesson 10 ({tu.Lesson})");
            t0 = m.Time;
            while (m.Time - t0 < 10f && tu.Step != Tutorial.StepTower) m.Step(PlayerInput.None);
            Check(tu.Step == Tutorial.StepTower, $"TUT3: on to the tower after a moment of healing (step {tu.Step})");
        }

        // the tower moment against a player who runs in first and stays
        static void TutV3Tower()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 13 });
            var tu = m.Tut; var p = m.Player; var tw = tu.Tower;
            while (m.Time < 90f && tu.Step != Tutorial.StepTower)
            {
                // none of her first minions is left, so the fresh pair starts behind her
                if (tu.Step >= Tutorial.StepFight) foreach (var u in m.Units) if (u.Kind == UnitKind.Minion && u.Team == Teams.Player) u.Alive = false;
                m.Step(TutV3ArenaPlayer(m));
            }
            Check(tu.Step == Tutorial.StepTower, $"TUT3: the tower moment is reached (step {tu.Step})");
            if (tu.Step != Tutorial.StepTower) return;
            Check(tu.Line == LineTowerWait.Replace("|", "") && tu.RingOn, $"TUT3: the tower moment opens with \"let your minions go first\" and a ring to wait in (line '{tu.Line}')");
            Check(SimMath.Hypot(tu.RingX - tw.X, tu.RingY - tw.Y) > tw.Range + p.R + 0.5f, "TUT3: the ring to wait in lies outside the tower's reach");

            // she runs in ahead of her minions: the tower takes her, says so, and her arrows do nothing
            bool shotAtHer = false, youLine = false, hitWhileAimedAt = false; int hits0 = tu.TowerHitsTaken;
            float t0 = m.Time;
            while (m.Time - t0 < 8f)
            {
                m.Step(TutV3ArenaPlayer(m, rush: true));
                bool onHer = tw.Target == p;
                foreach (var e in m.Events) if (e.Type == EventType.Damage && e.A == tw && e.B == p) shotAtHer = true;
                if (tu.Line == LineTowerYou.Replace("|", "")) youLine = true;
                if (onHer && tu.TowerHitsTaken > hits0) hitWhileAimedAt = true;
            }
            Check(shotAtHer, "TUT3: the tower shoots Tyra when she walks into its ring first");
            Check(youLine && tu.RingOn, "TUT3: under fire the line says to step back out, with the ring to step back to");
            Check(!hitWhileAimedAt && tu.TowerHitsTaken == hits0, $"TUT3: her hits do not count while the tower aims at her ({tu.TowerHitsTaken - hits0})");
            Check(p.Hp >= p.MaxHp * TutPlayerFloor - 15f, $"TUT3: under the tower's fire Tyra stays at 40 % ({p.Hp:0} of {p.MaxHp:0})");

            // she steps back out to the ring: the tower turns to her minions, and now she may break it
            t0 = m.Time; bool goLine = false;
            while (m.Time - t0 < 60f && !tu.Done)
            {
                m.Step(TutV3ArenaPlayer(m));
                if (tu.Line == LineTowerGo.Replace("|", "")) goLine = true;
            }
            Check(goLine, "TUT3: with the minions in the ring and Tyra out of the tower's aim the line says to attack");
            Check(tu.Done && tu.TowerHitsTaken == Tutorial.TowerHits, $"TUT3: stepping out and coming back with the minions breaks the tower (done {tu.Done}, hits {tu.TowerHitsTaken})");

            // and the player who never steps out is let go after a while, so nobody is stuck
            var m2 = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 13 });
            var tu2 = m2.Tut;
            while (m2.Time < 90f && tu2.Step != Tutorial.StepTower)
            {
                if (tu2.Step >= Tutorial.StepFight) foreach (var u in m2.Units) if (u.Kind == UnitKind.Minion && u.Team == Teams.Player) u.Alive = false;
                m2.Step(TutV3ArenaPlayer(m2));
            }
            t0 = m2.Time;
            while (m2.Time - t0 < 60f && !tu2.Done) m2.Step(TutV3ArenaPlayer(m2, rush: true));
            Check(tu2.Done, $"TUT3: a player who stays under the tower's fire still gets through ({m2.Time - t0:0.0} s, hits {tu2.TowerHitsTaken})");
        }
    }
}
