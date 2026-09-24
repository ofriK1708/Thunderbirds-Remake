using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>A rigid block as views see it. Shape and weight never change; position does.</summary>
    public sealed class BlockState
    {
        public BlockId Id { get; }

        /// <summary>Cell offsets from <see cref="Position"/> (bottom-left of the bounding box).</summary>
        public IReadOnlyList<GridPos> Cells { get; }

        public int Weight => Cells.Count;
        public ColourClass Colour { get; }

        public GridPos Position { get; internal set; }

        public BlockState(BlockId id, GridPos position, IReadOnlyList<GridPos> cells, ColourClass colour)
        {
            Id = id;
            Position = position;
            Cells = cells;
            Colour = colour;
        }
    }
}
