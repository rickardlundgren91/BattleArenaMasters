# Brief: the game's sounds, made in code ("Gör spelets ljud i kod")

You are a helper thread in the BAM Unity rebuild. Read `Docs/ARCHITECTURE.md`, `Docs/CONTRACTS.md` (Audio section) and
`Docs/DECISIONS.md` in `/mnt/project-files/bam-unity/` first. Your folder: `Assets/BAM/Audio/` (BAM.Audio). Effort: medium.

## Goal
Implement `Assets/BAM/Audio/BamAudio.cs` (keep the stub's public API) with every `SfxId` synthesized in code at boot:
no audio files, no music. The old game synthesized its sounds too: `/mnt/project-files/bam-ref/xyz/ABC2/Game/Sound.cs`
(Rickard's own code; reuse its recipes and ideas freely).

## Deliverables
1. A small synth: oscillators (sine, square, saw, triangle, noise), envelopes, pitch sweeps, simple filters, layering,
   rendered into `AudioClip.Create` PCM at `Init` (mono, 22050 or 44100 Hz). Keep the whole build under about 150 ms
   on a mid-range phone; spread it over frames if needed.
2. Every `SfxId` designed to fit a bright cartoon MOBA on phone speakers: short, punchy, clear transients, nothing harsh
   above about 8 kHz, no clipping, each hero's attack and special recognisable.
3. Playback: a pool of about 16 `AudioSource`s on one `DontDestroyOnLoad` object, a per-id voice limit and minimum
   interval (20 minion hits in a frame must not stack), `PlayAt` falling off with distance from `SetListener` (gentle;
   off-screen fights stay faintly audible), volume from `BamSettings.SfxVolume` (listen to `BamSettings.Changed`),
   `StopAll`. No garbage per call.
4. A way for Rickard to listen to every sound in his Unity: for example **BAM > Sound Preview**, a window that works in
   Play mode with one button per sound, or a menu that writes all sounds as WAV files to a folder outside `Assets`.

## Notes
- Compile with `python3 /mnt/project-files/bam-tools/compilecheck/compilecheck.py --only BAM.Audio`, then the full check.
- You can render your synth to WAV here (a small C# or Python harness outside the project) to inspect waveforms and
  levels, but you cannot listen: describe each sound so Rickard knows what to expect, and ask him in your thread which
  ones to redo after he listened.
