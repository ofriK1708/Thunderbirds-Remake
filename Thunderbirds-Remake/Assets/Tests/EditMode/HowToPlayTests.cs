using NUnit.Framework;
using TMPro;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Issue #20: the How to Play screen reads its numbers from config, never from hard-coded text.</summary>
    public class HowToPlayTests
    {
        private const string PrefabPath = "Assets/Prefabs/HowToPlayPanel.prefab";
        private ShipConfig _kestrel, _atlas;

        [SetUp]
        public void SetUp()
        {
            _kestrel = Ship("Kestrel", push: 3, load: 5);
            _atlas = Ship("Atlas", push: 7, load: 9);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_kestrel);
            Object.DestroyImmediate(_atlas);
        }

        private static ShipConfig Ship(string name, int push, int load)
        {
            var ship = ScriptableObject.CreateInstance<ShipConfig>();
            ship.displayName = name;
            ship.pushCapacity = push;
            ship.loadCapacity = load;
            return ship;
        }

        [Test]
        public void ShipsCard_ShowsEachShipsCapacitiesAndSpeed()
        {
            var text = HowToPlayText.Ships(_kestrel, _atlas, 2f);
            StringAssert.Contains("KESTREL", text);
            StringAssert.Contains("Pushes 3, carries 5 cells.", text);
            StringAssert.Contains("Pushes 7, carries 9 cells.", text);
            StringAssert.Contains("2x as fast", text);
            StringAssert.Contains("same speed", HowToPlayText.Ships(_kestrel, _atlas, 1f));
            StringAssert.Contains("2x slower", HowToPlayText.Ships(_kestrel, _atlas, 0.5f));
        }

        [Test]
        public void BlocksCard_ColourThresholdsFollowPushCapacities()
        {
            var text = HowToPlayText.Blocks(_kestrel, _atlas, Color.magenta, Color.blue, Color.red);
            StringAssert.Contains("up to 3: either ship", text);
            StringAssert.Contains("up to 7: Atlas only", text);
            StringAssert.Contains("over 7: no ship", text);
            StringAssert.Contains("<color=#FF00FF>LIGHT</color>", text, "each class word is in its configured colour");
            StringAssert.Contains("<color=#0000FF>HEAVY</color>", text);
        }

        [Test]
        public void SurvivalCard_ShowsGraceAndLives()
        {
            var text = HowToPlayText.Survival(2.5f, 3, 4f);
            StringAssert.Contains("2.5 s: release the load", text);
            StringAssert.Contains("one of your 3 lives", text);
            StringAssert.Contains("ghost for 4 s", text);
            StringAssert.Contains("one of your 1 life", HowToPlayText.Survival(3f, 1, 3f));
        }

        [Test]
        public void Prefab_IsWiredToTheRealConfigAndInputActions()
        {
            var view = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<HowToPlayView>();
            var serialized = new SerializedObject(view);
            var config = serialized.FindProperty("config").objectReferenceValue as GameConfig;

            Assert.IsNotNull(config, "config");
            Assert.IsNotNull(serialized.FindProperty("gameplayActions").objectReferenceValue as InputActionAsset, "gameplayActions");
            foreach (var card in new[] { "controlsText", "shipsText", "blocksText", "survivalText" })
                Assert.IsNotNull(serialized.FindProperty(card).objectReferenceValue as TMP_Text, card);
        }

        /// <summary>Config-driven lines can grow (e.g. two-digit capacities); none may wrap or spill out of its card.</summary>
        [Test]
        public void GeneratedCards_FitWithoutWrapping_EvenWithTwoDigitValues()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var panel = Object.Instantiate(prefab);
            try
            {
                panel.SetActive(true); // saved inactive; TMP only lays out active text
                var serialized = new SerializedObject(panel.GetComponent<HowToPlayView>());
                _kestrel.pushCapacity = _kestrel.loadCapacity = 10;
                _atlas.pushCapacity = _atlas.loadCapacity = 16;
                AssertFits(serialized, "shipsText", HowToPlayText.Ships(_kestrel, _atlas, 1.5f));
                AssertFits(serialized, "blocksText", HowToPlayText.Blocks(_kestrel, _atlas, Color.magenta, Color.blue, Color.red));
                AssertFits(serialized, "survivalText", HowToPlayText.Survival(12.5f, 10, 12.5f));
            }
            finally
            {
                Object.DestroyImmediate(panel);
            }
        }

        private static void AssertFits(SerializedObject view, string property, string text)
        {
            var label = (TMP_Text)view.FindProperty(property).objectReferenceValue;
            label.text = text;
            label.ForceMeshUpdate(true, true);
            Assert.IsFalse(label.isTextOverflowing, $"{property} overflows its card");
            Assert.AreEqual(text.Split('\n').Length, label.textInfo.lineCount, $"{property} wraps a line");
        }
    }
}
