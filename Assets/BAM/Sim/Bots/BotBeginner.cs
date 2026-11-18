using System;

namespace BAM.Sim
{
    // Easy bots play like real beginners (Rickard 2026-11-18 "Bottar på easy ska spela mer som nybörjare"). Only Easy (Bronze), and
    // every Easy bot in the match on both teams (same brain rule, 2026-10-31). What a person does in their first matches, so it can be
    // seen: they press the special as soon as it lights up, minions or not; they never use the grass (no hiding, no ambush) and run
    // straight home when hurt; a ranged hero stands and shoots instead of stepping back; and now and then they lose focus at the wave
    // and walk off a few steps to look around. Every number comes from Match.Rng. Normal, Hard and Ultra are untouched.
    public sealed partial class BotBrain
    {
        /// <summary>Easy (Bronze): plays like a beginner.</summary>
        bool Beginner => tierI == 0;

        // a beginner's chance to fire a ready special at whatever is in reach, minions too (rolled once per charge)
        internal const float BeginnerSpamChance = 0.6f;
        // losing focus at the wave: every 14-30 s, for 1.5-3.5 s, a spot 2.5-5 tiles away
        internal const float WanderEveryMin = 14, WanderEveryMax = 30, WanderForMin = 1.5f, WanderForMax = 3.5f, WanderRMin = 2.5f, WanderRMax = 5;

        bool spamRolled, spam;
        float wanderNext = float.NaN, wanderUntil = -99, wanderX, wanderY;

        /// <summary>The special just became ready: a beginner usually fires it at the first thing in reach.</summary>
        bool BeginnerSpam()
        {
            if (!Beginner || H.Hero == HeroId.Kugg) return false; // Kugg's Anchor roots him: keeps his own rule
            if (!spamRolled) { spamRolled = true; spam = Srnd() < BeginnerSpamChance; }
            return spam && !retreat;
        }

        /// <summary>The special was spent (or is not ready): roll again for the next charge.</summary>
        void BeginnerSpecialSpent() => spamRolled = false;

        /// <summary>A beginner at the wave with nothing going on sometimes walks off a few steps to look around.</summary>
        void BeginnerWander(float hpR)
        {
            if (!Beginner) return;
            var h = H; float now = Now;
            if (float.IsNaN(wanderNext)) wanderNext = now + Srand(WanderEveryMin * 0.5f, WanderEveryMax);
            bool calm = Mode == BotMode.Lane && !following && hpR > 0.5f && !h.InCombat(now) && !AnyFoeHeroSeenWithin(h, 8);
            if (!calm) { wanderUntil = -99; return; }
            if (now < wanderUntil) { SetGoal(wanderX, wanderY); return; }
            if (now < wanderNext) return;
            wanderNext = now + Srand(WanderEveryMin, WanderEveryMax);
            for (int k = 0; k < 6; k++)
            {
                float a = Srand(-SimMath.PI, SimMath.PI), r = Srand(WanderRMin, WanderRMax), x = h.X + SimMath.Cos(a) * r, y = h.Y + SimMath.Sin(a) * r;
                if (!Walkable(h, x, y) || NearEnemyTowerAt(x, y, h.Team, 0.5f)) continue;
                wanderX = x; wanderY = y; wanderUntil = now + Srand(WanderForMin, WanderForMax); SetGoal(x, y);
                return;
            }
        }
    }
}
