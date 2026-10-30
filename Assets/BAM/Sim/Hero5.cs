using System;

namespace BAM.Sim
{
    // Simon's and Rickard's XYZ hero changes of 2026-10-25 that BAM did not have yet (handover 2026-10-28, Rickard: "din plan
    // låter bra"): Bram's Anvil Drop (drag) and Backlash (passive), Tyra's Arrow Volley (tap) and Featherstep (passive). Numbers as
    // in XYZ main (SimHeroes6.cs, SimSpecials2.cs, SimBamFourReworks.cs); the constants live in BramS and TyraS (Heroes.cs).
    public sealed partial class Match
    {
        // ---------- Bram ----------
        /// <summary>Anvil Drop: the hammer comes down on the aimed point after DropDelay; a red circle warns where.</summary>
        bool CastAnvilDrop(Unit h, float dmg, float aa, float ak)
        {
            AnvilPoint(h, aa, ak, out float x, out float y);
            var p = NewProj(ProjMode.Drop, h, x, y);
            p.Kind = ProjKind.BramHammerSpecial; p.T = -BramS.DropDelay; p.Dur = BramS.DropDelay; p.Radius = BramS.DropRadius; p.Dmg = dmg * BramS.DropCoeff; p.Big = true;
            WarnOf(WarnCircle(h, x, y, BramS.DropRadius, BramS.DropDelay), p); // stays if Bram dies: the anvil still lands (BRAM-01)
            h.Facing = aa; h.AimT = Time + 0.6f; AddStatus(h, St.Casting, 0.2f);
            castX = x; castY = y;
            return true;
        }

        /// <summary>Where Anvil Drop lands for this aim (also for the aim circle): the aimed point, but never inside a wall tile,
        /// where every line from it is blocked and it would hit nobody (BRAM-03): there it comes down on the wall's face, the last
        /// point on the way back to Bram that is not in a wall. Over a wall it still flies (the wall rule).</summary>
        public void AnvilPoint(Unit h, float ang, float k, out float x, out float y) => LandPoint(h, ang, k, BramS.Reach, out x, out y);

        /// <summary>A thrown ground special's landing point (AimPoint), pulled back toward h off a wall tile (Bram's anvil, Blast's bomb).</summary>
        void LandPoint(Unit h, float ang, float k, float reach, out float x, out float y)
        {
            AimPoint(h, ang, k, reach, out x, out y);
            if (!Map.WallAt(x, y)) return;
            float d = SimMath.Hypot(h.X - x, h.Y - y);
            if (d < 1e-4f) return;
            float sx = (h.X - x) / d * 0.05f, sy = (h.Y - y) / d * 0.05f;
            for (float m = 0; m < d && Map.WallAt(x, y); m += 0.05f) { x += sx; y += sy; }
        }

        /// <summary>The anvil lands: every enemy hero and minion (not towers) within its radius with a clear line from it is hurt and
        /// stunned (Moa's shield blocks the stun). After Bram's death it still lands (as passive damage).</summary>
        void AnvilDrop(Proj p)
        {
            var h = p.Src; var dk = h.Alive ? DmgKind.Special : DmgKind.Passive;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (Hurtable(h, u) && SimMath.Dist(u, p.X, p.Y) <= p.Radius + u.R && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                SpecialHit(h, u, p.Dmg, p.Kind, true, dk);
                if (u.Alive) AddStatus(u, St.Stun, BramS.DropStun, 0, 0, h);
            }
            Emit(EventType.Explosion, h, null, p.Radius, (byte)p.Kind, true, p.X, p.Y, dk);
        }

        /// <summary>Backlash: every 4th auto attack hit sends an echo of the hammer from t back towards Bram (a fixed line, no random
        /// pick). A target standing on top of Bram uses up the 4th hit with no echo.</summary>
        void BramBacklash(Unit h, Unit t)
        {
            if (t.Has(St.Invulnerable) || t.Has(St.Untargetable) || ++h.PassHits < BramS.EchoEvery) return;
            h.PassHits = 0;
            float travel = Math.Min(BramS.EchoDistance, SimMath.Dist(h, t));
            if (travel < 0.01f) return;
            var p = NewProj(ProjMode.Echo, h, t.X, t.Y);
            p.Kind = ProjKind.BramHammer; p.Tgt = t; p.Ang = SimMath.Atan2(h.Y - t.Y, h.X - t.X); p.Travel = travel; p.Speed = BramS.EchoSpeed;
            p.Radius = BramS.EchoHitbox; p.Dmg = h.StatDmg() * BramS.EchoCoeff; p.Big = true;
        }

        /// <summary>The echo flies straight; the first enemy hero or minion other than the one it came from, before any wall, takes
        /// the hit (passive damage). It ends at its distance or a wall.</summary>
        void EchoUpdate(Proj p, float dt)
        {
            float ox = p.X, oy = p.Y, len = Math.Max(0, Math.Min(p.Speed * dt, p.Travel - p.D));
            var u = EchoHit(p, ox, oy, len, out float at);
            if (u != null)
            {
                float hx = ox + SimMath.Cos(p.Ang) * at, hy = oy + SimMath.Sin(p.Ang) * at;
                if (!Map.ShotBlocked(ox, oy, hx, hy))
                {
                    p.X = hx; p.Y = hy; p.D += at; p.Done = true;
                    SpecialHit(p.Src, u, p.Dmg, p.Kind, true, DmgKind.Passive);
                    return;
                }
            }
            p.X += SimMath.Cos(p.Ang) * len; p.Y += SimMath.Sin(p.Ang) * len; p.D += len;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            if (p.D >= p.Travel - 1e-5f) p.Done = true;
        }

        /// <summary>The first enemy the echo's path touches this tick that it may hit: as SweptHit, but Fenn's decoy and enemies the
        /// team cannot see are passed by here, so one in front never hides a real target behind it for a tick (BRAM-06).</summary>
        Unit EchoHit(Proj p, float ox, float oy, float len, out float along)
        {
            along = 0; Unit best = null; float bestAt = float.MaxValue, ca = SimMath.Cos(p.Ang), sa = SimMath.Sin(p.Ang);
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(p.Src, u) || u.Team == p.Team || u == p.Tgt || u.IsClone || !IsVisibleTo(u, p.Team)) continue;
                float dx = u.X - ox, dy = u.Y - oy, a = dx * ca + dy * sa, perp2 = dx * dx + dy * dy - a * a, rr = u.R + p.Radius;
                if (perp2 > rr * rr) continue;
                float hh = SimMath.Sqrt(Math.Max(0, rr * rr - perp2)), enter = a - hh;
                if (a + hh < 0 || enter > len) continue;
                enter = Math.Max(0, enter);
                if (enter < bestAt) { bestAt = enter; best = u; }
            }
            along = best != null ? bestAt : 0;
            return best;
        }

        // ---------- Knut ----------
        /// <summary>Anchor Slam: only with an enemy hero or minion in reach; a red circle warns during the wind-up.</summary>
        bool CastAnchorSlam(Unit h, float dmg)
        {
            if (EnemiesInRange(h, KnutS.SlamRadius, Pick.HeroOrMinion, true, spBuf).Count == 0) return false;
            var s = NewProj(ProjMode.AnchorSlam, h, h.X, h.Y);
            s.Kind = ProjKind.KnutSlam; s.T = -KnutS.SlamWindup; s.Dur = KnutS.SlamWindup; s.Radius = KnutS.SlamRadius; s.Dmg = dmg * KnutS.SlamCoeff; s.Big = true;
            WarnCircle(h, h.X, h.Y, KnutS.SlamRadius, KnutS.SlamWindup);
            AddStatus(h, St.Casting, KnutS.SlamWindup); Channel(h, KnutS.SlamWindup);
            return true;
        }

        /// <summary>The anchor lands where Knut stood (he cannot walk during the wind-up): every enemy hero and minion within its
        /// radius with a clear line takes the hit and the slow (Moa's shield blocks the slow). Cut by a stun, as Bonk's slam.</summary>
        void AnchorSlam(Proj p)
        {
            var h = p.Src;
            if (h == null || !h.Alive || h.Has(St.Stun)) return;
            areaBuf.Clear();
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (Hurtable(h, u) && SimMath.Dist(u, p.X, p.Y) <= p.Radius + u.R && Map.LosClear(p.X, p.Y, u.X, u.Y)) areaBuf.Add(u);
            }
            for (int i = 0; i < areaBuf.Count; i++)
            {
                var u = areaBuf[i];
                if (!u.Alive) continue;
                SpecialHit(h, u, p.Dmg, p.Kind, true);
                if (u.Alive) AddSlowOnce(u, St.SlowPct, KnutS.SlamSlowTime, -KnutS.SlamSlow, h);
            }
            Emit(EventType.Explosion, h, null, p.Radius, (byte)p.Kind, true, p.X, p.Y);
        }

        // ---------- Fenn 2.0 (Simon's #607, XYZ PR #609; Rickard 2026-10-29 "ta båda") ----------
        /// <summary>Pack Hunt: the called (tapped) wolf takes the nearest enemy hero or minion within its sight of where it appears as
        /// its prey at once: no hero first, never a tower or a decoy; ties go to the earlier unit (spawn order), no random pick. It
        /// keeps that prey while it is alive, visible and within the leash of Fenn, then hunts by the called wolf's old rule.</summary>
        void PackHunt(Unit w)
        {
            var l = EnemiesInRange(w, FennS.Sight, Pick.HeroOrMinion, false, aiBuf);
            Unit best = null; float bd = float.MaxValue;
            for (int i = 0; i < l.Count; i++)
            {
                var u = l[i]; if (u.IsClone || SimMath.Dist(w.Owner, u) > FennS.Leash) continue;
                float d = SimMath.Dist(u, w.X, w.Y); if (d < bd) { bd = d; best = u; }
            }
            w.WolfPrey = best;
        }

        /// <summary>Shadow Escape: a real enemy hero's auto attack hits Fenn's decoy; Fenn runs 20 % faster for 1 s.</summary>
        void ShadowEscape(Unit src, Unit decoy, DmgKind kind)
        {
            var att = src != null ? (src.Owner ?? src) : null;
            if (kind != DmgKind.Auto || att == null || att != src || att.Kind != UnitKind.Hero || att.IsClone || att.Team == decoy.Team) return;
            var f = decoy.Owner;
            if (f == null || !f.Alive || f.Hero != HeroId.Fenn || !decoy.Alive) return;
            AddStatus(f, St.SpeedPct, FennS.EscapeTime, FennS.EscapeBonus, 0, f);
            Emit(EventType.ShadowEscape, f, att, FennS.EscapeTime, 0, false, f.X, f.Y);
        }

        // ---------- Tyra ----------
        /// <summary>Arrow Volley: a random enemy hero or minion within her attack range with a clear line (no hero first; none: no
        /// cast), five arrows fanned round the way to it after the draw.</summary>
        bool CastVolley(Unit h, float dmg)
        {
            var l = EnemiesInRange(h, h.Range, Pick.HeroOrMinion, true, spBuf);
            if (l.Count == 0) return false;
            var t = PickRandom(l);
            LeadAim(h, t, Flight.Linear, TyraS.VolleyWindup, TyraS.VolleySpeed, out float ax, out float ay);
            float ang = SimMath.Atan2(ay - h.Y, ax - h.X), half = TyraS.VolleyHalfAngle; int n = TyraS.VolleyArrows;
            h.VolleyCast++; h.VolleyHit.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = NewProj(ProjMode.Volley, h, h.X, h.Y);
                p.Kind = ProjKind.TyraArrow; p.T = -TyraS.VolleyWindup; p.Ang = n == 1 ? ang : ang - half + 2 * half * i / (n - 1);
                p.Travel = h.Range; p.Speed = TyraS.VolleySpeed; p.Radius = TyraS.VolleyHitbox; p.Dmg = dmg * TyraS.VolleyCoeff; p.VolleyCast = h.VolleyCast;
            }
            h.Facing = ang; h.AimT = Time + TyraS.VolleyWindup + 0.2f;
            AddStatus(h, St.Casting, TyraS.VolleyWindup); AddStatus(h, St.Disarm, TyraS.VolleyWindup); Channel(h, TyraS.VolleyWindup);
            castX = ax; castY = ay; castTgt = t;
            return true;
        }

        /// <summary>One volley arrow: straight through enemies (never towers), each enemy hurt once by the whole volley; it ends at its
        /// reach or a wall.</summary>
        void VolleyUpdate(Proj p, float dt)
        {
            // the last step stops on the reach (precision TYRA-01: a full 0.3-tile step took it to 4.2, half a tile past the ring)
            var s = p.Src; float ox = p.X, oy = p.Y, step = Math.Max(0, Math.Min(p.Speed * dt, p.Travel - p.D));
            p.X += SimMath.Cos(p.Ang) * step; p.Y += SimMath.Sin(p.Ang) * step; p.D += step;
            if (Map.ShotBlocked(ox, oy, p.X, p.Y)) { p.Done = true; Blocked(p, ox, oy); return; }
            bool shared = s.VolleyCast == p.VolleyCast;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!Hurtable(s, u) || u.Team == p.Team || SimMath.Hypot(u.X - p.X, u.Y - p.Y) > p.Radius + u.R) continue;
                if (shared ? s.VolleyHit.Contains(u) : p.HasHit(u)) continue;
                if (shared) s.VolleyHit.Add(u); else p.AddHit(u);
                SpecialHit(s, u, p.Dmg, p.Kind, false);
            }
            if (p.D >= p.Travel - 1e-5f) p.Done = true;
        }

        /// <summary>Featherstep: her auto attack hurting an enemy hero (not Fenn's decoy) speeds her up, once every 10 s.</summary>
        void Featherstep(Unit h, Unit t, float dealt)
        {
            if (t.Kind != UnitKind.Hero || t.IsClone || dealt <= 0 || Time < h.FeatherReadyAt) return;
            h.FeatherReadyAt = Time + TyraS.FeatherCooldown;
            AddStatus(h, St.SpeedPct, TyraS.FeatherTime, TyraS.FeatherBonus, 0, h);
        }
    }
}
