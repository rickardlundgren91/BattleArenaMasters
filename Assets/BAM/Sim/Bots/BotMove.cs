using System;

namespace BAM.Sim
{
    // Human-like bot movement (old SimHumanMove.cs, Rickard 2026-10-25 "få bottarna att röra sig mänskligt"): only how a bot walks.
    // Softer turns, uneven strafing with short pauses, a calm shuffle at the wave instead of a new spot every decision, an own lane
    // offset and pace per bot, and braking into a stop. Targets, auto attacks, specials and damage are untouched. Every number
    // comes from Match.Rng.
    public sealed partial class BotBrain
    {
        float hmSide = float.NaN, hmPace, hmKite, shufT, shufX, shufY, strafeMag;

        /// <summary>This bot's own habits, rolled once per match.</summary>
        void HmInit()
        {
            if (!float.IsNaN(hmSide)) return;
            hmSide = Srand(-0.5f, 0.5f); hmPace = Srand(0.93f, 1f); hmKite = Srnd() < 0.5f ? -1 : 1;
        }

        /// <summary>Walking turns at half the old rate (9 rad/s); dodges, which turned at once, now take ~0.2 s to about-face.</summary>
        static float HmTurn(float turn) => turn <= 0 ? BotTables.HmDodgeTurn : turn * BotTables.HmTurn;

        /// <summary>Lane, tower and group goals: each bot keeps its own small offset, so the three don't walk one line.</summary>
        void HmOffset()
        {
            if (!hasGoal || gFollow != null && gFollow.Kind == UnitKind.Hero || gStop == 0.3f) return;
            HmInit(); float x = gX + hmSide, y = gY + hmSide * 0.4f;
            if (Walkable(H, x, y)) { gX = x; gY = y; }
        }

        /// <summary>Waiting at the wave: a new spot every 1.5-3 s, not every decision.</summary>
        void HmShuffle()
        {
            if (!(Now < shufT)) { shufT = Now + Srand(1.5f, 3f); shufX = Srand(-0.6f, 0.6f); shufY = Srand(-0.5f, 0.5f); }
            float jx = gX + shufX, jy = gY + shufY;
            if (Walkable(H, jx, jy) && !NearEnemyTowerAt(jx, jy, H.Team, 0.3f)) { gX = jx; gY = jy; }
        }

        /// <summary>A goal that barely moved keeps the old goal and path, so the bot doesn't re-aim every decision.</summary>
        bool HmKeepGoal(bool oHas, float oX, float oY, float oStop, Unit oFollow) =>
            hasGoal && oHas && gFollow == null && oFollow == null && gStop == oStop && path.Has && Hyp(gX - oX, gY - oY) < 0.45f;

        /// <summary>Close-range strafe: sometimes a short pause, sometimes a shorter or longer step; returns the sideways strength.</summary>
        float HmStrafe()
        {
            if (!(strafeT > 0))
            {
                if (strafeMag > 0 && Srnd() < 0.25f) { strafeMag = 0; strafeT = Srand(0.25f, 0.6f); } // a short stop, like a thumb resting
                else { strafe = strafe == 1 ? -1 : 1; strafeMag = Srand(0.5f, 1f); strafeT = Srand(0.5f, 1.6f); }
            }
            return strafeMag;
        }

        /// <summary>Backing off from a melee hero: a little to one side, not straight back.</summary>
        void HmKite(ref float ax, ref float ay)
        {
            HmInit(); float s = 0.35f * hmKite, x = ax, y = ay;
            ax = x - y * s; ay = y + x * s;
        }

        /// <summary>Pace: brake into a stop, and walk at the bot's own calm pace when nothing is going on at the wave.</summary>
        float HmPace(bool last, float d, bool chase)
        {
            HmInit();
            float k = last && !chase && d < BotTables.HmBrake ? Math.Max(0.4f, d / BotTables.HmBrake) : 1;
            if (!chase && Mode == BotMode.Lane && !H.InCombat(Now)) k *= hmPace;
            return k;
        }
    }
}
