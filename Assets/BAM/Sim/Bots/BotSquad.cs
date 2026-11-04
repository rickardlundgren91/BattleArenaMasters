using System;

namespace BAM.Sim
{
    // Team shape and using a lead (video review 2026-11-03, Rickard "fixa allt som behövs fixat"): the three bots of a team moved
    // as one clump all match (name tags on top of each other), never used a 3-0 lead with the player dead on the tower, and
    // stood in fights on 30-50 % health under Moa's heals instead of stepping out to regenerate. Here:
    //  - spread: a goal closer than SpreadGap to a teammate (or the spot that teammate walks to) moves away from them, and a bot
    //    that walks almost into a teammate steps aside (SpreadStep); one bot per team, the flanker, waits at the wave in the side grass;
    //  - lead push: with two kills more and an enemy hero dead long enough to reach the tower and hit it, the team goes on the tower
    //    and stays under it while it still outnumbers the defenders (the tower's own ramp still sends a bot out, TowerBotLeaveS);
    //  - rotate: Hard and Ultra step out of a fight under RotateHp to regenerate (anywhere, Rules.RegenAmount), unless a kill is a
    //    moment away. The side grass and the lead push are Normal and up; spreading is every level.
    // Every rule is the same for both teams at every level (Rickard's rule: ally and enemy bots think equally well).
    public sealed partial class BotBrain
    {
        /// <summary>Goals keep at least this far from a teammate and the spot it walks to (tiles).</summary>
        internal const float SpreadGap = 2.0f;
        /// <summary>Walking closer than this to a teammate makes the bot step aside a little.</summary>
        internal const float SpreadStep = 1.2f;
        /// <summary>At the enemy tower the spot slides round it to keep this far from teammates.</summary>
        internal const float SpreadFightGap = 1.5f;
        /// <summary>Under this health share a bot (Hard and Ultra) steps out of the fight to regenerate (Rules.RegenAmount).</summary>
        internal const float RotateHp = 0.4f;

        /// <summary>The bot of h's team that waits at the wave in the side grass: an assassin or fighter first, else the last bot.
        /// The same answer on every phone (unit order only).</summary>
        Unit Flanker()
        {
            Unit best = null; int bs = -1; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || u.Team != H.Team || u.IsClone || u.Bot == null || u.IsPlayer && !M.Config.PlayerIsBot) continue;
                if (M.Config.BotIsHuman && u == M.Player) continue;
                int s = u.Hero == HeroId.Tikta ? 4 : u.Hero == HeroId.Fenn ? 3 : u.Hero == HeroId.Bonk ? 2 : BotTables.Tank(u.Hero) || u.Hero == HeroId.Moa || u.Hero == HeroId.Hilde ? 0 : 1;
                if (s >= bs) { bs = s; best = u; }
            }
            return best;
        }

        /// <summary>Lane: the flanker waits in the side grass near the wave while no enemy hero is close.</summary>
        void FlankGrass(Unit enemyTower, float hpR)
        {
            var h = H;
            if (!v3 || !hasGoal || Mode != BotMode.Lane || hpR < 0.6f || Flanker() != h || AnyFoeHeroSeenWithin(h, 5)) return;
            if (M.Map.InGrass(gX, gY)) return;
            if (BushSpot(gX, gY, 6.5f, enemyTower, 0, out float bx, out float by) && Math.Abs(by - gY) < 3.5f) SetGoal(bx, by, 0.3f);
        }

        /// <summary>Move the goal away from teammates and from where they walk.</summary>
        void SpreadGoal()
        {
            var h = H; if (!hasGoal || retreat || Mode == BotMode.Danger || Mode == BotMode.BushChase || gStop == 0.3f) return;
            // a fight goal stays where the bot chose it: spreading round the target cost focus (bot lab: 14 % fewer kills, matches 30 s longer)
            if (gFollow != null) { if (gFollow.Kind == UnitKind.Tower) SpreadRound(gFollow); return; }
            var us = M.Units;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < us.Count; i++)
                {
                    var a = us[i];
                    if (a == h || a.Kind != UnitKind.Hero || !a.Alive || a.Team != h.Team || a.IsClone) continue;
                    PushFrom(a.X, a.Y, i);
                    var b = a.Bot; if (b != null && b.hasGoal && !b.retreat) PushFrom(b.gX, b.gY, i);
                }
        }
        /// <summary>Closest a teammate or its goal is to (x, y).</summary>
        float MateGap(float x, float y)
        {
            var h = H; float best = 99; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var a = us[i];
                if (a == h || a.Kind != UnitKind.Hero || !a.Alive || a.Team != h.Team || a.IsClone) continue;
                best = Math.Min(best, Hyp(a.X - x, a.Y - y));
                var b = a.Bot; if (b != null && b.hasGoal && !b.retreat) best = Math.Min(best, Hyp(b.gX - x, b.gY - y));
            }
            return best;
        }
        /// <summary>A fight or tower goal (a spot at a distance from f): slide it round f, at the same distance, away from teammates.</summary>
        void SpreadRound(Unit f)
        {
            float gap = SpreadFightGap; if (MateGap(gX, gY) >= gap) return;
            float r = Hyp(gX - f.X, gY - f.Y); if (r < 0.3f) return;
            // tries one side first, mirrored for the top team so both teams spread the same way (bug review nr 11)
            float a0 = SimMath.Atan2(gY - f.Y, gX - f.X), bestG = MateGap(gX, gY), bx = gX, by = gY;
            for (int k = 1; k <= 8; k++)
            {
                float a = a0 - Fwd * ((k & 1) == 0 ? 1 : -1) * ((k + 1) / 2) * 0.3f, x = f.X + SimMath.Cos(a) * r, y = f.Y + SimMath.Sin(a) * r;
                if (!Walkable(H, x, y) || f.Kind != UnitKind.Tower && NearEnemyTowerAt(x, y, H.Team, 0.2f) && !NearEnemyTowerAt(gX, gY, H.Team, 0.2f)) continue;
                float g = MateGap(x, y);
                if (g >= gap) { gX = x; gY = y; return; }
                if (g > bestG + 0.2f) { bestG = g; bx = x; by = y; }
            }
            gX = bx; gY = by;
        }
        void PushFrom(float mx, float my, int salt)
        {   // sideways only, as people spread along the wave: stepping back would take the bot out of reach of it
            float dx = gX - mx, dy = gY - my;
            if (Hyp(dx, dy) >= SpreadGap) return;
            float side = Math.Abs(dx) > 0.05f ? Math.Sign(dx) : ((H.HeroIndex + salt) & 1) == 0 ? 1 : -1, w = SimMath.Sqrt(SpreadGap * SpreadGap - dy * dy);
            for (int k = 0; k < 2; k++, side = -side)
            {
                float x = mx + side * w;
                if (Walkable(H, x, gY) && !NearEnemyTowerAt(x, gY, H.Team, 0.2f)) { gX = x; return; }
            }
        }

        /// <summary>A walking direction that bends away from a teammate closer than SpreadStep (not while dodging or fleeing).</summary>
        void SpreadStepAside(ref float dx, ref float dy)
        {
            var h = H; if (retreat || Mode == BotMode.Danger) return;
            float m = Hyp(dx, dy); if (m < 0.01f) return;
            float px = 0, py = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var a = us[i];
                if (a == h || a.Kind != UnitKind.Hero || !a.Alive || a.Team != h.Team || a.IsClone) continue;
                float ax = h.X - a.X, ay = h.Y - a.Y, d = Hyp(ax, ay);
                if (d >= SpreadStep) continue;
                if (d < 0.01f) { ax = h.HeroIndex % 2 == 0 ? 1 : -1; ay = 0; d = 1; }
                float w = (SpreadStep - d) / SpreadStep;
                px += ax / d * w; py += ay / d * w;
            }
            if (px == 0 && py == 0) return;
            dx = dx / m + px * 0.8f; dy = dy / m + py * 0.8f;
            float n = Hyp(dx, dy); if (n > 0.01f) { dx = dx / n * Math.Min(1, m); dy = dy / n * Math.Min(1, m); }
        }

        /// <summary>Two kills ahead with an enemy hero dead long enough to walk to the tower and hit it, and more of us alive near
        /// it than of them: push the tower (Normal and up). Ends by itself once the dead are back (foes >= mates).</summary>
        bool LeadPush(Unit tw, float hpR)
        {
            var h = H;
            if (tierI < 1 || tw == null || !tw.Alive || hpR < 0.5f || M.Kills[h.Team] - M.Kills[Teams.Other(h.Team)] < 2) return false;
            int foes = 0, mates = 0; float back = 99, dh = Dist(h, tw); var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || u.IsClone) continue;
                if (u.Team == h.Team) { if (u.Alive && HpR(u) > 0.4f && Dist(u, tw) < dh + 8) mates++; }
                else if (u.Alive) foes++; else back = Math.Min(back, u.RespawnT);
            }
            if (back > 98 || foes >= mates) return false;
            float travel = Math.Max(0, dh - h.Range - tw.R) / Math.Max(1, SpeedOf(h));
            return back > travel + 1.5f;
        }
    }
}
