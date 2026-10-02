using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// Issue #18: the GameManager singleton. Scene loads themselves are exercised end to end by
    /// SandboxPreviewTests (menu → Game → menu in Play mode); these cover the state it carries across them.
    /// </summary>
    public class GameManagerTests
    {
        private readonly Dictionary<string, int?> _saved = new Dictionary<string, int?>();
        private LevelCatalog _catalog;
        private LevelData _first, _second;

        [SetUp]
        public void SetUp()
        {
            GameManager.ResetInstance();
            _saved.Clear();
            for (var i = 0; i < LevelProgress.LevelCount; i++)
            {
                var key = LevelProgress.Key(i);
                _saved[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : (int?)null;
                PlayerPrefs.DeleteKey(key);
            }

            _first = ScriptableObject.CreateInstance<LevelData>();
            _second = ScriptableObject.CreateInstance<LevelData>();
            _catalog = ScriptableObject.CreateInstance<LevelCatalog>();
            var serialized = new SerializedObject(_catalog);
            var list = serialized.FindProperty("levels");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = _first;
            list.GetArrayElementAtIndex(1).objectReferenceValue = _second;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            GameManager.ResetInstance();
            Object.DestroyImmediate(_catalog);
            Object.DestroyImmediate(_first);
            Object.DestroyImmediate(_second);
            foreach (var pair in _saved)
                if (pair.Value.HasValue) PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
                else PlayerPrefs.DeleteKey(pair.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void Instance_IsCreatedOnce_AndReused()
        {
            var manager = GameManager.Instance;
            Assert.IsNotNull(manager);
            Assert.AreSame(manager, GameManager.Instance);
            Assert.AreEqual(1, Resources.FindObjectsOfTypeAll<GameManager>().Length);
        }

        [Test]
        public void ResetInstance_DestroysIt_AndTheNextUseStartsClean()
        {
            var old = GameManager.Instance;
            Assert.IsTrue(old.SelectLevel(_catalog, 0));

            GameManager.ResetInstance();

            Assert.IsTrue(old == null, "the old object is destroyed");
            Assert.IsNull(GameManager.Instance.SelectedLevel);
            Assert.AreEqual(-1, GameManager.Instance.SelectedIndex);
        }

        [Test]
        public void SelectedLevel_IsHeldUntilTaken_ThenHandedOverOnce()
        {
            var manager = GameManager.Instance;
            Assert.IsTrue(manager.SelectLevel(_catalog, 0));
            Assert.AreSame(_first, manager.SelectedLevel);
            Assert.AreEqual(0, manager.SelectedIndex);

            Assert.AreSame(_first, manager.TakeSelection(out var index));
            Assert.AreEqual(0, index);
            Assert.IsNull(manager.TakeSelection(out index), "no selection = sandbox");
            Assert.AreEqual(-1, index);
        }

        [Test]
        public void LockedOrMissingLevel_IsRefused_AndKeepsThePreviousSelection()
        {
            var manager = GameManager.Instance;
            Assert.IsTrue(manager.SelectLevel(_catalog, 0));

            Assert.IsFalse(manager.SelectLevel(_catalog, 1), "L2 is locked until L1 is completed");
            Assert.IsFalse(manager.SelectLevel(_catalog, 4), "no such level in the catalog");
            Assert.IsFalse(manager.SelectLevel(null, 0));
            Assert.AreSame(_first, manager.SelectedLevel);
        }

        [Test]
        public void CompletingALevel_UnlocksTheNext_AndSurvivesANewManager()
        {
            Assert.IsFalse(GameManager.Instance.IsUnlocked(1));
            GameManager.Instance.CompleteLevel(0);

            GameManager.ResetInstance(); // as after quitting and relaunching: progress lives in PlayerPrefs

            Assert.IsTrue(GameManager.Instance.IsCompleted(0));
            Assert.IsTrue(GameManager.Instance.IsUnlocked(1));
            Assert.IsTrue(GameManager.Instance.SelectLevel(_catalog, 1));
            Assert.AreSame(_second, GameManager.Instance.SelectedLevel);
        }
    }
}
