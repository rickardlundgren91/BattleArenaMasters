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
        const string Key = "BAM.GripValidator.Pending", AnatKey = "BAM.GripValidator.Anatomical";

        [MenuItem("BAM/Art/Grip Validator (Tyra)")]
        static void Open() => Run(false);

        /// <summary>The same run with the A/B switch AnimFix.AnatomicalWrist on (Play reloads the scripts, which would put a switch
        /// set by hand in the Editor back to its default, so the run sets it itself).</summary>
        [MenuItem("BAM/Art/Grip Validator (Tyra, anatomical wrist A-B)")]
        static void OpenAnatomical() => Run(true);

        static void Run(bool anatomical)
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
            SessionState.SetBool(AnatKey, anatomical);
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
                AnimFix.AnatomicalWrist = SessionState.GetBool(AnatKey, false);
                new GameObject("BAM Grip Validator").AddComponent<GripValidatorRunner>();
            };
        }
    }
}
