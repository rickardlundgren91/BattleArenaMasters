using System;

namespace BAM.Sim
{
    // The nine items of 2026-11-18 (Rickard; his card "Alla som items"; Items.cs): Brimming Edge, Last Stand, Rune Battery, Trophy
    // Chain, Cloak of Invisibility, Iron Plate, Divine Protection, Keen Edge, Quick Gloves. And the offers at levels 5 and 10.
    public sealed partial class Match
    {
        static void Items1118()
        {
            float x = 7.5f, y = 20;
            Check(ItemS.OfferLevels[0] == 5 && ItemS.OfferLevels[1] == 10, "items are offered at levels 5 and 10");
            for (int k = 12; k <= 20; k++) Check(ItemCatalog.Get((ItemId)k) != null, $"item {(ItemId)k} is in the catalog");

            Match M(out Unit p, out Unit fo)
            {
                var m = MakeQuiet(HeroId.Tyra, new[] { HeroId.Bram }, new[] { HeroId.Knut, HeroId.Moa });
                p = m.Player; fo = m.Foe(HeroId.Knut);
                Place(m.Foe(HeroId.Moa), x, y + 8); Place(m.Ally(HeroId.Bram), x - 4, y + 8); Place(p, x, y); Place(fo, x + 2, y);
                return m;
            }

            // Brimming Edge
            {
                var m = M(out var p, out var fo);
                p.Charge = 0; float plain = m.DealDamage(p, fo, 50, DmgKind.Auto); Place(fo, x + 2, y);
                Give(p, ItemId.Brimming);
                float low = m.DealDamage(p, fo, 50, DmgKind.Auto); Place(fo, x + 2, y);
                p.Charge = p.HDef.MaxCharge;
                float full = m.DealDamage(p, fo, 50, DmgKind.Auto);
                Check(Near(low, plain, 0.05f) && Near(full, plain * 1.2f, 0.05f), $"Brimming Edge: +20 % only with a full SPECIAL ({plain:0.0} / {low:0.0} / {full:0.0})");
            }
            // Last Stand
            {
                var m = M(out var p, out var fo);
                Give(p, ItemId.LastStand);
                m.DealDamage(fo, p, p.MaxHp * 5, DmgKind.Special);
                Check(p.Alive && Near(p.Hp, 1, 0.01f) && p.Has(St.Undying), "Last Stand: a killing hit leaves 1 health");
                m.DealDamage(fo, p, 500, DmgKind.Special);
                Check(p.Alive, "Last Stand: no dying while it lasts");
                for (int i = 0; i < (int)((ItemS.LastStandTime + 0.1f) / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(!p.Has(St.Undying), "Last Stand: it ends after 4 s");
                m.DealDamage(fo, p, p.MaxHp * 5, DmgKind.Special);
                Check(!p.Alive, "Last Stand: on cooldown a killing hit kills");
            }
            // Rune Battery
            {
                var m = M(out var p, out var fo);
                Place(fo, x, y + 8); p.Charge = 0; Give(p, ItemId.Battery);
                for (int i = 0; i < (int)(16.5f / Rules.Tick); i++) m.Step(PlayerInput.None);
                Check(Near(p.Charge, 2, 0.01f), $"Rune Battery: a charge every 8 s ({p.Charge} after 16.5 s)");
            }
            // Trophy Chain
            {
                var m = M(out var p, out var fo); var fo2 = m.Foe(HeroId.Moa);
                Give(p, ItemId.Trophy); var al = m.Ally(HeroId.Bram); Give(al, ItemId.Trophy);
                Place(fo2, x + 3, y);
                m.DealDamage(al, fo, 1, DmgKind.Auto); // Bram's hit: an assist
                m.DealDamage(p, fo, fo.MaxHp * 5, DmgKind.Special);
                int ti = p.ItemIndex(ItemId.Trophy), ta = al.ItemIndex(ItemId.Trophy);
                Check(p.Items[ti].Stacks == 1 && al.Items[ta].Stacks == 1, "Trophy Chain: a stack for the kill and for the assist");
                float hit = m.DealDamage(p, fo2, 50, DmgKind.Auto); p.Items[ti].Stacks = 0; Place(fo2, x + 3, y);
                float plain = m.DealDamage(p, fo2, 50, DmgKind.Auto);
                Check(Near(hit, plain * 1.05f, 0.05f), $"Trophy Chain: +5 % a stack ({plain:0.0} -> {hit:0.0})");
                p.Items[ti].Stacks = 3; m.DealDamage(fo2, p, p.MaxHp * 5, DmgKind.Special);
                Check(p.Items[ti].Stacks == 0, "Trophy Chain: all lost on death");
            }
            // Cloak of Invisibility
            {
                var m = M(out var p, out var fo);
                Give(p, ItemId.Cloak);
                var use = PlayerInput.None; use.ItemUse = 1;
                m.Step(use);
                Check(p.Has(St.Veil) && !m.IsVisibleTo(p, fo.Team), "Cloak: a tap on its icon makes the holder unseen");
                Check(m.SpellLeft(p) >= 0 && p.Items[0].ReadyAt > m.Time + ItemS.CloakCd - 1, "Cloak: the cooldown runs");
                m.DealDamage(p, fo, 10, DmgKind.Auto);
                Check(!p.Has(St.Veil), "Cloak: a hit ends it");
            }
            // Iron Plate
            {
                var m = M(out var p, out var fo);
                float plain = m.DealDamage(fo, p, 40, DmgKind.Auto); Place(p, x, y);
                Give(p, ItemId.Plate);
                float cut = m.DealDamage(fo, p, 40, DmgKind.Auto);
                Check(Near(cut, plain * 0.75f, 0.05f), $"Iron Plate: 25 % less ({plain:0.0} -> {cut:0.0})");
            }
            // Divine Protection
            {
                var m = M(out var p, out var fo);
                Give(p, ItemId.Divine);
                Check(m.UseItem(p, 0), "Divine Protection: used");
                Check(m.DealDamage(fo, p, 100, DmgKind.Special) == 0 && m.AddStatus(p, St.Stun, 1, 0, 0, fo) < 0, "Divine Protection: no damage, no stun");
                Check(!m.UseItem(p, 0), "Divine Protection: not again on cooldown");
            }
            // Keen Edge
            {
                var m = M(out var p, out var fo);
                Give(p, ItemId.Keen);
                int crits = 0, n = 600;
                for (int i = 0; i < n; i++) if (m.KeenMult(p, fo) > 1) crits++;
                Check(Math.Abs(crits / (float)n - ItemS.KeenChance) < 0.06f, $"Keen Edge: about 35 % crits ({crits * 100f / n:0} %)");
            }
            // Quick Gloves
            {
                var m = M(out var p, out var fo);
                float a0 = p.AtkHaste(); Give(p, ItemId.Gloves);
                Check(Near(p.AtkHaste(), a0 + 0.3f), "Quick Gloves: +30 % attack speed");
            }
            // bots use the button items
            {
                var m = M(out var p, out var fo);
                var b = m.Ally(HeroId.Bram); var keep = b.Bot;
                m.State = MatchState.Play;
                var mb = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Bram }, Enemies = new[] { HeroId.Knut, HeroId.Moa }, Seed = 7, PlayerIsBot = true });
                mb.State = MatchState.Play; mb.Time = 10;
                var bb = mb.Player; Give(bb, ItemId.Divine);
                bb.Hp = bb.MaxHp * 0.2f; bb.LastHurt = mb.Time;
                mb.ItemsTick(bb);
                Check(bb.Has(St.Invulnerable), "a bot uses Divine Protection when a fight goes badly");
            }
        }
    }
}
