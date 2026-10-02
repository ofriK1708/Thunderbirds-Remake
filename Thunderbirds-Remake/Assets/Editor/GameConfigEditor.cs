using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Editor
{
    /// <summary>
    /// One place to tune the game. The settings live in three assets (GameConfig and one ShipConfig per ship),
    /// not on a scene object; this inspector shows the two ship configs inline under the GameConfig, and the
    /// Thunderbirds menu jumps straight to it.
    /// </summary>
    [CustomEditor(typeof(GameConfig))]
    public sealed class GameConfigEditor : UnityEditor.Editor
    {
        private const string ConfigPath = "Assets/Config/GameConfig.asset";

        private const string AudioPath = "Assets/Resources/AudioConfig.asset";

        private UnityEditor.Editor _kestrel, _atlas, _audio;

        [MenuItem("Thunderbirds/Open Game Settings")]
        public static void Open()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            if (config == null)
            {
                Debug.LogError($"No GameConfig at {ConfigPath}.");
                return;
            }
            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            EditorApplication.ExecuteMenuItem("Window/General/Inspector");
        }

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Every tuning value of the game. Edits apply on the next Restart or level start, even in Play mode.",
                MessageType.Info);
            DrawDefaultInspector();

            var config = (GameConfig)target;
            DrawShip("Kestrel settings", config.kestrel, ref _kestrel);
            DrawShip("Atlas settings", config.atlas, ref _atlas);
            DrawAudio();
        }

        private static void DrawShip(string title, ShipConfig ship, ref UnityEditor.Editor editor)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (ship == null)
            {
                EditorGUILayout.HelpBox("No ShipConfig assigned in the Ships section above.", MessageType.Warning);
                return;
            }
            CreateCachedEditor(ship, null, ref editor);
            using (new EditorGUI.IndentLevelScope()) editor.OnInspectorGUI();
        }

        private void DrawAudio()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Sound settings", EditorStyles.boldLabel);
            var audio = AssetDatabase.LoadAssetAtPath<AudioConfig>(AudioPath);
            if (audio == null)
            {
                EditorGUILayout.HelpBox($"No AudioConfig at {AudioPath}: the game is silent.", MessageType.Warning);
                return;
            }
            CreateCachedEditor(audio, null, ref _audio);
            using (new EditorGUI.IndentLevelScope()) _audio.OnInspectorGUI();
        }

        private void OnDisable()
        {
            if (_audio != null) DestroyImmediate(_audio);
            if (_kestrel != null) DestroyImmediate(_kestrel);
            if (_atlas != null) DestroyImmediate(_atlas);
        }
    }
}
