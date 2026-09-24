using Thunderbirds.Rules;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Small hand-built states for tests that don't need the level parser.</summary>
    public static class TestStates
    {
        public static readonly BlockId BlockB = new BlockId('b');

        /// <summary>10 x 5 room, walls on the border, both ships on the floor and one 1-cell block.</summary>
        public static SimulationState SmallRoom()
        {
            var walls = new bool[10, 5];
            for (var x = 0; x < 10; x++) { walls[x, 0] = true; walls[x, 4] = true; }
            for (var y = 0; y < 5; y++) { walls[0, y] = true; walls[9, y] = true; }

            var kestrel = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), new GridPos(7, 1));
            var atlas = new ShipState(ShipId.Atlas, 4, 2, new GridPos(3, 1), new GridPos(5, 2));
            var block = new BlockState(BlockB, new GridPos(8, 1), new[] { new GridPos(0, 0) }, ColourClass.Teal);

            return new SimulationState(walls, new[] { kestrel, atlas }, new[] { block }, lives: 3, oxygenSeconds: 90f);
        }
    }
}
