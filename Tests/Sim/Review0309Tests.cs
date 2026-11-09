using System;

namespace BAM.Sim
{
    // The review before 0.3.9 (2026-11-09): nr 57, poison and burn ticks show the hero they tick on in grass (Rickard "dots ska man
    // synas av i gräs"); nr 59, no battle spell during a special's warning, Tikta's too, who walks in hers without St.Casting.
    public sealed partial class Match
    {
        static void Review0309() { DotRevealChecks(); WarningSpellChecks(); }

        static void DotRevealChecks()
        {
            var m = Make(HeroId.Tikta, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Bonk });
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram);
            float gx = -1, gy = -1;
            for (int y = 2; y < m.Map.H - 2 && gx < 0; y++)
                for (int x = 1; x < m.Map.W - 1 && gx < 0; x++)
                {
                    float sx = x + 0.5f, sy = y + 0.5f;
                    if (m.Map.InGrass(sx, sy) && m.Map.Free(sx, sy, 0.4f)) { gx = sx; gy = sy; }
                }
            Check(gx >= 0, "REVIEW-57: the arena has grass to hide in");
            Place(a, gx, gy); Place(h, gx + 6, gy); a.RevealT = 0;
            m.DealDamage(h, a, 5, DmgKind.Auto);
            Check(a.RevealT <= 0, "REVIEW-57: a plain hit in grass reveals nobody (0.3.8)");
            m.DealDamage(h, a, 5, DmgKind.Special, DmgFlags.DotTick);
            Check(Near(a.RevealT, Rules.RevealTime), $"REVIEW-57: a poison tick shows the poisoned hero in grass ({a.RevealT:0.0})");
            a.RevealT = 0;
            m.DealDamage(h, a, 5, DmgKind.Passive, DmgFlags.Burning);
            Check(Near(a.RevealT, Rules.RevealTime), $"REVIEW-57: a burn tick shows the burning hero in grass ({a.RevealT:0.0})");
            // Tikta's own poison on a hidden hero: it ticks every half second, so he stays seen the whole time
            a.RevealT = 0; m.TiktaPoison(h, a, 10, TiktaS.PoisonDuration);
            bool seen = true; int ticks = (int)Math.Round(TiktaS.PoisonDuration / Rules.Tick) - 1;
            for (int i = 0; i < ticks; i++)
            {
                m.Time += Rules.Tick; m.DotTick(a, Rules.Tick); a.RevealT = Math.Max(0, a.RevealT - Rules.Tick);
                if (i > 40 && a.RevealT <= 0) seen = false;
            }
            Check(seen && a.Hp < a.MaxHp, "REVIEW-57: Tikta's poison keeps a hidden hero seen while it ticks");
        }

        static void WarningSpellChecks()
        {
            var m = Make(HeroId.Tikta, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Bonk });
            m.Telegraph = true;
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram);
            foreach (var u in m.Heroes) if (u != h && u != a) Place(u, 1, 1);
            Place(h, 7.5f, 20); Place(a, 9.5f, 20);
            h.Charge = h.HDef.MaxCharge; h.Spell = SpellId.Blink; h.SpellAt = 0;
            Check(m.SpellReady(h) && m.TeleOn, "REVIEW-59: Blink is ready and tapped specials warn first");
            Check(m.TryTapSpecial(h) && h.TeleAt >= 0 && !h.Has(St.Casting), "REVIEW-59: Tikta's tap warns and she is free to walk (no Casting)");
            float x0 = h.X;
            Check(!m.CastSpell(h, 0, 1) && Near(h.X, x0), "REVIEW-59: no Blink during her warning");
            m.Time = h.TeleAt + 1e-3f; m.TelegraphTick(h);
            Check(h.TeleAt < 0, "REVIEW-59: the warning is over, the needles went");
            h.ClearStatuses();
            // 0.4.0 (full review 2026-11-09 nr 64): not while the four needles still leave her hand either; then it works again
            Check(!m.CastSpell(h, 0, 1) && Near(h.X, x0), "REVIEW-59 / FIX 64: no Blink while her needles still leave her hand");
            int n = 0; while (m.NeedleInHand(h) && n++ < 60) { m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick); }
            Check(m.CastSpell(h, 0, 1) && h.X > x0 + 0.5f, $"REVIEW-59: after the warning and the needles' release Blink works again ({h.X - x0:0.0}, {n} ticks)");
        }
    }
}
