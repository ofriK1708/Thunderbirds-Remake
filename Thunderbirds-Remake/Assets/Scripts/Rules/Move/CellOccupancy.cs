using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// What occupies each cell right now: a wall, a ship or a block. A snapshot built from the state.
    /// STAND-IN for GridModel (#5): when it lands, MoveResolver switches to it and this class goes away.
    /// Keep the query names close to GridModel's so that swap is mechanical.
    /// </summary>
    internal sealed class CellOccupancy
    {
        private readonly IReadOnlySimulationState _state;
        private readonly Dictionary<GridPos, BlockState> _blocks = new Dictionary<GridPos, BlockState>();
        private readonly Dictionary<GridPos, ShipState> _ships = new Dictionary<GridPos, ShipState>();

        public CellOccupancy(IReadOnlySimulationState state)
        {
            _state = state;
            foreach (var block in state.Blocks)
                foreach (var cell in CellsOf(block))
                    _blocks[cell] = block;
            foreach (var ship in state.Ships)
            {
                if (ship.IsGhost) continue; // ghosts are not solid (GDD §3)
                foreach (var cell in CellsOf(ship))
                    _ships[cell] = ship;
            }
        }

        public bool IsWall(GridPos p) => _state.IsWall(p);

        /// <summary>The block covering <paramref name="p"/>, or null.</summary>
        public BlockState BlockAt(GridPos p) => _blocks.GetValueOrDefault(p);

        /// <summary>The solid ship covering <paramref name="p"/>, or null.</summary>
        public ShipState ShipAt(GridPos p) => _ships.GetValueOrDefault(p);

        /// <summary>Absolute cells of a block.</summary>
        public static List<GridPos> CellsOf(BlockState block)
        {
            var cells = new List<GridPos>(block.Cells.Count);
            foreach (var offset in block.Cells) cells.Add(block.Position + offset);
            return cells;
        }

        /// <summary>Absolute cells of a ship's rectangle.</summary>
        public static List<GridPos> CellsOf(ShipState ship)
        {
            var cells = new List<GridPos>(ship.Width * ship.Height);
            for (var dy = 0; dy < ship.Height; dy++)
            for (var dx = 0; dx < ship.Width; dx++)
                cells.Add(ship.Position + new GridPos(dx, dy));
            return cells;
        }
    }
}
