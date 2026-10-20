using UnityEngine;

namespace Kitchen.UI
{
    public static class PaperUISettings
    {
        public const string VolumeKey = "kitchen.masterVolume";
        public const string MotionKey = "kitchen.uiMotion";
        public static bool MotionEnabled => PlayerPrefs.GetInt(MotionKey, 1) != 0;
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 0.8f));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize() { AudioListener.volume = Volume; }

        public static void SetVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            PlayerPrefs.Save();
        }

        public static void SetMotion(bool enabled)
        {
            PlayerPrefs.SetInt(MotionKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
