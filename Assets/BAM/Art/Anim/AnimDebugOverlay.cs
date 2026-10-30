using System.Text;
using BAM.Sim;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Animation Foundation 1.0: a text overlay in the top-left corner with each hero's animation state and its measured swing
    /// timing (AnimProbe), for checking in the Editor and on a phone. Only in the Editor and in Development Builds: a release
    /// build never creates it. On in the Editor from BAM > Art > Animation Debug Overlay; on by itself in a Development Build.
    /// The text is rebuilt 4 times a second into one reused buffer, so it costs next to nothing while it is on.
    /// </summary>
    public sealed class AnimDebugOverlay : MonoBehaviour
    {
        /// <summary>Shown now. The Editor's menu flips it; a Development Build starts with it on.</summary>
        public static bool On;

        static AnimDebugOverlay inst;
        static string[] names;
        readonly StringBuilder sb = new StringBuilder(2048);
        string text = "";
        float nextAt, fpsAcc, worstFrame;
        int fpsN;
        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!Debug.isDebugBuild || inst != null) return; // release builds: never
            if (!Application.isEditor) On = true;
            var go = new GameObject("BAM AnimDebugOverlay");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            inst = go.AddComponent<AnimDebugOverlay>();
            inst.useGUILayout = false;
            names = new string[16];
            for (int i = 0; i < names.Length; i++) names[i] = ((HeroId)i).ToString();
        }

        void Update()
        {
            if (!On) return;
            float dt = Time.unscaledDeltaTime;
            fpsAcc += dt; fpsN++;
            if (dt > worstFrame) worstFrame = dt;
            if (Time.unscaledTime < nextAt) return;
            nextAt = Time.unscaledTime + 0.25f;
            Build();
            fpsAcc = 0f; fpsN = 0; worstFrame = 0f;
        }

        void Build()
        {
            sb.Length = 0;
            sb.Append("ANIM  fps ").Append(fpsN > 0 ? Mathf.RoundToInt(fpsN / Mathf.Max(1e-4f, fpsAcc)) : 0)
              .Append("  worst frame ").Append(Mathf.RoundToInt(worstFrame * 1000f)).Append(" ms\n");
            sb.Append("hero   state      wind  rel.err  worst  sw/blow/rel  dbl rst lock miss\n");
            var heroes = BodyView.Heroes;
            for (int i = 0; i < heroes.Count; i++)
            {
                BodyView v = heroes[i];
                if (v == null || v.Preview) continue;
                AnimProbe p = v.Probe;
                string n = v.HeroIndex >= 0 && v.HeroIndex < names.Length ? names[v.HeroIndex] : "?";
                sb.Append(n);
                for (int k = n.Length; k < 7; k++) sb.Append(' ');
                sb.Append(State(v));
                sb.Append(' ').Append(Mathf.RoundToInt(v.DbgWindup * 1000f)).Append("ms ");
                Ms(p.LastErrMs); sb.Append(' '); Ms(p.WorstErrMs);
                sb.Append("  ").Append(p.Swings).Append('/').Append(p.Blows).Append('/').Append(p.Releases)
                  .Append("  ").Append(p.Doubles).Append(' ').Append(p.Restarts).Append(' ').Append(p.Locks)
                  .Append(' ').Append(p.Missing).Append('\n');
            }
            sb.Append("rel.err: + = blow after the shot, - = blow before it; within one frame is in sync");
            text = sb.ToString();
        }

        void Ms(float ms)
        {
            if (ms >= 0f) sb.Append('+');
            sb.Append(Mathf.RoundToInt(ms)).Append("ms");
        }

        // one word for what the body is doing, padded to 10
        static string State(BodyView v)
        {
            if (v.IsDead) return "dead      ";
            if (v.DbgCut) return "cut       ";
            if (v.DbgCastT >= 0f) return "cast      ";
            float t = v.DbgAtkT;
            if (t >= 0f)
            {
                bool move = v.DbgMoveW > 0.3f;
                if (t < v.DbgWindup) return move ? "wind+run  " : "windup    ";
                return move ? "recov+run " : "recover   ";
            }
            return v.DbgMoveW > 0.5f ? "run       " : "idle      ";
        }

        void OnGUI()
        {
            if (!On) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(11, Screen.height / 70), richText = false };
                style.normal.textColor = Color.white;
            }
            float w = Screen.width * 0.98f, h = Screen.height * 0.4f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(4f, 4f, w, style.lineHeight * 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(8f, 6f, w, h), text, style);
        }
    }
}
