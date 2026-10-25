using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    // Which enemy hero to fight, when to give up a chase, when to step in for a hurt teammate, when to push the tower
    // (old SimAI.cs pickHeroTarget, chaseGiveUp/chaseTrack, baitCheck, soakTarget, pushWindow, towerFinish, wantsBushChase).
    public sealed partial class BotBrain
    {
        static bool Outranges(Unit h, Unit e) => h.Range > e.Range + 0.8f;
        static float HarassKeep(Unit h, Unit e) => Math.Max(h.Range * 0.6f, Math.Min(h.Range + e.R - 0.15f, e.Range + e.R + h.R + 0.6f));

        /// <summary>An auto attack picks a random target in reach (D1), so only 1 / (1 + the other foes in reach) of o's hits land on e.</summary>
        float ShareOn(Unit o, Unit e)
        {
            int n = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u != e && u.Alive && u.Team == e.Team && !u.Has(St.Untargetable) && M.IsVisibleTo(u, o.Team) && Dist(u, o) <= o.Range + u.R) n++;
            }
            return 1f / (1 + n);
        }
        float TeamDpsOn(Unit h, float myDps, List<Unit> mates, Unit e)
        {
            float s = myDps * ShareOn(h, e);
            for (int i = 0; i < mates.Count; i++) { var o = mates[i]; if (Dist(o, e) < o.Range + 2) s += DpsOf(o) * ShareOn(o, e); }
            return s;
        }

        int V3ReadyFoes(Unit e)
        {
            int n = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && u.Team == e.Team && !u.IsClone && u.SpecialReady && Sees(u) && Dist(u, e) < 7) n++; }
            return n;
        }

        /// <summary>Is the low enemy e walking away into a trap (its unseen friends could be just ahead)? 0..1.</summary>
        float BaitCheck(Unit e)
        {
            var h = H;
            float sp = Hyp(e.MoVx, e.MoVy);
            if (HpR(e) > 0.35f || sp < 1 || (e.X - h.X) * e.MoVx + (e.Y - h.Y) * e.MoVy <= 0) return 0;
            float ax = e.X + e.MoVx / sp * 3, ay = e.Y + e.MoVy / sp * 3;
            float s = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || !u.Alive || u == e || u.Team != e.Team || u.IsClone || Sees(u)) continue;
                float lt = u.LastSeenT[h.Team];
                if (lt < -98) { s += 0.2f; continue; } // never seen
                float age = Now - lt, could = Hyp(u.LastSeenX[h.Team] - ax, u.LastSeenY[h.Team] - ay) - SpeedOf(u) * age;
                s += could < 2 ? (age < 1.5f ? 0.35f : 0.25f) : could < 6 ? 0.12f : 0.03f;
            }
            if (AnyBushNear(ax, ay, 2)) s += 0.15f;
            int theirs = 0, ours = 0;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || !u.Alive || u.IsClone || Dist(u, h) >= 8) continue;
                if (u.Team == e.Team) { if (Sees(u)) theirs++; } else if (u.Team == h.Team) ours++;
            }
            if (theirs > ours) s += 0.15f;
            if (HpR(h) < 0.4f) s += 0.15f;
            if (!(susU == e && Now < susT)) { susU = e; susT = Now + 5; susJ = Srand(-0.12f, 0.12f); }
            return SimMath.Clamp(s * BotTables.Suspicion[tierI] * TK(14) + susJ, 0, 1);
        }

        // ---------- the chase leash (V2): give up a hero that cannot be caught ----------
        bool ChaseGiveUp(Unit e, float d, float now)
        {
            var h = H;
            if (e == dropU && now < dropT) return true;
            if (e != chaseU || now - chaseLastT > 0.6f) return false;
            bool held = e.Has(St.Stun) || e.Has(St.SlowPct) || e.Has(St.SlowFlat) || e.Has(St.Root);
            float t = now - chaseT, closing = t > 0 ? (chaseGap - d) / t : 0, left = d - (h.Range + e.R + 0.3f);
            bool giveUp = Hyp(h.X - chaseX, h.Y - chaseY) > BotTables.ChaseLeash || !held && t > BotTables.ChaseLookT && left > closing * BotTables.ChaseCatchT;
            if (giveUp) { dropU = e; dropT = now + BotTables.ChaseDropT; chaseU = null; }
            return giveUp;
        }
        void ChaseTrack(Unit e)
        {
            var h = H; float now = Now, d = Dist(h, e);
            bool contact = d <= h.Range + e.R + 0.3f || HitAt(e, h) > chaseT;
            if (e != chaseU || now - chaseLastT > 0.6f || contact) { chaseU = e; chaseX = h.X; chaseY = h.Y; chaseGap = d; chaseT = now; }
            chaseLastT = now;
        }

        // ---------- the target ----------
        Unit PickHeroTarget(Unit enemyTower, float myDps)
        {
            var h = H; float now = Now; var us = M.Units;
            var allies = bufAllies; allies.Clear();
            for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a.Kind == UnitKind.Hero && a.Alive && a.Team == h.Team && a != h) allies.Add(a); }
            float tank = TowerTank(enemyTower, h.Team);
            Unit best = null; float bs = -1e9f; urgent = false; int sd = StarDiff; float bold = Temperament();
            bool safety = Safety; var lead = TeamLeader();
            // count living heroes like a human glancing at the scoreboard
            int numbers = 0;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && !u.IsClone) numbers += u.Team == h.Team ? 1 : -1; }
            float ns = TK(3), numReach = (numbers > 0 ? 1.5f : numbers < 0 ? -1.5f : 0) * ns, numRatio = 1 + (numbers > 0 ? -0.2f : numbers < 0 ? 0.25f : 0) * ns;
            bool canChase = Tier.CanChaseLow;
            for (int ei = 0; ei < us.Count; ei++)
            {
                var e = us[ei];
                if (!e.Alive || e.Team == h.Team || e.Kind != UnitKind.Hero || e.Has(St.Untargetable) || !Sees(e)) continue;
                int lf = LeadFight(lead, e);
                float d = Dist(h, e); bool low = HpR(e) < 0.35f;
                float teamDps = TeamDpsOn(h, myDps, allies, e), ttkE = (e.Hp + ShieldOf(e)) / teamDps;
                bool hitMe = now - HitAt(h, e) < 1.5f || now - h.LastCombat < 1 && d < e.Range + 0.5f;
                bool saving = false;
                for (int i = 0; i < allies.Count; i++) { var a = allies[i]; if (Dist(a, e) < e.Range + 1 && now - a.LastCombat < 1.5f && HpR(a) < 0.6f) { saving = true; break; } }
                bool assist = false;
                if (low) for (int i = 0; i < M.Heroes.Count; i++) { var k = M.Heroes[i]; if (k.Team == h.Team && k != h && now - e.LastHitBy[i] < 2.5f) { assist = true; break; } }
                Unit engU = null;
                if (!assist && d < 9)
                    for (int i = 0; i < M.Heroes.Count; i++)
                    {
                        var a = M.Heroes[i];
                        if (a.Team == h.Team && a != h && a.Alive && now - e.LastHitBy[i] < 1.5f && HpR(a) > 0.35f && Dist(a, e) < e.Range + 2) { engU = a; break; }
                    }
                float eta = Math.Max(0, d - h.Range) / Math.Max(1, SpeedOf(h));
                bool engaged = engU != null && (eta < 0.9f || eta < 3.5f && (eta < TtkAt(engU) || HpR(e) < 0.2f));
                float sus = safety && low && !hitMe && !saving ? BaitCheck(e) : 0; bool bait = sus > 0.3f, wary = sus > 0.55f;
                if (sus > 0.75f) continue;
                if (v3 && TK3(31) > 0.5f && !hitMe && InvulT(e) > 1) continue; // hits on an invulnerable hero are wasted
                if (!hitMe && !saving && d > h.Range + e.R + 0.3f && lf == 0 && ChaseGiveUp(e, d, now)) continue;
                bool guard = ProtectLead && !hitMe && !saving;
                bool committed = e == focus && commitT > now;
                float behind = Math.Max(0, -sd - 1) * 0.5f;
                int alliesNear = 0;
                for (int i = 0; i < allies.Count; i++) { var a = allies[i]; if (!a.IsClone && Dist(a, h) < 7 && HpR(a) > 0.5f) alliesNear++; }
                float reach = (v3 && InvulT(h) > 1.5f ? 3 * TK3(33) : 0)
                    + (v3 && TK3(34) != 0 && CountSeenNear(e.Team, h.Team, e, e.X, e.Y, 8) == 0 && alliesNear > 0 ? 3 * TK3(34) : 0)
                    + behind + h.Range + Tier.Roaming + (canChase && low && !bait && !guard ? 3 * TK(10) : 0) + (assist ? (wary ? 2 : 5) : 0) + (engaged ? 4 : 0)
                    + (safety && Outranges(h, e) ? 1.5f : 0) + bold * 0.8f + (low && overchase && !bait ? 2.5f : 0) + (hitMe ? 1 : 0) + (saving ? 2 : 0) - (guard ? 1.5f : 0)
                    + numReach + TK(1) + (lf != 0 ? BotTables.TeamReach : 0);
                if (d > reach) continue;
                bool underTower = enemyTower != null && enemyTower.Alive && Dist(e, enemyTower) < enemyTower.UDef.Range + 0.5f;
                if (underTower && (safety || HpR(h) < 0.5f) && tank < 60 && ttkE > Tier.TtkTowerDive * TK(13) && !(lf != 0 && HpR(h) > 0.5f)) continue;
                if (underTower && guard && tank < 25) continue;
                if (v3 && TK3(23) > 0 && !hitMe && !saving && !low && !engaged && lf == 0)
                {   // don't walk alone into a group; wait until a teammate is close enough to help
                    int fn = CountSeenNear(e.Team, h.Team, null, e.X, e.Y, 6), mn = 1;
                    for (int i = 0; i < allies.Count; i++) if (!allies[i].IsClone && Dist(allies[i], h) < 7) mn++;
                    if ((fn - mn) * TK3(23) >= 1) continue;
                }
                bool harass = safety && Outranges(h, e); float sk = harass ? HarassKeep(h, e) : h.Range * 0.85f;
                if (safety)
                {
                    float dd = d != 0 ? d : 1, spotX = e.X + (h.X - e.X) / dd * sk, spotY = e.Y + (h.Y - e.Y) / dd * sk;
                    float spent = 1 - 0.2f * Math.Max(SpentCert(h, e), misjudge ? 1 : 0);
                    float ratio = 0.6f * (sd <= -3 ? 0.85f : sd <= -2 ? 0.92f : sd >= 2 ? 0.75f : 1) * (1 - bold * 0.2f) * spent * (misjudge ? 0.85f : 1) * (engaged ? 0.6f : 1)
                        * (guard ? 1.25f : 1) * (wary ? 1.3f : 1) * numRatio * TK(2) * (v3 && TK3(35) != 0 ? 1 + TK3(35) * 0.1f * V3ReadyFoes(e) : 1)
                        * (v3 ? 1 + TK3(42) * (h.SpecialReady ? -0.15f : 0.15f) : 1);
                    if (TtkAt(h, spotX, spotY) < ttkE * ratio && !hitMe && !saving && !(assist && !wary) && !committed && lf == 0) continue;
                }
                float s = -ttkE - d * (0.5f + TK(23));
                if (v3 && TK3(9) != 0) s += TK3(9) * DpsOf(e) / Math.Max(1, myDps); // kill their damage dealer first
                if (low) s += (canChase ? 4 : 1.5f) + TK(22);
                if (saving) s += v3 ? 3 * TK3(22) : 3;
                if (v3 && (e.Has(St.Stun) || e.Has(St.SlowPct))) s += TK3(32); // a stunned or slowed hero can't get away
                if (assist) s += 5;
                if (engaged) s += 3;
                if (harass) s += 2;
                s += SpentCert(h, e);
                if (hitMe) s += 1.5f;
                for (int i = 0; i < M.Heroes.Count; i++) { var k = M.Heroes[i]; if (k.Team == h.Team && k != h && now - e.LastHitBy[i] < 2) { s += 1.5f; break; } }
                if (e == focus) s += committed ? 3 : 1;
                s += LeadScore(lead, lf); // join the human's fight
                for (int i = 0; i < allies.Count; i++) { var a = allies[i]; if (a.Bot != null && a.Bot.focus == e && Dist(a, h) < 8) s += TK(21) * (v3 ? TK3(3) : 1); } // focus the target a nearby teammate is on
                if (v3 && e.Hp + ShieldOf(e) < h.StatDmg() * 1.05f && d < h.Range + e.R + 1) s += 2 * TK3(8); // a one-hit kill in reach comes first
                if (s > bs) { bs = s; best = e; urgent = low || saving || hitMe || lf == 2; }
            }
            return best;
        }

        // ---------- stepping in for a hurt teammate ----------
        float SoakValue(Unit a) => SimMath.Clamp(1 - TtkAt(a) / 4, 0, 1) + (HpR(H) - 0.5f) - Dist(a, H) / 6 + (BotTables.Tank(H.Hero) ? 0.2f : 0)
            - (focus != null && focus.Alive && HpR(focus) < 0.25f ? 0.3f : 0);
        bool SoakWilling(Unit a)
        {
            int i = a.HeroIndex;
            if (float.IsNaN(soakRT[i]) || Now > soakRT[i]) { soakRT[i] = Now + 4; soakRJ[i] = Srand(-0.2f, 0.2f); }
            return SoakValue(a) * BotTables.Soak[tierI] + soakRJ[i] > 0.45f + TK(12);
        }
        /// <summary>The hurt teammate under fire to step in front of (lowest time-to-kill first).</summary>
        Unit SoakTarget()
        {
            var h = H; Unit best = null; float bt = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var a = us[i];
                if (!(a.Kind == UnitKind.Hero && a.Alive && a != h && a.Team == h.Team && !a.IsClone && HpR(a) < BotTables.SoakAllyHp
                    && !(a.Bot != null && (a.Bot.retreat || a.Bot.Mode == BotMode.Soak)) && Dist(a, h) < 7 && SoakWilling(a) && Now - a.LastCombat < 1)) continue;
                bool pressed = false;
                for (int j = 0; j < us.Count; j++) { var e = us[j]; if (e.Alive && e.Team != h.Team && e.Kind == UnitKind.Hero && Sees(e) && Dist(e, a) <= e.Range + a.R + 0.5f) { pressed = true; break; } }
                if (!pressed) continue;
                float t = TtkAt(a);
                if (best == null || t < bt) { best = a; bt = t; }
            }
            return best;
        }

        // ---------- the tower ----------
        /// <summary>V2: push the enemy tower while the enemy team is a hero down and the dead are still far from respawning.</summary>
        bool PushWindow(Unit tw, float hpR)
        {
            var h = H;
            if (tw == null || !tw.Alive || hpR < 0.45f) return false;
            int foes = 0, mates = 0; float back = 99, dh = Dist(h, tw); var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || u.IsClone) continue;
                if (u.Team == h.Team) { if (u.Alive && HpR(u) > 0.4f && Dist(u, tw) < dh + 8) mates++; }
                else if (u.Alive) foes++; else back = Math.Min(back, u.RespawnT);
            }
            if (foes >= mates) return false;
            float travel = Math.Max(0, dh - h.Range - tw.R) / Math.Max(1, SpeedOf(h));
            return back > travel + (foes == 0 ? 1 : 4) + TK(6);
        }
        /// <summary>V2: the enemy tower is nearly down, so stay and finish it instead of retreating.</summary>
        bool TowerFinish(Unit tw, float hpR, float myDps)
        {
            var h = H;
            if (tw == null || !tw.Alive || hpR < 0.15f || Dist(h, tw) > tw.UDef.Range + 3) return false;
            float dps = myDps; var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var a = us[i]; if (a != h && a.Alive && a.Kind == UnitKind.Hero && a.Team == h.Team && !a.IsClone && Dist(a, tw) < a.Range + tw.R + 1) dps += DpsOf(a); }
            return tw.Hp < dps * 3 && (tw.Target != h || h.Hp + ShieldOf(h) > TowerDpsOn(tw, h) * 3);
        }

        // ---------- after a hero that ran into the grass ----------
        bool WantsBushChase(Unit enemyTower)
        {
            var h = H; var e = seenU;
            bool low = HpR(e) < 0.35f || (e.Hp + ShieldOf(e)) / Math.Max(1, DpsOf(h)) < 2.5f;
            if (!low || HpR(h) < 0.5f || Now - seenT > 3 || Hyp(h.X - seenX, h.Y - seenY) > 7) return false;
            if (BotTables.Ranged(h.Hero) && HpR(e) > 0.2f) return false;
            if (enemyTower != null && enemyTower.Alive && Hyp(seenX - enemyTower.X, seenY - enemyTower.Y) < enemyTower.UDef.Range + 1) return false;
            int foes = 0, mates = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind != UnitKind.Hero || !u.Alive) continue;
                if (u != e && u.Team != h.Team && (Sees(u) ? Hyp(u.X - seenX, u.Y - seenY) < 6 : RemNear(u, h.Team, seenX, seenY, 6))) foes++;
                if (u != h && u.Team == h.Team && Dist(u, h) < 6) mates++;
            }
            int missing = Unaccounted(h, e);
            if (missing >= 2 || missing > 0 && BotTables.Suspicion[tierI] > 0.5f && foes + 1 > mates) return false;
            return foes <= mates && AnyBushNear(seenX, seenY, 2);
        }
    }
}
