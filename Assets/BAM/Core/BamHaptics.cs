using UnityEngine;

namespace BAM.Core
{
    /// <summary>
    /// Short vibration pulses on Android (the old game's Haptics.cs): a VibrationEffect with an amplitude, 12 to 45 ms, nothing
    /// when Settings has Vibration off, in the editor or on other platforms. Handheld.Vibrate is about 400 ms, too long for a
    /// button or a hit. Main thread only; at most one pulse every 0.08 s, so a team fight does not buzz on and on.
    /// </summary>
    public static class BamHaptics
    {
        static float lastAt = -1f;

        /// <summary>Settings > Vibrate on my hits (BamSettings.HitVibration): one soft pulse, about 17 ms, per own auto that hits.</summary>
        public const float LightHit = 0.15f;

        /// <summary>Strength 0..1 (the old scale): 0.12 a button press, 0.3 a hit on the player, 0.6 its death, 0.8 a kill.</summary>
        public static void Pulse(float strength)
        {
            if (strength <= 0f || !BamSettings.Vibration) return;
            float now = Time.unscaledTime;
            if (lastAt >= 0f && now - lastAt < 0.08f) return;
            lastAt = now;
            strength = Mathf.Clamp01(strength);
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!tried) Init();
            if (vib == null) return;
            long ms = (long)Mathf.Lerp(12f, 45f, strength);
            int amp = Mathf.Clamp((int)Mathf.Lerp(60f, 255f, strength), 1, 255);
            try
            {
                using (var fx = effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amp)) vib.Call("vibrate", fx);
            }
            catch (System.Exception) { vib = null; }
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject vib;
        static AndroidJavaClass effect;
        static bool tried;

        // min API 26: VibrationEffect is always there
        static void Init()
        {
            tried = true;
            try
            {
                using (var up = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var act = up.GetStatic<AndroidJavaObject>("currentActivity"))
                    vib = act.Call<AndroidJavaObject>("getSystemService", "vibrator");
                effect = new AndroidJavaClass("android.os.VibrationEffect");
            }
            catch (System.Exception e) { Debug.LogWarning("[BAM.Core] vibration unavailable (" + e.Message + ")"); vib = null; }
        }

        // never called: a reference to Handheld.Vibrate makes Unity add the VIBRATE permission to the Android manifest
        static void KeepVibratePermission() => Handheld.Vibrate();
#endif
    }
}
