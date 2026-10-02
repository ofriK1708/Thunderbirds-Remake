using System;
using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    [Flags]
    public enum CellNeighbours { None = 0, Up = 1, Right = 2, Down = 4, Left = 8 }

    /// <summary>One borderless sprite, reused for fill, edge strips and inward corner pieces.</summary>
    public sealed class CellView : MonoBehaviour
    {
        private const float Border = 0.055f;
        private const float Gap = 0.02f;
        private const float Seam = 0.012f;
        private static readonly GridPos[] Steps =
        {
            new GridPos(0, 1), new GridPos(1, 0), new GridPos(0, -1), new GridPos(-1, 0)
        };

        [SerializeField] private SpriteRenderer fill;
        [SerializeField] private SpriteRenderer[] edges;
        [SerializeField] private SpriteRenderer[] corners;

        public CellNeighbours Neighbours { get; private set; }
        // Clockwise from top-right: a corner exists when both sides, but not the diagonal, belong to us.
        public int InnerCorners { get; private set; }

        /// <param name="fillSprite">
        /// Optional texture for the cell's fill. Edges and corners always use the plain <paramref name="sprite"/>,
        /// so the outline stays a flat colour.
        /// </param>
        public void Initialize(Sprite sprite, Material material, Sprite fillSprite = null)
        {
            if (fill != null) return;
            fill = NewPart("Fill", fillSprite != null ? fillSprite : sprite, material, 0);
            edges = new SpriteRenderer[4];
            corners = new SpriteRenderer[4];
            for (var i = 0; i < 4; i++)
            {
                edges[i] = NewPart("Edge " + (CellNeighbours)(1 << i), sprite, material, 1);
                corners[i] = NewPart("Inner corner " + i, sprite, material, 3);
            }
        }

        public void Configure(GridPos offset, ISet<GridPos> shape, Color colour, Color outline)
        {
            Neighbours = CellNeighbours.None;
            InnerCorners = 0;
            for (var i = 0; i < 4; i++)
                if (shape.Contains(Add(offset, Steps[i]))) Neighbours |= (CellNeighbours)(1 << i);

            transform.localPosition = GridSpace.CellCenter(offset);
            float left = Has(3) ? 0f : Gap, right = Has(1) ? 0f : Gap;
            float bottom = Has(2) ? 0f : Gap, top = Has(0) ? 0f : Gap;
            Place(fill, new Vector2((left - right) / 2, (bottom - top) / 2),
                new Vector2(1 - left - right, 1 - bottom - top), colour);
            // Light seams remain readable on teal, without dividing it into dark separate squares.
            var seamColour = Color.Lerp(colour, Color.white, 0.18f);
            for (var i = 0; i < 4; i++)
            {
                var adjacent = Has(i);
                // External edges cover seam ends so the block's silhouette stays continuous.
                edges[i].sortingOrder = adjacent ? 1 : 2;
                var thickness = adjacent ? Seam : Border;
                var distance = 0.5f - thickness / 2 - (adjacent ? 0f : Gap);
                var vertical = i == 1 || i == 3;
                var position = new Vector2(Steps[i].X * distance, Steps[i].Y * distance);
                if (vertical) position.y = (bottom - top) / 2;
                else position.x = (left - right) / 2;
                Place(edges[i], position, vertical
                    ? new Vector2(thickness, 1 - bottom - top)
                    : new Vector2(1 - left - right, thickness), adjacent ? seamColour : outline);

                var next = (i + 1) % 4;
                var diagonal = Add(Steps[i], Steps[next]);
                var inward = Has(i) && Has(next) && !shape.Contains(Add(offset, diagonal));
                corners[i].enabled = inward;
                if (!inward) continue;
                InnerCorners |= 1 << i;
                var size = Gap + Border;
                Place(corners[i], new Vector2(diagonal.X, diagonal.Y) * (0.5f - size / 2),
                    Vector2.one * size, outline);
            }
        }

        private bool Has(int side) => (Neighbours & (CellNeighbours)(1 << side)) != 0;
        private static GridPos Add(GridPos a, GridPos b) => new GridPos(a.X + b.X, a.Y + b.Y);

        private SpriteRenderer NewPart(string partName, Sprite sprite, Material material, int order)
        {
            var part = new GameObject(partName).AddComponent<SpriteRenderer>();
            part.transform.SetParent(transform, false);
            part.sprite = sprite;
            if (material != null) part.sharedMaterial = material;
            part.sortingLayerName = "Blocks";
            part.sortingOrder = order;
            return part;
        }

        private static void Place(SpriteRenderer part, Vector2 position, Vector2 size, Color colour)
        {
            part.transform.localPosition = position;
            var bounds = part.sprite.bounds;
            part.transform.localScale = new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, 1);
            // Also support a source sprite whose pivot isn't centred.
            part.transform.localPosition -= Vector3.Scale(bounds.center, part.transform.localScale);
            part.color = colour;
        }
    }
}
