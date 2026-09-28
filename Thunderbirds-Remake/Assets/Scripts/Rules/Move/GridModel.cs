using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Spatial queries over the live state. Reads current positions and ghost flags on every query,
    /// so movement, falling and respawning cannot leave a stale occupancy cache.
    /// Each fresh SimulationState (including Restart) owns its own model.
    /// </summary>
    public sealed class GridModel
    {
        private readonly IReadOnlySimulationState _state;
        public GridModel(IReadOnlySimulationState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public int Width => _state.Width;
        public int Height => _state.Height;
        public bool IsWall(GridPos p) => _state.IsWall(p);

        /// <summary>The block covering <paramref name="p"/>, or null.</summary>
        public BlockState BlockAt(GridPos p)
        {
            foreach (var block in _state.Blocks)
                foreach (var offset in block.Cells)
                    if (block.Position + offset == p) return block;
            return null;
        }

        /// <summary>The solid ship covering <paramref name="p"/>, or null.</summary>
        public ShipState ShipAt(GridPos p)
        {
            foreach (var ship in _state.Ships)
            {
                if (ship.IsGhost) continue;
                var offset = p - ship.Position;
                if (offset.X >= 0 && offset.X < ship.Width && offset.Y >= 0 && offset.Y < ship.Height)
                    return ship;
            }
            return null;
        }

        /// <summary>Outside the level is a wall; ghosts do not occupy cells.</summary>
        public CellOccupant OccupantAt(GridPos p)
        {
            if (IsWall(p)) return new CellOccupant(CellKind.Wall, p);
            var block = BlockAt(p);
            if (block != null) return new CellOccupant(CellKind.Block, p, block: block);
            var ship = ShipAt(p);
            return ship != null
                ? new CellOccupant(CellKind.Ship, p, ship: ship)
                : new CellOccupant(CellKind.Empty, p);
        }

        public bool IsEmpty(GridPos p) => OccupantAt(p).Kind == CellKind.Empty;

        /// <summary>
        /// Direct supports below every exposed bottom edge, including higher edges of concave shapes.
        /// Each supporting block/ship occurs once; walls are returned per contact cell.
        /// Empty cells and the queried block itself are excluded. No transitive supports are included.
        /// </summary>
        public IReadOnlyList<CellOccupant> SupportsOf(BlockState block)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            var cells = new HashSet<GridPos>(CellsOf(block));
            var seenBlocks = new HashSet<BlockState>();
            var seenShips = new HashSet<ShipState>();
            var supports = new List<CellOccupant>();
            foreach (var cell in CellsOf(block))
            {
                var below = cell + Direction.Down.ToOffset();
                if (cells.Contains(below)) continue;
                var support = OccupantAt(below);
                if (support.Kind == CellKind.Empty) continue;
                if (support.Block != null && !seenBlocks.Add(support.Block)) continue;
                if (support.Ship != null && !seenShips.Add(support.Ship)) continue;
                supports.Add(support);
            }
            return supports;
        }

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

    public enum CellKind { Empty, Wall, Block, Ship }

    /// <summary>A cell lookup, or one direct support contact. Entity references expose shape and block weight.</summary>
    public readonly struct CellOccupant
    {
        public CellKind Kind { get; }
        public GridPos Position { get; }
        public BlockState Block { get; }
        public ShipState Ship { get; }

        internal CellOccupant(CellKind kind, GridPos position, BlockState block = null, ShipState ship = null)
        {
            Kind = kind;
            Position = position;
            Block = block;
            Ship = ship;
        }
    }
}
