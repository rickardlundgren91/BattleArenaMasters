namespace BAM.Core
{
    public static class BamInfo
    {
        public const string AppName = "Battle Arena Masters";
        public const string ShortName = "BAM";
        public const string PackageName = "com.battlearenamasters.game"; // permanent once uploaded to Google Play

        /// <summary>The privacy policy page (the Play thread hosts it; Google Play links to the same address).</summary>
        public const string PrivacyPolicyUrl = "https://battlearenamasters.com/privacy";

        /// <summary>The testers' Discord invite, behind "Report a bug" in Settings; empty hides the row (waiting for Rickard's link, 2026-10-31).</summary>
        public const string DiscordUrl = "";

        /// <summary>Path under Resources of the licenses text the Settings screen shows.</summary>
        public const string LicensesResource = "BAM/Licenses";
    }
}
