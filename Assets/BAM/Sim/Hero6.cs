using System;

namespace BAM.Sim
{
    /// <summary>Oaken 3.0, the Worldbreaker (Simon's rework in XYZ #518/#519/#568, Rickard 2026-10-25 "ta bort oaken väggarna som
    /// simon vill"; in BAM as hero 10 since 2026-10-28). A stone tank: no walls any more. Auto attack: a short punch. Tap, the slam:
    /// after a wind-up where he stands, every enemy hero and minion round him with a clear line takes the hit. Drag, the quake: after a
    /// wind-up a straight line from him (stopped by the first wall) hurts every enemy hero and minion in it with a clear line from him
    /// and knocks enemy heroes a short way along it; a knock that ends in a wall hurts (Speltest K5). Both wind-ups are cut by a stun.
    /// Passive, Stored Impact: after 4 s without losing health, the next direct hit from an enemy hero is cut by 30 %, and 60 % of what
    /// he was spared (at most 30 % of his attack) is added to his next auto attack hit within 5 s. Every 16 s. Numbers from Speltest
    /// SimHeroes6.cs ("OakenWorldbreaker", "OakenPassive") and SimBamFourReworks.cs.</summary>
    public static class OakenS
    {
        /// <summary>The slam: wind-up, radius, damage. Reach is how close an enemy hero must be for a bot to want it.</summary>
        public const float SlamWindup = 0.6f, SlamRadius = 1.7f, SlamCoeff = 1.4f, Reach = 2.0f;
        /// <summary>The quake: wind-up, length, half width, damage, and the knock (distance at KnockSpeed tiles/s).</summary>
        public const float QuakeWindup = 0.5f, QuakeRange = 3.3f, QuakeHalfWidth = 0.45f, QuakeCoeff = 1.1f, QuakeKnock = 0.6f, KnockSpeed = 9;
        /// <summary>Stored Impact.</summary>
        public const float ReadyAfter = 4, Reduction = 0.3f, StorageRatio = 0.6f, StorageCap = 0.3f, StoredLife = 5, Cooldown = 16;
    }

    public sealed partial class Match
    {
        bool CastOaken(Unit h, float dmg)
        {
            bool drag = Aimed(h, out float aa, out _);
            if (!drag)
            {   // the slam: only with an enemy hero or minion in reach (as Bonk's and Knut's slams)
                if (EnemiesInRange(h, OakenS.SlamRadius, Pick.HeroOrMinion, true, spBuf).Count == 0) return false;
                var s = NewProj(ProjMode.Worldbreaker, h, h.X, h.Y);
                s.Kind = ProjKind.OakenSlam; s.T = -OakenS.SlamWindup; s.Dur = OakenS.SlamWindup; s.Radius = OakenS.SlamRadius; s.Dmg = dmg * OakenS.SlamCoeff; s.Big = true;
                WarnCircle(h, h.X, h.Y, OakenS.SlamRadius, OakenS.SlamWindup);
                OakenWindup(h, h.Facing, OakenS.SlamWindup);
                return true;
            }
            var q = NewProj(ProjMode.Quake, h, h.X, h.Y);
            q.Kind = ProjKind.OakenQuake; q.T = -OakenS.QuakeWindup; q.Dur = OakenS.QuakeWindup; q.Ang = aa; q.Radius = OakenS.QuakeHalfWidth; q.Dmg = dmg * OakenS.QuakeCoeff; q.Big = true;
            float len = Map.LaneReach(h.X, h.Y, aa, OakenS.QuakeRange);
            q.D = len;
            WarnLine(h, aa, len, OakenS.QuakeHalfWidth, OakenS.QuakeWindup);
            OakenWindup(h, aa, OakenS.QuakeWindup);
            castX = h.X + SimMath.Cos(aa) * len; castY = h.Y + SimMath.Sin(aa) * len;
            return true;
        }

        /// <summary>He stands still during a wind-up (no walking, no attacks) and faces the way it goes.</summary>
        void OakenWindup(Unit h, float ang, float d)
        {
            h.Facing = ang; h.AimT = Time + d + 0.15f;
            AddStatus(h, St.Casting, d); AddStatus(h, St.Disarm, d); Channel(h, d);
        }

        /// <summary>The slam lands where Oaken stands: every enemy hero and minion within its radius with a clear line takes the hit.</summary>
        void OakenSlam(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || h.Has(St.Stun)) return;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (Hurtable(h, u) && SimMath.Dist(u, p.X, p.Y) <= p.Radius + u.R && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++) if (areaBuf[i].Alive) SpecialHit(h, areaBuf[i], p.Dmg, p.Kind, true);
            Emit(EventType.Explosion, h, null, p.Radius, (byte)p.Kind, true, p.X, p.Y);
        }

        /// <summary>The quake runs from where Oaken stood along its line (as long as the cast found it, so a wall ends it): every enemy
        /// hero and minion touching the strip with a clear line from him takes the hit, and each enemy hero hit (not CC immune) slides
        /// QuakeKnock tiles along it.</summary>
        void OakenQuake(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || h.Has(St.Stun)) return;
            float ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang), ex = p.X + ca * p.D, ey = p.Y + sa * p.D;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(h, u)) continue;
                float along = (u.X - p.X) * ca + (u.Y - p.Y) * sa;
                if (along < -u.R || along > p.D + u.R || SimMath.PtSeg(u.X, u.Y, p.X, p.Y, ex, ey) > p.Radius + u.R || !Map.LosClear(p.X, p.Y, u.X, u.Y)) continue;
                areaBuf.Add(u);
            }
            KegCrash wall = null;
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                bool free = CcImmune(u);
                SpecialHit(h, u, p.Dmg, p.Kind, true);
                if (free) CcBlocked(h, u);
                if (u.Kind != UnitKind.Hero || !u.Alive || free || KnockImmune(u)) continue;
                InterruptSpecial(u);
                // a slide into a wall hurts as Hilde's keg does (Speltest's K5 wallSlam), but only her keg stuns: this crash has none to give
                if (wall == null) wall = new KegCrash { By = h, Stunned = true };
                float time = OakenS.QuakeKnock / OakenS.KnockSpeed; u.KnockFreeAt = Time + Rules.KnockCooldown;
                u.KnockVX = ca * OakenS.KnockSpeed; u.KnockVY = sa * OakenS.KnockSpeed; u.KnockT = time; u.KnockPad = false; u.KnockKeg = wall; u.KnockRoll = null;
                Emit(EventType.Knock, h, u, time, (byte)p.Kind, true, u.X + ca * OakenS.QuakeKnock, u.Y + sa * OakenS.QuakeKnock);
            }
            Emit(EventType.Quake, h, null, p.D, (byte)p.Kind, true, ex, ey);
        }

        /// <summary>Stored Impact, after shields and before Queen's Instinct: a direct hit from an enemy hero on an Oaken who has lost
        /// no health for ReadyAfter s (and whose passive is off cooldown) is cut by Reduction, and part of the cut is stored.</summary>
        float StoredImpact(Unit src, Unit tgt, float amount, DmgKind kind, DmgFlags flags)
        {
            if (tgt.IsClone || amount <= 0 || Time - tgt.OakenLastLoss < OakenS.ReadyAfter || Time < tgt.OakenReadyAt) return amount;
            if ((flags & (DmgFlags.Burning | DmgFlags.DotTick | DmgFlags.NotDirect)) != 0 || (kind != DmgKind.Auto && kind != DmgKind.Special)) return amount;
            if (src == null || src.Kind != UnitKind.Hero || src.IsClone || src.Owner != null || src.Team == tgt.Team) return amount;
            float cut = amount * OakenS.Reduction;
            tgt.OakenStored = Math.Min(tgt.StatDmg() * OakenS.StorageCap, cut * OakenS.StorageRatio);
            tgt.OakenStoredUntil = Time + OakenS.StoredLife;
            tgt.OakenReadyAt = Time + OakenS.Cooldown;
            Emit(EventType.OakenGuard, tgt, src, cut, (byte)HeroId.Oaken, false, tgt.X, tgt.Y);
            return amount - cut;
        }

        /// <summary>His next auto attack hit after a Stored Impact (on anyone, within StoredLife) deals the stored damage on top.</summary>
        void OakenRelease(Unit h, Unit t)
        {
            if (!(h.OakenStored > 0) || Time > h.OakenStoredUntil || t.Has(St.Invulnerable) || t.Has(St.Untargetable)) return;
            float extra = h.OakenStored; h.OakenStored = 0;
            if (t.Alive) SpecialHit(h, t, extra, ProjKind.OakenFist, true, DmgKind.Passive);
        }
    }
}
