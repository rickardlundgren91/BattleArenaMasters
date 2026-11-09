using System;

namespace BAM.Sim
{
    // Kugg (Hero7.cs): the Anchor (rooted, longer reach, faster shots, 6 shots, pack-up, no charge), Spark Shot, death, the hero list.
    public sealed partial class Match
    {
        static void Kugg()
        {
            var m = Make(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var a = m.Foe(HeroId.Bram); var c = m.Foe(HeroId.Knut); var far = m.Foe(HeroId.Tikta);
            h.Bot = null; // the checks drive him by hand
            var info = HeroCatalog.Get(HeroId.Kugg);
            Check(info.Name == "Kugg" && info.Preview && HeroCatalog.IndexOf(HeroId.Kugg) < 0, "Kugg waits in Coming, a preview hero");
            Check(Array.Exists(HeroCatalog.HeroTestList, i => i.Id == HeroId.Kugg) && HeroCatalog.HeroTestList.Length == HeroCatalog.Count + 5 && HeroCatalog.HeroTestList[HeroCatalog.Count].Id == HeroId.Kugg, "HERO TEST lists Kugg (and the four after him) after the heroes in the game");
            float x = 7.5f, y = 20;
            Place(h, x, y); Place(a, x + 6, y); Place(c, x - 9, y + 9); Place(far, x - 9, y - 9);
            float range0 = h.Range, cd0 = h.AttackCd;
            Check(Near(range0, 2.5f), $"his own reach is 2.5 ({range0:0.00})");

            // the tap: an enemy within 8 tiles lights the button; the Anchor roots him with 8 tiles of reach and faster shots
            Check(m.TapHasTarget(h), "an enemy 6 tiles away is a reason to anchor");
            h.Charge = h.HDef.MaxCharge;
            Check(m.TryTapSpecial(h), "the tap anchors him");
            Check(h.KuggAnchored && h.KuggRooted && h.KuggShots == KuggS.Shots && h.Charge == 0, "anchored with 6 shots, the charge spent");
            Check(Near(h.Range, 8f) && Near(h.AttackCd, cd0 * KuggS.CdMult), $"reach 8 ({h.Range:0.00}), 30 % faster shots");
            Check((h.Flags & StatusFlags.Rooted) != 0, "the Anchor shows as rooted");
            Check(!m.TapHasTarget(h) && !m.TryCastSpecial(h, float.NaN, 0), "no second Anchor while anchored");
            float hx = h.X; m.MoveUnit(h, 1, 0, 0.5f);
            Check(h.X == hx, "he cannot walk while anchored");

            // the shots: each spends one, the anchored shell deals 90 %, no charge is gathered; the 6th starts the pack-up
            h.ClearStatuses(); h.Charge = 0;
            int shells = 0; float a0 = a.Hp;
            for (int i = 0; i < 2000 && h.KuggAnchored; i++)
            {
                m.UpdateAttack(h, Rules.Tick);
                m.UpdateProjectiles(Rules.Tick);
                m.Time += Rules.Tick;
            }
            foreach (var e in m.Events) if (e.Type == EventType.AttackStart && e.A == h && (ProjKind)e.Kind == ProjKind.KuggShell) shells++;
            for (int i = 0; i < 60; i++) { m.UpdateProjectiles(Rules.Tick); m.Time += Rules.Tick; }
            Check(shells == KuggS.Shots, $"six anchored shots ({shells})");
            Check(a.Hp < a0 && h.Charge == 0, $"they hit at 6 tiles and give no charge ({a0 - a.Hp:0}, charge {h.Charge})");
            Check(!h.KuggAnchored && h.KuggRooted && h.Has(St.Disarm), "after the last shot he packs up: still rooted, no attacks");
            Check(Near(h.Range, 2.5f), "his reach is back to 2.5");
            m.Time += KuggS.PackTime + 0.05f; m.KuggTick(h, Rules.Tick);
            Check(!h.KuggRooted, "after 1.1 s he can walk again");
            hx = h.X; m.MoveUnit(h, 1, 0, 0.1f);
            Check(h.X > hx, "and he walks");

            // the Anchor ends after 10 s at the latest, even with nothing to shoot
            Place(a, x + 20, y); h.Charge = h.HDef.MaxCharge; h.ClearStatuses();
            Check(m.TryCastSpecial(h, float.NaN, 0), "a cast anchors him again");
            m.Time += KuggS.MaxTime + 0.05f; m.KuggTick(h, Rules.Tick);
            Check(!h.KuggAnchored && h.KuggRooted && m.HasEvent(EventType.KuggPack), "after 10 s with nothing to shoot he packs up");

            // death ends the Anchor
            h.Charge = h.HDef.MaxCharge; h.KuggRooted = false; h.ClearStatuses();
            Check(m.TryCastSpecial(h, float.NaN, 0), "anchored once more");
            m.DealDamage(a, h, h.Hp + 10, DmgKind.Auto);
            Check(!h.Alive && !h.KuggAnchored && !h.KuggRooted, "his death ends the Anchor");

            // Spark Shot: on its time, a small shot at an enemy within 8 tiles for 35 %, without a swing or a charge
            var m2 = Make(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var k = m2.Player; k.Bot = null; var b = m2.Foe(HeroId.Bram);
            Place(k, x, y); Place(b, x + 7, y); Place(m2.Foe(HeroId.Knut), x - 9, y + 9); Place(m2.Foe(HeroId.Tikta), x - 9, y - 9);
            k.Charge = 0; m2.Events.Clear();
            k.KuggSparkAt = m2.Time; m2.KuggTick(k, Rules.Tick);
            bool spark = false; foreach (var e in m2.Events) if (e.Type == EventType.AttackStart && (ProjKind)e.Kind == ProjKind.KuggSpark && e.B == b) spark = true;
            Check(spark, "the Spark Shot fires at the enemy 7 tiles away");
            float nx = k.KuggSparkAt - m2.Time;
            Check(nx >= KuggS.SparkMin - 0.01f && nx <= KuggS.SparkMax + 0.01f, $"the next one comes in 7.5-15 s ({nx:0.0})");
            float b0 = b.Hp;
            for (int i = 0; i < 60; i++) m2.UpdateProjectiles(Rules.Tick);
            Check(Near(b0 - b.Hp, k.StatDmg() * KuggS.SparkCoeff * b.TakenMult, 0.1f) && k.Charge == 0, $"it deals 35 % and gives no charge ({b0 - b.Hp:0.0})");
            Place(b, x + 12, y); m2.Events.Clear(); k.KuggSparkAt = m2.Time; m2.KuggTick(k, Rules.Tick);
            Check(!m2.HasEvent(EventType.AttackStart), "no Spark Shot with nobody within 8 tiles");
        }
    }
}
