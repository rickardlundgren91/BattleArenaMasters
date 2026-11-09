using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // The Sim fixes of 0.4.0 (protocol 59) from the full review of 2026-11-09 (bam-work/review/full-2026-11-09): one check per finding,
    // each failed at 4c31d877 and passes with its fix. Run from RunHero2Tests (Hero2Tests.cs).
    public sealed partial class Match
    {
        static int CountEv(Match m, EventType t, Func<SimEvent, bool> f = null) { int n = 0; foreach (var e in m.Events) if (e.Type == t && (f == null || f(e))) n++; return n; }

        /// <summary>A match where nobody thinks: every hero parked stunned top-left; the caller places its actors (Place clears the stun).</summary>
        static Match Quiet(HeroId player, HeroId[] allies, HeroId[] enemies)
        {
            var m = Make(player, allies, enemies);
            foreach (var h in m.Heroes) { h.Bot = null; Place(h, 1 + h.HeroIndex, 1); m.AddStatus(h, St.Stun, 999); }
            return m;
        }

        /// <summary>Is a hook of Knut's still on its way (a Done proj goes back to the pool, so the proj itself is not asked)?</summary>
        static bool HookLive(Match m, Unit knut) { foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Src == knut && !q.Done) return true; return false; }

        // 72 (SIM-1): Knut's killing blow put his slow on the corpse; statuses do not tick while dead, so the hero came back slowed
        static void Fix72KnutSlowOnCorpse()
        {
            var m = Quiet(HeroId.Knut, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Tyra, HeroId.Tikta });
            var k = m.Player; var b = m.Foe(HeroId.Bram); var t = m.Foe(HeroId.Tyra);
            Place(k, 7.5f, 20); Place(b, 8.5f, 20); Place(t, 8.5f, 21); b.Hp = 1; k.Charge = 0;
            var p = m.NewProj(ProjMode.Auto, k, k.X, k.Y); p.Tgt = b; p.Dmg = 50; p.OnHit = true; p.Kind = ProjKind.KnutHit; p.Done = true;
            m.HitWith(p);
            Check(!b.Alive && !b.Has(St.SlowFlat) && k.Charge == 1, $"FIX 72: the killing blow puts no slow on the corpse and still charges Knut (alive {b.Alive}, slowed {b.Has(St.SlowFlat)}, charge {k.Charge})");
            p = m.NewProj(ProjMode.Auto, k, k.X, k.Y); p.Tgt = t; p.Dmg = 5; p.OnHit = true; p.Kind = ProjKind.KnutHit; p.Done = true;
            m.HitWith(p);
            Check(t.Alive && t.Has(St.SlowFlat), "FIX 72: a hit that does not kill still slows (the live rule)");
            // whatever sits on a corpse is gone at the revive
            m.AddStatus(b, St.SlowFlat, 999, -1, 0, k);
            Check(b.Has(St.SlowFlat), "FIX 72: a slow put on the corpse for the check");
            b.RespawnT = 0.01f; m.Step(PlayerInput.None); m.Step(PlayerInput.None);
            Check(b.Alive && !b.Has(St.SlowFlat) && b.Has(St.Invulnerable), $"FIX 72: the revived hero carries nothing from his death, only the respawn protection (alive {b.Alive}, slowed {b.Has(St.SlowFlat)})");
        }

        // 76 (SIM-2, HERO-A-2) and 66 (HERO-B-3): a shot that landed after its shooter died, or a dot tick of his, revealed the dead hero
        // and put him in a fight; he came back lit. The live rules stay: an own hit reveals 2 s, a dot tick reveals its target in grass.
        static void Fix76DeadAttackerReveal()
        {
            // Tyra's arrow in the air when she dies
            var m = Quiet(HeroId.Tyra, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var t = m.Player; var b = m.Foe(HeroId.Bram);
            Place(t, 7.5f, 20); m.Still(b, 7.5f, 16.5f);
            t.AtkTimer = 99;
            bool flying = false; int guard = 0;
            while (!flying && guard++ < 180) { m.Step(PlayerInput.None); foreach (var q in m.Projs) if (q.Src == t && q.Mode == ProjMode.Auto && q.T >= 0 && !q.Done) flying = true; }
            m.Kill(t, b);
            bool hit = false; guard = 0;
            while (!hit && guard++ < 60) { m.Step(PlayerInput.None); hit = CountEv(m, EventType.Hit, e => e.A == t && e.B == b) > 0; }
            Check(flying && hit, "FIX 76: Tyra's arrow was in the air when she died and landed after");
            Check(!t.Alive && t.RevealT == 0 && t.LastFight < 0, $"FIX 76: the late hit neither reveals the dead Tyra nor puts her in a fight (RevealT {t.RevealT:0.00}, LastFight {t.LastFight:0.0})");
            Check(b.RevealT > 0 || m.Map.InGrass(b.X, b.Y), "FIX 76: the hit still reveals its target (outside the grass)");

            // Tikta's poison ticking after her death
            m = Quiet(HeroId.Tikta, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Bonk });
            var tk = m.Player; var a = m.Foe(HeroId.Bram);
            Place(tk, 7.5f, 20); m.Still(a, 7.5f, 10); // far apart: no auto attacks
            m.TiktaPoison(tk, a, 10, TiktaS.PoisonDuration);
            m.DealDamage(a, tk, 99999, DmgKind.Auto);
            Check(!tk.Alive && tk.RevealT == 0, "FIX 66: Tikta dead and unrevealed");
            float hp0 = a.Hp; a.RevealT = 0;
            for (int i = 0; i < 60; i++) m.Step(PlayerInput.None); // the poison ticks every half second
            Check(a.Hp < hp0 && tk.RevealT == 0 && a.RevealT > 0, $"FIX 66: her poison ticks on and reveals the poisoned hero, not the dead Tikta (RevealT {tk.RevealT:0.00}, his {a.RevealT:0.00})");
            for (int i = 0; i < 80; i++) m.Step(PlayerInput.None); // the poison runs out (a living Tikta's tick would reveal her again: the live rule)
            Check(!a.Has(St.Poison) && !tk.Alive, "FIX 66: the poison is over, Tikta still dead");
            // the revive starts unrevealed whatever lit the corpse
            tk.RevealT = 1; tk.RespawnT = 0.01f; m.Step(PlayerInput.None); m.Step(PlayerInput.None);
            Check(tk.Alive && tk.RevealT == 0, $"FIX 76: a revived hero starts unrevealed (RevealT {tk.RevealT:0.00})");
            // the live rule: a living hero's own hit reveals him 2 s
            m.DealDamage(tk, a, 1, DmgKind.Auto);
            Check(Near(tk.RevealT, Rules.RevealTime), "FIX 76: a living hero's own hit still reveals him 2 s");
        }

        // 77 (SIM-3): a tower shot re-aimed in flight (its minion died) took the nearest enemy, a hero before a living minion
        static void Fix77TowerRetarget()
        {
            var m = LockScene(out var p, out var tw);
            float dir = tw.Y < m.Map.H / 2f ? 1 : -1;
            m.Still(p, tw.X, tw.Y + dir * (tw.Range + p.R + 3f)); // out of reach: the tower lets go of him
            for (int k = 0; k < 60; k++) m.Step(PlayerInput.None);
            var m1 = m.NewMinion(MinionKind.Melee, p.Team, tw.X, tw.Y + dir * (tw.R + 1.0f)); m.Still(m1, m1.X, m1.Y);
            var m2 = m.NewMinion(MinionKind.Melee, p.Team, tw.X + 1.0f, tw.Y + dir * (tw.Range - 0.5f)); m.Still(m2, m2.X, m2.Y);
            for (int k = 0; k < 30; k++) m.Step(PlayerInput.None);
            m.Still(p, tw.X - 0.5f, tw.Y + dir * (tw.R + 1.6f)); // the hero back in, nearer to the tower than minion 2
            for (int k = 0; k < 30; k++) m.Step(PlayerInput.None);
            Check(tw.Target == m1, "FIX 77: the tower is on the nearest minion with the hero in reach too (TOWER RULE)");
            bool shot = false; int g = 0;
            while (!shot && g++ < 120) { m.Step(PlayerInput.None); shot = CountEv(m, EventType.AttackStart, e => e.A == tw && e.B == m1) > 0; }
            m1.Hp = 1; m.Kill(m1, null); // its target dies while the shot flies
            Unit hitBy = null; g = 0;
            while (hitBy == null && g++ < 60) { m.Step(PlayerInput.None); foreach (var e in m.Events) if (e.Type == EventType.Hit && e.A == tw) hitBy = e.B; }
            Check(shot && hitBy == m2, $"FIX 77: the re-aimed tower shot takes the living minion, not the hero (hit {(hitBy == p ? "the hero" : hitBy == m2 ? "minion 2" : "nobody")})");
        }

        // 78 (SIM-4, HERO-A-4): a hero in a jump pad's flight (Grove) swung and could Blink in the air
        static void Fix78PadFlight()
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Allies = new[] { HeroId.Moa, HeroId.Bram }, Enemies = new[] { HeroId.Knut, HeroId.Fenn, HeroId.Tikta }, Seed = 5, Map = MapId.Grove });
            foreach (var u in m.Units) { if (u.Kind == UnitKind.Minion) u.Alive = false; u.ClearStatuses(); }
            m.State = MatchState.Play; m.Time = 10; m.WaveT = 1e9f; m.Events.Clear(); m.Telegraph = false;
            var p = m.Player; var foes = m.Heroes.FindAll(h => h.Team != p.Team);
            foreach (var h in m.Heroes) if (h != p) { Place(h, 1, 1); m.AddStatus(h, St.Stun, 999); }
            int pad = -1; for (int i = 0; i < m.Map.PadCount; i++) if (m.Map.PadY[i] > m.Map.H / 2) pad = i;
            Check(pad >= 0, "FIX 78: Grove has a pad on the bottom half");
            if (pad < 0) return;
            // one foe beside the pad (in her reach as she is thrown), one beside the landing spot (in reach as she lands)
            bool Beside(Unit e, float x, float y)
            {
                foreach (float dx in new[] { 1.5f, -1.5f, 2f, -2f }) foreach (float dy in new[] { 0f, 1f, -1f })
                    if (m.Map.Free(x + dx, y + dy, e.R) && !m.Map.InGrass(x + dx, y + dy)) { m.Still(e, x + dx, y + dy); return true; }
                return false;
            }
            Check(Beside(foes[0], m.Map.PadX[pad], m.Map.PadY[pad]) && Beside(foes[1], m.Map.PadToX[pad], m.Map.PadToY[pad]), "FIX 78: free spots beside the pad and the landing spot");
            Place(p, m.Map.PadX[pad], m.Map.PadY[pad]); p.Spell = SpellId.Blink; p.SpellAt = 0; p.AtkTimer = 99;
            m.Step(PlayerInput.None); // the pad throws her this tick, before her swing is looked at
            Check(p.KnockPad && p.KnockT > 0, "FIX 78: the pad throws her");
            var inp = PlayerInput.None; inp.Spell = true; inp.SpellAngle = 0f; inp.SpellK = 1f;
            int swings = CountEv(m, EventType.AttackStart, ev => ev.A == p), blinks = 0, landing = 0, guard = 0;
            while (p.KnockT > 0 && guard++ < 120)
            {
                m.Step(guard == 1 ? inp : PlayerInput.None);
                int sw = CountEv(m, EventType.AttackStart, ev => ev.A == p);
                if (p.KnockT > 0) swings += sw; else landing += sw; // the landing tick: she stands when the swing is looked at
                blinks += CountEv(m, EventType.SpellCast, ev => ev.A == p);
            }
            Check(swings == 0 && blinks == 0 && !p.KnockPad, $"FIX 78: no swing and no Blink in the air (swings {swings}, blinks {blinks}, {guard} ticks of flight)");
            for (int k = 0; k < 3; k++) { m.Step(PlayerInput.None); landing += CountEv(m, EventType.AttackStart, ev => ev.A == p); }
            Check(landing == 1, $"FIX 78: the swing held in the air comes as she lands ({landing})");
            Check(m.CastSpell(p, 0f, 1f), "FIX 78: the Blink works again on the ground");
        }

        // 63 (HERO-B-1): Kugg's stock was spent as the shell's wind-up began, so a stun that cut the shell cost a shot
        static void Fix63KuggStockOnStun()
        {
            var m = Quiet(HeroId.Kugg, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Tikta });
            var h = m.Player; var a = m.Foe(HeroId.Bram);
            Place(h, 7.5f, 20); m.Still(a, 13.5f, 20);
            h.Charge = 2;
            Check(m.TryTapSpecial(h) && h.KuggShots == 2, "FIX 63: anchored with 2 shots");
            int starts = 0, hits = 0, packs = 0, leftAfterStun = -1; bool stunned = false;
            for (int i = 0; i < 900 && (h.KuggRooted || i < 60); i++)
            {
                m.Step(PlayerInput.None);
                starts += CountEv(m, EventType.AttackStart, e => e.A == h && (ProjKind)e.Kind == ProjKind.KuggShell);
                hits += CountEv(m, EventType.Hit, e => e.A == h && (ProjKind)e.Kind == ProjKind.KuggShell);
                packs += CountEv(m, EventType.KuggPack, e => e.A == h);
                if (!stunned && starts == 2) { stunned = true; m.AddStatus(h, St.Stun, 0.5f, 0, 0, a); leftAfterStun = h.KuggShots; } // the 2nd shell is in his hand: the stun cuts it
            }
            Check(stunned && leftAfterStun == 1, $"FIX 63: the shell cut in his hand costs no stock ({leftAfterStun} left after the stun)");
            Check(starts == 3 && hits == 2 && packs == 1 && !h.KuggAnchored, $"FIX 63: he fires that stock again after the stun: 3 wind-ups, 2 shells landed, one pack-up ({starts}, {hits}, {packs})");
        }

        // 64 (HERO-B-5): a Blink while Tikta's dragged needles still left her hand moved their start off the red line
        static void Fix64NoBlinkInNeedleRelease()
        {
            var m = Quiet(HeroId.Tikta, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Bonk });
            var t = m.Player;
            Place(t, 7.5f, 20); t.Charge = t.HDef.MaxCharge; t.Spell = SpellId.Blink; t.SpellAt = 0;
            Check(m.TryCastSpecial(t, SimMath.PI / 2, 1), "FIX 64: the needle line is cast");
            float x0 = t.X, y0 = t.Y; bool blinked = false; int ticks = 0;
            while (m.NeedleInHand(t) && ticks++ < 60) { if (m.CastSpell(t, 0f, 1f)) blinked = true; m.Step(PlayerInput.None); }
            Check(!blinked && t.X == x0 && t.Y == y0, $"FIX 64: no Blink while her needles still leave her hand ({ticks} ticks)");
            Check(ticks >= 36 && m.CastSpell(t, 0f, 1f), $"FIX 64: the Blink works once the last needle has left ({ticks} ticks)");
        }

        // 67 (HERO-B-2): the burn's float sums gave a ninth tick of nothing after the eighth
        static void Fix67BurnTicks()
        {
            var m = Quiet(HeroId.Blast, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, HeroId.Knut, HeroId.Bonk });
            var h = m.Player; var a = m.Foe(HeroId.Bram);
            Place(h, 7.5f, 20); m.Still(a, 7.5f, 12); // far apart: no auto attacks
            m.DealDamage(h, a, 100, DmgKind.Auto);
            Check(a.Has(St.Burn), "FIX 67: Blast's hit lights the burn");
            int ticks = 0; float sum = 0, lo = 99, hi = 0;
            for (int i = 0; i < 150; i++)
            {
                m.Step(PlayerInput.None);
                foreach (var e in m.Events) if (e.Type == EventType.Damage && e.B == a && e.Dmg == DmgKind.Passive) { ticks++; sum += e.V; lo = Math.Min(lo, e.V); hi = Math.Max(hi, e.V); }
            }
            Check(ticks == 8 && Near(sum, 15, 0.001f) && Near(lo, 1.875f, 0.005f) && Near(hi, 1.875f, 0.005f) && !a.Has(St.Burn),
                $"FIX 67: a 2 s burn of a 100 hit is exactly 8 ticks of 1.875 ({ticks} ticks, {sum:0.000} in all, {lo:0.000}-{hi:0.000}, burn left {a.Has(St.Burn)})");
        }

        // 100 (HERO-A-1): with the warning on the thrown wolf homes on nobody, but the drag preview still marked a hero beside its flight
        static void Fix100FennPreview()
        {
            foreach (bool tele in new[] { true, false })
            {
                var m = OneTapScene(HeroId.Fenn, out var p, out float cx, out float cy);
                m.Telegraph = tele;
                var e = m.Heroes.Find(h => h.Team != p.Team);
                Place(e, p.X + 1.5f, p.Y + 0.7f); m.AddStatus(e, St.Stun, 999); // inside the homing strip, outside the landing circle
                var a = m.AimFor(p, 0f, DragShare(HeroId.Fenn, 1f));
                var buf = new List<Unit>(); m.AimTargets(p, a, buf);
                bool marked = buf.Contains(e), bitten = false;
                for (int t = 0; t < 40; t++) { m.Step(t == 0 ? DragIn(0f, 1f) : PlayerInput.None); if (CountEv(m, EventType.Hit, ev => ev.B == e && ev.Kind == (byte)ProjKind.WolfBite) > 0) bitten = true; }
                Check(marked == bitten && marked != tele, $"FIX 100: telegraph {tele}: the preview marks the hero beside the flight {marked}, the thrown wolf bites him {bitten}");
                if (tele) Check(a.PathHalf == 0, "FIX 100: no homing strip in the preview of a warned throw");
            }
        }

        // 101 (HERO-A-3): Knut died mid-haul and the freed catch kept the stun sized for the whole pull
        static void Fix101KnutDiesMidHaul()
        {
            var m = OneTapScene(HeroId.Knut, out var p, out float cx, out float cy);
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 5f, p.Y); e.Bot = null;
            m.Step(DragIn(0f, 1f));
            Proj hook = null; foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Src == p) hook = q;
            int guard = 0; while (hook != null && hook.Caught != e && !hook.Done && guard++ < 120) m.Step(PlayerInput.None);
            bool caught = hook != null && hook.Caught == e;
            int si = e.FindBy(St.Stun, p); float stunAtCatch = si >= 0 ? e.Sts[si].T : 0;
            m.Step(PlayerInput.None); // one tick of reel
            m.Kill(p, e);
            m.Step(PlayerInput.None);
            si = e.FindBy(St.Stun, p); float stunLeft = si >= 0 ? e.Sts[si].T : 0;
            Check(caught && stunAtCatch > KnutS.StunAfter + 0.2f && !HookLive(m, p) && stunLeft > 0 && stunLeft <= KnutS.StunAfter + 1e-3f,
                $"FIX 101: Knut dies mid-haul: the chain ends and the catch's stun shrinks from {stunAtCatch:0.00} to {stunLeft:0.00} s (at most {KnutS.StunAfter})");
        }

        // 102 (HERO-A-6): the tap's red line lasted its fixed time after the chain had caught or turned back
        static void Fix102KnutWarnEnds()
        {
            var m = OneTapScene(HeroId.Knut, out var p, out float cx, out float cy);
            m.Telegraph = true;
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 2f, p.Y); e.Bot = null;
            m.Step(TapIn());
            int catchTick = -1, warnGone = -1;
            for (int t = 1; t < 120; t++)
            {
                m.Step(PlayerInput.None);
                bool warn = false; foreach (var q in m.Projs) if (q.IsWarning && q.Src == p && !q.Done) warn = true;
                if (catchTick < 0) foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Caught == e) catchTick = t;
                if (catchTick >= 0 && !warn) { warnGone = t; break; }
            }
            Check(catchTick > 0 && warnGone > 0 && warnGone - catchTick <= 1, $"FIX 102: the tap's red line goes with the catch (catch at tick {catchTick}, line gone at {warnGone})");
            // a stun that turns the empty anchor back
            m = OneTapScene(HeroId.Knut, out p, out cx, out cy);
            m.Telegraph = true;
            e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 2f, p.Y); e.Bot = null;
            m.Step(TapIn());
            for (int t = 0; t < 18; t++) m.Step(PlayerInput.None); // the cast is at tick 18: the anchor leaves
            Place(e, p.X + 2f, p.Y + 3f); // stepped aside: nothing to catch
            m.Step(PlayerInput.None); m.Step(PlayerInput.None);
            bool outBefore = false; foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Src == p && q.Out && !q.Done) outBefore = true;
            m.AddStatus(p, St.Stun, 0.3f);
            int left = 0; for (int t = 0; t < 120; t++) { bool warn = false; foreach (var q in m.Projs) if (q.IsWarning && q.Src == p && !q.Done) warn = true; if (!warn) break; left++; m.Step(PlayerInput.None); }
            Check(outBefore && left <= 1, $"FIX 102: a stun that turns the empty anchor back takes the red line with it (anchor was out {outBefore}, line stayed {left} ticks)");
        }

        // 103 (HERO-A-7): the hook caught Fenn's decoy, which dies at the hit, and Knut stood Casting the whole pull of an empty chain
        static void Fix103DecoyHaul()
        {
            var m = Quiet(HeroId.Knut, new[] { HeroId.Tyra, HeroId.Moa }, new[] { HeroId.Fenn, HeroId.Bram, HeroId.Tikta });
            var k = m.Player; var f = m.Foe(HeroId.Fenn);
            Place(k, 7.5f, 20); Place(f, 12.5f, 20);
            f.ClonePending = true; m.FlushClones();
            var c = m.Units.Find(u => u.IsClone && u.Alive && u.Owner == f);
            Check(c != null, "FIX 103: Fenn's decoy stands");
            if (c == null) return;
            m.Still(f, 1, 3); // Fenn himself out of the way, alive (the decoy goes if he dies)
            Place(c, 12.5f, 20); m.AddStatus(c, St.Stun, 999);
            k.Charge = k.HDef.MaxCharge;
            Check(m.TryCastSpecial(k, 0f, 1f), "FIX 103: Knut throws the hook");
            int caughtAt = -1, stood = 0;
            for (int t = 0; t < 120; t++)
            {
                m.Step(PlayerInput.None);
                if (caughtAt < 0) foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Caught == c) caughtAt = t;
                if (caughtAt >= 0 && k.Has(St.Casting)) stood++;
                if (caughtAt >= 0 && !k.Has(St.Casting)) break;
            }
            Check(caughtAt >= 0 && !c.Alive && !HookLive(m, k) && stood <= 2, $"FIX 103: the decoy dies at the hit, the chain and Knut's stand end at once (caught at tick {caughtAt}, decoy alive {c.Alive}, stood {stood} ticks after)");
        }

        // 106 (HERO-A-11): a second press buffered while the special waited in its warning (Tikta walks in hers) was a SpecialDenied
        static void Fix106NoDenialInWarning()
        {
            var m = OneTapScene(HeroId.Tikta, out var p, out float cx, out float cy);
            m.Telegraph = true;
            var e = m.Heroes.Find(h => h.Team != p.Team); Place(e, p.X + 2f, p.Y); m.AddStatus(e, St.Stun, 999);
            int denied = 0, casts = 0;
            for (int t = 0; t < 40; t++)
            {
                m.Step(t == 0 || t == 2 ? TapIn() : PlayerInput.None);
                denied += CountEv(m, EventType.SpecialDenied, ev => ev.A == p);
                casts += CountEv(m, EventType.Cast, ev => ev.A == p);
            }
            Check(denied == 0 && casts == 1, $"FIX 106: a second tap in Tikta's warning is no denial, her special comes once (denied {denied}, casts {casts})");
        }

        // 107 (HERO-A-12): the tiles outside the arena are no wall, so a straight shot and its line went on past the edge; the hook flew 7 tiles out
        static void Fix107ShotsStopAtEdge()
        {
            var m = OneTapScene(HeroId.Knut, out var p, out float cx, out float cy);
            Place(p, m.Map.W - 0.6f, cy);
            float reach = m.Map.ShotReach(p.X, p.Y, 0f, KnutS.HookDistance), line = m.AimFor(p, 0f, 1f).Length;
            Check(Near(reach, 0.6f, 0.02f) && Near(line, 0.6f, 0.02f), $"FIX 107: a straight shot's reach and its aim line end at the arena's edge ({reach:0.00}, {line:0.00})");
            m.Step(DragIn(0f, 1f));
            float maxX = p.X; bool blocked = false; int ticks = 0;
            for (int t = 0; t < 120 && HookLive(m, p); t++)
            {
                m.Step(PlayerInput.None); ticks++;
                foreach (var q in m.Projs) if (q.Mode == ProjMode.Hook && q.Src == p && !q.Done) maxX = Math.Max(maxX, q.X);
                if (CountEv(m, EventType.Blocked, ev => ev.A == p) > 0) blocked = true;
            }
            Check(maxX <= m.Map.W + 1e-3f && blocked && ticks < 60, $"FIX 107: the anchor turns back at the arena's edge (reached x {maxX:0.00} of {m.Map.W}, puff {blocked}, {ticks} ticks)");
        }

        static void Fix0400()
        {
            Fix72KnutSlowOnCorpse(); Fix76DeadAttackerReveal(); Fix77TowerRetarget(); Fix78PadFlight(); Fix63KuggStockOnStun(); Fix64NoBlinkInNeedleRelease();
            Fix67BurnTicks(); Fix100FennPreview(); Fix101KnutDiesMidHaul(); Fix102KnutWarnEnds(); Fix103DecoyHaul(); Fix106NoDenialInWarning(); Fix107ShotsStopAtEdge();
            Check(SimMirror.Available, $"FIX 0.4.0: SimCopy.g.cs still mirrors the Sim classes (no field changed; hash {SimMirror.Hash()})");
        }
    }
}
