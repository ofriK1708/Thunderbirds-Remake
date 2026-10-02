using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Reusable help screen; its caller owns pausing and the gameplay state.</summary>
    public sealed class HowToPlayView : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private Button backButton;
        [SerializeField] private TMP_Text controlsText;
        [Tooltip("Bindings shown on the controls card; falls back to the default keys when unset.")]
        [SerializeField] private InputActionAsset gameplayActions;
        [Tooltip("Capacities, speed, crush grace and lives are read from here each time the screen opens.")]
        [SerializeField] private GameConfig config;
        [SerializeField] private TMP_Text shipsText;
        [SerializeField] private TMP_Text blocksText;
        [SerializeField] private TMP_Text survivalText;

        private GameObject returnPanel;
        private GameObject returnSelection;
        private IDisposable deviceSubscription;
        private bool gamepad;

        public void Open(GameObject caller)
        {
            if (gameObject.activeSelf) return;
            returnPanel = caller;
            var events = EventSystem.current;
            returnSelection = events != null ? events.currentSelectedGameObject : null;
            gamepad = false;
            if (events != null && events.currentInputModule is UnityEngine.InputSystem.UI.InputSystemUIInputModule input)
                gamepad = input.submit != null && input.submit.action.WasPerformedThisFrame()
                    && input.submit.action.activeControl?.device is Gamepad;
            if (returnPanel != null) returnPanel.SetActive(false);
            gameObject.SetActive(true);
            if (events != null) events.SetSelectedGameObject(backButton.gameObject);
        }

        public void Close()
        {
            if (!gameObject.activeSelf) return;
            gameObject.SetActive(false);
            if (returnPanel != null) returnPanel.SetActive(true);
            if (EventSystem.current != null && returnSelection != null && returnSelection.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(returnSelection);
            returnPanel = null;
            returnSelection = null;
        }

        private void OnEnable()
        {
            backButton.onClick.AddListener(Close);
            FitContent();
            RefreshControls();
            RefreshConfigCards();
            deviceSubscription = InputSystem.onAnyButtonPress.Call(control =>
            {
                if (!(control.device is Gamepad) && !(control.device is Keyboard) && !(control.device is Mouse)) return;
                gamepad = control.device is Gamepad;
                RefreshControls();
            });
        }

        private void OnDisable()
        {
            backButton.onClick.RemoveListener(Close);
            deviceSubscription?.Dispose();
            deviceSubscription = null;
        }

        private void OnRectTransformDimensionsChange() => FitContent();

        private void FitContent()
        {
            if (content == null) return;
            var size = ((RectTransform)transform).rect.size;
            content.localScale = Vector3.one * Mathf.Min(size.x / 1920f, size.y / 1080f);
        }

        private void RefreshControls()
        {
            var move = Binding("Move", gamepad ? "Left stick / D-pad" : "WASD / Arrow keys");
            var change = Binding("SwitchShip", gamepad ? "A / Cross (south)" : "Space / Tab");
            var restart = Binding("Restart", gamepad ? "View / Select" : "R");
            var pause = Binding("Pause", gamepad ? "Menu / Start" : "Esc");
            controlsText.text = (gamepad ? "<color=#E0A040>GAMEPAD</color>" : "<color=#E0A040>KEYBOARD + MOUSE</color>")
                + $"\nMove     {move}\nSwitch   {change}\nRestart  Hold {restart}\nPause    {pause}"
                + "\n\nHold a direction to keep moving.\nOnly the selected ship moves.";
        }

        private void RefreshConfigCards()
        {
            if (config == null || config.kestrel == null || config.atlas == null) return; // keep the built-in text
            if (shipsText != null) shipsText.text = HowToPlayText.Ships(config.kestrel, config.atlas, config.kestrelSpeedRatio);
            if (blocksText != null) blocksText.text = HowToPlayText.Blocks(config.kestrel, config.atlas);
            if (survivalText != null) survivalText.text = HowToPlayText.Survival(config.crushGraceSeconds, config.livesPerLevel);
        }

        private string Binding(string name, string fallback)
        {
            var action = gameplayActions != null ? gameplayActions.FindAction(name, false) : null;
            if (action == null) return fallback;
            var group = gamepad ? "Gamepad" : "Keyboard&Mouse";
            var display = action.GetBindingDisplayString(group: group);
            return string.IsNullOrEmpty(display) ? fallback : display;
        }
    }
}
