using System;

namespace BAM.Sim
{
    // Full review 2026-11-13 nr 113: every hero can be offered every item (Rickard: "alla heroes ska kunna få alla items").
    // Resonance Core (id 1) is gone from the game since 2026-11-13 (Rickard: "inte rätt item för spelet. ta bort").
    public sealed partial class Match
    {
        static void Review1113Items()
        {
            var heroes = new[] { HeroId.Fenn, HeroId.Knut, HeroId.Tyra, HeroId.Bram, HeroId.Blast, HeroId.Moa, HeroId.Tikta, HeroId.Bonk, HeroId.Kugg, HeroId.Ragn, HeroId.Nassla };
            foreach (var id in heroes)
            {
                var m = Create(new MatchConfig { PlayerHero = id, Allies = new[] { HeroId.Knut, HeroId.Moa }, Enemies = new[] { HeroId.Bram, HeroId.Tikta, HeroId.Kugg }, Seed = 113, Difficulty = Difficulty.Hard });
                var h = m.Player; var seen = new bool[ItemCatalog.Count + 1]; bool ok3 = true;
                for (int r = 0; r < 400; r++)
                {
                    m.RollOffer(h);
                    if (h.OfferCount != ItemS.Choices) ok3 = false;
                    for (int i = 0; i < h.OfferCount; i++) seen[(int)h.Offer[i]] = true;
                    h.OfferCount = 0;
                }
                int n = 0, all = 0; for (int k = 1; k <= ItemCatalog.Count; k++) { if (seen[k]) n++; if (ItemCatalog.Get((ItemId)k) != null) all++; }
                Check(ok3 && n == all && all == 19 && !seen[1], $"nr 113: {id} can be offered all {all} items, never the removed Resonance Core ({n} seen in 400 offers)");
            }
        }
    }
}
