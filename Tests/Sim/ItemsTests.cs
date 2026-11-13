using System;

namespace BAM.Sim
{
    // The six match items (Items.cs; Rickard 2026-11-11): offers at levels 6 and 12, the picks, and each item's rule.
    public sealed partial class Match
    {
        static Match ItemMatch(int seed)
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Knut, HeroId.Bonk }, Enemies = new[] { HeroId.Bram, HeroId.Tikta, HeroId.Kugg }, Seed = seed, Difficulty = Difficulty.Hard });
            var none = PlayerInput.None;
            while (m.State == MatchState.Countdown) m.Step(in none);
            return m;
        }
        static void Give(Unit h, ItemId id) => h.Items[h.ItemCount++] = new ItemSlot { Id = id, ReadyAt = -99 };
        Unit HeroOf(int team, Unit not = null) { for (int i = 0; i < Heroes.Count; i++) if (Heroes[i].Team == team && Heroes[i] != not) return Heroes[i]; return null; }

        static void ItemChecks()
        {
            // ---------- the offers and the picks ----------
            var m = ItemMatch(21); var none = PlayerInput.None; var p = m.Player;
            while (m.Level < 6) { p.Hp = p.MaxHp; m.Step(in none); }
            bool all3 = true, distinct = true;
            for (int i = 0; i < m.Heroes.Count; i++)
            {
                var h = m.Heroes[i]; if (h.OfferCount != 3) all3 = false;
                if (h.Offer[0] == h.Offer[1] || h.Offer[0] == h.Offer[2] || h.Offer[1] == h.Offer[2]) distinct = false;
            }
            Check(all3 && distinct, "level 6: every hero is offered three different items");
            for (int k = 0; k < 60 * 5; k++) { p.Hp = p.MaxHp; m.Step(in none); }
            int botsWith = 0; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != p && m.Heroes[i].ItemCount == 1 && m.Heroes[i].OfferCount == 0) botsWith++;
            Check(botsWith == 5, $"every bot took one item within 4 s ({botsWith}/5)");
            Check(p.ItemCount == 0 && p.OfferCount == 3, "the player's offer waits for the player");
            var want = p.Offer[1]; var pick = PlayerInput.None; pick.ItemPick = 2;
            pick = MatchTrace.Quantize(pick);
            Check(pick.ItemPick == 2, "the pick survives the wire packing");
            m.Step(in pick);
            Check(p.ItemCount == 1 && p.Items[0].Id == want && p.OfferCount == 0, "ItemPick 2 takes the second item");
            m.Step(in pick);
            Check(p.ItemCount == 1, "a pick with no offer does nothing");
            m.Time = Rules.SecondsPerLevel * 11; m.Step(in none); // straight to level 12
            Check(p.OfferCount == 3 && !p.HasItem(p.Offer[0]) && !p.HasItem(p.Offer[1]) && !p.HasItem(p.Offer[2]), "level 12: a new offer without the item already held");
            for (int k = 0; k < 60 * 5; k++) { p.Hp = p.MaxHp; m.Step(in none); }
            botsWith = 0; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != p && m.Heroes[i].ItemCount == 2 && m.Heroes[i].Items[0].Id != m.Heroes[i].Items[1].Id) botsWith++;
            Check(botsWith == 5, $"every bot holds two different items after level 12 ({botsWith}/5)");
            // a queued offer: a hero that never picked at 6 gets the level-12 offer after its first pick
            var q = ItemMatch(22); var qp = q.Player;
            q.Time = Rules.SecondsPerLevel * 11; q.Step(in none); // from level 1 straight to 12: both offers
            Check(qp.OfferCount == 3 && qp.OfferQueued == 1, "an unpicked offer queues the next");
            pick = PlayerInput.None; pick.ItemPick = 1; q.Step(in pick);
            Check(qp.ItemCount == 1 && qp.OfferCount == 3 && qp.OfferQueued == 0, "after the pick the queued offer shows");
            // HERO TEST (Rickard 2026-11-13): the player's choices for the level set, the dummies none; the guided match none
            var ht = Create(new MatchConfig { PlayerHero = HeroId.Tyra, HeroTest = true, HeroTestLevel = 12, Seed = 3 });
            Check(ht.ItemsOn && ht.Player.OfferCount == 3 && ht.Player.OfferQueued == 1, $"HERO TEST at Lv 12: an offer and a second queued ({ht.Player.OfferCount}, {ht.Player.OfferQueued})");
            Check(ht.HtFoe.OfferCount == 0 && ht.HtAlly.OfferCount == 0, "HERO TEST: the dummies get no offer");
            pick = PlayerInput.None; pick.ItemPick = 2; ht.Step(in pick);
            Check(ht.Player.ItemCount == 1 && ht.Player.OfferCount == 3 && ht.Player.OfferQueued == 0, "HERO TEST: after the pick the queued offer shows");
            var ht7 = Create(new MatchConfig { PlayerHero = HeroId.Ragn, HeroTest = true, HeroTestLevel = 7, Seed = 4 });
            Check(ht7.Player.OfferCount == 3 && ht7.Player.OfferQueued == 0, "HERO TEST at Lv 7: one offer");
            for (int k = 0; k < 60 * 10; k++) ht7.Step(in none);
            Check(ht7.Player.OfferCount == 3 && ht7.Player.ItemCount == 0, "HERO TEST: the offer waits for the player (no bot picks it)");
            var ht5 = Create(new MatchConfig { PlayerHero = HeroId.Ragn, HeroTest = true, HeroTestLevel = 5, Seed = 4 });
            Check(ht5.Player.OfferCount == 0, "HERO TEST at Lv 5: no offer yet");
            var tut = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Tutorial = true, Seed = 3 });
            Check(!tut.ItemsOn, "the guided match has no items");

            // ---------- the rules ----------
            m = ItemMatch(31); var a = m.Player; var ally = m.HeroOf(a.Team, a); var e = m.HeroOf(1 - a.Team);
            // Steadfast Charm
            Give(a, ItemId.Charm);
            int si = m.AddStatus(a, St.Stun, 1f, 0, 0, e);
            Check(si >= 0 && Math.Abs(a.Sts[si].T - 0.75f) < 1e-4f, $"Steadfast Charm: a 1 s stun from an enemy hero lasts 0.75 s ({(si >= 0 ? a.Sts[si].T : -1):0.00})");
            int s2 = m.AddStatus(a, St.Root, 1f, 0, 0, e);
            Check(Math.Abs(a.Sts[s2].T - 0.75f) < 1e-4f, "the same hit's second control is shortened too");
            m.Time += 0.1f;
            int s3 = m.AddStatus(a, St.Stun, 1f, 0, 0, e);
            Check(Math.Abs(a.Sts[s3].T - 1f) < 1e-4f, "on cooldown the next control is not shortened");
            a.ClearStatuses(); a.Items[0] = new ItemSlot { Id = ItemId.Charm, ReadyAt = -99 };
            int s4 = m.AddStatus(a, St.Stun, 1f, 0, 0, ally);
            Check(Math.Abs(a.Sts[s4].T - 1f) < 1e-4f, "an ally's control is not shortened");
            a.ClearStatuses(); a.ItemCount = 0;

            // Quarry Mark: Tyra marks, her ally's special does +20 %
            Give(a, ItemId.Mark);
            m.ItemsOnAutoHit(a, e);
            Check(e.Find(St.QuarryMark) >= 0, "Quarry Mark: an auto hit marks the enemy hero");
            float hp0 = e.Hp; e.ClearStatusesExcept(St.QuarryMark);
            m.DealDamage(a, e, 10, DmgKind.Auto);
            Check(Math.Abs(hp0 - e.Hp - 10) < 1e-3f && e.Find(St.QuarryMark) >= 0, "the marker's own hit does not spend it");
            hp0 = e.Hp; m.DealDamage(ally, e, 10, DmgKind.Special);
            Check(Math.Abs(hp0 - e.Hp - 12) < 1e-3f && e.Find(St.QuarryMark) < 0, $"another ally's hit does +20 % and spends the mark ({hp0 - e.Hp:0.0})");
            a.ItemCount = 0; e.Hp = e.MaxHp;

            // Resonance Core: 4 hits = 1 Echo, max 2; the special spends them
            Give(a, ItemId.Echo);
            for (int k = 0; k < 12; k++) m.ItemsOnAutoHit(a, e);
            Check(a.Echoes == 2, $"Resonance Core: 12 hits store 2 Echoes (max) ({a.Echoes})");
            m.ItemsOnCast(a);
            float sh = a.ShieldAmount;
            Check(a.Echoes == 0 && Math.Abs(sh - a.MaxHp * 0.08f) < 0.01f && a.Find(St.SpeedPct) >= 0, $"the special spends them for an 8 % shield and speed ({sh:0.0})");
            a.ClearStatuses(); a.ItemCount = 0;

            // Thicket Fang: out of a bush arms the next hit: +25 % and a slow
            Give(a, ItemId.Fang);
            a.Items[0].Was = true; // stood in grass last tick
            a.X = m.Towers[a.Team].X; a.Y = m.Towers[a.Team].Y + 2; // not in grass (the base)
            if (m.Map.InGrass(a.X, a.Y)) a.Y += 1;
            m.ItemsTick(a);
            Check(a.FangArmed(m.Time), "Thicket Fang: leaving grass arms it");
            Check(Math.Abs(m.ItemAutoMult(a, e) - 1.25f) < 1e-4f, "the armed hit does +25 %");
            m.ItemsOnAutoHit(a, e);
            Check(!a.FangArmed(m.Time) && e.Find(St.SlowPct) >= 0 && m.ItemAutoMult(a, e) == 1, "the hit spends it and slows");
            a.ItemCount = 0; e.ClearStatuses();

            // Guardian's Thread: enemy special damage on an ally near the holder shields that ally, then the cooldown
            Give(a, ItemId.Thread);
            ally.X = a.X + 1; ally.Y = a.Y; ally.Hp = ally.MaxHp; ally.ClearStatuses();
            m.DealDamage(e, ally, 5, DmgKind.Special);
            Check(Math.Abs(ally.ShieldAmount - ally.MaxHp * 0.12f) < 0.01f, "Guardian's Thread: a 12 % shield after an enemy special");
            ally.ClearStatuses(); m.DealDamage(e, ally, 5, DmgKind.Special);
            Check(ally.ShieldAmount == 0, "not again during the 5 s cooldown");
            m.DealDamage(e, a, 5, DmgKind.Auto);
            Check(a.ShieldAmount == 0, "auto attacks never trigger it");
            a.ItemCount = 0; ally.ClearStatuses();

            // Rally Beacon: falling through 30 % health near the holder, once per team per 6 s
            Give(a, ItemId.Rally);
            ally.Hp = ally.MaxHp * 0.35f; m.DealDamage(e, ally, ally.MaxHp * 0.1f, DmgKind.Auto);
            Check(ally.ShieldAmount > 0 && ally.Find(St.SpeedPct) >= 0, "Rally Beacon: shield and speed when health falls through 30 %");
            ally.ClearStatuses(); ally.Hp = ally.MaxHp * 0.35f; m.DealDamage(e, ally, ally.MaxHp * 0.1f, DmgKind.Auto);
            Check(ally.ShieldAmount == 0, "the team's cooldown holds the second");
            a.ItemCount = 0; ally.ClearStatuses(); ally.Hp = ally.MaxHp;

            // the four plain items (Rickard 2026-11-11): +15 % on minions, +10 % on towers, +5 % on heroes, 10 % lifesteal
            Unit mn = null; for (int i = 0; i < m.Units.Count; i++) if (m.Units[i].Kind == UnitKind.Minion && m.Units[i].Team != a.Team && m.Units[i].Alive) { mn = m.Units[i]; break; }
            Give(a, ItemId.MinionDmg); Give(a, ItemId.HeroDmg);
            Check(mn == null || Math.Abs(m.PlainDamageMult(a, mn) - 1.15f) < 1e-4f, "Minion Skewer: +15 % on minions");
            Check(Math.Abs(m.PlainDamageMult(a, e) - 1.05f) < 1e-4f, "Heroes Bane: +5 % on heroes");
            Check(m.PlainDamageMult(a, m.Towers[1 - a.Team]) == 1, "no tower bonus without Crushing Spanner");
            a.ItemCount = 0; Give(a, ItemId.TowerDmg);
            Check(Math.Abs(m.PlainDamageMult(a, m.Towers[1 - a.Team]) - 1.10f) < 1e-4f && m.PlainDamageMult(a, e) == 1, "Crushing Spanner: +10 % on towers only");
            a.ItemCount = 0; Give(a, ItemId.Lifesteal);
            a.Hp = a.MaxHp * 0.5f; e.Hp = e.MaxHp; e.ClearStatuses(); float ah = a.Hp;
            float took = m.DealDamage(a, e, 20, DmgKind.Auto);
            Check(Math.Abs(a.Hp - ah - took * 0.1f) < 1e-3f && took > 0, $"Parascythe: heals 10 % of the damage dealt ({a.Hp - ah:0.00} of {took:0.0})");
            a.ItemCount = 0;
        }
    }

    public sealed partial class Unit
    {
        internal void ClearStatusesExcept(St keep) { for (int i = StCount - 1; i >= 0; i--) if (Sts[i].Type != keep) RemoveAt(i); }
    }
}
