using System;

namespace BAM.Sim
{
    // Bots that play as a team with the human player (old SimTeamplay.cs, on by default in the old game). The player's
    // teammates join the player's fights, stay in them, pull back with the player and walk with the player; and with a
    // person in the match every bot reacts a little slower and holds its special for a moment worth it. A match without a
    // human (MatchConfig.PlayerIsBot, the headless runner) is unchanged. The old team signals (ATTACK/RETREAT) are not in BAM.
    // Healthy bots also stay within TeamLeash of the human (old teamLeash, Rickard 2026-10-25: "they should back off when I back
    // off, go in when I go in"): enemy heroes far from the human are not chased, and a goal outside the ring becomes a spot beside them.
    public sealed partial class BotBrain
    {
        bool HumanInMatch => !M.Config.PlayerIsBot && M.Tut == null && M.Player != null;

        /// <summary>The human on this bot's team (null = none, or they are dead).</summary>
        Unit TeamLeader()
        {
            if (!HumanInMatch) return null;
            var l = M.Player;
            return l != H && l.Team == H.Team && l.Alive ? l : null;
        }

        /// <summary>Is the leader l (near h) fighting enemy hero e? 0 = no, 1 = l hit e, 2 = e hit l (peel), 3 = l is walking at e.</summary>
        int LeadFight(Unit l, Unit e) => LeadFight(l, H, e);
        int LeadFight(Unit l, Unit h, Unit e)
        {
            if (l == null || Dist(l, h) > BotTables.TeamJoinR) return 0;
            if (h != l && (HpR(h) < BotTables.TeamJoinHp || LeaderBacking(l))) return 0; // hurt, or the human is pulling out: the bot's own caution decides
            float now = Now;
            if (now - HitAt(l, e) < BotTables.TeamFightT) return 2;
            if (now - HitAt(e, l) < BotTables.TeamFightT) return 1;
            if (h != l && LeadIntent(l) == e) return 3;
            return 0;
        }

        /// <summary>The enemy hero the human walks straight at (kept a moment after they stop to attack it).</summary>
        Unit LeadIntent(Unit l)
        {
            float now = Now; if (leadAt == now) return leadE; leadAt = now;
            Unit best = null; float bc = 0.8f; // cos of the angle between the walk and the line to the enemy
            float sp = Hyp(l.MoVx, l.MoVy);
            if (sp > SpeedOf(l) * 0.5f)
            {
                var us = M.Units;
                for (int i = 0; i < us.Count; i++)
                {
                    var e = us[i];
                    if (e.Kind != UnitKind.Hero || !e.Alive || e.Team == l.Team || e.IsClone || e.Has(St.Untargetable) || !M.IsVisibleTo(e, l.Team)) continue;
                    float d = Dist(l, e); if (d > BotTables.TeamIntentR || d < 0.01f) continue;
                    float c = (l.MoVx * (e.X - l.X) + l.MoVy * (e.Y - l.Y)) / (d * sp);
                    if (c > bc) { bc = c; best = e; }
                }
            }
            if (best != null) { leadE = best; leadT = now; }
            else if (leadE != null && (now - leadT > BotTables.TeamIntentKeep || !leadE.Alive || !M.IsVisibleTo(leadE, l.Team) || Dist(l, leadE) > BotTables.TeamIntentR + 1)) leadE = null;
            return leadE;
        }

        static float LeadScore(Unit l, int lf) => lf == 0 ? 0 : lf == 2 ? 4 + 4 * (1 - HpR(l)) : lf == 3 ? 5 : 4;

        bool LeaderInFight(Unit l)
        {
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var e = us[i]; if (e.Kind == UnitKind.Hero && e.Alive && e.Team != l.Team && LeadFight(l, l, e) != 0) return true; }
            return false;
        }

        /// <summary>The human is pulling out of a fight: low, or walking away from the nearest enemy hero while out of their own range of it.</summary>
        bool LeaderBacking(Unit l)
        {
            if (HpR(l) < BotTables.TeamLowHp) return true;
            Unit e = null; float ed = BotTables.TeamBackR; var us = M.Units;
            for (int i = 0; i < us.Count; i++)
            {
                var u = us[i];
                if (u.Kind == UnitKind.Hero && u.Alive && u.Team != l.Team && !u.IsClone && M.IsVisibleTo(u, l.Team) && Dist(u, l) < ed) { e = u; ed = Dist(u, l); }
            }
            if (e == null || ed < 0.01f || ed < l.Range + e.R + 0.5f) return false;
            float away = (l.MoVx * (l.X - e.X) + l.MoVy * (l.Y - e.Y)) / ed;
            return away > SpeedOf(l) * 0.5f;
        }

        /// <summary>After the retreat decision: pull back with the human, or don't leave them alone in a fight.</summary>
        void TeamStay(float hpR)
        {
            var h = H; var l = TeamLeader(); if (l == null || Dist(l, h) > 10) return;
            if (LeaderBacking(l))
            {
                if (!retreat && CountSeenNear(Teams.Other(h.Team), h.Team, null, h.X, h.Y, BotTables.TeamBackR) > 0) { retreat = true; retreatT = Now; }
                return;
            }
            if (!retreat || hpR < BotTables.TeamStayHp || ttk < 4 || Dist(l, h) > 8 || !LeaderInFight(l)) return;
            retreat = false;
        }

        /// <summary>The human is hitting the enemy tower: help.</summary>
        bool TeamTower(Unit tw, float hpR)
        {
            if (tw == null || !tw.Alive || hpR < 0.45f) return false;
            var l = TeamLeader(); if (l == null || Dist(l, tw) > tw.UDef.Range + 1.5f || Dist(l, H) > BotTables.TeamJoinR) return false;
            return Now - HitAt(tw, l) < 1.5f;
        }

        /// <summary>Nothing to fight: walk with the human, a little behind and to the side.</summary>
        void TeamFollow(float hpR, Unit myTower, int fwd)
        {
            var h = H; var l = TeamLeader();
            if (l == null || hpR < 0.45f) { following = false; return; }
            float d = Dist(h, l);
            if (d < (following ? BotTables.TeamFollowStop : BotTables.TeamFollowR) || HpR(l) < 0.3f) { following = false; return; } // close enough, or the leader is going home to heal
            if (myTower != null && myTower.Alive && Dist(l, myTower) < myTower.UDef.Range + 1 || Hyp(l.X - l.SpawnX, l.Y - l.SpawnY) < 4) { following = false; return; } // the leader is at home
            FollowSpot(l, fwd, out float gx, out float gy);
            var et = TowerOf(Teams.Other(h.Team)); // never walk in under the enemy tower on a plain follow
            if (et != null && et.Alive && Hyp(gx - et.X, gy - et.Y) < et.UDef.Range + 0.5f) { following = false; return; }
            following = true; Mode = BotMode.Follow; SetGoal(gx, gy);
        }

        /// <summary>A spot a little behind the human and to the side (the two bots take one side each).</summary>
        void FollowSpot(Unit l, int fwd, out float gx, out float gy)
        {
            var h = H; int si = 0; var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u == h) break; if (u.Kind == UnitKind.Hero && u.Team == h.Team && u != l) si++; }
            float side = si % 2 == 0 ? -1.3f : 1.3f;
            gx = l.X + side; gy = l.Y - fwd * 1.5f;
            if (!Walkable(h, gx, gy)) { gx = l.X; gy = l.Y - fwd * 1.0f; }
            if (!Walkable(h, gx, gy)) { gx = l.X; gy = l.Y; }
        }

        /// <summary>The bot keeps within TeamLeash of the human (healthy bots only; a hurt one uses its own caution).</summary>
        bool TeamLeashed(Unit l) => l != null && HpR(H) >= BotTables.TeamJoinHp;

        /// <summary>PickHeroTarget: an enemy hero this far from the human is not ours to chase, unless the human is fighting it
        /// (an enemy that just hit the bot, or one at our tower, is always fair game).</summary>
        bool TeamOutOfLeash(Unit l, Unit e, int lf) => lf == 0 && TeamLeashed(l) && Dist(l, e) > BotTables.TeamLeash + 1.5f
            && !(Now - HitAt(H, e) < BotTables.TeamFightT) && !AtOurTower(e);

        bool AtOurTower(Unit e) { var mt = TowerOf(H.Team); return mt != null && mt.Alive && Dist(e, mt) < mt.UDef.Range + 2; }

        bool TowerUnderAttack()
        {
            var us = M.Units;
            for (int i = 0; i < us.Count; i++) { var u = us[i]; if (u.Kind == UnitKind.Hero && u.Alive && u.Team != H.Team && !u.IsClone && M.IsVisibleTo(u, H.Team) && AtOurTower(u)) return true; }
            return false;
        }

        /// <summary>After the goal is picked: a goal outside the ring around the human becomes a spot beside them, so the bots back
        /// off when the human backs off and go in when they go in. Retreat, danger and leaving the tower are the bot's own safety.</summary>
        void TeamLeash(bool finish, int fwd)
        {
            var h = H; if (retreat || finish || Mode == BotMode.Danger || Mode == BotMode.LeaveTower) return;
            var l = TeamLeader(); if (!TeamLeashed(l)) return;
            if (TowerUnderAttack()) return; // defend the tower even when the human is elsewhere
            var mt = TowerOf(h.Team); if (mt != null && mt.Alive && Dist(l, mt) < mt.UDef.Range + 1 || Hyp(l.X - l.SpawnX, l.Y - l.SpawnY) < 4) return; // the human is home healing: hold the wave meanwhile
            if (hasGoal ? Hyp(gX - l.X, gY - l.Y) <= BotTables.TeamLeash : Dist(h, l) <= BotTables.TeamLeash) return;
            if (focus != null && LeadFight(l, h, focus) != 0) return; // the human's own fight
            Mode = BotMode.Follow; focus = null; following = true;
            FollowSpot(l, fwd, out float gx, out float gy); SetGoal(gx, gy);
        }

        float HumanReaction() => HumanInMatch ? BotTables.HumanReact : 1;

        /// <summary>With a person in the match: hold the ready special (up to 6 s) for a moment worth it.</summary>
        bool TeamSpecialHold(bool dz)
        {
            if (!HumanInMatch) { spHoldT = 0; return false; }
            float now = Now;
            if (!(spHoldT > 0) || now - spHoldL > 3) spHoldT = now;
            spHoldL = now;
            bool go = dz || now - spHoldT > BotTables.SpHoldMax;
            if (!go)
            {
                var us = M.Units;
                for (int i = 0; i < us.Count; i++)
                {
                    var u = us[i];
                    if (!u.Alive || u.Kind != UnitKind.Hero || u.IsClone) continue;
                    if (u.Team == H.Team) { if (u != H && Dist(u, H) < 9 && u.InCombat(now)) { go = true; break; } }
                    else if (M.IsVisibleTo(u, H.Team) && HpR(u) < 0.45f) { go = true; break; }
                }
            }
            if (go) { spHoldT = 0; return false; }
            return true;
        }
    }
}
