using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// The player's own volume choices from the Options screen, saved in PlayerPrefs like the fullscreen
    /// setting. They scale the designer's volumes in AudioConfig; they do not replace them.
    /// </summary>
    public static class SoundSettings
    {
        public const string MusicKey = "Thunderbirds.MusicVolume.v1";
        public const string EffectsKey = "Thunderbirds.EffectsVolume.v1";

        /// <summary>What the sliders show before the player has touched them.</summary>
        public const float DefaultVolume = 0.75f;

        private static float? _music, _effects;

        /// <summary>0..1. Scales the music.</summary>
        public static float Music => _music ?? (_music = PlayerPrefs.GetFloat(MusicKey, DefaultVolume)).Value;

        /// <summary>0..1. Scales every effect, the ship engines and the overload alarm.</summary>
        public static float Effects => _effects ?? (_effects = PlayerPrefs.GetFloat(EffectsKey, DefaultVolume)).Value;

        public static void SetMusic(float volume)
        {
            _music = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MusicKey, _music.Value);
            PlayerPrefs.Save();
        }

        public static void SetEffects(float volume)
        {
            _effects = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(EffectsKey, _effects.Value);
            PlayerPrefs.Save();
        }

        /// <summary>Forget the cached values and read PlayerPrefs again (entering Play mode, and tests).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload()
        {
            _music = null;
            _effects = null;
        }
    }
}
