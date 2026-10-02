using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Plays the music and every sound effect (GDD §7, course feature 2: the second and last singleton).
    /// It outlives scene loads so the music does not restart between the menu and a level. Created on first
    /// use and at game start, so no scene contains it; clips and volumes come from the AudioConfig asset.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour, IGameAudio
    {
        private static AudioManager _instance;

        private AudioConfig _config;
        private AudioSource _music, _effects, _overload;
        private readonly Dictionary<ShipId, AudioSource> _engines = new Dictionary<ShipId, AudioSource>();
        private readonly Dictionary<ShipId, float> _engineTargets = new Dictionary<ShipId, float>();

        public static AudioManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject(nameof(AudioManager));
                _instance = host.AddComponent<AudioManager>();
                if (Application.isPlaying) DontDestroyOnLoad(host);
                else host.hideFlags = HideFlags.HideAndDontSave;
                _instance.Initialise(Resources.Load<AudioConfig>(AudioConfig.ResourcePath));
                return _instance;
            }
        }

        /// <summary>Start the music as soon as the first scene is up, whichever scene that is.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWithTheGame() => Instance.PlayMusic();

        /// <summary>Drops the instance on entering Play mode (domain reload may be off) and between tests.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetInstance()
        {
            if (_instance == null) return;
            var host = _instance.gameObject;
            _instance = null;
            if (Application.isPlaying) Destroy(host);
            else DestroyImmediate(host);
        }

        private void Initialise(AudioConfig config)
        {
            _config = config;
            _music = NewSource("Music", loop: true);
            _effects = NewSource("Effects", loop: false);
            // PlayOneShot multiplies its volume argument by the source's own volume, so this one must be 1.
            _effects.volume = 1f;
            _overload = NewSource("Overload", loop: true);
            _engines[ShipId.Kestrel] = NewSource("Kestrel engine", loop: true);
            _engines[ShipId.Atlas] = NewSource("Atlas engine", loop: true);
            if (_config == null)
            {
                Debug.LogWarning($"No AudioConfig at Resources/{AudioConfig.ResourcePath}: the game is silent.");
                return;
            }
            _music.clip = _config.music;
            _overload.clip = _config.overload;
            _engines[ShipId.Kestrel].clip = _config.kestrelEngine;
            _engines[ShipId.Atlas].clip = _config.atlasEngine;
        }

        private AudioSource NewSource(string sourceName, bool loop)
        {
            var source = new GameObject(sourceName).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D: a single-screen game has no left and right to place sounds in
            source.volume = 0f; // loops are faded or set when they start
            return source;
        }

        /// <summary>Hover volume of an engine as a fraction of its flying volume.</summary>
        public float IdleEngineLevel => _config != null ? _config.idleEngineLevel : 0.25f;

        public void PlayMusic()
        {
            if (_config == null || _music.clip == null || _music.isPlaying) return;
            _music.volume = MusicVolume;
            _music.Play();
        }

        /// <summary>Designer volume (AudioConfig) times the player's Options slider.</summary>
        public float MusicVolume => _config == null ? 0f : _config.masterVolume * _config.musicVolume * SoundSettings.Music;

        public float EffectsVolume => _config == null ? 0f : _config.masterVolume * _config.effectsVolume * SoundSettings.Effects;

        /// <summary>Engines hum the whole time, so they have their own, lower designer volume.</summary>
        public float EngineVolume => _config == null ? 0f : _config.masterVolume * _config.engineVolume * SoundSettings.Effects;

        public void Play(GameSound sound)
        {
            var clip = ClipFor(sound);
            if (clip != null && _effects != null) _effects.PlayOneShot(clip, EffectsVolume);
        }

        public void SetEngineLevel(ShipId ship, float level) => _engineTargets[ship] = Mathf.Clamp01(level);

        public void SetOverloadAlarm(bool on)
        {
            // "_overload == null" is also true once Unity has destroyed it, e.g. while the game is quitting.
            if (_config == null || _overload == null || _overload.clip == null) return;
            if (on == _overload.isPlaying) return;
            if (on)
            {
                _overload.volume = EffectsVolume;
                _overload.Play();
            }
            else _overload.Stop();
        }

        private AudioClip ClipFor(GameSound sound)
        {
            if (_config == null) return null;
            switch (sound)
            {
                case GameSound.Bump: return _config.bump;
                case GameSound.Dock: return _config.dock;
                case GameSound.SwitchShip: return _config.switchShip;
                case GameSound.Crush: return _config.crush;
                case GameSound.LevelComplete: return _config.levelComplete;
                default: return _config.gameOver;
            }
        }

        private void Update()
        {
            if (_config == null) return;
            _music.volume = MusicVolume; // live: follows the Inspector and the Options sliders
            if (_overload.isPlaying) _overload.volume = EffectsVolume;

            // Engines fade toward their target level; a source is only running while it is audible.
            var step = Time.unscaledDeltaTime / _config.engineFadeSeconds;
            foreach (var pair in _engines)
            {
                var source = pair.Value;
                if (source.clip == null) continue;
                _engineTargets.TryGetValue(pair.Key, out var level);
                var target = level * EngineVolume;
                source.volume = Mathf.MoveTowards(source.volume, target, step * Mathf.Max(EngineVolume, 0.01f));
                if (source.volume > 0f && !source.isPlaying) source.Play();
                else if (source.volume <= 0f && target <= 0f && source.isPlaying) source.Stop();
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
