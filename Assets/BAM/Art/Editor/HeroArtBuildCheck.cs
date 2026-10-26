using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Before every player build (the AABs for the testers): import any hero model or clip that kept Unity's defaults again and
    /// rebuild the hero list, so no hero ships without its clips and stands in a T-pose (a tester's report, 2026-10-26).
    /// The editor's own rebuild runs a frame later on import, which a build started straight after a sync can beat.
    /// </summary>
    sealed class HeroArtBuildCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            HeroArtImport.ReimportStaleForBuild();
            HeroArtImport.BuildAll();
            Debug.Log("[BAM.Art] Hero models checked before the build");
        }
    }
}
