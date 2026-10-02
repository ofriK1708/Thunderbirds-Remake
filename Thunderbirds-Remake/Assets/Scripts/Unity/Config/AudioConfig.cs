using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Every sound in the game and how loud it is. One asset, in Resources so the AudioManager can load it in
    /// either scene without a scene reference. Empty slots are simply silent, so clips can be swapped freely.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Thunderbirds/Audio Config")]
    public sealed class AudioConfig : ScriptableObject
    {
        /// <summary>Resources path the AudioManager loads.</summary>
        public const string ResourcePath = "AudioConfig";

        [Header("Volume")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.45f;
        [Range(0f, 1f)] public float effectsVolume = 0.8f;
        [Tooltip("The two ship engines hum the whole time, so they sit well below the effects.")]
        [Range(0f, 1f)] public float engineVolume = 0.3f;

        [Header("Music")]
        [Tooltip("Looped in the menu and in every level.")]
        public AudioClip music;

        [Header("Ship engines (seamless loops)")]
        [Tooltip("Light and quick.")]
        public AudioClip kestrelEngine;
        [Tooltip("Heavy and low.")]
        public AudioClip atlasEngine;
        [Tooltip("How loud an engine is while its ship hovers in place, as a fraction of its flying volume.")]
        [Range(0f, 1f)] public float idleEngineLevel = 0.25f;
        [Tooltip("How quickly an engine swells and settles.")]
        [Min(0.01f)] public float engineFadeSeconds = 0.18f;

        [Header("Effects")]
        [Tooltip("A refused move: the ship bumps into a block, a wall or the other ship.")]
        public AudioClip bump;
        [Tooltip("A ship settles on its dock.")]
        public AudioClip dock;
        public AudioClip switchShip;
        [Tooltip("Seamless loop, played for as long as a ship carries more than it can.")]
        public AudioClip overload;
        [Tooltip("A ship is crushed and a life is lost.")]
        public AudioClip crush;
        public AudioClip levelComplete;
        public AudioClip gameOver;
    }
}
