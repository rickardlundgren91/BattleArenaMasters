using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// One hero's imported model and the clips it plays (Rickard's own models rigged on mixamo.com, Mixamo clips).
    /// Made by BAM.Art.Editor.HeroArtBuilder into Resources/BAMArt/Heroes/&lt;Hero&gt;.asset. Only what this asset
    /// references goes into the build, so the FBX files themselves stay outside Resources.
    /// When an asset is missing the hero falls back to its model built in code.
    /// </summary>
    public sealed class HeroArt : ScriptableObject
    {
        public GameObject Model;
        /// <summary>The colour picture (taken out of the FBX); null = whatever the FBX material has.</summary>
        public Texture2D Texture;
        public AnimationClip Idle, Run, Attack, Cast, Hit, Death;
        /// <summary>The slow stride, blended into the run by the speed over the ground (old Speltest's HeroMixamoGait).</summary>
        public AnimationClip Walk;
        /// <summary>Standing height in units the model is scaled to.</summary>
        public float Height = 1f;
        /// <summary>Where in the attack clip (0..1) the blow lands or the shot leaves; PlayAttack's windup is lined up with it.</summary>
        public float AttackRelease = 0.45f;
    }
}
