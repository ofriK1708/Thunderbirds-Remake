using UnityEngine;

namespace Thunderbirds.Unity
{
    public static class DisplaySettings
    {
        public const string FullscreenKey = "Thunderbirds.Fullscreen.v1";
        public static bool Fullscreen => PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) != 0;
        public static void SetFullscreen(bool enabled)
        {
            PlayerPrefs.SetInt(FullscreenKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            Apply(enabled);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Restore()
        {
            if (PlayerPrefs.HasKey(FullscreenKey)) Apply(Fullscreen);
        }
        private static void Apply(bool enabled)
        {
#if !UNITY_EDITOR
            Screen.fullScreenMode = enabled ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
#endif
        }
    }
}
