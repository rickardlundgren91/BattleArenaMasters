using System;

namespace BAM.Sim
{
    // Full review 2026-11-13 nr 111: in the guided match's arena half Knut (380 hp) could fall to Tyra's double damage before his
    // hook lesson was over. "He pulled you in! Tap BLINK to dash away!" then stood over an empty lane and the "Now take Knut down!"
    // moment was skipped. Knut now cannot drop below 40 % before the fight moment, as Tyra cannot (Tutorial.cs TutKnutFloor).
    public sealed partial class Match
    {
        static void Review1113()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Seed = 11, Tutorial = true });
            var tu = m.Tut; var p = m.Player; var k = tu?.Guard;
            Check(tu != null && !tu.Island && k != null, "REVIEW 13 NOV: the arena half of the guided match has Knut");
            if (tu == null || k == null) return;

            // the end of moment 3 as UpdateTutorial runs it: Knut becomes a real enemy and Tyra's special is full; no minions, so
            // every hit of hers goes to him
            foreach (var u in m.Units) if (u.Kind == UnitKind.Minion) u.Alive = false;
            m.TutNext(Tutorial.StepThrow); m.StartFight(); p.Charge = p.HDef.MaxCharge;
            if (m.OneTap) { tu.Burst = true; tu.BurstAt = m.Time; }
            float dir = m.Towers[Teams.Player].Y > k.Y ? 1f : -1f;
            Place(p, k.X, k.Y + dir * 2.4f);

            // a player who taps SPECIAL whenever it is ready and never presses BLINK
            PlayerInput Attack() { var i = PlayerInput.None; if (p.SpecialReady) i.Special = true; return i; }
            float t0 = m.Time;
            while (m.Time - t0 < 20f && !(tu.Step == Tutorial.StepBlink && tu.Line != null)) m.Step(Attack());
            Check(tu.Step == Tutorial.StepBlink && tu.Line != null, $"REVIEW 13 NOV: the BLINK line comes up (step {tu.Step}, line '{tu.Line}')");

            // the player waits 12 s with the line up: Knut stays on his feet, so the line never stands over an empty lane
            bool lineWithoutKnut = false, knutFell = false;
            t0 = m.Time;
            while (m.Time - t0 < 12f && tu.Step == Tutorial.StepBlink)
            {
                m.Step(Attack());
                if (!k.Alive) knutFell = true;
                if (tu.Line != null && !k.Alive) lineWithoutKnut = true;
            }
            Check(!knutFell, $"REVIEW 13 NOV: Knut cannot fall before the BLINK lesson is done (hp {k.Hp:0} of {k.MaxHp:0})");
            Check(!lineWithoutKnut, "REVIEW 13 NOV: no BLINK line while Knut is down");
            Check(k.Hp >= k.MaxHp * 0.4f - 0.01f, $"REVIEW 13 NOV: Knut is held at 40 % or more until the fight ({k.Hp:0} of {k.MaxHp:0})");

            // BLINK: the fight moment comes with its own line, and now Knut can be taken down
            var b = Attack(); b.Spell = true; m.Step(b);
            bool fightLine = false;
            t0 = m.Time;
            while (m.Time - t0 < 30f && k.Alive)
            {
                m.Step(Attack());
                if (tu.Step == Tutorial.StepFight && tu.Line == LineFight) fightLine = true;
            }
            Check(fightLine, "REVIEW 13 NOV: \"Now take Knut down!\" shows after the BLINK");
            Check(!k.Alive, $"REVIEW 13 NOV: Knut falls in the fight moment (hp {k.Hp:0})");
            t0 = m.Time;
            while (m.Time - t0 < 3f && tu.Step == Tutorial.StepFight) m.Step(Attack());
            Check(tu.Step == Tutorial.StepHeal || tu.Step == Tutorial.StepTower, $"REVIEW 13 NOV: on to healing or the tower after Knut (step {tu.Step})");
        }
    }
}
