using System;
using System.Diagnostics;
using BAM.Core;
using UnityEngine;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("BAM.Audio.Editor")]

namespace BAM.Audio
{
    /// <summary>Background music. Entries are only ever added at the end.</summary>
    public enum MusicId { None = 0, Match = 1, Menu = 2 }

    // CONTRACT between BAM.Audio (owner: the sound thread) and its callers (BAM.Game, BAM.UI). Docs/CONTRACTS.md explains it.
    // SfxId lives in SfxId.cs, the recipes in SfxRecipes.cs, the synth in SfxSynth.cs.
    // Music is the one recorded asset: Rickard's own loop in Resources/BAM/Music (Docs/LICENSES.md).
    public static class BamAudio
    {
        const int Voices = 16;
        // PlayAt: full volume within NearDist tiles of the listener, easing down to FarFloor at FarDist and beyond,
        // so a fight off screen is still faintly heard
        const float NearDist = 5f, FarDist = 18f, FarFloor = 0.15f, PanDist = 10f, PanMax = 0.45f;
        // building all clips takes about 50 ms on a desktop and maybe 3-5x that on a phone, so it is spread over frames
        const double BuildBudgetMs = 3.0;

        static bool ready;
        static GameObject host;
        static readonly AudioSource[] src = new AudioSource[Voices];
        static readonly SfxId[] voiceId = new SfxId[Voices];
        static readonly float[] voiceEnd = new float[Voices], voiceVol = new float[Voices];
        static readonly byte[] voicePri = new byte[Voices];

        static readonly AudioClip[] clips = new AudioClip[SfxRecipes.Count];
        static readonly SfxInfo[] info = new SfxInfo[SfxRecipes.Count];
        static readonly float[] lastStart = new float[SfxRecipes.Count], lastVol = new float[SfxRecipes.Count];
        // 1 for synth clips; for a recorded file, the gain that brings it to the synth clips' loudness
        static readonly float[] clipGain = new float[SfxRecipes.Count];
        static SfxSynth synth;
        static int nextBuild = 1;
        static double buildMs;

        static Vector3 listener;
        static float master = 1f;
        static uint rng = 0x2545F491u;

        // music sits under the effects: SfxVolume * MusicLevel, faded in and out so starts and stops never click
        const float MusicLevel = 0.45f, MusicFadeIn = 1.2f, MusicFadeOut = 0.5f;
        static AudioSource music;
        static MusicId musicWant, musicLoaded;
        static float musicGain; // 0..1 fade position

        internal static bool Ready => ready && host != null;
        internal static Vector3 ListenerPoint => listener;
        static bool AllBuilt => nextBuild >= SfxRecipes.Count;

        /// <summary>Once at boot: synthesizes the clips and builds the voice pool. Safe to call twice.</summary>
        public static void Init()
        {
            // host == null also covers a second Play session in the editor without a domain reload
            if (ready && host != null) return;
            if (!Application.isPlaying) { UnityEngine.Debug.Log("[BAM.Audio] Init needs Play mode; the editor preview renders WAV files instead"); return; }
            ready = true;
            for (int i = 0; i < SfxRecipes.Count; i++) { info[i] = SfxRecipes.Info((SfxId)i); lastStart[i] = -100f; lastVol[i] = 0f; }
            Array.Clear(clips, 0, clips.Length);
            Array.Clear(voiceEnd, 0, Voices);
            nextBuild = 1; buildMs = 0;
            synth = new SfxSynth();

            host = new GameObject("BAM.Audio");
            Object.DontDestroyOnLoad(host);
            for (int i = 0; i < Voices; i++)
            {
                var a = host.AddComponent<AudioSource>();
                a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f; a.dopplerLevel = 0f;
                src[i] = a;
            }
            music = host.AddComponent<AudioSource>();
            music.playOnAwake = false; music.loop = true; music.spatialBlend = 0f; music.priority = 0; music.volume = 0f;
            musicWant = musicLoaded = MusicId.None; musicGain = 0f; musicPaused = false;
            host.AddComponent<MusicFader>();
            // the scene is empty and the camera is made in code, so make sure something hears us
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();

            master = BamSettings.SfxVolume;
            BamSettings.Changed -= OnSettingsChanged;
            BamSettings.Changed += OnSettingsChanged;

            // the UI sounds come first in SfxId, so the menu is ready after the first frame or two
            host.AddComponent<ClipBuilder>();
        }

        /// <summary>A 2D sound (UI, the player's own hero).</summary>
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f) => Start(id, volume, pitch, 0f);

        /// <summary>A sound in the world: quieter the farther it is from the listener point.</summary>
        public static void PlayAt(SfxId id, Vector3 worldPosition, float volume = 1f)
        {
            float dx = worldPosition.x - listener.x, dz = worldPosition.z - listener.z;
            float d = Mathf.Sqrt(dx * dx + dz * dz);
            float t = Mathf.Clamp01((d - NearDist) / (FarDist - NearDist));
            float att = Mathf.Lerp(1f, FarFloor, t * t * (3f - 2f * t));
            Start(id, volume * att, 1f, Mathf.Clamp(dx / PanDist, -1f, 1f) * PanMax);
        }

        /// <summary>The game layer moves the listener point with the camera's focus every frame.</summary>
        public static void SetListener(Vector3 worldPosition) { listener = worldPosition; }

        /// <summary>
        /// Starts (or keeps) a looping music track with a short fade in; None fades it out. Calling it again with the
        /// track that is already on does nothing, and after StopAll it resumes where it was.
        /// </summary>
        public static void PlayMusic(MusicId id)
        {
            if (!Ready) return;
            musicWant = id;
            if (id == MusicId.None) return;
            if (musicLoaded != id)
            {
                var clip = Resources.Load<AudioClip>(MusicPath(id));
                if (clip == null && id == MusicId.Menu) clip = Resources.Load<AudioClip>(MusicPath(MusicId.Match)); // Rickard: Byte Strike for all music for now
                // no file = no music, quietly: taking the track out of the build only means removing the file
                if (clip == null) { if (!musicMissingLogged) UnityEngine.Debug.Log("[BAM.Audio] no music file at Resources/" + MusicPath(id) + ", music is off"); musicMissingLogged = true; musicWant = MusicId.None; return; }
                if (music.clip != clip) { music.Stop(); music.clip = clip; musicGain = 0f; musicPaused = false; }
                musicLoaded = id;
            }
            Resume();
        }

        static bool musicPaused, musicMissingLogged;
        static void Resume()
        {
            if (music.isPlaying) return;
            if (musicPaused) music.UnPause(); else music.Play();
            musicPaused = false;
        }

        /// <summary>Fades the music out (it is paused, not rewound).</summary>
        public static void StopMusic() => PlayMusic(MusicId.None);

        // the menu track is optional: until Rickard drops Resources/BAM/Music/Menu in, the menu plays the match loop
        static string MusicPath(MusicId id) => id == MusicId.Menu ? "BAM/Music/Menu" : "BAM/Music/ByteStrikeLoop";

        /// <summary>Silences everything at once (pause, leaving a match); the music fades out quickly and can be resumed.</summary>
        public static void StopAll()
        {
            if (!Ready) return;
            musicWant = MusicId.None;
            for (int i = 0; i < Voices; i++)
            {
                if (src[i] != null) src[i].Stop();
                voiceEnd[i] = 0f; voiceId[i] = SfxId.None;
            }
        }

        static void Start(SfxId id, float volume, float pitch, float pan)
        {
            int k = (int)id;
            if (!Ready || k <= 0 || k >= SfxRecipes.Count) return;
            float v = volume;
            if (v * master <= 0.005f) return;
            ref SfxInfo inf = ref info[k];
            float now = Time.unscaledTime;

            // a burst of the same sound collapses into one, unless the new one is clearly louder (much nearer)
            if (now - lastStart[k] < inf.MinGap && v <= lastVol[k] * 1.5f) return;

            int same = 0, free = -1, steal = -1;
            for (int i = 0; i < Voices; i++)
            {
                if (voiceEnd[i] <= now) { if (free < 0) free = i; continue; }
                if (voiceId[i] == id) same++;
                // the steal candidate: the least important voice, the one closest to finishing among equals
                if (voicePri[i] <= inf.Priority && (steal < 0 || voicePri[i] < voicePri[steal] ||
                    voicePri[i] == voicePri[steal] && voiceEnd[i] < voiceEnd[steal])) steal = i;
            }
            if (same >= inf.MaxVoices) return;
            int vi = free >= 0 ? free : steal;
            if (vi < 0) return;

            var clip = clips[k];
            if (clip == null) clip = Build(k); // asked for before the builder got to it: make it now

            if (inf.Vary > 0f) pitch *= 1f + inf.Vary * (Rand() * 2f - 1f);
            var a = src[vi];
            a.Stop();
            a.clip = clip;
            a.pitch = pitch;
            a.panStereo = pan;
            v *= clipGain[k];
            a.volume = Mathf.Min(1f, v * master);
            a.Play();

            voiceId[vi] = id; voicePri[vi] = inf.Priority; voiceVol[vi] = v;
            voiceEnd[vi] = now + clip.length / Mathf.Max(0.05f, pitch);
            lastStart[k] = now; lastVol[k] = v;
        }

        static void OnSettingsChanged()
        {
            master = BamSettings.SfxVolume;
            float now = Time.unscaledTime;
            for (int i = 0; i < Voices; i++)
                if (voiceEnd[i] > now && src[i] != null) src[i].volume = Mathf.Min(1f, voiceVol[i] * master);
        }

        static float Rand()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return (rng & 0xFFFFFF) / 16777216f;
        }

        static AudioClip Build(int k)
        {
            var sw = Stopwatch.StartNew();
            // a recorded clip dropped in Resources/BAM/Sfx/<SfxId name> replaces the synth recipe (Rickard's own AI clips)
            var file = Resources.Load<AudioClip>(SfxFolder + ((SfxId)k).ToString());
            if (file != null)
            {
                clips[k] = file;
                clipGain[k] = FileGain(file, info[k].Level);
                buildMs += sw.Elapsed.TotalMilliseconds;
                return file;
            }
            clipGain[k] = 1f;
            var data = SfxRecipes.Render(synth, (SfxId)k);
            var clip = AudioClip.Create("bam_" + (SfxId)k, data.Length, 1, SfxSynth.Rate, false);
            clip.SetData(data, 0);
            clips[k] = clip;
            buildMs += sw.Elapsed.TotalMilliseconds;
            return clip;
        }

        const string SfxFolder = "BAM/Sfx/";
        const float FileTargetRms = 0.15f; // the synth's own target (SfxSynth.Finish)

        // Loudness of a file over its sounding part, so files from different sessions sit at the same level as the synth.
        // A clip that cannot be read (streamed or compressed in memory) keeps its own level.
        static float FileGain(AudioClip c, float level)
        {
            int n = c.samples * c.channels;
            if (n <= 0 || n > 44100 * 2 * 4) return level;
            var d = new float[n];
            if (!c.GetData(d, 0)) return level;
            float peak = 0f;
            for (int i = 0; i < n; i++) { float a = Math.Abs(d[i]); if (a > peak) peak = a; }
            if (peak < 1e-4f) return 1f;
            double sum = 0; int cnt = 0; float floor = peak * 0.05f;
            for (int i = 0; i < n; i++) { float a = Math.Abs(d[i]); if (a > floor) { sum += (double)a * a; cnt++; } }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, cnt));
            // never push a peak past full scale
            return Mathf.Clamp(Mathf.Min(FileTargetRms * level / Mathf.Max(rms, 1e-4f), 0.95f / peak), 0.2f, 3f);
        }

        static void BuildNext()
        {
            while (nextBuild < SfxRecipes.Count && clips[nextBuild] != null) nextBuild++;
            if (nextBuild < SfxRecipes.Count) Build(nextBuild++);
            if (AllBuilt) UnityEngine.Debug.Log($"[BAM.Audio] {SfxRecipes.Count - 1} sounds built in {buildMs:F0} ms");
        }

        /// <summary>Moves the music volume toward its target every frame and pauses the source once it is silent.</summary>
        sealed class MusicFader : MonoBehaviour
        {
            void Update()
            {
                if (music == null || music.clip == null) return;
                bool on = musicWant != MusicId.None && musicWant == musicLoaded;
                float dt = Time.unscaledDeltaTime;
                musicGain = on ? Mathf.Min(1f, musicGain + dt / MusicFadeIn) : Mathf.Max(0f, musicGain - dt / MusicFadeOut);
                music.volume = musicGain * musicGain * MusicLevel * master; // squared: the fade sounds even to the ear
                if (on) Resume();
                else if (musicGain <= 0f && music.isPlaying) { music.Pause(); musicPaused = true; }
            }
        }

        /// <summary>Builds the clips a few milliseconds per frame, then removes itself.</summary>
        sealed class ClipBuilder : MonoBehaviour
        {
            readonly Stopwatch sw = new Stopwatch();

            void Update()
            {
                sw.Restart();
                do BuildNext(); while (!AllBuilt && sw.Elapsed.TotalMilliseconds < BuildBudgetMs);
                if (AllBuilt) Destroy(this);
            }
        }
    }
}
