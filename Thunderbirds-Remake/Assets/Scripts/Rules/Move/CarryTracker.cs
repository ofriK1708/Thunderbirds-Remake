using System.Collections.Generic;
using System.Linq;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Tick step 3 (GDD §3 Carrying): decides, for every block, whether a ship carries it.
    /// MoveResolver reads the result on the next step to move carried blocks with their ship.
    /// </summary>
    internal static class CarryTracker
    {
        public static void Update(SimulationState state)
        {
            var previous = new Dictionary<BlockState, ShipId?>(state.Blocks.Count);
            foreach (var block in state.Blocks)
            {
                previous[block] = block.CarriedBy;
                block.CarriedBy = null;
            }

            // Carrying climbs a stack one layer at a time: a block can only count as carried once what it
            // rests on does. Sweep until nothing new is carried — at most one sweep per stack layer.
            bool changed;
            do
            {
                changed = false;
                foreach (var block in state.Blocks)
                {
                    if (block.CarriedBy != null) continue;
                    var supports = state.Grid.SupportsOf(block);
                    foreach (var ship in state.Ships)
                    {
                        if (!IsCarriedBy(supports, ship.Id, previous[block] == ship.Id)) continue;
                        block.CarriedBy = ship.Id;
                        changed = true;
                        break;
                    }
                }
            } while (changed);
        }

        /// <summary>
        /// Is a block with these direct <paramref name="supports"/> carried by <paramref name="ship"/>?
        /// <paramref name="wasCarried"/> is true when this ship carried the block at the end of the last tick.
        /// </summary>
        internal static bool IsCarriedBy(IReadOnlyList<CellOccupant> supports, ShipId ship, bool wasCarried)
        {
            if (supports.Count == 0)
            {
                return false;
            }

            if (supports.All(supporter => BelongsTo(supporter, ship)))
            {
                return true;
            }

            if (supports.Any(supporter => BelongsTo(supporter, ship)))
            {
                return wasCarried;
            }
            return false;
        }

        /// <summary>True when this support contact is the ship itself or a block that ship already carries.</summary>
        internal static bool BelongsTo(CellOccupant support, ShipId ship) =>
            (support.Ship != null && support.Ship.Id == ship) ||
            (support.Block != null && support.Block.CarriedBy == ship);
    }
}