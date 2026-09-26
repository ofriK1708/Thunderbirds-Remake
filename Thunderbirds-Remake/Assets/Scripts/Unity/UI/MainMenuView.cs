using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Button playButton;
        [SerializeField] private Button howToPlayButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private HowToPlayView howToPlayPanel;
        [SerializeField] private OptionsView optionsPanel;
        [SerializeField] private UnityEvent onPlay = new UnityEvent();
        [SerializeField] private UnityEvent onHowToPlay = new UnityEvent();
        [SerializeField] private UnityEvent onOptions = new UnityEvent();

        private GameObject ownedEventSystem;

        private void Awake()
        {
            // Reuse the scene's input system when one is already present.
            if (EventSystem.current == null)
            {
                ownedEventSystem = new GameObject("Menu EventSystem",
                    typeof(EventSystem), typeof(InputSystemUIInputModule));
            }
            playButton.onClick.AddListener(onPlay.Invoke);
            howToPlayButton.onClick.AddListener(onHowToPlay.Invoke);
            howToPlayButton.onClick.AddListener(ShowHowToPlay);
            optionsButton.onClick.AddListener(onOptions.Invoke);
            optionsButton.onClick.AddListener(ShowOptions);
            quitButton.onClick.AddListener(Quit);
        }

        private void Start()
        {
            FitContent();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(
                    playButton.interactable ? playButton.gameObject :
                    howToPlayButton.interactable ? howToPlayButton.gameObject : quitButton.gameObject);
        }

        public void ShowHowToPlay()
        {
            if (howToPlayPanel != null) howToPlayPanel.Open(content.gameObject);
        }

        public void ShowOptions()
        {
            if (optionsPanel != null) optionsPanel.Open(content.gameObject);
        }

        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            var scale = Mathf.Min(size.x / 1920f, size.y / 1080f);
            content.localScale = Vector3.one * scale;
        }

        private void OnDestroy()
        {
            playButton.onClick.RemoveListener(onPlay.Invoke);
            howToPlayButton.onClick.RemoveListener(onHowToPlay.Invoke);
            howToPlayButton.onClick.RemoveListener(ShowHowToPlay);
            optionsButton.onClick.RemoveListener(onOptions.Invoke);
            optionsButton.onClick.RemoveListener(ShowOptions);
            quitButton.onClick.RemoveListener(Quit);
            if (ownedEventSystem != null) Destroy(ownedEventSystem);
        }

        private void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
