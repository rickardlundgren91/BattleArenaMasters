using BAM.Sim;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// Portrait camera looking down at the map from behind the player's side, about 11.5 tiles across at the focus.
    /// Follows the player's hero a little ahead (towards the enemy); while it is dead it follows a living teammate and comes
    /// back at the respawn. The guided match frames Tyra and the enemy tower together (the old game's tutCamY).
    /// </summary>
    sealed class MatchCamera
    {
        const float Pitch = 57f, Fov = 35f, TilesAcross = 11.5f;
        const float Lead = 1.2f;        // the focus sits this far ahead of the hero, so more of the way forward is seen
        const float Over = 3f;          // the focus may pass the map edge by this much (the hero is not stuck under the buttons)
        // the guided match: the enemy tower's foot this high on screen (share from the top), Tyra at most this low
        const float TutTowerAt = 0.27f, TutHeroAt = 0.75f;

        readonly Camera cam;
        Match m;
        Vector3 focus;
        Unit watched;
        float distance;

        public Vector3 Focus => focus;
        public Camera Camera => cam;

        public MatchCamera(Camera camera) { cam = camera; }

        public void Begin(Match match)
        {
            m = match;
            watched = null;
            cam.fieldOfView = Fov;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 120f;
            UpdateDistance();
            focus = Target(out _);
            Place();
        }

        public void End() { m = null; watched = null; }

        /// <summary>Length of map the screen shows top to bottom at the focus (about; the view is a trapezoid).</summary>
        float ViewLength => 2f * distance * Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) / Mathf.Sin(Pitch * Mathf.Deg2Rad);

        public void Frame(float alpha, float dt, bool over)
        {
            if (m == null) return;
            UpdateDistance();
            Vector3 target = Target(out float speed);
            if (over) speed = 3.5f;
            float k = 1f - Mathf.Exp(-speed * dt);
            focus += (target - focus) * k;
            float halfW = TilesAcross * 0.5f, halfH = ViewLength * 0.42f;
            focus.x = Mathf.Clamp(focus.x, halfW - Over, m.Map.W - halfW + Over);
            if (m.Map.H > 2f * halfH) focus.z = Mathf.Clamp(focus.z, halfH - Over, m.Map.H - halfH + Over);
            Place();
        }

        // far enough back that TilesAcross fit across the screen at the focus (phones differ in shape)
        void UpdateDistance()
        {
            float aspect = Mathf.Max(0.3f, cam.aspect);
            distance = TilesAcross * 0.5f / (Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad) * aspect);
        }

        Vector3 Target(out float speed)
        {
            speed = 6f;
            var p = m.Player;
            if (m.State == MatchState.Over)
            {
                // the fallen tower
                var tw = m.Towers[m.Winner == Teams.Player ? Teams.Enemy : Teams.Player];
                if (tw != null) return Coords.Of(tw);
            }
            if (m.Tut != null && m.Tut.Island)
            {
                // the training island: framed whole until the bridge opens, then on Fenn (also through the pad's flight)
                speed = 4f;
                return m.Tut.CamHold ? Coords.World(m.Tut.CamX, m.Tut.CamY) : Coords.Of(p);
            }
            if (m.Tut != null && p.Alive) return TutTarget(p, ref speed);
            if (p.Alive) { watched = null; return Coords.Of(p) + new Vector3(0f, 0f, Lead); }
            // spectating: the teammate nearest to where the player fell, kept until it dies
            if (watched == null || !watched.Alive)
            {
                watched = null;
                float best = float.MaxValue;
                for (int i = 0; i < m.Heroes.Count; i++)
                {
                    var h = m.Heroes[i];
                    if (h == p || !h.Alive || h.Team != p.Team) continue;
                    float d = (h.X - p.X) * (h.X - p.X) + (h.Y - p.Y) * (h.Y - p.Y);
                    if (d < best) { best = d; watched = h; }
                }
            }
            if (watched == null) return Coords.World(p.SpawnX, p.SpawnY) + new Vector3(0f, 0f, Lead);
            return Coords.Of(watched) + new Vector3(0f, 0f, Lead);
        }

        Vector3 TutTarget(Unit p, ref float speed)
        {
            var tut = m.Tut;
            Vector3 hero = Coords.Of(p);
            var tw = tut.Tower;
            if (tw == null) return hero + new Vector3(0f, 0f, Lead);
            float towerZ = m.Map.H - tw.Y, viewH = ViewLength;
            bool opening = tut.Step == Tutorial.StepMove && m.Time < Tutorial.OpeningTime;
            speed = opening ? 2.5f : 4f;
            // the opening: rest on the tower a little above the middle, then glide down the lane to Tyra
            if (tut.Step == Tutorial.StepMove && m.Time < Tutorial.OpenHold) return new Vector3(m.Map.W * 0.5f, 0f, towerZ - 0.1f * viewH);
            float onTower = towerZ - (0.5f - TutTowerAt) * viewH;
            float lead = hero.z + (TutHeroAt - 0.5f) * viewH;
            return new Vector3(hero.x, 0f, Mathf.Max(hero.z + Lead, Mathf.Min(onTower, lead)));
        }

        void Place()
        {
            var rot = Quaternion.Euler(Pitch, 0f, 0f);
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * distance, rot);
        }
    }
}
