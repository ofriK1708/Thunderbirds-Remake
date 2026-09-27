using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Builds a level's static visuals — wall tiles and the two dock pads — from pools (GDD §7 LevelView).
    /// <see cref="Build"/> first releases everything from the previous build, so Restart / Next Level
    /// reuse the same objects and never Instantiate during play once the pools are warm.
    /// </summary>
    public sealed class LevelView : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Header("Prefabs")]
        [SerializeField] private SpriteRenderer wallTilePrefab;
        [SerializeField] private SpriteRenderer dockPadPrefab;

        [Header("Pre-warm")]
        [Tooltip("Enough for the largest level's wall cells, so rebuilding never instantiates.")]
        [SerializeField, Min(0)] private int wallPrewarm = 400;

        [Header("Preview (until LevelController exists)")]
        [Tooltip("If set, this level is built on Start. LevelController will call Build instead.")]
        [SerializeField] private LevelData previewLevel;

        [Tooltip("Dock pads are drawn this dim until their ship docks (lit state arrives with docking).")]
        [SerializeField, Range(0f, 1f)] private float unlitDockAlpha = 0.35f;

        private ObjectPool<SpriteRenderer> _walls;
        private ObjectPool<SpriteRenderer> _docks;
        private readonly List<SpriteRenderer> _builtWalls = new List<SpriteRenderer>();
        private readonly List<SpriteRenderer> _builtDocks = new List<SpriteRenderer>();

        /// <summary>The level currently shown, or null.</summary>
        public LevelDefinition Level { get; private set; }

        private void Awake()
        {
            _walls = new ObjectPool<SpriteRenderer>(wallTilePrefab, NewGroup("Walls"), wallPrewarm);
            _docks = new ObjectPool<SpriteRenderer>(dockPadPrefab, NewGroup("Docks"), 2,
                onRelease: pad => pad.transform.localScale = Vector3.one);
        }

        private void Start()
        {
            if (Level == null && previewLevel != null)
                Build(LevelParser.Parse(previewLevel.grid, config.atlas.pushCapacity));
        }

        /// <summary>Show <paramref name="level"/>, replacing whatever was built before.</summary>
        public void Build(LevelDefinition level)
        {
            Clear();
            Level = level;

            for (var y = 0; y < level.Height; y++)
            for (var x = 0; x < level.Width; x++)
            {
                var cell = new GridPos(x, y);
                if (!level.IsWall(cell)) continue;

                var tile = _walls.Get();
                tile.transform.localPosition = GridSpace.CellCenter(cell);
                tile.color = config.sandstone;
                _builtWalls.Add(tile);
            }

            AddDock(level.KestrelDock, LevelDefinition.KestrelWidth, LevelDefinition.KestrelHeight);
            AddDock(level.AtlasDock, LevelDefinition.AtlasWidth, LevelDefinition.AtlasHeight);
        }

        /// <summary>
        /// Live tuning: after editing the palette in Play mode, rebuild from the pools (⋮ menu on the component).
        /// Also shows that a rebuild creates nothing new — watch the Walls group's child count stay the same.
        /// </summary>
        [ContextMenu("Rebuild")]
        private void Rebuild()
        {
            if (Application.isPlaying && Level != null) Build(Level);
        }

        /// <summary>Returns every built object to its pool.</summary>
        public void Clear()
        {
            foreach (var tile in _builtWalls) _walls.Release(tile);
            foreach (var pad in _builtDocks) _docks.Release(pad);
            _builtWalls.Clear();
            _builtDocks.Clear();
            Level = null;
        }

        private void AddDock(GridPos bottomLeft, int width, int height)
        {
            var pad = _docks.Get();
            pad.transform.localPosition = GridSpace.FootprintCenter(bottomLeft, width, height);
            pad.transform.localScale = new Vector3(width, height, 1f); // prefab sprite is 1 x 1 unit
            var colour = config.dockLit;
            colour.a = unlitDockAlpha;
            pad.color = colour;
            _builtDocks.Add(pad);
        }

        private Transform NewGroup(string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(transform, false);
            return group;
        }
    }
}
