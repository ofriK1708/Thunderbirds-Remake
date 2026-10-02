using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Persistent fullscreen and volume settings; the push-preview control awaits its feature.</summary>
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
            pushPreview.interactable = false;
            music.interactable = effects.interactable = true;
            backButton.onClick.AddListener(Close);
            ShowSavedVolumes();
            music.onValueChanged.AddListener(UpdateMusic);
            effects.onValueChanged.AddListener(UpdateEffects);
        }

        /// <summary>Put the sliders where the saved settings are, without counting that as a change.</summary>
        public void ShowSavedVolumes()
        {
            music.SetValueWithoutNotify(ToSlider(music, SoundSettings.Music));
            effects.SetValueWithoutNotify(ToSlider(effects, SoundSettings.Effects));
            musicValue.text = Percent(SoundSettings.Music);
            effectsValue.text = Percent(SoundSettings.Effects);
        }

        private void OnDisable()
        {
            fullscreen.onValueChanged.RemoveListener(DisplaySettings.SetFullscreen);
            backButton.onClick.RemoveListener(Close);
            music.onValueChanged.RemoveListener(UpdateMusic);
            effects.onValueChanged.RemoveListener(UpdateEffects);
        }

        /// <summary>Slider callbacks: the change is saved and heard at once (AudioManager reads the setting every frame).</summary>
        public void UpdateMusic(float value)
        {
            SoundSettings.SetMusic(FromSlider(music, value));
            musicValue.text = Percent(SoundSettings.Music);
        }

        public void UpdateEffects(float value)
        {
            SoundSettings.SetEffects(FromSlider(effects, value));
            effectsValue.text = Percent(SoundSettings.Effects);
        }

        // The sliders run 0..100 in the prefab; settings are 0..1.
        private static float FromSlider(Slider slider, float value) => Mathf.InverseLerp(slider.minValue, slider.maxValue, value);
        private static float ToSlider(Slider slider, float volume) => Mathf.Lerp(slider.minValue, slider.maxValue, volume);
        private static string Percent(float volume) => Mathf.RoundToInt(volume * 100f) + "%";
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }
    }
}
