using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Thunderbirds.Unity;
using Object = UnityEngine.Object;

namespace Thunderbirds.Editor
{
    public static class OptionsPrefabBuilder
    {
        private const string Path = "Assets/Prefabs/OptionsPanel.prefab";
        private static TMP_FontAsset font;
        private static readonly Color Paper = Hex("FFFFEC");
        private static readonly Color Gold = Hex("E0A040");

        [MenuItem("Thunderbirds/Build Options Prefab")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Orbitron SDF.asset");
            var root = new GameObject("OptionsPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster), typeof(OptionsView));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var background = Rect("Background", root.transform, 0, 0, 0, 0);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.gameObject.AddComponent<Image>().color = Hex("101010");
            var content = Rect("Content", root.transform, 0, 0, 1920, 1080);
            Text(content, "Title", "OPTIONS", 49, 0, 410, 1300, 75, Gold);
            Text(content, "Subtitle", "Make the rescue your own.", 23, 0, 337, 1300, 50, Paper);
            var view = root.GetComponent<OptionsView>();
            var fullscreen = ToggleRow(content, "Fullscreen", 210, true);
            var music = SliderRow(content, "Music Volume", 65, out var musicValue);
            var effects = SliderRow(content, "Sound Effects", -80, out var effectsValue);
            var preview = ToggleRow(content, "Push Preview", -225, false);
            Text(content, "Preview notice", "PREVIEW ONLY  /  Changes are not applied or saved yet.", 20,
                0, -339, 1450, 40, Gold);
            var backRect = Rect("Back Button", content, 0, -427, 310, 66);
            var image = backRect.gameObject.AddComponent<Image>();
            var back = backRect.gameObject.AddComponent<Button>();
            back.targetGraphic = image;
            Style(back);
            Text(backRect, "Label", "BACK", 25, 0, 0, 270, 54, Paper);
            Set(view, "content", content);
            Set(view, "backButton", back);
            Set(view, "fullscreen", fullscreen);
            Set(view, "pushPreview", preview);
            Set(view, "music", music);
            Set(view, "effects", effects);
            Set(view, "musicValue", musicValue);
            Set(view, "effectsValue", effectsValue);
            foreach (var selectable in new Selectable[] { fullscreen, music, effects, preview, back })
                Set(selectable.gameObject.AddComponent<OptionsCancelHandler>(), "screen", view);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            Object.DestroyImmediate(root);
            AttachToMainMenu();
            AssetDatabase.SaveAssets();
        }

        public static void BuildAndPreview()
        {
            Build();
            HowToPlayValidation.Capture(1920, 1080, Path, "options");
            HowToPlayValidation.Capture(1280, 960, Path, "options");
            Debug.Log("OPTIONS_LAYOUT_PASS");
        }

        public static void AttachToMainMenu()
        {
            const string menuPath = "Assets/Prefabs/MainMenu.prefab";
            var root = PrefabUtility.LoadPrefabContents(menuPath);
            try
            {
                var panel = root.GetComponentInChildren<OptionsView>(true);
                if (panel == null)
                    panel = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), root.transform)).GetComponent<OptionsView>();
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                panel.gameObject.SetActive(false);
                Set(root.GetComponent<MainMenuView>(), "optionsPanel", panel);
                var button = root.transform.Find("Content/Options Button").GetComponent<Button>();
                button.interactable = true;
                button.GetComponentInChildren<TMP_Text>().color = Paper;
                button.GetComponent<Outline>().effectColor = Gold;
                PrefabUtility.SaveAsPrefabAsset(root, menuPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform Row(Transform parent, string title, float y)
        {
            var row = Rect(title + " Row", parent, 0, y, 1120, 118);
            row.gameObject.AddComponent<Image>().color = Hex("1B2222");
            var label = Text(row, "Label", title, 27, -265, 0, 470, 55, Paper);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            return row;
        }

        private static Toggle ToggleRow(Transform parent, string title, float y, bool on)
        {
            var row = Row(parent, title, y);
            var box = Rect(title + " Toggle", row, 430, 0, 54, 54);
            var image = box.gameObject.AddComponent<Image>();
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = image;
            Style(toggle);
            var check = Rect("Checkmark", box, 0, 0, 30, 30).gameObject.AddComponent<Image>();
            check.color = Gold;
            check.raycastTarget = false;
            toggle.graphic = check;
            toggle.isOn = on;
            return toggle;
        }

        private static Slider SliderRow(Transform parent, string title, float y, out TMP_Text value)
        {
            var row = Row(parent, title, y);
            var control = Rect(title + " Slider", row, 220, 0, 340, 54);
            var slider = control.gameObject.AddComponent<Slider>();
            var track = Rect("Track", control, 0, 0, 340, 8).gameObject.AddComponent<Image>();
            track.color = Hex("394242");
            var fillArea = Rect("Fill Area", control, 0, 0, 320, 8);
            var fill = Rect("Fill", fillArea, 0, 0, 0, 0);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.gameObject.AddComponent<Image>().color = Gold;
            var handleArea = Rect("Handle Area", control, 0, 0, 320, 54);
            var handle = Rect("Handle", handleArea, 0, 0, 22, 36);
            var handleImage = handle.gameObject.AddComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            Style(slider);
            var colors = slider.colors;
            colors.normalColor = Paper;
            slider.colors = colors;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.value = 75;
            value = Text(row, "Value", "75%", 24, 470, 0, 110, 50, Gold);
            return slider;
        }

        private static void Style(Selectable selectable)
        {
            var colors = selectable.colors;
            colors.normalColor = Hex("394242");
            colors.highlightedColor = colors.selectedColor = Hex("008080");
            colors.pressedColor = Hex("785828");
            selectable.colors = colors;
        }

        private static TMP_Text Text(Transform parent, string name, string text, float size, float x, float y,
            float width, float height, Color color)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Set(Object target, string name, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
