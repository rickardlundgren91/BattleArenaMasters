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
    /// Short buzzes with it (Haptics.cs, via BamHaptics, off when Settings has Vibration off): hit for 5 % or more, death, kill,
    /// cast; one pulse per tick, the strongest, since BamHaptics drops anything within 0.08 s of the last.
    /// Picture only: the hit-stop feeds ticks slower, the rules never see a shorter tick.
    /// </summary>
    sealed class MatchJuice
    {
        // shake: the old amp was trauma² × 3 % of the screen width, here of the 11.5 tiles across
        const float ShakeWidth = 11.5f * 0.03f, TraumaFall = 1.7f;
        float trauma, t, stopT, zoomT = 9f, zoomA;
        Match m;

        public Vector2 Shake { get; private set; }
        /// <summary>1 = no punch; above 1 the picture is that much bigger.</summary>
        public float Zoom { get; private set; } = 1f;
        public bool Stopping => stopT > 0f;

        public void Begin(Match match)
        {
            m = match;
            trauma = 0f; stopT = 0f; zoomT = 9f; zoomA = 0f; Shake = Vector2.zero; Zoom = 1f;
        }

        void AddTrauma(float v) => trauma = Mathf.Min(1f, Mathf.Max(trauma, v));
        void Stop(float s) => stopT = Mathf.Max(stopT, s);
        void Punch(float a) { if (zoomT > 0.3f || a > zoomA) { zoomT = 0f; zoomA = a; } }

        static float ZoomFor(HeroId h)
        {
            switch (h)
            {
                case HeroId.Bram: return 0.12f;
                case HeroId.Knut: case HeroId.Blast: return 0.10f;
                case HeroId.Tyra: return 0.09f;
                default: return 0.08f;
            }
        }

        static bool Heavy(HeroId h) => h == HeroId.Knut || h == HeroId.Blast;

        static float Dist(Unit a, Unit b) => Mathf.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        /// <summary>After every tick: what happened in it.</summary>
        public void AfterStep()
        {
            if (m.Tut != null && m.Tut.Island) return;
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
                        if (b == null || e.V < 1f || b.MaxHp <= 0f) break;
                        float f = Mathf.Min(1f, e.V / b.MaxHp);
                        bool mine = e.A != null && (e.A == p || e.A.Owner == p);
                        if (b == p) { if (f >= 0.03f) AddTrauma(Mathf.Min(0.6f, 0.15f + f * 1.8f)); if (f >= 0.05f) buzz = Mathf.Max(buzz, 0.3f); }
                        else if (mine && b.IsRealHero && b.Team != p.Team)
                        {
                            // HeroJuice hit confirm: every melee hit bites a little, a big hit shakes more
                            if (f >= 0.15f) AddTrauma((Heavy(p.Hero) ? 0.22f : 0.14f) + f * 0.6f);
                            else if (p.Range <= 2.4f) AddTrauma(0.05f);
                            if (f >= 0.05f) buzz = Mathf.Max(buzz, (Heavy(p.Hero) ? 0.2f : 0.12f) + Mathf.Min(0.3f, f * 1.5f)); // buzz only for >= 5 % of max HP
                        }
                        else if (b.IsRealHero && p.Alive && Dist(b, p) < 6f && f >= 0.06f) AddTrauma(f * 1.4f);
                        else if (b.Kind == UnitKind.Tower && p.Alive && Dist(b, p) < 6f && f >= 0.04f) AddTrauma(f * 2f);
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
                        // Knut's anchor catching a hero: a signature moment
                        if (e.A == p && e.B != null && e.B.IsRealHero) { Stop(0.08f); AddTrauma(0.35f); Punch(0.05f); buzz = Mathf.Max(buzz, 0.6f); }
                        break;
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
