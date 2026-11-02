using System;
using System.Text;

namespace BAM.Sim
{
    // The warning before a tapped special (Rules.SpecialTelegraph, Sim/Telegraph.cs; tester feedback 2026-11-02).
    public sealed partial class Match
    {
        /// <summary>How the enemy moves in a timing run: stands, runs straight away from the caster, or runs across the line.</summary>
        enum Flee { Still, Away, Side }

        /// <summary>One timing run: hero taps at an enemy hero tapDist tiles off (inside its ring), the enemy moves as told from
        /// react s after the tap. Returns the seconds from the tap to the special's first hit on it, or NaN if it got away
        /// (nothing within 1.5 s).</summary>
        static float TeleRun(HeroId hero, bool tele, Flee flee, float react, out float speed)
        {
            var m = OneTapScene(hero, out var p, out float cx, out float cy);
            m.Telegraph = tele;
            Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
            float d = Math.Min(2.9f, TapReach(hero) - 0.3f);
            Place(e, p.X + d, p.Y); m.AddStatus(e, St.Stun, 999); // stunned: it does nothing by itself, the run moves it
            speed = m.SpeedOf(e);
            m.Events.Clear();
            float t0 = m.Time;
            for (int k = 0; k < 90; k++)
            {
                if (flee != Flee.Still && m.Time - t0 >= react - 1e-4f)
                {
                    if (flee == Flee.Away) m.MoveUnit(e, 1, 0, Rules.Tick); else m.MoveUnit(e, 0, 1, Rules.Tick);
                }
                m.Step(k == 0 ? TapIn() : PlayerInput.None);
                foreach (var ev in m.Events) if (ev.Type == EventType.Hit && ev.B == e && ev.Dmg == DmgKind.Special) return m.Time - t0;
            }
            return float.NaN;
        }

        /// <summary>The timing table (env BAM_TELEGRAPH_TABLE=1 prints it): per hero, tap to hit on a still enemy, and on one
        /// that runs away at once, away after 0.25 s (a quick human), across after 0.25 s; without and with the warning.</summary>
        static string TelegraphTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("hero    | dist | speed | warning | still | away 0 s | away 0.25 s | across 0.25 s");
            foreach (var hero in OneTapHeroes)
            {
                if (hero == HeroId.Moa) continue;
                foreach (bool tele in new[] { false, true })
                {
                    float sp = 0;
                    string F(float v) => float.IsNaN(v) ? "MISS" : v.ToString("0.00") + " s";
                    float still = TeleRun(hero, tele, Flee.Still, 0, out sp);
                    float a0 = TeleRun(hero, tele, Flee.Away, 0, out _), a25 = TeleRun(hero, tele, Flee.Away, 0.25f, out _), s25 = TeleRun(hero, tele, Flee.Side, 0.25f, out _);
                    sb.AppendLine($"{hero,-7} | {Math.Min(2.9f, TapReach(hero) - 0.3f):0.0}  | {sp:0.00}  | {(tele ? "on " : "off")}     | {F(still),-6}| {F(a0),-9}| {F(a25),-12}| {F(s25)}");
                }
            }
            return sb.ToString();
        }

        static void TelegraphChecks()
        {
            Check(Rules.SpecialTelegraph && Rules.TelegraphTime > 0.25f, "TELE: tapped specials warn first");
            if (Environment.GetEnvironmentVariable("BAM_TELEGRAPH_TABLE") == "1") Console.Write(TelegraphTable());
            foreach (var hero in OneTapHeroes)
            {
                int ticks = (int)Math.Round(TeleTime(hero) / Rules.Tick);
                var m = OneTapScene(hero, out var p, out float cx, out float cy);
                m.Telegraph = true;
                Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                float d = Math.Min(2.9f, TapReach(hero) - 0.3f);
                Place(e, p.X + d, p.Y); m.AddStatus(e, St.Stun, 999);
                m.Events.Clear();
                m.Step(TapIn());
                bool tg = false, cast = false, warn = false;
                foreach (var ev in m.Events) { if (ev.Type == EventType.Telegraph && ev.A == p) tg = true; if (ev.Type == EventType.Cast && ev.A == p) cast = true; }
                foreach (var q in m.Projs) if (q.IsWarning && q.Src == p && !q.Done) warn = true;
                if (hero == HeroId.Moa)
                {   // a shield on a friend: she waits as long and flashes, but lays no red warning (enemy bots must not run from it)
                    Check(tg && !cast && !warn && Telegraphing(p), $"TELE Moa: her shield waits too, with the flash and no red warning (flash {tg}, cast {cast}, warning {warn})");
                    for (int k = 0; k < ticks + 2; k++) m.Step(PlayerInput.None);
                    Check(!p.SpecialReady, "TELE Moa: her field comes after the wait");
                    continue;
                }
                Check(tg && !cast && warn && Telegraphing(p) && p.SpecialReady, $"TELE {hero}: the tap shows a red warning and the flash first, no cast yet (warning {warn}, flash {tg}, cast {cast})");
                Check(p.Has(St.Casting) && p.Has(St.Channel), $"TELE {hero}: the hero stands in the warning, with the interrupt bar");
                // the enemy steps across the line in the warning: the aim stays where it was locked at the tap
                Place(e, e.X, e.Y + 1.2f); m.AddStatus(e, St.Stun, 999);
                int castAt = -1; float ax = 0, ay = 0;
                for (int k = 1; k < ticks + 10 && castAt < 0; k++)
                {
                    m.Step(PlayerInput.None);
                    foreach (var ev in m.Events) if (ev.Type == EventType.Cast && ev.A == p) { castAt = k; ax = ev.X; ay = ev.Y; }
                }
                Check(castAt >= ticks - 1 && castAt <= ticks + 1, $"TELE {hero}: the cast follows {TeleTime(hero)} s after the tap (tick {castAt}, want {ticks})");
                if (hero != HeroId.Bonk)
                {
                    float off = Math.Abs(ay - cy); // the enemy stood on y = cy at the tap: a locked aim stays on that line
                    Check(off < 0.05f, $"TELE {hero}: the aim is locked at the tap ({off:0.000} tiles off the first line)");
                }
                Check(!p.SpecialReady, $"TELE {hero}: the charge is spent at the cast");

                // a stun in the warning cuts it: no cast, the charge stays, the warning goes
                m = OneTapScene(hero, out p, out cx, out cy);
                m.Telegraph = true;
                e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                Place(e, p.X + d, p.Y); m.AddStatus(e, St.Stun, 999);
                m.Step(TapIn());
                m.Step(PlayerInput.None);
                m.AddStatus(p, St.Stun, 0.5f);
                bool cut = false, castAfter = false;
                foreach (var ev in m.Events) if (ev.Type == EventType.Interrupt && ev.A == p) cut = true;
                for (int k = 0; k < ticks + 10; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) if (ev.Type == EventType.Cast && ev.A == p) castAfter = true; }
                bool warnLeft = false; foreach (var q in m.Projs) if (q.IsWarning && q.Src == p && !q.Done) warnLeft = true;
                Check(cut && !castAfter && p.SpecialReady && !Telegraphing(p) && !warnLeft, $"TELE {hero}: a stun in the warning cuts it, no cast, the charge stays (cut {cut}, cast {castAfter}, warning left {warnLeft})");
            }

            // nothing homes: Fenn's wolf lands on the locked spot, an enemy that stepped 1.2 tiles aside is not bitten
            {
                var m = OneTapScene(HeroId.Fenn, out var p, out _, out _);
                m.Telegraph = true;
                Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                Place(e, p.X + 2.7f, p.Y); m.AddStatus(e, St.Stun, 999);
                m.Step(TapIn());
                for (int k = 0; k < 10; k++) m.Step(PlayerInput.None);
                Place(e, e.X - 1.0f, e.Y + 1.25f); m.AddStatus(e, St.Stun, 999); // inside the old homing strip, outside the landing circle
                bool bit = false; float lx = 0, ly = 0;
                for (int k = 0; k < 60; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) { if (ev.Type == EventType.WolfPounce) { lx = ev.X; ly = ev.Y; } if (ev.Type == EventType.Hit && ev.B == e && ev.Dmg == DmgKind.Special) bit = true; } }
                Check(!bit && Math.Abs(lx - p.TeleX) < 0.05f && Math.Abs(ly - p.TeleY) < 0.05f, $"TELE Fenn: the wolf lands on the locked spot and homes on nobody (bit {bit}, landed {Math.Abs(lx - p.TeleX):0.00}/{Math.Abs(ly - p.TeleY):0.00} off)");
            }
            // Bonk's slam goes off after its warning even if everyone ran
            {
                var m = OneTapScene(HeroId.Bonk, out var p, out _, out _);
                m.Telegraph = true;
                Unit e = null; foreach (var h in m.Heroes) if (h.Team != p.Team) { e = h; break; }
                Place(e, p.X + 2.6f, p.Y); m.AddStatus(e, St.Stun, 999);
                m.Step(TapIn());
                Place(e, p.X + 3.6f, p.Y); m.AddStatus(e, St.Stun, 999);
                bool cast = false, hit = false;
                for (int k = 0; k < 60; k++) { m.Step(PlayerInput.None); foreach (var ev in m.Events) { if (ev.Type == EventType.Cast && ev.A == p) cast = true; if (ev.Type == EventType.Hit && ev.B == e && ev.Dmg == DmgKind.Special) hit = true; } }
                Check(cast && !hit && !p.SpecialReady, $"TELE Bonk: the warned slam goes off even when the enemy ran out (cast {cast}, hit {hit})");
            }
            // the point of it: a player who runs away at the flash gets out of the ones that land on a spot or fly a fixed reach,
            // where without the warning they were sure hits (Knut's hook, Tikta's needles, Fenn's wolf, Blast's bomb with its 0.5 s); from Bram's anvil even
            // 0.25 s after it (a quick human). The full table: BAM_TELEGRAPH_TABLE=1
            foreach (var (hero, react) in new[] { (HeroId.Knut, 0f), (HeroId.Tikta, 0f), (HeroId.Fenn, 0f), (HeroId.Blast, 0f), (HeroId.Bram, 0.25f) })
            {
                float before = TeleRun(hero, false, Flee.Away, react, out _), after = TeleRun(hero, true, Flee.Away, react, out _);
                Check(!float.IsNaN(before) && float.IsNaN(after), $"TELE {hero}: running away {react} s after the tap got hit before ({before:0.00} s), now gets away ({after:0.00})");
            }
            // the guided match keeps the old instant tap (its special lesson must never stall)
            {
                var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Moa, HeroId.Bram }, Enemies = new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta }, Seed = 3, Tutorial = true });
                Check(m.Tut == null || !m.TeleOn, "TELE: no warning in the guided match");
            }
        }
    }
}
