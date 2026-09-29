using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>Tick stage 4: full supported weight and one independent crush countdown per ship.</summary>
    internal sealed class LoadTracker
    {
        private readonly Dictionary<ShipId, CountdownTimer> _timers = new Dictionary<ShipId, CountdownTimer>();

        public void Tick(SimulationState state, SimulationConfig config, float dt, EventHub events)
        {
            foreach (var ship in state.Ships) ship.Load = 0;
            foreach (var block in state.Blocks)
            {
                var roots = SupportRoots(state.Grid, block);
                // A static support anywhere beneath this block takes its load. Unsupported branches
                // cannot transmit load either. A bridge supported only by both ships loads each fully.
                if (roots == 0 || (roots & 12) != 0) continue;
                foreach (var ship in state.Ships)
                    if (!ship.IsGhost && (roots & ShipBit(ship.Id)) != 0) ship.Load += block.Weight;
            }

            foreach (var ship in state.Ships)
            {
                if (!_timers.TryGetValue(ship.Id, out var timer))
                    _timers.Add(ship.Id, timer = new CountdownTimer());
                if (ship.IsGhost || ship.Load <= config.LoadCapacity(ship.Id))
                {
                    if (ship.IsStressed && !ship.IsGhost) events.Raise(new ShipRelieved(ship.Id));
                    ship.IsStressed = false;
                    ship.CrushSecondsLeft = 0;
                    timer.Reset();
                }
                else if (!ship.IsStressed)
                {
                    ship.IsStressed = true;
                    timer.Start(config.CrushGraceSeconds);
                    ship.CrushSecondsLeft = timer.Remaining;
                    events.Raise(new ShipStressed(ship.Id, ship.Load, timer.Remaining));
                    // Load may have landed during this tick: grant the full reaction window.
                }
                else
                {
                    timer.Tick(dt);
                    ship.CrushSecondsLeft = timer.Remaining;
                }
            }
        }

        public void Reset() => _timers.Clear();

        private static int ShipBit(ShipId ship) => ship == ShipId.Kestrel ? 1 : 2;

        private static int SupportRoots(GridModel grid, BlockState block)
        {
            var roots = 0;
            var visited = new HashSet<BlockId>();
            var pending = new Stack<BlockState>();
            pending.Push(block);
            while (pending.Count > 0)
            {
                var current = pending.Pop();
                if (!visited.Add(current.Id)) continue; // Shared support paths count once; cycles terminate.
                var supports = grid.SupportsOf(current);
                if (supports.Count == 0) roots |= 8;
                foreach (var support in supports)
                {
                    if (support.Kind == CellKind.Wall) roots |= 4;
                    else if (support.Ship != null) roots |= ShipBit(support.Ship.Id);
                    else if (support.Block != null) pending.Push(support.Block);
                }
            }
            return roots;
        }
    }
}
