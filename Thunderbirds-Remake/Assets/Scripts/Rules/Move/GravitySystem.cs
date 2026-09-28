using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Timed one-cell falls. Unsupported roots have independent clocks; exclusive riders follow
    /// their supports in the same step. Decisions use a snapshot before any positions change.
    /// </summary>
    internal sealed class GravitySystem
    {
        private readonly Dictionary<BlockId, double> _elapsed = new Dictionary<BlockId, double>();
        private readonly HashSet<BlockId> _falling = new HashSet<BlockId>();
        private readonly HashSet<GridPos> _fallenCells = new HashSet<GridPos>();

        /// <summary>Final occupied cells of blocks that fell this tick; ship moves yield to these cells.</summary>
        public ISet<GridPos> FallenCells => _fallenCells;

        public void Reset()
        {
            _elapsed.Clear();
            _falling.Clear();
            _fallenCells.Clear();
        }

        public void Tick(SimulationState state, float dt, float stepSeconds, EventHub events)
        {
            if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
                throw new ArgumentOutOfRangeException(nameof(dt));
            if (float.IsNaN(stepSeconds) || float.IsInfinity(stepSeconds) || stepSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));

            _fallenCells.Clear();
            var moved = new HashSet<BlockState>();
            var ordered = new List<BlockState>(state.Blocks);
            ordered.Sort((a, b) => a.Id.Letter.CompareTo(b.Id.Letter));
            double remaining = dt;
            var epsilon = stepSeconds * 1e-6;

            while (true)
            {
                var supports = new Dictionary<BlockState, IReadOnlyList<CellOccupant>>();
                var roots = new HashSet<BlockState>();
                foreach (var block in ordered)
                {
                    var below = state.Grid.SupportsOf(block);
                    supports.Add(block, below);
                    if (below.Count == 0) roots.Add(block);
                    else _elapsed.Remove(block.Id); // Supported time never banks a future fall.
                }

                var mobile = ExpandRiders(ordered, supports, roots);
                foreach (var block in ordered)
                    if (!mobile.Contains(block) && _falling.Remove(block.Id))
                        events.Raise(new BlockLanded(block.Id));

                if (roots.Count == 0) break;

                var untilNext = double.PositiveInfinity;
                foreach (var root in roots)
                {
                    if (!_elapsed.ContainsKey(root.Id)) _elapsed.Add(root.Id, 0);
                    untilNext = Math.Min(untilNext, Math.Max(0, stepSeconds - _elapsed[root.Id]));
                }

                if (untilNext > remaining + epsilon)
                {
                    foreach (var root in roots) _elapsed[root.Id] += remaining;
                    break;
                }

                var advance = Math.Min(untilNext, remaining);
                foreach (var root in roots) _elapsed[root.Id] += advance;
                remaining -= advance;
                var due = new HashSet<BlockState>();
                foreach (var root in roots)
                    if (_elapsed[root.Id] + epsilon >= stepSeconds)
                    {
                        due.Add(root);
                        _elapsed[root.Id] = Math.Max(0, _elapsed[root.Id] - stepSeconds);
                    }

                var fallingNow = ExpandRiders(ordered, supports, due);
                foreach (var block in ordered)
                {
                    if (!fallingNow.Contains(block)) continue;
                    var from = block.Position;
                    block.Position += Direction.Down.ToOffset();
                    moved.Add(block);
                    _falling.Add(block.Id);
                    events.Raise(new BlockFell(block.Id, from, block.Position, stepSeconds));
                }
                // Recheck contacts even when no time remains, so landing is reported on the arrival tick.
            }

            foreach (var block in moved)
                foreach (var cell in GridModel.CellsOf(block)) _fallenCells.Add(cell);
        }

        private static HashSet<BlockState> ExpandRiders(List<BlockState> ordered,
            Dictionary<BlockState, IReadOnlyList<CellOccupant>> supports, HashSet<BlockState> roots)
        {
            var group = new HashSet<BlockState>(roots);
            bool changed;
            do
            {
                changed = false;
                foreach (var block in ordered)
                {
                    if (group.Contains(block) || supports[block].Count == 0) continue;
                    var onlyOnGroup = true;
                    foreach (var support in supports[block])
                        if (support.Block == null || !group.Contains(support.Block))
                        {
                            onlyOnGroup = false;
                            break;
                        }
                    if (onlyOnGroup) { group.Add(block); changed = true; }
                }
            } while (changed);
            return group;
        }
    }
}
