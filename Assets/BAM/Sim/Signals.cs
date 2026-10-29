using System;

namespace BAM.Sim
{
    /// <summary>
    /// Team signals (old SimSignal.cs, Rickard 2026-10-14; back in BAM 2026-10-29: "Attack och defend finns inte med", "knappar
    /// för att säga till laget vad göra"). A human picks ATTACK or DEFEND on the emote wheel; it comes in through PlayerInput,
    /// so online every phone sees the same call. The word shows above the hero for SignalShowTime s, to its own team only. Bot
    /// teammates follow it for SignalBotTime s (BotSignal.cs): ATTACK keeps them in the fight and sends them to the caller's
    /// enemies, DEFEND sends them back to their own tower (the old game's RETREAT, as Rickard asked for DEFEND). Bots never
    /// call signals, so a match without a human is unchanged.
    /// </summary>
    public sealed partial class Match
    {
        public const float SignalShowTime = 2f, SignalCd = 1.5f, SignalBotTime = 6f;

        public bool SignalReady(Unit u) => u != null && u.Alive && (float.IsNaN(u.SignalAt) || Time - u.SignalAt >= SignalCd);

        void CallSignal(Unit u, TeamSignal kind)
        {
            if ((kind != TeamSignal.Attack && kind != TeamSignal.Defend) || State != MatchState.Play || !SignalReady(u)) return;
            u.Signal = kind; u.SignalAt = Time;
        }

        /// <summary>The signal shown above u right now (None = none).</summary>
        public TeamSignal SignalShown(Unit u)
        {
            if (u.Signal == TeamSignal.None || float.IsNaN(u.SignalAt)) return TeamSignal.None;
            float a = Time - u.SignalAt;
            return a >= 0 && a < SignalShowTime ? u.Signal : TeamSignal.None;
        }

        /// <summary>The newest teammate of h (not h) whose kind call is still followed, else null.</summary>
        internal Unit SignalFrom(Unit h, TeamSignal kind)
        {
            Unit best = null;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var u = Heroes[i];
                if (u == h || u.Team != h.Team || !u.Alive || u.IsClone || u.Signal != kind || float.IsNaN(u.SignalAt)) continue;
                float a = Time - u.SignalAt;
                if (a < 0 || a > SignalBotTime) continue;
                // a newer call of the other kind cancels this one
                if (best == null || u.SignalAt > best.SignalAt) best = u;
            }
            if (best == null) return null;
            for (int i = 0; i < Heroes.Count; i++)
            {
                var u = Heroes[i];
                if (u != h && u.Team == h.Team && u.Alive && u.Signal != kind && u.Signal != TeamSignal.None && !float.IsNaN(u.SignalAt) && u.SignalAt > best.SignalAt) return null;
            }
            return best;
        }
    }
}
