using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>A block as authored: its letter, bottom-left position and cell offsets. Never changes.</summary>
    public sealed class BlockDefinition
    {
        public BlockId Id { get; }
        public GridPos Position { get; }
        public IReadOnlyList<GridPos> Cells { get; }
        public int Weight => Cells.Count;

        public BlockDefinition(BlockId id, GridPos position, IReadOnlyList<GridPos> cells)
        {
            Id = id;
            Position = position;
            Cells = cells;
        }
    }

    /// <summary>
    /// A parsed, validated level (GDD §7 Level format). Immutable: it is kept for the whole level, and
    /// every start, Restart and respawn builds a fresh live <see cref="SimulationState"/> from it.
    /// </summary>
    public sealed class LevelDefinition
    {
        public const int KestrelWidth = 2, KestrelHeight = 2;
        public const int AtlasWidth = 4, AtlasHeight = 2;

        private readonly bool[,] _walls;

        public int Width { get; }
        public int Height { get; }
        public GridPos KestrelStart { get; }
        public GridPos AtlasStart { get; }
        public GridPos KestrelDock { get; }
        public GridPos AtlasDock { get; }
        public IReadOnlyList<BlockDefinition> Blocks { get; }

        internal LevelDefinition(bool[,] walls, GridPos kestrelStart, GridPos atlasStart,
            GridPos kestrelDock, GridPos atlasDock, IReadOnlyList<BlockDefinition> blocks)
        {
            _walls = walls;
            Width = walls.GetLength(0);
            Height = walls.GetLength(1);
            KestrelStart = kestrelStart;
            AtlasStart = atlasStart;
            KestrelDock = kestrelDock;
            AtlasDock = atlasDock;
            Blocks = blocks;
        }

        /// <summary>Outside the grid counts as wall, same as <see cref="IReadOnlySimulationState.IsWall"/>.</summary>
        public bool IsWall(GridPos p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= Width || p.Y >= Height) return true;
            return _walls[p.X, p.Y];
        }

        /// <summary>A fresh live state. Pass this (as a lambda) to the Simulation's buildState.</summary>
        public SimulationState CreateState(SimulationConfig config, int lives, float oxygenSeconds)
        {
            var kestrel = new ShipState(ShipId.Kestrel, KestrelWidth, KestrelHeight, KestrelStart, KestrelDock);
            var atlas = new ShipState(ShipId.Atlas, AtlasWidth, AtlasHeight, AtlasStart, AtlasDock);

            var blocks = new List<BlockState>(Blocks.Count);
            foreach (var b in Blocks)
                blocks.Add(new BlockState(b.Id, b.Position, b.Cells, ColourOf(b.Weight, config)));

            // The live state gets its own copy of the walls, so nothing can edit the definition.
            return new SimulationState((bool[,])_walls.Clone(), new[] { kestrel, atlas }, blocks, lives, oxygenSeconds);
        }

        /// <summary>GDD §3: teal = Kestrel can push it, yellow = only Atlas can, red = neither.</summary>
        public static ColourClass ColourOf(int weight, SimulationConfig config)
        {
            if (weight <= config.KestrelPushCapacity) return ColourClass.Teal;
            if (weight <= config.AtlasPushCapacity) return ColourClass.Yellow;
            return ColourClass.Red;
        }
    }
}
