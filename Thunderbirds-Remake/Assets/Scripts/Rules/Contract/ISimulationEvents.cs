using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>What views subscribe to: <c>sim.Events.ShipMoved += OnShipMoved;</c></summary>
    public interface ISimulationEvents
    {
        /// <summary>Every event raised since the last Restart, in order. For tests and debugging.</summary>
        IReadOnlyList<SimEvent> Log { get; }

        event Action<ShipMoved> ShipMoved;
        event Action<MoveRefused> MoveRefused;
        event Action<BlockMoved> BlockMoved;
        event Action<BlockFell> BlockFell;
        event Action<BlockLanded> BlockLanded;
        event Action<BlockReleased> BlockReleased;
        event Action<ShipStressed> ShipStressed;
        event Action<ShipRelieved> ShipRelieved;
        event Action<ShipCrushed> ShipCrushed;
        event Action<ShipRespawned> ShipRespawned;
        event Action<ActiveShipChanged> ActiveShipChanged;
        event Action<OxygenChanged> OxygenChanged;
        event Action<LevelComplete> LevelComplete;
        event Action<LevelFailed> LevelFailed;
    }
}
