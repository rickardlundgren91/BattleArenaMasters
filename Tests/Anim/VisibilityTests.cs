using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Weapon Precision & Skill Visibility 2.0 (2026-10-30): what the view's new Tyra looks rely on, checked against the rules in Hard
    // bot matches with Tyra on both sides. View-only changes; these tests read the rules, never change them.
    public sealed partial class Match
    {
        static void VisibilityTests()
        {
            int volleys = 0, bolts = 0, volleyHits = 0, autoHits = 0, badKind = 0, badFan = 0, badOrigin = 0, badReach = 0;
            int outside = 0, boltHits = 0, boltOutside = 0, multiTick = 0;
            double worstFan = 0, worstOut = 0, worstBolt = 0;
            for (int seed = 1; seed <= 12; seed++)
            {
                var m = Create(new MatchConfig { PlayerHero = HeroId.Tyra, Seed = 500 + seed, PlayerIsBot = true, Difficulty = Difficulty.Hard, AllyDifficulty = Difficulty.Hard });
                var volleySerials = new HashSet<int>();
                // each volley: who, where she stood, which way the drawn fan points (the Cast event's aim), its reach
                var fanOf = new Dictionary<int, (float x, float y, float ang, float reach)>(); // by VolleyCast id per Tyra serial
                var arrowFan = new Dictionary<int, (float x, float y, float ang, float reach)>();
                var boltOf = new Dictionary<int, (float x, float y, float ang)>();
                var castNow = new Dictionary<int, (float x, float y, float ang, float reach)>();
                for (int k = 0; k < 60 * 300 && m.State != MatchState.Over; k++)
                {
                    m.Step(PlayerInput.None);
                    castNow.Clear();
                    foreach (var e in m.Events)
                    {
                        if (e.Type != EventType.Cast || e.A == null || (HeroId)e.Kind != HeroId.Tyra) continue;
                        float ang = (float)Math.Atan2(e.Y - e.A.Y, e.X - e.A.X);
                        if (e.Big) bolts++; else volleys++;
                        castNow[e.A.Serial] = (e.A.X, e.A.Y, ang, e.A.Range);
                    }
                    // the arrows born this tick: their angles against the drawn fan (ArtKit.PlayVolleyDraw: dir = the Cast's aim,
                    // +-TyraS.VolleyHalfAngle, TyraS.VolleyArrows lines, radius = her range, origin = where she stands)
                    var born = new Dictionary<int, List<Proj>>();
                    for (int i = 0; i < m.Projs.Count; i++)
                    {
                        var p = m.Projs[i];
                        if (p.Src == null || p.Src.Hero != HeroId.Tyra) continue;
                        if (p.Mode == ProjMode.Volley && volleySerials.Add(p.Serial))
                        {
                            if (!born.TryGetValue(p.Src.Serial, out var l)) born[p.Src.Serial] = l = new List<Proj>();
                            l.Add(p);
                        }
                        if (p.Mode == ProjMode.Bolt && !boltOf.ContainsKey(p.Serial) && castNow.TryGetValue(p.Src.Serial, out var bc))
                            boltOf[p.Serial] = (bc.x, bc.y, p.Ang);
                    }
                    foreach (var kv in born)
                    {
                        if (!castNow.TryGetValue(kv.Key, out var fan)) { multiTick++; continue; }
                        var l = kv.Value;
                        if (l.Count != TyraS.VolleyArrows) badFan++;
                        for (int i = 0; i < l.Count; i++)
                        {
                            var p = l[i];
                            double want = fan.ang - TyraS.VolleyHalfAngle + 2 * TyraS.VolleyHalfAngle * i / (TyraS.VolleyArrows - 1);
                            double d = Math.Abs(Wrap(p.Ang - want));
                            worstFan = Math.Max(worstFan, d);
                            if (d > 1e-4) badFan++;
                            if (Math.Abs(p.X - fan.x) > 1e-4f || Math.Abs(p.Y - fan.y) > 1e-4f) badOrigin++;
                            if (Math.Abs(p.Travel - fan.reach) > 1e-4f) badReach++;
                            arrowFan[p.Serial] = fan;
                        }
                    }
                    foreach (var e in m.Events)
                    {
                        if (e.Type != EventType.Hit || e.A == null || e.A.Kind != UnitKind.Hero || e.A.Hero != HeroId.Tyra) continue;
                        var kind = (ProjKind)e.Kind;
                        if (kind == ProjKind.TyraArrow)
                        {
                            bool volley = e.Shot != 0 && volleySerials.Contains(e.Shot);
                            // MatchViews: TyraArrow + DmgKind.Special = a volley arrow's hit (FxKind.VolleyHit), Auto = her auto
                            if (volley != (e.Dmg == DmgKind.Special)) badKind++;
                            if (!volley) { autoHits++; continue; }
                            volleyHits++;
                            if (!arrowFan.TryGetValue(e.Shot, out var fan) || e.B == null) continue;
                            // the hit enemy against the drawn fan: within its radius plus the hit reach, and its angle within the fan
                            // plus what the hit reach covers at that distance
                            double dx = e.X - fan.x, dy = e.Y - fan.y, dist = Math.Sqrt(dx * dx + dy * dy);
                            double reach = TyraS.VolleyHitbox + e.B.R;
                            double ang = Math.Abs(Wrap(Math.Atan2(dy, dx) - fan.ang));
                            double slack = dist > reach ? Math.Asin(reach / dist) : Math.PI;
                            double over = Math.Max(dist - (fan.reach + reach), ang - (TyraS.VolleyHalfAngle + slack));
                            if (over > 0.05) { outside++; worstOut = Math.Max(worstOut, over); }
                        }
                        else if (kind == ProjKind.TyraBolt && e.Shot != 0 && boltOf.TryGetValue(e.Shot, out var b) && e.B != null)
                        {
                            // the bolt's hit against its drawn strip (WarnLine: from where she stood, TyraS.Hitbox each side, Length)
                            boltHits++;
                            double dx = e.X - b.x, dy = e.Y - b.y;
                            double along = dx * Math.Cos(b.ang) + dy * Math.Sin(b.ang), across = Math.Abs(-dx * Math.Sin(b.ang) + dy * Math.Cos(b.ang));
                            double over = Math.Max(across - (TyraS.Hitbox + e.B.R), along - (TyraS.Length + TyraS.Hitbox + e.B.R));
                            if (over > 0.05) { boltOutside++; worstBolt = Math.Max(worstBolt, over); }
                        }
                    }
                }
            }
            R(volleys > 20 && bolts > 5 ? "PASS" : "FAIL", "V1_TyraCasts", $"{volleys} volleys (tap), {bolts} bolts (drag) in 12 Hard matches");
            V(badKind == 0 && volleyHits > 0, "V2_VolleyHitIsSpecial", $"{volleyHits} volley hits + {autoHits} auto hits; {badKind} where TyraArrow+Special did not mean a volley arrow (the view's VolleyHit rule)");
            V(badFan == 0, "V3_FanMatchesArrows", $"arrow angles vs the drawn fan: worst {F(worstFan * 180 / Math.PI)} deg, {badFan} off; {multiTick} volleys born without a Cast that tick");
            V(badOrigin == 0 && badReach == 0, "V4_FanOriginAndReach", $"{badOrigin} arrows not from where she stood at the cast, {badReach} with a reach other than the fan's radius (her range)");
            V(outside == 0, "V5_VolleyHitsInsideFan", $"{outside} of {volleyHits} volley hits more than 0.05 tiles outside the drawn fan + hit reach (worst {F(worstOut)})");
            V(boltOutside == 0 && boltHits > 0, "V6_BoltHitsInsideStrip", $"{boltOutside} of {boltHits} bolt hits outside the drawn strip + hit reach (worst {F(worstBolt)})");
        }

        static double Wrap(double a)
        {
            while (a > Math.PI) a -= 2 * Math.PI;
            while (a < -Math.PI) a += 2 * Math.PI;
            return a;
        }
    }
}
