using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Shot hits by shot (precision SHOT-01, TYRA-07, TIKTA-02, CHAINMAP-01; thread "Kontrollera att skotten startar och landar rätt").
    // The view marks a dropped shot as hit only when a Hit of this tick carries its own serial (SimEvent.Shot, MatchViews.SyncProjs).
    // Checked here over Hard bot matches with all ten heroes: every stamped Hit comes from that shot (same shooter and kind, on a unit the
    // shot was touching), and Tyra's volley arrows ending in one tick are marked one by one. Helpers are prefixed Sh.
    public sealed partial class Match
    {
        struct ShRec { public Proj P; public int Src; public ProjKind Kind; public ProjMode Mode; public float Radius, Speed; }

        static void PrecShots()
        {
            if (!PViewKeysByShot)
            {   // main before the SHOT-01 fix: no SimEvent.Shot, the view keys hits by shooter + kind
                Fail("Shot_StampedHit_IsThatShots", "views", "P0", "SimEvent.Shot does not exist yet (SHOT-01 fix not applied): hits cannot be told apart per shot");
                Fail("SameTick_TwoProjectiles_SameKind", "views", "P0", "SimEvent.Shot does not exist yet (SHOT-01 fix not applied)");
                return;
            }
            var heroes = new[] { HeroId.Tyra, HeroId.Tikta, HeroId.Bram, HeroId.Bonk, HeroId.Blast, HeroId.Moa, HeroId.Knut, HeroId.Fenn, HeroId.Hilde, HeroId.Oaken };
            long dropped = 0, marked = 0, wrongShooter = 0, notTouching = 0, unknownShot = 0, volleyTicks = 0, volleyArrows = 0, volleyMarked = 0, oldRuleFalse = 0;
            string example = "";
            var live = new Dictionary<int, ShRec>(); var seen = new HashSet<int>();
            for (int mi = 0; mi < 30; mi++)
            {
                var m = PBotMatch(3000 + mi, heroes[mi % heroes.Length]);
                m.OneTap = false; // the volley's same-tick arrows need Tyra's old tap form (kept behind Rules.OneTapSpecial)
                live.Clear();
                for (int k = 0; k < 60 * 60 * 6 && m.State != MatchState.Over; k++)
                {
                    m.Step(PlayerInput.None);
                    var ev = m.Events;
                    // each shot's first stamped Hit this tick (its own; a passive's extra hits come after it)
                    var own = new Dictionary<int, SimEvent>();
                    for (int i = 0; i < ev.Count; i++) if (ev[i].Type == EventType.Hit && PShotOf(ev[i]) != 0 && !own.ContainsKey(PShotOf(ev[i]))) own[PShotOf(ev[i])] = ev[i];
                    seen.Clear();
                    foreach (var p in m.Projs) if (p.IsShot && !p.Done) seen.Add(p.Serial);
                    foreach (var kv in own)
                    {
                        Proj p = null; ShRec r;
                        if (live.TryGetValue(kv.Key, out r)) p = r.P;
                        else foreach (var q in m.Projs) if (q.Serial == kv.Key) p = q;
                        if (p == null) { foreach (var q in m.projPool) if (q.Serial == kv.Key) p = q; } // born and done inside this tick
                        if (p == null) { unknownShot++; continue; }
                        var e = kv.Value;
                        if (e.A != p.Src || (ProjKind)e.Kind != p.Kind) { wrongShooter++; if (example == "") example = $"seed {3000 + mi} t={m.Time:0.00}: shot {p.Serial} {p.Mode} {p.Kind}, hit says {(ProjKind)e.Kind} by #{e.ASerial}"; }
                        // touching: the unit hit is within the shot's reach of where the shot is (a homing shot lands on its target)
                        float reach = Math.Max(p.Radius, 0.5f) + (e.B != null ? e.B.R : 0) + p.Speed * Rules.Tick + 0.05f;
                        if (p.Mode != ProjMode.Auto && p.Mode != ProjMode.Hammer && p.Mode != ProjMode.Needle && e.B != null
                            && SimMath.Hypot(e.B.X - p.X, e.B.Y - p.Y) > reach + 0.6f) // the knocked or pulled may already move this tick
                        { notTouching++; if (example == "") example = $"seed {3000 + mi} t={m.Time:0.00}: shot {p.Serial} {p.Mode} at {SimMath.Hypot(e.B.X - p.X, e.B.Y - p.Y):0.00} from the unit it hit"; }
                    }
                    // shots the rules dropped this tick: marked by their own stamp (the view's rule) vs shooter + kind (the old HitKey)
                    var dropTick = new List<ShRec>();
                    foreach (var kv in live) if (!seen.Contains(kv.Key)) dropTick.Add(kv.Value);
                    var keyHit = new HashSet<int>();
                    for (int i = 0; i < ev.Count; i++) if (ev[i].Type == EventType.Hit && ev[i].A != null) keyHit.Add(ev[i].ASerial * 64 + ev[i].Kind);
                    int volleyNow = 0, volleyHitNow = 0;
                    foreach (var r in dropTick)
                    {
                        dropped++;
                        bool hit = own.ContainsKey(r.P.Serial);
                        if (hit) marked++;
                        if (!hit && keyHit.Contains(r.Src * 64 + (int)r.Kind)) oldRuleFalse++;
                        if (r.Mode == ProjMode.Volley) { volleyNow++; if (hit) volleyHitNow++; }
                        live.Remove(r.P.Serial);
                    }
                    if (volleyNow > 1 && volleyHitNow > 0) { volleyTicks++; volleyArrows += volleyNow; volleyMarked += volleyHitNow; }
                    foreach (var p in m.Projs)
                        if (p.IsShot && !p.Done && !live.ContainsKey(p.Serial))
                            live[p.Serial] = new ShRec { P = p, Src = p.Src != null ? p.Src.Serial : 0, Kind = p.Kind, Mode = p.Mode, Radius = p.Radius, Speed = p.Speed };
                }
            }
            Verdict("Shot_StampedHit_IsThatShots", "views", wrongShooter == 0 && notTouching == 0 && unknownShot == 0, "P0",
                $"30 Hard bot matches, 10 heroes: every Hit stamped with a shot (SimEvent.Shot) has that shot's shooter and kind and lands on a unit the shot touches: wrong shooter/kind {wrongShooter}, not touching {notTouching}, unknown shot {unknownShot}{(example != "" ? "; e.g. " + example : "")}");
            Verdict("SameTick_TwoProjectiles_SameKind", "views", oldRuleFalse > 0 || volleyTicks == 0 ? volleyMarked < volleyArrows : true, "P0",
                $"{dropped} shots ended, {marked} marked hit by their own stamp; ticks where several of one volley ended and one hit: {volleyTicks} ({volleyArrows} arrows, {volleyMarked} marked, the rest fizzle). The old shooter+kind key would have shown {oldRuleFalse} false hit splashes in these matches.");
        }
    }
}
