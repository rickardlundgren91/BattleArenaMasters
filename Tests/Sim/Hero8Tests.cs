using System;

namespace BAM.Sim
{
    // Øli's four (Hero8.cs): Ragn's Moon Leap and Wolf Run, Nässla's Thorn Thicket and Healing Sap, Skrall's Trance and Thunder
    // Judgement, Ravna's Crow Flock and Crow's Eye; the hero list.
    public sealed partial class Match
    {
        Unit Ally(HeroId h) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Hero == h && Heroes[i].Team == Player.Team && Heroes[i] != Player) return Heroes[i]; return null; }

        static void Hero8()
        {
            HeroList8(); Ragn(); Nassla(); Skrall(); Ravna();
        }

        static void HeroList8()
        {
            foreach (var id in new[] { HeroId.Ragn, HeroId.Nassla, HeroId.Skrall, HeroId.Ravna })
            {
                var info = HeroCatalog.Get(id);
                bool inGame = id == HeroId.Ragn || id == HeroId.Nassla; // in All for everyone since 0.4.0 (Rickard 2026-11-10)
                if (inGame) Check(info.Id == id && !info.Preview && HeroCatalog.InGame((int)id) && Array.Exists(HeroCatalog.HeroTestList, i => i.Id == id), $"{id} is in the game, in HERO TEST too");
                else
                {
                    Check(info.Id == id && info.Preview && HeroCatalog.IndexOf(id) < 0, $"{id} waits in Coming, a preview hero");
                    Check(Array.Exists(HeroCatalog.HeroTestList, i => i.Id == id) == HeroCatalog.PreviewsInHeroTest, $"HERO TEST lists {id} only with PreviewsInHeroTest");
                }
                Check(Defs.Hero(id).Id == id, $"{id} has its own numbers in Defs");
            }
            Check(HeroCatalog.Get(HeroId.Nassla).Name == "Nässla" && HeroCatalog.SpecialIcon(HeroId.Nassla) == "special_nassla", "Nässla shows her name, her icon file is plain ASCII");
            Check(!CanDragAim(HeroId.Skrall) && CanDragAim(HeroId.Ragn) && CanDragAim(HeroId.Nassla) && CanDragAim(HeroId.Ravna), "Skrall is tap only, the other three can aim");
        }

        static void Ragn()
        {
            var m = Make(HeroId.Ragn, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram);
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 4, y); Place(m.Foe(HeroId.Knut), x - 9, y + 9); Place(m.Foe(HeroId.Tikta), x - 9, y - 9);
            Check(Near(TapReach(HeroId.Ragn), RagnS.LeapReach), "his tap looks 5 tiles out");
            Check(m.TapHasTarget(h), "an enemy 4 tiles away is in leap reach");

            h.Charge = h.HDef.MaxCharge; float a0 = a.Hp;
            Check(m.TryTapSpecial(h), "the tap leaps");
            Check(h.RagnLeaping && h.Has(St.Casting) && h.Charge == 0, "in the air: casting, the charge spent");
            for (int i = 0; i < 120 && h.RagnLeaping; i++) { m.Time += Rules.Tick; m.RagnTick(h); }
            Check(!h.RagnLeaping && SimMath.Dist(h, a) <= h.R + a.R + RagnS.Contact, $"he rushed up to the enemy ({h.X:0.00})");
            Check(a.Hp < a0 && a.Has(St.Stun) && a.KnockT > 0, $"the landing hit, stunned and shoved him ({a0 - a.Hp:0})");
            Check(Near(a0 - a.Hp, h.StatDmg() * RagnS.LandCoeff * a.TakenMult, 0.5f), $"2 x his damage ({a0 - a.Hp:0.0})");
            Check(Near(h.ShieldAmount, h.MaxHp * RagnS.ShieldPct, 0.5f) && m.HasEvent(EventType.RagnLand), $"a shield of 20 % on landing ({h.ShieldAmount:0})");

            // the rush does not follow (Rickard 2026-11-10: a good player can dodge an ult): an enemy that steps out of the circle is missed
            Place(h, x, y); Place(a, x + 4, y); a.ClearStatuses(); a.KnockT = 0; a.KnockFreeAt = 0; h.ClearStatuses(); h.Charge = h.HDef.MaxCharge; a0 = a.Hp;
            Check(m.TryTapSpecial(h), "a second rush");
            for (int i = 0; i < 120 && h.RagnLeaping; i++) { m.Time += Rules.Tick; a.Y += 3.5f * Rules.Tick; m.RagnTick(h); }
            Check(a.Hp == a0 && !a.Has(St.Stun) && h.ShieldAmount == 0, $"he stepped {a.Y - y:0.0} tiles sideways and was missed, no shield ({a0 - a.Hp:0})");

            // a landing on nothing still gives the shield, hurts nobody
            Place(a, x + 20, y); Place(h, x, y); m.Events.Clear(); h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, 0, 0.6f), "a dragged leap 3 tiles to the right");
            for (int i = 0; i < 120 && h.RagnLeaping; i++) { m.Time += Rules.Tick; m.RagnTick(h); }
            Check(Near(h.X, x + 3, 0.15f) && h.ShieldAmount == 0 && !m.HasEvent(EventType.Hit), $"he lands 3 tiles on, nobody hit, no shield (Bronson) ({h.X - x:0.00})");

            // a wall stops the leap: aimed into the map's edge, he never ends inside it
            Place(h, 1.2f, y); h.Charge = h.HDef.MaxCharge;
            m.TryCastSpecial(h, SimMath.PI, 1);
            for (int i = 0; i < 120 && h.RagnLeaping; i++) { m.Time += Rules.Tick; m.RagnTick(h); }
            Check(m.Map.Free(h.X, h.Y, h.R), $"the edge stops the leap ({h.X:0.00})");

            // Wolf Run: faster after 3 s out of combat, normal right after a hit
            Place(h, x, y); h.LastCombat = m.Time - 5;
            float run = m.SpeedOf(h);
            h.LastCombat = m.Time;
            float fight = m.SpeedOf(h);
            Check(Near(run, fight * (1 + RagnS.RunBonus), 0.01f), $"Wolf Run: 40 % faster out of combat ({run:0.00} vs {fight:0.00})");

            // death ends a leap
            h.Charge = h.HDef.MaxCharge; Place(a, x + 4, y);
            m.TryCastSpecial(h, 0, 0.8f);
            m.DealDamage(a, h, h.Hp + h.ShieldAmount + 10, DmgKind.Auto);
            Check(!h.Alive && !h.RagnLeaping, "his death ends the leap");
        }

        static void Nassla()
        {
            var m = Make(HeroId.Nassla, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram); var ally = m.Ally(HeroId.Fenn);
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 3, y); Place(ally, x + 3, y + 0.5f); Place(m.Foe(HeroId.Knut), x - 9, y + 9); Place(m.Foe(HeroId.Tikta), x - 9, y - 9);
            Place(m.Ally(HeroId.Moa), x - 9, y);
            ally.Hp = ally.MaxHp * 0.5f; float al0 = ally.Hp, a0 = a.Hp, sp0 = m.SpeedOf(a);
            h.Charge = h.HDef.MaxCharge;
            Check(m.TryTapSpecial(h), "the tap throws the seed pod");
            Proj b = null; foreach (var p in m.Projs) if (p.Mode == ProjMode.Bramble) b = p;
            Check(b != null && b.Zone == ZoneKind.Bramble && Near(b.X, a.X, 0.05f) && b.Tgt == a, "brambles on the enemy");
            Check(Near(m.SpeedOf(a), sp0, 0.02f), "no slow before the first tick");
            for (int i = 0; i < 10; i++) { m.Time += Rules.Tick; a.Y += 0.03f; m.UpdateProjectiles(Rules.Tick); }
            Check(Near(b.Y, y, 0.01f), $"they stay where she aimed, they do not follow him ({b.Y - y:0.00})");
            for (int i = 0; i < 30 && b.Ticks == 0; i++) { m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick); }
            Check(b.Ticks == 1 && Near(m.SpeedOf(a), sp0 * (1 + NasslaS.Slow), 0.02f), $"the first tick slows him 75 % ({m.SpeedOf(a):0.00})");
            float by = b.Y; a.Y += 0.3f;
            int hits = 0;
            for (int i = 0; i < 300; i++) { m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick); }
            Check(Near(b.Y, by, 0.01f), "after the first tick they stay");
            foreach (var e in m.Events) if (e.Type == EventType.Hit && e.B == a && (ProjKind)e.Kind == ProjKind.NasslaBramble) hits++;
            Check(hits == NasslaS.PoisonTicks, $"four poison ticks, one a second ({hits})");
            Check(Near(a0 - a.Hp, h.StatDmg() * NasslaS.PoisonCoeff * a.TakenMult, 1f), $"1.5 x her damage in all ({a0 - a.Hp:0.0})");
            Check(Near(ally.Hp - al0, ally.MaxHp * NasslaS.HealPct * NasslaS.PoisonTicks, 0.5f), $"Healing Sap: the ally got 4 x 2 % ({ally.Hp - al0:0.0})");
            Check(b.Done, "the thicket is gone after 4 s");

            // it ends with her
            h.Charge = h.HDef.MaxCharge; Place(a, x + 3, y);
            m.TryTapSpecial(h);
            m.DealDamage(a, h, h.Hp + 10, DmgKind.Auto);
            m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick);
            bool left = false; foreach (var p in m.Projs) if (p.Mode == ProjMode.Bramble && !p.Done) left = true;
            Check(!left, "the thicket dies with her");
        }

        static void Skrall()
        {
            var m = Make(HeroId.Skrall, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram);
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 9, y); Place(m.Foe(HeroId.Knut), x - 9, y + 9); Place(m.Foe(HeroId.Tikta), x - 9, y - 9);
            h.MovedAt = m.Time - 1;

            // Trance: a step each second standing still with nobody in reach, at most 3
            for (int i = 0; i < 300; i++) { m.Time += Rules.Tick; m.SkrallTick(h, Rules.Tick); }
            Check(h.TranceSteps == SkrallS.Steps && m.HasEvent(EventType.TranceStep), $"5 s still: three steps ({h.TranceSteps})");
            // walking stops the gathering, but keeps what he has
            h.TranceSteps = 1; h.MovedAt = m.Time; m.Time += Rules.Tick * 0.25f; m.SkrallTick(h, Rules.Tick);
            Check(h.TranceSteps == 1 && h.TranceStill == 0, "walking gathers nothing");
            // an enemy in his reach: no trance in a fight
            h.TranceSteps = 0; Place(a, x + 3, y); h.MovedAt = m.Time - 2;
            for (int i = 0; i < 120; i++) { m.Time += Rules.Tick; m.SkrallTick(h, Rules.Tick); }
            Check(h.TranceSteps == 0, "no steps with an enemy in his reach");

            // the full Trance: +120 % on the next attack, which also jumps to another enemy close to the target
            var b = m.Foe(HeroId.Knut); Place(b, x + 4, y + 1);
            h.TranceSteps = 3; m.Events.Clear(); float a0 = a.Hp, b0 = b.Hp;
            h.AtkTimer = 99; m.UpdateAttack(h, Rules.Tick);
            Check(h.TranceSteps == 0, "the attack spends the steps");
            for (int i = 0; i < 60; i++) m.UpdateProjectiles(Rules.Tick);
            var t = a0 - a.Hp > b0 - b.Hp ? a : b; var o = t == a ? b : a; float t0 = t == a ? a0 : b0, o0 = t == a ? b0 : a0; // the target took the whole hit, the other the jump
            float hit = t0 - t.Hp;
            Check(Near(hit, h.StatDmg() * (1 + SkrallS.StepBonus * 3) * t.TakenMult, 0.5f), $"the attack deals 2.2 x ({hit:0.0})");
            Check(Near(o0 - o.Hp, h.StatDmg() * (1 + SkrallS.StepBonus * 3) * SkrallS.JumpCoeff * o.TakenMult, 0.5f), $"and half of it jumps to the other ({o0 - o.Hp:0.0})");

            // Thunder Judgement: three strikes after a red warning, one alone takes all three, heroes stunned
            Place(a, x + 3, y); Place(b, x - 9, y + 9); m.Events.Clear(); a0 = a.Hp;
            h.Charge = h.HDef.MaxCharge; m.Telegraph = true;
            Check(m.TryTapSpecial(h), "the tap calls the strikes");
            int warn = 0, strikes = 0; foreach (var p in m.Projs) { if (p.Mode == ProjMode.WarnCircle && !p.Done) warn++; if (p.Mode == ProjMode.Strike && !p.Done) strikes++; }
            Check(strikes == SkrallS.Strikes && warn == SkrallS.Strikes, $"three strikes, each with a red circle ({strikes}, {warn})");
            Check(m.HasEvent(EventType.Telegraph), "he flashes as every special");
            for (int i = 0; i < 60; i++) { m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick); }
            Check(Near(a0 - a.Hp, 3 * h.StatDmg() * SkrallS.StrikeCoeff * a.TakenMult, 1f), $"the lone enemy took all three ({a0 - a.Hp:0.0})");
            // a step aside dodges
            Place(a, x + 3, y); h.Charge = h.HDef.MaxCharge; a0 = a.Hp;
            m.TryTapSpecial(h);
            a.X += 2;
            for (int i = 0; i < 60; i++) { m.Time += Rules.Tick; m.UpdateProjectiles(Rules.Tick); }
            Check(a.Hp == a0, "stepping out of the red circles dodges them");
        }

        static void Ravna()
        {
            var m = Make(HeroId.Ravna, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; h.Bot = null; var a = m.Foe(HeroId.Bram); var b = m.Foe(HeroId.Knut);
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 4, y); Place(b, x + 2, y + 0.2f); Place(m.Foe(HeroId.Tikta), x - 9, y - 9);
            h.Charge = h.HDef.MaxCharge;
            Check(m.TryCastSpecial(h, 0, 1), "the flock flies to the right");
            Check(h.RavnaFlying && h.Has(St.Untargetable), "as a flock she cannot be hit");
            float h0 = h.Hp; m.DealDamage(a, h, 50, DmgKind.Auto);
            Check(h.Hp == h0, "a hit on the flock does nothing");
            float a0 = a.Hp, b0 = b.Hp;
            for (int i = 0; i < 200 && h.RavnaFlying; i++) { m.Time += Rules.Tick; m.RavnaTick(h, Rules.Tick); }
            Check(!h.RavnaFlying && !h.Has(St.Untargetable) && m.HasEvent(EventType.FlockEnd), "she is herself again");
            Check(Near(h.X - x, RavnaS.FlockReach, 0.2f), $"after 5.5 tiles ({h.X - x:0.00})");
            Check(Near(a0 - a.Hp, h.StatDmg() * a.TakenMult, 0.5f) && Near(b0 - b.Hp, h.StatDmg() * b.TakenMult, 0.5f), $"both enemies on the way torn once ({a0 - a.Hp:0}, {b0 - b.Hp:0})");

            // Crow's Eye: her hit keeps an enemy hero seen for 3 s
            a.RevealT = 0; m.AutoHit(h, a);
            Check(a.RevealT >= RavnaS.MarkTime - 0.01f, $"Crow's Eye: seen 3 s ({a.RevealT:0.0})");
        }
    }
}
