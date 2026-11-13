using System;

namespace BAM.Sim
{
    // Full review 2026-11-13 nr 124: the match traces are collected for the bots to learn from, but nothing ran
    // MatchTrace.FromBytes and Replay. A fault in the format would only show once someone wanted to use the data. Here a match
    // is recorded the way GameFlow records it (Quantize, trace.Step before m.Step, End), packed, read back and run again,
    // and must end with the same checksum and winner. Only a test: no rules change.
    public sealed partial class Match
    {
        static void TraceReplayChecks()
        {
            // 1. a whole bot match (the player's hero played by a bot, as the bot lab runs them) to its end
            TraceRoundTrip("bot match", new MatchConfig { PlayerHero = HeroId.Tyra, Difficulty = Difficulty.Normal, Seed = 124, PlayerIsBot = true, BotIsHuman = true },
                1, 60 * 60 * 15, (m, step, rng, inputs) => inputs[0] = PlayerInput.None, true);

            // 2. offline, a player whose controls change all the time (moves, specials with aim, spells, signals, item picks)
            TraceRoundTrip("player controls", new MatchConfig { PlayerHero = HeroId.Kugg, Difficulty = Difficulty.Normal, Seed = 1124 },
                1, 60 * 90, (m, step, rng, inputs) => inputs[0] = Wild(rng, step), false);

            // 3. online: two seats, the second becomes a bot after 20 s (the bot-seat record), random controls for both until then
            var seats = new[] { new SeatConfig { Hero = HeroId.Bram, Team = 0, Name = "A" }, new SeatConfig { Hero = HeroId.Moa, Team = 1, Spell = SpellId.Shield, Name = "B" } };
            TraceRoundTrip("online seats", new MatchConfig { Seats = seats, LocalSeat = 0, Difficulty = Difficulty.Normal, Seed = 2124 },
                2, 60 * 45, (m, step, rng, inputs) =>
                {
                    if (step == 60 * 20) m.SeatToBot(1);
                    inputs[0] = Wild(rng, step);
                    inputs[1] = m.Humans[1].Bot != null ? PlayerInput.None : Wild(rng, step);
                }, false);
        }

        static PlayerInput Wild(Random rng, int step)
        {
            var i = PlayerInput.None;
            if (step % 20 == 0 || rng.Next(6) == 0) { double a = rng.NextDouble() * Math.PI * 2; i.MoveX = (float)Math.Cos(a); i.MoveY = (float)Math.Sin(a); }
            if (rng.Next(25) == 0) { i.Special = true; i.AimAngle = (float)(rng.NextDouble() * Math.PI * 2 - Math.PI); i.AimK = (float)rng.NextDouble(); }
            if (rng.Next(300) == 0) { i.Spell = true; i.SpellAngle = (float)(rng.NextDouble() * Math.PI * 2 - Math.PI); i.SpellK = (float)rng.NextDouble(); }
            if (rng.Next(400) == 0) i.Signal = (TeamSignal)(1 + rng.Next(2));
            if (rng.Next(200) == 0) i.ItemPick = (byte)(1 + rng.Next(3));
            return i;
        }

        static void TraceRoundTrip(string what, MatchConfig cfg, int seats, int maxSteps, Action<Match, int, Random, PlayerInput[]> controls, bool toTheEnd)
        {
            var m = Create(cfg);
            var trace = MatchTrace.Begin(cfg, seats, 60, "test", 1763000000L, cfg.Seats != null, false);
            var rng = new Random(cfg.Seed);
            var inputs = new PlayerInput[seats];
            int step = 0;
            for (; step < maxSteps && m.State != MatchState.Over; step++)
            {
                controls(m, step, rng, inputs);
                for (int s = 0; s < seats; s++) inputs[s] = MatchTrace.Quantize(inputs[s]); // as GameFlow: the rules get the wire's rounding
                trace.Step(m, inputs);
                m.Step(inputs);
            }
            trace.End(m);
            if (toTheEnd) Check(m.State == MatchState.Over && m.Winner >= 0, $"TRACE {what}: the recorded match ends ({m.Time:0} s, winner {m.Winner})");

            byte[] bytes = trace.ToBytes();
            var back = MatchTrace.FromBytes(bytes);
            Check(back.Protocol == 60 && back.App == "test" && back.At == 1763000000L && back.Online == (cfg.Seats != null) && !back.Editor && back.Ended,
                $"TRACE {what}: the header reads back ({bytes.Length} bytes for {trace.Steps} steps)");
            Check(back.Seats == seats && back.Steps == trace.Steps && back.Winner == trace.Winner && back.Ticks == trace.Ticks && back.EndChecksum == trace.EndChecksum,
                $"TRACE {what}: seats, steps, winner, ticks and checksum read back");
            var c = back.Config;
            Check(c.Seed == cfg.Seed && c.PlayerHero == cfg.PlayerHero && c.Difficulty == cfg.Difficulty && c.PlayerIsBot == cfg.PlayerIsBot && c.BotIsHuman == cfg.BotIsHuman
                && c.Map == cfg.Map && c.LocalSeat == cfg.LocalSeat && (c.Seats == null) == (cfg.Seats == null), $"TRACE {what}: the config reads back");

            var again = back.Replay(null, out bool same);
            Check(same, $"TRACE {what}: the replay ends with the recorded checksum and winner ({again.Checksum():X} vs {trace.EndChecksum:X}, winner {again.Winner} vs {trace.Winner})");
            Check(again.Ticks == trace.Ticks && Math.Abs(again.Time - trace.Time) < 1e-4f, $"TRACE {what}: the replay ran as many ticks ({again.Ticks} vs {trace.Ticks})");

            // the check has teeth: a trace whose end checksum is off is not the same
            back.EndChecksum ^= 1;
            back.Replay(null, out bool wrong);
            Check(!wrong, $"TRACE {what}: a wrong checksum is caught");
        }
    }
}
