using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // What a bot reads from the match: speeds, damage per second, what its team sees and remembers, the danger at a spot
    // (old SimAI.cs incomingDps/ttkAt, SimThreat.cs, SimItems.cs nearEnemyTower). Nothing here changes the match.
    public sealed partial class BotBrain
    {
        // ---------- small readers ----------
        Unit TowerOf(int team) => M.Towers[team];
        static float Dist(Unit a, Unit b) => SimMath.Dist(a, b);
        static float Hyp(float x, float y) => SimMath.Hypot(x, y);
        /// <summary>The bot's team sees e and has had time to notice it (BotFair.cs). The rules' own visibility is M.IsVisibleTo.</summary>
        bool Sees(Unit e) => AiSees(e);
        float Now => M.Time;

        /// <summary>Old speedOf: (base + flat) x (1 + pct), clamped to 0.5..6; towers 0.</summary>
        internal static float SpeedOf(Unit u)
        {
            if (u.Kind == UnitKind.Tower) return 0;
            float flat = 0, pct = 0;
            for (int i = 0; i < u.StCount; i++)
            {
                var s = u.Sts[i];
                if (s.Type == St.SlowFlat || s.Type == St.SpeedFlat) flat += s.V;
                else if (s.Type == St.SlowPct || s.Type == St.SpeedPct) pct += s.V;
            }
            return SimMath.Clamp((u.BaseSpeed + flat) * (1 + pct), 0.5f, 6);
        }

        /// <summary>The damage per second u looks like it deals: Fenn's decoy looks like Fenn (a person cannot tell them apart).</summary>
        internal static float DpsOf(Unit u)
        {
            if (u.Kind != UnitKind.Hero) return u.DefDmg / u.UDef.Cd;
            var s = u.IsClone && u.Owner != null ? u.Owner : u;
            return s.StatDmg() / s.StatCd();
        }
        static float ShieldOf(Unit u) => u.ShieldAmount;
        static float HpR(Unit u) => u.Hp / u.MaxHp;

        /// <summary>Where team last saw e, if within the last 4 s (old remembered()).</summary>
        bool Remembered(Unit e, int team, out float x, out float y)
        {
            x = e.LastSeenX[team]; y = e.LastSeenY[team];
            return Now - e.LastSeenT[team] < BotTables.MemoryT;
        }
        bool RemNear(Unit u, int team, float px, float py, float r) => Remembered(u, team, out float x, out float y) && Hyp(x - px, y - py) < r;
        bool RemOrUnknownNear(Unit u, int team, float px, float py, float r) => !Remembered(u, team, out float x, out float y) || Hyp(x - px, y - py) < r;

        /// <summary>How sure h's team is that e just used its special (old spentCert).</summary>
        float SpentCert(Unit h, Unit e)
        {
            float t = e.CastSeen[h.Team];
            if (float.IsNaN(t)) return 0;
            float age = Now - t;
            return age < 3 ? 1 : age < 8 ? 1 - (age - 3) / 5 : 0;
        }

        /// <summary>Enemy heroes of h that h's team neither sees nor remembers (old unaccounted).</summary>
        int Unaccounted(Unit h, Unit e)
        {
            int n = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind == UnitKind.Hero && u.Alive && u != e && u.Team == e.Team && !u.IsClone && !Sees(u) && !Remembered(u, h.Team, out _, out _)) n++;
            }
            return n;
        }

        /// <summary>Living heroes of team ofTeam (not except, no clones) that team byTeam sees within r of (px, py), or remembers there.</summary>
        int CountSeenNear(int ofTeam, int byTeam, Unit except, float px, float py, float r)
        {
            int n = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var o = us[i];
                if (o.Kind != UnitKind.Hero || !o.Alive || o == except || o.Team != ofTeam || o.IsClone) continue;
                if (M.IsVisibleTo(o, byTeam) ? Hyp(o.X - px, o.Y - py) < r : RemNear(o, byTeam, px, py, r)) n++;
            }
            return n;
        }

        /// <summary>Living teammates of h (no clones) within r, into the list.</summary>
        List<Unit> MatesWithin(Unit h, float r, List<Unit> into)
        {
            into.Clear();
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var a = us[i];
                if (a.Kind == UnitKind.Hero && a.Alive && a != h && a.Team == h.Team && !a.IsClone && Dist(a, h) < r) into.Add(a);
            }
            return into;
        }

        static Unit Nearest(List<Unit> l, Unit from) => l.Count == 0 ? null : Match.Nearest(l, from.X, from.Y);

        // ---------- towers ----------
        float TowerRange => Defs.Tower.Range;
        bool NearEnemyTower(Unit u, float extra) { var t = TowerOf(Teams.Other(u.Team)); return t != null && t.Alive && Dist(t, u) <= t.UDef.Range + extra; }
        bool NearEnemyTowerAt(float x, float y, int team, float extra) { var t = TowerOf(Teams.Other(team)); return t != null && t.Alive && Hyp(t.X - x, t.Y - y) <= t.UDef.Range + extra; }

        /// <summary>Health of team's minions standing in the tower's range (they draw its fire).</summary>
        float TowerTank(Unit tower, int team)
        {
            if (tower == null || !tower.Alive) return 0;
            float a = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Alive && u.IsMinionLike && u.Team == team && Dist(u, tower) < tower.UDef.Range + u.R - 0.15f) a += u.Hp;
            }
            return a;
        }

        /// <summary>What an enemy tower's next shots at h deal per second, with the shots-in-a-row ramp.</summary>
        float TowerDpsOn(Unit tw, Unit h)
        {
            float k = tw.TowerFocus == h && Now - tw.TowerShotT < Rules.TowerRampReset ? tw.TowerStreak + 1 : 0;
            return DpsOf(tw) * Rules.TowerVsHero * M.TowerLateMult(h) * (1 + Math.Min(Rules.TowerRampMax, Rules.TowerRamp * (k + 1.5f)));
        }

        bool TowerBlocks(Unit h, float x, float y)
        {
            for (int t = 0; t < 2; t++) { var tw = TowerOf(t); if (tw != null && tw.Alive && Hyp(tw.X - x, tw.Y - y) < tw.R + h.R + 0.1f) return true; }
            return false;
        }
        bool Walkable(Unit h, float x, float y) => M.Map.Free(x, y, h.R) && !TowerBlocks(h, x, y);
        bool ClearWalk(Unit h, float px, float py)
        {
            float d = Hyp(px - h.X, py - h.Y); int n = (int)Math.Ceiling(d / 0.3f);
            for (int i = 1; i <= n; i++)
            {
                float x = h.X + (px - h.X) * i / n, y = h.Y + (py - h.Y) * i / n;
                if (!M.Map.Free(x, y, h.R) || TowerBlocks(h, x, y)) return false;
            }
            return true;
        }

        // ---------- the danger at a spot ----------
        /// <summary>Old specialReach: how far e's special reaches (Fenn and Moa: their attack range).</summary>
        static float SpecialReach(Unit e)
        {
            switch (e.Hero)
            {
                case HeroId.Bram: return BramS.Reach;
                case HeroId.Tyra: return TyraS.Length;
                case HeroId.Knut: return KnutS.HookDistance;
                case HeroId.Tikta: return TiktaS.Reach;
                case HeroId.Blast: return BlastS.Reach;
                case HeroId.Bonk: return BonkS.BoulderReach;
                case HeroId.Hilde: return HildeS.Reach;
                case HeroId.Oaken: return OakenS.Reach; // Speltest SimAI: the slam's reach
                default: return e.Range;
            }
        }

        /// <summary>Other units of h's side (not towers) that e could hit instead of h.</summary>
        int ShieldsNear(Unit h, Unit e)
        {
            int n = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u != h && u.Alive && u.Team == h.Team && u.Kind != UnitKind.Tower && !u.Has(St.Untargetable) && Dist(u, e) <= e.Range + u.R) n++;
            }
            return n;
        }

        /// <summary>Old incomingDps: the damage per second h would take standing at (px, py).</summary>
        float IncomingDps(Unit h, float px, float py)
        {
            float dps = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var e = us[i];
                if (!e.Alive || e.Team == h.Team) continue;
                float d = Hyp(e.X - px, e.Y - py);
                if (e.Kind == UnitKind.Hero)
                {
                    if (e.Has(St.Stun)) continue;
                    if (!Sees(e) && !e.IsClone)
                    {
                        if (!Remembered(e, h.Team, out float mx, out float my)) continue;
                        if (Hyp(mx - px, my - py) - 1 <= e.Range + h.R + 1.5f) dps += DpsOf(e) * 0.7f;
                        continue;
                    }
                    if (d <= e.Range + h.R + Math.Min(1.5f, SpeedOf(e) * 0.6f)) dps += DpsOf(e) / (1 + ShieldsNear(h, e));
                    else if (d <= SpecialReach(e) + h.R) dps += DpsOf(e) * 0.75f * (1 - SpentCert(h, e));
                }
                else if (e.Kind == UnitKind.Tower)
                {
                    if (d <= e.UDef.Range + h.R + 0.3f && (e.Target == h || TowerTank(e, h.Team) < 30 || M.TowerAggro(e, h))) dps += TowerDpsOn(e, h);
                }
                else if (d <= e.UDef.Range + h.R + 0.3f) dps += DpsOf(e) * 0.5f;
            }
            return dps;
        }
        /// <summary>Seconds h would last standing at (px, py). Normal and Hard bots also take off the burst of every seen enemy hero
        /// whose special is ready and reaches the spot (players see the same as the red diamond over the hero). Bot analysis
        /// 2026-10-30: almost half of all bot deaths came right after the killer's special, with the bot still sure it had time.</summary>
        float TtkAt(Unit h, float px, float py)
        {
            float d = IncomingDps(h, px, py); if (!(d > 0)) return 99;
            float hp = h.Hp + ShieldOf(h);
            if (Tier.V3) hp = Math.Max(hp * 0.15f, hp - ReadyBurst(h, px, py));
            return hp / d;
        }
        float TtkAt(Unit h) => TtkAt(h, h.X, h.Y);

        /// <summary>About how many auto attacks' worth of damage the hero's special puts on one hero at once, from the hero numbers
        /// (Tikta's four needles at 1.2, Blast's bomb with its burn, Bram's hammer with a bounce...). Moa's shields hurt nobody.</summary>
        static float SpecialBurst(HeroId id)
        {
            switch (id)
            {
                case HeroId.Tikta: return 4.8f;
                case HeroId.Bonk: return 1.7f;
                case HeroId.Blast: return 1.6f;
                case HeroId.Oaken: return 1.6f;
                case HeroId.Bram: return 1.5f;
                case HeroId.Tyra: return 1.3f;
                case HeroId.Knut: return 1.3f;
                case HeroId.Fenn: return 1.2f;
                case HeroId.Hilde: return 1.2f;
                default: return 0;
            }
        }

        /// <summary>The burst every seen enemy hero with its special ready (and not stunned) could put on h at (px, py) right now.</summary>
        float ReadyBurst(Unit h, float px, float py)
        {
            float b = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var e = us[i];
                if (e.Kind != UnitKind.Hero || !e.Alive || e.IsClone || e.Team == h.Team || !e.SpecialReady || e.Has(St.Stun) || !Sees(e)) continue;
                if (Hyp(e.X - px, e.Y - py) > SpecialReach(e) + h.R + 1) continue;
                b += SpecialBurst(e.Hero) * e.StatDmg();
            }
            return b;
        }

        // the threat snapshot (old SimThreat.cs): everything about each enemy that does not depend on the spot, worked out once
        struct Threat { public float X, Y, Reach, Dps, SpReach, SpDps; public byte Kind; }
        Threat[] threats = new Threat[64];
        int threatN; long threatTick = -1;

        void ThreatBegin()
        {
            var h = H; threatTick = M.Ticks; threatN = 0;
            var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var e = us[i];
                if (!e.Alive || e.Team == h.Team) continue;
                var t = new Threat { X = e.X, Y = e.Y };
                if (e.Kind == UnitKind.Hero)
                {
                    if (e.Has(St.Stun)) continue;
                    if (!Sees(e) && !e.IsClone)
                    {
                        if (!Remembered(e, h.Team, out float mx, out float my)) continue;
                        t.Kind = 1; t.X = mx; t.Y = my; t.Reach = e.Range + h.R + 1.5f; t.Dps = DpsOf(e) * 0.7f;
                    }
                    else
                    {
                        t.Kind = 0;
                        t.Reach = e.Range + h.R + Math.Min(1.5f, SpeedOf(e) * 0.6f); t.Dps = DpsOf(e) / (1 + ShieldsNear(h, e));
                        t.SpReach = SpecialReach(e) + h.R; t.SpDps = DpsOf(e) * 0.75f * (1 - SpentCert(h, e));
                    }
                }
                else if (e.Kind == UnitKind.Tower)
                {
                    if (!(e.Target == h || TowerTank(e, h.Team) < 30 || M.TowerAggro(e, h))) continue;
                    t.Kind = 2; t.Reach = e.UDef.Range + h.R + 0.3f; t.Dps = TowerDpsOn(e, h);
                }
                else { t.Kind = 2; t.Reach = e.UDef.Range + h.R + 0.3f; t.Dps = DpsOf(e) * 0.5f; }
                if (threatN == threats.Length) Array.Resize(ref threats, threatN * 2);
                threats[threatN++] = t;
            }
        }
        /// <summary>IncomingDps(H, px, py) from this tick's snapshot (the same numbers, without walking every unit per spot).</summary>
        float ThreatAt(float px, float py)
        {
            if (threatTick != M.Ticks) return IncomingDps(H, px, py);
            float dps = 0;
            for (int i = 0; i < threatN; i++)
            {
                ref var t = ref threats[i];
                if (t.Kind == 1) { if (Hyp(t.X - px, t.Y - py) - 1 <= t.Reach) dps += t.Dps; continue; }
                float d = Hyp(t.X - px, t.Y - py);
                if (t.Kind == 2) { if (d <= t.Reach) dps += t.Dps; continue; }
                if (d <= t.Reach) dps += t.Dps;
                else if (d <= t.SpReach) dps += t.SpDps;
            }
            return dps;
        }
        float V3TtkAt(float x, float y) { float d = ThreatAt(x, y); return d > 0 ? (H.Hp + ShieldOf(H)) / d : 99; }

        static readonly float[] SafeRings = { 1.5f, 3f };
        /// <summary>The safest spot 1.5 or 3 tiles around h, leaning towards its own tower.</summary>
        bool SafestSpot(out float bx, out float by)
        {
            var h = H; var own = TowerOf(h.Team); bx = by = 0; bool found = false; float bs = 1e9f;
            ThreatBegin();
            for (int ri = 0; ri < SafeRings.Length; ri++)
                for (int a = 0; a < 12; a++)
                {
                    float r = SafeRings[ri], px = h.X + SimMath.Cos(a * SimMath.PI / 6) * r, py = h.Y + SimMath.Sin(a * SimMath.PI / 6) * r;
                    if (!M.Map.Free(px, py, h.R)) continue;
                    float s = ThreatAt(px, py) * 3 + (own != null ? Hyp(px - own.X, py - own.Y) * 0.25f : 0);
                    if (s < bs) { bs = s; bx = px; by = py; found = true; }
                }
            return found;
        }

        /// <summary>A grass tile near (nx, ny) that no enemy sees or is remembered near, and out of the enemy tower's reach.</summary>
        bool BushSpot(float nx, float ny, float maxD, Unit enemyTower, float clear, out float bx, out float by)
        {
            var h = H; bx = by = 0; bool found = false, threat = false; float bs = 1e9f;
            float seeR = Math.Max(Rules.GrassSight + 0.5f, clear);
            var us = M.Units;
            for (int b = 0; b < bushN; b++)
            {
                float x = bushX[b], y = bushY[b], dn = Hyp(x - nx, y - ny);
                if (dn > maxD || !M.Map.Free(x, y, h.R)) continue;
                if (enemyTower != null && enemyTower.Alive && Hyp(x - enemyTower.X, y - enemyTower.Y) < enemyTower.UDef.Range + 0.5f) continue;
                bool seen = false;
                for (int i = 0; i < us.Count; i++)
                {
                    var u = us[i];
                    if (!u.Alive || u.Team == h.Team || u.Kind == UnitKind.Tower) continue;
                    if (Sees(u) ? Hyp(u.X - x, u.Y - y) < seeR : u.Kind == UnitKind.Hero && !u.IsClone && RemNear(u, h.Team, x, y, seeR)) { seen = true; break; }
                }
                if (seen) continue;
                if (!threat) { ThreatBegin(); threat = true; }
                float s = Hyp(h.X - x, h.Y - y) + dn * 0.5f + ThreatAt(x, y) * 3;
                if (s < bs) { bs = s; bx = x; by = y; found = true; }
            }
            return found;
        }
        bool AnyBushNear(float x, float y, float r)
        {
            for (int b = 0; b < bushN; b++) if (Hyp(bushX[b] - x, bushY[b] - y) < r) return true;
            return false;
        }
    }
}
