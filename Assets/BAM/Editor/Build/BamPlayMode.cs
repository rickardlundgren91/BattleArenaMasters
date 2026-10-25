using UnityEditor;
using UnityEditor.SceneManagement;

namespace BAM.Build
{
    /// <summary>
    /// Play always starts the game from its own scene (Scenes/Main.unity), whatever scene is open in the editor, so Play after
    /// BAM > Art Preview starts the game and not an empty tool scene. Art Preview clears this for its own run; it is set again
    /// when Play stops.
    /// </summary>
    [InitializeOnLoad]
    static class BamPlayMode
    {
        static BamPlayMode()
        {
            // not while Play is starting: that would turn a tool's own Play run into the game
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Apply();
            EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredEditMode) Apply(); };
        }

        static void Apply() => EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(BamProjectSetup.MainScene);
    }
}
