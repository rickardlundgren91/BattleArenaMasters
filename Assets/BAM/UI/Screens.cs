using System;
using System.Collections.Generic;
using BAM.Audio;
using BAM.Core;
using BAM.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace BAM.UI
{
    // CONTRACT between BAM.UI (owner: the menus thread) and BAM.Game (owner: the main thread). Docs/CONTRACTS.md explains it.
    // Every screen is built in code with uGUI and the Lilita One font. Buttons call BAM.Core.Flow.Game (IGameFlow) and
    // write BAM.Core.BamSettings; they never reach into BAM.Game.
    //
    // Every screen is built once in Init and then only shown and hidden. Screens form a stack (main menu -> hero select,
    // pause -> settings -> licenses); the back button pops it. A dialog (consent, confirm) sits above the stack.

    public static partial class Screens
    {
        // Above the HUD (which should stay below 50); the hint bubble sits between the HUD and the menus.
        const int MenuOrder = 100, HintOrder = 60;

        static bool ready;
        static Canvas menuCanvas;
        static RectTransform root;
        static readonly List<GameObject> stack = new List<GameObject>(8);
        static Dialog dialog;

        /// <summary>Once at boot, before anything is shown.</summary>
        public static void Init()
        {
            if (ready) return;
            ready = true;
            menuCanvas = UiKit.CreateCanvas("BAM.Menus", MenuOrder);
            menuCanvas.gameObject.AddComponent<UiRoot>();
            root = (RectTransform)menuCanvas.transform;
            BuildMainMenu();
            BuildHeroSelect();
            BuildSettings();
            BuildLicenses();
            BuildNav();
            BuildPause();
            BuildResult();
            dialog = new Dialog(root);
            BuildHint();
        }

        /// <summary>First start: asks for analytics consent if BamSettings.AnalyticsConsent is Unknown, then calls done.</summary>
        public static void ShowConsentIfNeeded(Action done)
        {
            if (BamSettings.AnalyticsConsent != Consent.Unknown) { done?.Invoke(); return; }
            Init();
            var art = ConsentArt(out var logoLayer);
            // Both answers look the same and are the same size (Google Play: declining must be as easy as accepting).
            dialog.Show(ConsentText.Title, ConsentText.Body,
                ConsentText.No, () => { DropConsentArt(art, logoLayer); BamSettings.AnalyticsConsent = Consent.Denied; done?.Invoke(); },
                ConsentText.Yes, () => { DropConsentArt(art, logoLayer); BamSettings.AnalyticsConsent = Consent.Granted; done?.Invoke(); },
                UiKit.Primary, UiKit.Primary, null,
                string.IsNullOrEmpty(BamInfo.PrivacyPolicyUrl) ? null : "Privacy policy",
                () => Application.OpenURL(BamInfo.PrivacyPolicyUrl));
        }

        // full review 2026-11-13 nr 126: the first thing a new player sees was the consent box on a bare dark-blue screen. Now
        // Home's painting lies under the dialog's dark veil and Rickard's logo (the app icon, Resources/BAM/Menu/bam_logo)
        // stands bright above the box. Built for the question only and destroyed with the answer.
        static GameObject ConsentArt(out GameObject logoLayer)
        {
            logoLayer = null;
            var layer = dialog.Root;
            var art = UiKit.NewRect("ConsentArt", layer);
            var go = art.gameObject;
            UiKit.Stretch(art);
            art.SetSiblingIndex(0); // under the veil (now child 1), so the painting is dimmed and the box stays readable

            var bg = Picture("home_bg", true);
            if (bg != null)
            {
                var paint = UiKit.NewRect("Painting", art);
                paint.anchorMin = paint.anchorMax = paint.pivot = new Vector2(0.5f, 0.5f);
                var raw = paint.gameObject.AddComponent<RawImage>();
                raw.texture = bg;
                raw.raycastTarget = false;
                var fit = paint.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // fills the screen, cut at the sides
                fit.aspectRatio = (float)bg.width / bg.height;
            }

            var logo = Picture("bam_logo", true);
            if (logo != null)
            {
                // over the veil: a layer of its own right after it (art 0, veil 1), under the box's safe area
                var top = UiKit.NewRect("ConsentLogo", layer);
                UiKit.Stretch(top);
                top.SetSiblingIndex(2);
                var safe = UiKit.SafeArea(top);
                var pic = UiKit.NewRect("Icon", safe);
                pic.anchorMin = pic.anchorMax = pic.pivot = new Vector2(0.5f, 1);
                pic.anchoredPosition = new Vector2(0, -90);
                pic.sizeDelta = new Vector2(400, 400);
                var raw = pic.gameObject.AddComponent<RawImage>();
                raw.texture = logo;
                raw.raycastTarget = false;
                logoLayer = top.gameObject;
            }
            return go;
        }

        static void DropConsentArt(GameObject art, GameObject logoLayer)
        {
            if (art != null) UnityEngine.Object.Destroy(art);
            if (logoLayer != null) UnityEngine.Object.Destroy(logoLayer);
        }

        public static void ShowMainMenu()
        {
            Init();
            ClearStack();
            Push(mainMenu);
            if (!updateAsked) HoldPlay(); // the first Home of this start (see OnPlay)
            CheckForUpdate(); // once per start: a newer version on Google Play (UpdateNotice.cs)
            // Rickard: Byte Strike for all music for now (BamAudio plays it for Menu until a menu track exists); after a
            // match the same loop goes on where it was
            BamAudio.PlayMusic(MusicId.Menu);
        }

        public static void ShowPause()
        {
            Init();
            ClearStack();
            Push(pause);
        }

        public static void HidePause()
        {
            if (!ready) return;
            if (stack.Count > 0 && stack[0] == pause)
            {
                ClearStack();
                dialog.Hide();
            }
        }

        public static void ShowResult(MatchResult result) => ShowResult(result, null);

        /// <summary>The result card; with the match it shows every hero's K/D/A, rating and MVP (count them with CountEvents).</summary>
        public static void ShowResult(MatchResult result, Match match)
        {
            Init();
            ClearStack();
            dialog.Hide();
            HideHint();
            FillResult(result, match);
            NoteMatchForOnline(result);
            Push(resultScreen);
            if (!result.Quit) BamAudio.Play(result.Won ? SfxId.Victory : SfxId.Defeat);
        }

        public static void HideAll()
        {
            if (!ready) return;
            ClearStack();
            dialog.Hide();
            HideHint();
        }

        /// <summary>True while a menu or dialog covers the match (the HUD ignores touches then).</summary>
        public static bool IsBlockingInput => stack.Count > 0 || (dialog != null && dialog.Open);

        /// <summary>
        /// The Android back button. The UI calls it itself outside a match; during a match the game layer calls it and, when
        /// it returns false (nothing of ours is open), opens the pause menu. Closes the top dialog or goes back one screen;
        /// on the pause menu it resumes, on the result screen it goes to the menu.
        /// </summary>
        public static bool HandleBack()
        {
            if (!ready) return false;
            if (OnlineHooks.Back != null && OnlineHooks.Back()) return true; // an online page or invite closes first
            if (dialog.Open)
            {
                // The consent question has no back action on purpose: the player answers Allow or No thanks.
                if (dialog.Back != null) { BamAudio.Play(SfxId.UiBack); dialog.Back(); }
                return true;
            }
            if (stack.Count == 0) return false;
            var top = stack[stack.Count - 1];
            if (top == pause) { BamAudio.Play(SfxId.UiBack); ResumeMatch(); return true; }
            if (top == resultScreen) { BamAudio.Play(SfxId.UiBack); GoToMenu(); return true; }
            if (stack.Count > 1) { BamAudio.Play(SfxId.UiBack); Pop(); return true; }
            return true; // the main menu has nothing below it; swallow the press instead of closing the app
        }

        // ---- navigation ----

        static void Push(GameObject screen)
        {
            // An overlay (the Modes sheet) leaves the screen under it visible.
            if (stack.Count > 0 && !IsOverlay(screen)) stack[stack.Count - 1].SetActive(false);
            stack.Add(screen);
            screen.SetActive(true);
            OnShown(screen);
            RefreshNav();
        }

        static bool IsOverlay(GameObject screen) => screen == modes;

        static void Pop()
        {
            if (stack.Count == 0) return;
            stack[stack.Count - 1].SetActive(false);
            stack.RemoveAt(stack.Count - 1);
            if (stack.Count > 0)
            {
                var screen = stack[stack.Count - 1];
                screen.SetActive(true);
                OnShown(screen);
            }
            RefreshNav();
        }

        static void ClearStack()
        {
            for (int i = 0; i < stack.Count; i++) stack[i].SetActive(false);
            stack.Clear();
            RefreshNav();
        }

        static void OnShown(GameObject screen)
        {
            if (screen == mainMenu) { RefreshHome(); AskOnlineOnce(); }
            else if (screen == heroSelect) RefreshHeroSelect();
            else if (screen == heroPage) RefreshHeroPage();
            else if (screen == settings) RefreshSettings();
            else if (screen == modes) RefreshModes();
        }

        // The game layer normally hides/shows our screens itself after these calls; the extra calls here only make sure
        // the player is never left without a screen if it does not.
        static void GoToMenu()
        {
            if (Flow.Game != null && Flow.Game.InMatch)
            {
                HideAll();
                Flow.Game.QuitToMenu();
                if (!IsBlockingInput) ShowMainMenu();
            }
            else ShowMainMenu();
        }

        static void ResumeMatch()
        {
            Flow.Game?.Resume();
            HidePause();
        }

        static void RestartMatch()
        {
            if (Flow.Game == null) return;
            HideAll();
            Flow.Game.RestartMatch();
        }

        static void StartMatch(MatchSetup setup)
        {
            if (Flow.Game == null)
            {
                Debug.LogError("[BAM.UI] Flow.Game is not set; cannot start a match");
                return;
            }
            HideAll();
            Flow.Game.StartMatch(setup);
        }

        /// <summary>Per-frame work (from UiRoot): the hint bubble's bob, the update notice and the review ask. No allocations.</summary>
        internal static void Tick() { TickHint(); TickUpdateNotice(); TickReviewAsk(); }

        // ---- shared screen pieces ----

        /// <summary>A hidden full-screen page: black outside the standing frame and the gradient inside it for menus, a dim veil over the match, and a safe-area body.</summary>
        static RectTransform NewScreen(string name, bool overMatch, out GameObject go)
        {
            var rt = UiKit.NewRect(name, root);
            UiKit.Stretch(rt);
            go = rt.gameObject;
            if (overMatch) UiKit.Fill(rt, UiKit.Dim).raycastTarget = true;
            else UiKit.Fill(rt, Color.black).raycastTarget = true; // black outside the standing frame, like the old game
            if (!overMatch) UiKit.Background(UiKit.WideArea(rt)); // the gradient reaches the sides on a foldable or tablet
            var body = UiKit.SafeArea(rt);
            if (!overMatch) UiKit.Background(body);
            go.SetActive(false);
            return body;
        }

        static VerticalLayoutGroup VList(RectTransform rt, float spacing, RectOffset padding)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childAlignment = TextAnchor.UpperCenter;
            return v;
        }
    }
}
