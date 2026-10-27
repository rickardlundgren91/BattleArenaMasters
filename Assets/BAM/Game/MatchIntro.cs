using BAM.Art;
using BAM.Audio;
using BAM.Sim;
using UnityEngine;

namespace BAM.Game
{
    /// <summary>
    /// The old game's match intro (ABC2 View3DIntro.cs, View3DTeleport.cs; Rickard 2026-10-16): during the countdown one camera move
    /// without a cut. It starts low in the south behind the player's three heroes with a long lens, so their tower stands big
    /// behind them; they beam in together and get their name and role above their heads. Then it flies over their heads to the
    /// tower, is pulled up towards the sky and lands on the normal match camera, blending the perspective lens into the
    /// orthographic one, so the last frame is the normal picture. A tap makes it finish fast. Picture only: the rules and the
    /// countdown are untouched. Not in the guided match.
    /// </summary>
    sealed class MatchIntro
    {
        const float Hold = 1.1f, Sweep = 2.1f, Total = Hold + Sweep;
        const float Past = 1.7f, Climb = 2.1f, Top = 2.6f;
        const float TanY = 0.28f;        // the lens: half the frame height over the distance (a long lens, the tower looks big)
        const float OverH = 3.4f;        // how high the camera passes over the heads
        const float StrikeAt = 0.4f;     // s into the intro: the heroes beam in, all at once
        static readonly float[] KeyT = { 0f, Hold, Past, Climb, Top, Total };

        readonly float[,] key = new float[6, 6];
        readonly float[] pose = new float[6];
        readonly Unit[] heroes = new Unit[3];
        int heroCount;
        float t, lastNow;
        bool active, skip, struck, ownsProjection;
        Match m;
        MatchViews views;

        public bool Active => active;
        public float Progress => Mathf.Clamp01((t - Hold) / Sweep);
        /// <summary>The name tags: in during the close shot, out as the sweep starts.</summary>
        public float LabelAlpha => Mathf.Clamp01(t / 0.3f) * (1f - Mathf.Clamp01(Progress / 0.25f));
        public int HeroCount => heroCount;
        public Unit Hero(int i) => heroes[i];

        public void Begin(Match match, MatchViews matchViews)
        {
            m = match; views = matchViews;
            t = 0f; skip = false; struck = false;
            lastNow = Time.unscaledTime;
            heroCount = 0;
            active = m.Tut == null && !m.HeroTest && m.State == MatchState.Countdown && -m.Time >= 2f && m.Player != null;
            if (!active) return;
            for (int i = 0; i < m.Heroes.Count && heroCount < 3; i++)
                if (m.Heroes[i].Team == m.Player.Team && m.Heroes[i].Alive) heroes[heroCount++] = m.Heroes[i];
            if (heroCount == 0) { active = false; return; }
            Plan();
            views.Hidden = HiddenUnit;
        }

        public void End(Camera cam)
        {
            Finish(cam);
            m = null; views = null;
        }

        public void Skip() { if (active) skip = true; }

        // before the beam has brought a hero in, it is not drawn
        bool HiddenUnit(Unit u)
        {
            if (!active || struck) return false;
            for (int i = 0; i < heroCount; i++) if (heroes[i] == u) return true;
            return false;
        }

        void Finish(Camera cam)
        {
            if (views != null) views.Hidden = null;
            active = false;
            if (ownsProjection && cam != null) { cam.ResetProjectionMatrix(); ownsProjection = false; }
        }

        /// <summary>After MatchCamera.Frame: while the intro plays, it takes the camera over from the match pose; frozen while paused.</summary>
        public void Frame(Camera cam, bool paused = false)
        {
            if (!active) return;
            if (m.State != MatchState.Countdown) { Finish(cam); return; }
            // paused: it holds its pose (no clock, no beam-in sound behind the pause menu) and goes on from there on Resume
            float now = Time.unscaledTime, dt = paused ? 0f : Mathf.Min(0.1f, now - lastNow);
            lastNow = now;
            float left = -m.Time; // never later than the countdown reaching 0
            float speed = skip ? 5f : left > 0.05f ? Mathf.Max(1f, (Total - t) / left) : 50f;
            t += dt * speed;
            if (!struck && t >= StrikeAt) Strike();
            if (t >= Total) { Finish(cam); return; }

            // the last pose is the match camera as it stands this frame: where its centre ray meets the ground
            Transform ct = cam.transform;
            Vector3 cp = ct.position, fw = ct.forward;
            float dEnd = fw.y < -0.01f ? -cp.y / fw.y : 20f;
            Vector3 g = cp + fw * dEnd;
            float el = Mathf.Asin(Mathf.Clamp(-fw.y, -1f, 1f));
            float orthoSize = cam.orthographicSize, aspect = cam.aspect, near = cam.nearClipPlane, far = cam.farClipPlane;
            SetKey(5, g.x, 0f, g.z, dEnd, el * Mathf.Rad2Deg, 0f);

            Pose(t);
            float e = 0f;
            if (t > Top) { float v = (t - Top) / (Total - Top); e = v * v * v * (v * (v * 6f - 15f) + 10f); }
            var tgt = new Vector3(pose[0], pose[1], pose[2]);
            float d = Mathf.Exp(pose[3]), pitch = Mathf.Min(86f * Mathf.Deg2Rad, pose[4]), yaw = pose[5];
            var f = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            ct.position = tgt - f * (Mathf.Cos(pitch) * d) + Vector3.up * (Mathf.Sin(pitch) * d);
            ct.LookAt(tgt, Vector3.up);

            // the frustum as slopes at the target: from the long lens to the match frame; scaled by 1/d the perspective
            // matrix has w = 1 at the target like the orthographic one, so a plain blend lands exactly on the match projection
            float tx0 = TanY * aspect;
            float tanX = Mathf.Lerp(tx0, orthoSize * aspect / dEnd, e), tanY = Mathf.Lerp(TanY, orthoSize / dEnd, e);
            const float n = 0.1f;
            var persp = Matrix4x4.Frustum(-tanX * n, tanX * n, -tanY * n, tanY * n, n, 400f);
            var ortho = Matrix4x4.Ortho(-orthoSize * aspect, orthoSize * aspect, -orthoSize, orthoSize, near, far);
            float wb = e * e, ks = (1f - wb) / d;
            var P = new Matrix4x4();
            for (int i = 0; i < 16; i++) P[i] = persp[i] * ks + ortho[i] * wb;
            cam.projectionMatrix = P;
            ownsProjection = true;
        }

        // the moment they arrive: a flash of light at each, one sound
        void Strike()
        {
            struck = true;
            if (views != null) views.Hidden = null;
            for (int i = 0; i < heroCount; i++)
            {
                Vector3 at = views.DrawnAt(heroes[i]);
                ArtKit.PlayFx(FxKind.Respawn, at, 1.6f, heroes[i].Team);
                ArtKit.PlayFx(FxKind.LevelUp, at, 1.2f, heroes[i].Team);
            }
            BamAudio.Play(SfxId.Respawn, 0.9f);
        }

        // the path: low in the south behind the heroes, over their heads to the tower, up towards the sky above it
        void Plan()
        {
            Vector3 hc = Vector3.zero;
            for (int i = 0; i < heroCount; i++) hc += Coords.Of(heroes[i]);
            hc /= heroCount;
            float hw = 0f;
            for (int i = 0; i < heroCount; i++) hw = Mathf.Max(hw, Mathf.Abs(Coords.Of(heroes[i]).x - hc.x));
            var tw = m.Towers[m.Player.Team];
            Vector3 tp = tw != null ? Coords.Of(tw) : hc + Vector3.forward * 3f;
            float aspect = Mathf.Max(0.3f, (float)Screen.width / Mathf.Max(1, Screen.height));
            // first shot: from far behind, aimed between the heroes and the tower so the tower stands big behind them
            float d0 = Mathf.Clamp((hw + 1.2f) / (TanY * aspect), 8f, 13f);
            Vector3 c0 = Vector3.Lerp(hc, tp, 0.3f);
            SetKey(0, c0.x, 1.3f, c0.z, d0, 5f, 0f);
            SetKey(1, c0.x, 1.3f, c0.z, d0 * 0.85f, 7f, 0f);
            // right over the heads, looking ahead at the tower
            Vector3 dir = tp - hc; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            float yaw = Mathf.Atan2(dir.x, dir.z), dist = Mathf.Max(1.5f, dir.magnitude), up = OverH - 1.4f;
            SetKey(2, tp.x, 1.4f, tp.z, Mathf.Sqrt(dist * dist + up * up), Mathf.Atan2(up, dist) * Mathf.Rad2Deg, yaw);
            // in front of the tower, then pulled up towards the sky above it
            SetKey(3, tp.x, 1.5f, tp.z, 1.9f, 30f, yaw);
            SetKey(4, tp.x, 0.3f, tp.z, 11f, 74f, yaw);
        }

        void SetKey(int i, float x, float h, float z, float d, float pitchDeg, float yaw)
        {
            key[i, 0] = x; key[i, 1] = h; key[i, 2] = z; key[i, 3] = Mathf.Log(d); key[i, 4] = pitchDeg * Mathf.Deg2Rad; key[i, 5] = yaw;
        }

        // a smooth curve through the poses (Hermite, tangents from the neighbours, at rest at both ends)
        void Pose(float time)
        {
            int n = KeyT.Length, i = 0;
            while (i < n - 2 && time > KeyT[i + 1]) i++;
            float t0 = KeyT[i], t1 = KeyT[i + 1], h = t1 - t0, s = Mathf.Clamp01((time - t0) / h), s2 = s * s, s3 = s2 * s;
            float h00 = 2 * s3 - 3 * s2 + 1, h10 = s3 - 2 * s2 + s, h01 = -2 * s3 + 3 * s2, h11 = s3 - s2;
            for (int k = 0; k < 6; k++)
            {
                float m0 = i == 0 ? 0f : Slope(i, k), m1 = i + 1 == n - 1 ? 0f : Slope(i + 1, k);
                pose[k] = h00 * key[i, k] + h10 * h * m0 + h01 * key[i + 1, k] + h11 * h * m1;
            }
        }

        // the slope at pose i, kept monotone (no overshoot past a pose, so the camera never backs up or dips)
        float Slope(int i, int k)
        {
            float a = (key[i, k] - key[i - 1, k]) / (KeyT[i] - KeyT[i - 1]), b = (key[i + 1, k] - key[i, k]) / (KeyT[i + 1] - KeyT[i]);
            if (a * b <= 0f) return 0f;
            float mm = (a + b) / 2f;
            return Mathf.Sign(mm) * Mathf.Min(Mathf.Abs(mm), 3f * Mathf.Min(Mathf.Abs(a), Mathf.Abs(b)));
        }
    }
}
