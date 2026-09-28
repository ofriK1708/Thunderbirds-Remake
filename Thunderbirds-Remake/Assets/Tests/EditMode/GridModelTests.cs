using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    public class GridModelTests
    {
        private static BlockState Block(char id, int x, int y, params GridPos[] cells) =>
            new BlockState(new BlockId(id), new GridPos(x, y), cells, ColourClass.Teal);

        private static SimulationState State(bool[,] walls, BlockState[] blocks, params ShipState[] ships) =>
            new SimulationState(walls, ships, blocks, 3, 90f);

        [Test]
        public void Occupancy_RespectsActualShapeAndShipFootprint()
        {
            var block = Block('a', 2, 3, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1));
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(5, 1), new GridPos(0, 0));
            var walls = new bool[10, 8];
            walls[1, 1] = true;
            var grid = State(walls, new[] { block }, ship).Grid;

            Assert.AreEqual(10, grid.Width);
            Assert.AreEqual(8, grid.Height);
            Assert.AreEqual(CellKind.Wall, grid.OccupantAt(new GridPos(1, 1)).Kind);
            Assert.AreSame(block, grid.OccupantAt(new GridPos(3, 4)).Block);
            Assert.AreEqual(3, grid.BlockAt(new GridPos(2, 3)).Weight);
            Assert.IsTrue(grid.IsEmpty(new GridPos(3, 3)), "The hole in an L is empty.");
            Assert.AreSame(ship, grid.OccupantAt(new GridPos(6, 2)).Ship);
            Assert.IsNull(grid.ShipAt(new GridPos(7, 2)));
        }

        [TestCase(-1, 2)]
        [TestCase(10, 2)]
        [TestCase(2, -1)]
        [TestCase(2, 8)]
        public void OutsideLevel_IsSolid(int x, int y)
        {
            var grid = State(new bool[10, 8], new BlockState[0]).Grid;
            Assert.AreEqual(CellKind.Wall, grid.OccupantAt(new GridPos(x, y)).Kind);
            Assert.IsFalse(grid.IsEmpty(new GridPos(x, y)));
        }

        [Test]
        public void LShape_IsSupportedUnderItsHigherArm_WithoutSelfSupport()
        {
            // aa      The leg hangs freely; only the higher right-hand arm touches a ledge.
            // a#
            var block = Block('a', 2, 3, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1));
            var walls = new bool[8, 8];
            walls[3, 3] = true;
            var supports = State(walls, new[] { block }).Grid.SupportsOf(block);

            Assert.AreEqual(1, supports.Count);
            Assert.AreEqual(CellKind.Wall, supports[0].Kind);
            Assert.AreEqual(new GridPos(3, 3), supports[0].Position);
        }

        [Test]
        public void FloatingShape_IgnoresSelfSideAndCeilingContacts()
        {
            var block = Block('a', 2, 3, new GridPos(0, 0), new GridPos(0, 1), new GridPos(1, 1));
            var walls = new bool[8, 8];
            walls[1, 3] = true;
            walls[2, 5] = true;
            Assert.IsEmpty(State(walls, new[] { block }).Grid.SupportsOf(block));
        }

        [Test]
        public void Supports_ReturnDistinctEntitiesAndAllWallContacts()
        {
            var top = Block('a', 1, 3, Enumerable.Range(0, 8).Select(x => new GridPos(x, 0)).ToArray());
            var bottom = Block('b', 1, 2, new GridPos(0, 0), new GridPos(1, 0));
            var kestrel = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(3, 1), default);
            var atlas = new ShipState(ShipId.Atlas, 2, 2, new GridPos(5, 1), default);
            var walls = new bool[10, 8];
            walls[7, 2] = walls[8, 2] = true;
            var supports = State(walls, new[] { top, bottom }, kestrel, atlas).Grid.SupportsOf(top);

            Assert.AreEqual(5, supports.Count);
            CollectionAssert.AreEquivalent(new[] { bottom }, supports.Where(s => s.Block != null).Select(s => s.Block));
            CollectionAssert.AreEquivalent(new[] { kestrel, atlas }, supports.Where(s => s.Ship != null).Select(s => s.Ship));
            CollectionAssert.AreEquivalent(new[] { new GridPos(7, 2), new GridPos(8, 2) },
                supports.Where(s => s.Kind == CellKind.Wall).Select(s => s.Position));
        }

        [Test]
        public void Supports_AreDirect_NotTheWholeStack()
        {
            var top = Block('a', 2, 3, new GridPos(0, 0));
            var middle = Block('b', 2, 2, new GridPos(0, 0));
            var bottom = Block('c', 2, 1, new GridPos(0, 0));
            var grid = State(new bool[8, 8], new[] { top, middle, bottom }).Grid;
            Assert.AreSame(middle, grid.SupportsOf(top).Single().Block);
        }

        [Test]
        public void GhostAndPositionChanges_AreVisibleWithoutRebuilding()
        {
            var block = Block('a', 2, 3, new GridPos(0, 0));
            var ship = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(2, 1), default);
            var grid = State(new bool[10, 8], new[] { block }, ship).Grid;
            Assert.AreSame(ship, grid.SupportsOf(block).Single().Ship);

            ship.IsGhost = true;
            Assert.IsNull(grid.ShipAt(new GridPos(2, 1)));
            Assert.IsEmpty(grid.SupportsOf(block));
            ship.IsGhost = false;
            ship.Position = new GridPos(5, 1);
            Assert.IsTrue(grid.IsEmpty(new GridPos(2, 1)));
            Assert.AreSame(ship, grid.ShipAt(new GridPos(5, 1)));
            block.Position = new GridPos(5, 3);
            Assert.IsTrue(grid.IsEmpty(new GridPos(2, 3)));
            Assert.AreSame(block, grid.BlockAt(new GridPos(5, 3)));
            Assert.AreSame(ship, grid.SupportsOf(block).Single().Ship);
        }

        [Test]
        public void BottomBoundary_SupportsABlock()
        {
            var block = Block('a', 2, 0, new GridPos(0, 0));
            var support = State(new bool[8, 8], new[] { block }).Grid.SupportsOf(block).Single();
            Assert.AreEqual(CellKind.Wall, support.Kind);
            Assert.AreEqual(new GridPos(2, -1), support.Position);
        }

        private const string LevelText =
            "##############\n#AAAA....2222#\n#AAAA....2222#\n#............#\n#KKa.......11#\n#KKa.......11#\n##############";

        [Test]
        public void Push_UpdatesTheExistingGridImmediately()
        {
            var config = new SimulationConfig();
            var state = LevelParser.Parse(LevelText, 8).CreateState(config, 3, 90f);
            var grid = state.Grid;
            var block = state.GetBlock(new BlockId('a'));
            Assert.IsTrue(MoveResolver.TryMove(state, ShipId.Kestrel, Direction.Right, config).Accepted);
            Assert.AreSame(block, grid.BlockAt(new GridPos(4, 1)));
            Assert.IsNull(grid.BlockAt(new GridPos(3, 1)));
            Assert.AreSame(state.GetShip(ShipId.Kestrel), grid.ShipAt(new GridPos(3, 1)));
            Assert.IsTrue(grid.IsEmpty(new GridPos(1, 1)));
        }

        [Test]
        public void Restart_RebuildsIndependentGridWithoutChangingDefinition()
        {
            var config = new SimulationConfig();
            var level = LevelParser.Parse(LevelText, 8);
            var simulation = new Simulation(() => level.CreateState(config, 3, 90f), config);
            var first = (SimulationState)simulation.State;
            MoveResolver.TryMove(first, ShipId.Kestrel, Direction.Right, config);
            simulation.Restart();
            var second = (SimulationState)simulation.State;

            Assert.AreNotSame(first.Grid, second.Grid);
            Assert.AreNotSame(first.GetBlock(new BlockId('a')), second.GetBlock(new BlockId('a')));
            Assert.AreEqual(new GridPos(3, 1), level.Blocks.Single().Position);
            Assert.AreSame(second.GetBlock(new BlockId('a')), second.Grid.BlockAt(new GridPos(3, 1)));
            Assert.AreEqual(level.KestrelStart, second.GetShip(ShipId.Kestrel).Position);
            Assert.AreEqual(new GridPos(4, 1), first.GetBlock(new BlockId('a')).Position);
        }
    }
}
