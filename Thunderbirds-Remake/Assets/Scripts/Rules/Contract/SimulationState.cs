using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// The concrete state behind <see cref="IReadOnlySimulationState"/>. Built fresh on every Restart,
    /// by the level parser for the real Simulation or by hand for FakeSimulation.
    /// </summary>
    public sealed class SimulationState : IReadOnlySimulationState
    {
        private readonly bool[,] _walls;
        private readonly List<ShipState> _ships;
        private readonly List<BlockState> _blocks;

        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<ShipState> Ships => _ships;
        public IReadOnlyList<BlockState> Blocks => _blocks;

        public ShipId ActiveShip { get; internal set; }
        public int LivesLeft { get; internal set; }
        public float OxygenRemaining { get; internal set; }
        public float OxygenTotal { get; }
        public SimStatus Status { get; internal set; }

        /// <param name="walls">walls[x, y], y = 0 is the bottom row.</param>
        public SimulationState(bool[,] walls, IEnumerable<ShipState> ships, IEnumerable<BlockState> blocks,
            int lives, float oxygenSeconds, ShipId activeShip = ShipId.Kestrel)
        {
            _walls = walls ?? throw new ArgumentNullException(nameof(walls));
            Width = walls.GetLength(0);
            Height = walls.GetLength(1);
            _ships = new List<ShipState>(ships);
            _blocks = new List<BlockState>(blocks);
            LivesLeft = lives;
            OxygenTotal = oxygenSeconds;
            OxygenRemaining = oxygenSeconds;
            ActiveShip = activeShip;
            Status = SimStatus.Playing;
        }

        public bool IsWall(GridPos p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height) return true;
            return _walls[p.X, p.Y];
        }

        public ShipState GetShip(ShipId id)
        {
            foreach (var ship in _ships)
                if (ship.Id == id) return ship;
            throw new KeyNotFoundException($"No ship {id}");
        }

        public BlockState GetBlock(BlockId id)
        {
            foreach (var block in _blocks)
                if (block.Id == id) return block;
            throw new KeyNotFoundException($"No {id}");
        }
    }
}
