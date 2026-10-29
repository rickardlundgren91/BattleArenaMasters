using System.IO;
using BAM.Sim;
using UnityEditor;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Rickard's sliders for the held weapons (BAM > Art > Weapon Tuner). They change the live table in <see cref="Weapons"/>, which
    /// every hero view reads each frame, so in Play the hero on Home, on the hero page and in a match moves at once. Save writes
    /// Resources/BAMArt/WeaponTune.json (laid over the code's numbers at the next start); Reset puts the code's numbers back for
    /// the chosen hero.
    /// </summary>
    sealed class WeaponTuner : EditorWindow
    {
        const string FilePath = "Assets/BAM/Art/Resources/BAMArt/WeaponTune.json";

        int hero = (int)HeroId.Bonk;
        Vector2 scroll;

        [MenuItem("BAM/Art/Weapon Tuner")]
        static void Open() => GetWindow<WeaponTuner>("Weapon Tuner");

        void OnGUI()
        {
            GearDef[] all = Weapons.All;
            var names = new string[all.Length];
            for (int i = 0; i < all.Length; i++) names[i] = ((HeroId)i).ToString();
            hero = EditorGUILayout.Popup("Hero", Mathf.Clamp(hero, 0, all.Length - 1), names);
            GearDef g = all[hero];
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Press Play first: the sliders move the hero live on Home, the hero page and in a match.", MessageType.Info);
            if (g == null) { EditorGUILayout.HelpBox("This hero holds no weapon.", MessageType.None); return; }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (g.Weapons != null)
                for (int w = 0; w < g.Weapons.Length; w++)
                {
                    WeaponDef d = g.Weapons[w];
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField(d.Model + (d.Left ? " (left hand)" : " (right hand)"), EditorStyles.boldLabel);
                    d.Len = EditorGUILayout.Slider(new GUIContent("Size", "Length as a share of the hero's height"), d.Len, 0.1f, 2f);
                    d.Grip = EditorGUILayout.Slider(new GUIContent("Grip", "Where the fist holds it: 0 = the handle's end, 1 = the top"), d.Grip, 0f, 1f);
                    d.HoldUp = EditorGUILayout.Slider(new GUIContent("Hold up", "Degrees above level at rest (negative = pointing down; 0 = off, left as the hand holds it)"), d.HoldUp, -90f, 90f);
                    d.HoldOut = EditorGUILayout.Slider(new GUIContent("Hold out", "Degrees from straight ahead towards its hand's side; past 90 it leans back"), d.HoldOut, -180f, 180f);
                    EditorGUILayout.LabelField("Extra turn (degrees about the grip)");
                    d.Turn = new Vector3(
                        EditorGUILayout.Slider("  x", d.Turn.x, -180f, 180f),
                        EditorGUILayout.Slider("  y", d.Turn.y, -180f, 180f),
                        EditorGUILayout.Slider("  z", d.Turn.z, -180f, 180f));
                    EditorGUILayout.LabelField("Extra shift (share of the hero's height)");
                    d.Offset = new Vector3(
                        EditorGUILayout.Slider("  right", d.Offset.x, -0.5f, 0.5f),
                        EditorGUILayout.Slider("  up", d.Offset.y, -0.5f, 0.5f),
                        EditorGUILayout.Slider("  forward", d.Offset.z, -0.5f, 0.5f));
                }

            if (g.Cannon != null)
            {
                // Hilde's shoulder cannon (HILDE-04): placed on her shoulder by the code, these move it on top, live
                CannonDef c = g.Cannon;
                EditorGUILayout.Space();
                EditorGUILayout.LabelField(c.Model + " (on the right shoulder)", EditorStyles.boldLabel);
                c.Len = EditorGUILayout.Slider(new GUIContent("Size", "Length as a share of the hero's height"), c.Len, 0.1f, 1.2f);
                EditorGUILayout.LabelField("Extra turn (degrees about the cannon)");
                c.Turn = new Vector3(
                    EditorGUILayout.Slider("  x", c.Turn.x, -180f, 180f),
                    EditorGUILayout.Slider("  y", c.Turn.y, -180f, 180f),
                    EditorGUILayout.Slider("  z", c.Turn.z, -180f, 180f));
                EditorGUILayout.LabelField("Extra shift (share of the hero's height)");
                c.Offset = new Vector3(
                    EditorGUILayout.Slider("  right", c.Offset.x, -0.5f, 0.5f),
                    EditorGUILayout.Slider("  up", c.Offset.y, -0.5f, 0.5f),
                    EditorGUILayout.Slider("  forward", c.Offset.z, -0.5f, 0.5f));
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Arms", EditorStyles.boldLabel);
            g.ArmSpread = EditorGUILayout.Slider(new GUIContent("Arms out", "Degrees both upper arms turn out from the sides"), g.ArmSpread, 0f, 45f);
            if (g.Shoulder)
            {
                EditorGUILayout.LabelField("Right hand on the shoulder (share of the hero's height)");
                g.CarryAt = new Vector3(
                    EditorGUILayout.Slider("  hand forward", g.CarryAt.x, -0.3f, 0.4f),
                    EditorGUILayout.Slider("  hand down", g.CarryAt.y, -0.3f, 0.4f),
                    EditorGUILayout.Slider("  hand in", g.CarryAt.z, -0.3f, 0.3f));
                g.Elbow = new Vector3(
                    EditorGUILayout.Slider("  elbow down", g.Elbow.x, -0.4f, 0.4f),
                    EditorGUILayout.Slider("  elbow forward", g.Elbow.y, -0.4f, 0.4f),
                    EditorGUILayout.Slider("  elbow out", g.Elbow.z, -0.4f, 0.4f));
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Save", GUILayout.Height(28))) Save(all);
                if (GUILayout.Button("Reset " + names[hero], GUILayout.Height(28)))
                {
                    GearDef code = Weapons.CodeDefaults()[hero];
                    if (code != null) WeaponTuning.Copy(WeaponTuning.From(names[hero], code), g);
                }
            }
            EditorGUILayout.HelpBox("Save writes every hero's numbers to " + FilePath + ". Send that file back to Claude so it goes into the game.", MessageType.None);
        }

        static void Save(GearDef[] all)
        {
            File.WriteAllText(FilePath, WeaponTuning.ToJson(all));
            AssetDatabase.Refresh();
            Debug.Log("[BAM.Art] Weapon numbers saved to " + FilePath);
        }

        // the sliders move a hero that plays in the Game view: keep this window drawn while it runs
        void OnInspectorUpdate() { if (Application.isPlaying) Repaint(); }
    }
}
