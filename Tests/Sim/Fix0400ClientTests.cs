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

            // nr 65: Kugg's text says what the rules do: ready from MinShots hits, every hit one more shot, up to Shots (KuggS, Unit.SpecialReady)
            var kugg = HeroCatalog.Get(HeroId.Kugg);
            Check(kugg.SpecialText.Contains("from " + KuggS.MinShots + " hits") && kugg.SpecialText.Contains("up to " + KuggS.Shots),
                "Kugg's special text follows the 2..6 rule: " + kugg.SpecialText);

            // nr 104: the bars follow Defs in terciles of the heroes in the game: level 1 hp, auto attack damage per second, walking speed
            var all = HeroCatalog.All;
            int n = all.Length;
            var hp = new float[n]; var dps = new float[n]; var spd = new float[n];
            for (int i = 0; i < n; i++) { var d = Defs.Hero(all[i].Id); hp[i] = d.Hp; dps[i] = d.Dmg / d.Cd; spd[i] = d.Speed; }
            int[] hb = Terciles(hp), db = Terciles(dps), sb = Terciles(spd);
            for (int i = 0; i < n; i++)
                Check(all[i].Health == hb[i] && all[i].Damage == db[i] && all[i].Speed == sb[i],
                    $"{all[i].Name}'s bars follow Defs: health {hb[i]} ({hp[i]} hp), damage {db[i]} ({dps[i]:0.0}/s), speed {sb[i]} ({spd[i]}); the catalog says {all[i].Health}/{all[i].Damage}/{all[i].Speed}");
        }

        static void CatalogPercent(HeroInfo h)
        {
            string text = (h.SpecialDesc ?? "") + " " + (h.OneTapDesc ?? "") + " " + (h.PassiveDesc ?? "");
            Check(!text.Contains(" %"), h.Name + "'s texts write percentages as N%");
        }

        /// <summary>1..3 by rank among the values (the lowest third 1, the top third 3); equal values share the bar of their middle rank.</summary>
        static int[] Terciles(float[] v)
        {
            int n = v.Length;
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) => v[a] != v[b] ? v[a].CompareTo(v[b]) : a.CompareTo(b));
            var rank = new int[n];
            for (int r = 0; r < n; r++) rank[order[r]] = r;
            var bar = new int[n];
            for (int i = 0; i < n; i++)
            {
                int lo = rank[i], hi = rank[i];
                for (int j = 0; j < n; j++) if (v[j] == v[i]) { if (rank[j] < lo) lo = rank[j]; if (rank[j] > hi) hi = rank[j]; }
                bar[i] = 1 + (lo + hi) / 2 * 3 / n;
            }
            return bar;
        }
    }
}
