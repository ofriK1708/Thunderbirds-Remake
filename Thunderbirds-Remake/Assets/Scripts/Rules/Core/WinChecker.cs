namespace Thunderbirds.Rules
{
    /// <summary>Both solid ships must exactly occupy their own docks.</summary>
    internal static class WinChecker
    {
        public static bool IsComplete(IReadOnlySimulationState state)
        {
            var kestrelDocked = false;
            var atlasDocked = false;
            foreach (var ship in state.Ships)
            {
                if (ship.IsGhost || ship.Position != ship.Dock) continue;
                if (ship.Id == ShipId.Kestrel) kestrelDocked = true;
                if (ship.Id == ShipId.Atlas) atlasDocked = true;
            }
            return kestrelDocked && atlasDocked;
        }
    }
}
