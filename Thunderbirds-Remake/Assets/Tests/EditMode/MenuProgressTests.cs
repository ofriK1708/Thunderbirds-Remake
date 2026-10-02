using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Thunderbirds.Tests.EditMode
{
    public class MenuProgressTests
    {
        private readonly Dictionary<string, int?> _saved = new Dictionary<string, int?>();
        private LevelCatalog _catalog;
        private LevelData _level;
        private GameObject _root;

        [SetUp]
        public void SetUp()
        {
            _saved.Clear();
            for (var i = 0; i < 5; i++) Preserve(LevelProgress.Key(i));
            Preserve(DisplaySettings.FullscreenKey);
            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            _level = ScriptableObject.CreateInstance<LevelData>();
            var serialized = new SerializedObject(_catalog);
            var list = serialized.FindProperty("levels");
            list.arraySize = 5;
            for (var i = 0; i < 5; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = _level;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private void Preserve(string key)
        {
            _saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
            PlayerPrefs.DeleteKey(key);
        }
        [TearDown]
        public void TearDown()
        {
            GameManager.ResetInstance();
            if (_root != null) Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_catalog); Object.DestroyImmediate(_level);
            foreach (var pair in _saved)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
                else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
        }
        [Test]
        public void CompletingLevel_Persists_UnlocksNext_AndReplayDoesNotRelock()
        {
            Assert.IsTrue(LevelProgress.IsUnlocked(0));
            Assert.IsFalse(LevelProgress.IsUnlocked(1));
            LevelProgress.Complete(0);
            LevelProgress.Complete(1);
            LevelProgress.Complete(0);
            Assert.AreEqual(1, PlayerPrefs.GetInt(LevelProgress.Key(1)));
            Assert.IsTrue(LevelProgress.IsCompleted(0));
            Assert.IsTrue(LevelProgress.IsUnlocked(2));
            Assert.IsFalse(LevelProgress.IsUnlocked(3));
            Assert.IsFalse(LevelProgress.IsUnlocked(5));
        }
        [Test]
        public void Selection_RejectsLockedAndMissing_AndHandoffIsConsumedOnce()
        {
            Assert.IsFalse(GameManager.Instance.SelectLevel(_catalog, 1));
            Assert.IsFalse(GameManager.Instance.SelectLevel(null, 0));
            Assert.IsTrue(GameManager.Instance.SelectLevel(_catalog, 0));
            Assert.AreEqual(_level, GameManager.Instance.TakeSelection(out var index));
            Assert.AreEqual(0, index);
            Assert.IsNull(GameManager.Instance.TakeSelection(out index));
            Assert.AreEqual(-1, index);
        }
        [Test]
        public void LevelTiles_ReflectProgress_AndMissingAssetsStayDisabled()
        {
            _root = new GameObject("Test canvas", typeof(RectTransform), typeof(Canvas));
            var caller = new GameObject("Main content"); caller.transform.SetParent(_root.transform);
            LevelProgress.Complete(0);
            var view = LevelSelectView.Create(_root.transform, null, _catalog);
            view.Open(caller);
            var buttons = view.GetComponentsInChildren<Button>();
            Assert.AreEqual(7, buttons.Length);
            Assert.IsTrue(buttons.Single(b => b.name == "L2 Button").interactable);
            Assert.IsFalse(buttons.Single(b => b.name == "L3 Button").interactable);
            Assert.AreEqual("L1\nCOMPLETED", buttons.Single(b => b.name == "L1 Button").GetComponentInChildren<TMPro.TMP_Text>().text);
            var serialized = new SerializedObject(_catalog);
            serialized.FindProperty("levels").arraySize = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
            view.Close(); view.Open(caller);
            Assert.IsTrue(buttons.Where(b => b.name.StartsWith("L")).All(b => !b.interactable));
            view.Close(); Assert.IsTrue(caller.activeSelf);
        }
        [Test]
        public void FullscreenPreference_PersistsBothValues()
        {
            DisplaySettings.SetFullscreen(false);
            Assert.IsFalse(DisplaySettings.Fullscreen);
            DisplaySettings.SetFullscreen(true);
            Assert.IsTrue(DisplaySettings.Fullscreen);
            Assert.AreEqual(1, PlayerPrefs.GetInt(DisplaySettings.FullscreenKey));
        }
    }
}
