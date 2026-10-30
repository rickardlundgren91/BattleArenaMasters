using System;
using System.Collections.Generic;
using BAM.Online.Mp;

namespace BAM.Sim
{
    // Precision tests on set-up scenes: math and wire precision, D1 targeting, walls, hits, interrupts and the input path.
    public sealed partial class Match
    {
        // ---------------------------------------------------------------- math and wire
        static void PrecMath()
        {
            // SimMath's own sine/atan2 (deterministic on every phone) against the system's
            double sinErr = 0, atanErr = 0;
            var r = new Random(1);
            for (int i = 0; i < 20000; i++)
            {
                float a = (float)(r.NextDouble() * 20 - 10);
                sinErr = Math.Max(sinErr, Math.Abs(SimMath.Sin(a) - Math.Sin(a)));
                sinErr = Math.Max(sinErr, Math.Abs(SimMath.Cos(a) - Math.Cos(a)));
                float y = (float)(r.NextDouble() * 2 - 1), x = (float)(r.NextDouble() * 2 - 1);
                double d = Math.Abs(SimMath.Atan2(y, x) - Math.Atan2(y, x)); if (d > Math.PI) d = 2 * Math.PI - d;
                atanErr = Math.Max(atanErr, d);
            }
            Verdict("SimMath_TrigAccuracy", "math", sinErr < 1e-5 && atanErr < 1e-5, "P3",
                $"max |sin/cos err| {sinErr:E1}, max |atan2 err| {atanErr:E1} rad (9 tiles out: {atanErr * 9:E1} tile)");

            // the online wire: what a drag, a move and a tap become after packing (the host steps with the same round trip)
            var w = new MpWriter();
            double angErr = 0, kErr = 0, mvErr = 0; bool flags = true, nanOk = true;
            for (int i = 0; i < 20000; i++)
            {
                var pi = PlayerInput.None;
                pi.MoveX = (float)(r.NextDouble() * 2 - 1); pi.MoveY = (float)(r.NextDouble() * 2 - 1);
                pi.Special = r.NextDouble() < 0.5; pi.Spell = r.NextDouble() < 0.5;
                pi.AimAngle = r.NextDouble() < 0.3 ? float.NaN : (float)(r.NextDouble() * 2 * Math.PI - Math.PI); pi.AimK = (float)r.NextDouble();
                pi.SpellAngle = (float)(r.NextDouble() * 2 * Math.PI - Math.PI); pi.SpellK = (float)r.NextDouble();
                var o = MpInput.RoundTrip(in pi, w);
                if (o.Special != pi.Special || o.Spell != pi.Spell) flags = false;
                mvErr = Math.Max(mvErr, Math.Max(Math.Abs(o.MoveX - pi.MoveX), Math.Abs(o.MoveY - pi.MoveY)));
                if (pi.Special)
                {
                    if (float.IsNaN(pi.AimAngle) != float.IsNaN(o.AimAngle)) nanOk = false;
                    else if (!float.IsNaN(pi.AimAngle)) { double d = Math.Abs(SimMath.AngDiff(o.AimAngle, pi.AimAngle)); angErr = Math.Max(angErr, d); }
                    kErr = Math.Max(kErr, Math.Abs(o.AimK - pi.AimK));
                }
            }
            Verdict("Wire_InputRoundTrip", "online", flags && nanOk && angErr < 1e-4 && kErr <= 1 / 255.0 && mvErr <= 1 / 127.0, "P1",
                $"angle err {angErr:E1} rad ({angErr * 9:E1} tile at 9 tiles), aimK err {kErr:0.0000}, move err {mvErr:0.0000}, tap(NaN) kept {nanOk}, flags kept {flags}");

            // the match clock: Time adds 1/60 s each tick as a float; Ticks counts them exactly
            var m = PMake(HeroId.Bram, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Knut, HeroId.Tikta });
            m.Time = 0; m.Ticks = 0;
            for (int i = 0; i < m.Heroes.Count; i++) POff(m.Heroes[i]);
            double worst = 0; int at = 0;
            for (int k = 1; k <= 60 * 60 * 15; k++)
            {
                m.Step(PlayerInput.None);
                double err = Math.Abs(m.Time - m.Ticks / 60.0);
                if (err > worst) { worst = err; at = k; }
                if (m.State != MatchState.Play) break;
            }
            Verdict("MatchClock_FloatDrift", "timing", worst < 0.05, "P3",
                $"after 15 min the float match clock is off the tick count by up to {worst * 1000:0} ms (worst at tick {at}); level-ups, waves and HUD timers read Time");
        }

        // ---------------------------------------------------------------- D1 targeting
        static void PrecTargeting()
        {
            // Tyra in the west lane by the middle rocks; enemies set so each rule is tested: in reach, behind the rock, hidden in
            // grass, out of reach, untargetable
            var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var t = m.Player; var e1 = m.PFoe(HeroId.Bram); var wall = m.PFoe(HeroId.Knut); var grass = m.PFoe(HeroId.Tikta);
            POff(m.PAlly(HeroId.Moa)); POff(m.PAlly(HeroId.Fenn));
            PDummy(e1); PDummy(wall); PDummy(grass);
            PPlace(t, 2.0f, 15.5f); PPlace(e1, 2.0f, 18.5f); PPlace(wall, 5.5f, 17.5f); PPlace(grass, 1.5f, 12.5f);
            var mn = m.NewMinion(MinionKind.Melee, Teams.Enemy, 5.0f, 14.0f);     // in reach
            var far = m.NewMinion(MinionKind.Melee, Teams.Enemy, 7.0f, 15.5f);    // out of reach
            var untg = m.NewMinion(MinionKind.Melee, Teams.Enemy, 3.5f, 13.6f);   // untargetable
            bool pre = m.Map.LosClear(t, e1) && !m.Map.LosClear(t, wall) && m.Map.InGrass(grass.X, grass.Y) && SimMath.Dist(t, grass) > Rules.GrassSight
                && SimMath.Dist(t, wall) <= t.Range + wall.R && SimMath.Dist(t, grass) <= t.Range + grass.R && SimMath.Dist(t, far) > t.Range + far.R && m.Map.LosClear(t, mn);
            var picks = new Dictionary<int, int>();
            int bad = 0, shots = 0;
            for (int k = 0; k < 60 * 120; k++)
            {
                foreach (var u in new[] { e1, wall, grass }) { u.Hp = u.MaxHp; u.RevealT = 0; if (!u.Has(St.Disarm)) m.AddStatus(u, St.Disarm, 1e6f); }
                t.Hp = t.MaxHp; // the dummies do not shoot back, and Tyra never dies
                foreach (var u in new[] { mn, far, untg }) { u.Hp = u.MaxHp; u.Alive = true; if (!u.Has(St.Stun)) m.AddStatus(u, St.Stun, 1e6f); }
                if (!untg.Has(St.Untargetable)) m.AddStatus(untg, St.Untargetable, 1e6f);
                m.Step(PlayerInput.None);
                foreach (var e in m.Events)
                {
                    if (e.Type != EventType.AttackStart || e.A != t) continue;
                    shots++;
                    picks[e.BSerial] = picks.TryGetValue(e.BSerial, out int c) ? c + 1 : 1;
                    if (e.B != e1 && e.B != mn) bad++;
                }
            }
            picks.TryGetValue(e1.Serial, out int n1); picks.TryGetValue(mn.Serial, out int n2);
            double share = shots > 0 ? n1 / (double)shots : 0;
            Verdict("TargetingD1_EligibleOnly", "targeting", pre && bad == 0 && shots > 30, "P0",
                $"{shots} auto attacks, {bad} at a target that is behind the rock, hidden in grass, out of reach or untargetable (set-up valid: {pre})");
            // D1: random among the valid ones: a fair coin over ~70 shots stays within 0.3..0.7
            Verdict("TargetingD1_SeededRandom_Fair", "targeting", shots > 30 && share > 0.3 && share < 0.7, "P2",
                $"hero {n1} vs minion {n2} picks ({share:P0} hero), seed 7");
        }

        // ---------------------------------------------------------------- walls
        static void PrecWalls()
        {
            // an auto attack whose target steps behind the rock during the flight is stopped there: Blocked, no Hit, no damage
            int hits = 0, blocked = 0, cases = 0;
            foreach (var hero in new[] { HeroId.Bram, HeroId.Tyra, HeroId.Moa, HeroId.Tikta, HeroId.Blast, HeroId.Hilde })
            {
                var m = PMake(hero, new[] { HeroId.Knut, HeroId.Fenn }, new[] { HeroId.Bonk, HeroId.Oaken, hero == HeroId.Tikta ? HeroId.Tyra : HeroId.Tikta });
                var s = m.Player; var e = m.PFoe(HeroId.Bonk);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e);
                PPlace(s, 2.0f, 15.2f); PPlace(e, 2.0f, 15.2f + Math.Min(2.5f, s.Range - 0.3f));
                var ev = new List<SimEvent>(); bool moved = false;
                for (int k = 0; k < 120 && !moved; k++)
                {
                    m.Step(PlayerInput.None); ev.AddRange(m.Events);
                    foreach (var p in m.Projs) if (p.Mode == ProjMode.Auto && p.Src == s && p.Flying && !moved)
                        {
                            // the target dashes behind the rock (east of it, row 17) once the shot is out
                            e.X = e.PX = 5.6f; e.Y = e.PY = 17.5f; moved = true;
                        }
                }
                for (int k = 0; k < 30; k++) { m.Step(PlayerInput.None); ev.AddRange(m.Events); if (m.Projs.Count == 0) break; }
                if (!moved) continue;
                cases++;
                foreach (var x in ev) { if (x.Type == EventType.Hit && x.A == s && x.B == e && e.Hp < e.MaxHp) hits++; if (x.Type == EventType.Blocked && x.A == s) blocked++; }
            }
            Verdict("LOS_ShotStopsAtWall", "walls", cases >= 4 && hits == 0 && blocked == cases, "P0",
                $"{cases} heroes' shots whose target ducked behind a rock mid-flight: {blocked} Blocked, {hits} hits through the rock");

            // aimed specials never hit through a wall: random casts toward enemies that the caster has no clear line to
            var rnd = new Random(11); int tries = 0, through = 0; string first = null;
            var lineHeroes = new[] { HeroId.Tyra, HeroId.Tikta, HeroId.Bonk, HeroId.Knut, HeroId.Oaken }; // Blast's drag bomb is thrown over walls (2026-10-30)
            var map = new Arena();
            for (int c = 0; c < 20000 && tries < 300; c++)
            {
                var hero = lineHeroes[tries % lineHeroes.Length];
                float sx = (float)(rnd.NextDouble() * 13 + 1), sy = (float)(rnd.NextDouble() * 10 + 12);
                float ex = (float)(rnd.NextDouble() * 13 + 1), ey = (float)(rnd.NextDouble() * 10 + 12);
                float d = SimMath.Hypot(ex - sx, ey - sy);
                if (!map.Free(sx, sy, 0.4f) || !map.Free(ex, ey, 0.4f) || d < 1 || d > Defs.Hero(hero).DragReach || map.LosClear(sx, sy, ex, ey)) continue;
                var foe = hero == HeroId.Bram ? HeroId.Moa : HeroId.Bram;
                var m = PMake(hero, new[] { HeroId.Fenn, HeroId.Moa }, new[] { foe, hero == HeroId.Tikta ? HeroId.Tyra : HeroId.Tikta, hero == HeroId.Knut ? HeroId.Bonk : HeroId.Knut });
                var s = m.Player; var e = m.PFoe(foe);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e);
                PPlace(s, sx, sy); PPlace(e, ex, ey); s.Charge = s.HDef.MaxCharge; m.AddStatus(s, St.Disarm, 1e6f);
                tries++;
                var ev = m.PRun(150, PDrag(SimMath.Atan2(ey - sy, ex - sx)), PlayerInput.None);
                bool hit = false; foreach (var x in ev) if ((x.Type == EventType.Hit || x.Type == EventType.Pull || x.Type == EventType.Knock) && x.B == e && (x.A == s || (x.A != null && x.A.Owner == s))) hit = true;
                if (hit) { through++; if (first == null) first = $"{hero} at ({F(sx)},{F(sy)}) -> ({F(ex)},{F(ey)})"; }
            }
            Verdict("LOS_AimedSpecialNeverThroughWall", "walls", through == 0 && tries > 50, "P0",
                $"{tries} drag casts at an enemy behind a wall (Tyra, Tikta, Bonk, Knut, Oaken): {through} hit it{(first != null ? "; first: " + first : "")}");
        }

        // ---------------------------------------------------------------- hits
        static void PrecHits()
        {
            // a shield takes the whole hit: Hit V 0, no Damage event, health kept
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != t && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(t, 7.5f, 20); PPlace(e, 7.5f, 17.5f);
                m.AddStatus(e, St.Shield, 100, 0, 5000);
                var ev = m.PRun(150, PlayerInput.None, PlayerInput.None);
                int hit0 = 0, hitPos = 0, dmg = 0; foreach (var x in ev) { if (x.Type == EventType.Hit && x.B == e) { if (x.V == 0) hit0++; else hitPos++; } if (x.Type == EventType.Damage && x.B == e) dmg++; }
                Verdict("ShieldHit_ZeroDamage", "hits", hit0 > 0 && hitPos == 0 && dmg == 0 && e.Hp == e.MaxHp, "P1",
                    $"{hit0} hits on a full shield with V 0, {hitPos} with damage, {dmg} Damage events, health {F(e.Hp)}/{F(e.MaxHp)}");
                Info("ShieldHit_GivesCharge", "hits", $"hits into the shield still charged Tyra's special: {F(t.Charge)} of {t.HDef.MaxCharge} (old game: the same, as an on-hit)");
            }
            // an invulnerable hero (4 s after respawn): Hit V 0 still charges the attacker's special
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != t && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(t, 7.5f, 20); PPlace(e, 7.5f, 17.5f);
                m.AddStatus(e, St.Invulnerable, 100);
                var ev = m.PRun(60 * 8, PlayerInput.None, PlayerInput.None);
                int hit0 = 0; foreach (var x in ev) if (x.Type == EventType.Hit && x.B == e && x.V == 0) hit0++;
                Verdict("Invulnerable_NoChargeFarm", "hits", t.Charge == 0, "P2",
                    $"{hit0} hits on an invulnerable hero (0 damage) gave Tyra {F(t.Charge)} charge (SUSPECTED design question: respawn protection can be farmed for the special; OnHeroHit has no dealt>0 check)");
            }
            // burn and poison ticks never send Hit events (the art would show an auto hit that did not happen)
            {
                var m = PMake(HeroId.Blast, new[] { HeroId.Tikta, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tyra });
                var b = m.Player; var tk = m.PAlly(HeroId.Tikta); var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != b && m.Heroes[i] != e && m.Heroes[i] != tk) POff(m.Heroes[i]);
                PDummy(e); PDummy(tk); PPlace(b, 7.5f, 20); PPlace(tk, 6.5f, 20); PPlace(e, 7.5f, 15);
                m.AddStatus(b, St.Disarm, 1e6f); m.AddStatus(tk, St.Disarm, 1e6f);
                m.Ignite(b, e, 100); m.TiktaPoison(tk, e, 20, 2);
                var ev = m.PRun(60 * 3, PlayerInput.None, PlayerInput.None);
                int hits = 0, ticks = 0; foreach (var x in ev) { if (x.Type == EventType.Hit) hits++; if (x.Type == EventType.Damage && x.B == e) ticks++; }
                Verdict("TickDamage_NoFakeHit", "hits", hits == 0 && ticks >= 4, "P1", $"{ticks} burn/poison ticks, {hits} Hit events");
            }
            // two killing shots land in the same tick: one death, one kill, no hit on the dead
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var a = m.Player; var b = m.PAlly(HeroId.Moa); var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != a && m.Heroes[i] != b && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(b); PDummy(e); PPlace(a, 7.5f, 20); PPlace(b, 8.5f, 20); PPlace(e, 7.5f, 17.5f);
                m.AddStatus(a, St.Disarm, 1e6f); m.AddStatus(b, St.Disarm, 1e6f);
                e.Hp = 1;
                foreach (var s in new[] { a, b }) { var p = m.NewProj(ProjMode.Auto, s, s.X, s.Y); p.Kind = s.HDef.Attack; p.Tgt = e; p.Dmg = 50; p.Travel = 0.1f; p.OnHit = true; }
                var ev = m.PRun(12, PlayerInput.None, PlayerInput.None);
                int deaths = 0, hits = 0;
                foreach (var x in ev) { if (x.Type == EventType.Death && x.B == e) deaths++; else if (x.Type == EventType.Hit && x.B == e) hits++; }
                Verdict("SameTick_TwoLethalShots", "hits", deaths == 1 && hits == 1 && a.Kills + b.Kills == 1, "P0",
                    $"both shots due in the same tick: {deaths} death, {hits} hit on the target (the second shot does not hit the dead), kills {a.Kills}+{b.Kills}");
            }
            // Arrow Volley: three enemies in the fan, each hurt once and shown once
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var e1 = m.PFoe(HeroId.Bram); var e2 = m.PFoe(HeroId.Knut); var e3 = m.PFoe(HeroId.Tikta);
                POff(m.PAlly(HeroId.Moa)); POff(m.PAlly(HeroId.Fenn));
                PDummy(e1); PDummy(e2); PDummy(e3);
                PPlace(t, 7.5f, 21); PPlace(e1, 7.5f, 18.5f); PPlace(e2, 7.0f, 18.0f); PPlace(e3, 8.0f, 18.0f);
                m.AddStatus(t, St.Disarm, 1e6f); t.Charge = t.HDef.MaxCharge;
                var ev = m.PRun(90, PTap(), PlayerInput.None);
                var hitsPer = new Dictionary<int, int>(); var dmgPer = new Dictionary<int, int>();
                foreach (var x in ev)
                {
                    if (x.A != t) continue;
                    if (x.Type == EventType.Hit) hitsPer[x.BSerial] = hitsPer.TryGetValue(x.BSerial, out int c) ? c + 1 : 1;
                    if (x.Type == EventType.Damage) dmgPer[x.BSerial] = dmgPer.TryGetValue(x.BSerial, out int c2) ? c2 + 1 : 1;
                }
                bool once = true; foreach (var kv in hitsPer) if (kv.Value != 1) once = false; foreach (var kv in dmgPer) if (kv.Value != 1) once = false;
                Verdict("Volley_MultiHit_OncePerEnemy", "hits", once && hitsPer.Count == 3, "P0", $"{hitsPer.Count} enemies hit; hits per enemy [{string.Join(",", hitsPer.Values)}], damage per enemy [{string.Join(",", dmgPer.Values)}]");
            }
            // area hits: Bonk's slam on three enemies is three Hit events, each at that enemy
            {
                var m = PMake(HeroId.Bonk, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var b = m.Player; var es = new[] { m.PFoe(HeroId.Bram), m.PFoe(HeroId.Knut), m.PFoe(HeroId.Tikta) };
                POff(m.PAlly(HeroId.Moa)); POff(m.PAlly(HeroId.Fenn));
                PPlace(b, 7.5f, 20); PPlace(es[0], 8.5f, 20); PPlace(es[1], 6.5f, 20); PPlace(es[2], 7.5f, 19);
                foreach (var e in es) PDummy(e);
                m.AddStatus(b, St.Disarm, 1e6f); b.Charge = b.HDef.MaxCharge;
                var ev = m.PRun(60, PTap(), PlayerInput.None);
                int hits = 0, atTarget = 0; var seen = new HashSet<int>();
                foreach (var x in ev) if (x.Type == EventType.Hit && x.A == b && (ProjKind)x.Kind == ProjKind.BonkSlam) { hits++; seen.Add(x.BSerial); if (Near(x.X, x.B.X) && Near(x.Y, x.B.Y)) atTarget++; }
                Verdict("AoE_OverlappingHits_OnePerTarget", "hits", hits == 3 && seen.Count == 3 && atTarget == 3, "P0", $"{hits} slam hits on {seen.Count} enemies, {atTarget} drawn at the enemy itself");
            }
            // a shot whose shooter dies in the wind-up never flies
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != t && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(t, 7.5f, 20); PPlace(e, 7.5f, 17.5f);
                var ev = new List<SimEvent>(); bool killed = false;
                for (int k = 0; k < 120; k++)
                {
                    m.Step(PlayerInput.None); ev.AddRange(m.Events);
                    bool started = false; foreach (var x in m.Events) if (x.Type == EventType.AttackStart && x.A == t) started = true;
                    if (!killed && started) { m.DealDamage(e, t, 1e6f, DmgKind.Auto); killed = true; t.RespawnT = 1e9f; }
                }
                int hits = 0; foreach (var x in ev) if (x.Type == EventType.Hit && x.A == t) hits++;
                Verdict("DeadShooter_InHand_NoShot", "hits", killed && hits == 0, "P0", $"Tyra killed in her wind-up: {hits} hits from the shot in her hand");
            }
        }
        static bool Near(float a, float b, float eps = 0.01f) => Math.Abs(a - b) <= eps;

        // ---------------------------------------------------------------- interrupts
        static void PrecInterrupts()
        {
            // a stun during a special's wind-up cuts it: no shot, no hit, the warning goes
            var cut = new List<string>(); int bad = 0;
            foreach (var (hero, drag) in new[] { (HeroId.Tyra, true), (HeroId.Bonk, true), (HeroId.Bonk, false), (HeroId.Knut, false), (HeroId.Oaken, false), (HeroId.Oaken, true), (HeroId.Tyra, false) })
            {
                var m = PMake(hero, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Tikta, hero == HeroId.Knut ? HeroId.Blast : HeroId.Knut });
                var s = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(s, 7.5f, 21); PPlace(e, 7.5f, 19.6f);
                m.AddStatus(s, St.Disarm, 1e6f); s.Charge = s.HDef.MaxCharge;
                var ev = m.PRun(1, drag ? PDrag(-SimMath.PI / 2) : PTap(), PlayerInput.None);
                bool cast = false; foreach (var x in ev) if (x.Type == EventType.Cast) cast = true;
                m.AddStatus(s, St.Stun, 1.0f); ev.AddRange(m.Events); // the Interrupt is emitted outside a Step
                m.PRun(90, PlayerInput.None, PlayerInput.None, ev);
                int hits = 0, interrupts = 0; foreach (var x in ev) { if (x.Type == EventType.Hit && x.A == s) hits++; if (x.Type == EventType.Interrupt && x.A == s) interrupts++; }
                bool warn = false; foreach (var p in m.Projs) if (p.IsWarning && p.Src == s && !p.Done) warn = true;
                string name = hero + (drag ? " drag" : " tap");
                if (!cast || hits > 0 || interrupts != 1 || warn) { bad++; cut.Add($"{name}: cast {cast}, {hits} hits, {interrupts} interrupts, warning left {warn}"); }
            }
            Verdict("InterruptBeforeRelease_Special_NoProjectile", "interrupts", bad == 0, "P0", bad == 0 ? "7 wind-up specials cut by a stun: no hit, one Interrupt, warning gone" : string.Join("; ", cut));

            // a stun during an AUTO attack's wind-up: does the shot still leave the stunned hero's hand?
            var flew = new List<string>();
            foreach (var hero in new[] { HeroId.Tyra, HeroId.Bram, HeroId.Bonk, HeroId.Hilde, HeroId.Fenn })
            {
                var m = PMake(hero, new[] { HeroId.Moa, HeroId.Tikta }, new[] { HeroId.Knut, HeroId.Blast, hero == HeroId.Fenn ? HeroId.Tyra : HeroId.Fenn });
                var s = m.Player; var e = m.PFoe(HeroId.Knut);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(s, 7.5f, 20); PPlace(e, 7.5f, 20 - Math.Min(1.6f, s.Range));
                var ev = new List<SimEvent>(); float stunAt = -1;
                for (int k = 0; k < 200; k++)
                {
                    m.Step(PlayerInput.None); ev.AddRange(m.Events);
                    bool started = false; foreach (var x in m.Events) if (x.Type == EventType.AttackStart && x.A == s) started = true;
                    if (stunAt < 0 && started) { m.AddStatus(s, St.Stun, 1.5f); stunAt = m.Time; } // after the loop: the stun now emits an Interrupt
                    if (stunAt >= 0 && m.Time > stunAt + 1.0f) break;
                }
                int hits = 0; foreach (var x in ev) if (x.Type == EventType.Hit && x.A == s) hits++;
                if (hits > 0) flew.Add(hero.ToString());
            }
            Verdict("InterruptBeforeRelease_Auto_NoProjectile", "interrupts", flew.Count == 0, "P1",
                flew.Count == 0 ? "a stun in the auto attack's wind-up cancels the shot" :
                $"stunned in the wind-up, the auto attack still lands for: {string.Join(", ", flew)} (InterruptSpecial skips ProjMode.Auto and Smash; the body shows a stun while the shot leaves). SUSPECTED rule question, not a confirmed bug");
        }

        // ---------------------------------------------------------------- input path
        static void PrecInput()
        {
            // a tap is cast in the very tick it reaches the rules (0 ticks of rules latency)
            {
                var m = PMake(HeroId.Bonk, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var b = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != b && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(b, 7.5f, 20); PPlace(e, 8.5f, 20); b.Charge = b.HDef.MaxCharge;
                m.Step(PTap());
                bool cast = false; foreach (var x in m.Events) if (x.Type == EventType.Cast && x.A == b) cast = true;
                Verdict("Input_TapCastsSameTick", "input", cast, "P1", cast ? "Cast event in the tick of the press" : "no Cast in the tick of the press");
            }
            // a drag casts exactly along its angle (Boulder Toss: Cast X/Y on the aimed line)
            {
                var m = PMake(HeroId.Bonk, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var b = m.Player; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != b) POff(m.Heroes[i]);
                double worst = 0;
                for (int k = 0; k < 16; k++)
                {
                    PPlace(b, 7.5f, 20); b.Charge = b.HDef.MaxCharge;
                    float ang = -SimMath.PI + k * SimMath.PI / 8 + 0.1f;
                    m.Step(PDrag(ang));
                    foreach (var x in m.Events) if (x.Type == EventType.Cast) { double a2 = Math.Atan2(x.Y - 20, x.X - 7.5); worst = Math.Max(worst, Math.Abs(SimMath.AngDiff((float)a2, ang))); }
                    m.PRun(40, PlayerInput.None, PlayerInput.None);
                }
                Verdict("Input_DragAngleExact", "input", worst < 1e-3, "P1", $"16 drag angles: Cast point off the aimed angle by at most {worst:E1} rad");
            }
            // moving: the stick moves the hero in the same tick, and letting go stops it at once
            {
                var m = PMake(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var t = m.Player; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != t) POff(m.Heroes[i]);
                PPlace(t, 7.5f, 20);
                var mv = PlayerInput.None; mv.MoveX = 1;
                float x0 = t.X; m.Step(mv); float d1 = t.X - x0; float x1 = t.X; m.Step(PlayerInput.None); float d2 = t.X - x1;
                Verdict("Input_MoveSameTick_StopsAtOnce", "input", Near(d1, t.BaseSpeed * Rules.Tick, 0.002f) && Math.Abs(d2) < 1e-5, "P2",
                    $"first tick moved {F(d1)} (speed x tick = {F(t.BaseSpeed * Rules.Tick)}), tick after letting go {F(d2)}");
            }
            // a tap with nobody in reach: one 'No target in reach' (SpecialDenied), charge kept
            {
                var m = PMake(HeroId.Bram, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Tyra, HeroId.Knut, HeroId.Tikta });
                var b = m.Player; for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != b) POff(m.Heroes[i]);
                PPlace(b, 7.5f, 20); b.Charge = b.HDef.MaxCharge; m.AddStatus(b, St.Disarm, 1e6f);
                var ev = m.PRun(60, PTap(), PlayerInput.None);
                int denied = 0; foreach (var x in ev) if (x.Type == EventType.SpecialDenied) denied++;
                Verdict("Input_SpecialDenied_ExactlyOnce", "input", denied == 1 && b.SpecialReady, "P2", $"{denied} SpecialDenied events, special still ready {b.SpecialReady}");
            }
            // a press while stunned (or knocked back, or casting) longer than the 0.25 s buffer: cast later, or told 'denied'?
            {
                var m = PMake(HeroId.Bonk, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
                var b = m.Player; var e = m.PFoe(HeroId.Bram);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != b && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(b, 7.5f, 20); PPlace(e, 8.5f, 20); b.Charge = b.HDef.MaxCharge; m.AddStatus(b, St.Disarm, 1e6f);
                m.AddStatus(b, St.Stun, 0.5f);
                var ev = m.PRun(90, PTap(), PlayerInput.None);
                int cast = 0, denied = 0; foreach (var x in ev) { if (x.Type == EventType.Cast) cast++; if (x.Type == EventType.SpecialDenied) denied++; }
                Verdict("Input_PressDuringStun_NotSilentlyLost", "input", cast + denied > 0, "P2",
                    $"special pressed in a 0.5 s stun: {cast} casts, {denied} denials after the stun (the 0.25 s buffer runs out while PlayerAct is skipped: the press vanishes with no feedback; the HUD only checked SpecialReady)");
            }
        }
    }
}
