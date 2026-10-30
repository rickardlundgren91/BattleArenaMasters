using System;
using System.Collections.Generic;
using BAM.Art;

namespace BAM.Sim
{
    // Animation Foundation 1.0 tests (Rickard's brief, step 7). A partial Match, so the tests reach the rules directly, as the
    // precision suite does. PASS / FAIL / INFO per line; INFO is a measurement, not a verdict.
    public sealed partial class Match
    {
        static int pass, fail, info;
        static void R(string status, string id, string detail)
        {
            if (status == "PASS") pass++; else if (status == "FAIL") fail++; else info++;
            Console.WriteLine($"{status,-5} {id,-44} {detail}");
        }
        static void V(bool ok, string id, string detail) => R(ok ? "PASS" : "FAIL", id, detail);
        static string F(double v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

        static readonly HeroId[] Playable = { HeroId.Bram, HeroId.Fenn, HeroId.Tyra, HeroId.Moa, HeroId.Knut, HeroId.Tikta, HeroId.Blast, HeroId.Bonk, HeroId.Hilde, HeroId.Oaken };

        // ---------------------------------------------------------------- 10: the plans at every windup
        static void PlanTests()
        {
            var rnd = new Random(1);
            int n = 0, badBlow = 0, badMono = 0, badTotal = 0, badFinite = 0;
            double worst = 0;
            float[] windups = { 0.05f, 0.075f, 0.1f, 0.15f, 0.195f, 0.2f, 0.3f, 0.45f, 0.7f, 1.0f, 1.2f };
            for (int c = 0; c < 4000; c++)
            {
                float len = 0.4f + (float)rnd.NextDouble() * 7.5f; // Mixamo swings 0.4 s .. Blast's 7.7 s throw
                float start = (float)rnd.NextDouble() * 0.3f, rel = 0.2f + (float)rnd.NextDouble() * 0.7f;
                float blow = rnd.NextDouble() < 0.2 ? -1f : (float)rnd.NextDouble() * len;
                float settle = rnd.NextDouble() < 0.3 ? -1f : (float)rnd.NextDouble() * len;
                bool bow = rnd.NextDouble() < 0.15; float draw = bow ? (float)rnd.NextDouble() * len : -1f;
                float gap = 0.3f + (float)rnd.NextDouble() * 3f;
                foreach (float w in windups)
                {
                    var p = AnimTiming.PlanAttack(w, len, start, rel, blow, settle, draw, bow, gap);
                    if (!p.Valid) continue;
                    n++;
                    double at = AnimTiming.AttackClipTime(p, w, w);
                    double err = Math.Abs(at - p.Blow);
                    worst = Math.Max(worst, err);
                    if (err > 1e-5) badBlow++;
                    if (!(p.Total >= w + 0.35f - 1e-5f) || !(p.Total < 10f)) badTotal++;
                    if (float.IsNaN(p.Speed) || float.IsNaN(p.RecRate) || float.IsInfinity(p.Total) || float.IsNaN(p.Fade)) badFinite++;
                    // never goes back, passes the blow once
                    float prev = -1f; int crossings = 0;
                    for (float t = 0f; t <= p.Total; t += 1f / 240f)
                    {
                        float ct = AnimTiming.AttackClipTime(p, w, t);
                        if (ct < prev - 1e-5f) { badMono++; break; }
                        if (prev < p.Blow && ct >= p.Blow) crossings++;
                        prev = ct;
                    }
                    if (crossings > 1) badMono++;
                }
            }
            V(badBlow == 0, "T10_BlowOnRelease_AnyWindup", $"{n} plans (windup 0.05-1.2 s, random clips): clip time at t = windup is the blow, worst {worst:E1} s off");
            V(badMono == 0, "T11_NoSecondBlow_InPicture", $"clip time never goes back and passes the blow once in {n} plans (bad {badMono})");
            V(badTotal == 0 && badFinite == 0, "T14_PlanAlwaysEnds", $"total finite and >= windup + 0.35 s in every plan (bad {badTotal}, non-finite {badFinite})");

            // the old BlowSegment, verbatim with Mathf replaced, against the new plan: the refactor changes nothing
            int diff = 0, m = 0;
            for (int c = 0; c < 20000; c++)
            {
                float len = 0.4f + (float)rnd.NextDouble() * 7.5f, start = (float)rnd.NextDouble() * 0.3f, rel = 0.2f + (float)rnd.NextDouble() * 0.7f;
                float blowM = rnd.NextDouble() < 0.2 ? -1f : (float)rnd.NextDouble() * len, settle = rnd.NextDouble() < 0.3 ? -1f : (float)rnd.NextDouble() * len;
                bool bow = rnd.NextDouble() < 0.15; float draw = bow ? (float)rnd.NextDouble() * len : -1f;
                float gap = 0.3f + (float)rnd.NextDouble() * 3f, w = windups[rnd.Next(windups.Length)];
                var p = AnimTiming.PlanAttack(w, len, start, rel, blowM, settle, draw, bow, gap);
                bool ok = OldBlowSegment(w, len, start, rel, blowM, settle, draw, bow, gap, out float[] o);
                m++;
                if (ok != p.Valid || ok && (o[0] != p.From || o[1] != p.Blow || o[2] != p.To || o[3] != p.Speed || o[4] != p.RecRate || o[5] != p.Total || o[6] != p.Fade)) diff++;
            }
            V(diff == 0, "REF_AttackPlan_SameAsBefore", $"{m} random swings: AnimTiming.PlanAttack equals the old BlowSegment bit for bit ({diff} differ)");

            // casts: the peak lands on the special's own wind-up (Tap and Drag leads from BodyView.CastLead)
            float[] leads = { 0.12f, TyraS.Windup, TyraS.VolleyWindup, KnutS.SlamWindup, BonkS.BoulderWindup, BonkS.SlamWindup, OakenS.QuakeWindup, OakenS.SlamWindup };
            int cn = 0, cbad = 0, cdiff = 0; double cworst = 0;
            for (int c = 0; c < 4000; c++)
            {
                float len = 0.5f + (float)rnd.NextDouble() * 4f, blowM = rnd.NextDouble() < 0.2 ? -1f : 0.05f + (float)rnd.NextDouble() * (len - 0.05f);
                float settle = rnd.NextDouble() < 0.3 ? -1f : (float)rnd.NextDouble() * len;
                foreach (float lead in leads)
                {
                    var cp = AnimTiming.PlanCast(len, blowM, settle, -1f, false, lead);
                    bool ok = OldCastSegment(len, blowM, settle, -1f, false, lead, out float[] o);
                    if (ok != cp.Valid || ok && (o[0] != cp.From || o[1] != cp.To || o[2] != cp.Peak || o[3] != cp.Pre || o[4] != cp.BlowAt || o[5] != cp.Len || o[6] != cp.Fade)) cdiff++;
                    if (!cp.Valid) continue;
                    cn++;
                    double e = Math.Abs(cp.BlowAt - lead);
                    // a peak earlier in the clip than 1.15 x lead starts the clip at 0 and plays it slower: still on time
                    cworst = Math.Max(cworst, e);
                    if (e > 1e-4) cbad++;
                    if (Math.Abs(AnimTiming.CastClipTime(cp, cp.BlowAt) - cp.Peak) > 1e-4) cbad++;
                }
            }
            V(cbad == 0, "T10_CastPeakOnSpecial_TapAndDrag", $"{cn} casts over {leads.Length} tap/drag wind-ups: the peak shows on the rules' wind-up, worst {cworst * 1000:0.00} ms");
            V(cdiff == 0, "REF_CastPlan_SameAsBefore", $"AnimTiming.PlanCast equals the old CastSegment bit for bit ({cdiff} differ)");

            // the real heroes' auto windups
            var sb = new System.Text.StringBuilder();
            foreach (var h in Playable) sb.Append(h).Append(' ').Append(Defs.Heroes[(int)h].SpawnDelay).Append("s=").Append(AnimTiming.ReleaseTick(Defs.Heroes[(int)h].SpawnDelay)).Append("t ");
            R("INFO", "T10_HeroWindups", sb.ToString().Trim());
        }

        static float Clamp(float v, float a, float b) { if (v < a) v = a; else if (v > b) v = b; return v; }

        // BodyView.BlowSegment as it was on main be47ed0 (Mathf.Min/Max/Clamp -> the same order of operations)
        static bool OldBlowSegment(float windup, float len, float startF, float relF, float ciBlow, float ciSettle, float ciDraw, bool Bow, float atkGap, out float[] o)
        {
            o = null;
            float start0 = startF * len;
            float blow = ciBlow > 0f ? ciBlow : relF * len;
            if (Bow && ciDraw > 0f) blow = ciDraw;
            blow = Clamp(blow, MathF.Min(start0 + 0.05f, len), len);
            float pre = MathF.Min(blow - start0, Clamp(windup * 1.8f, 0.25f, 0.45f));
            if (pre < 0.02f) return false;
            float atkFrom = blow - pre, atkBlow = blow;
            float to = MathF.Min(len, MathF.Min(ciSettle > blow ? ciSettle : blow + 0.5f, blow + 1.6f));
            float budget = MathF.Max(0.3f, atkGap - windup - 0.1f);
            float recRate = Clamp((to - blow) / budget, 1.2f, 2.5f);
            float atkTo = MathF.Min(to, blow + budget * recRate);
            float atkSpeed = pre / windup;
            float play = (atkTo - blow) / recRate, rec = MathF.Max(0.35f, play);
            float atkTotal = windup + rec;
            float atkFade = Clamp(MathF.Max(rec * 0.4f, rec - play), 0.12f, 0.35f);
            if (Bow) atkFade = MathF.Max(0.12f, rec - play - 0.06f);
            o = new[] { atkFrom, atkBlow, atkTo, atkSpeed, recRate, atkTotal, atkFade };
            return true;
        }

        static bool OldCastSegment(float len, float ciBlow, float ciSettle, float ciDraw, bool Bow, float lead, out float[] o)
        {
            o = null;
            const float CastRate = 1.15f;
            float blow = ciBlow > 0f ? ciBlow : 0.4f * len;
            if (Bow && ciDraw > 0f) blow = ciDraw;
            float castFrom = MathF.Max(0f, blow - lead * CastRate);
            float castTo = MathF.Min(len, ciSettle > blow ? MathF.Min(ciSettle, blow + 1.2f) : blow + 0.6f);
            if (castTo - castFrom < 0.1f) return false;
            float castPeak = blow;
            float castPre = lead > 0.01f && blow > castFrom ? (blow - castFrom) / lead : CastRate;
            float castBlowAt = (blow - castFrom) / castPre;
            float play = castBlowAt + MathF.Max(0f, castTo - blow) / CastRate;
            float castLen = MathF.Max(play, castBlowAt + 0.35f);
            float castFade = Clamp(MathF.Max((castLen - castBlowAt) * 0.4f, castLen - play), 0.12f, 0.35f);
            o = new[] { castFrom, castTo, castPeak, castPre, castBlowAt, castLen, castFade };
            return true;
        }

        // ---------------------------------------------------------------- 1, 2: locomotion blend
        static void LocomotionTests()
        {
            // moveW eases at 6/s (BodyView): idle -> run and back take 1/6 s at any frame rate, give or take one frame
            double[] fps = { 30, 60, 90, 120, 144 };
            double worst = 0;
            foreach (double f in fps)
            {
                foreach (int dir in new[] { 1, 0 })
                {
                    float w = dir == 1 ? 0f : 1f, goal = dir; double t = 0; float dt = (float)(1.0 / f);
                    while (w != goal && t < 2) { w = AnimTiming.Approach(w, goal, 6f, dt); t += dt; if (w < 0f || w > 1f) worst = 99; }
                    worst = Math.Max(worst, Math.Abs(t - 1.0 / 6.0) - dt);
                }
            }
            V(worst <= 1e-6, "T01_T02_IdleRun_RunIdle_Blend", $"0->1 and 1->0 in 1/6 s within one frame at 30-144 fps, never past 0..1 (worst extra {worst * 1000:0.00} ms)");
        }

        // ---------------------------------------------------------------- sim side: bot matches
        sealed class Swing { public int Unit; public long Start; public float W; public int Launches; public long Launch = -1; public bool Released; }

        static void Note(Dictionary<string, int> d, string k) { d.TryGetValue(k, out int n); d[k] = n + 1; }
        static string Top(Dictionary<string, int> d) { var l = new List<string>(); foreach (var kv in d) l.Add(kv.Key + " x" + kv.Value); l.Sort(); return string.Join("; ", l.GetRange(0, Math.Min(8, l.Count))); }

        static void SimTests()
        {
            int matches = 0, swings = 0, launches = 0, doubles = 0, relBad = 0, relN = 0, earlySwing = 0, moving = 0, heroSwings = 0;
            int deaths = 0, respawns = 0, attackWhileDead = 0, hitAfterDeath = 0, zeroW = 0, sameTickDeath = 0;
            var earlyWhy = new Dictionary<string, int>();
            var relDetail = new List<string>();
            foreach (var hero in Playable)
            {
                for (int seed = 1; seed <= 3; seed++)
                {
                    var m = Create(new MatchConfig { PlayerHero = hero, Seed = seed * 31 + (int)hero, PlayerIsBot = true, Difficulty = Difficulty.Hard, AllyDifficulty = Difficulty.Hard });
                    matches++;
                    var byProj = new Dictionary<int, Swing>();
                    var last = new Dictionary<int, Swing>();
                    var deadNow = new HashSet<int>();
                    var seen = new HashSet<int>();
                    for (int k = 0; k < 60 * 240 && m.State != MatchState.Over; k++)
                    {
                        m.Step(PlayerInput.None);
                        long tick = m.Ticks;
                        foreach (var e in m.Events)
                        {
                            if (e.Type == EventType.Death && e.B != null && e.B.Kind == UnitKind.Hero) { deaths++; deadNow.Add(e.B.Serial); }
                            if (e.Type == EventType.Respawn && e.A != null) { respawns++; deadNow.Remove(e.A.Serial); }
                            if (e.Type != EventType.AttackStart || e.A == null) continue;
                            var a = e.A;
                            if (e.V <= 0f) { zeroW++; continue; } // Tikta's special needles and the wolf's bite: a throw with no windup
                            swings++;
                            // dead since an earlier tick: the view would swing a body lying on the ground
                            if (deadNow.Contains(a.Serial)) attackWhileDead++;
                            // swung, then died later in the same tick (a minion, Fenn's clone running out): the view starts the swing
                            // and SetDead follows in the same frame, so the death wins
                            else if (!a.Alive) sameTickDeath++;
                            if (a.Kind == UnitKind.Hero) { heroSwings++; if (m.Time - a.MovedAt < 0.1f) moving++; }
                            if (last.TryGetValue(a.Serial, out var prev) && (tick - prev.Start) * Rules.Tick < prev.W - 1e-4f) { earlySwing++; Note(earlyWhy, $"{a.Kind}/{(a.Kind == UnitKind.Hero ? a.Hero.ToString() : a.Kind == UnitKind.Minion ? a.Minion.ToString() : "")} gap {(tick - prev.Start)}t w {prev.W} stun={a.Has(St.Stun)}"); }
                            var s = new Swing { Unit = a.Serial, Start = tick, W = e.V };
                            last[a.Serial] = s;
                            // the shot this swing made: the newest Auto shot of A's still in the hand
                            for (int i = m.Projs.Count - 1; i >= 0; i--)
                            {
                                var p = m.Projs[i];
                                if (p.Src == a && !p.Done && (p.Mode == ProjMode.Auto) && !p.Launched && !byProj.ContainsKey(p.Serial)) { byProj[p.Serial] = s; break; }
                            }
                        }
                        foreach (var e in m.Events)
                            if (e.Type == EventType.Hit && e.A != null && e.A.Kind == UnitKind.Hero && deadNow.Contains(e.A.Serial) && (ProjKind)e.Kind != ProjKind.BlastFire) hitAfterDeath++;
                        for (int i = 0; i < m.Projs.Count; i++)
                        {
                            var p = m.Projs[i];
                            if (p.Mode != ProjMode.Auto || !p.Launched || p.Done || seen.Contains(p.Serial)) continue;
                            seen.Add(p.Serial);
                            if (!byProj.TryGetValue(p.Serial, out var s)) continue;
                            launches++;
                            s.Launches++;
                            if (s.Launches > 1) doubles++;
                            relN++;
                            int want = AnimTiming.ReleaseTick(s.W);
                            long got = tick - s.Start;
                            if (got != want) { relBad++; if (relDetail.Count < 4) relDetail.Add($"w {s.W} want {want} got {got}"); }
                        }
                    }
                }
            }
            V(relBad == 0, "T10_ReleaseTick_MatchesRules", $"{relN} auto shots in {matches} Hard bot matches: the shot leaves on AnimTiming.ReleaseTick(windup) every time{(relBad > 0 ? " BAD " + relBad + ": " + string.Join("; ", relDetail) : "")}");
            V(doubles == 0, "T11_NoDoubleProjectile", $"{swings} swings, {launches} shots tracked: no swing let two shots go ({doubles})");
            V(earlySwing == 0, "T05_NoSwingBeforeLastRelease", $"no unit started a swing before its last one's release ({earlySwing} of {swings})");
            V(attackWhileDead == 0, "T08_T09_NoSwingWhileDead", $"{deaths} hero deaths, {respawns} respawns: no swing from a dead unit ({attackWhileDead})");
            R("INFO", "T08_SwingThenDeathSameTick", $"{sameTickDeath} swings by a minion or clone that died later in the same tick (the death follows in the same frame)");
            R("INFO", "T10_ZeroWindupThrows", $"{zeroW} AttackStart events with windup 0 (Tikta's special needles, the wolf's bite): the view clamps them to 0.05 s, so their throw shows about 50 ms after the needle leaves");
            if (earlyWhy.Count > 0) R("INFO", "T05_EarlySwings", Top(earlyWhy));
            R("INFO", "T08_HitsAfterShooterDied", $"{hitAfterDeath} hero shots that landed after their shooter died (a shot in flight keeps going: the view must not replay a swing for it)");
            R("INFO", "T03_T04_SwingOnTheMove", $"{moving} of {heroSwings} hero swings start while the hero moved in the last 0.1 s: the view's upper-body layer is used that often");
        }

        // ---------------------------------------------------------------- 12, 13, 15: the rules never depend on the picture
        static void DeterminismTests()
        {
            // the same match stepped in frames of 1-4 ticks (30-144 fps, hitches) and with a "view" reading everything after every
            // tick ends identical: the rules step on ticks, never on frame times, and reading them changes nothing
            ulong a = RunGrouped(new Random(5), 1, false), b = RunGrouped(new Random(9), 4, true), c = RunGrouped(new Random(3), 2, true);
            V(a == b && b == c, "T12_T13_RulesIgnoreFramesAndViews", $"checksum {a:X} with 1 tick a frame, {b:X} with 1-4 and a reading view, {c:X} with 1-2");
            // online: host and guest step the same ticks with the same inputs (lockstep): the same AttackStart at the same tick
            ulong h1 = SwingHash(11), h2 = SwingHash(11);
            V(h1 == h2, "T15_SameSwingsOnEveryPhone", $"two copies of one match give the same swings (tick, unit, windup): {h1:X}; the view times them from that tick");
        }

        static ulong RunGrouped(Random r, int maxPerFrame, bool read)
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Seed = 77, PlayerIsBot = true, Difficulty = Difficulty.Hard, AllyDifficulty = Difficulty.Hard });
            int done = 0; float sink = 0;
            while (done < 60 * 120)
            {
                int n = 1 + r.Next(maxPerFrame);
                for (int i = 0; i < n && done < 60 * 120; i++, done++)
                {
                    m.Step(PlayerInput.None);
                    if (read) { foreach (var e in m.Events) sink += e.V; foreach (var u in m.Units) sink += u.X + u.Hp; }
                }
            }
            return m.Checksum() ^ (sink == float.MinValue ? 1UL : 0UL);
        }

        static ulong SwingHash(int seed)
        {
            var m = Create(new MatchConfig { PlayerHero = HeroId.Bram, Seed = seed, PlayerIsBot = true, Difficulty = Difficulty.Hard, AllyDifficulty = Difficulty.Hard });
            ulong h = 1469598103934665603UL;
            for (int k = 0; k < 60 * 90; k++)
            {
                m.Step(PlayerInput.None);
                foreach (var e in m.Events) if (e.Type == EventType.AttackStart) { h = (h ^ (ulong)m.Ticks) * 1099511628211UL; h = (h ^ (ulong)e.ASerial) * 1099511628211UL; h = (h ^ (ulong)BitConverter.SingleToInt32Bits(e.V)) * 1099511628211UL; }
            }
            return h;
        }

        // ---------------------------------------------------------------- 13: the picture's blow against the rules' release
        // GameFlow's loop modelled frame by frame: ticks step from an accumulator (at most 4 a frame, the rest dropped), or online
        // from the link (stalls, then one extra tick a frame to catch up). A swing starts on a tick; its shot is first drawn the
        // frame its release tick steps. "Now": the view sums frame times (BodyView today). "Sim clock": the view reads the
        // rules' tick count (AnimTiming.SimElapsed, the proposed sync, not wired yet). Error = blow frame time - shot frame time.
        static void ClockTests()
        {
            var cases = new (string name, Func<Random, int, double> dt, bool online)[]
            {
                ("60 fps", (r, f) => 1 / 60.0, false),
                ("30 fps", (r, f) => 1 / 30.0, false),
                ("120 fps", (r, f) => 1 / 120.0, false),
                ("jitter 8-25 ms", (r, f) => 0.008 + r.NextDouble() * 0.017, false),
                ("hitch 150 ms every 2 s", (r, f) => f % 120 == 60 ? 0.15 : 1 / 60.0, false),
                ("online, 60 fps, link stalls 100 ms every 2 s", (r, f) => 1 / 60.0, true),
            };
            float[] ws = { 0.15f, 0.195f, 0.2f, 0.7f };
            foreach (var c in cases)
            {
                double worstNow = 0, worstSim = 0, sumNow = 0; int n = 0, earlyNow = 0;
                var r = new Random(4);
                foreach (float w in ws)
                    for (int startFrame = 0; startFrame < 400; startFrame += 7)
                    {
                        Model(c.dt, c.online, r, startFrame, w, out double eNow, out double eSim);
                        n++; sumNow += Math.Abs(eNow);
                        if (Math.Abs(eNow) > Math.Abs(worstNow)) worstNow = eNow;
                        if (Math.Abs(eSim) > Math.Abs(worstSim)) worstSim = eSim;
                        if (eNow < -0.0175) earlyNow++;
                    }
                R("INFO", "T13_BlowVsShot " + c.name, $"now: worst {F(worstNow * 1000)} ms, mean |{F(sumNow / n * 1000)}| ms, {earlyNow}/{n} blows over a frame BEFORE the shot; sim clock: worst {F(worstSim * 1000)} ms");
                if (c.name == "60 fps") V(Math.Abs(worstNow) <= 1 / 60.0 + 1e-6, "T13_InSyncAt60", $"at a steady 60 fps today's clock is within one frame ({F(worstNow * 1000)} ms)");
                if (c.name.StartsWith("hitch") || c.name.StartsWith("online")) V(Math.Abs(worstSim) <= 1e-6, "T13_SimClockExact " + c.name, $"the sim clock puts the blow on the shot's frame ({F(worstSim * 1000)} ms)");
            }
        }

        static void Model(Func<Random, int, double> dtOf, bool online, Random r, int startFrame, float w, out double eNow, out double eSim)
        {
            double acc = 0, now = 0; long ticks = 0, avail = 0, startTick = -1, relTick = -1;
            double viewT = -1, blowNow = -1, blowSim = -1, shotAt = -1, linkAcc = 0;
            int rel = AnimTiming.ReleaseTick(w);
            for (int f = 0; f < startFrame + 400; f++)
            {
                double dt = dtOf(r, f); now += dt;
                if (!online)
                {
                    acc += dt; int n = 0;
                    while (acc >= Rules.Tick && n < 4) { acc -= Rules.Tick; n++; Tick(); }
                    if (acc >= Rules.Tick) acc = 0;
                }
                else
                {
                    // the host's ticks arrive at 60 a second, except for 100 ms every 2 s
                    linkAcc += dt;
                    while (linkAcc >= Rules.Tick) { linkAcc -= Rules.Tick; if (!(now % 2.0 > 1.0 && now % 2.0 < 1.1)) avail++; }
                    acc += dt; int n = 0;
                    while (acc >= Rules.Tick && n < 4) { acc -= Rules.Tick; n++; if (avail > ticks) Tick(); }
                    if (avail - ticks > 1 && avail > ticks) Tick(); // one extra tick a frame when behind
                    if (acc >= Rules.Tick) acc = 0;
                }
                // LateUpdate: the views
                if (viewT >= 0) { viewT += dt; if (blowNow < 0 && viewT >= w) blowNow = now; }
                if (startTick >= 0 && blowSim < 0)
                {
                    if (AnimTiming.SimElapsed(startTick, ticks, (float)(acc / Rules.Tick)) >= rel * AnimTiming.Tick) blowSim = now;
                }
                if (blowNow >= 0 && blowSim >= 0 && shotAt >= 0) break;

                void Tick()
                {
                    ticks++;
                    if (f >= startFrame && startTick < 0) { startTick = ticks; relTick = ticks + rel; viewT = 0; }
                    if (ticks == relTick) shotAt = now;
                }
            }
            eNow = blowNow - shotAt; eSim = blowSim - shotAt;
        }

        static int Main(string[] a)
        {
            PlanTests();
            LocomotionTests();
            SimTests();
            DeterminismTests();
            ClockTests();
            Console.WriteLine($"anim tests: {pass} pass, {fail} fail, {info} info");
            return fail > 0 ? 1 : 0;
        }
    }
}
