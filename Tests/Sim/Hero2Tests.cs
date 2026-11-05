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
            m.Telegraph = false; // the forms' own rules; the warning before a tapped special has its own checks (TelegraphTests.cs)
            return m;
        }
        Unit Foe(HeroId h) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Hero == h && Heroes[i].Team != Player.Team) return Heroes[i]; return null; }
        static void Place(Unit u, float x, float y) { u.X = u.PX = x; u.Y = u.PY = y; u.Hp = u.MaxHp; u.ClearStatuses(); u.KnockFreeAt = -99; } // a fresh setup: no knockback cooldown left over
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
            BlastWalls();
        }

        // BAM wall rule for Chain Reaction (precision audit CHAIN-01/02) and a bot's bomb led into a wall (BLAST-04). The wall
        // column x = 3..4 runs down rows 4..10; (3, 10) is its last tile, so its corner can be seen round.
        static void BlastWalls()
        {
            var m = Make(HeroId.Blast, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var b = m.Player; var e1 = m.Foe(HeroId.Bram); var e2 = m.Foe(HeroId.Knut); var e3 = m.Foe(HeroId.Tikta);
            Place(b, 6.5f, 7.5f); Place(e1, 2.6f, 7.5f); Place(e2, 2.0f, 8.4f); Place(e3, 12, 30);
            m.Ignite(b, e1, 100);
            var bomb = m.NewProj(ProjMode.Bomb, b, 4.1f, 7.5f); bomb.Kind = ProjKind.BlastBomb; bomb.Dmg = 1; bomb.Done = true;
            m.BombBlast(bomb);
            bool any = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.ChainBlast && !p.Done) any = true;
            Check(!any && Near(m.ChainLeft(b), 0), "CHAIN-01: a burning hero behind a wall from the bomb sets off no chain");

            Place(e1, 4.32f, 10.6f); Place(e3, 3.5f, 11.3f); float h3 = e3.Hp;
            var c = m.NewProj(ProjMode.ChainBlast, b, e1.X, e1.Y); c.Kind = ProjKind.BlastChain; c.Tgt = e1; c.Radius = BlastChainS.Radius; c.Dmg = 20; c.Done = true;
            m.ChainBlast(c);
            Check(!m.Map.LosClear(c.X, c.Y, e3.X, e3.Y) && Near(h3, e3.Hp), "CHAIN-02: the chain's blast does not reach round a wall corner");
            Place(e3, 4.32f, 11.5f); h3 = e3.Hp;
            c = m.NewProj(ProjMode.ChainBlast, b, e1.X, e1.Y); c.Kind = ProjKind.BlastChain; c.Tgt = e1; c.Radius = BlastChainS.Radius; c.Dmg = 20; c.Done = true;
            m.ChainBlast(c);
            Check(h3 - e3.Hp > 0, "the chain still hits in the open");

            // the bomb is the drag since 2026-10-30: aimed into the wall at any length, it lands off the wall (BLAST-04)
            Place(e1, 4.35f, 7.5f); Place(e2, 12, 30);
            bool offWall = true; string at = "";
            for (int i = 1; i <= 20; i++)
            {
                b.Charge = b.HDef.MaxCharge; b.BlastChainReadyAt = 99; m.Projs.Clear();
                Check(m.TryCastSpecial(b, SimMath.PI, i / 20f), "a dragged Blast throws the bomb");
                Proj bm = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Bomb && !p.Done && p.Src == b) bm = p;
                if (bm == null || m.Map.WallAt(bm.X, bm.Y)) { offWall = false; at = $"{bm?.X:0.00}, {bm?.Y:0.00}"; }
            }
            Check(offWall, $"BLAST-04: an aimed bomb never lands inside a wall ({at})");

            // the tap is the fire strip, aimed at an enemy in reach with a clear line
            Place(e1, 8.5f, 7.5f); b.Charge = b.HDef.MaxCharge; m.Projs.Clear();
            Check(m.TryCastSpecial(b, float.NaN, 0), "a tapping Blast lays the strip");
            Proj lane = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.FireLane && p.Src == b) lane = p;
            Check(lane != null && Math.Abs(lane.Ang) < 0.05f, $"the tap strip points at the enemy in reach ({lane?.Ang:0.00})");

            // BLAST-05: the explosion's look is drawn only where the blast reaches; on open ground it is the blast itself
            float r = m.BlastLookDisc(7.5f, 14.5f, BlastS.Radius, 0.5f, out float ox, out float oy);
            Check(Near(r, BlastS.Radius) && Near(ox, 7.5f) && Near(oy, 14.5f), "BLAST-05: in the open the look is the blast's own disc");
            int walls = 0;
            foreach (var (bx, by) in new[] { (4.3f, 7.5f), (4.6f, 9.8f), (4.32f, 10.6f), (2.6f, 7.5f) })
            {
                r = m.BlastLookDisc(bx, by, BlastS.Radius, 0.5f, out float lx, out float ly);
                bool ok = r <= BlastS.Radius + 1e-3f && SimMath.Hypot(lx - bx, ly - by) + r <= BlastS.Radius + 1e-3f;
                for (int i = 0; i < 64 && r > 0.5f; i++)
                {
                    float a = i * 2 * (float)Math.PI / 64;
                    for (float f = 0.25f; f <= 1.001f; f += 0.25f)
                    {
                        float qx = lx + SimMath.Cos(a) * r * f, qy = ly + SimMath.Sin(a) * r * f;
                        if (m.Map.WallAt(qx, qy) || !m.Map.LosClear(bx, by, qx, qy)) { ok = false; walls++; }
                    }
                }
                Check(ok && r >= 0.5f, $"BLAST-05: by a wall the look ({lx:0.00}, {ly:0.00}) r {r:0.00} stays where the blast from ({bx}, {by}) reaches");
            }

            // the bomb's fuse ring is cut where its blast stops: round in the open, short toward a wall, never past where it reaches
            var reach = new float[48];
            m.BlastReach(7.5f, 14.5f, BlastS.Radius, reach);
            bool round = true; foreach (var d in reach) round &= Near(d, BlastS.Radius);
            Check(round, "BLAST-05: in the open the fuse is a full circle");
            m.BlastReach(4.6f, 7.5f, BlastS.Radius, reach);
            float west = reach[24]; bool inside = true;
            for (int i = 0; i < reach.Length; i++)
            {
                float a = i * 2 * (float)Math.PI / reach.Length;
                inside &= m.Reaches(4.6f, 7.5f, 4.6f + SimMath.Cos(a) * reach[i], 7.5f + SimMath.Sin(a) * reach[i]);
            }
            Check(west < 0.7f && Near(reach[0], BlastS.Radius) && inside, $"BLAST-05: by a wall the fuse stops at it (toward the wall {west:0.00}, away {reach[0]:0.00})");
        }

        public static int RunHero2Tests()
        {
            Tikta(); TiktaDrag(); TiktaHidden(); Blast(); Bonk(); Hilde(); BramTyra(); Oaken(); Kugg(); Signals(); FennWolf(); Fenn2(); Blinks(); KnockCooldown(); RegenFights(); OneTapChecks(); TowerStopChecks(); TowerLockChecks(); TelegraphChecks();
            Console.WriteLine($"hero tests: {checks - fails}/{checks} passed");
            return fails == 0 ? 0 : 1;
        }
    }
}

static class TestMain { static int Main() => BAM.Sim.Match.RunHero2Tests(); }
