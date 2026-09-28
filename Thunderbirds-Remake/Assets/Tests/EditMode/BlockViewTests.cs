using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    public class BlockViewTests
    {
        private GameObject _root;
        private GameConfig _config;
        private Texture2D _texture;
        private Sprite _sprite;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Block view tests");
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _texture = new Texture2D(1, 1);
            _texture.SetPixel(0, 0, Color.white);
            _texture.Apply();
            _sprite = Sprite.Create(_texture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f, 1);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_sprite);
            Object.DestroyImmediate(_texture);
            Object.DestroyImmediate(_config);
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)] [TestCase(7)]
        [TestCase(8)] [TestCase(9)] [TestCase(10)] [TestCase(11)]
        [TestCase(12)] [TestCase(13)] [TestCase(14)] [TestCase(15)]
        public void EveryNeighbourMask_UsesLightSeamsInsideAndDarkBordersOutside(int mask)
        {
            var steps = new[] { new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0) };
            var shape = new HashSet<GridPos> { new GridPos(0, 0) };
            for (var i = 0; i < 4; i++) if ((mask & (1 << i)) != 0) shape.Add(steps[i]);
            var cell = _root.AddComponent<CellView>();
            cell.Initialize(_sprite, null);
            cell.Configure(new GridPos(0, 0), shape, _config.blockTeal, _config.tombVoid);
            Assert.AreEqual((CellNeighbours)mask, cell.Neighbours);
            for (var i = 0; i < 4; i++)
            {
                var edge = cell.transform.Find("Edge " + (CellNeighbours)(1 << i)).GetComponent<SpriteRenderer>();
                if ((mask & (1 << i)) == 0) Assert.AreEqual(_config.tombVoid, edge.color);
                else
                {
                    Assert.Greater(edge.color.grayscale, _config.blockTeal.grayscale);
                    Assert.Less(Mathf.Min(edge.bounds.size.x, edge.bounds.size.y), 0.02f);
                }
            }
        }

        [TestCase(1, 1, 1)] [TestCase(1, -1, 2)]
        [TestCase(-1, -1, 4)] [TestCase(-1, 1, 8)]
        public void InwardCorner_IsFilledForEveryRotation_AndRemovedForSolidSquare(int x, int y, int corner)
        {
            var shape = new HashSet<GridPos> { new GridPos(0, 0), new GridPos(x, 0), new GridPos(0, y) };
            var cell = _root.AddComponent<CellView>();
            cell.Initialize(_sprite, null);
            cell.Configure(new GridPos(0, 0), shape, _config.blockTeal, _config.tombVoid);
            Assert.AreEqual(corner, cell.InnerCorners);
            Assert.AreEqual(1, cell.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("Inner corner") && r.enabled));
            shape.Add(new GridPos(x, y));
            cell.Configure(new GridPos(0, 0), shape, _config.blockTeal, _config.tombVoid);
            Assert.AreEqual(0, cell.InnerCorners);
            Assert.IsFalse(cell.GetComponentsInChildren<SpriteRenderer>().Any(r => r.name.StartsWith("Inner corner") && r.enabled));
        }

        [Test]
        public void AdjacentSameColourBlocks_KeepTwoSeparatedBorders_WhileOneBlockHasASeam()
        {
            var a = Build(new[] { new GridPos(0, 0) });
            var b = Build(new[] { new GridPos(0, 0) });
            b.transform.localPosition = Vector3.right;
            Assert.AreEqual(CellNeighbours.None, a.Cells[0].Neighbours);
            Assert.AreEqual(CellNeighbours.None, b.Cells[0].Neighbours);
            var right = a.Cells[0].transform.Find("Edge Right").GetComponent<SpriteRenderer>();
            var left = b.Cells[0].transform.Find("Edge Left").GetComponent<SpriteRenderer>();
            Assert.AreEqual(_config.tombVoid, right.color);
            Assert.AreEqual(_config.tombVoid, left.color);
            Assert.Less(right.bounds.max.x, left.bounds.min.x, "Separate outlines have a visible gap.");
            var joined = Build(new[] { new GridPos(0, 0), new GridPos(1, 0) });
            Assert.AreEqual(CellNeighbours.Right, joined.Cells[0].Neighbours);
            Assert.AreEqual(CellNeighbours.Left, joined.Cells[1].Neighbours);
            var seam = joined.Cells[0].transform.Find("Edge Right").GetComponent<SpriteRenderer>();
            var outline = joined.Cells[0].transform.Find("Edge Up").GetComponent<SpriteRenderer>();
            Assert.Greater(outline.sortingOrder, seam.sortingOrder, "Seams must not cut through the outside outline.");
        }

        [TestCase(ColourClass.Teal)] [TestCase(ColourClass.Yellow)] [TestCase(ColourClass.Red)]
        public void ModelColourClass_UsesConfiguredPalette(ColourClass colour)
        {
            _config.blockTeal = Color.cyan;
            _config.blockYellow = Color.magenta;
            _config.blockRed = Color.red;
            var view = Build(new[] { new GridPos(0, 0) }, colour);
            Assert.AreEqual(_config.ColourOf(colour), view.Cells[0].transform.Find("Fill").GetComponent<SpriteRenderer>().color);
        }

        [Test]
        public void Rebuild_ReusesCells_ResetsTopologyColourAndPosition()
        {
            var view = Build(new[] { new GridPos(0, 0), new GridPos(1, 0), new GridPos(0, 1) });
            var original = view.Cells.Select(c => c.GetInstanceID()).ToArray();
            var model = new BlockState(new BlockId('b'), new GridPos(4, 6), new[] { new GridPos(0, 0) }, ColourClass.Yellow);
            view.Build(model, _config, _sprite);
            Assert.AreEqual(3, view.AllocatedCells);
            Assert.Contains(view.Cells[0].GetInstanceID(), original);
            Assert.AreEqual(1, view.GetComponentsInChildren<CellView>().Length);
            Assert.AreEqual(CellNeighbours.None, view.Cells[0].Neighbours);
            Assert.AreEqual(0, view.Cells[0].InnerCorners);
            Assert.AreEqual(new Vector3(4, 6, 0), view.transform.localPosition);
            Assert.AreEqual(_config.blockYellow, view.Cells[0].transform.Find("Fill").GetComponent<SpriteRenderer>().color);
            view.Clear();
            Assert.AreEqual(0, view.GetComponentsInChildren<CellView>().Length);
        }

        private BlockView Build(GridPos[] cells, ColourClass colour = ColourClass.Teal)
        {
            var view = new GameObject("Block").AddComponent<BlockView>();
            view.transform.SetParent(_root.transform, false);
            view.Build(new BlockState(new BlockId('a'), new GridPos(0, 0), cells, colour), _config, _sprite);
            return view;
        }
    }
}
