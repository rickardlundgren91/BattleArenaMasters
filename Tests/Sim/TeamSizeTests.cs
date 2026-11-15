using System;

namespace BAM.Sim
{
    // 2v2 (Rickard 2026-11-15: "lägg in 2v2 mode och lägg in grove mapen som val på både 3v3 och 2v2", "gör även så att man kan
    // köra 2v2 med vänner och mot andra"): MatchConfig.TeamSize 2 puts two heroes on each team, offline and online, on either map,
    // and a match trace keeps it.
    public sealed partial class Match
    {
        static void TeamSizeChecks()
        {
            foreach (var map in new[] { MapId.Runes, MapId.Grove })
            {
                // offline: the player and one bot ally against two bots, the player in the middle slot as in 3v3
                var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Difficulty = Difficulty.Normal, Seed = 77, TeamSize = 2, Map = map });
                int t0 = 0, t1 = 0;
                foreach (var h in m.Heroes) if (h.Team == Teams.Player) t0++; else t1++;
                Check(m.Map.Id == map && t0 == 2 && t1 == 2, $"2v2 on {map}: two heroes a team ({t0} and {t1})");
                Check(m.Player.X == m.Map.SpawnX[0][1] && m.Player.Y == m.Map.SpawnY[0][1], $"2v2 on {map}: the player starts in the middle slot");
                bool botsOk = true; foreach (var h in m.Heroes) if (h != m.Player && h.Bot == null) botsOk = false;
                Check(botsOk, $"2v2 on {map}: the other three heroes are bots");

                // online: three people against each other (two on team 0, one on team 1), the empty place a bot, the room's map
                var seats = new[] { new SeatConfig { Hero = HeroId.Bram, Team = 0, Name = "A" }, new SeatConfig { Hero = HeroId.Moa, Team = 1, Name = "B" }, new SeatConfig { Hero = HeroId.Knut, Team = 0, Name = "C" } };
                var o = Create(new MatchConfig { Seats = seats, LocalSeat = 1, Difficulty = Difficulty.Normal, Seed = 78, TeamSize = 2, Map = map });
                t0 = t1 = 0; int bots = 0;
                foreach (var h in o.Heroes) { if (h.Team == 0) t0++; else t1++; if (h.Bot != null) bots++; }
                Check(o.Map.Id == map && t0 == 2 && t1 == 2 && bots == 1, $"online 2v2 on {map}: 2 + 2 heroes, one bot ({t0}, {t1}, {bots} bots)");
                Check(o.Humans.Count == 3 && o.Player == o.Humans[1] && o.Player.Team == 1, $"online 2v2 on {map}: every seat has its hero");
            }

            // 3v3 stays as it was
            var three = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Difficulty = Difficulty.Normal, Seed = 77 });
            Check(three.Heroes.Count == 6 && three.Size == 3, "3v3: six heroes");

            // a trace keeps 2v2 (and an old trace without the flag reads as 3v3)
            TraceRoundTrip("2v2 bot match on Grove", new MatchConfig { PlayerHero = HeroId.Bram, Difficulty = Difficulty.Normal, Seed = 3124, PlayerIsBot = true, BotIsHuman = true, TeamSize = 2, Map = MapId.Grove },
                1, 60 * 60 * 15, (mm, step, rng, inputs) => inputs[0] = PlayerInput.None, true);
            var tr = MatchTrace.Begin(new MatchConfig { PlayerHero = HeroId.Bram, Seed = 5, TeamSize = 2, Map = MapId.Grove }, 1, 61, "test", 0, false, false);
            var back = MatchTrace.FromBytes(tr.ToBytes());
            Check(back.Config.TeamSize == 2 && back.Config.Map == MapId.Grove, "a 2v2 trace reads back as 2v2 on Grove");
        }
    }
}
