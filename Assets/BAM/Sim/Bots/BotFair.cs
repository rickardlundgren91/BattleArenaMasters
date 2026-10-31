using System;

namespace BAM.Sim
{
    // Fair bots (old SimAiFair.cs, Rickard 2026-10-25 "bottarna ska inte veta framtiden"): a bot acts only on what a person on its
    // team could know. An enemy hero that steps out of grass is noticed only after a human reaction time (the tier's DodgeReactMin
    // x 0.9-1.4, rolled from Match.Rng the first time a bot of that team asks: Platinum ~0.22-0.35 s, Bronze ~0.4-0.63 s); a hero
    // hidden for less than NoticeGrace (a grass edge flicker) stays noticed. Aim lead starts from zero when the target comes into
    // view and builds up over LeadSeenT, so a bot cannot use how a hidden hero walked. The rules' own visibility (Match.IsVisibleTo)
    // and auto attacks are unchanged; this is only what the bots decide on.
    public sealed partial class BotBrain
    {
        /// <summary>Per enemy team: when u came into view, and when it went out of view (NaN while seen). Called every tick
        /// for every hero from Match.TrackMotion; no dice.</summary>
        internal static void TrackSight(Match m, Unit u)
        {
            float now = m.Time;
            for (int tm = 0; tm < 2; tm++)
            {
                if (tm == u.Team) continue;
                if (m.IsVisibleTo(u, tm))
                {
                    if (!float.IsNaN(u.HidAt[tm])) { if (now - u.HidAt[tm] >= BotTables.NoticeGrace) { u.VisSince[tm] = now; u.NoticeAt[tm] = float.NaN; } u.HidAt[tm] = float.NaN; }
                    else if (float.IsNaN(u.VisSince[tm])) u.VisSince[tm] = now;
                }
                else if (float.IsNaN(u.HidAt[tm])) u.HidAt[tm] = now;
            }
        }

        /// <summary>The bot's team sees e and the bot has had time to notice it.</summary>
        bool AiSees(Unit e)
        {
            int tm = H.Team;
            if (!M.IsVisibleTo(e, tm)) return false;
            if (e.Kind != UnitKind.Hero || e.IsClone || e.Team == tm || float.IsNaN(e.VisSince[tm])) return true;
            if (float.IsNaN(e.NoticeAt[tm])) e.NoticeAt[tm] = e.VisSince[tm] + BotTables.DodgeReactMin[tierI] * Srand(0.9f, 1.4f);
            return Now >= e.NoticeAt[tm] + TunnelExtra(e); // tunnel vision while chasing another hero (BotHuman.cs)
        }

        /// <summary>0..1: how much of t's walk the bot h has watched (scales its aim lead in Match.LeadAim).</summary>
        internal static float LeadSeen(Match m, Unit h, Unit t)
        {
            if (t.Team == h.Team) return 1;
            if (!m.IsVisibleTo(t, h.Team)) return 0;
            float s = t.VisSince[h.Team];
            return float.IsNaN(s) ? 1 : SimMath.Clamp((m.Time - s) / BotTables.LeadSeenT, 0, 1);
        }
    }
}
