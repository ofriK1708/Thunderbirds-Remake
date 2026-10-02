using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// A refused push tells the player why: the whole chain shows the colour of its total weight
    /// (red when no ship could move it) and the message only says "try Atlas" when Atlas could.
    /// </summary>
    public class RefusalFeedbackTests
    {
        private GameConfig _config;
        private Sprite _plain, _stone;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _plain = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 4f);
            _stone = Sprite.Create(new Texture2D(8, 8), new Rect(0, 0, 8, 8), Vector2.one * 0.5f, 8f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var sprite in new[] { _plain, _stone })
            {
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }
            Object.DestroyImmediate(_config);
        }

        private BlockView NewBlock(ColourClass colour)
        {
            var view = new GameObject("Block").AddComponent<BlockView>();
            view.Build(new BlockState(new BlockId('a'), new GridPos(0, 0), new[] { new GridPos(0, 0) }, colour), _config, _plain);
            return view;
        }

        private static Color Part(BlockView view, string name) =>
            view.Cells[0].transform.Find(name).GetComponent<SpriteRenderer>().color;

        // ---- the rules already report it; these pin that down ----

        private static Simulation Corridor(string row)
        {
            // Two identical rows, because ships and these blocks are 2 cells high.
            var grid = string.Join("\n", new string('#', row.Length), row, row, new string('#', row.Length));
            var config = new SimulationConfig();
            var definition = LevelParser.Parse(grid, config.AtlasPushCapacity);
            return new Simulation(() => definition.CreateState(config, 3, 90f), config);
        }

        private static MoveRefused PushRight(Simulation sim)
        {
            sim.SetHeldDirection(Direction.Right);
            sim.Tick(0.2f);
            sim.ClearInput();
            return sim.Events.Log.OfType<MoveRefused>().Single();
        }

        [Test]
        public void ChainTooHeavyForAnyShip_IsReportedAsTooHeavyClass_WithEveryBlockOfTheChain()
        {
            // Light block (4 cells) + heavy block (8) = 12: more than Atlas (8) can push.
            var refused = PushRight(Corridor("#KKaabbbb..AAAA.11.2222#"));
            Assert.AreEqual(RefuseReason.TooHeavy, refused.Reason);
            Assert.AreEqual(ColourClass.TooHeavy, refused.ChainColour);
            Assert.AreEqual(2, refused.Chain.Count);
        }

        [Test]
        public void ChainOnlyAtlasCouldPush_IsReportedAsHeavyClass()
        {
            var refused = PushRight(Corridor("#KKbbbb....AAAA.11.2222#")); // one 8-cell block
            Assert.AreEqual(RefuseReason.TooHeavy, refused.Reason);
            Assert.AreEqual(ColourClass.Heavy, refused.ChainColour);
        }

        // ---- what the player is told ----

        [Test]
        public void Message_OnlySuggestsAtlas_WhenAtlasCouldPushIt()
        {
            StringAssert.Contains("Try Atlas", LevelController.RefusalMessage(ShipId.Kestrel, RefuseReason.TooHeavy, ColourClass.Heavy));
            var none = LevelController.RefusalMessage(ShipId.Kestrel, RefuseReason.TooHeavy, ColourClass.TooHeavy);
            StringAssert.Contains("either ship", none);
            StringAssert.DoesNotContain("Try Atlas", none);
            StringAssert.DoesNotContain("Try", LevelController.RefusalMessage(ShipId.Atlas, RefuseReason.TooHeavy, ColourClass.TooHeavy));
            StringAssert.Contains("Blocked", LevelController.RefusalMessage(ShipId.Kestrel, RefuseReason.Blocked, ColourClass.Light));
            StringAssert.Contains("falling block", LevelController.RefusalMessage(ShipId.Kestrel, RefuseReason.FallWonTie, ColourClass.Light));
        }

        // ---- what the player sees ----

        [Test]
        public void TexturedBlock_FlashesTheChainColourOnItsEdgeAndGlow_ThenGoesBack()
        {
            _config.blockCell = _stone;
            var view = NewBlock(ColourClass.Light);
            try
            {
                Assert.AreEqual(_config.blockLight, Part(view, "Edge Up"));

                view.Flash(_config.blockRed, 1f);
                Assert.IsTrue(view.IsFlashing);
                Assert.AreEqual(_config.blockRed, Part(view, "Edge Up"), "a purple block in a too-heavy chain turns red");
                Assert.AreEqual(_config.blockRed.r, Part(view, "Glow Up").r);
                Assert.AreEqual(_config.blockStone, Part(view, "Fill"), "the stone itself is not repainted");

                view.Advance(0.9f);
                Assert.AreEqual(_config.blockRed, Part(view, "Edge Up"));
                view.Advance(0.2f);
                Assert.IsFalse(view.IsFlashing);
                Assert.AreEqual(_config.blockLight, Part(view, "Edge Up"), "back to its own class");
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void FlatBlock_FlashesItsFill()
        {
            var view = NewBlock(ColourClass.Heavy);
            try
            {
                view.Flash(_config.blockRed, 0.5f);
                Assert.AreEqual(_config.blockRed, Part(view, "Fill"));
                view.Advance(0.6f);
                Assert.AreEqual(_config.blockHeavy, Part(view, "Fill"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Rebuild_CancelsAFlash()
        {
            var view = NewBlock(ColourClass.Light);
            try
            {
                view.Flash(_config.blockRed, 5f);
                view.Build(new BlockState(new BlockId('a'), new GridPos(0, 0), new[] { new GridPos(0, 0) }, ColourClass.Light), _config, _plain);
                Assert.IsFalse(view.IsFlashing, "Restart must not leave a block red");
                Assert.AreEqual(_config.blockLight, Part(view, "Fill"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }
    }
}
