using System;
using System.Collections.Generic;

namespace BAM.Sim
{
    /// <summary>
    /// What a dragged special would cover if it were let go now, from the rules' own functions (the Precision program, AIM-01..06):
    /// the HUD's aim, the red hit marks and the precision tests all read this, so the picture can no longer drift from the rules.
    /// Map space (tiles). A line runs from (X, Y) along Ang: its middle travels Length (cut at the first wall the way the shot or
    /// strip really is), a body is hit within Half of that path, and the danger goes Tip further than Length at the far end
    /// (a round shot's body) and Back behind the start (Blast's strip is a capsule). A circle is at (CX, CY) with Radius.
    /// </summary>
    public struct AimInfo
    {
        public AimShape Shape;
        public float X, Y, Ang, Length, Half, Tip, Back;
        public float CX, CY, Radius;
        /// <summary>Only the first enemy along the line is hit (a bolt, hook, needle or boulder stops in it).</summary>
        public bool FirstOnly;
        /// <summary>A circle that needs a clear line from its middle (an arc blast); Oaken's quake: a clear line from him.</summary>
        public bool Los;
        /// <summary>Moa's field: it covers ally heroes, not enemies.</summary>
        public bool Allies;
        /// <summary>Fenn's thrown wolf: it turns onto an enemy hero within PathHalf of its whole flight (0 = none).</summary>
        public float PathHalf;
        /// <summary>Hilde's keg pushes the heroes it hits PushLength tiles along Ang (0 = no push).</summary>
        public float PushLength;
    }

    public sealed partial class Match
    {
        /// <summary>The shape h's drag would cover now, aimed at ang (map space) with length share k (as TryCastSpecial gets them).</summary>
        public AimInfo AimFor(Unit h, float ang, float k)
        {
            var d = h.HDef;
            var a = new AimInfo { Shape = d.Drag, X = h.X, Y = h.Y, Ang = ang };
            k = SimMath.Clamp(k, 0, 1);
            switch (h.Hero)
            {
                case HeroId.Tyra: Line(ref a, BoltReach(h, ang), TyraS.Hitbox, true); break; // to the first wall, enemy tower or the edge
                case HeroId.Knut: Line(ref a, Map.ShotReach(h.X, h.Y, ang, KnutS.HookDistance), KnutS.HookRadius, true); break;
                case HeroId.Tikta: Line(ref a, Map.ShotReach(h.X, h.Y, ang, TiktaS.LineReach), TiktaS.LineHitbox, true); break;
                case HeroId.Bonk: Line(ref a, Map.ShotReach(h.X, h.Y, ang, BonkS.BoulderReach), BonkS.BoulderHitbox, true); break;
                case HeroId.Blast: BombPoint(h, ang, k, out a.CX, out a.CY); a.Shape = AimShape.Circle; a.Radius = BlastS.Radius; a.Length = SimMath.Hypot(a.CX - h.X, a.CY - h.Y); a.Los = true; break; // Supernova (the drag since 2026-10-30)
                case HeroId.Oaken: Line(ref a, Map.LaneReach(h.X, h.Y, ang, OakenS.QuakeRange), OakenS.QuakeHalfWidth, false); a.Tip = 0; a.Los = true; break; // flat ends (OakenQuake)
                case HeroId.Bram:
                    AnvilPoint(h, ang, k, out a.CX, out a.CY); // the aimed point, off a wall tile as the anvil lands (BRAM-02/03)
                    a.Shape = AimShape.Circle; a.Radius = BramS.DropRadius; a.Length = SimMath.Hypot(a.CX - h.X, a.CY - h.Y); a.Los = true;
                    break;
                case HeroId.Hilde: Circle(ref a, h, ang, k, HildeS.Reach, 1.5f, HildeS.KegRadius); a.Los = true; a.PushLength = HildeS.KegKnock; break;
                case HeroId.Moa: Circle(ref a, h, ang, k, MoaS.Range, 0f, MoaS.AreaRadius); a.Allies = true; break;
                case HeroId.Fenn:
                {
                    // as CastFenn: the aimed point, then cut where a wall stops the flying wolf (it leaves from Fenn's spot)
                    AimPoint(h, ang, k, FennS.ThrowReach, out float gx, out float gy);
                    float ga = SimMath.Atan2(gy - h.Y, gx - h.X), gd = WolfReach(h.X, h.Y, ga, SimMath.Hypot(gx - h.X, gy - h.Y));
                    a.Shape = AimShape.Circle; a.Ang = ga; a.Length = gd; a.CX = h.X + SimMath.Cos(ga) * gd; a.CY = h.Y + SimMath.Sin(ga) * gd;
                    a.Radius = FennS.PounceRadius; a.PathHalf = FennS.HomeRadius; a.Los = true;
                    break;
                }
                default: a.Shape = AimShape.None; break;
            }
            return a;
        }

        static void Line(ref AimInfo a, float len, float half, bool first)
        {
            a.Shape = AimShape.Line; a.Length = len; a.Half = half; a.Tip = half; a.FirstOnly = first;
        }

        void Circle(ref AimInfo a, Unit h, float ang, float k, float reach, float minDist, float r)
        {
            AimPoint(h, ang, k, reach, out a.CX, out a.CY, minDist); // clamped to the map like the rules (BRAM-02)
            a.Shape = AimShape.Circle; a.Radius = r; a.Length = SimMath.Hypot(a.CX - h.X, a.CY - h.Y);
        }

        /// <summary>The units team-of-h can see that a would hit if let go now (Moa: the allies it shields), by the rules' tests: inside
        /// the shape counting their own radius, only the first along a line that stops in it, only with a clear line where the rules
        /// want one. Fenn: the first enemy hero met along the flight, else the one the landing bites. Fills and returns buf.</summary>
        public List<Unit> AimTargets(Unit h, in AimInfo a, List<Unit> buf)
        {
            buf.Clear();
            if (a.Shape == AimShape.None) return buf;
            float ca = SimMath.Cos(a.Ang), sa = SimMath.Sin(a.Ang), ex = a.X + ca * a.Length, ey = a.Y + sa * a.Length;
            Unit first = null; float firstAt = float.MaxValue;
            for (int i = 0; i < Units.Count; i++)
            {
                var u = Units[i];
                if (!u.Alive || u.Kind == UnitKind.Tower || u.Has(St.Untargetable) || !IsVisibleTo(u, h.Team)) continue;
                if (a.Allies) { if (u.Team == h.Team && u.Kind == UnitKind.Hero && !u.IsClone && SimMath.Dist(u, a.CX, a.CY) <= a.Radius + u.R) buf.Add(u); continue; }
                if (u.Team == h.Team) continue;
                if (a.PathHalf > 0) continue; // the wolf: after this loop
                if (a.Shape == AimShape.Circle)
                {
                    if (SimMath.Dist(u, a.CX, a.CY) <= a.Radius + u.R && (!a.Los || Map.LosClear(a.CX, a.CY, u.X, u.Y))) buf.Add(u);
                    continue;
                }
                float along = (u.X - a.X) * ca + (u.Y - a.Y) * sa;
                if (along < -a.Back - u.R || along > a.Length + a.Tip + u.R) continue;
                if (a.Tip == 0 && along > a.Length + u.R) continue; // Oaken's flat end
                if (SimMath.PtSeg(u.X, u.Y, a.X, a.Y, ex, ey) > a.Half + u.R) continue;
                if (a.Los && !Map.LosClear(a.X, a.Y, u.X, u.Y)) continue;
                if (!a.FirstOnly) { buf.Add(u); continue; }
                float c = Contact(u, a.X, a.Y, ca, sa, a.Half);
                if (c < firstAt) { firstAt = c; first = u; }
            }
            if (a.PathHalf > 0) first = WolfPrey(h, a);
            if (a.PathHalf > 0 && first == null) { first = PounceTarget(h.Team, a.CX, a.CY, a.Radius); if (first != null && !IsVisibleTo(first, h.Team)) first = null; }
            if (first != null) buf.Add(first);
            return buf;
        }

        /// <summary>The enemy hero the thrown wolf would turn onto, as WolfFly picks it: at each tick's spot of its flight, the
        /// nearest (to its edge) hero within HomeRadius with a clear line, that this team can see; null when none.</summary>
        Unit WolfPrey(Unit h, in AimInfo a)
        {
            float ca = SimMath.Cos(a.Ang), sa = SimMath.Sin(a.Ang), step = FennS.ThrowSpeed * Rules.Tick;
            for (float d = 0; ; d = Math.Min(a.Length, d + step))
            {
                float wx = a.X + ca * d, wy = a.Y + sa * d, bd = float.MaxValue; Unit prey = null;
                for (int i = 0; i < Units.Count; i++)
                {
                    var u = Units[i];
                    if (!u.Alive || u.Team == h.Team || u.Kind != UnitKind.Hero || u.Has(St.Untargetable) || !IsVisibleTo(u, h.Team)) continue;
                    float e = SimMath.Hypot(u.X - wx, u.Y - wy) - u.R;
                    if (e <= a.PathHalf && e < bd && InStrip(a, u) && Map.LosClear(wx, wy, u.X, u.Y)) { bd = e; prey = u; }
                }
                if (prey != null || d >= a.Length) return prey;
            }
        }

        // as InWolfStrip (Fenn 2.0): only a hero inside the red strip from where the wolf leaves to where it lands
        static bool InStrip(in AimInfo a, Unit u)
        {
            float ux = u.X - a.X, uy = u.Y - a.Y;
            if (a.Length < 1e-4f) return SimMath.Hypot(ux, uy) <= a.PathHalf + u.R;
            float ca = SimMath.Cos(a.Ang), sa = SimMath.Sin(a.Ang), along = ux * ca + uy * sa, side = Math.Abs(ux * sa - uy * ca);
            return along >= -u.R && along <= a.Length + a.PathHalf + u.R && side <= a.PathHalf + u.R;
        }

        // how far along the line its front first touches u (a body of half width half)
        static float Contact(Unit u, float x, float y, float ca, float sa, float half)
        {
            float dx = u.X - x, dy = u.Y - y, along = dx * ca + dy * sa, perp = -dx * sa + dy * ca, r = half + u.R;
            return along - (float)Math.Sqrt(Math.Max(0, r * r - perp * perp));
        }
    }
}
