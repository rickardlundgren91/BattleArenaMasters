using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Tikta 2.0 (Deathmark, Strongest Wins poison, swept line needles) and Blast 2.0 (Chain Reaction): the old TIK-/BLA- criteria
    // (XYZ TiktaDeathmarkTests.cs, BlastChainTests.cs) that apply to BAM. A partial Match, so the tests reach the rules directly.
    public sealed partial class Match
    {
        static int fails, checks;
        static void Check(bool ok, string what) { checks++; if (!ok) { fails++; Console.WriteLine("FAIL " + what); } }
        static bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;

        static Match Make(HeroId player, HeroId[] allies, HeroId[] enemies)
        {
            var m = Create(new MatchConfig { PlayerHero = player, Allies = allies, Enemies = enemies, Seed = 7, PlayerIsBot = true });
            for (int i = 0; i < m.Units.Count; i++) { var u = m.Units[i]; if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; // past the countdown
            m.Events.Clear();
            return m;
        }
        Unit Foe(HeroId h) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Hero == h && Heroes[i].Team != Player.Team) return Heroes[i]; return null; }
        static void Place(Unit u, float x, float y) { u.X = u.PX = x; u.Y = u.PY = y; u.Hp = u.MaxHp; u.ClearStatuses(); }
        bool HasEvent(EventType t) { foreach (var e in Events) if (e.Type == t) return true; return false; }
        float AutoHit(Unit s, Unit t)
        {
            var p = NewProj(ProjMode.Auto, s, s.X, s.Y); p.Tgt = t; p.Dmg = 5; p.OnHit = true; p.Kind = ProjKind.TiktaNeedle; p.Done = true;
            float before = t.Hp; HitWith(p); return before - t.Hp;
        }
        Proj LineNeedle(Unit s, float ang, int cast)
        {
            var p = NewProj(ProjMode.NeedleLine, s, s.X, s.Y);
            p.Kind = ProjKind.TiktaNeedleSpecial; p.Ang = ang; p.Speed = TiktaS.Speed; p.Dmg = 10; p.Radius = TiktaS.LineHitbox; p.TiktaCast = cast;
            return p;
        }

        static void Tikta()
        {
            var m = Make(HeroId.Tikta, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Blast });
            var t = m.Player; var a = m.Foe(HeroId.Bram); var b = m.Foe(HeroId.Knut);
            float y = 20; Place(t, 7.5f, y); Place(a, 7.5f, y + 2); Place(b, 9f, y + 1);

            // TIK: an auto that hurts an enemy hero marks it for 3 s; one mark at a time, not moved, not longer
            m.AutoHit(t, a);
            int i = a.FindBy(St.TiktaMark, t);
            Check(i >= 0 && Near(a.Sts[i].T, TiktaMarkS.Duration), "auto hit marks the hero for 3 s");
            Check((a.Flags & StatusFlags.Marked) != 0, "the marked hero shows the Marked flag");
            Check(m.HasEvent(EventType.TiktaMark), "TiktaMark event");
            a.Sts[i].T = 1; m.AutoHit(t, b); m.AutoHit(t, a);
            Check(b.FindBy(St.TiktaMark, t) < 0, "a second hero is not marked while the first carries it");
            Check(Near(a.Sts[a.FindBy(St.TiktaMark, t)].T, 1), "a new hit does not make the mark longer");

            // TIK: a hit a shield takes whole does not mark
            Place(a, 7.5f, y + 2); m.AddStatus(a, St.Shield, 5, 0, 1000);
            m.AutoHit(t, a);
            Check(a.FindBy(St.TiktaMark, t) < 0, "a hit the shield takes whole does not mark");

            // TIK: the first needle that hits the marked hero takes the mark and adds 50 % of StatDmg to the same hit (swept line)
            Place(a, 7.5f, y + 2); m.AddStatus(a, St.TiktaMark, 3, 0, 0, t);
            int cast = ++t.TiktaCastId;
            var n1 = m.LineNeedle(t, SimMath.PI / 2, cast); m.Events.Clear();
            float hp = a.Hp; m.NeedleLineUpdate(n1, 0.2f); // a 3.2 tile step: the end point is past Bram, only the sweep finds him
            float want = (10 + t.StatDmg() * TiktaMarkS.DamageBonus) * a.TakenMult;
            Check(n1.Done && Near(hp - a.Hp, want), $"needle + Deathmark in one hit: took {hp - a.Hp:0.00}, want {want:0.00}");
            Check(a.FindBy(St.TiktaMark, t) < 0 && m.HasEvent(EventType.TiktaMarkPop), "the needle takes the mark");

            // TIK: at most one bonus per cast, even if the hero is marked again mid-storm
            m.AddStatus(a, St.TiktaMark, 3, 0, 0, t); a.Hp = a.MaxHp;
            var n2 = m.LineNeedle(t, SimMath.PI / 2, cast);
            hp = a.Hp; m.NeedleLineUpdate(n2, 0.2f);
            Check(Near(hp - a.Hp, 10 * a.TakenMult) && a.FindBy(St.TiktaMark, t) >= 0, "a second needle of the same cast gets no bonus and leaves the mark");
            var n3 = m.LineNeedle(t, SimMath.PI / 2, ++t.TiktaCastId); a.Hp = a.MaxHp;
            hp = a.Hp; m.NeedleLineUpdate(n3, 0.2f);
            Check(Near(hp - a.Hp, want), "a new cast gets the bonus again");

            // TIK: invulnerable: no hit, the mark stays
            Place(a, 7.5f, y + 2); m.AddStatus(a, St.TiktaMark, 3, 0, 0, t); m.AddStatus(a, St.Invulnerable, 1);
            Check(m.TiktaMarkBonus(t, a, null) == 0 && a.FindBy(St.TiktaMark, t) >= 0, "invulnerable keeps the mark");

            // TIK: Tikta's death takes her mark off; minions, towers and clones are never marked
            Place(a, 7.5f, y + 2); m.AddStatus(a, St.TiktaMark, 3, 0, 0, t);
            m.Kill(t, a);
            Check(a.FindBy(St.TiktaMark, t) < 0, "Tikta's death removes her mark");
            m.Revive(t); Place(t, 7.5f, y);
            m.TiktaTryMark(t, m.Towers[1], 10);
            Check(m.Towers[1].Find(St.TiktaMark) < 0, "a tower is not marked");

            // TIK: the mark runs out after 3 s
            Place(a, 7.5f, y + 2); m.AutoHit(t, a);
            for (int k = 0; k < (int)(3.1f / Rules.Tick); k++) m.Step(PlayerInput.None);
            Check(a.FindBy(St.TiktaMark, t) < 0, "the mark runs out after 3 s");

            // TIK: Final Sting is gone (no bonus on a low-health target)
            bool big = false; a.Hp = a.MaxHp * 0.1f;
            Check(m.HeroAttackCoeff(t, a, ref big) == 1 && !big, "no Final Sting bonus");

            // Strongest Wins poison
            Place(a, 7.5f, y + 2);
            m.TiktaPoison(t, a, 2, 4); m.TiktaPoison(t, a, 1, 2);
            int pi = a.FindBy(St.Poison, t);
            Check(Near(a.Sts[pi].V, 2) && Near(a.Sts[pi].T, 4), "a weaker poison neither weakens nor shortens");
            a.Sts[pi].T = 1; m.TiktaPoison(t, a, 2, 4);
            Check(Near(a.Sts[pi].T, 4), "the same strength renews its time");
            m.TiktaPoison(t, a, 2, 1);
            Check(Near(a.Sts[pi].T, 4), "the same strength never shortens");
            m.TiktaPoison(t, a, 3, 2);
            Check(Near(a.Sts[pi].V, 3) && Near(a.Sts[pi].T, 2), "a stronger poison replaces with its full time");
        }

        static void Blast()
        {
            var m = Make(HeroId.Blast, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var b = m.Player; var e1 = m.Foe(HeroId.Bram); var e2 = m.Foe(HeroId.Knut); var e3 = m.Foe(HeroId.Tikta);
            float x = 7.5f, y = 20;
            Place(b, x, y - 3); Place(e1, x, y + 0.3f); Place(e2, x + 1.0f, y + 0.3f); Place(e3, x - 1.3f, y - 0.6f);
            m.Ignite(b, e1, 100);
            var bomb = m.NewProj(ProjMode.Bomb, b, x, y); bomb.Kind = ProjKind.BlastBomb; bomb.Dmg = 1; bomb.Done = true;
            m.Events.Clear(); m.BombBlast(bomb);
            Proj chain = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.ChainBlast && !p.Done) chain = p;
            Check(chain != null && chain.Tgt == e1 && m.HasEvent(EventType.ChainSet), "Supernova on a burning hero sets off the chain on him");
            Check(Near(m.ChainLeft(b), BlastChainS.Cooldown), "the 9 s cooldown starts at once");
            if (chain == null) return;
            Check(Near(chain.X, e1.X) && Near(chain.Y, e1.Y) && Near(-chain.T, BlastChainS.Delay), "frozen at the hero's spot, 0.25 s fuse");

            // BLA: the second blast hits the OTHER enemies within 1 tile (+ radius), once, for 50 % StatDmg, lights no burn
            e1.X += 3; // the trigger walks away: the spot stays frozen
            float h1 = e1.Hp, h2 = e2.Hp, h3 = e3.Hp, burn2 = e2.Sts[e2.Find(St.Burn)].Amount;
            float dmg = b.StatDmg() * BlastChainS.Coeff;
            e1.X -= 3; e1.X += 0.2f; // back inside the ring: still never hit, he set it off
            chain.Done = true; m.ChainBlast(chain); // what the shot loop runs when the 0.25 s fuse is over
            Check(Near(h1, e1.Hp), "the trigger hero is not hit by the chain");
            int bi = e2.Find(St.Burn);
            Check(Near(h2 - e2.Hp, dmg * e2.TakenMult), $"the hero beside him takes the chain once: {h2 - e2.Hp:0.0} of {dmg * e2.TakenMult:0.0}");
            Check(bi >= 0 && Near(e2.Sts[bi].Amount, burn2), "the chain lights no new burn");
            Check(Near(h3, e3.Hp), "a hero outside the 1 tile ring is not hit by the chain");

            // the shot loop sets it off after the fuse
            var c2 = m.NewProj(ProjMode.ChainBlast, b, x, y); c2.Tgt = e1; c2.T = -BlastChainS.Delay; c2.Radius = BlastChainS.Radius; c2.Dmg = 1;
            for (int k = 0; k * Rules.Tick < BlastChainS.Delay + 0.05f; k++) m.UpdateProjectiles(Rules.Tick);
            Check(c2.Done, "the chain goes off after its 0.25 s fuse");

            // BLA: on cooldown no new chain; not burning no chain; dead Blast no chain
            Place(e1, x, y + 0.3f); m.Ignite(b, e1, 100);
            int before = m.Projs.Count; m.BombBlast(bomb);
            bool any = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.ChainBlast && !p.Done) any = true;
            Check(!any, "no chain while on cooldown");
            b.BlastChainReadyAt = 0; Place(e1, x, y + 0.3f); Place(e2, x + 1, y + 0.3f); Place(e3, x - 1.3f, y - 0.6f);
            m.BombBlast(bomb); any = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.ChainBlast && !p.Done) any = true;
            Check(!any, "no chain when nobody burns from Blast");
            b.BlastChainReadyAt = 0; m.Ignite(b, e1, 100); b.Alive = false;
            m.BombBlast(bomb); any = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.ChainBlast && !p.Done) any = true;
            Check(!any, "no chain from a dead Blast");
        }

        public static int RunHero2Tests()
        {
            Tikta(); TiktaDrag(); Blast(); Bonk(); Hilde(); BramTyra(); Oaken(); Signals(); FennWolf(); Fenn2();
            Console.WriteLine($"hero tests: {checks - fails}/{checks} passed");
            return fails == 0 ? 0 : 1;
        }
    }
}

static class TestMain { static int Main() => BAM.Sim.Match.RunHero2Tests(); }
