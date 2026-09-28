using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Thunderbirds.Unity;
using Object = UnityEngine.Object;

namespace Thunderbirds.Editor
{
    /// <summary>Batch verification in a disposable project copy; never saves a scene.</summary>
    [InitializeOnLoad]
    public static class HowToPlayValidation
    {
        private const string Running = "Thunderbirds.HowToPlayValidation";
        private static GameObject menu;
        private static int createdFrame;

        static HowToPlayValidation()
        {
            if (SessionState.GetBool(Running, false)) EditorApplication.update += CheckPlayMode;
        }

        public static void Run()
        {
            SessionState.SetBool(Running, true);
            EditorApplication.update -= CheckPlayMode;
            EditorApplication.update += CheckPlayMode;
            EditorApplication.EnterPlaymode();
        }

        private static void CheckPlayMode()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (menu == null)
                {
                    menu = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MainMenu.prefab"));
                    createdFrame = Time.frameCount;
                    return;
                }
                if (Time.frameCount < createdFrame + 5) return;
                var help = menu.GetComponentInChildren<HowToPlayView>(true);
                var content = menu.transform.Find("Content").gameObject;
                var button = content.transform.Find("How to Play Button").GetComponent<Button>();
                var back = help.GetComponentInChildren<Button>(true);
                Require(button.interactable, "Help button must be enabled");
                Require(!help.gameObject.activeSelf, "Help must start hidden");
                Require(EventSystem.current != null, "Menu must supply an EventSystem");
                Require(EventSystem.current.currentSelectedGameObject == button.gameObject, "Help must be initial selection");
                for (var i = 0; i < 3; i++)
                {
                    button.onClick.Invoke();
                    Require(help.gameObject.activeSelf && !content.activeSelf, "Opening help must hide menu content");
                    Require(EventSystem.current.currentSelectedGameObject == back.gameObject, "Back must receive focus");
                    if (i == 1)
                        ExecuteEvents.Execute(back.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                    else back.onClick.Invoke();
                    Require(!help.gameObject.activeSelf && content.activeSelf, "Back/Cancel must restore menu");
                    Require(EventSystem.current.currentSelectedGameObject == button.gameObject, "Return focus must be restored");
                }
                // The same screen returns to its actual caller, including a future Pause panel.
                var caller = new GameObject("Other caller");
                help.Open(caller);
                Require(!caller.activeSelf, "Opening help must hide its caller");
                help.Close();
                Require(caller.activeSelf, "Closing help must restore its caller");
                Object.Destroy(caller);
                Debug.Log("HOW_TO_PLAY_RUNTIME_PASS: startup, open, Back, Cancel, repeated navigation, caller and focus restoration.");
                Finish(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static void Finish(int exitCode)
        {
            SessionState.SetBool(Running, false);
            EditorApplication.update -= CheckPlayMode;
            EditorApplication.Exit(exitCode);
        }

        public static void CapturePreviews()
        {
            Directory.CreateDirectory("Logs");
            Capture(1920, 1080);
            Capture(1280, 720);
            Capture(1280, 960);
            Capture(2560, 1080);
            Debug.Log("HOW_TO_PLAY_LAYOUT_PASS: all help text fits at 16:9, 4:3 and ultrawide.");
        }

        public static void BuildAndPreview()
        {
            HowToPlayPrefabBuilder.Build();
            CapturePreviews();
        }

        public static void Capture(int width, int height, string prefabPath = "Assets/Prefabs/HowToPlayPanel.prefab", string prefix = "how-to-play")
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            root.SetActive(true);
            var cameraObject = new GameObject("Preview Camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases();
            // Match the screen's runtime responsive fit during editor rendering.
            var size = ((RectTransform)root.transform).rect.size;
            root.transform.Find("Content").localScale = Vector3.one * Mathf.Min(size.x / 1920, size.y / 1080);
            foreach (var label in root.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Require(!label.isTextOverflowing, "Text overflow: " + label.name + " at " + width + "x" + height);
            }
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes($"Logs/{prefix}-{width}x{height}.png", image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(cameraObject);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
