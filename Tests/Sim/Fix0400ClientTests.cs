using System;

namespace BAM.Sim
{
    // The 0.4.0 fix round's checks on the hero catalog (full review 2026-11-09 nr 62, 65, 104): its texts and bars are data in the
    // Sim assembly, so they are checked here against the rules they describe. Called from RunHero2Tests.
    public sealed partial class Match
    {
        static void Fix0400Client()
        {
            // nr 62: every percentage is written "N%", none "N %"
            foreach (var h in HeroCatalog.All) CatalogPercent(h);
            foreach (var h in HeroCatalog.Coming) CatalogPercent(h);

            // nr 65: Kugg's text says what the rules do: every hit loads a shot, up to Shots, he anchors once MinShots are loaded (KuggS, Unit.SpecialReady)
            var kugg = HeroCatalog.Get(HeroId.Kugg);
            Check(kugg.SpecialText.Contains("once " + KuggS.MinShots + " are loaded") && kugg.SpecialText.Contains("up to " + KuggS.Shots),
                "Kugg's special text follows the 2..6 rule: " + kugg.SpecialText);

            // nr 104 -> full review 2026-11-13 nr 118: the bars are worked out from Defs on the hero page (UI/HubPages.cs), the catalog
            // keeps only Rickard's Difficulty, shown as 1..3 dots there (nr 128)
            foreach (var h in HeroCatalog.All)
                Check(h.Difficulty >= 1 && h.Difficulty <= 3, h.Name + "'s Difficulty is 1..3 dots: " + h.Difficulty);
        }

        static void CatalogPercent(HeroInfo h)
        {
            string text = (h.SpecialDesc ?? "") + " " + (h.OneTapDesc ?? "") + " " + (h.PassiveDesc ?? "");
            Check(!text.Contains(" %"), h.Name + "'s texts write percentages as N%");
        }
    }
}
