using System;
using System.Collections.Generic;
using BAM.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace BAM.Art
{
    // Kugg's Anchor form (Rickard 2026-11-05: "han blev som ett torn när han gjorde special", the old game's ROI17 swapping his body
    // for the R17 Guardian Turret): while the rules keep him rooted (Hero7.cs KuggRooted, the Anchor and its pack-up) the golem is
    // hidden and a turret stands on his spot instead, growing up out of the dust in FormGrow s and sinking back when he packs up. It
    // turns with the view, so its guns face what he shoots, and his shots leave its guns (FormMouth). Stand-in: the old game's own
    // turret (ABC2 fig_r17GuardianTurret) until Rickard's kugg_torn (O-072) is converted to the same file.
    sealed partial class BodyView
    {
        Transform formT;
        float formK, formSize;
        bool formOn, bodyOff;
        const float FormGrow = 0.3f, FormHeight = 1.15f; // the turret stands this many of his heights tall

        public override void SetForm(int form)
        {
            formOn = form == 1 && HeroIndex == (int)HeroId.Kugg;
            if (formOn && formT == null) BuildForm();
        }

        void BuildForm()
        {
            var mesh = FormModels.Mesh("kugg_turret");
            if (mesh == null) { formOn = false; return; } // the file is missing: he keeps his own body
            var go = new GameObject("Kugg turret");
            formT = go.transform;
            formT.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = FormModels.Mat("kugg_turret");
            r.shadowCastingMode = ArtKit.Shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            float s = transform.lossyScale.y;
            formSize = def.Height * FormHeight / Mathf.Max(0.01f, mesh.bounds.size.y * (s > 0f ? s : 1f));
            formT.localScale = Vector3.zero;
            go.SetActive(false);
        }

        // every frame after the rest of the look: the turret's growth and the golem hidden under it
        void FormLook(float dt)
        {
            if (formT == null) return;
            formK = Mathf.MoveTowards(formK, formOn && !dead ? 1f : 0f, dt / FormGrow);
            bool show = formK > 0f && renderersOn && !dead;
            if (formT.gameObject.activeSelf != show) formT.gameObject.SetActive(show);
            if (show) formT.localScale = Vector3.one * (formSize * BackOut(formK));
            bool hide = (formOn || formK > 0f) && !dead;
            if (hide) { for (int i = 0; i < rends.Length; i++) rends[i].enabled = false; bodyOff = true; }
            else if (bodyOff) { for (int i = 0; i < rends.Length; i++) rends[i].enabled = renderersOn; bodyOff = false; }
        }

        // a little overshoot as it pops up, like a spring
        static float BackOut(float k) { k -= 1f; return 1f + k * k * (2.7f * k + 1.7f); }

        /// <summary>The turret's gun mouths (between its two barrels, mesh units of the old game's turret) while it stands.</summary>
        bool FormMouth(out Vector3 at)
        {
            at = Vector3.zero;
            if (formT == null || !formT.gameObject.activeSelf || formK < 0.5f) return false;
            at = formT.TransformPoint(new Vector3(0f, 0.42f, 0.32f));
            return true;
        }

        void FormReset()
        {
            formOn = false; formK = 0f;
            if (formT != null) formT.gameObject.SetActive(false);
            if (bodyOff) { for (int i = 0; i < rends.Length; i++) rends[i].enabled = renderersOn; bodyOff = false; }
        }
    }

    /// <summary>Whole-body forms from Resources/BAMArt/Forms: &lt;name&gt;_mesh.bytes (the old game's packed mesh, AbcHeroModels.Decode in
    /// Speltest) and &lt;name&gt;_tex.jpg. Each is read once and shared by every view.</summary>
    static class FormModels
    {
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

        public static Mesh Mesh(string name)
        {
            if (meshes.TryGetValue(name, out var m)) return m;
            var asset = Resources.Load<TextAsset>("BAMArt/Forms/" + name + "_mesh");
            Mesh mesh = null;
            if (asset != null)
            {
                try { mesh = Decode(asset.bytes, name); }
                catch (Exception e) { Debug.LogWarning("BAM: could not read the form " + name + " (" + e.Message + ")"); }
            }
            return meshes[name] = mesh;
        }

        public static Material Mat(string name)
        {
            if (mats.TryGetValue(name, out var m) && m != null) return m;
            var tex = Resources.Load<Texture2D>("BAMArt/Forms/" + name + "_tex");
            m = ArtMaterials.Instance(ArtMaterials.Lit);
            m.name = name;
            m.SetTexture(ArtMaterials.BaseMap, tex);
            m.mainTexture = tex;
            m.SetColor(ArtMaterials.BaseColor, Color.white);
            return mats[name] = m;
        }

        // counts, the box the 16-bit positions span, a scale, then positions, signed-byte normals, 16-bit UVs and 16-bit indices;
        // the old game was right-handed (three.js): x mirrored and each triangle turned
        static Mesh Decode(byte[] d, string name)
        {
            int nv = BitConverter.ToInt32(d, 0), ni = BitConverter.ToInt32(d, 4);
            float[] lo = { F(d, 8), F(d, 12), F(d, 16) }, hi = { F(d, 20), F(d, 24), F(d, 28) };
            float h = F(d, 32);
            int o = 36;
            var verts = new Vector3[nv];
            for (int i = 0; i < nv; i++)
            {
                float x = lo[0] + U16(d, o + i * 6) / 65535f * (hi[0] - lo[0]);
                float y = lo[1] + U16(d, o + i * 6 + 2) / 65535f * (hi[1] - lo[1]);
                float z = lo[2] + U16(d, o + i * 6 + 4) / 65535f * (hi[2] - lo[2]);
                verts[i] = new Vector3(-x, y, z) * h;
            }
            o += nv * 6;
            var normals = new Vector3[nv];
            for (int i = 0; i < nv; i++) normals[i] = new Vector3(-(sbyte)d[o + i * 3], (sbyte)d[o + i * 3 + 1], (sbyte)d[o + i * 3 + 2]) / 127f;
            o += nv * 3;
            var uvs = new Vector2[nv];
            for (int i = 0; i < nv; i++) uvs[i] = new Vector2(U16(d, o + i * 4) / 65535f, U16(d, o + i * 4 + 2) / 65535f);
            o += nv * 4;
            var tris = new int[ni];
            for (int i = 0; i < ni; i += 3)
            {
                tris[i] = U16(d, o + i * 2);
                tris[i + 1] = U16(d, o + (i + 2) * 2);
                tris[i + 2] = U16(d, o + (i + 1) * 2);
            }
            var mesh = new Mesh { name = name };
            mesh.vertices = verts; mesh.normals = normals; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        static float F(byte[] d, int at) => BitConverter.ToSingle(d, at);
        static int U16(byte[] d, int at) => d[at] | (d[at + 1] << 8);
    }
}
