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
        bool chaseWalk; // walking after a hero that got out of reach (HmHold)
        // walking along with the wave (LaneGlide): the front minion, where the goal sits from it, the goal GoalLane gave, its pace
        Unit laneU; float laneOx, laneOy, laneGx, laneGy, laneLx, laneLy, laneV; bool laneOn;

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

        /// <summary>How far inside its stop distance a bot walking after a hero keeps going before it stops (Rickard 2026-11-20
        /// "bottarna går ett step i taget och pausar"): with the old 0.1 tiles it took a tiny step and stopped every time the hero
        /// moved; now it walks on until it is a little inside reach, and keeps walking while the hero keeps walking. A fifth of the
        /// stop distance, at most 0.6 tiles (0.3 while harassing from outside the enemy's reach, so it does not walk into it).</summary>
        float HmHold() => Math.Min(Mode == BotMode.Harass ? BotTables.HmHoldHarass : BotTables.HmHold, gStop * BotTables.HmHoldShare);

        /// <summary>No single little step (Rickard 2026-11-20 "dom borde nästan aldrig göra ett step"): a bot that is standing does
        /// not walk to a calm spot (lane, group, hold, defend, follow, support, tower without a target) closer than HmMinStep; it
        /// stays where it is until a later decision puts the spot further away. Fights, retreats, dodges and chases are untouched.</summary>
        void HmNoLittleStep()
        {
            if (!path.Has || gFollow != null || Now - mT < 0.15f) return;
            switch (Mode)
            {
                case BotMode.Lane: case BotMode.Group: case BotMode.Hold: case BotMode.Defend: case BotMode.Follow: case BotMode.Support: case BotMode.Tower: break;
                default: return;
            }
            if (Hyp(gX - H.X, gY - H.Y) < BotTables.HmMinStep) path.Clear();
        }

        /// <summary>After a decision: a lane goal that still follows the front minion keeps its spot relative to it.</summary>
        void LaneGlideSet()
        {
            laneOn = laneU != null && Mode == BotMode.Lane && hasGoal && gFollow == null && Hyp(gX - laneGx, gY - laneGy) < 2;
            if (!laneOn) { laneU = null; return; }
            laneOx = gX - laneU.X; laneOy = gY - laneU.Y; laneLx = laneU.X; laneLy = laneU.Y; laneV = 0;
        }

        /// <summary>Walking with the wave (Rickard 2026-11-20 "bottarna går ett step i taget och pausar"): the bot walked to a spot
        /// behind the front minion, stopped, and took the next step only at its next decision, while the wave kept walking. Now the
        /// spot moves along with the front minion every tick, and close to it the bot walks at the wave's pace (GlidePace), so it
        /// walks with the wave in one go and stops when the wave stops.</summary>
        void LaneGlide(float dt)
        {
            if (!laneOn) return;
            var u = laneU;
            if (u == null || !u.Alive || Mode != BotMode.Lane || !hasGoal || gFollow != null) { laneOn = false; laneU = null; return; }
            float v = Hyp(u.X - laneLx, u.Y - laneLy) / Math.Max(dt, 1e-4f); laneLx = u.X; laneLy = u.Y;
            laneV += (Math.Min(v, 4) - laneV) * Math.Min(1, dt / 0.2f);
            if (laneV < 0.3f) return; // the wave stands (fighting): keep the spot
            float nx = u.X + laneOx, ny = u.Y + laneOy;
            if (Hyp(nx - gX, ny - gY) < 0.25f || !Walkable(H, nx, ny) || NearEnemyTowerAt(nx, ny, H.Team, 0.3f)) return;
            gX = nx; gY = ny;
            // a bot already walking follows the new spot; a standing one starts only once the wave is a real walk away (no little step)
            if (path.Has || Hyp(gX - H.X, gY - H.Y) >= BotTables.HmMinStep) { if (!M.Map.FindPath(H.X, H.Y, gX, gY, Now, path)) path.Clear(); }
        }

        /// <summary>Close to its spot by a walking wave, the bot slows to the wave's pace instead of arriving and stopping.</summary>
        float GlidePace(float d)
        {
            if (!laneOn || laneV < 0.3f) return 1;
            return Math.Min(1, laneV / Math.Max(0.5f, SpeedOf(H)) + Math.Max(0, d - BotTables.GlideNear) * 0.8f);
        }

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
