using System;
using BAM.Core;
using UnityEngine;
#if BAM_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.Review;
#endif

namespace BAM.UI
{
    // Launch plan 2026-11-15 (Rickard: "starta nu hjälptrådar som gör alla dina fix"): Google Play's own review card (In-App
    // Review) after the player's THIRD win, never after a loss. A win is a finished match the player won against bots or online
    // (not the guided tutorial, not HERO TEST, not a quit). The card comes on Home right after the result, when nothing else is
    // open; a loss (or a quit) before the player is back on Home takes the ask away, and the next win brings it back. Asked once
    // per install: Google also limits how often its card may show and never says whether it did, so the game cannot ask again.
    //
    // Needs Google's In-App Review package (com.google.play.review 1.8.0 or newer): it defines BAM_PLAY_REVIEW (BAM.UI.asmdef
    // versionDefines). Without the package, in the Editor and on other platforms, only the win count is kept.
    public static partial class Screens
    {
        const string ReviewWinsKey = "bam.review.wins", ReviewAskedKey = "bam.review.asked";
        public const int ReviewAfterWins = 3;
        static bool reviewDue; // a win just made the ask due; shown on Home, taken away by a loss

        /// <summary>From GameFlow once per match as it ends (with its result): counts wins and makes the review ask due after the third.</summary>
        public static void NoteMatchForReview(MatchResult r)
        {
            if (r.Tutorial) return;
            if (!r.Won) { reviewDue = false; return; } // never after a loss
            if (PlayerPrefs.GetInt(ReviewAskedKey, 0) != 0) return;
            int wins = PlayerPrefs.GetInt(ReviewWinsKey, 0) + 1;
            PlayerPrefs.SetInt(ReviewWinsKey, wins);
            PlayerPrefs.Save();
            if (wins < ReviewAfterWins) return;
            reviewDue = true;
#if BAM_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
            PrepareReview(); // Google: ask Play for the card ahead, while the result shows, so it opens at once on Home
#endif
        }

        /// <summary>From Tick: Home on top, no dialog, not in a match, the update notice first. No allocations until then.</summary>
        static void TickReviewAsk()
        {
            if (!reviewDue) return;
            if (stack.Count == 0 || stack[stack.Count - 1] != mainMenu || dialog.Open) return;
            if (Flow.Game != null && Flow.Game.InMatch) return;
            if (updateLatest != 0 && !updateShown) return; // "New version!" goes first; the card next frame after it
            reviewDue = false;
#if BAM_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
            LaunchReview();
#else
            Debug.Log("[BAM.UI] Review ask due (win " + PlayerPrefs.GetInt(ReviewWinsKey, 0) + "); Google's card needs the Android build with com.google.play.review");
#endif
        }

#if BAM_PLAY_REVIEW && UNITY_ANDROID && !UNITY_EDITOR
        static ReviewManager playReviews;
        static PlayReviewInfo reviewInfo; // Play's ticket for one card, from PrepareReview
        static bool reviewPreparing, reviewLaunchWhenReady;

        static void PrepareReview()
        {
            if (reviewInfo != null || reviewPreparing) return;
            try
            {
                if (playReviews == null) playReviews = new ReviewManager();
                reviewPreparing = true;
                var op = playReviews.RequestReviewFlow();
                op.Completed += done =>
                {
                    reviewPreparing = false;
                    try
                    {
                        if (!done.IsSuccessful) { Debug.Log("[BAM.UI] Google Play review card not available (" + done.Error + ")"); reviewLaunchWhenReady = false; return; }
                        reviewInfo = done.GetResult();
                        if (!reviewLaunchWhenReady) return;
                        reviewLaunchWhenReady = false;
                        // the player may have started a match while Play answered: then Home asks again after it (if won)
                        if (Flow.Game != null && Flow.Game.InMatch) return;
                        LaunchReview();
                    }
                    catch (Exception e) { Debug.Log("[BAM.UI] Google Play review card failed: " + e.Message); }
                };
            }
            catch (Exception e)
            {
                reviewPreparing = false;
                Debug.Log("[BAM.UI] Google Play review card could not start: " + e.Message);
            }
        }

        static void LaunchReview()
        {
            if (reviewInfo == null)
            {
                // not ready yet (Play is slow, or the player tapped Home at once): open it as soon as Play answers
                reviewLaunchWhenReady = true;
                PrepareReview();
                return;
            }
            var info = reviewInfo;
            reviewInfo = null; // one card per ticket
            // asked: Google decides whether the card really shows and never says, so this install is not asked again
            PlayerPrefs.SetInt(ReviewAskedKey, 1);
            PlayerPrefs.Save();
            try
            {
                var op = playReviews.LaunchReviewFlow(info);
                op.Completed += done => Debug.Log("[BAM.UI] Google Play review card done: " + (done.IsSuccessful ? "ok" : done.Error.ToString()));
            }
            catch (Exception e) { Debug.Log("[BAM.UI] Google Play review card could not open: " + e.Message); }
        }
#endif
    }
}
