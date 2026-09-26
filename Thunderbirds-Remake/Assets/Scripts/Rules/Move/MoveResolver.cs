using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>The outcome of one attempted ship step.</summary>
    internal readonly struct MoveResult
    {
        public readonly bool Accepted;
        public readonly RefuseReason Reason;               // meaningful only when refused
        public readonly IReadOnlyList<BlockState> Chain;   // blocks that moved, or would have
        public readonly int ChainWeight;

        public MoveResult(bool accepted, RefuseReason reason, IReadOnlyList<BlockState> chain, int chainWeight)
        {
            Accepted = accepted;
            Reason = reason;
            Chain = chain;
            ChainWeight = chainWeight;
        }
    }

    /// <summary>
    /// Validates and applies one ship step (GDD §3 Push). Sideways: the ship pushes a chain of blocks;
    /// the move is refused if the chain's full weight is over pushCapacity or its front is blocked.
    /// Up/down: lifting and carrying are #12 — until then any block in the way refuses the step.
    /// </summary>
    internal static class MoveResolver
    {
        public static MoveResult TryMove(SimulationState state, ShipId shipId, Direction dir, SimulationConfig config)
        {
            var ship = state.GetShip(shipId);
            var grid = new CellOccupancy(state);
            var step = dir.ToOffset();

            var sideways = dir == Direction.Left || dir == Direction.Right;
            var chain = sideways ? CollectChain(ship, dir, grid) : new List<BlockState>();
            var weight = 0;
            foreach (var block in chain) weight += block.Weight;

            // Everything that moves must land on a cell that is free or is itself moving away.
            var moving = new HashSet<BlockState>(chain);
            var movers = CellOccupancy.CellsOf(ship);
            foreach (var block in chain) movers.AddRange(CellOccupancy.CellsOf(block));
            foreach (var cell in movers)
            {
                var next = cell + step;
                if (grid.IsWall(next)) return Refused(RefuseReason.Blocked, chain, weight);

                var otherShip = grid.ShipAt(next);
                if (otherShip != null && otherShip != ship) return Refused(RefuseReason.Blocked, chain, weight);

                var block = grid.BlockAt(next);
                if (block != null && !moving.Contains(block))
                    return Refused(RefuseReason.Blocked, chain, weight); // up/down into a block (until #12)
            }

            if (weight > config.PushCapacity(shipId)) return Refused(RefuseReason.TooHeavy, chain, weight);

            foreach (var block in chain) block.Position += step;
            ship.Position += step;
            return new MoveResult(true, default, chain, weight);
        }

        /// <summary>
        /// The push chain for a sideways step (GDD §3 Push): every block the move would displace, plus
        /// everything riding on those blocks — and, in turn, whatever those displace or carry.
        /// Walls and ships are ignored here; TryMove checks the chain's front afterwards.
        /// </summary>
        /// <returns>Each block once, in the order found.</returns>
        internal static List<BlockState> CollectChain(ShipState ship, Direction dir, CellOccupancy grid)
        {
            // A block joins the chain when it is
            //   1. in front (cell + dir) of the ship or of a block already in the chain, or
            //   2. riding on a chain block: it covers the cell directly above one of the chain block's cells.
            var chain = new List<BlockState>();
            var blocksIdsVisited = new HashSet<BlockId>();
            var movers = new Queue<GridPos>(CellOccupancy.CellsOf(ship));
            var step = dir.ToOffset();
            var stepUp = Direction.Up.ToOffset();
            while (movers.Count > 0)
            {
                var pos = movers.Dequeue();
                TryAddToChain(grid.BlockAt(pos + step), blocksIdsVisited, chain, movers);

                // Only block cells carry riders: ship cells return null here, so blocks on the ship's
                // roof stay out of the push chain (they are carried, #12).
                if (grid.BlockAt(pos) != null)
                    TryAddToChain(grid.BlockAt(pos + stepUp), blocksIdsVisited, chain, movers);
            }
            return chain;
        }

        /// <summary>Adds <paramref name="block"/> to the chain once and queues its cells for expansion.</summary>
        private static void TryAddToChain(BlockState block, HashSet<BlockId> blocksIdsVisited, List<BlockState> chain,
            Queue<GridPos> movers)
        {
            if (block == null || !blocksIdsVisited.Add(block.Id)) return;

            chain.Add(block);
            foreach (var blockPos in CellOccupancy.CellsOf(block))
                movers.Enqueue(blockPos);
        }

        private static MoveResult Refused(RefuseReason reason, List<BlockState> chain, int weight) =>
            new MoveResult(false, reason, chain, weight);
    }
}
