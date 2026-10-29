using System;

namespace BAM.Sim
{
    // A blink or a recall gets a hero out of the shots on their way to it (Rickard 2026-10-29: "skottet ska inte hoppa, man kan
    // blinka sig ur skottläget"): they fly on to the spot it left and end there, no hit, and never jump after it (Combat.LoseShots).
    public sealed partial class Match
    {
        static void Blinks()
        {
            var m = Make(HeroId.Tyra, new[] { HeroId.Bram, HeroId.Fenn }, new[] { HeroId.Tikta, HeroId.Knut, HeroId.Moa });
            var t = m.Player; var f = m.Foe(HeroId.Moa); var tk = m.Foe(HeroId.Tikta);
            foreach (var u in m.Heroes) if (u != t && u != f) Place(u, 1, 1);
            float x = 7.5f, y = 20;

            // an auto arrow in flight, then the target blinks 3 tiles sideways
            foreach (bool inHand in new[] { false, true })
            {
                Place(t, x, y); Place(f, x + 3.5f, y); f.Spell = SpellId.Blink; f.SpellAt = 0;
                m.AutoAttack(t, f);
                Proj a = m.Projs[m.Projs.Count - 1];
                int warm = inHand ? 2 : (int)(t.HDef.SpawnDelay / Rules.Tick) + 4;
                for (int i = 0; i < warm; i++) m.UpdateProjectiles(Rules.Tick);
                Check(!a.Done && (inHand ? a.T < 0 : a.T > 0), $"the arrow is {(inHand ? "still in Tyra's hand" : "on its way")}");
                float ox = f.X, oy = f.Y;
                Check(m.CastSpell(f, SimMath.PI / 2, 1), "the target blinks");
                float worst = 0, px = a.X, py = a.Y;
                for (int i = 0; i < 90 && !a.Done; i++) { m.UpdateProjectiles(Rules.Tick); worst = Math.Max(worst, SimMath.Hypot(a.X - px, a.Y - py)); px = a.X; py = a.Y; }
                Check(a.Done && f.Hp == f.MaxHp, $"{(inHand ? "in the hand" : "flying")}: the arrow misses the blinked hero ({f.MaxHp - f.Hp:0.0} damage)");
                Check(SimMath.Hypot(a.X - ox, a.Y - oy) < 0.1f, $"it ends where the hero stood ({SimMath.Hypot(a.X - ox, a.Y - oy):0.00} tiles off)");
                Check(worst < 0.35f, $"it never jumps after the hero (largest step {worst:0.00} tiles)");
                m.Projs.Clear();
            }

            // Tikta's homing storm needle, then a recall home: the needle flies to the spot and ends there
            Place(tk, x, y); Place(f, x + 2.5f, y); f.Spell = SpellId.Recall; f.SpellAt = 0;
            var n = m.NewProj(ProjMode.Needle, tk, tk.X, tk.Y);
            n.Kind = ProjKind.TiktaNeedleSpecial; n.Tgt = f; n.Speed = TiktaS.Speed; n.Dmg = 10; n.Radius = TiktaS.LineHitbox;
            m.UpdateProjectiles(Rules.Tick);
            float nx = f.X, ny = f.Y;
            m.LoseShots(f); f.X = f.PX = f.SpawnX; f.Y = f.PY = f.SpawnY; // the recall's jump home (SpellTick)
            for (int i = 0; i < 60 && !n.Done; i++) m.UpdateProjectiles(Rules.Tick);
            Check(n.Done && f.Hp == f.MaxHp && SimMath.Hypot(n.X - nx, n.Y - ny) < 0.05f, "a recalled hero's needle ends on the spot it left, no hit");
            m.Projs.Clear();

            // without a blink the arrow still lands (the homing itself is unchanged)
            Place(t, x, y); Place(f, x + 3.5f, y);
            m.AutoAttack(t, f);
            for (int i = 0; i < 90; i++) m.UpdateProjectiles(Rules.Tick);
            Check(f.Hp < f.MaxHp, "no blink: the arrow hits");
        }
    }
}
