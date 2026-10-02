using TMPro;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Thunderbirds.Editor
{
    // Editor-only authoring. The saved prefab is editable; no layout is generated in the player.
    public static class LevelOverlayPrefabBuilder
    {
        private static TMP_FontAsset font;
        public static void CapturePreviews()
        {
            System.IO.Directory.CreateDirectory("Logs");
            foreach (var size in new[] {new Vector2Int(1920, 1080), new Vector2Int(1280, 960), new Vector2Int(2560, 1080)})
            for (var screen = 0; screen < 3; screen++)
            {
                var view = Object.Instantiate(AssetDatabase.LoadAssetAtPath<LevelOverlayView>("Assets/Resources/LevelOverlays.prefab"));
                if (screen == 0) view.ShowPause();
                else { view.ShowOutcome(screen == 2, Thunderbirds.Rules.FailReason.OutOfOxygen, 42, true); view.Advance(0.5f); view.Advance(0.5f); }
                var cameraObject = new GameObject("Preview camera", typeof(Camera));
                var camera = cameraObject.GetComponent<Camera>();
                var target = new RenderTexture(size.x, size.y, 24); camera.targetTexture = target;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var canvas = view.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                foreach (var text in view.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    if (text.isTextOverflowing) throw new System.InvalidOperationException("Overlay text overflow: " + text.text);
                }
                camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                image.ReadPixels(new UnityEngine.Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes($"Logs/overlay-{screen}-{size.x}x{size.y}.png", image.EncodeToPNG());
                RenderTexture.active = null; camera.targetTexture = null;
                Object.DestroyImmediate(image); Object.DestroyImmediate(target); Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(cameraObject);
            }
        }

        [MenuItem("Thunderbirds/Build Level Overlays Prefab")]
        public static void Build()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Orbitron SDF.asset");
            var root = new GameObject("LevelOverlays", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(LevelOverlayView));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 20;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            var view = root.GetComponent<LevelOverlayView>();
            var shade = Rect("Shade", root.transform, Vector2.zero, Vector2.zero);
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one;
            shade.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.035f, 0.04f, 0.94f);
            Set(view, "shade", shade.gameObject);
            var controls = Rect("Controls", shade, Vector2.zero, Vector2.zero);
            controls.anchorMin = Vector2.zero; controls.anchorMax = Vector2.one;
            Set(view, "controls", controls.gameObject.AddComponent<CanvasGroup>());
            Panel(view, controls, "pausePanel", "PAUSED", new[] {"RESUME", "RESTART", "HOW TO PLAY", "OPTIONS", "LEVEL SELECT", "MAIN MENU"},
                new[] {"resume", "restart", "help", "options", "pauseSelect", "pauseMenu"});
            Panel(view, controls, "failedPanel", "OUT OF OXYGEN", new[] {"RETRY", "LEVEL SELECT", "MAIN MENU"},
                new[] {"retry", "failedSelect", "failedMenu"}, "failureTitle");
            var complete = Panel(view, controls, "completePanel", "RESCUE COMPLETE", new[] {"NEXT LEVEL", "LEVEL SELECT", "MAIN MENU"},
                new[] {"next", "completeSelect", "completeMenu"});
            Set(view, "oxygenText", Label(complete, "Oxygen", "Oxygen remaining: 90 seconds", new Vector2(0, 190), new Vector2(900, 65), 28));
            var help = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/HowToPlayPanel.prefab"), root.transform);
            var options = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/OptionsPanel.prefab"), root.transform);
            foreach (var panel in new[] {help, options})
            {
                panel.GetComponent<Canvas>().sortingOrder = 30;
                LevelOverlayView.FillOverlay(panel.transform); // root canvases are saved at scale 0
                panel.SetActive(false);
            }
            Set(view, "helpPanel", help.GetComponent<HowToPlayView>()); Set(view, "optionsPanel", options.GetComponent<OptionsView>());
            Set(view, "catalog", AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset"));
            shade.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/LevelOverlays.prefab");
            Object.DestroyImmediate(root); AssetDatabase.SaveAssets();
        }

        private static Transform Panel(LevelOverlayView view, Transform parent, string field, string title, string[] labels, string[] fields, string titleField = null)
        {
            var panel = Rect(field, parent, Vector2.zero, new Vector2(1000, 900));
            Set(view, field, panel.gameObject);
            var text = Label(panel, "Title", title, new Vector2(0, 310), new Vector2(950, 100), 48);
            text.color = new Color(0.88f, 0.63f, 0.25f);
            if (titleField != null) Set(view, titleField, text);
            for (var i = 0; i < labels.Length; i++)
            {
                var rect = Rect(fields[i] + " Button", panel, new Vector2(0, (labels.Length == 6 ? 190 : 70) - i * 90), new Vector2(640, 72));
                var image = rect.gameObject.AddComponent<Image>(); image.color = Color.white;
                var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
                var colors = button.colors; colors.normalColor = new Color(0.08f, 0.2f, 0.22f);
                colors.highlightedColor = colors.selectedColor = new Color(0, 0.5f, 0.5f);
                colors.pressedColor = new Color(0.4f, 0.3f, 0.12f); colors.disabledColor = new Color(0.15f, 0.15f, 0.15f); button.colors = colors;
                Label(rect, "Label", labels[i], Vector2.zero, new Vector2(600, 65), 27);
                Set(view, fields[i], button);
            }
            panel.gameObject.SetActive(false); return panel;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f; rect.anchoredPosition = pos; rect.sizeDelta = size; return rect;
        }
        private static TMP_Text Label(Transform parent, string name, string value, Vector2 pos, Vector2 size, int sizeInPoints)
        {
            var text = Rect(name, parent, pos, size).gameObject.AddComponent<TextMeshProUGUI>(); text.font = font; text.text = value;
            text.fontSize = sizeInPoints; text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, 1, 0.925f); text.raycastTarget = false; return text;
        }
        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
