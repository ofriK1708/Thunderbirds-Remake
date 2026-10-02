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
        public IReadOnlyList<CellView> Cells => _cells;
        public int AllocatedCells => _pool?.CountAll ?? 0;

        /// <summary>Build/rebind using the model's weight-derived colour class and the shared palette.</summary>
        public void Build(BlockState block, GameConfig config, Sprite sprite, Material material = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (sprite == null) throw new ArgumentNullException(nameof(sprite));
            Clear();
            if (_pool == null)
            {
                var template = new GameObject("Cell template").AddComponent<CellView>();
                template.transform.SetParent(transform, false);
                template.Initialize(sprite, material, config.blockCell);
                template.gameObject.SetActive(false);
                _pool = new ObjectPool<CellView>(template, transform, block.Weight);
            }
            var shape = new HashSet<GridPos>(block.Cells);
            foreach (var offset in block.Cells)
            {
                var cell = _pool.Get();
                cell.name = $"Cell {offset.X},{offset.Y}";
                var classColour = config.ColourOf(block.Colour);
                if (config.blockCell == null)
                    cell.Configure(offset, shape, classColour, config.tombVoid); // flat look: colour fill, dark outline
                else
                    // Textured look: stone fill, with the colour class on the edge and glowing inward from it.
                    cell.Configure(offset, shape, config.blockStone, classColour,
                        Color.Lerp(config.blockStone, Color.black, 0.45f), config.blockEdgeWidth,
                        config.blockGlow, config.blockGlowWidth, varyFill: true);
                _cells.Add(cell);
            }
            SyncPosition(block);
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
