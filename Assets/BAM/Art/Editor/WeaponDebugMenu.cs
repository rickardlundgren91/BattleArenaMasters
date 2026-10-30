using UnityEditor;

namespace BAM.Art.Editor
{
    /// <summary>BAM > Art > Weapon Debug View: Weapon Precision 2.0's grip points, weapon axes, muzzles and shot lines in Play
    /// (WeaponDebug), and Reset Weapon Numbers to measure one move from the start.</summary>
    static class WeaponDebugMenu
    {
        const string Item = "BAM/Art/Weapon Debug View";

        [MenuItem(Item)]
        static void Toggle() => WeaponDebug.On = !WeaponDebug.On;

        [MenuItem(Item, true)]
        static bool Check()
        {
            Menu.SetChecked(Item, WeaponDebug.On);
            return true;
        }

        [MenuItem("BAM/Art/Reset Weapon Numbers")]
        static void Reset() => WeaponDebug.ResetStats();
    }
}
