using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// The one place events are raised, shared by Simulation and FakeSimulation so they behave alike.
    /// <see cref="Raise"/> logs and queues; <see cref="Flush"/> notifies listeners in raise order.
    /// The simulation flushes once at the end of each Tick, so views never see a half-updated grid.
    /// </summary>
    public sealed class EventHub : ISimulationEvents
    {
        private readonly List<SimEvent> _log = new List<SimEvent>();
        private readonly Queue<SimEvent> _pending = new Queue<SimEvent>();

        public IReadOnlyList<SimEvent> Log => _log;

        public event Action<ShipMoved> ShipMoved;
        public event Action<MoveRefused> MoveRefused;
        public event Action<BlockMoved> BlockMoved;
        public event Action<BlockFell> BlockFell;
        public event Action<BlockLanded> BlockLanded;
        public event Action<BlockReleased> BlockReleased;
        public event Action<ShipStressed> ShipStressed;
        public event Action<ShipRelieved> ShipRelieved;
        public event Action<ShipCrushed> ShipCrushed;
        public event Action<ShipRespawned> ShipRespawned;
        public event Action<ActiveShipChanged> ActiveShipChanged;
        public event Action<OxygenChanged> OxygenChanged;
        public event Action<LevelComplete> LevelComplete;
        public event Action<LevelFailed> LevelFailed;

        public void Raise(SimEvent e)
        {
            if (e == null) throw new ArgumentNullException(nameof(e));
            _log.Add(e);
            _pending.Enqueue(e);
        }

        public void Flush()
        {
            while (_pending.Count > 0)
                Dispatch(_pending.Dequeue());
        }

        /// <summary>Forget everything (Restart). Subscribers stay subscribed.</summary>
        public void Clear()
        {
            _log.Clear();
            _pending.Clear();
        }

        private void Dispatch(SimEvent e)
        {
            switch (e)
            {
                case ShipMoved x: ShipMoved?.Invoke(x); break;
                case MoveRefused x: MoveRefused?.Invoke(x); break;
                case BlockMoved x: BlockMoved?.Invoke(x); break;
                case BlockFell x: BlockFell?.Invoke(x); break;
                case BlockLanded x: BlockLanded?.Invoke(x); break;
                case BlockReleased x: BlockReleased?.Invoke(x); break;
                case ShipStressed x: ShipStressed?.Invoke(x); break;
                case ShipRelieved x: ShipRelieved?.Invoke(x); break;
                case ShipCrushed x: ShipCrushed?.Invoke(x); break;
                case ShipRespawned x: ShipRespawned?.Invoke(x); break;
                case ActiveShipChanged x: ActiveShipChanged?.Invoke(x); break;
                case OxygenChanged x: OxygenChanged?.Invoke(x); break;
                case LevelComplete x: LevelComplete?.Invoke(x); break;
                case LevelFailed x: LevelFailed?.Invoke(x); break;
                default: throw new ArgumentException($"No dispatch for {e.GetType().Name}", nameof(e));
            }
        }
    }
}
