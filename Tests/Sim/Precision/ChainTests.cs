using System;
using System.Collections.Generic;
using BAM.Online.Mp;

namespace BAM.Sim
{
    // Precision tests over the whole chain: special geometry against what the aim indicator draws (HDef.DragReach/DragWidth, the
    // values Hud.Aim hands AimIndicator), wind-up against the AttackStart the animation plays, the event stream the views read in
    // full bot matches, determinism, and the online lockstep.
    public sealed partial class Match
    {
        // ---------------------------------------------------------------- special geometry vs the aim indicator
        // Did the drag cast at (x, y) reach the enemy standing at (ex, ey)? Caster faces up the open middle (x 7.5, from y 28).
        static bool PDragHits(HeroId hero, HeroId foe, float ang, float k, float ex, float ey, out float castX, out float castY)
        {
            var m = PMake(hero, new[] { HeroId.Moa, HeroId.Fenn }, new[] { foe, hero == HeroId.Tikta ? HeroId.Tyra : HeroId.Tikta, hero == HeroId.Knut ? HeroId.Bonk : HeroId.Knut });
            var s = m.Player; var e = m.PFoe(foe);
            for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
            PDummy(e); PPlace(s, 7.5f, 28); PPlace(e, ex, ey);
            m.AddStatus(s, St.Disarm, 1e6f); s.Charge = s.HDef.MaxCharge;
            var ev = m.PRun(60 * 4, PDrag(ang, k), PlayerInput.None);
            castX = castY = float.NaN; bool hit = false;
            foreach (var x in ev)
            {
                if (x.Type == EventType.Cast && x.A == s) { castX = x.X; castY = x.Y; }
                if ((x.Type == EventType.Hit || x.Type == EventType.Damage || x.Type == EventType.Pull || x.Type == EventType.Knock) && x.B == e && (x.A == s || (x.A != null && x.A.Owner == s))) hit = true;
            }
            return hit;
        }

        static float PEdge(Func<float, bool> hitAt, float lo, float hi)
        {
            // lo hits, hi does not: the edge between them
            if (!hitAt(lo) || hitAt(hi)) return float.NaN;
            for (int i = 0; i < 14; i++) { float mid = (lo + hi) * 0.5f; if (hitAt(mid)) lo = mid; else hi = mid; }
            return (lo + hi) * 0.5f;
        }

        // Map.AimFor (Sim/Aim.cs, AIM fix) by reflection so the suite still builds on a main without it: the line the caster at
        // (7.5, 28) would see drawn, aimed at ang with drag share k. False when AimFor does not exist yet.
        static readonly System.Reflection.MethodInfo PAimForM = typeof(Match).GetMethod("AimFor");
        static bool PAimFor(HeroId hero, float ang, float k, out float len, out float tip, out float half)
        {
            len = tip = half = float.NaN;
            if (PAimForM == null) return false;
            var m = PMake(hero, new[] { HeroId.Moa, HeroId.Fenn }, new[] { HeroId.Bram, hero == HeroId.Tikta ? HeroId.Tyra : HeroId.Tikta, hero == HeroId.Knut ? HeroId.Bonk : HeroId.Knut });
            var s = m.Player;
            for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s) POff(m.Heroes[i]);
            PPlace(s, 7.5f, 28);
            object a = PAimForM.Invoke(m, new object[] { s, ang, k });
            var t = a.GetType();
            len = (float)t.GetField("Length").GetValue(a); tip = (float)t.GetField("Tip").GetValue(a); half = (float)t.GetField("Half").GetValue(a);
            return true;
        }

        static void PrecGeometry()
        {
            const float Up = -SimMath.PI / 2, Tol = 0.05f;
            foreach (var hero in new[] { HeroId.Tyra, HeroId.Tikta, HeroId.Knut, HeroId.Blast, HeroId.Bonk, HeroId.Oaken })
            {
                var d = Defs.Hero(hero); float er = Defs.Hero(HeroId.Bram).Radius;
                float along = Math.Min(2.5f, d.DragReach * 0.5f);
                float reach = PEdge(a => PDragHits(hero, HeroId.Bram, Up, 1, 7.5f, 28 - a, out _, out _), 0.8f, d.DragReach + 2f);
                float width = PEdge(w => PDragHits(hero, HeroId.Bram, Up, 1, 7.5f + w, 28 - along, out _, out _), 0f, 2.5f);
                // the drawn arrow: Map.AimFor (Length + Tip, Half) once the AIM fix is in, else HDef.DragReach/DragWidth as Hud.Aim drew it
                float arrowLen = d.DragReach, arrowHalf = d.DragWidth; string src = "DragReach/DragWidth";
                if (PAimFor(hero, Up, 1, out float aLen, out float aTip, out float aHalf)) { arrowLen = aLen + aTip; arrowHalf = aHalf; src = $"Map.AimFor (length {F(aLen)} + tip {F(aTip)})"; }
                float wantReach = arrowLen + er, wantWidth = arrowHalf + er;
                bool ok = Math.Abs(reach - wantReach) <= Tol && Math.Abs(width - wantWidth) <= Tol;
                Verdict($"SpecialPreview_Line_{hero}", "geometry", ok, "P1",
                    $"aim arrow from {src}: {F(arrowLen)} long, {F(arrowHalf)} half wide; sim hits a Bram (r {F(er)}) out to {F(reach)} along (arrow+r {F(wantReach)}, off {F(reach - wantReach)}) and {F(width)} across (arrow+r {F(wantWidth)}, off {F(width - wantWidth)})");
            }
            foreach (var (hero, foe) in new[] { (HeroId.Bram, HeroId.Knut), (HeroId.Hilde, HeroId.Bram) })
            {
                var d = Defs.Hero(hero); float er = Defs.Hero(foe).Radius;
                // the landing spot at full and half drag, as the indicator puts it: min(reach, max(1.5, k reach)) out
                PDragHits(hero, foe, Up, 1, 0.5f, 0.5f, out float cx1, out float cy1);
                PDragHits(hero, foe, Up, 0.5f, 0.5f, 0.5f, out float cx2, out float cy2);
                float want1 = Math.Min(d.DragReach, Math.Max(1.5f, d.DragReach)), want2 = Math.Min(d.DragReach, Math.Max(1.5f, 0.5f * d.DragReach));
                float off1 = SimMath.Hypot(cx1 - 7.5f, cy1 - (28 - want1)), off2 = SimMath.Hypot(cx2 - 7.5f, cy2 - (28 - want2));
                float cy = 28 - want1;
                float radius = PEdge(r => PDragHits(hero, foe, Up, 1, 7.5f + r, cy, out _, out _), 0f, 3f);
                float want = d.DragWidth + er;
                bool ok = off1 <= 0.01f && off2 <= 0.01f && Math.Abs(radius - want) <= Tol;
                Verdict($"SpecialPreview_Circle_{hero}", "geometry", ok, "P1",
                    $"landing spot off the indicator's by {F(off1)} (full drag) / {F(off2)} (half); circle {F(d.DragWidth)} radius, sim hits a {foe} (r {F(er)}) out to {F(radius)} (circle+r {F(want)}, off {F(radius - want)})");
            }
            Info("SpecialPreview_NotHeadless", "geometry",
                "not checked here (Unity-only, needs an Editor test): AimIndicator.Line's minimum width 0.3/length 0.6, the arrow head and RangeAt/AreaAt art calibration, drawing from DrawnAt (interpolated) while the rules cast from X/Y; Fenn's thrown wolf (homes onto heroes along the flight, no fixed circle); Moa (ally shields)");
        }

        // ---------------------------------------------------------------- wind-up vs AttackStart (the animation's release)
        static void PrecRelease()
        {
            var rows = new List<string>(); int bad = 0; double worstEarly = 0;
            for (int hi = 0; hi < Defs.Heroes.Length; hi++)
            {
                var hero = (HeroId)hi;
                var foe = hero == HeroId.Knut ? HeroId.Bram : HeroId.Knut;
                var m = PMake(hero, new[] { hero == HeroId.Moa ? HeroId.Fenn : HeroId.Moa, hero == HeroId.Tikta ? HeroId.Tyra : HeroId.Tikta }, new[] { foe, HeroId.Blast == hero ? HeroId.Hilde : HeroId.Blast, hero == HeroId.Oaken ? HeroId.Bonk : HeroId.Oaken });
                var s = m.Player; var e = m.PFoe(foe);
                for (int i = 0; i < m.Heroes.Count; i++) if (m.Heroes[i] != s && m.Heroes[i] != e) POff(m.Heroes[i]);
                PDummy(e); PPlace(s, 7.5f, 20); PPlace(e, 7.5f, 20 - Math.Min(1.5f, s.Range - 0.1f));
                long startTick = -1, releaseTick = -1; float v = -1; int serial = -1;
                for (int k = 0; k < 300 && releaseTick < 0; k++)
                {
                    m.Step(PlayerInput.None);
                    foreach (var x in m.Events)
                    {
                        if (x.Type == EventType.AttackStart && x.A == s && startTick < 0) { startTick = m.Ticks; v = x.V; foreach (var p in m.Projs) if (p.Src == s && (p.Mode == ProjMode.Auto || p.Mode == ProjMode.Smash) && !p.Done) serial = p.Serial; }
                        if (startTick >= 0 && hero == HeroId.Bonk && x.Type == EventType.Explosion && x.A == s && releaseTick < 0) releaseTick = m.Ticks;
                        if (startTick >= 0 && hero != HeroId.Bonk && x.Type == EventType.Hit && x.A == s && releaseTick < 0) releaseTick = m.Ticks; // a shot launched and landed inside one tick
                    }
                    if (startTick >= 0 && releaseTick < 0) foreach (var p in m.Projs) if (p.Serial == serial && p.Flying) releaseTick = m.Ticks;
                }
                double sim = (releaseTick - startTick) * Rules.Tick, delta = sim - v;
                worstEarly = Math.Min(worstEarly, delta);
                if (startTick < 0 || releaseTick < 0 || Math.Abs(delta) > Rules.Tick + 1e-4) bad++;
                rows.Add($"{hero} {(v * 1000):0}/{(sim * 1000):0}ms");
            }
            Verdict("AttackStart_V_MatchesSimRelease", "timing", bad == 0, "P2",
                $"AttackStart wind-up (V, what PlayAttack times the blow to) / shot out in the rules: {string.Join(", ", rows)}; the shot is drawn up to {(-worstEarly * 1000):0} ms before the animation's release (one tick: T counts from -V and gains a tick in its own birth tick)");
        }

        // ---------------------------------------------------------------- full bot matches: the event stream the views read
        struct PShot { public Proj P; public int Serial, Src, Tgt; public ProjKind Kind; public ProjMode Mode; public float T, X, Y, Radius; }

        // MatchViews.HitKey at d4afdde: (attacker serial, kind), the volley's arrows as TyraArrow
        static int PViewHitKey(int src, ProjKind kind) => src * 64 + (int)(kind == ProjKind.TyraVolley ? ProjKind.TyraArrow : kind);

        static void PrecBotMatches()
        {
            int matches = 0; long ticks = 0;
            long heroVanish = 0, dropped = 0, falseConfirm = 0, ghostWrongSpot = 0, silentVanish = 0, sameTickBlockedGhost = 0, teleports = 0, dupAttackStart = 0;
            long contractBreaks = 0, contractChecked = 0; int maxEvents = 0; double maxJump = 0; long followJumps = 0; string jumpWhat = "";
            var falseBy = new Dictionary<string, int>(); var vanishBy = new Dictionary<string, int>(); var breakBy = new Dictionary<string, int>();
            var prev = new Dictionary<int, PShot>(); var now = new Dictionary<int, PShot>();
            var heroes = new[] { HeroId.Tyra, HeroId.Bram, HeroId.Bonk, HeroId.Tikta, HeroId.Blast, HeroId.Moa, HeroId.Knut, HeroId.Fenn, HeroId.Hilde, HeroId.Oaken };
            for (int mi = 0; mi < 30; mi++)
            {
                var m = PBotMatch(1000 + mi, heroes[mi % heroes.Length]);
                matches++; prev.Clear();
                var usedHit = new List<bool>();
                for (int k = 0; k < 60 * 60 * 8 && m.State != MatchState.Over; k++)
                {
                    m.Step(PlayerInput.None); ticks++;
                    var ev = m.Events;
                    maxEvents = Math.Max(maxEvents, ev.Count);
                    // live shots now
                    now.Clear();
                    foreach (var p in m.Projs) if (!p.Done && p.IsShot)
                            now[p.Serial] = new PShot { P = p, Serial = p.Serial, Src = p.Src != null ? p.Src.Serial : 0, Tgt = p.Tgt != null ? p.Tgt.Serial : 0, Kind = p.Kind, Mode = p.Mode, T = p.T, X = p.X, Y = p.Y, Radius = p.Radius };
                    // shots flying both ticks: no jump bigger than a tile in one tick
                    foreach (var kv in now)
                        if (prev.TryGetValue(kv.Key, out PShot a) && a.T >= 0 && kv.Value.T >= 0)
                        {
                            double j = Math.Sqrt((kv.Value.X - a.X) * (kv.Value.X - a.X) + (kv.Value.Y - a.Y) * (kv.Value.Y - a.Y));
                            if (j > 1.0)
                            {
                                if (kv.Value.Tgt != a.Tgt) { teleports++; if (j > maxJump) { maxJump = j; jumpWhat = a.Mode + "/" + a.Kind; } } // a retarget (CHAINMAP-05)
                                else if (kv.Value.P.Tgt != null && kv.Value.P.Tgt.Kind != UnitKind.Tower && (a.Mode == ProjMode.Auto)) followJumps++; // the same target blinked or slid: the timed shot follows it
                            }
                        }
                    // AttackStart twice for one attacker and kind in a tick (the views pair them by that)
                    for (int i = 0; i < ev.Count; i++)
                        if (ev[i].Type == EventType.AttackStart)
                            for (int j = i + 1; j < ev.Count; j++) if (ev[j].Type == EventType.AttackStart && ev[j].ASerial == ev[i].ASerial && ev[j].Kind == ev[i].Kind) dupAttackStart++;
                    // shots born and ended inside this tick, stopped by a wall: the views' pending ghost flies from the shooter to the target
                    for (int i = 0; i < ev.Count; i++)
                    {
                        if (ev[i].Type != EventType.Blocked || ev[i].A == null) continue;
                        bool tracked = false; foreach (var kv in prev) if (kv.Value.Src == ev[i].ASerial && (byte)kv.Value.Kind == ev[i].Kind) tracked = true;
                        bool started = false; for (int j = 0; j < ev.Count; j++) if (ev[j].Type == EventType.AttackStart && ev[j].ASerial == ev[i].ASerial && ev[j].Kind == ev[i].Kind) started = true;
                        if (!tracked && started) sameTickBlockedGhost++;
                    }
                    // the dropped shots: did each really hit? does the view think so?
                    usedHit.Clear(); for (int i = 0; i < ev.Count; i++) usedHit.Add(false);
                    var viewKeys = new HashSet<int>(); var keyHits = new Dictionary<int, List<int>>();
                    for (int i = 0; i < ev.Count; i++)
                        if (ev[i].Type == EventType.Hit && ev[i].A != null)
                        {
                            int hk = PViewHitKey(ev[i].ASerial, (ProjKind)ev[i].Kind); viewKeys.Add(hk);
                            if (!keyHits.TryGetValue(hk, out var l)) keyHits[hk] = l = new List<int>(); l.Add(i);
                        }
                    foreach (var kv in prev)
                    {
                        if (now.ContainsKey(kv.Key)) continue;
                        var s = kv.Value; dropped++;
                        bool launched = s.T >= -Rules.Tick - 1e-5f; // drawn (T >= 0) or launched and landed this tick (the views' ghost)
                        if (!launched) continue; // cancelled in the hand: nothing flew
                        int match = -1;
                        for (int i = 0; i < ev.Count && match < 0; i++)
                        {
                            var x = ev[i];
                            if (usedHit[i] || x.Type != EventType.Hit || x.ASerial != s.Src || x.Kind != (byte)s.Kind) continue;
                            if (PViewKeysByShot && PShotOf(x) != s.Serial) continue; // after the SHOT-01 fix every hit a flying shot makes carries its stamp: another stamp or none is not this shot's
                            if (s.Mode == ProjMode.Auto || s.Mode == ProjMode.Needle || s.Mode == ProjMode.Hammer) { if (x.BSerial == s.Tgt || (s.P.Tgt != null && x.BSerial == s.P.Tgt.Serial)) match = i; } // a retarget in its last tick
                            else if (x.B != null && SimMath.Hypot(x.B.X - s.P.X, x.B.Y - s.P.Y) <= s.Radius + x.B.R + 0.6f) match = i;
                        }
                        bool blocked = false; foreach (var x in ev) if (x.Type == EventType.Blocked && x.ASerial == s.Src && x.Kind == (byte)s.Kind) blocked = true;
                        if (match >= 0) usedHit[match] = true;
                        int key = PViewHitKey(s.Src, s.Kind);
                        int ownHit = -1;
                        if (PViewKeysByShot) for (int i = 0; i < ev.Count && ownHit < 0; i++) if (ev[i].Type == EventType.Hit && PShotOf(ev[i]) == s.Serial) ownHit = i;
                        bool viewHit = PViewKeysByShot ? ownHit >= 0 : viewKeys.Contains(key);
                        if (viewHit && match < 0)
                        {
                            falseConfirm++; string w = s.Mode + "/" + s.Kind + (blocked ? " (blocked)" : "");
                            falseBy[w] = falseBy.TryGetValue(w, out int c) ? c + 1 : 1;
                        }
                        if (PViewKeysByShot) { if (match >= 0 && s.T < 0 && ownHit >= 0 && SimMath.Hypot(ev[ownHit].X - ev[match].X, ev[ownHit].Y - ev[match].Y) > 0.05f) ghostWrongSpot++; }
                        else if (match >= 0 && s.T < 0 && keyHits[key].Count > 1)
                        {
                            // the views fly an unseen shot to hitAt[key]: the LAST hit of that key this tick
                            var last = ev[keyHits[key][keyHits[key].Count - 1]]; var mine = ev[match];
                            if (SimMath.Hypot(last.X - mine.X, last.Y - mine.Y) > 0.05f) ghostWrongSpot++;
                        }
                        // a shot that flew on to its fallen target's spot (Proj.Fizzle) is seen to land there: only one that ends away from it vanishes
                        bool atTarget = s.P.Tgt != null && SimMath.Hypot(s.P.Tgt.X - s.P.X, s.P.Tgt.Y - s.P.Y) <= s.P.Tgt.R + 0.35f
                            || s.P.Fizzle && SimMath.Hypot(s.P.FX - s.P.X, s.P.FY - s.P.Y) <= 0.05f; // or at the spot its target blinked or recalled from (SHOT-08)
                        if (match < 0 && !blocked && !atTarget && (s.Mode == ProjMode.Auto || s.Mode == ProjMode.Needle || s.Mode == ProjMode.Hammer) && s.T >= 0)
                        {
                            silentVanish++; if (s.Kind <= ProjKind.BlastFire || s.Kind >= ProjKind.BramHammerSpecial && s.Kind != ProjKind.TowerShot) heroVanish++; string w = s.Mode + "/" + s.Kind;
                            vanishBy[w] = vanishBy.TryGetValue(w, out int c) ? c + 1 : 1;
                        }
                    }
                    // the views' contract (MatchViews.IsTick): a hit's Damage is followed by its Hit within 3 events, same attacker and
                    // target, no Damage between; else a special or passive hit is taken for a burn/poison tick (no flinch, no hit tick)
                    for (int j = 0; j < ev.Count; j++)
                    {
                        var h = ev[j]; if (h.Type != EventType.Hit || !(h.V > 0)) continue;
                        int i = j - 1; while (i >= 0 && !(ev[i].Type == EventType.Damage && ev[i].BSerial == h.BSerial)) i--;
                        if (i < 0) continue;
                        if (ev[i].Dmg == DmgKind.Auto) continue; // IsTick never looks at auto damage
                        contractChecked++;
                        bool between = false; for (int q = i + 1; q < j; q++) if (ev[q].Type == EventType.Damage) between = true;
                        if (j - i > 3 || between || ev[i].ASerial != h.ASerial)
                        {
                            contractBreaks++; string w = ((ProjKind)h.Kind) + (j - i > 3 ? " far" : "") + (between ? " dmg-between" : "") + (ev[i].ASerial != h.ASerial ? " owner!=src" : "");
                            breakBy[w] = breakBy.TryGetValue(w, out int c) ? c + 1 : 1;
                        }
                    }
                    var t = prev; prev = now; now = t;
                }
            }
            string Top(Dictionary<string, int> d) { var l = new List<KeyValuePair<string, int>>(d); l.Sort((a, b) => b.Value.CompareTo(a.Value)); var s = new List<string>(); for (int i = 0; i < l.Count && i < 5; i++) s.Add(l[i].Key + " x" + l[i].Value); return string.Join(", ", s); }
            string per(long n) => F2(n * 100.0 / matches) + " per 100 matches";
            Info("BotMatches_Run", "chain", $"{matches} Hard bot matches, {ticks} ticks ({ticks / 3600.0:0} min of play), {dropped} shots ended");
            Verdict("SameTick_HitKey_NoFalseConfirm", "views", falseConfirm == 0, "P1",
                $"view rule modelled: {(PViewKeysByShot ? "per shot (SimEvent.Shot)" : "attacker+kind (HitKey)")}; {falseConfirm} shots that missed (reach end or wall) were marked as hits ({per(falseConfirm)}): {Top(falseBy)}");
            Verdict("SameTick_GhostFliesToOwnHit", "views", ghostWrongSpot == 0, "P2", $"{ghostWrongSpot} unseen shots flown to another hit's spot (hitAt keeps the last per key)");
            Verdict("Blocked_SameTickShot_NoGhostThroughWall", "views", sameTickBlockedGhost == 0, "P2",
                $"{sameTickBlockedGhost} shots born and blocked in one tick: the pending ghost flies shooter -> target, through the wall ({per(sameTickBlockedGhost)})");
            Verdict("Shot_EndsWithHitOrBlocked", "views", silentVanish == 0, "P3",
                $"{silentVanish} homing shots ({heroVanish} of them heroes') ended in flight with no Hit or Blocked (target died with nobody to retarget, or went untargetable): drawn as vanishing in mid-air ({per(silentVanish)}): {Top(vanishBy)}");
            Verdict("Projectile_NoTeleport_OnRetarget", "views", teleports == 0, "P2",
                $"{teleports} ticks where a shot that changed target jumped more than 1 tile in one tick (max {F((float)maxJump)} tiles, {jumpWhat})");
            Info("Projectile_FollowsMovedTarget", "views",
                $"{followJumps} ticks where a timed shot jumped more than 1 tile with the SAME target: the target blinked in the tick the shot was fired (from then on a blink makes it miss, SHOT-08), or a fast melee slash (Fenn/Knut, 0.08 s over up to 3 tiles). Rules as they are, not CHAINMAP-05");
            Verdict("AttackStart_UniquePerTick", "views", dupAttackStart == 0, "P2", $"{dupAttackStart} ticks with two AttackStart of one attacker and kind (the views pair shots by that)");
            Verdict("EventOrder_DamageThenHit_Contract", "views", contractBreaks == 0, "P2",
                $"{contractBreaks} of {contractChecked} special/passive hits break MatchViews.IsTick's 'Damage then Hit within 3' rule (taken for a DoT tick: no flinch, no hit tick): {Top(breakBy)}");
            Verdict("Events_UnderCap", "chain", maxEvents < 2048 / 4, "P1", $"most events in one tick: {maxEvents} (Emit drops past 2048)");
        }

        // ---------------------------------------------------------------- determinism
        static void PrecDeterminism()
        {
            // the same seed and inputs give the same match and the same events, tick by tick
            Match Scripted() => Create(new MatchConfig { PlayerHero = HeroId.Bonk, Seed = 4242, Difficulty = Difficulty.Hard });
            var a = Scripted(); var b = Scripted();
            var ra = new Random(9); var rb = new Random(9);
            PlayerInput Inp(Random r)
            {
                var i = PlayerInput.None; double g = r.NextDouble() * 6.283; i.MoveX = (float)Math.Cos(g); i.MoveY = (float)Math.Sin(g);
                if (r.NextDouble() < 0.02) { i.Special = true; i.AimAngle = r.NextDouble() < 0.5 ? float.NaN : (float)(r.NextDouble() * 6 - 3); i.AimK = (float)r.NextDouble(); }
                return i;
            }
            int firstDiff = -1; var ea = new List<SimEvent>(); var eb = new List<SimEvent>();
            for (int k = 0; k < 60 * 60 * 5; k++)
            {
                var ia = Inp(ra); var ib = Inp(rb);
                a.Step(ia); b.Step(ib);
                ea.Clear(); ea.AddRange(a.Events); eb.Clear(); eb.AddRange(b.Events);
                if (firstDiff < 0 && (a.Checksum() != b.Checksum() || PEventHash(ea) != PEventHash(eb))) firstDiff = k;
            }
            Verdict("Determinism_SameSeedSameInputs", "determinism", firstDiff < 0, "P0", firstDiff < 0 ? "5 min, checksum and event stream equal every tick" : "drifted apart at tick " + firstDiff);

            // what the online fingerprint (Checksum) sees: a match that drifted in a field it does not hash is found only later
            var rows = new List<string>(); int late = 0;
            foreach (var what in new[] { "stun 0.5 s", "facing +0.5", "attack timer 0", "status timer -0.1", "proj T -0.05", "rng step" })
            {
                var x = Scripted(); var y = Scripted(); var rx = new Random(9); var ry = new Random(9);
                for (int k = 0; k < 900; k++) { x.Step(Inp(rx)); y.Step(Inp(ry)); }
                Unit h = null; foreach (var u in y.Heroes) if (u.Alive && u.Bot != null) { h = u; break; }
                // the field must exist to drift: give both runs the same slow, and step both until a shot is in the air
                if (what == "status timer -0.1") { Unit hx = null; foreach (var u in x.Heroes) if (u.Serial == h.Serial) hx = u; x.AddStatus(hx, St.Root, 3f); y.AddStatus(h, St.Root, 3f); }
                if (what == "proj T -0.05") for (int k = 0; k < 600 && y.Projs.Count == 0; k++) { x.Step(Inp(rx)); y.Step(Inp(ry)); }
                switch (what)
                {
                    case "stun 0.5 s": y.AddStatus(h, St.Stun, 0.5f); break;
                    case "facing +0.5": h.Facing += 0.5f; break;
                    case "attack timer 0": h.AtkTimer = 0; break;
                    case "status timer -0.1": foreach (var u in y.Units) for (int s = 0; s < u.StCount; s++) u.Sts[s].T -= 0.1f; break;
                    case "proj T -0.05": foreach (var p in y.Projs) p.T -= 0.05f; break;
                    case "rng step": y.Rng.Int(10); break;
                }
                bool now = x.Checksum() != y.Checksum(); int seen = now ? 0 : -1;
                for (int k = 1; k <= 600 && seen < 0; k++) { x.Step(Inp(rx)); y.Step(Inp(ry)); if (x.Checksum() != y.Checksum()) seen = k; }
                if (seen != 0) late++;
                rows.Add($"{what}: {(seen < 0 ? "not seen in 10 s" : seen == 0 ? "at once" : (seen / 60f).ToString("0.00") + " s later")}");
            }
            Verdict("Checksum_SeesDriftAtOnce", "online", late == 0, "P3",
                $"a one-field drift in the online fingerprint (Match.Checksum) shows: {string.Join("; ", rows)}");
        }

        // ---------------------------------------------------------------- online lockstep: two phones in one process
        sealed class PPipe { public readonly List<(double t, byte[] d)> Q = new List<(double, byte[])>(); }

        static void PrecLockstep()
        {
            foreach (bool vs in new[] { false, true })
            {
                const int n = 2;
                var seats = new SeatConfig[n];
                seats[0] = new SeatConfig { Hero = HeroId.Tyra, Team = 0 }; seats[1] = new SeatConfig { Hero = HeroId.Bonk, Team = vs ? 1 : 0 };
                var ms = new Match[n]; var ls = new MpLockstep[n]; var toHost = new PPipe(); var toGuest = new PPipe();
                var net = new Random(7); double now = 0;
                void Put(PPipe p, MpWriter w, double lag)
                {
                    var b = new byte[w.Len]; Buffer.BlockCopy(w.Data, 0, b, 0, w.Len);
                    double t = now + lag + net.NextDouble() * 0.08; if (p.Q.Count > 0 && p.Q[p.Q.Count - 1].t > t) t = p.Q[p.Q.Count - 1].t;
                    p.Q.Add((t, b));
                }
                for (int s = 0; s < n; s++)
                {
                    ms[s] = Create(new MatchConfig { Seats = seats, LocalSeat = s, Seed = 777, Difficulty = Difficulty.Normal });
                    ls[s] = new MpLockstep(s == 0, n, s);
                }
                ls[0].Send = w => Put(toGuest, w, 0.04);
                ls[1].Send = w => Put(toHost, w, 0.04);
                ls[0].Start();
                var hashes = new List<ulong>[n]; for (int s = 0; s < n; s++) hashes[s] = new List<ulong>();
                var evBuf = new List<SimEvent>();
                var ins = new PlayerInput[Match.MaxSeats];
                var rnd = new Random(3); var cur = new PlayerInput[n];
                // the guest's special presses: when submitted (frame) and when stepped on the guest's own phone (frame)
                var pressAt = new Queue<int>(); var lat = new List<int>(); int presses = 0, arrivedHost = 0, arrivedGuest = 0;
                int frames = 60 * 60 * 4; int g0 = 0, g1 = 0, g2 = 0;
                for (int f = 0; f < frames + 600; f++)
                {
                    now = f / 60.0;
                    for (int s = 0; s < n; s++)
                    {
                        if (s == 0) { while (toHost.Q.Count > 0 && toHost.Q[0].t <= now) { var d = toHost.Q[0].d; toHost.Q.RemoveAt(0); var r = new MpReader(d, d.Length); int t = r.U8(); if (t == MpMsg.In) ls[0].OnIn(1, ref r); else if (t == MpMsg.Hash) { long ff = r.I32(); ls[0].OnHash(1, ff, r.U64()); } } }
                        else { while (toGuest.Q.Count > 0 && toGuest.Q[0].t <= now) { var d = toGuest.Q[0].d; toGuest.Q.RemoveAt(0); var r = new MpReader(d, d.Length); int t = r.U8(); if (t == MpMsg.Frames) ls[1].OnFrames(ref r); else if (t == MpMsg.Desync) ls[1].GoSolo("desync"); } }
                        if (f < frames)
                        {
                            if (rnd.NextDouble() < 0.02) { double g = rnd.NextDouble() * 6.28; cur[s].MoveX = (float)Math.Cos(g); cur[s].MoveY = (float)Math.Sin(g); }
                            var i = PlayerInput.None; i.MoveX = cur[s].MoveX; i.MoveY = cur[s].MoveY;
                            if (s == 1 && f % 97 == 0) { i.Special = true; i.AimAngle = float.NaN; presses++; pressAt.Enqueue(f); }
                            else if (s == 0 && rnd.NextDouble() < 0.01) { i.Special = true; i.AimAngle = (float)(rnd.NextDouble() * 6 - 3); i.AimK = 1; }
                            ls[s].Submit(in i);
                        }
                        int k = 0;
                        while (k < (s == 0 ? 1 : 1 + 4) && ls[s].Next(ms[s], ins))
                        {
                            if (ins[1].Special) { if (s == 0) arrivedHost++; else { arrivedGuest++; if (pressAt.Count > 0) lat.Add(f - pressAt.Dequeue()); } }
                            ms[s].Step(ins); ls[s].AfterStep(ms[s]);
                            evBuf.Clear(); evBuf.AddRange(ms[s].Events); hashes[s].Add(PEventHash(evBuf) ^ ms[s].Checksum());
                            k++; if (s != 0 && ls[s].Backlog <= 3) break;
                        }
                        ls[s].Flush();
                        if (s == 1 && f > 300 && f < frames) { if (k == 0) g0++; else if (k == 1) g1++; else g2++; }
                    }
                }
                int same = Math.Min(hashes[0].Count, hashes[1].Count), firstDiff = -1;
                for (int i = 0; i < same && firstDiff < 0; i++) if (hashes[0][i] != hashes[1][i]) firstDiff = i;
                string mode = vs ? "vs" : "together";
                Verdict($"Lockstep_HostGuestSameEvents_{mode}", "online", firstDiff < 0 && same > 60 * 60 * 3 && ls[0].Problem == null && ls[1].Problem == null, "P0",
                    $"{same} ticks stepped on both phones (lag 40-120 ms one way): {(firstDiff < 0 ? "every tick's events and checksum equal, so hits, FX, sounds and the kill feed match" : "first different tick " + firstDiff)}; problems host '{ls[0].Problem ?? "-"}' guest '{ls[1].Problem ?? "-"}'");
                Verdict($"Lockstep_GuestPressesArrive_{mode}", "online", arrivedHost == presses && arrivedGuest == presses, "P0",
                    $"guest pressed the special {presses} times; stepped with it on the host {arrivedHost}, on the guest {arrivedGuest}");
                int gt = Math.Max(1, g0 + g1 + g2);
                Info($"Lockstep_GuestFramePacing_{mode}", "online",
                    $"guest frames (60 fps) that stepped 0 / 1 / 2+ ticks: {g0 * 100.0 / gt:0.0} % / {g1 * 100.0 / gt:0.0} % / {g2 * 100.0 / gt:0.0} %. A 0-tick frame redraws the same tick with a new alpha (GameFlow.NetTicks keeps acc from the local clock), a 2+ tick frame jumps: visible stutter under jitter, NOT TESTED on phones");
                if (lat.Count > 0)
                {
                    lat.Sort(); double avg = 0; foreach (int x in lat) avg += x; avg /= lat.Count;
                    Info($"Lockstep_GuestInputDelay_{mode}", "online",
                        $"guest press -> stepped on the guest's own phone: avg {avg * 1000 / 60:0} ms, p95 {lat[(int)(lat.Count * 0.95)] * 1000 / 60} ms, max {lat[lat.Count - 1] * 1000 / 60} ms (no prediction: the guest's taps and moves wait for the host's frame; the host's own are 0 ms)");
                }
            }
        }
    }
}
