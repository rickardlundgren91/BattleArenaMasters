using System;

namespace BAM.Sim
{
    // The full review 2026-11-09, the bots (BOTS batch): nr 69 Kugg's bot anchors only with 4+ stocks, never while retreating or in
    // danger and never as a panic cast; nr 87 a jump pad's tile is no spot and no step for a bot, like a tower body; nr 88 a tower
    // blocks all eight tiles round it for paths; nr 89 an unanchored Kugg's special reaches Range + 1 in the bots' danger sums, 8 anchored.
    public sealed partial class Match
    {
        static void Fix0400Bots() { KuggBotAnchorChecks(); KuggBotPanicChecks(); PadBlockChecks(); TowerPathChecks(); KuggReachChecks(); }

        /// <summary>A bot match for these checks: the player's hero is a bot of level d, every other hero stands still (no brain), no minions
        /// and no waves, past the countdown.</summary>
        static Match MakeBots(HeroId player, HeroId[] allies, HeroId[] enemies, Difficulty d, int seed, MapId map = MapId.Runes)
        {
            var m = Create(new MatchConfig { PlayerHero = player, Allies = allies, Enemies = enemies, Seed = seed, PlayerIsBot = true, Difficulty = d, Map = map });
            for (int i = 0; i < m.Units.Count; i++) { var u = m.Units[i]; if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != m.Player) m.Heroes[i].Bot = null;
            m.WaveT = float.MaxValue; // no waves: the heroes under test have the lane to themselves
            m.State = MatchState.Play; m.Time = 10; m.Telegraph = false; m.Events.Clear();
            return m;
        }
        static int CastsBy(Match m, Unit h) { int n = 0; foreach (var e in m.Events) if (e.Type == EventType.Cast && e.A == h) n++; return n; }

        static void KuggBotAnchorChecks()
        {
            // nr 69: the Kugg bot wants his Anchor only with 4+ stocks (he may anchor from 2, KuggS.MinShots)
            var m = MakeBots(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            var k = m.Player; var b = k.Bot; var ty = m.Foe(HeroId.Tyra);
            float x = 7.5f, y = 17.5f;
            Place(k, x, y); Place(ty, x, y - 5);
            k.Charge = 3;
            Check(!b.SpecialWanted(false), "FIX-69: with 3 stocks and an enemy hero 5 tiles off the Kugg bot does not want to anchor");
            k.Charge = 4;
            Check(b.SpecialWanted(false), "FIX-69: with 4 stocks he does");
            Check(!BotTables.Ranged(HeroId.Kugg) && BotTables.Ranged(HeroId.Tyra), "FIX-69: Kugg (reach 2.5) is no ranged hero to the bots");

            // nr 69: never while in danger. Three ranged enemies 5 tiles away cannot hit him yet, but their reach plus a step counts in his
            // time-to-kill (BotSense.IncomingDps); with 60 health that is ~1.4 s, under Platinum's danger line (3 s x 1.25). Rooted, so
            // he cannot step out of their reach (a free bot did so before it had even noticed them, and anchored safely a step later)
            var m2 = MakeBots(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            k = m2.Player; b = k.Bot;
            Place(k, x, y); Place(m2.Foe(HeroId.Tyra), x, y - 5); Place(m2.Foe(HeroId.Bram), x - 3, y - 4); Place(m2.Foe(HeroId.Blast), x + 3, y - 4);
            k.MaxHp = 60; k.Hp = 60; k.Charge = 6; m2.AddStatus(k, St.Root, 30);
            bool sawDanger = false, castInDanger = false; int casts = 0;
            for (int i = 0; i < 360; i++)
            {
                m2.Step(PlayerInput.None);
                int c = CastsBy(m2, k); casts += c;
                if (b.Mode == BotMode.Danger || b.Mode == BotMode.Retreat) { sawDanger = true; if (c > 0) castInDanger = true; }
            }
            Check(sawDanger, "FIX-69: the set-up puts the Kugg bot in Danger");
            Check(!castInDanger, $"FIX-69: no Anchor while in Danger or Retreat ({casts} casts in 6 s)");
        }

        static void KuggBotPanicChecks()
        {
            // nr 69: no panic Anchor at 15 % health (Bronze and Silver bots fire their special in panic, BotTables.PanicSpecial: one roll per
            // match, 35 % on Bronze, so about 7 of these 20 Kuggs anchored before)
            int casts = 0;
            for (int seed = 1; seed <= 20; seed++)
            {
                var m = MakeBots(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Easy, seed);
                var k = m.Player;
                Place(k, 7.5f, 17.5f); Place(m.Foe(HeroId.Tyra), 7.5f, 12.5f);
                for (int i = 0; i < 240; i++)
                {
                    k.Hp = k.MaxHp * 0.1f; k.Charge = 6; k.LastCombat = k.LastFight = m.Time; // bleeding, in a fight, the Anchor ready
                    m.Step(PlayerInput.None);
                    casts += CastsBy(m, k);
                }
            }
            Check(casts == 0, $"FIX-69: a Kugg bot at 10 % health in a fight never anchors in panic ({casts} of 20 did)");
        }

        static void PadBlockChecks()
        {
            // nr 87: a pad tile and 0.4 round it are no spot and no step for a bot (Walkable, ClearWalk); one tile off is free
            var m = MakeBots(HeroId.Fenn, new[] { HeroId.Moa, HeroId.Kugg }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7, MapId.Grove);
            var h = m.Player; var b = h.Bot; var map = m.Map;
            Check(map.PadCount == 2, $"FIX-87: Grove has two pads ({map.PadCount})");
            bool spots = true, steps = true, free = true;
            for (int i = 0; i < map.PadCount; i++)
            {
                float px = map.PadX[i], py = map.PadY[i];
                spots &= !b.Walkable(h, px, py) && !b.Walkable(h, px + 0.8f, py) && !b.Walkable(h, px, py - 0.8f);
                free &= b.Walkable(h, px + 1, py) && b.Walkable(h, px - 1, py) && map.PadAt(px + 1, py) < 0 && map.Free(px, py, h.R);
                Place(h, px - 2, py);
                steps &= !b.ClearWalk(h, px + 1, py) && b.ClearWalk(h, px - 4, py); // the pads sit one tile from the right edge, by the wall bar's end, since 2026-11-15
            }
            Check(spots, "FIX-87: a pad tile (and 0.4 round it) is no spot for a bot");
            Check(free, "FIX-87: one tile beside the pad is free, and the pad tile itself is open ground to the rules");
            Check(steps, "FIX-87: a straight walk across a pad is not clear; one beside it is");
            var r = MakeBots(HeroId.Fenn, new[] { HeroId.Moa, HeroId.Kugg }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            Check(r.Map.PadCount == 0 && r.Player.Bot.Walkable(r.Player, 7.5f, 17.5f), "FIX-87: Runes has no pads and the lane's middle is a spot");
        }

        static void TowerPathChecks()
        {
            // nr 88: a path past the own tower keeps off its eight neighbour tiles (a diagonal one's centre is 1.41 from the tower, inside the
            // 1.1 + R wall), the goal tile excepted
            var m = MakeBots(HeroId.Fenn, new[] { HeroId.Moa, HeroId.Kugg }, new[] { HeroId.Tyra, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            var path = new PathBuf();
            for (int t = 0; t < 2; t++)
            {
                var tw = m.Towers[t]; int tx = (int)Math.Floor(tw.X), ty = (int)Math.Floor(tw.Y), up = t == 0 ? -1 : 1; // past the tower towards the middle
                bool ok = m.Map.FindPath(tx - 1.5f, ty - 2 * up + 0.5f, tx + 2.5f, ty + 2 * up + 0.5f, m.Time, path);
                bool hug = false;
                for (int i = 0; i < path.Count - 1; i++) { int cx = (int)Math.Floor(path.X[i]), cy = (int)Math.Floor(path.Y[i]); if (Math.Abs(cx - tx) <= 1 && Math.Abs(cy - ty) <= 1) hug = true; }
                Check(ok && path.Count > 0 && !hug, $"FIX-88: a path past tower {t} never enters its eight neighbour tiles ({path.Count} points)");
                bool ok2 = m.Map.FindPath(tx - 2.5f, ty + 0.5f, tx - 0.5f, ty + up + 0.5f, m.Time, path);
                Check(ok2 && (int)Math.Floor(path.X[path.Count - 1]) == tx - 1 && (int)Math.Floor(path.Y[path.Count - 1]) == ty + up, $"FIX-88: a goal on a diagonal neighbour tile of tower {t} is still reached");
            }
        }

        static void KuggReachChecks()
        {
            // nr 89: in the bots' danger sums an unanchored Kugg's special reaches his cannon's range + 1, not his 8-tile ring; anchored, 8
            var m = MakeBots(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Kugg, HeroId.Bram, HeroId.Blast }, Difficulty.Hard, 7);
            var b = m.Player.Bot; var k = m.Foe(HeroId.Kugg); var br = m.Foe(HeroId.Bram);
            Place(m.Player, 7.5f, 17.5f); Place(k, 7.5f, 12.5f); Place(br, 9.5f, 12.5f);
            k.Charge = 2;
            Check(Near(k.Range, 2.5f) && Near(b.SpecialReach(k), 3.5f), $"FIX-89: a 2-stock Kugg's special reaches 3.5 in the bots' eyes ({b.SpecialReach(k):0.0})");
            Check(Near(b.SpecialReach(br), Match.TapReach(HeroId.Bram)), "FIX-89: the other heroes keep their tap ring");
            k.Charge = 6;
            Check(m.TryCastSpecial(k, float.NaN, 0) && k.KuggAnchored, "FIX-89: Kugg anchors");
            Check(Near(k.Range, 8f) && Near(b.SpecialReach(k), KuggS.TapReach), $"FIX-89: anchored he reaches 8 ({b.SpecialReach(k):0.0})");
        }
    }
}
