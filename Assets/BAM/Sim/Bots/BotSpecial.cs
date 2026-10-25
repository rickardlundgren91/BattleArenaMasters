using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // When a bot fires its special, tap or drag, and how it steps out of shots and red warnings (old SimAI.cs aiSpecialWanted,
    // sureShot, dodgeVector; SimHeroes6.cs newHeroSpecialWanted; SimSpecials2.cs v2Wanted; SimBotDrag.cs).
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
        static bool SpecialIsShot(HeroId id) => id == HeroId.Bram || id == HeroId.Knut || id == HeroId.Tikta || id == HeroId.Tyra;

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

        /// <summary>Does the bot want its special now (old newHeroSpecialWanted with v2Wanted for Tyra)?</summary>
        bool SpecialWanted(bool dz)
        {
            var h = H; bool los = SpecialIsShot(h.Hero); var l = bufFoes; var us = M.Units;
            switch (h.Hero)
            {
                case HeroId.Tyra: // Gold and Platinum aim the bolt past the minions (DragPlan); they still tap in danger
                    return DragTier && !dz ? false : SureShot(FoeList(h.Range, true, los), dz);
                case HeroId.Bram: return SureShot(FoeList(BramS.Reach, true, los), dz);
                case HeroId.Knut: return SureShot(FoeList(KnutS.TapReach, true, los), dz);
                case HeroId.Tikta: return Foes(TiktaS.Reach, true, los, l) > 0;
                case HeroId.Blast: return Foes(BlastS.Reach, true, los, l) > 0 || Foes(BlastS.Reach, false, false, l) >= 4;
                case HeroId.Fenn:
                    {
                        if (Foes(4, true, los, l) > 0) return true;
                        var tw = TowerOf(Teams.Other(h.Team)); if (tw == null || !tw.Alive || Dist(h, tw) > h.Range + tw.R + 1) return false;
                        for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Alive && u.IsMinionLike && u.Team == h.Team && Dist(u, tw) < tw.UDef.Range + 1) return false; }
                        return true; // the wolf tanks the tower for him
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

        /// <summary>No enemy minion or hero (other than t) close to the line from h to t, and no wall: a line form would stop on it.</summary>
        bool LineFree(Unit t, float w)
        {
            var h = H;
            if (M.Map.ShotBlocked(h.X, h.Y, t.X, t.Y)) return false;
            float L = Dist(h, t); if (L < 0.01f) return true;
            float cx = (t.X - h.X) / L, cy = (t.Y - h.Y) / L; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u == t || !u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable)) continue;
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
                if (!u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || SimMath.PtSeg(u.X, u.Y, h.X, h.Y, ex, ey) > hw + u.R) continue;
                n++;
            }
            return n;
        }

        /// <summary>Should this bot cast its drag form now, and where: ang = map direction, k = 0..1 of the drag reach.</summary>
        bool DragPlan(bool dz, out float ang, out float k)
        {
            ang = float.NaN; k = 1;
            var h = H;
            if (!DragTier) return false;
            switch (h.Hero)
            {
                case HeroId.Tyra:
                    {   // the bolt at the lowest enemy hero with no minion in front (it stops in the first enemy)
                        var hs = FoeHeroes(h, TyraS.Length - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (LineFree(e, TyraS.Hitbox) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
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
                case HeroId.Fenn:
                    {   // the thrown wolf homes on an enemy hero but walls stop it (Rickard 2026-10-25), so throw it at the lowest
                        // enemy hero within the throw that has no wall in between
                        float reach = FennS.ThrowReach; var hs = FoeHeroes(h, reach - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (!M.Map.ShotBlocked(h.X, h.Y, e.X, e.Y) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
                        ang = AngTo(t.X, t.Y); k = SimMath.Clamp(Dist(h, t) / reach, 0, 1); return true;
                    }
                case HeroId.Tikta:
                    {
                        if (FoeHeroes(h, TiktaS.Reach).Count > 0) return false; // the tap storm reaches someone: that is better
                        var hs = FoeHeroes(h, TiktaS.LineReach - 0.3f); Unit t = null;
                        for (int i = 0; i < hs.Count; i++) { var e = hs[i]; if (LineFree(e, 0.2f) && (t == null || e.Hp < t.Hp)) t = e; }
                        if (t == null) return false;
                        M.LeadAim(h, t, Match.Flight.Linear, 0, TiktaS.Speed, out float ax, out float ay);
                        ang = AngTo(ax, ay); return true;
                    }
                case HeroId.Blast:
                    {   // Inferno Lane through an enemy hero when the strip also covers two more enemies
                        float len = BlastS.LaneLength, hw = BlastS.LaneHalfWidth; int best = 2; var hs = FoeHeroes(h, len - 0.3f);
                        for (int i = 0; i < hs.Count; i++)
                        {
                            var e = hs[i];
                            if (M.Map.ShotBlocked(h.X, h.Y, e.X, e.Y)) continue;
                            float a = AngTo(e.X, e.Y); int n = LaneCount(a, M.Map.LaneReach(h.X, h.Y, a, len), hw);
                            if (n > best) { best = n; ang = a; }
                        }
                        return !float.IsNaN(ang);
                    }
            }
            return false;
        }

        /// <summary>The bot's cast: the drag form when it is the better play (and works), else the tap.</summary>
        bool BotCast(bool dz)
        {
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

        /// <summary>The way out of every enemy shot line, bomb circle and red warning the bot stands in (false = none).</summary>
        bool DodgeVector(out float vx, out float vy)
        {
            var h = H; vx = vy = 0;
            float R = Tier.ProjAvoid;
            Temperament();
            float chance = SimMath.Clamp(BotTables.DodgeChance[tierI] + 0.1f * Skill + (v3 ? TK3(6) : 0), 0, BotTables.DodgeChanceMax);
            var ps = M.Projs;
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                if (p.Done || p.Src == null || p.Team == h.Team) continue;
                bool warn = p.IsWarning, line = (p.Mode == ProjMode.Hook && p.Out) || p.Mode == ProjMode.Bolt || p.Mode == ProjMode.Hammer || p.Mode == ProjMode.HammerLine, bomb = p.Mode == ProjMode.Bomb;
                if (!warn && !line && !bomb) continue;
                if (!DodgeRoll(p, chance, out var dg) || Now < dg.At || Now > dg.At + dg.Len) continue;
                if (warn) { TelegraphDodge(p, ref vx, ref vy); continue; }
                if (line)
                {
                    float cx = SimMath.Cos(p.Ang), cy = SimMath.Sin(p.Ang), dx = h.X - p.X, dy = h.Y - p.Y;
                    float along = dx * cx + dy * cy, perp = -dx * cy + dy * cx;
                    if (along > -0.3f && along < R && Math.Abs(perp) < p.Radius + h.R + 0.35f)
                    {
                        float side = perp >= 0 ? 1 : -1;
                        if (!M.Map.Free(h.X - cy * side * 0.6f, h.Y + cx * side * 0.6f, h.R)) side = -side;
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
            if (p.Mode == ProjMode.WarnCircle)
            {
                float d = Hyp(h.X - p.X, h.Y - p.Y);
                if (d >= p.Radius + pad) return;
                vx += (h.X - p.X) / (d != 0 ? d : 1); vy += (h.Y - p.Y) / (d != 0 ? d : 1);
                return;
            }
            float cx = SimMath.Cos(p.Ang), cy = SimMath.Sin(p.Ang), dx = h.X - p.X, dy = h.Y - p.Y, along = dx * cx + dy * cy, perp = -dx * cy + dy * cx;
            if (along < -0.3f || along > p.D + 0.3f || Math.Abs(perp) > p.Radius + pad) return;
            float side = perp >= 0 ? 1 : -1;
            if (!M.Map.Free(h.X - cy * side * 0.6f, h.Y + cx * side * 0.6f, h.R)) side = -side;
            vx += -cy * side; vy += cx * side;
        }
    }
}
