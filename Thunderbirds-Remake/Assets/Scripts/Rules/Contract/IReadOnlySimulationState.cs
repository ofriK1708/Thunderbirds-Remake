using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>What views may read at any time. Positions are bottom-left cells; y = 0 is the bottom row.</summary>
    public interface IReadOnlySimulationState
    {
        int Width { get; }
        int Height { get; }

        /// <summary>True for wall cells; everything outside the grid also counts as wall.</summary>
        bool IsWall(GridPos p);

        ShipId ActiveShip { get; }
        IReadOnlyList<ShipState> Ships { get; }
        IReadOnlyList<BlockState> Blocks { get; }

        int LivesLeft { get; }
        float OxygenRemaining { get; }
        float OxygenTotal { get; }
        SimStatus Status { get; }

        ShipState GetShip(ShipId id);
        BlockState GetBlock(BlockId id);
    }
}
