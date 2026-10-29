using System;

namespace BAM.Sim
{
    // Team signals (Signals.cs, BotSignal.cs): ATTACK and DEFEND from the emote wheel.
    public sealed partial class Match
    {
        static void Signals()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Moa, HeroId.Bram }, Enemies = new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta }, Seed = 11, Difficulty = Difficulty.Hard });
            var none = PlayerInput.None;
            while (m.State == MatchState.Countdown) m.Step(in none);
            var p = m.Player; var my = m.Towers[p.Team];
            // the human stands far up the map; the bots walk with them
            float midY = (m.Towers[0].Y + m.Towers[1].Y) / 2;
            p.X = p.PX = my.X; p.Y = p.PY = midY;
            for (int k = 0; k < 60 * 12; k++) { p.X = p.PX = my.X; p.Y = p.PY = midY; p.Hp = p.MaxHp; m.Step(in none); }
            Unit a = null; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != p && m.Heroes[i].Team == p.Team && m.Heroes[i].Alive) { a = m.Heroes[i]; break; }
            Check(a != null && SimMath.Dist(a, my) > my.UDef.Range + 2, $"a bot teammate is away from our tower before the call ({(a == null ? -1 : SimMath.Dist(a, my)):0.0})");

            var call = PlayerInput.None; call.Signal = TeamSignal.Defend;
            m.Step(in call);
            Check(p.Signal == TeamSignal.Defend && m.SignalShown(p) == TeamSignal.Defend, "DEFEND shows above the caller");
            Check(m.SignalFrom(a, TeamSignal.Defend) == p && m.SignalFrom(p, TeamSignal.Defend) == null, "teammates follow it, the caller does not");
            float at = p.SignalAt; m.Step(in call);
            Check(p.SignalAt == at, "a second call within 1.5 s does nothing");
            float d0 = SimMath.Dist(a, my), best = d0;
            for (int k = 0; k < 60 * 5; k++) { p.X = p.PX = my.X; p.Y = p.PY = midY; p.Hp = p.MaxHp; m.Step(in none); if (a.Alive) best = Math.Min(best, SimMath.Dist(a, my)); }
            Check(best < my.UDef.Range + 0.5f, $"the bot walks back to our tower: {d0:0.0} -> {best:0.0} tiles");
            for (int k = 0; k < 60 * 2; k++) m.Step(in none);
            Check(m.SignalShown(p) == TeamSignal.None && m.SignalFrom(a, TeamSignal.Defend) == null, "the call ends after 6 s");

            // ATTACK cancels an older DEFEND; a newer DEFEND cancels ATTACK
            call.Signal = TeamSignal.Attack; m.Step(in call);
            Check(m.SignalFrom(a, TeamSignal.Attack) == p && m.SignalFrom(a, TeamSignal.Defend) == null, "ATTACK is followed");
        }
    }
}
