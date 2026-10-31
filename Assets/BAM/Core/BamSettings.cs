using System;
using UnityEngine;

namespace BAM.Core
{
    public enum GraphicsQuality { Low = 0, High = 1 }

    public enum Consent { Unknown = 0, Granted = 1, Denied = 2 }

    /// <summary>
    /// The player's settings, saved with PlayerPrefs. Every module reads them here and listens to <see cref="Changed"/>:
    /// Audio (SfxVolume, MusicOn), the quality code (Graphics, Fps), BamHaptics (Vibration), the HUD (LeftHanded),
    /// Analytics (AnalyticsConsent).
    /// The Settings screen (BAM.UI) is the only place that writes them, apart from the flags the game sets itself.
    /// </summary>
    public static class BamSettings
    {
        const string Key = "bam.";
        static bool loaded;
        static float sfxVolume;
        static GraphicsQuality graphics;
        static bool leftHanded, tutorialDone, tutorialCard, musicOn, vibration;
        static int fps;
        static Consent consent;
        static int lastHero, lastDifficulty, spell;

        /// <summary>Raised after any setting changed (on the main thread).</summary>
        public static event Action Changed;

        public static float SfxVolume
        {
            get { Load(); return sfxVolume; }
            set { Load(); value = Mathf.Clamp01(value); if (Mathf.Approximately(sfxVolume, value)) return; sfxVolume = value; PlayerPrefs.SetFloat(Key + "sfx", value); Commit(); }
        }

        /// <summary>The music on or off (the old Settings' Music row); the effects follow SfxVolume.</summary>
        public static bool MusicOn
        {
            get { Load(); return musicOn; }
            set { Load(); if (musicOn == value) return; musicOn = value; PlayerPrefs.SetInt(Key + "music", value ? 1 : 0); Commit(); }
        }

        /// <summary>Short buzzes on button presses and in a match (BamHaptics; the old Settings' Vibration row).</summary>
        public static bool Vibration
        {
            get { Load(); return vibration; }
            set { Load(); if (vibration == value) return; vibration = value; PlayerPrefs.SetInt(Key + "vib", value ? 1 : 0); Commit(); }
        }

        /// <summary>The frame rate the game asks for: 30 or 60 (60 unless the player picks 30 to save battery).</summary>
        public static int Fps
        {
            get { Load(); return fps; }
            set { Load(); value = value <= 30 ? 30 : 60; if (fps == value) return; fps = value; PlayerPrefs.SetInt(Key + "fps", value); Commit(); }
        }

        public static GraphicsQuality Graphics
        {
            get { Load(); return graphics; }
            set { Load(); if (graphics == value) return; graphics = value; PlayerPrefs.SetInt(Key + "gfx", (int)value); Commit(); }
        }

        /// <summary>Mirrors the HUD: joystick on the right, special button on the left.</summary>
        public static bool LeftHanded
        {
            get { Load(); return leftHanded; }
            set { Load(); if (leftHanded == value) return; leftHanded = value; PlayerPrefs.SetInt(Key + "left", value ? 1 : 0); Commit(); }
        }

        /// <summary>Unknown until the player answered the consent question. Analytics runs only while Granted.</summary>
        public static Consent AnalyticsConsent
        {
            get { Load(); return consent; }
            set { Load(); if (consent == value) return; consent = value; PlayerPrefs.SetInt(Key + "consent", (int)value); Commit(); }
        }

        public static bool TutorialDone
        {
            get { Load(); return tutorialDone; }
            set { Load(); if (tutorialDone == value) return; tutorialDone = value; PlayerPrefs.SetInt(Key + "tut", value ? 1 : 0); Commit(); }
        }

        /// <summary>The card on Home after the first finished (or skipped) tutorial, "Tutorial done · Next: your first match"
        /// (the old HubTutCard): a tap on it or the first finished bot match takes it away.</summary>
        public static bool TutorialCard
        {
            get { Load(); return tutorialCard; }
            set { Load(); if (tutorialCard == value) return; tutorialCard = value; PlayerPrefs.SetInt(Key + "tutcard", value ? 1 : 0); Commit(); }
        }

        /// <summary>The player has picked a hero or a bot difficulty before (the tutorial then leaves the picks alone).</summary>
        public static bool PickedBefore => PlayerPrefs.HasKey(Key + "hero") || PlayerPrefs.HasKey(Key + "diff");

        /// <summary>The hero picked last time (index into HeroCatalog.All), so hero select opens on it.</summary>
        public static int LastHero
        {
            get { Load(); return lastHero; }
            set { Load(); if (lastHero == value) return; lastHero = value; PlayerPrefs.SetInt(Key + "hero", value); Commit(); }
        }

        /// <summary>The battle spell the player takes into every match (the old hero page's BATTLE SPELL card, "abc2-spell"):
        /// it counts for every hero. Blink until the player picks another.</summary>
        public static BAM.Sim.SpellId Spell
        {
            get { Load(); return (BAM.Sim.SpellId)spell; }
            set { Load(); int v = value == BAM.Sim.SpellId.None ? (int)BAM.Sim.SpellId.Blink : (int)value; if (spell == v) return; spell = v; PlayerPrefs.SetInt(Key + "spell", v); Commit(); }
        }

        public static BAM.Sim.Difficulty LastDifficulty
        {
            get { Load(); return (BAM.Sim.Difficulty)lastDifficulty; }
            set { Load(); if (lastDifficulty == (int)value) return; lastDifficulty = (int)value; PlayerPrefs.SetInt(Key + "diff", lastDifficulty); PlayerPrefs.SetInt(Key + "diffrun", 0); Commit(); }
        }

        /// <summary>
        /// The bot level follows the player (Rickard 2026-10-31 "om man inte vinner matcher ska man inte tvingas till hard matcher
        /// och helst hållas nere på easy tills det börjar gå bra"): after an offline bot match at the picked level, 2 losses in a
        /// row move the pick one level down, and a run of wins on Easy moves it up to Normal: 3 the first time, 2 more after every
        /// step down (at most 7), so a new player does not bounce between Easy and Normal (real matches 30-31 Oct: Easy 82 %,
        /// Normal 27 %). Hard and Ultra are only ever picked by hand. The player can always pick another level. Returns true
        /// when the pick moved.
        /// </summary>
        public static bool AfterBotMatch(BAM.Sim.Difficulty played, bool won)
        {
            Load();
            if ((int)played != lastDifficulty) return false;
            int run = PlayerPrefs.GetInt(Key + "diffrun", 0), need = PlayerPrefs.GetInt(Key + "diffneed", 3);
            run = won ? Mathf.Max(0, run) + 1 : Mathf.Min(0, run) - 1;
            int to = lastDifficulty;
            if (run <= -2 && lastDifficulty > 0) { to = lastDifficulty - 1; PlayerPrefs.SetInt(Key + "diffneed", Mathf.Min(7, need + 2)); }
            else if (run >= need && lastDifficulty == (int)BAM.Sim.Difficulty.Easy) to = (int)BAM.Sim.Difficulty.Normal;
            if (to != lastDifficulty) { LastDifficulty = (BAM.Sim.Difficulty)to; return true; }
            PlayerPrefs.SetInt(Key + "diffrun", run); Commit();
            return false;
        }

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            sfxVolume = PlayerPrefs.GetFloat(Key + "sfx", 0.8f);
            graphics = (GraphicsQuality)PlayerPrefs.GetInt(Key + "gfx", (int)DefaultGraphics());
            leftHanded = PlayerPrefs.GetInt(Key + "left", 0) == 1;
            musicOn = PlayerPrefs.GetInt(Key + "music", 1) == 1;
            vibration = PlayerPrefs.GetInt(Key + "vib", 1) == 1;
            fps = PlayerPrefs.GetInt(Key + "fps", 60) <= 30 ? 30 : 60;
            consent = (Consent)PlayerPrefs.GetInt(Key + "consent", 0);
            tutorialDone = PlayerPrefs.GetInt(Key + "tut", 0) == 1;
            tutorialCard = PlayerPrefs.GetInt(Key + "tutcard", 0) == 1;
            lastHero = Mathf.Clamp(PlayerPrefs.GetInt(Key + "hero", 0), 0, BAM.Sim.HeroCatalog.Count - 1);
            lastDifficulty = Mathf.Clamp(PlayerPrefs.GetInt(Key + "diff", (int)BAM.Sim.Difficulty.Normal), 0, (int)BAM.Sim.Difficulty.Ultra);
            spell = Mathf.Clamp(PlayerPrefs.GetInt(Key + "spell", (int)BAM.Sim.SpellId.Blink), (int)BAM.Sim.SpellId.Blink, (int)BAM.Sim.SpellId.Sprint);
        }

        // First start: Low on weak phones (little memory or few cores), else High. The player can change it in Settings.
        static GraphicsQuality DefaultGraphics() =>
            SystemInfo.systemMemorySize > 0 && SystemInfo.systemMemorySize < 3500 || SystemInfo.processorCount < 6 ? GraphicsQuality.Low : GraphicsQuality.High;

        static void Commit()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
