using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using BAM.Core;
using BAM.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// BAM > Art > Grip Validator (Tyra): the Tyra pilot's measuring tool (issue #5, Simon's yes 2026-10-31). Opens an empty scene,
    /// enters Play, builds Tyra's real view with ArtKit (as the match does) and drives it through a fixed script at 30, 60 and 120
    /// frames a second: idle, walk, run, a shot on the run, standing shots with the stance between them, both casts, hits in a cast,
    /// a cut shot, direction changes with the legs twisted, a turn on the spot, death and respawn. After the whole LateUpdate of
    /// every frame it measures her bow and writes one CSV row per frame and a PASS/FAIL summary (Logs/GripValidator next to Assets,
    /// and the Console). Editor only: nothing here is in a build, and the game's code is only read.
    ///
    /// Numbers (h = her art height from sole to crown at her scale, BodyView.ShownHeight, so the game's gearK does not move them):
    /// GripError = |the bow's grip point - the palm socket| / h (the 0.6 palm rule when the weapon has no socket); AngleError = the bow's
    /// long axis against the knuckle line (index root - little finger root), folded to 0-90; InFistSpin = the bow's turn in the hand's
    /// own space from the frame before; AnatError = its turn from the fist's own grip (WeaponDef.GripY/GripZ); BelowGround = (ground -
    /// lowest bow vertex) / h; Penetration = how deep a bow vertex lies inside the body, by the nearest body vertex's normal on the
    /// baked skinned mesh, left-hand vertices left out (an approximation: the offline check used ray parity).
    /// PASS uses issue #5's limits (GripError at most 1.5 % h, AngleError at most 30 degrees, in Held, Windup, Released and
    /// Interrupted); the pilot's tighter goals (0.5 % h, 12 degrees, 0.5 degrees a frame, 0.3 % h) are reported beside them.
    /// </summary>
    static class GripValidatorMenu
    {
        const string Key = "BAM.GripValidator.Pending";

        [MenuItem("BAM/Art/Grip Validator (Tyra)")]
        static void Open()
        {
            if (EditorApplication.isPlaying) { EditorApplication.isPlaying = false; return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            AssetDatabase.Refresh();
            HeroArtImport.BuildAll();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("BAM Tool Scene"); // tells Boot (BAM.Game) not to start the game in this scene
            var cam = new GameObject("Main Camera") { tag = "MainCamera" };
            var c = cam.AddComponent<Camera>();
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.11f, 0.14f, 0.25f);
            c.fieldOfView = 38f;
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 60f;
            new GameObject("Sun").AddComponent<Light>().type = LightType.Directional;
            SessionState.SetBool(Key, true);
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Hook()
        {
            EditorApplication.playModeStateChanged += s =>
            {
                if (s != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Key, false)) return;
                SessionState.SetBool(Key, false);
                new GameObject("BAM Grip Validator").AddComponent<GripValidatorRunner>();
            };
        }
    }

    /// <summary>Drives Tyra's view through the script (Update, before the views' LateUpdate) and measures after it (LateUpdate, late).</summary>
    [DefaultExecutionOrder(10000)]
    sealed class GripValidatorRunner : MonoBehaviour
    {
        // issue #5's limits (PASS) and the pilot's goals
        const float GripPass = 0.015f, AnglePass = 30f, GripGoal = 0.005f, AngleGoal = 12f, SpinGoal = 0.5f, PenGoal = 0.003f;
        const float DeadGroundFor = 0.6f; // the hero sinks on purpose from 0.6 s (BodyView.AnimateDeath)
        const float Warmup = 1f;          // unmeasured idle first: the rest aim is sampled from the idle (BodyView.MeasureRest)
        static readonly int[] Rates = { 30, 60, 120 };
        static readonly string[] States = { "Held", "Windup", "Released", "Interrupted", "Dead" };

        delegate void Act(GripValidatorRunner r);

        sealed class Step
        {
            public string Name;
            public float Dur, Speed, TurnDeg;
            public Vector3 Dir;        // travel in the hero's start space (0 = standing)
            public bool Squared;
            public Act Begin;
            public float[] At;         // extra actions at these times into the step
            public Act[] Then;
        }

        Step[] script;
        int rateIndex = -1, stepIndex, frame;
        float stepT, runT, deadAt = -1f, windupFrom = -1f, windupLen, cutAt = -1f;
        int thenNext;
        bool done;
        UnitView view;
        BodyView body;
        Camera cam;
        string status = "Starting";

        // measuring
        float h = 1f;
        Vector3[] bowVerts;
        SkinnedMeshRenderer[] skins;
        bool[][] skip;          // per skin, per vertex: left-hand vertices (contact there is the grip)
        string[][] part;        // per skin, per vertex: its main bone, for the report
        Mesh bake;
        readonly List<Vector3> bv = new List<Vector3>(), bn = new List<Vector3>();
        Vector3[] wv = new Vector3[0], wn = new Vector3[0];
        string[] wPart = new string[0];
        int[] cellHead = new int[1 << 16], cellNext = new int[0];
        int wCount;
        Quaternion prevFist;
        bool hasPrev;

        readonly StringBuilder csv = new StringBuilder(1 << 20);
        // per rate, per state: max GripError, AngleError, InFistSpin, AnatError, Penetration, BelowGround; frames
        readonly float[,,] max = new float[3, 5, 6];
        readonly int[,] count = new int[3, 5];
        readonly string[,] penWhere = new string[3, 5];

        void Start()
        {
            ArtKit.Init();
            ArtKit.ApplyQuality(GraphicsQuality.High);
            cam = Camera.main;
            script = Script();
            csv.Append("Rate,Frame,Time,Step,State,GripErrorPct,GripErrorRulePct,AngleErrorDeg,AnatErrorDeg,FistTwistDeg,InFistSpinDeg,BelowGroundPct,PenetrationPct,PenPart,Away\n");
            NextRate();
        }

        void NextRate()
        {
            if (view != null) Destroy(view.gameObject);
            view = null; body = null;
            rateIndex++;
            if (rateIndex >= Rates.Length) { Finish(); return; }
            Time.captureDeltaTime = 1f / Rates[rateIndex];
            view = ArtKit.CreateHero(HeroId.Tyra, 0);
            view.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            body = view as BodyView;
            stepIndex = -1; stepT = 0f; runT = 0f; frame = 0; hasPrev = false;
            deadAt = -1f; windupFrom = -1f; cutAt = -1f;
            skins = null;
        }

        void Update()
        {
            if (done || view == null) return;
            float dt = Time.deltaTime;
            runT += dt;
            if (runT < Warmup) { status = "Warmup " + Rates[rateIndex] + " fps"; return; }
            if (stepIndex < 0 || stepT >= script[stepIndex].Dur) { StartStep(stepIndex + 1); if (done || view == null || stepIndex < 0) return; }
            Step s = script[stepIndex];
            stepT += dt;
            while (s.At != null && thenNext < s.At.Length && stepT >= s.At[thenNext]) { s.Then[thenNext](this); thenNext++; }
            Transform tr = view.transform;
            if (s.TurnDeg != 0f) tr.rotation = Quaternion.AngleAxis(s.TurnDeg * dt / s.Dur, Vector3.up) * tr.rotation;
            if (s.Speed > 0f) tr.position += s.Dir.normalized * (s.Speed * dt);
            status = Rates[rateIndex] + " fps: " + s.Name + " (" + (stepIndex + 1) + "/" + script.Length + ")";
        }

        void StartStep(int k)
        {
            if (k >= script.Length) { NextRate(); return; }
            stepIndex = k; stepT = 0f; thenNext = 0;
            Step s = script[k];
            view.SetMoving(s.Speed > 0f, s.Speed > 2f ? 1f : 0.4f);
            view.SetSquared(s.Squared);
            s.Begin?.Invoke(this);
        }

        // ------------------------------------------------------------------ the script

        static void Shot(GripValidatorRunner r) { r.view.PlayAttack(0.195f); r.windupFrom = r.runT; r.windupLen = 0.195f; } // Defs.cs: Tyra's auto windup
        static void Volley(GripValidatorRunner r) { r.view.PlayCast(false); r.windupFrom = r.runT; r.windupLen = 0.25f; }  // TyraS.VolleyWindup
        static void Bolt(GripValidatorRunner r) { r.view.PlayCast(true); r.windupFrom = r.runT; r.windupLen = 0.35f; }     // TyraS.Windup

        static Step[] Script()
        {
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            return new[]
            {
                new Step { Name = "Idle", Dur = 2f },
                new Step { Name = "Walk", Dur = 2f, Speed = 1.4f, Dir = fwd },
                new Step { Name = "Run", Dur = 2f, Speed = 3f, Dir = fwd },
                new Step { Name = "Run + shot", Dur = 1.6f, Speed = 3f, Dir = fwd, Squared = true, Begin = Shot },
                new Step { Name = "Stop", Dur = 1f },
                new Step { Name = "Shots standing (stance between)", Dur = 5.6f, Squared = true, Begin = Shot, At = new[] { 1.85f, 3.7f }, Then = new Act[] { Shot, Shot } },
                new Step { Name = "Volley (tap cast)", Dur = 1.6f, Squared = true, Begin = Volley },
                new Step { Name = "Bolt (drag cast)", Dur = 1.8f, Squared = true, Begin = Bolt },
                new Step { Name = "Hits in a cast", Dur = 1.8f, Squared = true, Begin = Bolt,
                    At = new[] { 0.2f, 0.9f }, Then = new Act[] { r => r.view.PlayHit(false), r => r.view.PlayHit(true) } },
                new Step { Name = "Cut shot", Dur = 1f, Squared = true, Begin = Shot,
                    At = new[] { 0.1f }, Then = new Act[] { r => { r.view.Interrupt(true); r.cutAt = r.runT; } } },
                new Step { Name = "Sideways right, facing ahead", Dur = 1.5f, Speed = 3f, Dir = right, Squared = true, Begin = Shot },
                new Step { Name = "Backwards, facing ahead", Dur = 1.5f, Speed = 3f, Dir = -fwd, Squared = true, Begin = Shot },
                new Step { Name = "Sideways left, facing ahead", Dur = 1.5f, Speed = 3f, Dir = -right, Squared = true, Begin = Shot },
                new Step { Name = "Turn 180 standing", Dur = 0.5f, TurnDeg = 180f },
                new Step { Name = "Turn 180 running", Dur = 0.6f, Speed = 3f, Dir = fwd, TurnDeg = 180f },
                new Step { Name = "Die", Dur = 2.5f, Begin = r => { r.view.SetDead(true); r.deadAt = r.runT; } },
                new Step { Name = "Respawn", Dur = 1.5f, Begin = r => { r.view.SetDead(false); r.deadAt = -1f; } },
            };
        }

        int State()
        {
            if (deadAt >= 0f) return 4;
            if (cutAt >= 0f && runT - cutAt < 0.15f) return 3; // BodyView.CutFade
            if (windupFrom >= 0f)
            {
                float e = runT - windupFrom;
                if (e < windupLen) return cutAt >= windupFrom ? 3 : 1;
                if (e < windupLen + 0.45f) return 2;
            }
            return 0;
        }

        // ------------------------------------------------------------------ measuring, after every view's LateUpdate

        void LateUpdate()
        {
            if (done || body == null) return;
            Follow();
            if (runT < Warmup || stepIndex < 0) return;
            HeldGear g = body.Gear;
            if (g == null || g.WeaponCount == 0) { Debug.LogWarning("[GripValidator] Tyra has no held weapon (code figure?)."); Finish(); return; }
            if (skins == null) Prepare(g);
            const int i = 0;
            Transform t = g.WeaponT(i), hd = g.HandT(i);
            if (t == null || hd == null) return;
            frame++;
            int st = State();
            bool away = t.localScale.y < 1e-4f;
            g.GripPoints(i, out Vector3 grip, out Vector3 locked);
            bool sock = g.SocketWorld(i, out Vector3 socket);
            Vector3 rule = g.PalmRuleWorld(i);
            float gripErr = (grip - (sock ? socket : rule)).magnitude / h, gripRule = (grip - rule).magnitude / h;
            Transform[] f = g.FingersAt(i);
            float ang = -1f;
            if (f != null && f.Length > 12 && f[3] != null && f[12] != null)
            {
                float a = Vector3.Angle(t.up, f[3].position - f[12].position);
                ang = Mathf.Min(a, 180f - a);
            }
            Quaternion fist = Quaternion.Inverse(hd.rotation) * t.rotation;
            float spin = hasPrev ? Quaternion.Angle(fist, prevFist) : 0f;
            prevFist = fist; hasPrev = true;
            float anat = g.AnatomicalGrip(i, out Quaternion q) ? Quaternion.Angle(fist, q) : -1f;
            float twist = g.FistTwist != null ? g.FistTwist[i] : 0f;
            float floor = view.transform.position.y, low = float.PositiveInfinity;
            if (!away) for (int k = 0; k < bowVerts.Length; k++) { float y = t.TransformPoint(bowVerts[k]).y; if (y < low) low = y; }
            float below = away ? -1f : (floor - low) / h;
            float pen = 0f; string where = "";
            if (!away && frame % 2 == 0) pen = Penetration(t, sock ? socket : locked, out where);

            int r = rateIndex;
            if (!away)
            {
                bool groundCounts = st != 4 || runT - deadAt < DeadGroundFor;
                Max(r, st, 0, gripErr); Max(r, st, 1, ang); Max(r, st, 2, spin); Max(r, st, 3, anat); Max(r, st, 4, pen);
                if (groundCounts) Max(r, st, 5, below);
                if (pen > 0f && pen >= max[r, st, 4]) penWhere[r, st] = where;
                count[r, st]++;
            }
            var ci = CultureInfo.InvariantCulture;
            csv.Append(Rates[r]).Append(',').Append(frame).Append(',').Append(runT.ToString("F4", ci)).Append(',')
               .Append(script[stepIndex].Name.Replace(',', ' ')).Append(',').Append(States[st]).Append(',')
               .Append((gripErr * 100f).ToString("F3", ci)).Append(',').Append((gripRule * 100f).ToString("F3", ci)).Append(',')
               .Append(ang.ToString("F2", ci)).Append(',').Append(anat.ToString("F2", ci)).Append(',').Append(twist.ToString("F2", ci)).Append(',')
               .Append(spin.ToString("F3", ci)).Append(',').Append((below * 100f).ToString("F3", ci)).Append(',')
               .Append((pen * 100f).ToString("F3", ci)).Append(',').Append(where).Append(',').Append(away ? 1 : 0).Append('\n');
        }

        void Max(int r, int st, int k, float v) { if (v > max[r, st, k]) max[r, st, k] = v; }

        void Follow()
        {
            if (cam == null || view == null) return;
            float sh = body != null ? body.ShownHeight : 1.8f;
            Vector3 c = view.transform.position + Vector3.up * (0.55f * sh);
            cam.transform.position = c + new Vector3(-1.2f, 0.35f, 2.6f) * sh;
            cam.transform.LookAt(c);
        }

        void Prepare(HeldGear g)
        {
            // h = her art height at this scale (HeroArtImport Height, sole to crown), not the game's gear scale (gearK)
            h = body.ShownHeight > 0.01f ? body.ShownHeight : Mathf.Max(0.1f, g.FigH);
            var list = new List<SkinnedMeshRenderer>(view.GetComponentsInChildren<SkinnedMeshRenderer>(true));
            skins = list.ToArray();
            skip = new bool[skins.Length][]; part = new string[skins.Length][];
            int total = 0;
            for (int s = 0; s < skins.Length; s++)
            {
                Mesh m = skins[s].sharedMesh;
                if (m == null) { skip[s] = new bool[0]; part[s] = new string[0]; continue; }
                BoneWeight[] w = m.boneWeights;
                Transform[] bones = skins[s].bones;
                int n = m.vertexCount;
                skip[s] = new bool[n]; part[s] = new string[n];
                for (int v = 0; v < n; v++)
                {
                    int bi = v < w.Length ? w[v].boneIndex0 : -1;
                    string name = bi >= 0 && bi < bones.Length && bones[bi] != null ? bones[bi].name : "?";
                    int colon = name.IndexOf(':');
                    if (colon >= 0) name = name.Substring(colon + 1);
                    part[s][v] = name;
                    skip[s][v] = name.StartsWith("LeftHand");
                }
                total += n;
            }
            wv = new Vector3[total]; wn = new Vector3[total]; wPart = new string[total]; cellNext = new int[total];
            bake = new Mesh();
            Mesh bow = Props.Mesh(g.Def(0).Model);
            Vector3[] all = bow != null ? bow.vertices : new Vector3[0];
            var pick = new List<Vector3>(all.Length / 3 + 1);
            for (int k = 0; k < all.Length; k += 3) pick.Add(all[k]);
            bowVerts = pick.ToArray();
        }

        // the deepest bow vertex inside the body: nearest body vertex (a 5 % h grid), inside when behind that vertex's normal
        float Penetration(Transform t, Vector3 palm, out string where)
        {
            where = "";
            float cell = 0.05f * h, inv = 1f / cell;
            wCount = 0;
            for (int c = 0; c < cellHead.Length; c++) cellHead[c] = -1;
            for (int s = 0; s < skins.Length; s++)
            {
                SkinnedMeshRenderer sk = skins[s];
                if (sk == null || !sk.enabled || sk.sharedMesh == null) continue;
                sk.BakeMesh(bake);
                bake.GetVertices(bv); bake.GetNormals(bn);
                Transform st = sk.transform;
                for (int v = 0; v < bv.Count && v < skip[s].Length; v++)
                {
                    if (skip[s][v]) continue;
                    Vector3 p = st.TransformPoint(bv[v]);
                    wv[wCount] = p;
                    wn[wCount] = v < bn.Count ? st.TransformDirection(bn[v]) : Vector3.up;
                    wPart[wCount] = part[s][v];
                    int key = Key(p, inv);
                    cellNext[wCount] = cellHead[key]; cellHead[key] = wCount;
                    wCount++;
                }
            }
            float deepest = 0f;
            for (int k = 0; k < bowVerts.Length; k++)
            {
                Vector3 p = t.TransformPoint(bowVerts[k]);
                if ((p - palm).sqrMagnitude < 0.0036f * h * h) continue; // within 6 % h of the palm: the grip itself
                int bx = Mathf.FloorToInt(p.x * inv), by = Mathf.FloorToInt(p.y * inv), bz = Mathf.FloorToInt(p.z * inv);
                float best = float.PositiveInfinity; int bi = -1;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dz = -1; dz <= 1; dz++)
                            for (int v = cellHead[Hash(bx + dx, by + dy, bz + dz)]; v >= 0; v = cellNext[v])
                            {
                                float d = (wv[v] - p).sqrMagnitude;
                                if (d < best) { best = d; bi = v; }
                            }
                if (bi < 0 || best > cell * cell) continue;
                if (Vector3.Dot(p - wv[bi], wn[bi]) >= 0f) continue; // in front of the skin
                float depth = Mathf.Sqrt(best);
                if (depth > deepest) { deepest = depth; where = wPart[bi]; }
            }
            return deepest / h;
        }

        int Key(Vector3 p, float inv) => Hash(Mathf.FloorToInt(p.x * inv), Mathf.FloorToInt(p.y * inv), Mathf.FloorToInt(p.z * inv));
        int Hash(int x, int y, int z) => (int)(((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(z * 83492791)) & (uint)(cellHead.Length - 1));

        // ------------------------------------------------------------------ the report

        void Finish()
        {
            if (done) return;
            done = true;
            Time.captureDeltaTime = 0f;
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "GripValidator");
            Directory.CreateDirectory(dir);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            string csvPath = Path.Combine(dir, "Tyra_" + stamp + ".csv"), sumPath = Path.Combine(dir, "Tyra_" + stamp + "_summary.txt");
            File.WriteAllText(csvPath, csv.ToString());
            string sum = Summary();
            File.WriteAllText(sumPath, sum);
            Debug.Log("[GripValidator] " + sum + "\nFiles: " + csvPath);
            status = "Done: see the Console and " + dir;
            EditorUtility.RevealInFinder(sumPath);
        }

        string Summary()
        {
            var ci = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("Tyra grip validation ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", ci)).Append('\n');
            sb.Append("Switches: GripLock ").Append(AnimFix.GripLock).Append(", PalmSocket ").Append(AnimFix.PalmSocket)
              .Append(", WristTurn ").Append(AnimFix.WristTurn).Append(", AnatomicalWrist ").Append(AnimFix.AnatomicalWrist)
              .Append(", GripLockFull ").Append(AnimFix.GripLockFull).Append('\n');
            sb.Append("h = ").Append(h.ToString("F3", ci)).Append(" world units (her art height at this scale). PASS = issue #5: GripError <= 1.5 % h and AngleError <= 30 deg in Held/Windup/Released/Interrupted.\n");
            sb.Append("Goals (pilot): GripError <= 0.5 % h, AngleError <= 12 deg, InFistSpin <= 0.5 deg/frame, Penetration <= 0.3 % h (approximate), BelowGround <= 0.\n\n");
            bool all = true;
            for (int r = 0; r < Rates.Length; r++)
            {
                sb.Append("== ").Append(Rates[r]).Append(" fps\n");
                sb.Append("State        frames  GripError%  AngleError  InFistSpin  AnatError  Penetration%  BelowGround%  #5\n");
                for (int st = 0; st < States.Length; st++)
                {
                    if (count[r, st] == 0) continue;
                    bool grip = st != 4;
                    bool pass = !grip || (max[r, st, 0] <= GripPass && max[r, st, 1] <= AnglePass);
                    if (!pass) all = false;
                    sb.Append(States[st].PadRight(12)).Append(count[r, st].ToString().PadLeft(7))
                      .Append(Cell(max[r, st, 0] * 100f, GripGoal * 100f, 11, ci)).Append(Cell(max[r, st, 1], AngleGoal, 12, ci))
                      .Append(Cell(max[r, st, 2], SpinGoal, 12, ci)).Append(Cell(max[r, st, 3], -1f, 11, ci))
                      .Append(Cell(max[r, st, 4] * 100f, PenGoal * 100f, 14, ci)).Append(Cell(max[r, st, 5] * 100f, 0f, 14, ci))
                      .Append(grip ? (pass ? "  PASS" : "  FAIL") : "  (no grip requirement)");
                    if (!string.IsNullOrEmpty(penWhere[r, st])) sb.Append("  deepest at ").Append(penWhere[r, st]);
                    sb.Append('\n');
                }
                sb.Append('\n');
            }
            sb.Append(all ? "Overall: PASS against issue #5's limits.\n" : "Overall: FAIL against issue #5's limits (see the rows marked FAIL).\n");
            sb.Append("A '*' marks a value over the pilot's goal. Not a substitute for watching the video at normal speed and on the phone.\n");
            return sb.ToString();
        }

        static string Cell(float v, float goal, int width, CultureInfo ci)
        {
            string s = v.ToString("F2", ci) + (goal >= 0f && v > goal ? "*" : " ");
            return s.PadLeft(width);
        }

        void OnGUI()
        {
            GUI.Label(new Rect(12f, 12f, 900f, 24f), "Grip Validator (Tyra): " + status);
        }

        void OnDestroy()
        {
            Time.captureDeltaTime = 0f;
            if (bake != null) Destroy(bake);
        }
    }
}
