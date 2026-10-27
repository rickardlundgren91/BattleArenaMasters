using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    /// <summary>
    /// Blast's fire as the old game drew it (Speltest View3D.cs burn, View3DStudioBlast.cs Inferno Lane): flame tongues that stay put
    /// and flicker every frame, bright at the root and fading at the tip, with a soft glow under them. Particles alone (the BAM first
    /// version) only gave loose blobs that rise and vanish. The tongue is the flame cell of the effect atlas, the glow is Rickard's
    /// tx_fx_glow_soft. Each piece is one quad with a shared additive material; its colour goes in a property block, so nothing is
    /// made per frame. Ready is false without the atlas: callers then keep their particles.
    /// </summary>
    static class FireLook
    {
        // the old game's colours: a burning target (View3D.cs) and Blast's forge lane (View3DStudioBlast.cs)
        internal static readonly Color BurnOuter = new Color(1f, 0.38f, 0.06f), BurnCore = new Color(1f, 0.85f, 0.4f), BurnGlow = new Color(1f, 0.42f, 0.1f),
            Forge = new Color(1f, 0.455f, 0.243f), ForgeHot = new Color(1f, 0.85f, 0.45f), ForgeFoe = new Color(1f, 0.3f, 0.26f);

        static bool tried;
        static Material flameMat, glowMat;
        static Mesh flameMesh, glowMesh;
        static MaterialPropertyBlock mpb;

        internal static bool Ready
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    if (FxAtlas.Texture != null)
                    {
                        flameMat = FxAtlas.Material(ArtMaterials.UnlitAdd);
                        flameMat.name = "BAM fire tongues";
                        flameMat.renderQueue = 3040; // over the zones and rings, under the hit flashes
                        flameMesh = FxAtlas.Quad(FxAtlas.Flame);
                        var g = RickardFx.Load("tx_fx_glow_soft");
                        if (g != null)
                        {
                            glowMat = ArtMaterials.Instance(ArtMaterials.UnlitAdd);
                            glowMat.name = "BAM fire glow";
                            glowMat.SetTexture(ArtMaterials.BaseMap, g);
                            glowMat.mainTexture = g;
                            glowMat.renderQueue = 3020;
                            glowMesh = FxAtlas.FullQuad();
                        }
                        else { glowMat = flameMat; glowMesh = FxAtlas.Quad(FxAtlas.Glow); }
                        mpb = new MaterialPropertyBlock();
                    }
                }
                return flameMat != null;
            }
        }

        /// <summary>One piece under parent: a flame tongue, or a soft glow.</summary>
        internal static MeshRenderer Piece(Transform parent, bool glow)
        {
            var go = new GameObject(glow ? "Fire glow" : "Fire tongue");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = glow ? glowMesh : flameMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = glow ? glowMat : flameMat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        /// <summary>A standing flame: its root at foot (world), len tall and w wide as the old comet sprite, leaning by lean radians.
        /// The flame cell is narrower than its quad and its root sits low in it, hence the factors.</summary>
        internal static void Tongue(MeshRenderer r, Vector3 foot, float len, float w, float lean, Color c, float a, float inv)
        {
            var t = r.transform;
            t.SetPositionAndRotation(foot + new Vector3(0f, len * 0.36f, 0f), ArtDriver.CamRot * Quaternion.Euler(0f, 0f, lean * Mathf.Rad2Deg) * Quaternion.Euler(-90f, 0f, 0f));
            t.localScale = new Vector3(w * 2.1f * inv, 1f, len * 1.2f * inv);
            Tint(r, c, a);
        }

        /// <summary>A soft glow facing the camera at center (world), size wide.</summary>
        internal static void Glow(MeshRenderer r, Vector3 center, float size, Color c, float a, float inv)
        {
            var t = r.transform;
            t.SetPositionAndRotation(center, ArtDriver.CamRot * Quaternion.Euler(-90f, 0f, 0f));
            t.localScale = new Vector3(size * inv, 1f, size * inv);
            Tint(r, c, a);
        }

        /// <summary>A glow lying on the ground at center (world): w across, len along dir.</summary>
        internal static void Floor(MeshRenderer r, Vector3 center, Vector3 dir, float w, float len, Color c, float a, float inv)
        {
            var t = r.transform;
            t.SetPositionAndRotation(center, Quaternion.LookRotation(dir));
            t.localScale = new Vector3(w * inv, 1f, len * inv);
            Tint(r, c, a);
        }

        // additive: the colour's brightness is its strength (the flame cell's own alpha gives the shape)
        static void Tint(MeshRenderer r, Color c, float a)
        {
            a = Mathf.Clamp01(a);
            mpb.SetColor(ArtMaterials.BaseColor, new Color(c.r * a, c.g * a, c.b * a, 1f));
            r.SetPropertyBlock(mpb);
        }
    }
}
