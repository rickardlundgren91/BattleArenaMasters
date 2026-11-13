using System;

namespace BAM.Sim
{
    // Drag aim on top of the one-tap special (Rules.DragAimSpecial, tester Ski 2026-11-07 "More control on specials"): a tap rolls the
    // random target, a drag on SPECIAL aims the same one special by hand (Match.TryAimSpecial in OneTap.cs).
    public sealed partial class Match
    {
        static PlayerInput DragIn(float ang, float k) { var i = PlayerInput.None; i.Special = true; i.AimAngle = ang; i.AimK = k; return i; }

        /// <summary>The Cast event of p in the next few steps (the first input sent once): its spot, or false when nothing was cast.</summary>
        static bool CastOf(Match m, Unit p, PlayerInput first, int steps, out float x, out float y)
        {
            x = y = 0; bool cast = false;
            for (int k = 0; k < steps && !cast; k++)
            {
                m.Step(k == 0 ? first : PlayerInput.None);
                foreach (var ev in m.Events) if (ev.Type == EventType.Cast && ev.A == p) { cast = true; x = ev.X; y = ev.Y; }
            }
            return cast;
        }

        static void DragAimChecks()
        {
            Check(Rules.DragAimSpecial, "DRAG: drag aim is on");
            Check(!CanDragAim(HeroId.Bonk) && !CanDragAim(HeroId.Kugg) && CanDragAim(HeroId.Tyra) && CanDragAim(HeroId.Moa), "DRAG: Bonk and Kugg are tap only, the rest can aim");
            foreach (var hero in OneTapHeroes)
            {
                // nobody near: a tap keeps the charge, a drag up the map fires anyway the way it points
                var m = OneTapScene(hero, out var p, out float cx, out float cy);
                Check(m.DragAimOn, $"DRAG {hero}: drag aim on in a normal match");
                float up = SimMath.PI / 2; // +y on the map
                bool cast = CastOf(m, p, DragIn(up, 1), 2, out float ax, out float ay);
                if (!CanDragAim(hero) && !(hero == HeroId.Fenn && !m.FennVeil)) // the wolf Fenn of the older checks still aims (FennVeilTests.cs has the Veil)
                {
                    Check(!cast && p.SpecialReady, $"DRAG {hero}: nothing to aim, so a drag is a tap: nobody near, no cast, the charge stays");
                    continue;
                }
                Check(cast && !p.SpecialReady, $"DRAG {hero}: a drag fires with nobody in reach (cast {cast}, ready {p.SpecialReady})");
                if (!cast) continue;
                if (hero == HeroId.Moa || hero == HeroId.Bram || hero == HeroId.Blast || hero == HeroId.Fenn)
                {   // a placed form lands at most at the white ring, never further than a tap can put it (DragShare)
                    float d = SimMath.Hypot(ax - p.X, ay - p.Y);
                    Check(d <= TapReach(hero) + 0.05f && d > 0.5f && ay > p.Y + 0.5f, $"DRAG {hero}: a full drag lands up the map at the white ring at most ({d:0.00} tiles, ring {TapReach(hero):0.00})");
                }
                else Check(ay > p.Y + 0.5f || (ay >= p.Y - 0.01f && Math.Abs(ax - p.X) < 0.05f), $"DRAG {hero}: the line goes up the map, the way it was dragged ({ax - p.X:0.00}, {ay - p.Y:0.00})");

                // an enemy in reach east, a drag west: the drag wins over the random target
                m = OneTapScene(hero, out p, out cx, out cy);
                var e = m.Heroes.Find(h => h.Team != p.Team);
                Place(e, p.X + Math.Min(2.0f, TapReach(hero) - 0.3f), p.Y); m.AddStatus(e, St.Stun, 999);
                cast = CastOf(m, p, DragIn(SimMath.PI, 0.6f), 2, out ax, out ay);
                Check(cast && ax < p.X + 0.01f, $"DRAG {hero}: dragged west with an enemy east in reach, it goes west ({ax - p.X:0.00})");

                // the same red warning as a tap: aim locked, then the cast
                m = OneTapScene(hero, out p, out cx, out cy);
                m.Telegraph = true;
                bool warned = false; float wx = 0, castAt = -1;
                for (int k = 0; k < 60; k++)
                {
                    m.Step(k == 0 ? DragIn(SimMath.PI, 0.6f) : PlayerInput.None);
                    foreach (var ev in m.Events)
                    {
                        if (ev.Type == EventType.Telegraph && ev.A == p) { warned = true; wx = ev.X; }
                        if (ev.Type == EventType.Cast && ev.A == p && castAt < 0) castAt = m.Time;
                    }
                }
                Check(warned && castAt >= 10 + TeleTime(hero) - 0.02f && wx < p.X + 0.01f, $"DRAG {hero}: the drag warns like a tap, aim locked west, cast after {TeleTime(hero)} s (warned {warned}, cast at +{castAt - 10:0.00} s)");
            }

            // Moa: a drag puts the field where she aims, not on the random ally
            {
                var m = OneTapScene(HeroId.Moa, out var p, out float cx, out float cy);
                var a = m.Heroes.Find(h => h.Team == p.Team && h != p); Place(a, cx + 1.4f, cy);
                m.Step(DragIn(SimMath.PI / 2, 0.5f));
                Proj f = null; foreach (var q in m.Projs) if (q.Mode == ProjMode.HoneyField && q.Src == p) f = q;
                Check(f != null && f.Y > p.Y + 0.5f && Math.Abs(f.X - p.X) < 0.05f, "DRAG Moa: the field lands up the map where it was dragged, not on the ally east");
            }

            // a drag aim switched off in the match (the guided match uses the same switch, DragAimOn): a drag is a tap
            {
                var m = OneTapScene(HeroId.Tyra, out var p, out float cx, out float cy);
                m.DragAim = false;
                bool cast = CastOf(m, p, DragIn(SimMath.PI / 2, 1), 2, out _, out _);
                Check(!m.DragAimOn && !cast && p.SpecialReady, "DRAG off: with nobody in reach a drag does nothing, as a tap");
            }

            // Easy and Normal bots never drag (Bronze and Silver tap): a Normal bot match plays exactly the same with drag aim on and off
            // (same brain on both teams). Gold, Platinum and Ultra drag since the bot audit of 2026-11-13 (HeroReview1113Tests BotDragChecks)
            for (int s = 1; s <= 3; s++)
            {
                string[] end = new string[2];
                for (int v = 0; v < 2; v++)
                {
                    var m = Create(new MatchConfig { PlayerHero = OneTapHeroes[s], Allies = new[] { OneTapHeroes[(s + 1) % 8], OneTapHeroes[(s + 2) % 8] }, Enemies = new[] { OneTapHeroes[(s + 3) % 8], OneTapHeroes[(s + 4) % 8], OneTapHeroes[(s + 5) % 8] }, Seed = 40 + s, PlayerIsBot = true, Difficulty = Difficulty.Normal });
                    m.DragAim = v == 0;
                    int casts = 0;
                    while (m.State != MatchState.Over && m.Time < 180) { m.Step(PlayerInput.None); foreach (var ev in m.Events) if (ev.Type == EventType.Cast) casts++; }
                    float hp = 0; foreach (var h in m.Heroes) hp += h.Hp;
                    end[v] = $"{m.Time:0.000} {casts} {hp:0.0}";
                }
                Check(end[0] == end[1], $"DRAG bots: a Normal bot match (seed {40 + s}) plays the same with drag aim on and off ({end[0]} / {end[1]})");
            }
        }
    }
}
