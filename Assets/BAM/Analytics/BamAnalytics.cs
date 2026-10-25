using BAM.Core;

namespace BAM.Analytics
{
    // CONTRACT between BAM.Analytics (owner: the Play thread) and BAM.Game. Docs/CONTRACTS.md explains it.
    // Nothing is collected or sent unless BamSettings.AnalyticsConsent == Granted. Every call is cheap and safe without consent.
    public static class BamAnalytics
    {
        /// <summary>Once at boot. Starts the analytics SDK only if consent is Granted, and follows BamSettings.Changed after that.</summary>
        public static void Init() { }

        public static void MatchStarted(MatchSetup setup) { }
        public static void MatchEnded(MatchSetup setup, MatchResult result) { }
        public static void TutorialStep(int step, string name) { }
    }
}
