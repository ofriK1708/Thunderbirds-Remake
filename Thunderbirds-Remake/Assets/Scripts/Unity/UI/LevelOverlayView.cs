using System;
using TMPro;
using Thunderbirds.Rules;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    public sealed class LevelOverlayView : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject shade, pausePanel, failedPanel, completePanel;
        [SerializeField] private CanvasGroup controls;
        [SerializeField] private TMP_Text failureTitle, oxygenText;
        [SerializeField] private Button resume, restart, help, options, pauseSelect, pauseMenu;
        [SerializeField] private Button retry, failedSelect, failedMenu, next, completeSelect, completeMenu;
        [SerializeField] private HowToPlayView helpPanel;
        [SerializeField] private OptionsView optionsPanel;
        [SerializeField] private LevelCatalog catalog;
        private Action _resume, _restart, _next, _select, _menu;
        private float _delay, _lock;
        private bool _ending, _complete, _hasNext;
        private int _transitionFrame = -1;
        private InputSystemUIInputModule _module;
        public LevelCatalog Catalog => catalog;
        public bool IsVisible => shade.activeSelf;
        public bool IsReady => IsVisible && !_ending && _lock <= 0 && controls.interactable;

        public void Bind(Action resumeAction, Action restartAction, Action nextAction, Action selectAction,
            Action menuAction, InputSystemUIInputModule module)
        {
            _resume = resumeAction; _restart = restartAction; _next = nextAction;
            _select = selectAction; _menu = menuAction; _module = module;
            Wire(resume, () => _resume()); Wire(restart, () => _restart());
            Wire(help, () => helpPanel.Open(pausePanel)); Wire(options, () => optionsPanel.Open(pausePanel));
            Wire(pauseSelect, () => _select()); Wire(pauseMenu, () => _menu());
            Wire(retry, () => _restart()); Wire(failedSelect, () => _select()); Wire(failedMenu, () => _menu());
            Wire(next, () => { if (_hasNext) _next(); });
            Wire(completeSelect, () => _select()); Wire(completeMenu, () => _menu());
            FillOverlay(helpPanel.transform); FillOverlay(optionsPanel.transform);
            Hide();
        }

        /// <summary>
        /// The help and options prefabs are root canvases, which Unity saves at scale 0 and size 0 because it
        /// sizes root canvases itself. Nested here they are child canvases and nothing sizes them, so stretch them.
        /// </summary>
        public static void FillOverlay(Transform panel)
        {
            var rect = (RectTransform)panel;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.pivot = Vector2.one * 0.5f;
            rect.localScale = Vector3.one;
        }

        private void Wire(Button button, Action action) => button.onClick.AddListener(() => { if (IsReady) action(); });

        public void Hide()
        {
            helpPanel.Close(); optionsPanel.Close();
            pausePanel.SetActive(false); failedPanel.SetActive(false); completePanel.SetActive(false);
            shade.SetActive(false); _ending = false; _lock = _delay = 0;
            controls.interactable = false;
            EventSystem.current?.SetSelectedGameObject(null);
            _transitionFrame = Time.frameCount;
        }

        public void ShowPause()
        {
            Hide(); shade.SetActive(true); pausePanel.SetActive(true);
            controls.interactable = true;
            EventSystem.current?.SetSelectedGameObject(resume.gameObject);
        }

        public void ShowOutcome(bool completed, FailReason reason, float oxygen, bool hasNext)
        {
            Hide(); shade.SetActive(true); // blocks clicks during the half-second hold
            _complete = completed; _hasNext = hasNext; _ending = true; _delay = 0.5f;
            failureTitle.text = reason == FailReason.Crushed ? "CRUSHED" : "OUT OF OXYGEN";
            oxygenText.text = $"Oxygen remaining: {Mathf.CeilToInt(oxygen)} seconds";
            next.interactable = hasNext;
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        // Explicit time input makes both separate half-second safeguards testable.
        public void Advance(float dt)
        {
            if (_ending)
            {
                _delay -= dt;
                if (_delay > 0) return;
                _ending = false; _lock = 0.5f;
                (_complete ? completePanel : failedPanel).SetActive(true);
                return;
            }
            if (!IsVisible || controls.interactable) return;
            _lock = Mathf.Max(0, _lock - dt);
            if (_lock > 0 || (_module != null &&
                ((_module.submit?.action.IsPressed() ?? false) || (_module.leftClick?.action.IsPressed() ?? false)))) return;
            controls.interactable = true;
            EventSystem.current?.SetSelectedGameObject((_complete ? (_hasNext ? next : completeSelect) : retry).gameObject);
        }

        public void Cancel()
        {
            if (_transitionFrame == Time.frameCount || !IsReady) return;
            _transitionFrame = Time.frameCount;
            if (helpPanel.gameObject.activeSelf) helpPanel.Close();
            else if (optionsPanel.gameObject.activeSelf) optionsPanel.Close();
            else if (pausePanel.activeSelf) _resume();
        }
        public void OnCancel(BaseEventData data) { Cancel(); data.Use(); }
    }
}
