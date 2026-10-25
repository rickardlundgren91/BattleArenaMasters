using System;
using BAM.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // CONTRACT between BAM.UI (owner: the menus thread) and BAM.Game (owner: the main thread). Docs/CONTRACTS.md explains it.
    // Every screen is built in code with uGUI and the Lilita One font. Buttons call BAM.Core.Flow.Game (IGameFlow) and
    // write BAM.Core.BamSettings; they never reach into BAM.Game.

    /// <summary>The shared look, also used by the in-match HUD (BAM.Game).</summary>
    public static class UiKit
    {
        public static Font Font => null;

        /// <summary>A screen-space canvas scaled for portrait phones (reference 1080x1920, match width/height 0.5).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder) => null;
        /// <summary>A full-screen child that keeps clear of notches and rounded corners (Screen.safeArea).</summary>
        public static RectTransform SafeArea(Canvas canvas) => null;
        public static Text Label(Transform parent, string text, int size, TextAnchor anchor, Color color) => null;
        public static Button Button(Transform parent, string text, Action onClick) => null;
        public static Image Panel(Transform parent, Color color) => null;

        public static readonly Color Ally = new Color(0.25f, 0.55f, 1f);
        public static readonly Color Enemy = new Color(0.95f, 0.25f, 0.2f);
    }

    public static class Screens
    {
        /// <summary>Once at boot, before anything is shown.</summary>
        public static void Init() { }

        /// <summary>First start: asks for analytics consent if BamSettings.AnalyticsConsent is Unknown, then calls done.</summary>
        public static void ShowConsentIfNeeded(Action done) { done?.Invoke(); }

        public static void ShowMainMenu() { }
        public static void ShowPause() { }
        public static void HidePause() { }
        public static void ShowResult(MatchResult result) { }

        /// <summary>Guided match: a hint bubble with one short sentence. anchorScreenPos = what it points at (null = centred).</summary>
        public static void ShowHint(string text, Vector2? anchorScreenPos = null) { }
        public static void HideHint() { }

        public static void HideAll() { }

        /// <summary>True while a menu or dialog covers the match (the HUD ignores touches then).</summary>
        public static bool IsBlockingInput => false;
    }
}
