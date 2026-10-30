using UnityEditor;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// The rig and animation pass of 2026-10-28 part by part (BAM > Art > Animation Check): each <see cref="AnimFix"/> switch on or
    /// off live in Play, and slow motion, so a spin, a slide or a snap can be watched and its part turned off to compare with the
    /// game before it. Only the Editor's Play: a build always plays with every part on, and the switches are back on after the
    /// next script reload.
    /// </summary>
    sealed class AnimationCheck : EditorWindow
    {
        float slow = 1f;
        Vector2 scroll;

        [MenuItem("BAM/Art/Animation Check")]
        static void Open() => GetWindow<AnimationCheck>("Animation Check");

        void OnEnable() => EditorApplication.playModeStateChanged += PlayMode;
        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayMode;
            if (Application.isPlaying && slow < 1f) Time.timeScale = 1f;
        }

        void PlayMode(PlayModeStateChange s) { if (s == PlayModeStateChange.ExitingPlayMode) slow = 1f; }

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Turn a part off to see that part as before 2026-10-28. Works live in Play (Home, hero page, match). "
                + "A build on the phone always has every part on.", MessageType.Info);

            AnimFix.FixedGrip = Part("Weapons without spins",
                "The rest angle keeps its side (no half-turn flips), follows the hand a little, and turns into the swing's grip with the eased swing. "
                + "Moa's staff and Tikta's needles keep their angle through the swing; Tyra's bow stands up for the shot.",
                AnimFix.FixedGrip);
            AnimFix.UpperSwing = Part("Swing on the run with the upper body",
                "On the move the attack, cast and flinch play on the upper body; the legs keep running instead of freezing and sliding.",
                AnimFix.UpperSwing);
            AnimFix.LegsFollow = Part("Legs follow the travel",
                "Facing a target while moving, the hips and legs turn to where the hero goes; backing off, the stride plays backwards.",
                AnimFix.LegsFollow);
            AnimFix.SquaredFacing = Part("Face the target in a fight",
                "In a fight the hero keeps facing its target between swings and turns smoothly, not at 900 degrees a second and back.",
                AnimFix.SquaredFacing);
            AnimFix.BlowTiming = Part("Blow timing per clip",
                "Only the part of the attack round its measured blow plays (about 1.8x, not the whole clip at 4x; Bonk's long windup "
                + "slower); a cast starts just before its peak; clips that walk the body away are held in place.",
                AnimFix.BlowTiming);
            AnimFix.Life = Part("Softer blends, lean, breath",
                "Eased blends in and out of swings, a lean into turns and starts, a faint breath when standing in a match.",
                AnimFix.Life);
            AnimFix.ArmRoom = Part("Arm spread only on hanging arms",
                "The arms are turned out from a broad body only while they hang; a raised arm in a swing plays as its clip made it.",
                AnimFix.ArmRoom);

            AnimFix.GripLock = Part("Grip locked in the palm",
                "After every turn the weapon's grip point is moved into the palm, so it never floats off the fist or slides along "
                + "the handle. Only a shift: how the weapon is aimed stays. Weapon numbers: BAM > Art > Weapon Debug View.",
                AnimFix.GripLock);
            AnimFix.WristTurn = Part("Wrist turns instead of the bow (Tyra)",
                "Tyra's bow keeps its shot grip in her fist and her wrist turns up to 60 degrees instead, so the bow no longer twists "
                + "in her hand between standing and shooting. Where the bow points stays the same.",
                AnimFix.WristTurn);
            AnimFix.ContactTiming = Part("Weapon meets the target with the number (melee)",
                "Fenn, Knut, Bonk and Oaken: the weapon reaches its target on the moment the damage number shows, instead of the "
                + "swing's fastest moment showing a little before it.",
                AnimFix.ContactTiming);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All on")) SetAll(true);
            if (GUILayout.Button("All off (as before)")) SetAll(false);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                float s = EditorGUILayout.Slider(new GUIContent("Slow motion", "The whole match slows down (rules and looks together)."), slow, 0.1f, 1f);
                if (!Mathf.Approximately(s, slow)) { slow = s; Time.timeScale = slow; }
            }
            if (!Application.isPlaying) EditorGUILayout.HelpBox("Slow motion works in Play.", MessageType.None);
            EditorGUILayout.EndScrollView();
        }

        static bool Part(string name, string what, bool on)
        {
            EditorGUILayout.Space(4);
            bool v = EditorGUILayout.ToggleLeft(name, on, EditorStyles.boldLabel);
            EditorGUILayout.LabelField(what, EditorStyles.wordWrappedMiniLabel);
            return v;
        }

        static void SetAll(bool on)
        {
            AnimFix.FixedGrip = AnimFix.UpperSwing = AnimFix.LegsFollow = AnimFix.SquaredFacing = on;
            AnimFix.BlowTiming = AnimFix.Life = AnimFix.ArmRoom = AnimFix.GripLock = AnimFix.WristTurn = AnimFix.ContactTiming = on;
        }
    }
}
