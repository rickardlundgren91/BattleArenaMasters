using System.Text;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// Weapon Precision 2.0: a debug view of every hero's held weapons, drawn over the match, the hero pages and HERO TEST, and a
    /// text block with each weapon's measured grip error and largest one-frame turn. Only in the Editor and in Development Builds:
    /// a release build never creates it, and while it is off nothing is drawn or measured (HeldGear only checks <see cref="On"/>).
    ///
    /// What is drawn (world space, through everything):
    /// - green cross: the palm the grip is locked to (HeldGear palmL, 0.6 of the way from the wrist to the index knuckle);
    /// - yellow cross: the weapon's own grip point (gripL); with AnimFix.GripLock on it sits on the green one;
    /// - red / green / blue: the weapon's own x / y (along the handle) / z axes from the grip, and a white line handle end to head;
    /// - magenta cross: a two-handed weapon's support point (WeaponDef.Support);
    /// - cyan cross: where a shot would leave now (BodyView.MuzzlePoint, the same point as UnitView.Muzzle);
    /// - for each shot launched in the last second: a cyan line from the muzzle it left to where the rules start it (the 0.08 s ease
    ///   of ProjView.LaunchFrom), and an orange line the way it flies.
    /// </summary>
    public sealed class WeaponDebug : MonoBehaviour
    {
        /// <summary>Drawn and measured now. The Editor's menu flips it; off by default everywhere (it draws over the picture).</summary>
        public static bool On;

        static WeaponDebug inst;
        static string[] names;
        const int Colors = 7, MaxSegs = 400, MaxShots = 24;
        static readonly Color[] Col =
        {
            new Color(0.2f, 1f, 0.3f), new Color(1f, 0.9f, 0.1f), new Color(1f, 0.25f, 0.25f), new Color(0.3f, 1f, 0.3f),
            new Color(0.3f, 0.5f, 1f), new Color(1f, 0.3f, 1f), new Color(0.2f, 1f, 1f),
        };
        const int Palm = 0, Grip = 1, AxX = 2, AxY = 3, AxZ = 4, Sup = 5, Muz = 6;
        // segments per colour, rebuilt each frame into fixed arrays (no allocations)
        readonly Vector3[][] segA = new Vector3[Colors + 2][], segB = new Vector3[Colors + 2][];
        readonly int[] segN = new int[Colors + 2];
        const int White = Colors, Orange = Colors + 1;
        Material[] mats;

        // shots: where each left (muzzle), where the rules start it, which way it flies, when
        static readonly Vector3[] shotFrom = new Vector3[MaxShots], shotSim = new Vector3[MaxShots], shotDir = new Vector3[MaxShots];
        static readonly float[] shotAt = new float[MaxShots];
        static int shotNext;

        readonly StringBuilder sb = new StringBuilder(1024);
        string text = "";
        float nextAt;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || inst != null) return; // release builds: never
            var go = new GameObject("BAM WeaponDebug");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            inst = go.AddComponent<WeaponDebug>();
            inst.useGUILayout = false;
            names = new string[16];
            for (int i = 0; i < names.Length; i++) names[i] = ((HeroId)i).ToString();
            for (int c = 0; c < inst.segA.Length; c++) { inst.segA[c] = new Vector3[MaxSegs]; inst.segB[c] = new Vector3[MaxSegs]; }
        }

        /// <summary>A shot left <paramref name="muzzle"/>; the rules fly it from <paramref name="sim"/> along <paramref name="dir"/>
        /// (ProjView.LaunchFrom calls this only while the view is on).</summary>
        internal static void Shot(Vector3 muzzle, Vector3 sim, Vector3 dir)
        {
            int k = shotNext;
            shotNext = (shotNext + 1) % MaxShots;
            shotFrom[k] = muzzle; shotSim[k] = sim; shotDir[k] = dir; shotAt[k] = Time.unscaledTime;
        }

        /// <summary>The worst grip error and turn start over for every hero (the debug menu's Reset).</summary>
        public static void ResetStats()
        {
            var heroes = BodyView.Heroes;
            for (int i = 0; i < heroes.Count; i++) if (heroes[i] != null && heroes[i].Gear != null) heroes[i].Gear.ResetStats();
        }

        void OnEnable() => RenderPipelineManager.endCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

        void LateUpdate()
        {
            if (!On) return;
            for (int c = 0; c < segN.Length; c++) segN[c] = 0;
            var heroes = BodyView.Heroes;
            for (int h = 0; h < heroes.Count; h++)
            {
                BodyView v = heroes[h];
                if (v == null || !v.isActiveAndEnabled) continue;
                HeldGear g = v.Gear;
                float s = 0.04f * v.ShownHeight;
                if (g != null)
                    for (int i = 0; i < g.WeaponCount; i++)
                    {
                        Transform t = g.WeaponT(i);
                        if (t == null || t.localScale.y < 1e-4f) continue;
                        g.GripPoints(i, out Vector3 grip, out Vector3 palm);
                        Cross(Palm, palm, s);
                        Cross(Grip, grip, s * 0.7f);
                        float ax = 0.15f * v.ShownHeight;
                        Seg(AxX, grip, grip + t.right.normalized * ax);
                        Seg(AxY, grip, grip + t.up.normalized * ax);
                        Seg(AxZ, grip, grip + t.forward.normalized * ax);
                        g.Ends(i, out Vector3 a, out Vector3 b);
                        Seg(White, a, b);
                        if (g.SupportPoint(i, out Vector3 sp)) Cross(Sup, sp, s);
                    }
                if (v.MuzzlePoint(out Vector3 mz)) Cross(Muz, mz, s * 1.3f);
            }
            float now = Time.unscaledTime;
            for (int k = 0; k < MaxShots; k++)
            {
                if (shotAt[k] <= 0f || now - shotAt[k] > 1f) continue;
                Seg(Muz, shotFrom[k], shotSim[k]);
                Seg(Orange, shotSim[k], shotSim[k] + shotDir[k] * 2f);
            }
            if (now >= nextAt) { nextAt = now + 0.25f; Build(); }
        }

        void Cross(int c, Vector3 p, float s)
        {
            Seg(c, p - Vector3.right * s, p + Vector3.right * s);
            Seg(c, p - Vector3.up * s, p + Vector3.up * s);
            Seg(c, p - Vector3.forward * s, p + Vector3.forward * s);
        }

        void Seg(int c, Vector3 a, Vector3 b)
        {
            int n = segN[c];
            if (n >= MaxSegs) return;
            segA[c][n] = a; segB[c][n] = b;
            segN[c] = n + 1;
        }

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (!On || cam == null || cam.cameraType == CameraType.Preview) return;
            if (mats == null) MakeMaterials();
            if (mats == null) return;
            GL.PushMatrix();
            GL.LoadProjectionMatrix(GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            GL.modelview = cam.worldToCameraMatrix;
            for (int c = 0; c < segN.Length; c++)
            {
                if (segN[c] == 0 || mats[c] == null) continue;
                mats[c].SetPass(0);
                GL.Begin(GL.LINES);
                for (int i = 0; i < segN[c]; i++) { GL.Vertex(segA[c][i]); GL.Vertex(segB[c][i]); }
                GL.End();
            }
            GL.PopMatrix();
        }

        // one flat colour per pass on BAM's own unlit material (vertex colours are not in it), drawn last and through everything
        void MakeMaterials()
        {
            if (ArtMaterials.Unlit == null) return;
            mats = new Material[segN.Length];
            for (int c = 0; c < mats.Length; c++)
            {
                var m = ArtMaterials.Instance(ArtMaterials.Unlit);
                m.SetTexture(ArtMaterials.BaseMap, Texture2D.whiteTexture);
                m.mainTexture = Texture2D.whiteTexture;
                m.SetColor(ArtMaterials.BaseColor, c < Colors ? Col[c] : c == White ? Color.white : new Color(1f, 0.55f, 0.1f));
                if (m.HasProperty("_ZTest")) m.SetFloat("_ZTest", (float)CompareFunction.Always);
                m.renderQueue = 4000;
                mats[c] = m;
            }
        }

        // per weapon: the grip's distance from the palm before the lock (now / worst, % of the hero's height) and its largest turn
        // in one frame in the hero's own space (a flip or a spin; a fast swing turns 20-40 degrees a frame at 60 fps)
        void Build()
        {
            sb.Length = 0;
            sb.Append("WEAPONS  lock ").Append(AnimFix.GripLock ? "on" : "off").Append("   grip err % now/worst   turn deg/frame worst\n");
            var heroes = BodyView.Heroes;
            for (int h = 0; h < heroes.Count; h++)
            {
                BodyView v = heroes[h];
                if (v == null || !v.isActiveAndEnabled || v.Gear == null) continue;
                HeldGear g = v.Gear;
                string n = v.HeroIndex >= 0 && v.HeroIndex < names.Length ? names[v.HeroIndex] : "?";
                sb.Append(n).Append(v.Preview ? "(menu)" : "");
                for (int i = 0; i < g.WeaponCount; i++)
                {
                    if (g.WeaponT(i) == null) continue;
                    sb.Append("  ").Append(g.Def(i).Left ? 'L' : 'R').Append(' ');
                    Pct(g.GripErr[i]); sb.Append('/'); Pct(g.GripErrMax[i]);
                    sb.Append("  ").Append(Mathf.RoundToInt(g.JumpMax[i]));
                }
                sb.Append('\n');
            }
            text = sb.ToString();
        }

        void Pct(float share)
        {
            int t = Mathf.RoundToInt(share * 1000f); // tenths of a percent
            sb.Append(t / 10).Append('.').Append(t % 10);
        }

        void OnGUI()
        {
            if (!On) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Screen.height / 70), richText = false };
                style.normal.textColor = Color.white;
            }
            float w = Screen.width * 0.98f, lines = 2f + BodyView.Heroes.Count;
            float y = Screen.height - style.lineHeight * lines - 8f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(4f, y - 2f, w, style.lineHeight * lines), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(8f, y, w, style.lineHeight * lines), text, style);
        }
    }
}
