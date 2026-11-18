using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // When a bot fires its special, tap or drag, and how it steps out of shots and red warnings (old SimAI.cs aiSpecialWanted,
    // sureShot, dodgeVector; SimHeroes6.cs newHeroSpecialWanted; SimSpecials2.cs v2Wanted; SimBotDrag.cs). In the one-tap game Gold,
    // Platinum and Ultra bots drag the one special by hand as a player may (OneTapDrag, bot audit 2026-11-13).
    public sealed partial class BotBrain
    {
        // ---------- who is in reach ----------
        /// <summary>Old foes(r): living visible enemies (not towers, not untargetable) within r (x the trigger-range knob) of h;
        /// heroesOnly = old heroesIn(r); los = only those with a clear line (specials that fly or strike).</summary>
        int Foes(float r, bool heroesOnly, bool los, List<Unit> into)
        {
            var h = H; into.Clear(); float rs = TK(24); var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (!u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || !Sees(u) || Dist(u, h) > r * rs + u.R) continue;
                if (heroesOnly && (u.Kind != UnitKind.Hero || los && !M.Map.LosClear(h, u))) continue;
                into.Add(u);
            }
            return into.Count;
        }

        /// <summary>Bram, Knut, Tikta and Tyra's specials fly or strike: bots want them only for a hero they have a clear line to.</summary>
        static bool SpecialIsShot(HeroId id) => id == HeroId.Bram || id == HeroId.Knut || id == HeroId.Tikta || id == HeroId.Tyra || id == HeroId.Bonk || id == HeroId.Oaken;

        /// <summary>Skillshots fire at once in every tier (the old V2 wait for a still target is off: knob 20 is 0 or less).</summary>
        bool SureShot(List<Unit> hs, bool dz)
        {
            if (hs.Count == 0) { spWantT = 0; return false; }
            if (dz || TK(20) <= 0) return true;
            Unit t = Nearest(hs, H);
            if (t.Has(St.Stun) || t.Has(St.SlowPct) || Hyp(t.MoVx, t.MoVy) < 0.7f) return true;
            if (!(spWantT > 0)) spWantT = Now;
            return Now - spWantT > TK(20);
        }

        bool DragTier => tierI >= 2; // Gold and Platinum drag; Bronze and Silver always tap

        /// <summary>Does the bot want its special now (old newHeroSpecialWanted with v2Wanted for Tyra)? Internal for Tests/Sim.</summary>
        internal bool SpecialWanted(bool dz)
        {
            var h = H; bool los = SpecialIsShot(h.Hero); var l = bufFoes; var us = M.Units;
            if (M.OneTap && h.Hero != HeroId.Moa)
            {   // one tap at a random enemy in reach (OneTap.cs): wanted with an enemy hero in that reach, or a minion pack of 3+ to
                // clear; the same rule on both teams and every level (the levels differ in how fast and how often, not in the brain)
                float r = Match.TapReach(h.Hero);
                if (h.Hero == HeroId.Fenn)
                {   // Shadow Veil (Heroes.cs): vanish next to an enemy hero to strike it (Omega's old AI went in on a hero), or to get away
                    // when hurt and chased; the brain then walks in and swings, or keeps running, as it would anyway
                    if (h.Has(St.Veil)) return false;
                    if ((retreat || danger) && HpR(h) < 0.45f) return Foes(5, true, false, l) > 0;
                    return !retreat && HpR(h) >= 0.3f && Foes(FennS.VeilEngage, true, false, l) > 0;
                }
                if (h.Hero == HeroId.Ravna && Match.NightFlightOn)
                {   // Night Flight goes the way she runs (Rickard 2026-11-12, no target needed): the brain flies at an enemy hero in the
                    // flock's reach, or away when hurt and chased; it then steers the flock as before (Hero8.cs RavnaSteer)
                    if ((retreat || danger) && HpR(h) < 0.35f) return Foes(5, true, false, l) > 0;
                    return !retreat && Foes(r, true, false, l) > 0;
                }
                // a long shot: Gold, Platinum and Ultra drag Knut's hook, Bram's hammer and Tyra's bolt at a hero past the white ring, as a
                // player may (bot audit 2026-11-13, Rickard "Se om det är fel på fler bottar som med Knut" ... "ja bygg ... den bot fixen":
                // the bots only tapped, so on Hard a hero stood in the long reach and out of the ring 3.6-4.9 s a minute with it ready)
                if (DragTier && M.DragAimOn && !retreat && DragHero(out bool far) != null && far) return true;
                if (!M.TapHasTarget(h)) return false;
                if (BeginnerSpam()) return true; // Easy: fired at the first thing in reach, minions too (BotBeginner.cs)
                // Kugg's Anchor roots him for up to 10 s (Hero7.cs): ROI17's old AI (SimAI AI_Joker) deployed above 60 % health with an
                // enemy hero within 7 tiles or 4 enemies in reach, never to escape. The bot waits for KuggAnchorStocks (he may anchor from
                // 2, but two shots never pay for ~3.4 s rooted: 16 anchors a match, 85 % with 2 stocks, win 15 %) and never anchors while
                // retreating or in danger, where "never to escape" had let him (full review 2026-11-09 nr 69)
                // ... and never with an enemy hero already on him (BotTables.KuggAnchorClear, hero review 2026-11-13)
                if (h.Hero == HeroId.Kugg) return !retreat && !danger && h.Charge >= BotTables.KuggAnchorStocks && HpR(h) >= 0.6f && Foes(BotTables.KuggAnchorClear, true, false, l) == 0 && (Foes(7, true, true, l) > 0 || h.Charge >= h.HDef.MaxCharge && Foes(r, false, true, l) >= 4); // a minion wave only with all 6 stocks
                // Knut's hook and Tyra's bolt fly past minions (Match.PassesMinions): a minion pack is no reason to throw them
                return SureShot(FoeList(r, true, true), dz) || (!Match.PassesMinions(h.Hero) && Foes(r, false, true, l) >= 3);
            }
            switch (h.Hero)
            {
                case HeroId.Tyra: // the volley on an enemy hero in her range; Gold and Platinum may aim the bolt instead (DragPlan, tried first)
                    return SureShot(FoeList(h.Range, true, los), dz);
                case HeroId.Bram: return SureShot(FoeList(BramS.Reach, true, los), dz);
                case HeroId.Knut: // the slam with an enemy hero well inside it (it has a short wind-up), or a minion pack; the hook is DragPlan's
                    return Foes(KnutS.SlamRadius - 0.4f, true, true, l) > 0 || Foes(KnutS.SlamRadius - 0.3f, false, true, l) >= 3;
                case HeroId.Tikta: return Foes(TiktaS.Reach, true, los, l) > 0;
                case HeroId.Blast: return Foes(BlastS.LaneLength, true, true, l) > 0 || Foes(BlastS.LaneLength, false, true, l) >= 4; // the tap strip needs a clear line
                case HeroId.Hilde: // the tower gets the big keg (DragPlan); else the Powder Burst on an enemy hero close by, or any in reach in danger
                    {
                        var tw = TowerOf(Teams.Other(h.Team));
                        if (tw != null && tw.Alive && Dist(h, tw) <= HildeS.Reach + tw.R) return true;
                        return Foes(2, true, los, l) > 0 || dz && Foes(HildeS.Reach, true, los, l) > 0;
                    }
                case HeroId.Bonk: // the slam when an enemy hero stands well inside it (it has a short wind-up), or a minion pack
                    return Foes(BonkS.SlamRadius - 0.4f, true, los, l) > 0 || Foes(BonkS.SlamRadius - 0.3f, false, true, l) >= 3;
                case HeroId.Oaken: // Speltest SimHeroes6.cs: the slam with an enemy hero in its reach; Gold and Platinum also quake one further off (DragPlan)
                    return Foes(OakenS.Reach, true, los, l) > 0 || DragTier && Foes(OakenS.QuakeRange - 0.2f, true, los, l) > 0;
                case HeroId.Fenn:
                    {
                        if (Foes(4, true, los, l) > 0) return true;
                        var tw = TowerOf(Teams.Other(h.Team)); if (tw == null || !tw.Alive || Dist(h, tw) > h.Range + tw.R + 1) return false;
                        for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.IsMinionLike && u.Team == h.Team && Dist(u, tw) < tw.UDef.Range + 1) return false; }
                        return true; // OneTap off (tests only): the old wolf's rule, kept as it was (the wolf itself is gone since 2026-11-13, nr 116)
                    }
                case HeroId.Moa:
                    {
                        float r = MoaS.Range;
                        for (int i = 0; i < us.Count; i++)
                        {
                            var a = us[i];
                            if (a.Kind != UnitKind.Hero || !a.Alive || a.Team != h.Team || a.IsClone || Dist(a, h) > r + a.R || HpR(a) >= 0.5f) continue;
                            for (int j = 0; j < us.Count; j++) { var e = us[j]; if (e.Kind == UnitKind.Hero && e.Alive && e.Team != h.Team && Sees(e) && Dist(e, a) < 6) return true; }
                        }
                        return dz;
                    }
            }
            return false;
        }
        List<Unit> FoeList(float r, bool heroesOnly, bool los) { Foes(r, heroesOnly, los, bufFoes); return bufFoes; }

        // ---------- the drag forms (old SimBotDrag.cs) ----------
        /// <summary>Enemy heroes (clones too) the team sees within r of c, not untargetable.</summary>
        List<Unit> FoeHeroes(Unit c, float r)
        {
            var h = H; var l = bufHeroes; l.Clear(); var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind == UnitKind.Hero && u.Alive && u.Team != h.Team && !u.Has(St.Untargetable) && Sees(u) && Dist(u, c) <= r + u.R) l.Add(u);
            }
            return l;
        }
        float AngTo(float x, float y) => SimMath.Atan2(y - H.Y, x - H.X);

        /// <summary>No enemy minion or hero (other than t) close to the line from h to t, and no wall: a line form would stop on it.
        /// heroesOnly: only heroes block (Knut's hook flies past minions).</summary>
        bool LineFree(Unit t, float w, bool heroesOnly = false)
        {
            var h = H;
            if (M.Map.ShotBlocked(h.X, h.Y, t.X, t.Y)) return false;
            float L = Dist(h, t); if (L < 0.01f) return true;
            float cx = (t.X - h.X) / L, cy = (t.Y - h.Y) / L; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u == t || !u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower || (heroesOnly && u.Kind != UnitKind.Hero) || u.Has(St.Untargetable) || !Sees(u)) continue; // only what the team sees
                float dx = u.X - h.X, dy = u.Y - h.Y, along = dx * cx + dy * cy, perp = Math.Abs(-dx * cy + dy * cx);
                if (along > 0 && along < L && perp < w + u.R) return false;
            }
            return true;
        }

        /// <summary>Enemies (not towers) in Blast's lane from h along ang.</summary>
        int LaneCount(float ang, float len, float hw)
        {
            var h = H; float ex = h.X + SimMath.Cos(ang) * len, ey = h.Y + SimMath.Sin(ang) * len; int n = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (!u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || !Sees(u) || SimMath.PtSeg(u.X, u.Y, h.X, h.Y, ex, ey) > hw + u.R) continue;
                n++;
            }
            return n;
        }

        /// <summary>Should this bot cast its drag form now, and where: ang = map direction, k = 0..1 of the drag reach.</summary>
        bool DragPlan(bool dz, out float ang, out float k)
        {
            ang = float.NaN; k = 1;
            if (M.OneTap) return false; // no drag forms: the tap aims by itself (OneTap.cs)
            var h = H;
            if (h.Hero == HeroId.Hilde)
            {   // every tier (XYZ SimBotDrag.cs, 2026-10-25): the big keg on the enemy tower in reach, the only form that hurts it
                var tw = TowerOf(Teams.Other(h.Team));
                if (tw != null && tw.Alive && Dist(h, tw) <= HildeS.Reach + tw.R) { ang = AngTo(tw.X, tw.Y); k = SimMath.Clamp(Dist(h, tw) / HildeS.Reach, 0, 1); return true; }
                return false; // else the tap, as in XYZ (its bots never aim the push)
            }
            if (h.Hero == HeroId.Knut)
            {   // every tier (the tap is no hook since 2026-10-28): pull a hurt enemy hero out of the slam's reach to him, with a clear
                // line (XYZ SimBotDrag.cs, where the tap was Anchor Leap); with someone in slam reach the slam is better
                if (dz || FoeHeroes(h, KnutS.SlamRadius - 0.4f).Count > 0) return false;
                var hs = FoeHeroes(h, KnutS.HookDistance - 0.5f); Unit t = null;
                for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (HpR(e) < 0.6f && LineFree(e, KnutS.HookRadius, KnutS.HookPassesMinions) && (t == null || e.Hp < t.Hp)) t = e; }
                if (t == null) return false;
                M.LeadAim(h, t, Match.Flight.Linear, 0, KnutS.OutSpeed, out float ax, out float ay); // a steady flight since 2026-11-02
                ang = AngTo(ax, ay); return true;
            }
            if (!DragTier) return false;
            switch (h.Hero)
            {
                case HeroId.Tyra:
                    {   // the bolt at the lowest enemy hero with no minion in front (it stops in the first enemy)
                        var hs = FoeHeroes(h, TyraS.Length - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if ((TyraS.BoltPierces || LineFree(e, TyraS.Hitbox, TyraS.BoltPassesMinions)) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
                        // a second enemy hero in the volley's cone round that one: the tap hits both, so tap instead (XYZ SimBotDrag.cs)
                        float ta = AngTo(t.X, t.Y); var vs = FoeHeroes(h, h.Range);
                        for (int i = 0; i < vs.Count; i++)
                        {
                            var e = vs[i]; float da = AngTo(e.X, e.Y) - ta;
                            if (e != t && Math.Abs(SimMath.Atan2(SimMath.Sin(da), SimMath.Cos(da))) <= TyraS.VolleyHalfAngle) return false;
                        }
                        M.LeadAim(h, t, Match.Flight.Linear, TyraS.Windup, TyraS.Speed, out float ax, out float ay);
                        ang = AngTo(ax, ay); return true;
                    }
                case HeroId.Moa:
                    {   // the area shield when two or more allies stand together in a fight and none is low
                        float range = MoaS.Range, ar = MoaS.AreaRadius; var allies = bufAllies; allies.Clear(); var us = M.Units;
                        for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a.Kind == UnitKind.Hero && a.Alive && a.Team == h.Team && !a.IsClone && Dist(a, h) <= range + a.R) allies.Add(a); }
                        for (int i = 0; i < allies.Count; i++) if (HpR(allies[i]) < 0.35f) return false; // one big shield on the low one is better
                        Unit best = null; int bestN = 1;
                        for (int i = 0; i < allies.Count; i++)
                        {
                            var c = allies[i]; int n = 0;
                            for (int j = 0; j < allies.Count; j++) if (Dist(allies[j], c) <= ar + allies[j].R - 0.2f) n++;
                            if (n > bestN && FoeHeroes(c, 6).Count > 0) { bestN = n; best = c; }
                        }
                        if (best == null) return false;
                        ang = AngTo(best.X, best.Y); k = SimMath.Clamp(Dist(h, best) / range, 0, 1); return true;
                    }
                case HeroId.Fenn: return false; // Shadow Veil has nothing to aim (SpecialWanted taps it)
                case HeroId.Bram:
                    {   if (Match.SilverOn) return false; // the Hammer Line is tapped at a random enemy (SpecialWanted)
                        // Anvil Drop on an enemy hero in reach with another enemy hero next to it, so the stun lands on two (XYZ SimBotDrag.cs)
                        float reach = BramS.Reach, r = BramS.DropRadius; var os = FoeHeroes(h, reach + r); // FoeHeroes refills one list: no second call below
                        for (int i = 0; i < os.Count; i++)
                        {
                            var e = os[i]; if (Dist(h, e) > reach - 0.3f + e.R) continue;
                            int n = 0;
                            for (int j = 0; j < os.Count; j++) if (os[j] != e && Dist(os[j], e) <= r * 1.6f) n++;
                            if (n == 0) continue;
                            M.LeadAim(h, e, Match.Flight.Const, BramS.DropDelay, 0, out float ax, out float ay);
                            ang = AngTo(ax, ay); k = SimMath.Clamp(Hyp(ax - h.X, ay - h.Y) / reach, 0, 1); return true;
                        }
                        return false;
                    }
                case HeroId.Tikta:
                    {
                        if (FoeHeroes(h, TiktaS.Reach).Count > 0) return false; // the tap storm reaches someone: that is better
                        var hs = FoeHeroes(h, TiktaS.LineReach - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (LineFree(e, 0.2f, TiktaS.NeedlesPassMinions) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
                        M.LeadAim(h, t, Match.Flight.Linear, 0, TiktaS.Speed, out float ax, out float ay);
                        ang = AngTo(ax, ay); return true;
                    }
                case HeroId.Bonk:
                    {   // the boulder at the lowest enemy hero out of slam reach with nothing in front (it stops in the first enemy)
                        if (FoeHeroes(h, BonkS.SlamRadius - 0.4f).Count > 0) return false;
                        var hs = FoeHeroes(h, BonkS.BoulderReach - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (LineFree(e, BonkS.BoulderHitbox) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
                        M.LeadAim(h, t, Match.Flight.Linear, BonkS.BoulderWindup, BonkS.BoulderSpeed, out float ax, out float ay);
                        ang = AngTo(ax, ay); return true;
                    }
                case HeroId.Oaken:
                    {   // Speltest SimBotDrag.cs (Oaken 3.0): the quake when its line covers more enemies than one, or when no enemy hero is
                        // close enough for the slam (then at the lowest one in the quake's reach)
                        float len = OakenS.QuakeRange, hw = OakenS.QuakeHalfWidth; int best = 1; Unit far = null;
                        bool slamHits = FoeHeroes(h, OakenS.SlamRadius).Count > 0;
                        var hs = FoeHeroes(h, len - 0.2f);
                        for (int i = 0; i < hs.Count; i++)
                        {
                            var e = hs[i];
                            if (M.Map.ShotBlocked(h.X, h.Y, e.X, e.Y)) continue;
                            float a = AngTo(e.X, e.Y); int n = LaneCount(a, M.Map.LaneReach(h.X, h.Y, a, len), hw);
                            if (n > best) { best = n; ang = a; }
                            if (far == null || e.Hp < far.Hp) far = e;
                        }
                        if (float.IsNaN(ang) && !slamHits && far != null) ang = AngTo(far.X, far.Y);
                        return !float.IsNaN(ang);
                    }
                case HeroId.Blast:
                    {   // Supernova (the drag since 2026-10-30) on the enemy hero in reach with the most other enemies around it, led by its fuse
                        float reach = BlastS.Reach, r = BlastS.Radius; var os = FoeHeroes(h, reach + r); var us = M.Units; Unit t = null; int best = -1;
                        for (int i = 0; i < os.Count; i++)
                        {
                            var e = os[i]; if (Dist(h, e) > reach - 0.3f + e.R) continue;
                            int n = 0;
                            for (int j = 0; j < us.Count; j++)
                            {
                                var u = us[j];
                                if (u != e && u.Alive && u.Team != h.Team && u.Kind != UnitKind.Tower && Sees(u) && Dist(u, e) <= r) n++;
                            }
                            if (n > best || n == best && e.Hp < t.Hp) { best = n; t = e; }
                        }
                        if (t == null) return false;
                        M.LeadAim(h, t, Match.Flight.Const, BlastS.Delay, 0, out float ax, out float ay);
                        ang = AngTo(ax, ay); k = SimMath.Clamp(Hyp(ax - h.X, ay - h.Y) / reach, 0, 1); return true;
                    }
            }
            return false;
        }

        // ---------- the one-tap game's drag (bot audit 2026-11-13) ----------
        // A player may drag SPECIAL to aim the one special by hand (Rules.DragAimSpecial, protocol 58); until this audit the bots only
        // tapped, at a random enemy in the white ring. So they never threw Knut's hook, Bram's hammer or Tyra's bolt at a hero past the
        // ring, though the forms fly 7, 8 and 9 tiles; spent 20-42 % of their casts on a minion when they wanted the special for a hero
        // (Ragn worst: as many leaps on minions as on heroes); and Moa put her field on a random ally, reaching the most hurt one 78 %
        // of the time. Gold, Platinum and Ultra now drag as a player who aims would; Bronze and Silver (Easy, Normal) tap as before.
        // Both teams alike (BOTS SAME BRAIN). Hard bots, 2400 matches: Ragn's leaps on heroes 9 200 -> 13 200, Moa's field on the most hurt
        // ally 78 -> 91 %, Tyra 51 -> 61 % (her long bolt; 62 % in 9600 matches with the whole review), the others within 5 points.

        /// <summary>How far a drag sends h's special at a hero: Knut's hook, Bram's hammer and Tyra's bolt their own length; the others stop
        /// at the white ring, as the tap (OneTap.DragRing, DragShare).</summary>
        static float LongReach(HeroId id) => id == HeroId.Knut ? KnutS.HookDistance : id == HeroId.Bram && Match.SilverOn ? BramS.LineReach : id == HeroId.Tyra ? TyraS.Length : Match.TapReach(id);

        /// <summary>The enemy hero a Gold/Platinum/Ultra bot drags its special at: the lowest one the form reaches (half a tile short of a
        /// long form's end) with a clear line, a straight form with no base and nothing in front that stops it (Bram's hammer flies through
        /// every enemy); far = past the white ring. Null when none.</summary>
        Unit DragHero(out bool far)
        {
            var h = H; far = false; float ring = Match.TapReach(h.Hero), reach = Math.Max(ring, LongReach(h.Hero) - 0.5f);
            bool line = h.Hero == HeroId.Knut || h.Hero == HeroId.Bram || h.Hero == HeroId.Tyra || h.Hero == HeroId.Tikta;
            float w = h.Hero == HeroId.Knut ? KnutS.HookRadius : h.Hero == HeroId.Bram ? BramS.LineWidth * 0.5f : h.Hero == HeroId.Tyra ? TyraS.Hitbox : TiktaS.LineHitbox;
            var hs = FoeHeroes(h, reach); Unit t = null;
            for (int i = 0; i < hs.Count; i++)
            {
                var e = hs[i];
                if (!M.Map.LosClear(h, e)) continue;
                if (!line && Dist(h, e) > ring + e.R) continue; // a placed form lands at most at the white ring
                if (line && (M.TowerInWay(h, e, w) || h.Hero != HeroId.Bram && !LineFree(e, w, Match.PassesMinions(h.Hero)))) continue;
                if (t == null || e.Hp < t.Hp) t = e;
            }
            if (t != null) far = Dist(h, t) > ring + t.R;
            return t;
        }

        /// <summary>A Gold/Platinum/Ultra bot's drag of the one special: at the lowest enemy hero the form reaches (DragHero), led where it
        /// lands later (Knut's hook and Tyra's bolt by their flight, Tikta's needles too, Blast's bomb by its 1 s, Nässla's thicket till it
        /// roots, Ragn's leap by its warning, as his tap); Bram's hammer straight at the hero (a led line got walked into and stepped out
        /// of by Hard bots, hero review 2026-11-13); Moa's field onto the most hurt ally hero in her ring. False: the tap. Internal for Tests/Sim.</summary>
        internal bool OneTapDrag(out float ang, out float k)
        {
            ang = float.NaN; k = 1; var h = H;
            if (!DragTier || !M.DragAimOn || !Match.CanDragAim(h.Hero)) return false;
            float ring = Math.Max(0.01f, M.DragRing(h.Hero)), tele = M.TeleOn ? Match.TeleTime(h.Hero) : 0;
            if (h.Hero == HeroId.Moa)
            {
                var a = M.MostHurtAlly(h, Match.TapReach(HeroId.Moa));
                if (a == null) return false;
                if (a == h) { ang = h.Facing; k = 0; return true; } // her own spot
                ang = AngTo(a.X, a.Y); k = SimMath.Clamp(Dist(h, a) / ring, 0, 1); return true;
            }
            var t = DragHero(out _);
            if (t == null) return false;
            float ax = t.X, ay = t.Y;
            switch (h.Hero)
            {
                case HeroId.Knut: M.LeadAim(h, t, Match.Flight.Linear, tele, KnutS.OutSpeed, out ax, out ay); break;
                case HeroId.Tyra: M.LeadAim(h, t, Match.Flight.Linear, tele + TyraS.Windup, TyraS.Speed, out ax, out ay); break;
                case HeroId.Tikta: M.LeadAim(h, t, Match.Flight.Linear, tele, TiktaS.Speed, out ax, out ay); break;
                case HeroId.Blast: M.LeadAim(h, t, Match.Flight.Const, tele + BlastS.Delay, 0, out ax, out ay); break;
                case HeroId.Nassla: M.LeadAim(h, t, Match.Flight.Const, tele + NasslaS.Warn, 0, out ax, out ay); break;
                case HeroId.Ragn: M.LeadAim(h, t, Match.Flight.Const, tele, 0, out ax, out ay); break;
            }
            ang = AngTo(ax, ay); k = SimMath.Clamp(Hyp(ax - h.X, ay - h.Y) / ring, 0, 1);
            return true;
        }

        /// <summary>The bot's cast: the drag form when it is the better play (and works), else the tap.</summary>
        bool BotCast(bool dz)
        {
            if (M.OneTap)
            {   // the one special: Gold, Platinum and Ultra drag it by hand where a player would (OneTapDrag), else the tap, as every level
                if (OneTapDrag(out float da, out float dk) && M.TryAimSpecial(H, da, dk)) return true;
                return M.TryTapSpecial(H);
            }
            if (DragPlan(dz, out float a, out float k) && M.TryCastSpecial(H, a, k)) return true;
            return M.TryCastSpecial(H, float.NaN, 0);
        }

        // ---------- dodging ----------
        // one roll per enemy shot or warning, kept by its serial: does this bot dodge it, from when, for how long
        struct DodgeRec { public int Serial; public bool Yes; public float At, Len; }
        readonly DodgeRec[] dodges = new DodgeRec[64];
        int dodgeNext;

        bool DodgeRoll(Proj p, float chance, out DodgeRec rec)
        {
            for (int i = 0; i < dodges.Length; i++) if (dodges[i].Serial == p.Serial) { rec = dodges[i]; return rec.Yes; }
            rec = new DodgeRec { Serial = p.Serial };
            if (Srnd() < chance)
            {
                float react = BotTables.DodgeReact[tierI] * Srand(0.8f, 1.25f) * (v3 ? TK3(24) : 1);
                rec.Yes = true; rec.At = Now + Math.Max(react, BotTables.DodgeReactMin[tierI]); rec.Len = BotTables.DodgeStep[tierI] * Srand(0.8f, 1.25f);
            }
            dodges[dodgeNext] = rec; dodgeNext = (dodgeNext + 1) % dodges.Length;
            return rec.Yes;
        }

        /// <summary>The way out of every enemy shot line, bomb circle, Nässla's thicket and red warning the bot stands in (false = none).</summary>
        bool DodgeVector(out float vx, out float vy)
        {
            var h = H; vx = vy = 0;
            float R = Tier.ProjAvoid;
            Temperament();
            float chance = SimMath.Clamp(BotTables.DodgeChance[tierI] + 0.1f * Skill + (v3 ? TK3(6) : 0), 0, tierI >= 4 ? 1 : BotTables.DodgeChanceMax);
            var ps = M.Projs;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p.Done || p.Src == null || p.Team == h.Team) continue;
                bool warn = p.IsWarning || p.Mode == ProjMode.Smash, line = (p.Mode == ProjMode.Hook && p.Out) || p.Mode == ProjMode.Bolt || p.Mode == ProjMode.Hammer || p.Mode == ProjMode.HammerLine || p.Mode == ProjMode.Boulder, bomb = p.Mode == ProjMode.Bomb || p.Mode == ProjMode.Keg || p.Mode == ProjMode.Bramble;
                // Nässla's thicket too, like a bomb's circle: until the hero review of 2026-11-13 bots stood in it for all four ticks,
                // so her special looked much stronger in bot matches than against a player who walks out
                if (!warn && !line && !bomb) continue;
                if (warn && M.CastHiddenFrom(p, h.Team)) continue; // a hidden Tikta's strip is not seen (TIKTA-05)
                if (!DodgeRoll(p, chance, out var dg) || Now < dg.At || Now > dg.At + dg.Len) continue;
                if (warn) { TelegraphDodge(p, ref vx, ref vy); continue; }
                if (line)
                {
                    float cx = SimMath.Cos(p.Ang), cy = SimMath.Sin(p.Ang), dx = h.X - p.X, dy = h.Y - p.Y;
                    float along = dx * cx + dy * cy, perp = -dx * cy + dy * cx;
                    if (along > -0.3f && along < R && Math.Abs(perp) < p.Radius + h.R + 0.35f)
                    {
                        float side = perp >= 0 ? 1 : -1;
                        if (!Walkable(h, h.X - cy * side * 0.6f, h.Y + cx * side * 0.6f)) side = -side; // not onto a pad or into a tower (nr 87)
                        vx += -cy * side; vy += cx * side;
                    }
                }
                else
                {
                    float d = Hyp(h.X - p.X, h.Y - p.Y);
                    if (d < p.Radius + h.R + 0.3f && d < R) { vx += (h.X - p.X) / (d != 0 ? d : 1); vy += (h.Y - p.Y) / (d != 0 ? d : 1); }
                }
            }
            return vx != 0 || vy != 0;
        }

        /// <summary>A red warning the bot stands in: step away from its centre, or across the line.</summary>
        void TelegraphDodge(Proj p, ref float vx, ref float vy)
        {
            var h = H; float pad = h.R + 0.3f;
            if (p.Mode == ProjMode.WarnCircle || p.Mode == ProjMode.Smash)
            {
                float d = Hyp(h.X - p.X, h.Y - p.Y);
                if (d >= p.Radius + pad) return;
                vx += (h.X - p.X) / (d != 0 ? d : 1); vy += (h.Y - p.Y) / (d != 0 ? d : 1);
                return;
            }
            float cx = SimMath.Cos(p.Ang), cy = SimMath.Sin(p.Ang), dx = h.X - p.X, dy = h.Y - p.Y, along = dx * cx + dy * cy, perp = -dx * cy + dy * cx;
            if (along < -0.3f || along > p.D + 0.3f || Math.Abs(perp) > p.Radius + pad) return;
            float side = perp >= 0 ? 1 : -1;
            if (!Walkable(h, h.X - cy * side * 0.6f, h.Y + cx * side * 0.6f)) side = -side; // not onto a pad or into a tower (nr 87)
            vx += -cy * side; vy += cx * side;
        }
    }
}
