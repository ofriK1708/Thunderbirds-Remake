using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Persistent fullscreen setting; audio and push-preview controls await their features.</summary>
    public sealed class OptionsView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Button backButton;
        [SerializeField] private Toggle fullscreen;
        [SerializeField] private Toggle pushPreview;
        [SerializeField] private Slider music;
        [SerializeField] private Slider effects;
        [SerializeField] private TMP_Text musicValue;
        [SerializeField] private TMP_Text effectsValue;
        private GameObject caller;
        private GameObject previousSelection;

        private void Awake()
        {
            if (fullscreen.GetComponent<CheckboxHoverEffect>() == null)
                fullscreen.gameObject.AddComponent<CheckboxHoverEffect>();
            if (pushPreview.GetComponent<CheckboxHoverEffect>() == null)
                pushPreview.gameObject.AddComponent<CheckboxHoverEffect>();
        }

        public void Open(GameObject from)
        {
            if (gameObject.activeSelf) return;
            caller = from;
            previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (caller != null) caller.SetActive(false);
            gameObject.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(fullscreen.gameObject);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (caller != null) caller.SetActive(true);
            if (EventSystem.current != null && previousSelection != null && previousSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousSelection);
            caller = null;
            previousSelection = null;
        }

        private void OnEnable()
        {
            Fit();
            fullscreen.SetIsOnWithoutNotify(DisplaySettings.Fullscreen);
            fullscreen.onValueChanged.AddListener(DisplaySettings.SetFullscreen);
            music.interactable = effects.interactable = pushPreview.interactable = false;
            backButton.onClick.AddListener(Close);
            music.onValueChanged.AddListener(UpdateMusic);
            effects.onValueChanged.AddListener(UpdateEffects);
            UpdateMusic(music.value);
            UpdateEffects(effects.value);
        }

        private void OnDisable()
        {
            fullscreen.onValueChanged.RemoveListener(DisplaySettings.SetFullscreen);
            backButton.onClick.RemoveListener(Close);
            music.onValueChanged.RemoveListener(UpdateMusic);
            effects.onValueChanged.RemoveListener(UpdateEffects);
        }

        private void UpdateMusic(float value) => musicValue.text = Mathf.RoundToInt(value) + "%";
        private void UpdateEffects(float value) => effectsValue.text = Mathf.RoundToInt(value) + "%";
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }
    }
}
