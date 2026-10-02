using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// Issue #25: every level in the project must pass the parser's validator with the capacities in the real
    /// GameConfig, so retuning a ShipConfig can never silently break a level. Covers every LevelData asset,
    /// not just the catalog, so a level that was never added to the catalog is checked too.
    /// </summary>
    public class LevelDataValidationTests
    {
        private static IEnumerable<TestCaseData> AllLevels()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(LevelData)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                yield return new TestCaseData(path).SetName("Level_IsValid({0})");
            }
        }

        [TestCaseSource(nameof(AllLevels))]
        public void Level_IsValid(string path)
        {
            var level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            var error = Validate(level, MaxBlockWeight());
            if (error != null) Assert.Fail($"{path}\n{error}");
        }

        [Test]
        public void Catalog_HasNoEmptySlots()
        {
            foreach (var catalog in LoadAll<LevelCatalog>())
                for (var i = 0; i < catalog.Count; i++)
                    Assert.IsNotNull(catalog.Get(i), $"{AssetDatabase.GetAssetPath(catalog)}: slot {i} is empty");
        }

        [Test]
        public void BrokenLevel_ReportsItsNameAndEveryError()
        {
            var level = ScriptableObject.CreateInstance<LevelData>();
            try
            {
                level.displayName = "Broken";
                level.grid = "#####\n#...#\n#####"; // no ships, no docks

                var error = Validate(level, 8);

                StringAssert.StartsWith("Broken:", error);
                StringAssert.Contains("Missing", error);
                StringAssert.Contains("'K'", error);
                StringAssert.Contains("'A'", error);
            }
            finally
            {
                Object.DestroyImmediate(level);
            }
        }

        /// <summary>The level's name and every parser error, or null if the level is playable.</summary>
        internal static string Validate(LevelData level, int maxBlockWeight)
        {
            try
            {
                LevelParser.Parse(level.grid, maxBlockWeight);
                return null;
            }
            catch (LevelParseException e)
            {
                return $"{level.displayName}: " + string.Join("; ", e.Errors);
            }
        }

        /// <summary>The same capacity LevelView passes to the parser: the heaviest block Atlas can push.</summary>
        private static int MaxBlockWeight()
        {
            var configs = LoadAll<GameConfig>().ToList();
            Assert.AreEqual(1, configs.Count, "expected exactly one GameConfig asset");
            Assert.IsNotNull(configs[0].atlas, "GameConfig has no Atlas ShipConfig");
            return configs[0].atlas.pushCapacity;
        }

        private static IEnumerable<T> LoadAll<T>() where T : Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name)
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)));
    }
}
