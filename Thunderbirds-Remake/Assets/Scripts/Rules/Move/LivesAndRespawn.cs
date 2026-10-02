namespace Thunderbirds.Rules
{
    /// <summary>
    /// Tick stage 5 (GDD §3 Load, crush &amp; lives): a ship whose crush countdown ran out drops its load,
    /// costs a life and returns to its start cell as a ghost (not solid, not selectable). It stays a ghost for
    /// RespawnGhostSeconds, so the load that crushed it falls through instead of landing on it again, and then
    /// materialises as soon as its start area is clear. While it is a ghost, control passes to the other ship.
    /// </summary>
    internal static class LivesAndRespawn
    {
        public static void Resolve(SimulationState state, SimulationConfig config, float dt, EventHub events)
        {
            // Ghosts from earlier ticks count down first, so a ship crushed in this tick keeps its full time.
            foreach (var ship in state.Ships)
                if (ship.IsGhost && ship.GhostSecondsLeft > 0f)
                    ship.GhostSecondsLeft = System.Math.Max(0f, ship.GhostSecondsLeft - dt);

            foreach (var ship in state.Ships)
                if (ship.IsCrushDue) Crush(state, ship, config, events);

            if (state.LivesLeft <= 0) return; // the failure check ends the level this tick

            foreach (var ship in state.Ships)
                if (ship.IsGhost && ship.GhostSecondsLeft <= 0f && IsStartClear(state, ship))
                    Materialise(state, ship, events);
        }

        private static void Crush(SimulationState state, ShipState ship, SimulationConfig config, EventHub events)
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
            ship.GhostSecondsLeft = config.RespawnGhostSeconds;
            // No protection and a clear start: it materialises straight away in Resolve, with no ghost phase.
            if (ship.GhostSecondsLeft <= 0f && IsStartClear(state, ship)) return;

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
            ship.GhostSecondsLeft = 0f;
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
