using System;

namespace BAM.Sim
{
    /// <summary>The warning before a tapped special (Rules.SpecialTelegraph, Rickard's tester 2026-11-02: "the reaction window is very
    /// short once an ultimate is activated" and "some ultimates seem to be guaranteed hits as long as the target is within range").
    /// The tap picks its target as before (OneTap.cs) but does not cast yet: the aim is locked at once (a line's direction, a placed
    /// form's spot), the hero stands still with the interrupt bar over the head and flashes (EventType.Telegraph), and a red warning
    /// of the special's real shape lies on the ground for TeleTime. Then the hero's own cast runs at the locked aim, wind-up and all.
    /// Nothing homes after the tap: Fenn's thrown wolf lands on the locked spot (WolfFly). A stun, root or knockback in the warning
    /// cuts it like any wind-up (InterruptSpecial): no cast, the charge stays. The bots read the red warning as every other
    /// (BotSpecial.DodgeVector), the same brain on both teams. Moa's shield on a friend waits as long, with the flash but no red warning.</summary>
    public sealed partial class Match
    {
        /// <summary>This match warns before tapped specials (Rules.SpecialTelegraph; a test may turn it off).</summary>
        public bool Telegraph = Rules.SpecialTelegraph;
        /// <summary>Tapped specials warn now: the one-tap game with the warning on, outside the guided match.</summary>
        public bool TeleOn => OneTap && Telegraph && Tut == null; // the guided match's lessons keep the old instant tap
        /// <summary>Set while a warned special is being cast, so the cast goes even if its target walked away (Bonk's slam).</summary>
        bool teleFire;

        /// <summary>How long h's special waits before its cast starts: one number for every hero. Moa too: her shield on a friend
        /// shows no red warning (it hurts nobody) but she stands and flashes as long, so no special is quicker than another.</summary>
        public static float TeleTime(HeroId id) => Rules.TelegraphTopUp && OwnWarning(id) > 0 ? 0 : Rules.TelegraphTime;

        /// <summary>The red warning h's special already shows at its cast before anything can land, its aim already fixed: Tyra's draw,
        /// Bram's falling anvil, Blast's fuse, Bonk's wind-up (0 = none: Knut's hook, Tikta's needles and Fenn's homing wolf hit at
        /// once, Moa's shield is at once).</summary>
        public static float OwnWarning(HeroId id) => id == HeroId.Tyra ? TyraS.Windup : id == HeroId.Bram ? BramS.DropDelay
            : id == HeroId.Blast ? BlastS.Delay : id == HeroId.Bonk ? BonkS.SlamWindup : 0;

        /// <summary>A special that warns by itself (OwnWarning) is cast at once, as before, with the same flash on the hero (Telegraph
        /// event, V = its own warning) so every special reads alike.</summary>
        bool CastWithFlash(Unit h, Unit tgt, float ang, float k)
        {
            if (!TryCastSpecial(h, ang, k)) return false;
            Emit(EventType.Telegraph, h, tgt, OwnWarning(h.Hero), (byte)h.Hero, false, castX, castY);
            return true;
        }

        /// <summary>Is h's tapped special waiting in its warning now?</summary>
        public static bool Telegraphing(Unit h) => h != null && h.TeleAt >= 0;

        /// <summary>The forms that land on a spot (the spot is locked); the rest fly along a line (the direction is locked).</summary>
        static bool PlacedForm(HeroId id) => id == HeroId.Bram || id == HeroId.Blast || id == HeroId.Fenn || id == HeroId.Moa;

        /// <summary>The tap's start: the aim (ang, k as TryCastSpecial takes them; ang NaN for Bonk's slam round him) is locked, the
        /// warning shows, the cast follows in TeleTime. tgt is the picked target (only for the event).</summary>
        bool StartTelegraph(Unit h, Unit tgt, float ang, float k)
        {
            float tt = TeleTime(h.Hero);
            h.TeleAng = ang; h.TeleK = k; h.TeleX = h.X; h.TeleY = h.Y;
            float ex = h.X, ey = h.Y;
            switch (h.Hero)
            {
                case HeroId.Bram:
                    AnvilPoint(h, ang, k, out h.TeleX, out h.TeleY);
                    WarnCircle(h, h.TeleX, h.TeleY, BramS.DropRadius, tt);
                    ex = h.TeleX; ey = h.TeleY;
                    break;
                case HeroId.Blast:
                    BombPoint(h, ang, k, out h.TeleX, out h.TeleY);
                    WarnCircle(h, h.TeleX, h.TeleY, BlastS.Radius, tt);
                    ex = h.TeleX; ey = h.TeleY;
                    break;
                case HeroId.Fenn:
                {   // where the thrown wolf lands: as CastFenn works it out (a wall or a base ends the throw)
                    AimPoint(h, ang, k, FennS.ThrowReach, out float gx, out float gy);
                    float ga = SimMath.Atan2(gy - h.Y, gx - h.X), gd = WolfReach(h.X, h.Y, ga, SimMath.Hypot(gx - h.X, gy - h.Y));
                    h.TeleX = h.X + SimMath.Cos(ga) * gd; h.TeleY = h.Y + SimMath.Sin(ga) * gd;
                    WarnLine(h, ga, gd, Defs.Wolf.Radius, tt);
                    WarnCircle(h, h.TeleX, h.TeleY, FennS.PounceRadius, tt);
                    ex = h.TeleX; ey = h.TeleY;
                    break;
                }
                case HeroId.Tyra:
                {
                    float len = BoltReach(h, ang);
                    WarnLine(h, ang, len, TyraS.Hitbox, tt);
                    ex = h.X + SimMath.Cos(ang) * len; ey = h.Y + SimMath.Sin(ang) * len;
                    break;
                }
                case HeroId.Knut:
                {   // the anchor has no warning of its own: this one stays while it flies out
                    float len = TowerReach(h.X, h.Y, ang, Map.ShotReach(h.X, h.Y, ang, KnutS.HookDistance), KnutS.HookRadius);
                    WarnLine(h, ang, len, KnutS.HookRadius, tt + KnutS.OutTime);
                    ex = h.X + SimMath.Cos(ang) * len; ey = h.Y + SimMath.Sin(ang) * len;
                    break;
                }
                case HeroId.Tikta:
                {
                    float len = TowerReach(h.X, h.Y, ang, NeedleReach, TiktaS.LineHitbox);
                    WarnLine(h, ang, len, 0.25f, tt);
                    ex = h.X + SimMath.Cos(ang) * len; ey = h.Y + SimMath.Sin(ang) * len;
                    break;
                }
                case HeroId.Bonk:
                    WarnCircle(h, h.X, h.Y, BonkS.SlamRadiusOneTap, tt);
                    break;
                case HeroId.Moa: // her honey field on the friend's spot: no red warning (it hurts nobody, enemy bots must not run from it)
                    AimPoint(h, ang, k, MoaS.Range, out h.TeleX, out h.TeleY, 0f);
                    ex = h.TeleX; ey = h.TeleY;
                    break;
                default: // a hero with no warned form (Hilde, Oaken): cast at once
                    return TryCastSpecial(h, ang, k);
            }
            h.TeleAt = Time + tt;
            if (!float.IsNaN(ang)) { h.Facing = ang; h.AimT = Time + tt + 0.2f; }
            AddStatus(h, St.Casting, tt); AddStatus(h, St.Disarm, tt); Channel(h, tt); // stands, no swing, a stun cuts it
            Emit(EventType.Telegraph, h, tgt, tt, (byte)h.Hero, false, ex, ey);
            return true;
        }

        /// <summary>Per tick for every living real hero: the warned special goes when its warning is over, at the locked aim.</summary>
        void TelegraphTick(Unit h)
        {
            if (h.TeleAt < 0 || Time < h.TeleAt - 1e-4f) return;
            h.TeleAt = -1;
            if (!h.Alive || h.Has(St.Stun) || State != MatchState.Play) return;
            float ang = h.TeleAng, k = h.TeleK;
            if (PlacedForm(h.Hero))
            {   // the locked spot, from wherever a push left the hero (it stands still in the warning)
                float dx = h.TeleX - h.X, dy = h.TeleY - h.Y, d = SimMath.Hypot(dx, dy);
                float reach = h.Hero == HeroId.Bram ? BramS.Reach : h.Hero == HeroId.Blast ? BlastS.Reach : h.Hero == HeroId.Moa ? MoaS.Range : FennS.ThrowReach;
                if (d > 1e-4f) ang = SimMath.Atan2(dy, dx);
                k = SimMath.Clamp(d / reach, 0, 1);
            }
            h.RemoveAll(St.Casting); h.RemoveAll(St.Disarm);
            teleFire = true;
            TryCastSpecial(h, ang, k);
            teleFire = false;
        }

        /// <summary>The warning is cut (a stun, root or knockback, InterruptSpecial): no cast, the charge stays. True if there was one.</summary>
        bool CancelTelegraph(Unit h)
        {
            if (h.TeleAt < 0) return false;
            h.TeleAt = -1;
            h.RemoveAll(St.Disarm);
            return true;
        }
    }
}
