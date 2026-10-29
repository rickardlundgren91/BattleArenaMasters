using System;

namespace BAM.Sim
{
    // Bots following a teammate's team signal (old SimSignal.cs signalSteer/signalBonus; Match.CallSignal in Signals.cs).
    // ATTACK: a bot that is pulling back with enough health stays in, prefers the enemy heroes near the caller, and with no
    // fight of its own walks to the caller. DEFEND: a bot that is not pulling back walks to its own tower and keeps to it,
    // fighting what comes. Bots never call signals, so a match without a human is unchanged.
    public sealed partial class BotBrain
    {
        Unit sigAtk, sigDef;

        /// <summary>After the bot has decided whether to pull back.</summary>
        void SignalSteer(float hpR)
        {
            sigDef = M.SignalFrom(H, TeamSignal.Defend);
            sigAtk = sigDef == null ? M.SignalFrom(H, TeamSignal.Attack) : null;
            if (sigAtk != null && retreat && hpR > 0.35f) retreat = false;
        }

        /// <summary>Target score: enemy heroes near the teammate who called ATTACK.</summary>
        float SignalBonus(Unit e) => sigAtk != null && Dist(sigAtk, e) < sigAtk.Range + e.R + 3 ? 2 : 0;

        /// <summary>The goal while a signal is followed; last, so it wins over the lane, the team leash and the tower.</summary>
        void SignalGoal(Unit target, Unit myTower, int fwd)
        {
            var h = H;
            if (retreat || danger) return;
            if (sigDef != null && myTower != null && myTower.Alive)
            {
                float tr = myTower.UDef.Range;
                if (target != null && Dist(target, myTower) < tr + 3) return; // already fighting at our tower
                if (Dist(h, myTower) < tr - 0.5f) return;
                float x = myTower.X, y = myTower.Y + fwd * (tr - 1.5f);
                if (!Walkable(h, x, y)) { x = myTower.X; y = myTower.Y + fwd * 1.5f; }
                Mode = BotMode.Defend; focus = null; SetGoal(x, y);
            }
            else if (sigAtk != null && target == null && Dist(h, sigAtk) > 3)
            {
                Mode = BotMode.Group; SetGoal(sigAtk.X, sigAtk.Y, 2, sigAtk);
            }
        }
    }
}
