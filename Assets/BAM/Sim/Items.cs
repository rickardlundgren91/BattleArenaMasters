using System;

namespace BAM.Sim
{
    /// <summary>The match items (Rickard 2026-11-11, thread "Lägg in sex items i BAM"): Simon's Items V2 from the old game
    /// (Speltest SimItemsV2.cs, XYZ main cb45566), the six Rickard kept. Sole Target Sigil and Siege Banner are left out.</summary>
    /// 1 was Resonance Core, removed from the game 2026-11-13 (Rickard: "inte rätt item för spelet. ta bort"); the number stays free
    /// so the other items keep their ids on the wire and in match traces.
    public enum ItemId : byte { None = 0, Mark = 2, Fang = 3, Thread = 4, Rally = 5, Charm = 6, Lifesteal = 7, MinionDmg = 8, TowerDmg = 9, HeroDmg = 10, Boots = 11,
        // the nine of 2026-11-18 (Rickard, his card: "Alla som items"; ItemS below)
        Brimming = 12, LastStand = 13, Battery = 14, Trophy = 15, Cloak = 16, Plate = 17, Divine = 18, Keen = 19, Gloves = 20 }

    /// <summary>One item a hero holds and its state. Times are match times (ReadyAt: the item works again from then).</summary>
    public struct ItemSlot
    {
        public ItemId Id;
        public float ReadyAt;
        /// <summary>Thicket Fang: armed until Until; Was = stood in grass
        /// last tick. Steadfast Charm: its speed comes at Until; At = when it last shortened a control.</summary>
        public int Stacks, Hits;
        public float Until, At;
        public bool Was;
    }

    /// <summary>Every item number, in one place (the old game's item data, Speltest SimItemsV2.AddItemsV2).</summary>
    public static class ItemS
    {
        /// <summary>A choice of three at these match levels (Rickard 2026-11-11: "välja av tre items lvl 6 och lvl 12"). The second
        /// one moved from 12 (2:56) to 10 (2:24) on Rickard's choice of 2026-11-16 (a card: "Level 10 (2:24)"): at 12 it came so
        /// late that against Easy bots it was held under 30 s in two matches of five and never came in almost one of five; at 10
        /// the win rates and the match length are the same (12 000 bot matches, bam-work/items/polish/README.md).</summary>
        public static readonly int[] OfferLevels = { 5, 8 }; // 6, 10 -> 5, 10 -> 5, 8 (Rickard 2026-11-18: "ta lvl 5 och 10", then "Items lvl 5 och 8")
        public const int Choices = 3;
        /// <summary>A bot takes its pick 1-4 s after the offer (the old game's timer).</summary>
        public const float BotPickMin = 1, BotPickMax = 4;
        /// <summary>A card's worth to a bot: how often the holder's team won in 12 000 bot matches where every bot picked at random
        /// (bot lab 2026-11-16, Normal/Gold/Hard on both maps, ±0.6-1.9). Real players' picks in 0.4.1 agree at both ends: they took
        /// Parascythe and Rally Beacon nearly every time and Crushing Spanner and Steadfast Charm one time in ten. Hard and Ultra bots
        /// take the card worth most (BotTables.BestCard): that team won 55.9 % against random pickers (4000 Hard matches).</summary>
        public static float BotWorth(ItemId id) => id switch
        {
            ItemId.Lifesteal => 54.1f, ItemId.Rally => 52.8f, ItemId.HeroDmg => 51.2f, ItemId.Thread => 51.0f, ItemId.Boots => 50.6f,
            ItemId.MinionDmg => 49.9f, ItemId.Mark => 49.5f, ItemId.Fang => 49.4f, ItemId.Charm => 49.2f, ItemId.TowerDmg => 49.1f,
            // the nine of 2026-11-18: bot lab 2026-11-18 (BotWorthNew below)
            _ => BotWorthNew(id),
        };
        /// <summary>The odds per rarity (common, rare, epic, legendary), from the old game's itemData.rarityOdds.</summary>
        public static readonly float[] RarityOdds = { 55, 25, 15, 5 };

        // Quarry Mark (rare)
        public const float MarkTime = 1.5f, MarkBonus = 0.20f, MarkCd = 4;
        // Thicket Fang (rare)
        public const float FangArm = 2.5f, FangBonus = 0.25f, FangSlow = -0.30f, FangSlowTime = 0.6f, FangCd = 4;
        // Guardian's Thread (rare)
        public const float ThreadRange = 2.5f, ThreadShield = 0.12f, ThreadTime = 1.25f, ThreadCd = 5;
        // Rally Beacon (legendary): one cooldown for the whole team
        public const float RallyRange = 2.5f, RallyBelow = 0.3f, RallyShield = 0.12f, RallySpeed = 0.12f, RallyTime = 1.25f, RallyCd = 6;
        // Steadfast Charm (common)
        public const float CharmCut = 0.25f, CharmMaxCut = 0.35f, CharmSpeed = 0.10f, CharmSpeedTime = 0.75f, CharmCd = 5;
        // the four plain items (Rickard 2026-11-11: "10% lifesteal, 15% minion dmg, 10% tower dmg, 5% hero dmg"; the old game's
        // Parascythe, Minion Skewer, Crushing Spanner and Heroes Bane, with Rickard's numbers)
        public const float Lifesteal = 0.10f, MinionDmg = 0.15f, TowerDmg = 0.10f, HeroDmg = 0.05f;
        // Swift Boots (common; Rickard 2026-11-13: "item boots 10% movementspeed")
        public const float BootsSpeed = 0.10f;

        // The nine of 2026-11-18 (Rickard: "dmg 20% if you hold your special, survive killing dmg 4 seconds, +1 special charge every 8sec,
        // 5% dmg per hero kill or assist (lose all on death), invis- cloak of invisibility, dmg reduction 25%, invincible- divine protection,
        // crit chance 35%, attackspeed 30%"; then on a card "Alla som items"). Most are ABC2 items (itemData): DamageSpecialFull,
        // SurviveAfterFatalDamage (4 s, +40 % speed, cd 100-80), SpecialChargeOverTime, StackingDamageOnKill (max 10),
        // InvisibilityPotion (4 s, cd 90-50), InvincibilityPotion (4 s, slowed, cd 130-90); cooldowns cut for BAM's 4:30 matches.
        /// <summary>Brimming Edge: +BrimmingDmg damage of every kind while the SPECIAL is fully charged.</summary>
        public const float BrimmingDmg = 0.20f;
        /// <summary>Last Stand: a killing hit leaves 1 health and the hero cannot die for LastStandTime s, LastStandSpeed faster.</summary>
        public const float LastStandTime = 4f, LastStandSpeed = 0.4f, LastStandCd = 90f;
        /// <summary>Rune Battery: one SPECIAL charge every BatteryEvery s.</summary>
        public const float BatteryEvery = 8f;
        /// <summary>Trophy Chain: +TrophyDmg damage per hero kill or assist since the last death, at most TrophyMax of them.</summary>
        public const float TrophyDmg = 0.05f; public const int TrophyMax = 10;
        /// <summary>Cloak of Invisibility (tap its icon): no enemy hero or minion sees the holder for CloakTime s or until it hits something.</summary>
        public const float CloakTime = 4f, CloakCd = 60f;
        /// <summary>Iron Plate: PlateCut less damage taken from everything.</summary>
        public const float PlateCut = 0.25f;
        /// <summary>Divine Protection (tap its icon): no damage and no stun, root or slow for DivineTime s, DivineSlow slower.</summary>
        public const float DivineTime = 4f, DivineSlow = -0.3f, DivineCd = 90f;
        /// <summary>Keen Edge: an auto attack has KeenChance to hit for KeenMult (the number is drawn from the match's seeded Rng).</summary>
        public const float KeenChance = 0.35f, KeenMult = 1.5f;
        /// <summary>Quick Gloves: +GlovesHaste attack speed.</summary>
        public const float GlovesHaste = 0.30f;
        /// <summary>An item with a button: a tap on its icon uses it (PlayerInput.ItemUse).</summary>
        public static bool Active(ItemId id) => id == ItemId.Cloak || id == ItemId.Divine;
        static float BotWorthNew(ItemId id) => id switch
        {
            // holder's team win rate, 3000 Normal bot matches with random picks from all nineteen (±2-5)
            ItemId.Brimming => 46.5f, ItemId.LastStand => 52.9f, ItemId.Battery => 50.0f, ItemId.Trophy => 47.5f, ItemId.Cloak => 50.6f,
            ItemId.Plate => 57.3f, ItemId.Divine => 52.4f, ItemId.Keen => 54.0f, ItemId.Gloves => 57.6f, _ => 50f,
        };
    }

    /// <summary>Names, rarity, icon and text of the items (HUD and menus; the rules read only Rarity).</summary>
    public static class ItemCatalog
    {
        public sealed class Info
        {
            public ItemId Id; public string Name, Icon, Text; public int Rarity;
            /// <summary>One short line for the item choice in the match (Text is the whole rule).</summary>
            public string Short;
        }

        static readonly Info[] all =
        {
            null,
            null, // 1: Resonance Core, removed 2026-11-13
            new Info { Id = ItemId.Mark, Short = $"Mark a hero: the next ally hit deals +{P(ItemS.MarkBonus)}", Name = "Quarry Mark", Rarity = 1, Icon = "item_v2_hunters_mark",
                Text = $"Your attack marks an enemy hero for {ItemS.MarkTime}s. The next hit on it from another ally deals +{P(ItemS.MarkBonus)} damage (cd {ItemS.MarkCd}s)" },
            new Info { Id = ItemId.Fang, Short = $"Leave a bush: your next hit deals +{P(ItemS.FangBonus)} and slows", Name = "Thicket Fang", Rarity = 1, Icon = "item_v2_thicket_fang",
                Text = $"Leaving a bush empowers your next hit on a hero within {ItemS.FangArm}s: +{P(ItemS.FangBonus)} damage and a {P(-ItemS.FangSlow)} slow for {ItemS.FangSlowTime}s (cd {ItemS.FangCd}s)" },
            new Info { Id = ItemId.Thread, Short = $"Enemy SPECIAL hits you or an ally near you: a shield", Name = "Guardian's Thread", Rarity = 1, Icon = "item_v2_guardians_thread",
                Text = $"When an enemy SPECIAL hurts you or an ally within {ItemS.ThreadRange} tiles, they get a {P(ItemS.ThreadShield)} max HP shield for {ItemS.ThreadTime}s (cd {ItemS.ThreadCd}s)" },
            new Info { Id = ItemId.Rally, Short = $"You or an ally near you drops below {P(ItemS.RallyBelow)} HP: shield and speed", Name = "Rally Beacon", Rarity = 3, Icon = "item_v2_rally_beacon",
                Text = $"When you or an ally within {ItemS.RallyRange} tiles drops to {P(ItemS.RallyBelow)} health: a {P(ItemS.RallyShield)} max HP shield and +{P(ItemS.RallySpeed)} move speed for {ItemS.RallyTime}s (once per team every {ItemS.RallyCd}s)" },
            new Info { Id = ItemId.Charm, Short = $"Stuns, roots and slows on you are {P(ItemS.CharmCut)} shorter", Name = "Steadfast Charm", Rarity = 0, Icon = "item_v2_steadfast_charm",
                Text = $"A stun, root, slow or disarm from an enemy hero lasts {P(ItemS.CharmCut)} shorter (max {ItemS.CharmMaxCut}s); when it ends, +{P(ItemS.CharmSpeed)} move speed for {ItemS.CharmSpeedTime}s (cd {ItemS.CharmCd}s)" },
            new Info { Id = ItemId.Lifesteal, Short = $"Heal {P(ItemS.Lifesteal)} of the damage you deal", Name = "Parascythe", Rarity = 3, Icon = "item_bloodBlade",
                Text = $"Heal for {P(ItemS.Lifesteal)} of the damage you deal" },
            new Info { Id = ItemId.MinionDmg, Short = $"+{P(ItemS.MinionDmg)} damage to minions", Name = "Minion Skewer", Rarity = 0, Icon = "item_spear",
                Text = $"+{P(ItemS.MinionDmg)} damage to minions" },
            new Info { Id = ItemId.TowerDmg, Short = $"+{P(ItemS.TowerDmg)} damage to towers", Name = "Crushing Spanner", Rarity = 0, Icon = "item_hammer",
                Text = $"+{P(ItemS.TowerDmg)} damage to towers" },
            new Info { Id = ItemId.HeroDmg, Short = $"+{P(ItemS.HeroDmg)} damage to heroes", Name = "Heroes Bane", Rarity = 2, Icon = "item_dagger",
                Text = $"+{P(ItemS.HeroDmg)} damage to heroes" },
            new Info { Id = ItemId.Boots, Short = $"+{P(ItemS.BootsSpeed)} move speed", Name = "Swift Boots", Rarity = 0, Icon = "item_boots",
                Text = $"+{P(ItemS.BootsSpeed)} move speed, all match long" },
            // the nine of 2026-11-18
            new Info { Id = ItemId.Brimming, Short = $"+{P(ItemS.BrimmingDmg)} damage while your SPECIAL is full", Name = "Brimming Edge", Rarity = 2, Icon = "item_brimming",
                Text = $"+{P(ItemS.BrimmingDmg)} damage while you hold a fully charged SPECIAL" },
            new Info { Id = ItemId.LastStand, Short = $"Survive a killing hit for {ItemS.LastStandTime}s", Name = "Last Stand", Rarity = 2, Icon = "item_laststand",
                Text = $"A killing hit leaves you at 1 health: you cannot die for {ItemS.LastStandTime}s and move +{P(ItemS.LastStandSpeed)} faster (cd {ItemS.LastStandCd}s)" },
            new Info { Id = ItemId.Battery, Short = $"+1 SPECIAL charge every {ItemS.BatteryEvery}s", Name = "Rune Battery", Rarity = 2, Icon = "item_battery",
                Text = $"Your SPECIAL gains a charge every {ItemS.BatteryEvery}s" },
            new Info { Id = ItemId.Trophy, Short = $"+{P(ItemS.TrophyDmg)} damage per kill or assist", Name = "Trophy Chain", Rarity = 3, Icon = "item_trophy",
                Text = $"+{P(ItemS.TrophyDmg)} damage for every hero kill or assist (up to {ItemS.TrophyMax}); you lose them all when you die" },
            new Info { Id = ItemId.Cloak, Short = $"TAP: invisible for {ItemS.CloakTime}s", Name = "Cloak of Invisibility", Rarity = 3, Icon = "item_cloak",
                Text = $"Tap its icon: enemies cannot see you for {ItemS.CloakTime}s or until you hit something (cd {ItemS.CloakCd}s). Towers still see you" },
            new Info { Id = ItemId.Plate, Short = $"Take {P(ItemS.PlateCut)} less damage", Name = "Iron Plate", Rarity = 2, Icon = "item_plate",
                Text = $"You take {P(ItemS.PlateCut)} less damage from everything" },
            new Info { Id = ItemId.Divine, Short = $"TAP: invincible for {ItemS.DivineTime}s", Name = "Divine Protection", Rarity = 2, Icon = "item_divine",
                Text = $"Tap its icon: no damage and no stuns for {ItemS.DivineTime}s, but you move {P(-ItemS.DivineSlow)} slower (cd {ItemS.DivineCd}s)" },
            new Info { Id = ItemId.Keen, Short = $"{P(ItemS.KeenChance)} chance to crit", Name = "Keen Edge", Rarity = 1, Icon = "item_keen",
                Text = $"Your attacks have a {P(ItemS.KeenChance)} chance to deal {ItemS.KeenMult}x damage" },
            new Info { Id = ItemId.Gloves, Short = $"+{P(ItemS.GlovesHaste)} attack speed", Name = "Quick Gloves", Rarity = 1, Icon = "item_gloves",
                Text = $"+{P(ItemS.GlovesHaste)} attack speed, all match long" },
        };
        static string P(float v) => Math.Round(v * 100) + "%";

        public const int Count = 20;
        public static Info Get(ItemId id) => id > ItemId.None && (int)id <= Count ? all[(int)id] : null;
        public static int Rarity(ItemId id) { var i = Get(id); return i != null ? i.Rarity : 0; }
    }

    // ---------------------------------------------------------------- the hero's side
    public sealed partial class Unit
    {
        public const int MaxItems = 2;
        /// <summary>The items this hero holds (ItemCount of them).</summary>
        public readonly ItemSlot[] Items = new ItemSlot[MaxItems];
        public int ItemCount;
        /// <summary>The choice waiting for this hero (OfferCount of them, 0 = none). A second offer while one waits is queued.</summary>
        public readonly ItemId[] Offer = new ItemId[ItemS.Choices];
        public int OfferCount;
        internal int OfferQueued;
        /// <summary>The match time a bot takes its pick (NaN = not set yet).</summary>
        internal float BotPickAt = float.NaN;

        public bool HasItem(ItemId id) { for (int i = 0; i < ItemCount; i++) if (Items[i].Id == id) return true; return false; }
        internal int ItemIndex(ItemId id) { for (int i = 0; i < ItemCount; i++) if (Items[i].Id == id) return i; return -1; }
        /// <summary>Thicket Fang is armed (the HUD shows it to the holder's team).</summary>
        public bool FangArmed(float now) { int i = ItemIndex(ItemId.Fang); return i >= 0 && Items[i].Until > now; }
    }

    // ---------------------------------------------------------------- the rules
    public sealed partial class Match
    {
        /// <summary>Rally Beacon's cooldown per team: it works again from this time, however many of the team hold it.</summary>
        internal readonly float[] RallyReadyAt = { -99, -99 };
        readonly ItemId[] itemPool = new ItemId[ItemCatalog.Count];

        /// <summary>No items in the guided match. HERO TEST has them since 2026-11-13 (Rickard tried the ITEM button there at Lv 7:
        /// "blev inte erbjuden item"): its level never changes, so the player gets the offers of the levels up to the one set at the
        /// start (HeroTest.cs HtOffers), the dummies none.</summary>
        public bool ItemsOn => Tut == null;

        static bool RealHero(Unit u) => u != null && u.Kind == UnitKind.Hero && !u.IsClone && u.HeroIndex >= 0;

        // ---------- the offer ----------
        /// <summary>The match reached level lvl (Step): an offer for every real hero at the offer levels.</summary>
        void ItemLevelUp(int from, int to)
        {
            if (!ItemsOn) return;
            for (int l = from + 1; l <= to; l++)
            {
                if (Array.IndexOf(ItemS.OfferLevels, l) < 0) continue;
                for (int i = 0; i < Heroes.Count; i++)
                {
                    var h = Heroes[i];
                    if (h.OfferCount > 0) { h.OfferQueued++; continue; }
                    RollOffer(h);
                }
            }
        }

        /// <summary>Swift Boots: +10 % to the holder's percent speed changes (Movement.SpeedOf, BotSense.SpeedOf). Fenn's decoy walks
        /// with its owner's boots, so it cannot be told apart by its pace.</summary>
        internal static float ItemSpeedPct(Unit u)
        {
            if (u.Kind != UnitKind.Hero) return 0;
            var h = u.IsClone && u.Owner != null ? u.Owner : u;
            return h.ItemCount > 0 && h.HasItem(ItemId.Boots) ? ItemS.BootsSpeed : 0;
        }

        /// <summary>Three items the hero does not hold, weighted by rarity, from all ten: every hero can be offered every item
        /// (Rickard 2026-11-13, full review nr 113: "alla heroes ska kunna få alla items"; the old game's role filter is gone).</summary>
        void RollOffer(Unit h)
        {
            int pn = 0;
            for (int k = 1; k <= ItemCatalog.Count; k++)
            {
                var id = (ItemId)k;
                if (ItemCatalog.Get(id) == null || h.HasItem(id)) continue; // a removed item's free number
                itemPool[pn++] = id;
            }
            int n = 0;
            while (n < ItemS.Choices && pn > 0)
            {
                float tot = 0;
                for (int i = 0; i < pn; i++) tot += ItemS.RarityOdds[ItemCatalog.Rarity(itemPool[i])];
                float r = Rng.Float() * tot; int pick = 0;
                while (pick < pn - 1 && r > ItemS.RarityOdds[ItemCatalog.Rarity(itemPool[pick])]) { r -= ItemS.RarityOdds[ItemCatalog.Rarity(itemPool[pick])]; pick++; }
                h.Offer[n++] = itemPool[pick];
                itemPool[pick] = itemPool[--pn];
            }
            h.OfferCount = n; h.BotPickAt = float.NaN;
        }

        /// <summary>The hero takes choice i of its offer (a seat's PlayerInput.ItemPick, or a bot). A queued offer comes next.</summary>
        internal void PickItem(Unit h, int i)
        {
            if (h == null || i < 0 || i >= h.OfferCount) return;
            var id = h.Offer[i];
            h.OfferCount = 0; h.BotPickAt = float.NaN;
            if (!h.HasItem(id) && h.ItemCount < Unit.MaxItems)
            {
                h.Items[h.ItemCount++] = new ItemSlot { Id = id, ReadyAt = -99 };
                Emit(EventType.ItemPicked, h, null, 0, (byte)id);
            }
            if (h.OfferQueued > 0) { h.OfferQueued--; RollOffer(h); }
        }

        /// <summary>Bots (and a seat a bot took over) pick 1-4 s after the offer, as in the old game: Easy and Normal at random, Hard and
        /// Ultra the card worth most to bots (BotCard). Runs for dead heroes too, so a bot that died meanwhile still gets its item.</summary>
        void BotItemPicks()
        {
            if (!ItemsOn) return;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var h = Heroes[i];
                if (h.OfferCount == 0 || h.Bot == null) continue;
                if (float.IsNaN(h.BotPickAt)) { h.BotPickAt = Time + Rng.Range(ItemS.BotPickMin, ItemS.BotPickMax); continue; }
                if (Time >= h.BotPickAt) PickItem(h, BotCard(h));
            }
        }

        /// <summary>The card a bot takes: at random (a beginner's pick), or from Hard up the one worth most to bots (ItemS.BotWorth).</summary>
        int BotCard(Unit h)
        {
            if (!BotTables.BestCard[BotTables.Index(h.Bot.Tier)]) return Rng.Int(h.OfferCount);
            int b = 0;
            for (int i = 1; i < h.OfferCount; i++) if (ItemS.BotWorth(h.Offer[i]) > ItemS.BotWorth(h.Offer[b])) b = i;
            return b;
        }

        // ---------- every tick, per living hero ----------
        void ItemsTick(Unit h)
        {
            if (h.ItemCount == 0 || h.IsClone) return;
            for (int k = 0; k < h.ItemCount; k++)
            {
                ref var it = ref h.Items[k];
                if (it.Id == ItemId.Fang)
                {
                    bool g = Map.InGrass(h.X, h.Y);
                    if (it.Was && !g && Time >= it.ReadyAt) it.Until = Time + ItemS.FangArm; // out of a bush: armed (not while it cools down)
                    it.Was = g;
                    if (it.Until > 0 && it.Until <= Time) it.Until = 0;
                }
                else if (it.Id == ItemId.Charm && it.Until > 0 && Time >= it.Until)
                {
                    it.Until = 0;
                    AddStatus(h, St.SpeedPct, ItemS.CharmSpeedTime, ItemS.CharmSpeed);
                }
                else if (it.Id == ItemId.Rally) it.ReadyAt = RallyReadyAt[h.Team]; // the icon shows the team's cooldown
                else if (it.Id == ItemId.Battery && Time >= it.ReadyAt)
                {
                    if (it.ReadyAt > 0 && h.HDef != null && h.Charge < h.HDef.MaxCharge && !h.KuggRooted) { h.Charge = Math.Min(h.HDef.MaxCharge, h.Charge + 1); ItemProc(h, ItemId.Battery); }
                    it.ReadyAt = Time + ItemS.BatteryEvery; // the first charge comes 8 s after the pick
                }
            }
            if (h.Bot != null && !h.Dummy && State == MatchState.Play) BotUseItems(h);
        }

        /// <summary>Death: no armed fang, bush memory or waiting Steadfast speed carries over; cooldowns keep running.</summary>
        void ItemsOnDeath(Unit h)
        {
            for (int k = 0; k < h.ItemCount; k++)
            {
                ref var it = ref h.Items[k];
                if (it.Id == ItemId.Fang) { it.Until = 0; it.Was = false; }
                if (it.Id == ItemId.Charm) it.Until = 0;
                if (it.Id == ItemId.Trophy) it.Stacks = 0; // Trophy Chain: all lost on death (Rickard: "lose all on death")
            }
        }

        /// <summary>A shield from an item: one of the same item on the hero is replaced, never stacked. No By, so it never counts
        /// as one of Moa's shields (CcImmune).</summary>
        void ItemShield(Unit u, ItemId id, float dur, float amount)
        {
            for (int i = u.StCount - 1; i >= 0; i--) if (u.Sts[i].Type == St.Shield && u.Sts[i].Item == id) u.RemoveAt(i);
            int s = u.AddRaw(St.Shield, dur, 0, amount, null, false);
            if (s >= 0) u.Sts[s].Item = id;
            Emit(EventType.Shield, u, u, amount);
        }
        void ItemProc(Unit u, ItemId id, int n = 0) => Emit(EventType.ItemProc, u, null, n, (byte)id);

        // ---------- the auto attack (Combat.HitWith) ----------
        /// <summary>Thicket Fang's bonus on an auto attack about to land on an enemy hero.</summary>
        float ItemAutoMult(Unit s, Unit t)
        {
            if (!RealHero(s) || !RealHero(t) || s.Team == t.Team || s.ItemCount == 0) return 1;
            int f = s.ItemIndex(ItemId.Fang);
            return f >= 0 && s.Items[f].Until > Time ? 1 + ItemS.FangBonus : 1;
        }

        /// <summary>After a hero's auto attack landed on an enemy hero (OnHeroHit): the mark, the fang's slow.</summary>
        void ItemsOnAutoHit(Unit s, Unit t)
        {
            if (!RealHero(s) || !RealHero(t) || s.Team == t.Team || s.ItemCount == 0) return;
            int m = s.ItemIndex(ItemId.Mark);
            if (m >= 0 && Time >= s.Items[m].ReadyAt && t.Alive && t.Find(St.QuarryMark) < 0) // a target already marked keeps that mark
            {
                t.AddRaw(St.QuarryMark, ItemS.MarkTime, 0, 0, s, false);
                s.Items[m].ReadyAt = Time + ItemS.MarkCd;
                ItemProc(t, ItemId.Mark);
            }
            int f = s.ItemIndex(ItemId.Fang);
            if (f >= 0 && s.Items[f].Until > Time)
            {
                s.Items[f].Until = 0; s.Items[f].ReadyAt = Time + ItemS.FangCd;
                if (t.Alive) AddStatus(t, St.SlowPct, ItemS.FangSlowTime, ItemS.FangSlow, 0, s);
                ItemProc(t, ItemId.Fang);
            }
        }

        // ---------- the damage (Combat.DealDamage) ----------
        /// <summary>Quarry Mark: the first auto or special hit from another ally of the marker spends the mark for its bonus.</summary>
        float ItemDamageMult(Unit att, Unit tgt, DmgKind kind, bool dot)
        {
            float plain = PlainDamageMult(att, tgt);
            return plain == 1 ? MarkMult(att, tgt, kind, dot) : plain * MarkMult(att, tgt, kind, dot);
        }

        /// <summary>Minion Skewer, Crushing Spanner, Heroes Bane: the holder's damage of every kind (its wolf's too) on that kind of unit.</summary>
        float PlainDamageMult(Unit att, Unit tgt)
        {
            if (!RealHero(att) || att.ItemCount == 0 || att.Team == tgt.Team) return 1;
            float k = 1;
            if (tgt.Kind == UnitKind.Minion) k = att.HasItem(ItemId.MinionDmg) ? 1 + ItemS.MinionDmg : 1;
            else if (tgt.Kind == UnitKind.Tower) k = att.HasItem(ItemId.TowerDmg) ? 1 + ItemS.TowerDmg : 1;
            else if (tgt.Kind == UnitKind.Hero) k = att.HasItem(ItemId.HeroDmg) ? 1 + ItemS.HeroDmg : 1;
            // Brimming Edge and Trophy Chain: every kind of target
            if (att.HasItem(ItemId.Brimming) && att.Charge >= att.HDef.MaxCharge) k *= 1 + ItemS.BrimmingDmg;
            int t = att.ItemIndex(ItemId.Trophy);
            if (t >= 0 && att.Items[t].Stacks > 0) k *= 1 + ItemS.TrophyDmg * att.Items[t].Stacks;
            return k;
        }

        // ---------- the nine of 2026-11-18 ----------
        /// <summary>Iron Plate: what a hit on the holder still does.</summary>
        internal static float PlateMult(Unit tgt) => tgt.Kind == UnitKind.Hero && !tgt.IsClone && tgt.ItemCount > 0 && tgt.HasItem(ItemId.Plate) ? 1 - ItemS.PlateCut : 1;

        /// <summary>Last Stand: amount about to hit tgt. A killing hit with the item ready leaves 1 health and starts the 4 s in which
        /// the hero cannot die (St.Undying); while that lasts no hit takes the last point.</summary>
        float LastStand(Unit tgt, float amount)
        {
            if (tgt.Kind != UnitKind.Hero || tgt.IsClone || amount < tgt.Hp) return amount;
            if (tgt.Has(St.Undying)) return Math.Max(0, tgt.Hp - 1);
            if (tgt.ItemCount == 0) return amount;
            int k = tgt.ItemIndex(ItemId.LastStand);
            if (k < 0 || Time < tgt.Items[k].ReadyAt) return amount;
            tgt.Items[k].ReadyAt = Time + ItemS.LastStandCd;
            tgt.AddRaw(St.Undying, ItemS.LastStandTime, 0, 0, tgt, false);
            tgt.AddRaw(St.SpeedPct, ItemS.LastStandTime, ItemS.LastStandSpeed, 0, tgt, false);
            ItemProc(tgt, ItemId.LastStand);
            return Math.Max(0, tgt.Hp - 1);
        }

        /// <summary>Keen Edge: an auto attack's damage factor (KeenMult on a crit, from the seeded Rng so a replay matches).</summary>
        float KeenMult(Unit s, Unit t)
        {
            if (!RealHero(s) || s.ItemCount == 0 || t.Team == s.Team || !s.HasItem(ItemId.Keen)) return 1;
            if (Rng.Float() >= ItemS.KeenChance) return 1;
            ItemProc(s, ItemId.Keen);
            return ItemS.KeenMult;
        }

        /// <summary>Trophy Chain: a hero died; its killer and those who assisted gain a stack (called before LastHitBy is cleared).</summary>
        void TrophyOnKill(Unit dead, Unit killer)
        {
            for (int i = 0; i < Heroes.Count; i++)
            {
                var a = Heroes[i];
                if (a.ItemCount == 0 || a.Team == dead.Team || a.IsClone) continue;
                bool got = a == killer || Time - dead.LastHitBy[i] < Rules.AssistWindow;
                int k = a.ItemIndex(ItemId.Trophy);
                if (!got || k < 0) continue;
                if (a.Items[k].Stacks < ItemS.TrophyMax) { a.Items[k].Stacks++; ItemProc(a, ItemId.Trophy, a.Items[k].Stacks); }
            }
        }

        /// <summary>A tap on the icon of the item in slot i (PlayerInput.ItemUse, or a bot): Cloak of Invisibility, Divine Protection.</summary>
        internal bool UseItem(Unit h, int i)
        {
            if (h == null || !h.Alive || h.IsClone || i < 0 || i >= h.ItemCount || !ItemsOn) return false;
            ref var it = ref h.Items[i];
            if (!ItemS.Active(it.Id) || Time < it.ReadyAt || h.Has(St.Stun)) return false;
            if (it.Id == ItemId.Cloak)
            {
                h.RemoveAll(St.Veil);
                AddStatus(h, St.Veil, ItemS.CloakTime, CloakTag, 0, h);
                h.RevealT = 0; // it starts unseen, even straight out of a fight
                it.ReadyAt = Time + ItemS.CloakCd;
            }
            else
            {
                GiveImmune(h, ItemS.DivineTime, ItemS.DivineSlow);
                it.ReadyAt = Time + ItemS.DivineCd;
            }
            ItemProc(h, it.Id);
            return true;
        }
        /// <summary>V of the Veil status the Cloak gives (Fenn's has 0): only his own Veil has his strike and reach rules.</summary>
        internal const float CloakTag = -9f;

        /// <summary>The Cloak ends when its holder hits anything (Combat.DealDamage).</summary>
        internal void CloakBreak(Unit att)
        {
            if (att == null || att.Kind != UnitKind.Hero || att.IsClone) return;
            for (int i = att.StCount - 1; i >= 0; i--)
                if (att.Sts[i].Type == St.Veil && att.Sts[i].V == CloakTag) { att.RemoveAt(i); Emit(EventType.VeilEnd, att, null, 0, 0, false, att.X, att.Y); }
        }

        /// <summary>Bots use the button items: the Cloak to get away, Divine Protection when a fight goes badly.</summary>
        void BotUseItems(Unit h)
        {
            for (int i = 0; i < h.ItemCount; i++)
            {
                var id = h.Items[i].Id;
                if (!ItemS.Active(id) || Time < h.Items[i].ReadyAt) continue;
                bool hurt = Time - h.LastHurt < 1.5f;
                if (id == ItemId.Cloak && h.Hp < h.MaxHp * 0.35f && hurt) UseItem(h, i);
                else if (id == ItemId.Divine && h.Hp < h.MaxHp * 0.3f && hurt) UseItem(h, i);
            }
        }

        float MarkMult(Unit att, Unit tgt, DmgKind kind, bool dot)
        {
            if (dot || kind == DmgKind.Passive || !RealHero(att) || !RealHero(tgt) || att.Team == tgt.Team) return 1;
            for (int i = 0; i < tgt.StCount; i++)
            {
                ref var s = ref tgt.Sts[i];
                if (s.Type != St.QuarryMark || !(s.T > 0) || s.By == null || s.By.Team != att.Team || s.By == att) continue;
                tgt.RemoveAt(i);
                ItemProc(tgt, ItemId.Mark, 1);
                return 1 + ItemS.MarkBonus;
            }
            return 1;
        }

        /// <summary>After damage took health and the target lives: Guardian's Thread on enemy special damage, Rally Beacon when
        /// health falls through 30 %.</summary>
        void ItemsOnHurt(Unit att, Unit tgt, float before, DmgKind kind, bool dot)
        {
            if (!RealHero(tgt) || !tgt.Alive) return;
            if (kind == DmgKind.Special && !dot && RealHero(att) && att.Team != tgt.Team) GuardiansThread(tgt);
            float thr = ItemS.RallyBelow * tgt.MaxHp;
            if (before > thr && tgt.Hp <= thr) RallyBeacon(tgt);
        }

        /// <summary>The holder that answers: the hurt hero itself, else the nearest holder in range, the lower HeroIndex among equals.</summary>
        int NearHolder(Unit tgt, ItemId id, float range, bool needReady, out Unit holder)
        {
            holder = null; int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var u = Heroes[i]; if (u.Team != tgt.Team || !u.Alive) continue;
                int k = u.ItemIndex(id); if (k < 0 || (needReady && Time < u.Items[k].ReadyAt)) continue;
                float d = u == tgt ? -1 : SimMath.Dist(u, tgt); if (d > range) continue;
                if (d < bd) { best = k; holder = u; bd = d; }
            }
            return best;
        }

        void GuardiansThread(Unit tgt)
        {
            int k = NearHolder(tgt, ItemId.Thread, ItemS.ThreadRange, true, out var holder); if (k < 0) return;
            holder.Items[k].ReadyAt = Time + ItemS.ThreadCd; // one holder per hit, so one special cannot hand out a shield per holder
            ItemShield(tgt, ItemId.Thread, ItemS.ThreadTime, tgt.MaxHp * ItemS.ThreadShield);
            ItemProc(tgt, ItemId.Thread);
        }

        void RallyBeacon(Unit tgt)
        {
            if (Time < RallyReadyAt[tgt.Team]) return; // one cooldown for the whole team, however many hold it
            if (NearHolder(tgt, ItemId.Rally, ItemS.RallyRange, false, out _) < 0) return;
            RallyReadyAt[tgt.Team] = Time + ItemS.RallyCd;
            ItemShield(tgt, ItemId.Rally, ItemS.RallyTime, tgt.MaxHp * ItemS.RallyShield);
            AddStatus(tgt, St.SpeedPct, ItemS.RallyTime, ItemS.RallySpeed);
            ItemProc(tgt, ItemId.Rally);
        }

        /// <summary>Parascythe: the holder heals for 10 % of the health its damage took (its wolf's and burns too), from any enemy unit.</summary>
        void ItemLifesteal(Unit att, Unit tgt, float taken)
        {
            if (taken <= 0 || !RealHero(att) || !att.Alive || att.ItemCount == 0 || att.Team == tgt.Team || tgt.IsClone) return;
            if (att.HasItem(ItemId.Lifesteal)) Heal(att, taken * ItemS.Lifesteal, att);
        }

        // ---------- control (Combat.AddStatus, AddSlowOnce) ----------
        static bool CharmControl(St type, float v) =>
            type == St.Stun || type == St.Root || type == St.Disarm || ((type == St.SlowFlat || type == St.SlowPct) && v < 0);

        /// <summary>Steadfast Charm: a stun, root, slow or disarm from an enemy hero (or its clone, wolf or shot) on the holder is
        /// 25 % shorter (at most 0.35 s); a burst of speed when it has run out. Returns the duration to put on.</summary>
        float CharmCutDur(Unit u, St type, float dur, float v, Unit by)
        {
            if (u.ItemCount == 0 || !(dur > 0) || !CharmControl(type, v) || !RealHero(u)) return dur;
            var att = by != null ? (by.Owner ?? by) : null;
            if (att == null || att.Kind != UnitKind.Hero || att.Team == u.Team) return dur;
            int k = u.ItemIndex(ItemId.Charm); if (k < 0) return dur;
            ref var it = ref u.Items[k];
            bool same = it.At == Time && Time < it.ReadyAt; // a second control status from the same hit is shortened too
            if (Time < it.ReadyAt && !same) return dur;
            dur -= Math.Min(dur * ItemS.CharmCut, ItemS.CharmMaxCut);
            it.Until = Math.Max(it.Until, Time + dur); // the speed comes when the longest of these has run out (ItemsTick)
            if (same) return dur;
            it.ReadyAt = Time + ItemS.CharmCd; it.At = Time;
            ItemProc(u, ItemId.Charm);
            return dur;
        }
    }
}
