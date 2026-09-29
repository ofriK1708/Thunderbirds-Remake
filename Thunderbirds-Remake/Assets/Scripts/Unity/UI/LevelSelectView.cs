using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Campaign selection built under the existing menu Canvas without editing scenes.</summary>
    public sealed class LevelSelectView : MonoBehaviour, ICancelHandler
    {
        private readonly Button[] _levels = new Button[LevelProgress.LevelCount];
        private LevelCatalog _catalog;
        private GameObject _caller;
        private GameObject _previous;
        private RectTransform _frame;
        private Button _sandbox;
        public static LevelSelectView Create(Transform parent, TMP_FontAsset font, LevelCatalog catalog)
        {
            var root = new GameObject("Level Select", typeof(RectTransform), typeof(Image), typeof(LevelSelectView));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.063f, 0.063f, 0.063f);
            var view = root.GetComponent<LevelSelectView>();
            view._catalog = catalog;
            view._frame = Rect("Content", root.transform, Vector2.zero, new Vector2(1600, 900));
            view.Label("SELECT A MISSION", new Vector2(0, 320), new Vector2(1500, 90), 56, font);
            view.Label("Complete a mission to unlock the next.", new Vector2(0, 230), new Vector2(1500, 60), 26, font);
            for (var i = 0; i < view._levels.Length; i++)
            {
                var index = i;
                view._levels[i] = view.Button("L" + (i + 1), new Vector2((i - 2) * 290, 50), new Vector2(260, 190), font,
                    () => view.Launch(index));
            }
            view.Label("More missions will appear as they become available.", new Vector2(0, -125), new Vector2(1500, 60), 23, font);
            view._sandbox = view.Button("Sandbox", new Vector2(0, -245), new Vector2(510, 76), font, () =>
            {
                LevelLaunch.Clear();
                SceneManager.LoadScene("Game");
            });
            view.Button("Back", new Vector2(0, -345), new Vector2(510, 76), font, view.Close);
            root.SetActive(false);
            return view;
        }

        public void Open(GameObject caller)
        {
            _caller = caller;
            _previous = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            caller.SetActive(false);
            gameObject.SetActive(true);
            Fit();
            GameObject first = null;
            for (var i = 0; i < _levels.Length; i++)
            {
                var available = _catalog != null && _catalog.Get(i) != null;
                var unlocked = LevelProgress.IsUnlocked(i);
                _levels[i].interactable = available && unlocked;
                _levels[i].GetComponentInChildren<TMP_Text>().text = $"L{i + 1}\n" +
                    (!available ? "COMING SOON" : LevelProgress.IsCompleted(i) ? "COMPLETED" : unlocked ? "PLAY" : "LOCKED");
                if (first == null && _levels[i].interactable) first = _levels[i].gameObject;
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(first != null ? first : _sandbox.gameObject);
        }

        private void Launch(int index)
        {
            if (LevelLaunch.Select(_catalog, index)) SceneManager.LoadScene("Game");
        }
        public void Close()
        {
            gameObject.SetActive(false);
            if (_caller != null) _caller.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_previous);
        }
        public void OnCancel(BaseEventData data) { Close(); data.Use(); }
        private void OnRectTransformDimensionsChange() => Fit();
        private void Fit()
        {
            if (_frame == null) return;
            var size = ((RectTransform)transform).rect.size;
            _frame.localScale = Vector3.one * Mathf.Min(size.x / 1920, size.y / 1080);
        }
        private Button Button(string title, Vector2 position, Vector2 size, TMP_FontAsset font, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(title + " Button", _frame, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colours = button.colors;
            colours.normalColor = new Color(0.14f, 0.21f, 0.21f);
            colours.highlightedColor = colours.selectedColor = new Color(0, 0.5f, 0.5f);
            colours.disabledColor = new Color(0.16f, 0.16f, 0.16f);
            button.colors = colours;
            button.onClick.AddListener(action);
            var cancel = new EventTrigger.Entry { eventID = EventTriggerType.Cancel };
            cancel.callback.AddListener(data => OnCancel(data));
            rect.gameObject.AddComponent<EventTrigger>().triggers.Add(cancel);
            var text = Label(title, Vector2.zero, size - Vector2.one * 20, 28, font);
            text.transform.SetParent(rect, false);
            return button;
        }
        private TMP_Text Label(string text, Vector2 position, Vector2 size, int fontSize, TMP_FontAsset font)
        {
            var label = Rect("Label", _frame, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text; label.font = font; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = new Color(1, 1, 0.925f);
            label.raycastTarget = false;
            return label;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }
    }
}
