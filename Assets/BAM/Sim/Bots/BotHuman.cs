using System;

namespace BAM.Sim
{
    // Human mistakes and bot smarts by level (Rickard 2026-10-31 "gör bottarna mer mänskliga och bättre på att vara bottar efter
    // nivå", proposals A-K in bam-work/bots-analys/matchanalys-2026-10-31.md, L left out). Every bot of a tier gets the very same
    // rules on both teams (Rickard 2026-10-31: the enemy never thinks better than your own teammates). Easy makes the mistakes
    // people make in their first matches, Hard almost none and plays smarter. Every number comes from Match.Rng.
    public sealed partial class BotBrain
    {
        float tiltT = -99, carefulT = -99, greedT = -99;

        /// <summary>A: after a death a Bronze bot often plays angry for a while; G: a Platinum bot usually plays careful instead.</summary>
        void HumanOnDeath()
        {
            float now = Now;
            if (Srnd() < BotTables.TiltChance[tierI]) { tiltT = now + BotTables.MoodT * Srand(0.8f, 1.2f); carefulT = -99; }
            else if (Srnd() < BotTables.CarefulChance[tierI]) { carefulT = now + BotTables.MoodT * Srand(0.8f, 1.2f); tiltT = -99; }
        }

        /// <summary>A, G: the mood on top of the bot's temperament (+ bolder, - more careful).</summary>
        float Mood() => (Now < tiltT ? BotTables.TiltBold[tierI] : 0) - (Now < carefulT ? BotTables.CarefulBold : 0);

        /// <summary>Greedy after a kill: chases the next low hero too far for a few seconds (Easy often, Hard almost never).</summary>
        void HumanOnKill() { if (Srnd() < BotTables.GreedChance[tierI]) greedT = Now + BotTables.GreedT * Srand(0.8f, 1.2f); }
        bool Greedy => Now < greedT;
        /// <summary>Easy only, greedy: follow the kill in under the enemy tower.</summary>
        bool GreedDive => Greedy && tierI == 0;

        /// <summary>B, tunnel vision: while chasing one hero, another enemy hero that steps into view is noticed this much later.</summary>
        float TunnelExtra(Unit e) =>
            focus != null && focus != e && focus.Alive && focus.Kind == UnitKind.Hero && (Mode == BotMode.Hero || Mode == BotMode.Harass) ? BotTables.Tunnel[tierI] : 0;

        /// <summary>C: this aimed special goes where the target is, not where it is going (and a bit wider).</summary>
        internal bool Fumble() => Srnd() < BotTables.Fumble[tierI];

        /// <summary>K: Normal (Silver) and up keep a stun slam for a hero instead of spending it on minions while one is close.</summary>
        bool SaveCc()
        {
            if (tierI < 1) return false;
            float r;
            switch (H.Hero)
            {
                // one tap (Rules.OneTapSpecial): the tap's own reach (Match.TapReach, + the widest hero's R as TapTargets counts it);
                // the old slam radii 1.8 / 2.0 kept the special back with a hero already in reach (bug review 2026-11-02, nr 4)
                case HeroId.Knut: r = M.OneTap ? Match.TapReach(H.Hero) + 0.4f : KnutS.SlamRadius; break;
                case HeroId.Bonk: r = M.OneTap ? Match.TapReach(H.Hero) + 0.4f : BonkS.SlamRadius; break;
                default: return false;
            }
            return !AnyFoeHeroSeenWithin(H, r) && AnyFoeHeroSeenWithin(H, BotTables.SaveCcR);
        }

        /// <summary>H, I, J: the extra score for e as a target: focus fire, fight where our tower helps, and against a hero that
        /// outranges us wait for its special to be spent.</summary>
        float SmartScore(Unit e, Unit h)
        {
            float s = 0; var mt = TowerOf(h.Team);
            if (mt != null && mt.Alive && Dist(e, mt) < mt.UDef.Range + 0.5f) s += BotTables.OwnTowerFight[tierI];
            if (Outranges(e, h)) s += SpentCert(h, e) * BotTables.KiteWait[tierI];
            return s;
        }
    }
}
