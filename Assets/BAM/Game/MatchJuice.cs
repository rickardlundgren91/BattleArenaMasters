using BAM.Core;
using BAM.Sim;
using UnityEngine;
using EventType = BAM.Sim.EventType;

namespace BAM.Game
{
    /// <summary>
    /// The old game's hit feel (ABC2 SimLoop.cs updateJuice, HeroJuice.cs): camera shake from "trauma", a short hit-stop and a
    /// zoom punch. Shake when the player is hit, when a hero or a tower near it is hit hard, when a hero near it dies, when a
    /// tower falls, on the player's big hits (15 % of the target's health or more) and kills, and when it casts its special.
    /// Hit-stop only for the player's own death and its killing blows (Rickard 2026-10-18: longer ones felt like a freeze).
    /// Short buzzes with it (Haptics.cs, via BamHaptics, off when Settings has Vibration off): a big hit on the player (15 % or
    /// more), its death, a kill, its special and a hook catching a hero; none for its own hits (Rickard 2026-10-26: vibration,
    /// but not overused). Only if the player turned on Settings > Vibrate on my hits (off by default; tester feedback, Ski
    /// 2026-11-07): a light buzz for each own auto attack that hits an enemy it can see. One pulse per tick, the strongest, since BamHaptics drops anything within 0.08 s of the last.
    /// Picture only: the hit-stop feeds ticks slower, the rules never see a shorter tick.
    /// </summary>
    sealed class MatchJuice
    {
        // shake: the old amp was trauma² × 3 % of the screen width, here of the 11.5 tiles across
        const float ShakeWidth = 11.5f * 0.03f, TraumaFall = 1.7f;
        float trauma, t, stopT, zoomT = 9f, zoomA;
        bool landed; // the guided match's arena half: Tyra has come down from the island's boost pad
        Match m;

        public Vector2 Shake { get; private set; }
        /// <summary>1 = no punch; above 1 the picture is that much bigger.</summary>
        public float Zoom { get; private set; } = 1f;
        public bool Stopping => stopT > 0f;

        public void Begin(Match match)
        {
            m = match;
            trauma = 0f; stopT = 0f; zoomT = 9f; zoomA = 0f; Shake = Vector2.zero; Zoom = 1f;
            landed = false;
        }

        void AddTrauma(float v) => trauma = Mathf.Min(1f, Mathf.Max(trauma, v));
        void Stop(float s) => stopT = Mathf.Max(stopT, s);
        void Punch(float a) { if (zoomT > 0.3f || a > zoomA) { zoomT = 0f; zoomA = a; } }

        static float ZoomFor(HeroId h)
        {
            switch (h)
            {
                case HeroId.Bram: return 0.12f;
                case HeroId.Knut: case HeroId.Blast: case HeroId.Bonk: return 0.10f;
                case HeroId.Tyra: return 0.09f;
                default: return 0.08f;
            }
        }

        internal static bool Heavy(HeroId h) => h == HeroId.Knut || h == HeroId.Blast || h == HeroId.Bonk || h == HeroId.Hilde || h == HeroId.Oaken;

        static float Dist(Unit a, Unit b) => Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        /// <summary>After every tick: what happened in it.</summary>
        public void AfterStep()
        {
            if (m.Tut != null && m.Tut.Island) return;
            // the guided match's arena half begins with Tyra coming down from the island's boost pad (Tutorial.Air): a thump
            // as she lands
            if (!landed && m.Tut != null && m.Tut.Air <= 0f) { landed = true; AddTrauma(0.45f); }
            var p = m.Player;
            var ev = m.Events;
            float buzz = 0f;
            for (int i = 0; i < ev.Count; i++)
            {
                var e = ev[i];
                switch (e.Type)
                {
                    case EventType.Damage:
                    {
                        var b = e.B;
                        if (b == null || e.V < 1f || b.MaxHp <= 0f || MatchViews.IsTick(ev, i)) break; // a burn or poison tick never shakes
                        float f = Mathf.Min(1f, e.V / b.MaxHp);
                        bool mine = e.A != null && (e.A == p || e.A.Owner == p);
                        // the light hit buzz, weakest of all, so a kill or a big hit in the same tick still wins
                        if (e.A == p && e.Dmg == DmgKind.Auto && b.Team != p.Team && BamSettings.HitVibration && m.IsVisibleTo(b, p.Team))
                            buzz = Mathf.Max(buzz, BamHaptics.LightHit);
                        if (b == p) { if (f >= 0.03f) AddTrauma(Mathf.Min(0.6f, 0.15f + f * 1.8f)); if (f >= 0.15f) buzz = Mathf.Max(buzz, 0.3f); } // only a big hit buzzes (Rickard 2026-10-26: vibration, but not overused)
                        else if (mine && b.IsRealHero && b.Team != p.Team)
                        {
                            // HeroJuice hit confirm: every melee hit bites a little, a big hit shakes more
                            if (f >= 0.15f) AddTrauma((Heavy(p.Hero) ? 0.22f : 0.14f) + f * 0.6f);
                            else if (p.Range <= 2.4f) AddTrauma(0.05f);
                        }
                        else if (b.IsRealHero && p.Alive && Dist(b, p) < 6f && f >= 0.06f) AddTrauma(f * 1.4f);
                        else if (b.Kind == UnitKind.Tower && p.Alive && Dist(b, p) < 6f && f >= 0.04f) AddTrauma(f * 2f);
                        break;
                    }
                    case EventType.Explosion:
                    {
                        // Bonk's club into the ground shakes the screen a little when it is near (Rickard 2026-10-26)
                        var k = (ProjKind)e.Kind;
                        // Bram's Anvil Drop landing near you shakes like Bonk's slam (its red warning shows to everyone, so it is
                        // seen); a short hit-stop only when it hit someone (Rickard 2026-10-30 "måste synas mer")
                        if (k == ProjKind.BramHammerSpecial && p.Alive)
                        {
                            float ax = e.X - p.X, ay = e.Y - p.Y;
                            if (ax * ax + ay * ay < 36f)
                            {
                                bool hit = false;
                                for (int j = 0; j < i && !hit; j++) hit = ev[j].Type == EventType.Hit && ev[j].A == e.A && (ProjKind)ev[j].Kind == ProjKind.BramHammerSpecial && ev[j].Shot == 0;
                                AddTrauma(hit ? 0.5f : 0.3f);
                                if (hit) Stop(0.07f);
                            }
                            break;
                        }
                        // a club swung from where the player cannot see Bonk (a miss in grass) shakes nothing: swinging does not reveal (BONK-09)
                        if ((k == ProjKind.BonkSlam || k == ProjKind.BonkClub) && p.Alive && (e.A == null || m.IsVisibleTo(e.A, p.Team)))
                        {
                            float dx = e.X - p.X, dy = e.Y - p.Y;
                            if (dx * dx + dy * dy < 36f)
                            {
                                // MLBB-weight (Rickard 2026-10-26 "känns inte mäktig"): a real shake and a short hit-stop on the slam.
                                // A club swing that hit nobody shakes as a plain one and never stops: a hit-stop reads as a hit (BONK-11)
                                bool landed = k == ProjKind.BonkSlam;
                                for (int j = 0; j < i && !landed; j++) landed = ev[j].Type == EventType.Hit && ev[j].A == e.A && (ProjKind)ev[j].Kind == ProjKind.BonkClub;
                                AddTrauma(k == ProjKind.BonkSlam ? 0.55f : e.Big && landed ? 0.4f : 0.22f);
                                if (landed && (k == ProjKind.BonkSlam || e.Big)) Stop(k == ProjKind.BonkSlam ? 0.08f : 0.06f);
                            }
                        }
                        break;
                    }
                    case EventType.Knock:
                    {
                        // Bonk's boulder lands on someone near you: a thump and a tiny freeze
                        if (e.B != null && p.Alive && Dist(e.B, p) < 7f) { AddTrauma(e.B == p ? 0.5f : 0.3f); Stop(0.05f); }
                        break;
                    }
                    case EventType.Death:
                    {
                        var b = e.B;
                        if (b == null || !b.IsRealHero) break;
                        if (b == p) { AddTrauma(0.55f); Stop(0.05f); buzz = Mathf.Max(buzz, 0.6f); }
                        else if (e.A != null && (e.A == p || e.A.Owner == p)) { AddTrauma(0.4f); Stop(0.05f); buzz = Mathf.Max(buzz, 0.8f); }
                        else if (Dist(b, p) < 7f) AddTrauma(0.4f);
                        break;
                    }
                    case EventType.TowerDown:
                        AddTrauma(0.75f); Punch(0.1f);
                        break;
                    case EventType.Cast:
                        if (e.A == p) { AddTrauma(0.3f); Punch(ZoomFor(p.Hero)); buzz = Mathf.Max(buzz, 0.4f); }
                        break;
                    case EventType.Pull:
                    {
                        // Knut's anchor catching a hero: a signature moment, for everyone who sees it (Rickard 2026-11-02, the hook
                        // feel list): Knut's player and the caught one get the full stop, shake and buzz, a player near it a shorter stop
                        var b = e.B;
                        if (b == null || !b.IsRealHero) break;
                        if (e.A == p) { Stop(0.1f); AddTrauma(0.4f); Punch(0.06f); buzz = Mathf.Max(buzz, 0.6f); }
                        else if (b == p) { Stop(0.1f); AddTrauma(0.5f); buzz = Mathf.Max(buzz, 0.7f); }
                        else if (p.Alive && Dist(b, p) < 7f && m.IsVisibleTo(b, p.Team)) { Stop(0.07f); AddTrauma(0.2f); }
                        break;
                    }
                }
            }
            // one buzz for the whole tick: the strongest wins (a kill and its killing hit must not cancel each other)
            if (buzz > 0f) BamHaptics.Pulse(buzz);
        }

        /// <summary>Every frame: lets the shake and the punch die down.</summary>
        public void Frame(float dt)
        {
            t += dt; zoomT += dt;
            stopT = Mathf.Max(0f, stopT - dt);
            trauma = Mathf.Max(0f, trauma - dt * TraumaFall);
            float amp = trauma * trauma * ShakeWidth;
            Shake = new Vector2(amp * (Mathf.Sin(t * 53f) + 0.6f * Mathf.Sin(t * 31.7f + 1f)) / 1.6f,
                                amp * (Mathf.Sin(t * 47.3f + 2f) + 0.6f * Mathf.Sin(t * 27.1f)) / 1.6f);
            float z = zoomT;
            float k = z < 0.09f ? Smooth(z / 0.09f) : z < 0.22f ? 1f : z < 0.65f ? 1f - Smooth((z - 0.22f) / 0.43f) : 0f;
            Zoom = 1f + zoomA * k;
        }

        static float Smooth(float x) => x * x * (3f - 2f * x);

        /// <summary>Moves the match camera after it has placed itself this frame.</summary>
        public void Apply(Camera cam)
        {
            if (Shake != Vector2.zero) cam.transform.position += cam.transform.right * Shake.x + cam.transform.up * Shake.y;
            if (Zoom != 1f) cam.orthographicSize /= Zoom;
        }
    }
}
