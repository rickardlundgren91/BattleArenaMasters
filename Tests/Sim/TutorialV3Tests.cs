using System;

namespace BAM.Sim
{
    // The guided match after the tutorial review of 2026-11-15 (Rickard: "snygg, sammanhängande och lärorik"): every lesson is done
    // by the player. The island: the first minion must be walked into Tyra's reach, a minion hides in the grass and then Tyra does.
    // The arena: BLINK with the joystick at rest goes away from Knut, only her special finishes him, and the tower shoots: her
    // hits count only while it is busy with her minions. Ten lessons fill the guide card's ten dots (Tutorial.Lesson).
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

        // a player who does what the arena's lines say; rush = straight into the tower's ring at the tower moment, and stays
        static PlayerInput TutV3ArenaPlayer(Match m, bool rush = false, bool blink = true, bool special = true)
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
            if (blink && tu.Step == Tutorial.StepBlink && tu.Hooked && m.SpellReady(p)) { i.Spell = true; i.SpellAngle = float.NaN; } // a tap, joystick at rest
            return i;
        }

        static void TutorialV3()
        {
            TutV3Island();
            TutV3Arena();
            TutV3Knut();
            TutV3Tower();
        }

        static void TutV3Island()
        {
            var m = Create(new MatchConfig { Tutorial = true, Island = true, Seed = 5 });
            var tu = m.Tut; var p = m.Player;
            Check(tu != null && tu.Island && tu.Lesson == 0 && tu.Lessons == Tutorial.AllLessons, "TUT3: the island starts at lesson 0 of 10");
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

            // lesson 4, Focus: "stand in the ring" until all three are in her reach, then the line names FOCUS (the arrow over the
            // dots, not over the ring) while her hits land on them at random
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
            Check(focusSecond && focusArrows, "TUT3: with all three in reach the line names FOCUS and the arrow leaves the ring for the dots");
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
            while (m.Time - t0 < 90f && !tu.Done) m.Step(TutV3IslandPlayer(m));
            Check(tu.Done && tu.Lesson == Tutorial.IslandLessons, $"TUT3: the island ends on the pad with its five lessons done (done {tu.Done}, lesson {tu.Lesson}, step {tu.Step})");
            Check(m.Time < 60f, $"TUT3: a player who follows the marks is through the island in under a minute ({m.Time:0.0} s)");
        }

        static void TutV3Arena()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 5 });
            var tu = m.Tut; var p = m.Player; var tw = tu.Tower;
            Check(tu.Lesson == Tutorial.IslandLessons && tu.Lessons == Tutorial.AllLessons, "TUT3: the arena half goes on from lesson 5 of 10");
            Check(tw.Has(St.Untargetable) && tw.Has(St.Disarm), "TUT3: before its moment the enemy tower neither shoots nor is a target");
            int towerHitsOnTyra = 0; bool knutBySpecial = false, lineWithoutMarks = true; float minHp = p.Hp; int lessonAtTower = -1;
            while (m.State != MatchState.Over && !tu.Done && m.Time < 120f)
            {
                var k = tu.Guard;
                m.Step(TutV3ArenaPlayer(m));
                foreach (var e in m.Events)
                {
                    if (e.Type == EventType.Damage && e.B == p && e.A == tw) towerHitsOnTyra++;
                    if (e.Type == EventType.Death && e.B == k && e.A == p && tu.Step == Tutorial.StepFight) knutBySpecial = true;
                }
                if (tu.Line != null && tu.Line.Contains("|")) lineWithoutMarks = false;
                if (tu.Step == Tutorial.StepTower && lessonAtTower < 0) lessonAtTower = tu.Lesson;
                minHp = Math.Min(minHp, p.Hp);
            }
            Check(tu.Done && m.State == MatchState.Over && m.Winner == Teams.Player, $"TUT3: a player who follows the lines breaks the tower (done {tu.Done}, step {tu.Step}, {m.Time:0.0} s)");
            Check(tu.Lesson == Tutorial.AllLessons && lessonAtTower == Tutorial.AllLessons - 1, $"TUT3: all ten lessons are done at the end, nine when the tower moment starts ({tu.Lesson}, {lessonAtTower})");
            Check(tu.TowerHitsTaken == Tutorial.TowerHits, $"TUT3: the tower falls to exactly {Tutorial.TowerHits} of Tyra's hits ({tu.TowerHitsTaken})");
            Check(towerHitsOnTyra == 0, $"TUT3: the tower never shoots a player who lets the minions go first ({towerHitsOnTyra} shots)");
            Check(knutBySpecial, "TUT3: Knut falls in the fight moment");
            Check(lineWithoutMarks, "TUT3: Line is the plain text, without the '|' marks");
            Check(minHp >= p.MaxHp * 0.3f, $"TUT3: Tyra stays well on her feet ({minHp:0} of {p.MaxHp:0})");
            Check(m.Time < 60f, $"TUT3: a player who follows the lines is through the arena in under a minute ({m.Time:0.0} s)");
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
            Check(tu.Lesson == Tutorial.IslandLessons + 3, $"TUT3: the BLINK is lesson 8 ({tu.Lesson})");

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
            Check(tu.Lesson == Tutorial.IslandLessons + 4, $"TUT3: Knut down is lesson 9 ({tu.Lesson})");
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
