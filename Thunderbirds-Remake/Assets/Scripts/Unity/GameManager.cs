using UnityEngine;
using UnityEngine.SceneManagement;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// The one object that outlives a scene load (GDD §7, course feature 2: Singleton). It carries what the next
    /// scene needs to know — which level was chosen, and whether the menu should reopen on Level Select — and is
    /// the single door to unlock progress and scene changes. Created on first use, so no scene has to contain it.
    /// Restart, respawn and Next Level never come through here: they rebuild the rules model inside Game.unity.
    /// </summary>
    public sealed class GameManager : MonoBehaviour
    {
        public const string MenuScene = "MainMenu";
        public const string GameScene = "Game";

        private static GameManager _instance;

        public static GameManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject(nameof(GameManager));
                _instance = host.AddComponent<GameManager>();
                if (Application.isPlaying) DontDestroyOnLoad(host);
                else host.hideFlags = HideFlags.HideAndDontSave; // Edit Mode tests: never saved into a scene
                return _instance;
            }
        }

        /// <summary>The campaign level waiting to be played, or null for the sandbox.</summary>
        public LevelData SelectedLevel { get; private set; }

        /// <summary>Catalog index of <see cref="SelectedLevel"/>, or -1.</summary>
        public int SelectedIndex { get; private set; } = -1;

        /// <summary>Set when leaving a level for Level Select; the main menu reads it once.</summary>
        public bool OpenLevelSelectOnMenu { get; private set; }

        // ------------------------------------------------------------ selection ----

        /// <summary>Remember a level for the Game scene. False (and nothing changes) if it is missing or locked.</summary>
        public bool SelectLevel(LevelCatalog catalog, int index)
        {
            var level = catalog != null ? catalog.Get(index) : null;
            if (level == null || !IsUnlocked(index)) return false;
            SelectedLevel = level;
            SelectedIndex = index;
            return true;
        }

        /// <summary>Hand the selection to the Game scene, once: a second call returns null and -1.</summary>
        public LevelData TakeSelection(out int index)
        {
            var level = SelectedLevel;
            index = SelectedIndex;
            ClearSelection();
            return level;
        }

        public void ClearSelection()
        {
            SelectedLevel = null;
            SelectedIndex = -1;
        }

        /// <summary>True once after returning from a level via "Level Select".</summary>
        public bool ConsumeOpenLevelSelect()
        {
            var open = OpenLevelSelectOnMenu;
            OpenLevelSelectOnMenu = false;
            return open;
        }

        // ------------------------------------------------------------- progress ----

        public bool IsUnlocked(int index) => LevelProgress.IsUnlocked(index);
        public bool IsCompleted(int index) => LevelProgress.IsCompleted(index);

        /// <summary>Saves the completion to PlayerPrefs, which unlocks the next level.</summary>
        public void CompleteLevel(int index) => LevelProgress.Complete(index);

        // ----------------------------------------------------------- scene flow ----

        /// <summary>Menu → Game with a campaign level. False if the level is missing or locked.</summary>
        public bool PlayLevel(LevelCatalog catalog, int index)
        {
            if (!SelectLevel(catalog, index)) return false;
            SceneManager.LoadScene(GameScene);
            return true;
        }

        /// <summary>Menu → Game with no selection: LevelView falls back to its preview level.</summary>
        public void PlaySandbox()
        {
            ClearSelection();
            SceneManager.LoadScene(GameScene);
        }

        /// <summary>Game → Menu. The level state is discarded; only unlock progress is kept (GDD §3).</summary>
        public void ReturnToMenu(bool openLevelSelect)
        {
            ClearSelection();
            OpenLevelSelectOnMenu = openLevelSelect;
            SceneManager.LoadScene(MenuScene);
        }

        // ------------------------------------------------------------ lifecycle ----

        private void Awake()
        {
            if (_instance != null && _instance != this) Destroy(gameObject); // a stray copy never replaces the original
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Drops the instance so the next use starts clean: on entering Play mode (domain reload may be off),
        /// and between tests.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetInstance()
        {
            if (_instance == null) return;
            var host = _instance.gameObject;
            _instance = null;
            if (Application.isPlaying) Destroy(host);
            else DestroyImmediate(host);
        }
    }
}
