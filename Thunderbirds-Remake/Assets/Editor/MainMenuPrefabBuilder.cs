using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Thunderbirds.Editor
{
    /// <summary>Builds an editable UI asset without changing either team-owned scene.</summary>
    public static class MainMenuPrefabBuilder
    {
        private static TMP_FontAsset font;
        private static readonly Color Ink = Hex("101010");
        private static readonly Color Paper = Hex("FFFFEC");
        private static readonly Color Gold = Hex("E0A040");

        public static void CapturePreview()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainMenu.prefab");
            var root = Object.Instantiate(prefab);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Ink;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            foreach (var label in root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            image.ReadPixels(new UnityEngine.Rect(0, 0, 1920, 1080), 0, 0);
            image.Apply();
            System.IO.File.WriteAllBytes("Logs/main-menu-preview.png", image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
        }

        [MenuItem("Thunderbirds/Build Main Menu Prefab")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset") == null)
            {
                throw new System.InvalidOperationException("Import TMP Essential Resources before building the menu.");
            }
            const string fontPath = "Assets/Art/Fonts/Orbitron SDF.asset";
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            if (font == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/Orbitron.ttf");
                font = TMP_FontAsset.CreateFontAsset(source);
                font.name = "Orbitron SDF";
                AssetDatabase.CreateAsset(font, fontPath);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            }

            var root = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(Thunderbirds.Unity.MainMenuView));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var background = Rect("Background", root.transform, Vector2.zero, Vector2.zero);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.gameObject.AddComponent<Image>().color = Ink;

            // Keep content inside the reference frame on both wide and narrow displays.
            var frame = Rect("Content", root.transform, Vector2.zero, new Vector2(1500, 900));
            Label(frame, "Eyebrow", "Ofri & Rotem Present", 23, new Vector2(0, 325), new Vector2(1300, 50), Gold);
            Label(frame, "Title", "THUNDERBIRDS", 86, new Vector2(0, 228), new Vector2(1450, 115), Paper);
            Label(frame, "Subtitle", "H E A V Y   L I F T", 33, new Vector2(0, 148), new Vector2(1300, 60), Gold);
            var line = Rect("Title underline", frame, new Vector2(0, 91), new Vector2(90, 3));
            line.gameObject.AddComponent<Image>().color = Gold;

            var play = Button(frame, "Play", 12, false);
            var how = Button(frame, "How to Play", -82, false);
            var options = Button(frame, "Options", -176, false);
            var quit = Button(frame, "Quit", -270, true);
            Label(frame, "Footer", "TWO SHIPS. ONE RESCUE.", 18, new Vector2(0, -395), new Vector2(1000, 40), Hex("888888"));

            var serialized = new SerializedObject(root.GetComponent<Thunderbirds.Unity.MainMenuView>());
            serialized.FindProperty("content").objectReferenceValue = frame;
            serialized.FindProperty("playButton").objectReferenceValue = play;
            serialized.FindProperty("howToPlayButton").objectReferenceValue = how;
            serialized.FindProperty("optionsButton").objectReferenceValue = options;
            serialized.FindProperty("quitButton").objectReferenceValue = quit;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/MainMenu.prefab");
            Object.DestroyImmediate(root);
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HowToPlayPanel.prefab") != null)
                HowToPlayPrefabBuilder.AttachToMainMenu();
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/OptionsPanel.prefab") != null)
                OptionsPrefabBuilder.AttachToMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("MainMenu prefab built. Drag Assets/Prefabs/MainMenu.prefab into MainMenu scene.");
        }

        private static Button Button(Transform parent, string title, float y, bool enabled)
        {
            var rect = Rect(title + " Button", parent, new Vector2(0, y), new Vector2(510, 76));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            var colors = button.colors;
            colors.normalColor = Hex("253535");
            colors.highlightedColor = Hex("008080");
            colors.selectedColor = Hex("008080");
            colors.pressedColor = Hex("785828");
            colors.disabledColor = Hex("1B2222");
            button.colors = colors;
            var border = rect.gameObject.AddComponent<Outline>();
            border.effectColor = enabled ? Gold : Hex("394242");
            border.effectDistance = new Vector2(1, -1);
            Label(rect, "Label", title.ToUpperInvariant(), 27, Vector2.zero, new Vector2(480, 60), enabled ? Paper : Hex("888888"));
            return button;
        }

        private static void Label(Transform parent, string name, string text, float size, Vector2 position, Vector2 dimensions, Color color)
        {
            var label = Rect(name, parent, position, dimensions).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
