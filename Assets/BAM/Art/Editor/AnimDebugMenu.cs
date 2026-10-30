using UnityEditor;

namespace BAM.Art.Editor
{
    /// <summary>BAM > Art > Animation Debug Overlay: Animation Foundation 1.0's state and swing-timing text in Play (AnimDebugOverlay).</summary>
    static class AnimDebugMenu
    {
        const string Item = "BAM/Art/Animation Debug Overlay";

        [MenuItem(Item)]
        static void Toggle() => AnimDebugOverlay.On = !AnimDebugOverlay.On;

        [MenuItem(Item, true)]
        static bool Check()
        {
            Menu.SetChecked(Item, AnimDebugOverlay.On);
            return true;
        }
    }
}
