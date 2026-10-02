using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Builds a level's static visuals: wall tiles from a pool, and the two dock markers (GDD §7 LevelView).
    /// <see cref="Build"/> first releases everything from the previous build, so Restart / Next Level
    /// reuse the same objects and never Instantiate during play once the pools are warm.
    /// </summary>
    public sealed class LevelView : MonoBehaviour
    {
        [SerializeField] private GameConfig config;

        [Header("Prefabs")]
        [SerializeField] private SpriteRenderer wallTilePrefab;
        [Tooltip("The plain 1 x 1 cell sprite that block cells and the placeholder ships are built from.")]
        [SerializeField] private SpriteRenderer dockPadPrefab;

        [Header("Pre-warm")]
        [Tooltip("Enough for the largest level's wall cells, so rebuilding never instantiates.")]
        [SerializeField, Min(0)] private int wallPrewarm = 400;

        [Header("Sandbox")]
        [Tooltip("Built on Start when no campaign level was selected (the Sandbox button, or pressing Play in Game.unity).")]
        [SerializeField] private LevelData previewLevel;

        private ObjectPool<SpriteRenderer> _walls;
        private readonly List<SpriteRenderer> _builtWalls = new List<SpriteRenderer>();
        private readonly Dictionary<ShipId, DockMarkerView> _dockMarkers = new Dictionary<ShipId, DockMarkerView>();
        private Transform _markerGroup;
        private Sprite _plainWallSprite;
        private SpriteRenderer _background;

        /// <summary>The level currently shown, or null.</summary>
        public LevelDefinition Level { get; private set; }

        private void Awake()
        {
            _plainWallSprite = wallTilePrefab.sprite;
            _walls = new ObjectPool<SpriteRenderer>(wallTilePrefab, NewGroup("Walls"), wallPrewarm);
        }

        private void Start()
        {
            var selected = GameManager.Instance.TakeSelection(out var campaignIndex);
            var data = selected != null ? selected : previewLevel;
            if (Level == null && data != null)
            {
                Build(LevelParser.Parse(data.grid, config.atlas.pushCapacity));
                // The controller runs the level; walls and dock markers stay here so Next Level can rebuild them.
                gameObject.AddComponent<LevelController>().Initialize(config, data, Level, dockPadPrefab, campaignIndex);
            }
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
                DressWall(tile, x, y);
                _builtWalls.Add(tile);
            }

            ShowBackground(level);

            AddDock(ShipId.Kestrel, level.KestrelDock, LevelDefinition.KestrelWidth, LevelDefinition.KestrelHeight);
            AddDock(ShipId.Atlas, level.AtlasDock, LevelDefinition.AtlasWidth, LevelDefinition.AtlasHeight);
        }

        /// <summary>Brighten a dock's marker while its ship sits on it. Called by the level controller.</summary>
        public void SetDockLit(ShipId ship, bool lit)
        {
            if (_dockMarkers.TryGetValue(ship, out var marker)) marker.SetLit(lit);
        }

        /// <summary>The marker on a ship's dock, or null before the first Build. Read by tests.</summary>
        public DockMarkerView DockMarker(ShipId ship) =>
            _dockMarkers.TryGetValue(ship, out var marker) ? marker : null;

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
            _builtWalls.Clear();
            foreach (var marker in _dockMarkers.Values) marker.Hide(); // two markers, reused by every level
            if (_background != null) _background.gameObject.SetActive(false);
            Level = null;
        }

        /// <summary>
        /// Which wall tile a cell uses. Depends only on the cell, so a wall looks the same after every rebuild.
        /// </summary>
        public static int WallVariant(int x, int y, int variants)
        {
            unchecked
            {
                var hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
                hash ^= hash >> 13;
                return (int)(hash % (uint)variants);
            }
        }

        /// <summary>Textured tile from GameConfig.wallTiles, or the flat sandstone colour when there is no art.</summary>
        private void DressWall(SpriteRenderer tile, int x, int y)
        {
            var tiles = config.wallTiles;
            var art = tiles != null && tiles.Length > 0 ? tiles[WallVariant(x, y, tiles.Length)] : null;
            if (art == null)
            {
                tile.sprite = _plainWallSprite; // pooled tiles keep their last sprite: always set it
                tile.color = config.sandstone;
                tile.transform.localScale = Vector3.one;
                return;
            }
            tile.sprite = art;
            tile.color = Color.white;
            var size = art.bounds.size;
            tile.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f); // exactly one cell, whatever its resolution
        }

        /// <summary>One tiled sprite behind the whole level.</summary>
        private void ShowBackground(LevelDefinition level)
        {
            if (config.backgroundTile == null) return;
            if (_background == null)
            {
                _background = new GameObject("Background").AddComponent<SpriteRenderer>();
                _background.transform.SetParent(transform, false);
                _background.sortingLayerName = "Background";
                _background.drawMode = SpriteDrawMode.Tiled;
            }
            _background.sprite = config.backgroundTile;
            var scale = config.backgroundTileCells / config.backgroundTile.bounds.size.x;
            _background.transform.localScale = new Vector3(scale, scale, 1f);
            _background.size = new Vector2(level.Width, level.Height) / scale;
            _background.transform.localPosition = new Vector3(level.Width * 0.5f, level.Height * 0.5f, 0f);
            _background.gameObject.SetActive(true);
        }

        private void AddDock(ShipId ship, GridPos bottomLeft, int width, int height)
        {
            // A dock is only its marker: the ship's letter in a glowing ring, centred on the dock's footprint.
            if (!_dockMarkers.TryGetValue(ship, out var marker))
            {
                if (_markerGroup == null) _markerGroup = NewGroup("Dock Markers");
                marker = DockMarkerView.Create(_markerGroup, ship, config.dockLit);
                _dockMarkers.Add(ship, marker);
            }
            marker.SetLit(false);
            marker.Place(bottomLeft, width, height);
        }

        private Transform NewGroup(string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(transform, false);
            return group;
        }
    }
}
