using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace BAM.Build
{
    /// <summary>
    /// Keeps the advertising ID permission out of the final Android manifest. Some Google and Unity libraries declare
    /// it; the Data safety answers say BAM never reads the advertising ID, so the shipped app must not ask for it.
    /// The removal marker goes in the launcher (app) manifest, which outranks every library manifest when Gradle merges.
    /// </summary>
    sealed class BamGradleGuard : IPostGenerateGradleAndroidProject
    {
        const string AndroidNs = "http://schemas.android.com/apk/res/android";
        const string ToolsNs = "http://schemas.android.com/tools";
        const string AdId = "com.google.android.gms.permission.AD_ID";

        // Late, so it sees what other packages' build steps added.
        public int callbackOrder => 1000;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            string launcher = Path.Combine(unityLibraryPath, "..", "launcher", "src", "main", "AndroidManifest.xml");
            string library = Path.Combine(unityLibraryPath, "src", "main", "AndroidManifest.xml");
            string target = File.Exists(launcher) ? launcher : library;
            if (!File.Exists(target))
            {
                Debug.LogError("[BAM.Build] No AndroidManifest.xml found in the Gradle project; AD_ID could not be blocked. Do not upload this build.");
                return;
            }

            try
            {
                var doc = new XmlDocument { PreserveWhitespace = true };
                doc.Load(target);
                var manifest = doc.DocumentElement;
                if (manifest == null || manifest.Name != "manifest") throw new InvalidOperationException("root is not <manifest>");

                if (manifest.GetAttribute("xmlns:tools") != ToolsNs) manifest.SetAttribute("xmlns:tools", ToolsNs);

                XmlElement entry = null;
                foreach (XmlNode n in manifest.ChildNodes)
                    if (n is XmlElement e && e.Name == "uses-permission" && e.GetAttribute("name", AndroidNs) == AdId) { entry = e; break; }
                if (entry == null)
                {
                    entry = doc.CreateElement("uses-permission");
                    entry.SetAttribute("name", AndroidNs, AdId);
                    manifest.PrependChild(entry);
                }
                entry.SetAttribute("node", ToolsNs, "remove");

                doc.Save(target);
                Debug.Log("[BAM.Build] AD_ID permission blocked (tools:node=\"remove\") in " + Path.GetFullPath(target));
            }
            catch (Exception e)
            {
                Debug.LogError("[BAM.Build] Could not block AD_ID in " + target + ": " + e.Message + ". Do not upload this build.");
            }
        }
    }
}
