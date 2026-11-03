using UnityEditor;

namespace BAM.Build
{
    /// <summary>
    /// BAM > Test > Immortal in matches (Rickard 2026-11-03 "kunna vara odödlig i match för att kunna testa"): a tick in the menu
    /// keeps the player at full health in matches against bots in the editor (GameFlow.EditorImmortal), so things like the tower
    /// ring can be tried without dying. It can be turned on and off during Play. Editor only: phone builds and online matches never
    /// use it. The key must match GameFlow.ImmortalKey (this assembly does not reference BAM.Game).
    /// </summary>
    static class BamImmortal
    {
        const string Key = "BAM.ImmortalInMatch", Menu = "BAM/Test/Immortal in matches (editor only)";

        [MenuItem(Menu, priority = 70)]
        static void Toggle()
        {
            bool on = !EditorPrefs.GetBool(Key, false);
            EditorPrefs.SetBool(Key, on);
            UnityEditor.Menu.SetChecked(Menu, on);
        }

        [MenuItem(Menu, true)]
        static bool ToggleValidate()
        {
            UnityEditor.Menu.SetChecked(Menu, EditorPrefs.GetBool(Key, false));
            return true;
        }
    }
}
