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
        /// <summary>A run on all fours, played instead of the run while the hero's speed passive is on (Ragn's Wolf Run); null = none.</summary>
        public AnimationClip RunFour;
        /// <summary>Standing height in units the model is scaled to.</summary>
        public float Height = 1f;
        /// <summary>Where in the attack clip (0..1) the blow lands or the shot leaves; PlayAttack's windup is lined up with it.</summary>
        public float AttackRelease = 0.45f;
        /// <summary>Where in the attack clip (0..1) the swing starts; the frames before it are skipped (a clip whose wind-up first
        /// dips away from the resting pose).</summary>
        public float AttackStart;
        /// <summary>The body is the 2026-11-13 remake (Art/ModelsV2/&lt;Hero&gt;): its own weapon and part table (Weapons.ForV2) and cloth
        /// (ClothSway). BAM &gt; Art &gt; Models V2 switches each hero between the remake and the old body.</summary>
        public bool V2;
        /// <summary>The V2 body's other looks (Art/ModelsV2/&lt;Hero&gt;/Skins/&lt;Skin&gt;: its own body and colour picture, same clips and
        /// weapons), in folder-name order; index i here is skin i + 1 (0 = the plain V2 body above). Built by HeroArtImport, chosen
        /// with <see cref="HeroSkins"/> (Rickard 2026-11-14: "hur byter jag skins nu").</summary>
        public string[] SkinNames = new string[0];
        public GameObject[] SkinModels = new GameObject[0];
        public Texture2D[] SkinTextures = new Texture2D[0];
        /// <summary>Each skin's main colour (from its picture), for its name in the skin list.</summary>
        public Color[] SkinColors = new Color[0];
        public int SkinCount => SkinModels != null ? SkinModels.Length : 0;
    }
}
