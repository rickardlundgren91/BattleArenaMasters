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
        static bool leftHanded, tutorialDone, tutorialCard, tutorialSkipped, musicOn, vibration, hitVibration, hardOpen;
        static int fps;
        static Consent consent;
        static int lastHero, lastDifficulty, spell, lastMap, teamSize;

        /// <summary>Raised after any setting changed (on the main thread).</summary>
        public static event Action Changed;

        /// <summary>The skin this player wears on a hero (BAM.Art HeroSkins.Chosen: only a look the player may wear, gold skins
        /// bought); null = none. Online sends it to the other phones, so they see the player's skin too (Rickard 2026-11-15).</summary>
        public static Func<BAM.Sim.HeroId, int> SkinOf;
        /// <summary>Raised when the player picked another skin (HeroSkins.Choose), so a room can tell the others.</summary>
        public static event Action SkinPicked;
        public static void RaiseSkinPicked() => SkinPicked?.Invoke();

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

        /// <summary>A light buzz each time the player's own auto attack hits (tester feedback, Ski 2026-11-07: "an optional rumble
        /// for light attacks that hit"). OFF until the player turns it on: vibration is for the big moments (Rickard 2026-10-26).
        /// Needs <see cref="Vibration"/> on too.</summary>
        public static bool HitVibration
        {
            get { Load(); return hitVibration; }
            set { Load(); if (hitVibration == value) return; hitVibration = value; PlayerPrefs.SetInt(Key + "hitvib", value ? 1 : 0); Commit(); }
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

        /// <summary>The guided match has started by itself once (Boot's first start). It never starts by itself again: a player who
        /// left it without Skip or Pause > Home (closed the app, or stopped Play in the Editor) lands on Home next time, with the
        /// "Tutorial skipped" card (Rickard 2026-11-16: "När jag öppnar spelet så startar det ofta ett game direkt. Sluta med det").</summary>
        public static bool TutorialOffered
        {
            get => PlayerPrefs.GetInt(Key + "tutoff", 0) == 1;
            set { PlayerPrefs.SetInt(Key + "tutoff", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>The card on Home after the first finished (or skipped) tutorial, "Tutorial done · Next: your first match"
        /// (the old HubTutCard): a tap on it or the first finished bot match takes it away.</summary>
        public static bool TutorialCard
        {
            get { Load(); return tutorialCard; }
            set { Load(); if (tutorialCard == value) return; tutorialCard = value; PlayerPrefs.SetInt(Key + "tutcard", value ? (tutorialSkipped ? 2 : 1) : 0); Commit(); }
        }

        /// <summary>The card on Home is for a skipped tutorial: "Tutorial skipped · Play it any time in Settings > How to play" in place
        /// of "Tutorial done" (full review 2026-11-13 nr 112). Kept in the same key as TutorialCard: 1 done, 2 skipped.</summary>
        public static bool TutorialSkipped
        {
            get { Load(); return tutorialSkipped; }
            set { Load(); if (tutorialSkipped == value) return; tutorialSkipped = value; if (tutorialCard) { PlayerPrefs.SetInt(Key + "tutcard", value ? 2 : 1); Commit(); } }
        }

        /// <summary>The player has picked a hero or a bot difficulty before (the tutorial then leaves the picks alone).</summary>
        public static bool PickedBefore => PlayerPrefs.HasKey(Key + "hero") || PlayerPrefs.HasKey(Key + "diff");

        /// <summary>The hero picked last time (a HeroId number), so hero select opens on it. Tyra until the player picks (the beginner hero, 2026-11-04).</summary>
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

        /// <summary>The map CLASSIC plays (MAP under BOT DIFFICULTY): Runes until the player picks Grove (Rickard 2026-11-13:
        /// "lägg in grove som en valbar match precis som den vanliga kartan").</summary>
        /// <summary>Plateau (the third map) is not in the map pick: not finished (Rickard 2026-11-19: "ta bort 3e mapen nu innan vi gör
        /// detta till en update för den e inte klar", "plateau"). Its files stay; true puts it back in MODES.</summary>
        public const bool PlateauOpen = false;
        /// <summary>The highest map the pick offers now.</summary>
        public static BAM.Sim.MapId MaxMap => PlateauOpen ? BAM.Sim.MapId.Plateau : BAM.Sim.MapId.Grove;

        public static BAM.Sim.MapId LastMap
        {
            get { Load(); return (BAM.Sim.MapId)Mathf.Min(lastMap, (int)MaxMap); }
            set { Load(); if (lastMap == (int)value) return; lastMap = (int)value; PlayerPrefs.SetInt(Key + "map", lastMap); Commit(); }
        }

        /// <summary>Heroes per team CLASSIC and the online queue play: 3 (3v3) or 2 (2v2, Rickard 2026-11-15: "lägg in 2v2 mode").</summary>
        public static int TeamSize
        {
            get { Load(); return teamSize; }
            set { Load(); int v = value == 2 ? 2 : 3; if (teamSize == v) return; teamSize = v; PlayerPrefs.SetInt(Key + "teamsize", v); Commit(); }
        }

        public static BAM.Sim.Difficulty LastDifficulty
        {
            get { Load(); return OpenLevel((BAM.Sim.Difficulty)lastDifficulty); }
            set { Load(); value = OpenLevel(value); if (lastDifficulty == (int)value) return; lastDifficulty = (int)value; PlayerPrefs.SetInt(Key + "diff", lastDifficulty); PlayerPrefs.SetInt(Key + "diffrun", 0); Commit(); }
        }

        /// <summary>The level itself when it is open, else the nearest open level under it: a locked Ultra plays Hard, a locked Hard Normal.</summary>
        public static BAM.Sim.Difficulty OpenLevel(BAM.Sim.Difficulty d)
        {
            if (d == BAM.Sim.Difficulty.Ultra && !UltraOpen) d = BAM.Sim.Difficulty.Hard;
            if (d == BAM.Sim.Difficulty.Hard && !HardOpen) d = BAM.Sim.Difficulty.Normal;
            return d;
        }

        /// <summary>Hard opens after this many won matches against Normal bots, counted on this phone like Ultra's wins (Rickard
        /// 2026-11-04: good, hard bots, but a new player who loses too much stops playing; 2 of 4 new players went from their first
        /// Easy win straight to Hard). A phone that had Hard or Ultra picked, or a win against Hard, when the lock came keeps Hard (Load).</summary>
        public const int NormalWinsForHard = 3;
        public static int NormalWins => PlayerPrefs.GetInt(Key + "normalwins", 0);
        public static bool HardOpen { get { Load(); return hardOpen || NormalWins >= NormalWinsForHard; } }
        public static void AddNormalWin() { PlayerPrefs.SetInt(Key + "normalwins", NormalWins + 1); Commit(); }

        /// <summary>Ultra opens after this many won matches against Hard bots (Rickard 2026-10-31), counted on this phone.</summary>
        public const int UltraWins = 30;
        public static int HardWins => PlayerPrefs.GetInt(Key + "hardwins", 0);
        public static bool UltraOpen => HardOpen && (HardWins >= UltraWins || Application.isEditor); // open in the Unity editor for testing, like the heroes, once Hard is
        public static void AddHardWin() { PlayerPrefs.SetInt(Key + "hardwins", HardWins + 1); Commit(); }

        /// <summary>
        /// The bot level follows the player (Rickard 2026-10-31 "om man inte vinner matcher ska man inte tvingas till hard matcher
        /// och helst hållas nere på easy tills det börjar gå bra"): after an offline bot match at the picked level, 2 losses in a
        /// row on Normal move the pick down to Easy, and a run of wins on Easy moves it up to Normal: 3 the first time, 2 more
        /// after every step down (at most 7), so a new player does not bounce between Easy and Normal (real matches 30-31 Oct:
        /// Easy 82 %, Normal 27 %). Hard and Ultra change only by hand, never by themselves (Rickard 2026-11-01: a player who
        /// picked Hard keeps Hard, its rating and its wins towards Ultra). The player can always pick another open level (Hard
        /// opens after NormalWinsForHard wins against Normal, Ultra after UltraWins against Hard). Returns true when the pick moved
        /// (the result card says so).
        /// </summary>
        public const int BeginnerDownLosses = 3, BeginnerUpWins = 2;
        public static bool AfterBotMatch(BAM.Sim.Difficulty played, bool won)
        {
            Load();
            if ((int)played != lastDifficulty) return false;
            int run = PlayerPrefs.GetInt(Key + "diffrun", 0), need = PlayerPrefs.GetInt(Key + "diffneed", 3);
            run = won ? Mathf.Max(0, run) + 1 : Mathf.Min(0, run) - 1;
            int to = lastDifficulty;
            if (run <= -2 && lastDifficulty == (int)BAM.Sim.Difficulty.Normal) { to = (int)BAM.Sim.Difficulty.Easy; PlayerPrefs.SetInt(Key + "diffneed", Mathf.Min(7, need + 2)); }
            else if (run >= need && lastDifficulty == (int)BAM.Sim.Difficulty.Easy) to = (int)BAM.Sim.Difficulty.Normal;
            // Beginner (2026-11-18): 3 losses in a row on Easy move the pick down to Beginner, 2 wins in a row there back up to Easy
            else if (run <= -BeginnerDownLosses && lastDifficulty == (int)BAM.Sim.Difficulty.Easy) to = (int)BAM.Sim.Difficulty.Beginner;
            else if (run >= BeginnerUpWins && lastDifficulty == (int)BAM.Sim.Difficulty.Beginner) to = (int)BAM.Sim.Difficulty.Easy;
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
            hitVibration = PlayerPrefs.GetInt(Key + "hitvib", 0) == 1;
            fps = PlayerPrefs.GetInt(Key + "fps", 60) <= 30 ? 30 : 60;
            consent = (Consent)PlayerPrefs.GetInt(Key + "consent", 0);
            tutorialDone = PlayerPrefs.GetInt(Key + "tut", 0) == 1;
            int tutCard = PlayerPrefs.GetInt(Key + "tutcard", 0);
            tutorialCard = tutCard == 1 || tutCard == 2;
            tutorialSkipped = tutCard == 2;
            lastHero = (int)BAM.Sim.HeroCatalog.CleanMenu(PlayerPrefs.GetInt(Key + "hero", (int)BAM.Sim.HeroId.Tyra)); // keeps an editor preview hero (Ragn, Nässla)
            lastDifficulty = (int)BAM.Sim.DifficultyOrder.Clean(PlayerPrefs.GetInt(Key + "diff", (int)BAM.Sim.Difficulty.Normal));
            // Hard's lock came 2026-11-04: a phone that already had Hard or Ultra picked, or a win against Hard, keeps Hard open.
            // Decided once, on the first start with the lock, and saved, so it never changes after that (a new phone: locked).
            if (!PlayerPrefs.HasKey(Key + "hardopen"))
            {
                bool had = lastDifficulty == (int)BAM.Sim.Difficulty.Hard || lastDifficulty == (int)BAM.Sim.Difficulty.Ultra || PlayerPrefs.GetInt(Key + "hardwins", 0) > 0;
                PlayerPrefs.SetInt(Key + "hardopen", had ? 1 : 0);
                PlayerPrefs.Save();
            }
            hardOpen = PlayerPrefs.GetInt(Key + "hardopen", 0) == 1;
            // Grove is CLASSIC's map until a player picks RUNES, so every tester tries it (Rickard 2026-11-13: "gör grove till standard")
            teamSize = PlayerPrefs.GetInt(Key + "teamsize", 3) == 2 ? 2 : 3;
            lastMap = Mathf.Clamp(PlayerPrefs.GetInt(Key + "map", (int)BAM.Sim.MapId.Grove), (int)BAM.Sim.MapId.Runes, (int)MaxMap); // a saved Plateau plays Grove while Plateau is hidden
            spell = Mathf.Clamp(PlayerPrefs.GetInt(Key + "spell", (int)BAM.Sim.SpellId.Blink), (int)BAM.Sim.SpellId.Blink, (int)BAM.Sim.SpellId.Bladearmor);
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
