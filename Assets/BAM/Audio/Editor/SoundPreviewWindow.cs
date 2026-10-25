using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Audio.Editor
{
    /// <summary>
    /// BAM > Sound Preview: one button per sound to listen to in Play mode, a distance slider to hear PlayAt's fall-off,
    /// a burst test for the voice limits, and an export of every sound as a WAV file (works without Play mode).
    /// </summary>
    sealed class SoundPreviewWindow : EditorWindow
    {
        // where each group starts in SfxId, for the headings
        static readonly SfxId[] Groups = { SfxId.UiTap, SfxId.AttackBram, SfxId.BramHammerThrow, SfxId.HitLight, SfxId.MinionMelee, SfxId.HeroDeath };
        static readonly string[] GroupNames = { "UI", "Hero auto attacks", "Specials", "Combat", "Minions and towers", "Events" };

        float volume = 1f, distance;
        bool burst;
        Vector2 scroll;

        [MenuItem("BAM/Sound Preview")]
        static void Open() => GetWindow<SoundPreviewWindow>("BAM Sounds");

        [MenuItem("BAM/Export Sounds as WAV")]
        static void ExportMenu() => Export();

        void OnGUI()
        {
            bool playing = Application.isPlaying;
            if (!playing)
                EditorGUILayout.HelpBox("Press Play to listen to the sounds here (the game does not need to be in a match). " +
                    "Or export every sound as a WAV file and play them in any player.", MessageType.Info);
            if (GUILayout.Button("Export all sounds as WAV (folder SoundPreview next to Assets)")) Export();

            using (new EditorGUI.DisabledScope(!playing))
            {
                volume = EditorGUILayout.Slider("Volume", volume, 0f, 1f);
                distance = EditorGUILayout.Slider(new GUIContent("Distance (tiles)", "0 = Play (2D). Above 0 = PlayAt that far to the right of the listener."), distance, 0f, 25f);
                burst = EditorGUILayout.Toggle(new GUIContent("Burst x20", "Plays the sound 20 times in one frame: the voice limit should let only one or two through."), burst);
                if (GUILayout.Button("Stop all")) BamAudio.StopAll();

                scroll = EditorGUILayout.BeginScrollView(scroll);
                int perRow = Mathf.Max(1, (int)((position.width - 20) / 150));
                for (int g = 0; g < Groups.Length; g++)
                {
                    int from = (int)Groups[g], to = g + 1 < Groups.Length ? (int)Groups[g + 1] : SfxRecipes.Count;
                    EditorGUILayout.LabelField(GroupNames[g], EditorStyles.boldLabel);
                    for (int i = from; i < to; i += perRow)
                    {
                        EditorGUILayout.BeginHorizontal();
                        for (int j = i; j < Math.Min(to, i + perRow); j++)
                            if (GUILayout.Button(((SfxId)j).ToString(), GUILayout.Width(145))) Listen((SfxId)j);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        void Listen(SfxId id)
        {
            if (!BamAudio.Ready) BamAudio.Init();
            int n = burst ? 20 : 1;
            for (int i = 0; i < n; i++)
            {
                if (distance <= 0f) BamAudio.Play(id, volume);
                else BamAudio.PlayAt(id, BamAudio.ListenerPoint + new Vector3(distance, 0f, 0f), volume);
            }
        }

        static void Export()
        {
            string dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SoundPreview");
            Directory.CreateDirectory(dir);
            var synth = new SfxSynth();
            for (int i = 1; i < SfxRecipes.Count; i++)
            {
                var id = (SfxId)i;
                WriteWav(Path.Combine(dir, $"{i:00}_{id}.wav"), SfxRecipes.Render(synth, id), SfxSynth.Rate);
            }
            Debug.Log($"[BAM.Audio] {SfxRecipes.Count - 1} sounds written to {dir}");
            EditorUtility.RevealInFinder(Path.Combine(dir, "01_UiTap.wav"));
        }

        // 16-bit mono PCM
        static void WriteWav(string path, float[] data, int rate)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int n = data.Length;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + n * 2);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' }); w.Write(16);
                w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(n * 2);
                for (int i = 0; i < n; i++) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(data[i], -1f, 1f) * 32767f));
            }
        }
    }
}
