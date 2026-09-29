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
        public readonly int ChainWeight;                   // pushed / lifted weight (carried blocks excluded)
        public readonly IReadOnlyList<BlockState> Released; // carried blocks an obstacle left behind

        public MoveResult(bool accepted, RefuseReason reason, IReadOnlyList<BlockState> chain, int chainWeight,
            IReadOnlyList<BlockState> released = null)
        {
            Accepted = accepted;
            Reason = reason;
            Chain = chain;
            ChainWeight = chainWeight;
            Released = released ?? new BlockState[0];
        }
    }

    /// <summary>
    /// Validates and applies one ship step (GDD §3 Push, Lift, Carrying).
    /// Sideways: the ship pushes a chain of blocks, refused over pushCapacity or when the chain's front is blocked.
    /// Up: the ship lifts everything above it, refused over loadCapacity or into a ceiling.
    /// Sideways and down: blocks the ship carries come along; one whose path is blocked is released and stays.
    /// </summary>
    internal static class MoveResolver
    {
        public static MoveResult TryMove(SimulationState state, ShipId shipId, Direction dir, SimulationConfig config,
            ISet<GridPos> fallenCells = null)
        {
            var ship = state.GetShip(shipId);
            var grid = state.Grid;
            var step = dir.ToOffset();

            var carried = new HashSet<BlockState>();
            foreach (var block in state.Blocks)
                if (block.CarriedBy == shipId) carried.Add(block);

            // The blocks this ship displaces by force, and the capacity that limits them.
            List<BlockState> chain;
            int capacity;
            if (dir == Direction.Up)
            {
                chain = CollectChain(ship, dir, grid); // the whole stack above rises, carried or not
                capacity = config.LoadCapacity(shipId);
            }
            else if (dir == Direction.Down)
            {
                chain = new List<BlockState>(); // ships never push down
                capacity = int.MaxValue;
            }
            else
            {
                chain = CollectChain(ship, dir, grid, carried); // carried blocks travel, they aren't pushed
                capacity = config.PushCapacity(shipId);
            }
            var weight = 0;
            foreach (var block in chain) weight += block.Weight;

            // A fresh fall wins this tick, even when the ship could normally push that block away.
            var movers = GridModel.CellsOf(ship);
            if (fallenCells != null)
                foreach (var cell in movers)
                    if (fallenCells.Contains(cell + step)) return Refused(RefuseReason.FallWonTie, chain, weight);

            var moving = new HashSet<BlockState>(chain);
            var released = new List<BlockState>();
            if (dir != Direction.Up) CarryAlong(ship, step, grid, carried, moving, released);

            // Ship and forced chain must land on cells that are free or are themselves moving away.
            foreach (var block in chain) movers.AddRange(GridModel.CellsOf(block));
            foreach (var cell in movers)
            {
                var next = cell + step;
                if (grid.IsWall(next))
                    return Refused(dir == Direction.Up ? RefuseReason.Ceiling : RefuseReason.Blocked, chain, weight);

                var otherShip = grid.ShipAt(next);
                if (otherShip != null && otherShip != ship) return Refused(RefuseReason.Blocked, chain, weight);

                var block = grid.BlockAt(next);
                if (block != null && !moving.Contains(block)) return Refused(RefuseReason.Blocked, chain, weight);
            }

            if (weight > capacity) return Refused(RefuseReason.TooHeavy, chain, weight);

            var moved = new List<BlockState>(chain);
            foreach (var block in carried)
                if (moving.Contains(block) && !chain.Contains(block)) moved.Add(block);
            foreach (var block in moved) block.Position += step;
            foreach (var block in released) block.CarriedBy = null;
            ship.Position += step;
            return new MoveResult(true, default, moved, weight, released);
        }

        /// <summary>
        /// Adds to <paramref name="moving"/> every carried block that can come along, and lists the rest in
        /// <paramref name="released"/>. A carried block comes along while its path is clear and it still rests
        /// on something that moves (the ship, or another block coming along) — otherwise it stays behind,
        /// which may in turn strand whatever rode only on it. Repeats until nothing more is released.
        /// </summary>
        private static void CarryAlong(ShipState ship, GridPos step, GridModel grid, HashSet<BlockState> carried,
            HashSet<BlockState> moving, List<BlockState> released)
        {
            var candidates = new List<BlockState>();
            foreach (var block in carried)
                if (moving.Add(block)) candidates.Add(block);

            bool changed;
            do
            {
                changed = false;
                foreach (var block in candidates)
                {
                    if (!moving.Contains(block)) continue;
                    if (PathClear(block, ship, step, grid, moving) && RestsOnMover(block, ship, grid, moving)) continue;
                    moving.Remove(block);
                    released.Add(block);
                    changed = true;
                }
            } while (changed);
        }

        private static bool PathClear(BlockState block, ShipState ship, GridPos step, GridModel grid,
            HashSet<BlockState> moving)
        {
            foreach (var cell in GridModel.CellsOf(block))
            {
                var next = cell + step;
                if (grid.IsWall(next)) return false;
                var otherShip = grid.ShipAt(next);
                if (otherShip != null && otherShip != ship) return false;
                var other = grid.BlockAt(next);
                if (other != null && !moving.Contains(other)) return false;
            }
            return true;
        }

        private static bool RestsOnMover(BlockState block, ShipState ship, GridModel grid, HashSet<BlockState> moving)
        {
            foreach (var support in grid.SupportsOf(block))
                if (support.Ship == ship || (support.Block != null && moving.Contains(support.Block))) return true;
            return false;
        }

        /// <summary>
        /// The push chain for a sideways step (GDD §3 Push): every block the move would displace, plus
        /// everything riding on those blocks — and, in turn, whatever those displace or carry.
        /// Walls and ships are ignored here; TryMove checks the chain's front afterwards.
        /// </summary>
        /// <param name="exclude">Blocks never added (and not expanded), e.g. the ones this ship carries.</param>
        /// <returns>Each block once, in the order found.</returns>
        internal static List<BlockState> CollectChain(ShipState ship, Direction dir, GridModel grid,
            ICollection<BlockState> exclude = null)
        {
            // A block joins the chain when it is
            //   1. in front (cell + dir) of the ship or of a block already in the chain, or
            //   2. riding on a chain block: it covers the cell directly above one of the chain block's cells.
            var chain = new List<BlockState>();
            var blocksIdsVisited = new HashSet<BlockId>();
            var movers = new Queue<GridPos>(GridModel.CellsOf(ship));
            var step = dir.ToOffset();
            var stepUp = Direction.Up.ToOffset();
            while (movers.Count > 0)
            {
                var pos = movers.Dequeue();
                TryAddToChain(grid.BlockAt(pos + step), blocksIdsVisited, chain, movers, exclude);

                // Only block cells carry riders: ship cells return null here, so blocks on the ship's
                // roof stay out of a sideways push chain (for Up they are "in front", so they join the lift).
                if (grid.BlockAt(pos) != null)
                    TryAddToChain(grid.BlockAt(pos + stepUp), blocksIdsVisited, chain, movers, exclude);
            }
            return chain;
        }

        /// <summary>Adds <paramref name="block"/> to the chain once and queues its cells for expansion.</summary>
        private static void TryAddToChain(BlockState block, HashSet<BlockId> blocksIdsVisited, List<BlockState> chain,
            Queue<GridPos> movers, ICollection<BlockState> exclude)
        {
            if (block == null || (exclude != null && exclude.Contains(block)) || !blocksIdsVisited.Add(block.Id)) return;

            chain.Add(block);
            foreach (var blockPos in GridModel.CellsOf(block))
                movers.Enqueue(blockPos);
        }

        private static MoveResult Refused(RefuseReason reason, List<BlockState> chain, int weight) =>
            new MoveResult(false, reason, chain, weight);
    }
}
