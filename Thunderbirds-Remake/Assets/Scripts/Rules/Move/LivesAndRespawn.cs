namespace Thunderbirds.Rules
{
    /// <summary>
    /// Tick stage 5 (GDD §3 Load, crush &amp; lives): a ship whose crush countdown ran out drops its load,
    /// costs a life and returns to its start cell. If that area is occupied it waits there as a ghost
    /// (not solid, not selectable) and control passes to the other ship; it materialises once the area is clear.
    /// </summary>
    internal static class LivesAndRespawn
    {
        public static void Resolve(SimulationState state, EventHub events)
        {
            foreach (var ship in state.Ships)
                if (ship.IsCrushDue) Crush(state, ship, events);

            if (state.LivesLeft <= 0) return; // the failure check ends the level this tick

            foreach (var ship in state.Ships)
                if (ship.IsGhost && IsStartClear(state, ship)) Materialise(state, ship, events);
        }

        private static void Crush(SimulationState state, ShipState ship, EventHub events)
        {
            foreach (var block in state.Blocks)
            {
                if (block.CarriedBy != ship.Id) continue;
                block.CarriedBy = null;
                events.Raise(new BlockReleased(block.Id, ship.Id));
            }

            state.LivesLeft--;
            ship.IsStressed = false;
            ship.CrushSecondsLeft = 0f;
            ship.Load = 0;
            events.Raise(new ShipCrushed(ship.Id, state.LivesLeft));
            if (state.LivesLeft <= 0) return; // no respawn after the last life

            ship.Position = ship.Start;
            ship.IsGhost = true;
            if (IsStartClear(state, ship)) return; // materialises straight away in Resolve

            events.Raise(new ShipRespawned(ship.Id, ship.Start, isGhost: true));
            if (state.ActiveShip != ship.Id) return;
            var other = state.GetShip(OtherOf(ship.Id));
            if (other.IsGhost) return; // both waiting: whichever materialises first takes control
            state.ActiveShip = other.Id;
            events.Raise(new ActiveShipChanged(other.Id, automatic: true));
        }

        private static void Materialise(SimulationState state, ShipState ship, EventHub events)
        {
            ship.IsGhost = false;
            events.Raise(new ShipRespawned(ship.Id, ship.Position, isGhost: false));
            if (state.ActiveShip == ship.Id || !state.GetShip(state.ActiveShip).IsGhost) return;
            state.ActiveShip = ship.Id;
            events.Raise(new ActiveShipChanged(ship.Id, automatic: true));
        }

        /// <summary>Every cell of the ship's footprint at its start is empty (a ghost does not block itself).</summary>
        private static bool IsStartClear(SimulationState state, ShipState ship)
        {
            foreach (var cell in GridModel.CellsOf(ship))
                if (!state.Grid.IsEmpty(cell)) return false;
            return true;
        }

        private static ShipId OtherOf(ShipId ship) => ship == ShipId.Kestrel ? ShipId.Atlas : ShipId.Kestrel;
    }
}
