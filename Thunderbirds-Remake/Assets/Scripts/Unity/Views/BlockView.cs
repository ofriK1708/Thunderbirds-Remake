using System;
using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>Reads model colour and position; owns pooled cells, never movement or weight rules.</summary>
    public sealed class BlockView : MonoBehaviour
    {
        private ObjectPool<CellView> _pool;
        private readonly List<CellView> _cells = new List<CellView>();
        private BlockState _block;
        private GameConfig _config;
        private float _flashLeft;

        public IReadOnlyList<CellView> Cells => _cells;
        public int AllocatedCells => _pool?.CountAll ?? 0;

        /// <summary>True while the block shows a refused chain's colour instead of its own.</summary>
        public bool IsFlashing => _flashLeft > 0f;

        /// <summary>Build/rebind using the model's weight-derived colour class and the shared palette.</summary>
        public void Build(BlockState block, GameConfig config, Sprite sprite, Material material = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            Clear();
            _block = block;
            _config = config;
            _flashLeft = 0f;
            if (_pool == null)
            {
                var template = new GameObject("Cell template").AddComponent<CellView>();
                template.transform.SetParent(transform, false);
                template.Initialize(sprite, material, config.blockCell);
                template.gameObject.SetActive(false);
                _pool = new ObjectPool<CellView>(template, transform, block.Weight);
            }
            foreach (var offset in block.Cells)
            {
                var cell = _pool.Get();
                cell.name = $"Cell {offset.X},{offset.Y}";
                _cells.Add(cell);
            }
            Paint(config.ColourOf(block.Colour));
            SyncPosition(block);
        }

        /// <summary>
        /// Show the whole block in <paramref name="chainColour"/> for a moment: a refused push tints every block
        /// of the chain with the class of the chain's total weight, so a group no ship can move turns red
        /// (GDD §3 Push). The block's own colour comes back afterwards.
        /// </summary>
        public void Flash(Color chainColour, float seconds)
        {
            if (_block == null || seconds <= 0f) return;
            _flashLeft = seconds;
            Paint(chainColour);
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>Time is passed in, so tests don't wait.</summary>
        public void Advance(float dt)
        {
            if (_flashLeft <= 0f) return;
            _flashLeft -= dt;
            if (_flashLeft <= 0f) Paint(_config.ColourOf(_block.Colour));
        }

        /// <summary>Colour every cell: the class colour is the fill in the flat look, the edge and glow in the textured one.</summary>
        private void Paint(Color classColour)
        {
            var shape = new HashSet<GridPos>(_block.Cells);
            for (var i = 0; i < _cells.Count; i++)
            {
                var offset = _block.Cells[i];
                if (_config.blockCell == null)
                    _cells[i].Configure(offset, shape, classColour, _config.tombVoid); // flat look: colour fill, dark outline
                else
                    // Textured look: stone fill, with the colour class on the edge and glowing inward from it.
                    _cells[i].Configure(offset, shape, _config.blockStone, classColour,
                        Color.Lerp(_config.blockStone, Color.black, 0.45f), _config.blockEdgeWidth,
                        _config.blockGlow, _config.blockGlowWidth, varyFill: true);
            }
        }

        public void SyncPosition(BlockState block) =>
            transform.localPosition = new Vector3(block.Position.X, block.Position.Y, 0);

        public void Clear()
        {
            foreach (var cell in _cells) _pool.Release(cell);
            _cells.Clear();
        }
    }
}
