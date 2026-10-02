using System.Collections.Generic;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Wall, block and background art: optional sprites in GameConfig that replace the flat colours.</summary>
    public class TileArtTests
    {
        [Test]
        public void WallVariant_IsStablePerCell_InRange_AndUsesEveryTile()
        {
            var seen = new HashSet<int>();
            for (var x = 0; x < 32; x++)
            for (var y = 0; y < 12; y++)
            {
                var variant = LevelView.WallVariant(x, y, 4);
                Assert.AreEqual(variant, LevelView.WallVariant(x, y, 4), "same cell, same tile after a rebuild");
                Assert.That(variant, Is.InRange(0, 3));
                seen.Add(variant);
            }
            Assert.AreEqual(4, seen.Count);
            Assert.AreEqual(0, LevelView.WallVariant(5, 7, 1), "a single tile is always tile 0");
        }

        [Test]
        public void WallVariant_DoesNotRepeatInLongRuns()
        {
            // A border wall is one long row: the same glyph tile many times in a row would look stamped.
            for (var y = 0; y < 10; y++)
            {
                var run = 1;
                for (var x = 1; x < 40; x++)
                {
                    run = LevelView.WallVariant(x, y, 4) == LevelView.WallVariant(x - 1, y, 4) ? run + 1 : 1;
                    Assert.Less(run, 5, $"row {y}, column {x}");
                }
            }
        }

        [Test]
        public void RealGameConfig_HasWallBlockAndBackgroundArt_EachOneCellInSize()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/Config/GameConfig.asset");
            Assert.AreEqual(4, config.wallTiles.Length);
            foreach (var tile in config.wallTiles)
            {
                Assert.IsNotNull(tile);
                Assert.AreEqual(1f, tile.bounds.size.x, 1e-3f);
                Assert.AreEqual(1f, tile.bounds.size.y, 1e-3f);
            }
            Assert.IsNotNull(config.blockCell);
            Assert.IsNotNull(config.backgroundTile);
        }

        [Test]
        public void BlockCell_TexturesOnlyTheFill_AndStillTintsItByColourClass()
        {
            var plain = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
            var textured = Sprite.Create(new Texture2D(64, 64), new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 100f);
            var cell = new GameObject("Cell").AddComponent<CellView>();
            try
            {
                cell.Initialize(plain, null, textured);
                cell.Configure(new GridPos(0, 0), new HashSet<GridPos> { new GridPos(0, 0) }, Color.cyan, Color.black);

                var fill = cell.transform.Find("Fill").GetComponent<SpriteRenderer>();
                Assert.AreSame(textured, fill.sprite);
                Assert.AreEqual(Color.cyan, fill.color, "tinted, so teal / yellow / red still read");
                // The fill is scaled to the cell whatever the texture's resolution (0.64 units here).
                Assert.AreEqual(0.96f, fill.sprite.bounds.size.x * fill.transform.localScale.x, 1e-3f);
                foreach (var part in cell.GetComponentsInChildren<SpriteRenderer>())
                    if (part != fill) Assert.AreSame(plain, part.sprite, part.name + " stays a flat colour");
            }
            finally
            {
                Object.DestroyImmediate(cell.gameObject);
                Object.DestroyImmediate(plain.texture); Object.DestroyImmediate(plain);
                Object.DestroyImmediate(textured.texture); Object.DestroyImmediate(textured);
            }
        }

        [Test]
        public void TexturedBlock_IsStone_WithItsColourClassOnTheEdge_AndGlowingInward()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var plain = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
            var stone = Sprite.Create(new Texture2D(64, 64), new Rect(0, 0, 64, 64), Vector2.one * 0.5f, 64f);
            var view = new GameObject("Block").AddComponent<BlockView>();
            try
            {
                config.blockCell = stone;
                // Two cells side by side: the shared side is a seam, the other three sides of each are outside edges.
                var block = new BlockState(new BlockId('a'), new GridPos(0, 0),
                    new[] { new GridPos(0, 0), new GridPos(1, 0) }, ColourClass.Heavy);
                view.Build(block, config, plain);
                var left = view.Cells[0].transform;

                Assert.AreEqual(config.blockStone, left.Find("Fill").GetComponent<SpriteRenderer>().color, "stone, not paint");
                Assert.AreEqual(config.blockHeavy, left.Find("Edge Up").GetComponent<SpriteRenderer>().color, "the class colour is on the edge");
                Assert.AreNotEqual(config.blockHeavy, left.Find("Edge Right").GetComponent<SpriteRenderer>().color, "the seam is not");

                var glowUp = left.Find("Glow Up").GetComponent<SpriteRenderer>();
                Assert.IsTrue(glowUp.enabled);
                Assert.AreEqual(config.blockHeavy.r, glowUp.color.r);
                Assert.AreEqual(config.blockGlow, glowUp.color.a, 1e-4f);
                Assert.IsFalse(left.Find("Glow Right").GetComponent<SpriteRenderer>().enabled, "no glow along a seam");
                Assert.Greater(glowUp.sortingOrder, left.Find("Fill").GetComponent<SpriteRenderer>().sortingOrder);
                Assert.Less(glowUp.sortingOrder, left.Find("Edge Up").GetComponent<SpriteRenderer>().sortingOrder);

                // The edge is as wide as the setting says.
                var edge = left.Find("Edge Up");
                Assert.AreEqual(config.blockEdgeWidth, edge.GetComponent<SpriteRenderer>().sprite.bounds.size.y * edge.localScale.y, 1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(plain.texture); Object.DestroyImmediate(plain);
                Object.DestroyImmediate(stone.texture); Object.DestroyImmediate(stone);
            }
        }

        [Test]
        public void FlatBlock_WithoutATexture_KeepsTheOldLook_ColourFillAndNoGlow()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var plain = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
            var view = new GameObject("Block").AddComponent<BlockView>();
            try
            {
                view.Build(new BlockState(new BlockId('a'), new GridPos(0, 0), new[] { new GridPos(0, 0) }, ColourClass.Light), config, plain);
                var cell = view.Cells[0].transform;
                Assert.AreEqual(config.blockLight, cell.Find("Fill").GetComponent<SpriteRenderer>().color);
                Assert.AreEqual(config.tombVoid, cell.Find("Edge Up").GetComponent<SpriteRenderer>().color);
                Assert.IsNull(cell.Find("Glow Up"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(plain.texture); Object.DestroyImmediate(plain);
            }
        }
    }
}
