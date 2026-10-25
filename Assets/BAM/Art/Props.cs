using System.Collections.Generic;
using UnityEngine;

namespace BAM.Art
{
    /// <summary>
    /// Rickard's Meshy models from the old game (Resources/BAMArt/Props/&lt;name&gt;.bytes + &lt;name&gt;.jpg, copied as they were):
    /// the tower, the spectral wolf and props. The .bytes file is the old game's own mesh format (tools/meshy/convert.py):
    /// vertex count, submesh count, flags (1 = normals, 2 = uvs), positions, normals, uvs, then 16-bit indices per submesh.
    /// Each model stands with its bottom at y = 0 and its middle at x = z = 0. Read once, then cached.
    /// </summary>
    static class Props
    {
        const string Folder = "BAMArt/Props/";
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// <summary>The model's mesh, or null when the file is missing or broken (the caller keeps its code model).</summary>
        public static Mesh Mesh(string name)
        {
            if (meshes.TryGetValue(name, out Mesh m)) return m;
            m = null;
            var asset = Resources.Load<TextAsset>(Folder + name);
            if (asset != null)
            {
                try { m = Decode(name, asset.bytes); }
                catch (System.Exception e) { Debug.LogWarning("[BAM.Art] Could not read the model " + name + ": " + e.Message); }
                Resources.UnloadAsset(asset);
            }
            meshes[name] = m;
            return m;
        }

        public static Texture2D Texture(string name) => Resources.Load<Texture2D>(Folder + name);

        static Mesh Decode(string name, byte[] d)
        {
            int nv = System.BitConverter.ToInt32(d, 0), ns = System.BitConverter.ToInt32(d, 4), flags = System.BitConverter.ToInt32(d, 8);
            int o = 12;
            var v = new Vector3[nv];
            for (int i = 0; i < nv; i++, o += 12) v[i] = new Vector3(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4), System.BitConverter.ToSingle(d, o + 8));
            var mesh = new Mesh { name = name };
            mesh.vertices = v;
            if ((flags & 1) != 0)
            {
                var n = new Vector3[nv];
                for (int i = 0; i < nv; i++, o += 12) n[i] = new Vector3(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4), System.BitConverter.ToSingle(d, o + 8));
                mesh.normals = n;
            }
            if ((flags & 2) != 0)
            {
                var uv = new Vector2[nv];
                for (int i = 0; i < nv; i++, o += 8) uv[i] = new Vector2(System.BitConverter.ToSingle(d, o), System.BitConverter.ToSingle(d, o + 4));
                mesh.uv = uv;
            }
            // one submesh is enough for one texture: join them
            var all = new List<int>();
            for (int s = 0; s < ns; s++)
            {
                int count = System.BitConverter.ToInt32(d, o); o += 4;
                for (int i = 0; i < count; i++, o += 2) all.Add(d[o] | (d[o + 1] << 8));
            }
            mesh.SetTriangles(all, 0);
            if ((flags & 1) == 0) mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }
    }
}
