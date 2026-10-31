using BAM.Sim;
using BAM.UI;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// The old game's camera (AbcGame.cs Layout and updateCamera): orthographic, tilted down 55 degrees (elevation asin 0.82),
    /// 11.5 tiles across the camera frame (UiKit.CameraFrame) with the focus 52% down it (Rickard 2026-10-25: "rätt zoom
    /// på kamera"). On a phone the frame is the whole safe area and the picture runs on under the status and navigation bars;
    /// a wider screen (a foldable opened up, a tablet) keeps the phone's tile size and lane length and simply shows more to
    /// the sides (the arena's edge and the water round it) instead of the old game's black edges (a Z Fold tester, 2026-10-31).
    /// Follows the player's hero a little ahead (towards the enemy); while it is dead it follows a living teammate and comes
    /// back at the respawn. The guided match frames Tyra and the enemy tower together (the old game's tutCamY).
    /// </summary>
    sealed class MatchCamera
    {
        // Back: orthographic framing ignores distance, but the High shadow distance (30 m, BamQuality) is measured from the camera,
        // so the camera stays close: the ground then lies about 9-28 m away on a long phone (the island's cloud sea at about 38)
        const float TilesAcross = 11.5f, Back = 18f, FocusDown = 0.52f;
        static readonly float Pitch = Mathf.Asin(0.82f) * Mathf.Rad2Deg;
        const float Lead = 1.2f;        // the focus sits this far ahead of the hero, so more of the way forward is seen
        const float Over = 3f;          // the focus may pass the map edge by this much (the hero is not stuck under the buttons)
        // the guided match: the enemy tower's foot this high on screen (share from the top), Tyra at most this low
        const float TutTowerAt = 0.27f, TutHeroAt = 0.75f;

        static readonly Color IslandSky = new Color32(0x79, 0xb9, 0xee, 0xff);
        readonly Camera cam;
        Color menuBackground;
        bool skyChanged;
        Match m;
        Vector3 focus;
        Unit watched;
        Rect frame;                     // the standing frame in screen pixels
        Vector2 screen;
        float upp = 0.02f, lift;        // world units per screen pixel; the camera's height above the focus along its own up

        /// <summary>Screen pixels one tile wide at the focus (the HUD's overhead marks size themselves by it).</summary>
        public float PixelsPerTile => upp > 1e-6f ? 1f / upp : 1f;

        public Vector3 Focus => focus;
        public Camera Camera => cam;

        public MatchCamera(Camera camera) { cam = camera; }

        public void Begin(Match match)
        {
            m = match;
            watched = null;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            // the training island floats in a light-blue sky above a cloud sea
            if (m.Tut != null && m.Tut.Island) { menuBackground = cam.backgroundColor; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = IslandSky; skyChanged = true; }
            UpdateSize();
            focus = Target(out _);
            Place();
        }

        public void End()
        {
            if (skyChanged) { cam.backgroundColor = menuBackground; skyChanged = false; }
            cam.orthographic = false;
            cam.rect = new Rect(0f, 0f, 1f, 1f);
            frame = default;
            m = null; watched = null;
        }

        /// <summary>Length of map the frame shows top to bottom at the focus (about; the view is a trapezoid).</summary>
        float FrameLength => frame.height * upp / Mathf.Sin(Pitch * Mathf.Deg2Rad);

        public void Frame(float alpha, float dt, bool over)
        {
            if (m == null) return;
            UpdateSize();
            Vector3 target = Target(out float speed);
            if (over) speed = 3.5f;
            float k = 1f - Mathf.Exp(-speed * dt);
            focus += (target - focus) * k;
            // half the width actually seen (more than 11.5 tiles on a wide screen), so a wide view is not pushed off-centre
            float halfW = 0.5f * Mathf.Max(TilesAcross, screen.x * upp), halfH = FrameLength * 0.42f;
            focus.x = 2f * halfW >= m.Map.W + 2f * Over ? m.Map.W * 0.5f : Mathf.Clamp(focus.x, halfW - Over, m.Map.W - halfW + Over);
            if (m.Map.H > 2f * halfH) focus.z = Mathf.Clamp(focus.z, halfH - Over, m.Map.H - halfH + Over);
            Place();
        }

        // The camera draws the whole screen: TilesAcross over the camera frame's width, the focus FocusDown of the way down
        // the frame. Wider than the frame it shows more of the sides at the same scale.
        void UpdateSize()
        {
            float sw = Screen.width, sh = Screen.height;
            Rect f = UiKit.CameraFrame();
            if (f.width < 1f || f.height < 1f || sw < 1f || sh < 1f) return;
            if (f != frame || sw != screen.x || sh != screen.y)
            {
                frame = f;
                screen = new Vector2(sw, sh);
                cam.rect = new Rect(0f, 0f, 1f, 1f);
            }
            upp = TilesAcross / f.width;
            lift = (0.5f * sh - f.yMax + FocusDown * f.height) * upp;
            cam.orthographicSize = 0.5f * sh * upp;
        }

        // ahead = towards the enemy: up the screen, which online on the top team (Coords.Turned) is down the map's z
        static Vector3 LeadV => new Vector3(0f, 0f, Coords.Turned ? -Lead : Lead);

        Vector3 Target(out float speed)
        {
            speed = 6f;
            var p = m.Player;
            if (m.State == MatchState.Over)
            {
                // the fallen tower
                var tw = m.Towers[Teams.Other(m.Winner)];
                if (tw != null) return Coords.Of(tw);
            }
            if (m.Tut != null && m.Tut.Island)
            {
                // the training island: framed whole until the bridge opens, then on the hero (also through the pad's flight)
                speed = 4f;
                return m.Tut.CamHold ? Coords.World(m.Tut.CamX, m.Tut.CamY) : Coords.Of(p);
            }
            if (m.Tut != null && p.Alive) return TutTarget(p, ref speed);
            if (p.Alive) { watched = null; return Coords.Of(p) + LeadV; }
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
            if (watched == null) return Coords.World(p.SpawnX, p.SpawnY) + LeadV;
            return Coords.Of(watched) + LeadV;
        }

        Vector3 TutTarget(Unit p, ref float speed)
        {
            var tut = m.Tut;
            Vector3 hero = Coords.Of(p);
            var tw = tut.Tower;
            if (tw == null) return hero + LeadV;
            float towerZ = m.Map.H - tw.Y, viewH = FrameLength;
            bool opening = tut.Step == Tutorial.StepMove && m.Time < Tutorial.OpeningTime;
            speed = opening ? 2.5f : 4f;
            // the opening: rest on the tower a little above the middle, then glide down the lane to Tyra
            if (tut.Step == Tutorial.StepMove && m.Time < Tutorial.OpenHold) return new Vector3(m.Map.W * 0.5f, 0f, towerZ - 0.1f * viewH);
            float onTower = towerZ - (0.5f - TutTowerAt) * viewH;
            float lead = hero.z + (TutHeroAt - 0.5f) * viewH;
            return new Vector3(hero.x, 0f, Mathf.Max(hero.z + Lead, Mathf.Min(onTower, lead)));
        }

        // the focus lands FocusDown of the way down the frame: the camera sits a little above it along its own up
        void Place()
        {
            var rot = Quaternion.Euler(Pitch, Coords.Turned ? 180f : 0f, 0f); // online, top team: from the other end of the map
            cam.transform.SetPositionAndRotation(focus - rot * Vector3.forward * Back + rot * Vector3.up * lift, rot);
        }
    }
}
