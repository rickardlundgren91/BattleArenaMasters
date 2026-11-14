using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BAM.Art.Editor
{
    /// <summary>
    /// Turns the skins' parts (ModelsV2/&lt;Hero&gt;/Skins/&lt;Skin&gt;/Parts/*.obj, and *.obj beside the skin's body) into Props meshes
    /// the game can read: Resources/BAMArt/Props/v2s_&lt;hero&gt;_&lt;skin&gt;_&lt;slot&gt;.bytes + .jpg (SkinGear). The same conversion the V2
    /// parts had (bam-work/remake-v2-wire): x mirrored into Unity's axes, triangles turned round, the middle at x = z = 0, the
    /// bottom at y = 0, or the top for a part that hangs (a cape, mantle, pelt, scarf or apron flap). A part is converted again
    /// only when its OBJ or picture is newer than the converted file. Runs with every hero rebuild (HeroArtImport.BuildAll).
    /// </summary>
    static class SkinParts
    {
        const string PropsDir = "Assets/BAM/Art/Resources/BAMArt/Props/";
        static readonly HashSet<string> hangs = new HashSet<string> { "cape", "mantle", "pelt", "scarf", "apronflap" };

        /// <summary>True when a part of the skin is not converted yet, or changed since (HeroArtImport.EnsureBuilt).</summary>
        public static bool Pending(string hero, string skinDir) => Convert(hero, skinDir, true) > 0;

        public static int Convert(string hero, string skinDir, bool onlyCount = false)
        {
            string skin = Path.GetFileName(skinDir);
            var objs = new List<string>(Directory.GetFiles(skinDir, "*.obj"));
            if (Directory.Exists(skinDir + "/Parts")) objs.AddRange(Directory.GetFiles(skinDir + "/Parts", "*.obj"));
            int n = 0;
            foreach (string o in objs)
            {
                string obj = o.Replace('\\', '/');
                string baseName = Path.GetFileNameWithoutExtension(obj);
                int us = baseName.LastIndexOf('_');
                if (us < 0) continue;
                string slot = baseName.Substring(us + 1).ToLowerInvariant();
                string name = ("v2s_" + hero + "_" + skin + "_" + slot).ToLowerInvariant();
                string bytes = PropsDir + name + ".bytes", jpg = PropsDir + name + ".jpg";
                string tex = Path.GetDirectoryName(obj).Replace('\\', '/') + "/" + baseName + "_Tex.jpg";
                bool fresh = File.Exists(bytes) && File.GetLastWriteTimeUtc(bytes) >= File.GetLastWriteTimeUtc(obj)
                    && (!File.Exists(tex) || (File.Exists(jpg) && File.GetLastWriteTimeUtc(jpg) >= File.GetLastWriteTimeUtc(tex)));
                if (fresh) continue;
                if (onlyCount) { n++; continue; }
                try
                {
                    byte[] data = Prop(obj, hangs.Contains(slot), out int verts);
                    if (data == null) { Debug.LogWarning("[BAM.Art] " + obj + ": " + verts + " corners, more than a Props mesh can hold (65535)"); continue; }
                    File.WriteAllBytes(bytes, data);
                    if (File.Exists(tex)) File.Copy(tex, jpg, true);
                    AssetDatabase.ImportAsset(bytes);
                    if (File.Exists(jpg)) AssetDatabase.ImportAsset(jpg);
                    n++;
                    Debug.Log("[BAM.Art] Skin part " + name + " (" + verts + " corners) from " + obj);
                }
                catch (System.Exception e) { Debug.LogWarning("[BAM.Art] Could not convert " + obj + ": " + e.Message); }
            }
            return n;
        }

        static float F(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        // OBJ -> the Props .bytes format (Props.cs): count, 1 submesh, flags 3, positions, normals, uvs, 16-bit indices
        static byte[] Prop(string path, bool top, out int count)
        {
            var V = new List<Vector3>(); var VT = new List<Vector2>(); var VN = new List<Vector3>();
            var pos = new List<Vector3>(); var nor = new List<Vector3>(); var uv = new List<Vector2>();
            var tris = new List<int>();
            var index = new Dictionary<(int, int, int), int>();
            var face = new List<int>();
            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length < 2) continue;
                string[] t = line.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                switch (t[0])
                {
                    case "v": V.Add(new Vector3(F(t[1]), F(t[2]), F(t[3]))); break;
                    case "vt": VT.Add(new Vector2(F(t[1]), F(t[2]))); break;
                    case "vn": VN.Add(new Vector3(F(t[1]), F(t[2]), F(t[3]))); break;
                    case "f":
                        face.Clear();
                        for (int i = 1; i < t.Length; i++)
                        {
                            string[] c = t[i].Split('/');
                            int vi = Idx(c[0], V.Count), ti = c.Length > 1 && c[1].Length > 0 ? Idx(c[1], VT.Count) : -1, ni = c.Length > 2 && c[2].Length > 0 ? Idx(c[2], VN.Count) : -1;
                            var key = (vi, ti, ni);
                            if (!index.TryGetValue(key, out int k))
                            {
                                k = pos.Count;
                                index[key] = k;
                                Vector3 p = V[vi];
                                pos.Add(new Vector3(-p.x, p.y, p.z));
                                Vector3 nn = ni >= 0 ? VN[ni] : Vector3.zero;
                                nor.Add(new Vector3(-nn.x, nn.y, nn.z));
                                uv.Add(ti >= 0 ? VT[ti] : Vector2.zero);
                            }
                            face.Add(k);
                        }
                        for (int i = 1; i + 1 < face.Count; i++) { tris.Add(face[0]); tris.Add(face[i + 1]); tris.Add(face[i]); } // turned round with the mirror
                        break;
                }
            }
            count = pos.Count;
            if (count == 0 || count > 65535) return null;
            Vector3 lo = pos[0], hi = pos[0];
            foreach (Vector3 p in pos) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
            var shift = new Vector3(-(lo.x + hi.x) / 2f, top ? -hi.y : -lo.y, -(lo.z + hi.z) / 2f);
            bool normals = VN.Count > 0;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(count); w.Write(1); w.Write(normals ? 3 : 2);
                foreach (Vector3 p in pos) { Vector3 q = p + shift; w.Write(q.x); w.Write(q.y); w.Write(q.z); }
                if (normals) foreach (Vector3 nn in nor) { w.Write(nn.x); w.Write(nn.y); w.Write(nn.z); }
                foreach (Vector2 u in uv) { w.Write(u.x); w.Write(u.y); }
                w.Write(tris.Count);
                foreach (int i in tris) w.Write((ushort)i);
                w.Flush();
                return ms.ToArray();
            }
        }

        static int Idx(string s, int n) { int i = int.Parse(s, CultureInfo.InvariantCulture); return i < 0 ? n + i : i - 1; }
    }
}
