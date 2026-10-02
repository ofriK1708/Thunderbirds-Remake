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
        [SerializeField] private SpriteRenderer[] glows; // created on first use: only textured blocks glow

        private static Sprite _glowSprite;

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
            fill = NewPart("Fill", fillSprite != null ? fillSprite : sprite, material, -1); // under the glow (0)
            edges = new SpriteRenderer[4];
            corners = new SpriteRenderer[4];
            for (var i = 0; i < 4; i++)
            {
                edges[i] = NewPart("Edge " + (CellNeighbours)(1 << i), sprite, material, 1);
                corners[i] = NewPart("Inner corner " + i, sprite, material, 3);
            }
        }

        /// <param name="colour">Fill colour (a tint, when the fill has a texture).</param>
        /// <param name="outline">Colour of the outside edge, the inward corners and the glow.</param>
        /// <param name="seam">Colour of the thin line between two cells of the same block. Default: a lighter fill.</param>
        /// <param name="border">Width of the outside edge, in cells.</param>
        /// <param name="glow">0..1: how strongly the outline colour fades inward over the fill.</param>
        /// <param name="glowWidth">How far the glow reaches, in cells.</param>
        /// <param name="varyFill">Mirror the fill texture differently per cell, so a big block doesn't look stamped.</param>
        public void Configure(GridPos offset, ISet<GridPos> shape, Color colour, Color outline, Color? seam = null,
            float border = Border, float glow = 0f, float glowWidth = 0.3f, bool varyFill = false)
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
            fill.flipX = varyFill && ((offset.X * 7 + offset.Y * 3) & 1) == 1;
            fill.flipY = varyFill && ((offset.X * 5 + offset.Y) & 1) == 1;
            // Light seams remain readable on teal, without dividing it into dark separate squares.
            var seamColour = seam ?? Color.Lerp(colour, Color.white, 0.18f);
            ConfigureGlow(glow, glowWidth, border, outline, left, right, bottom, top);
            for (var i = 0; i < 4; i++)
            {
                var adjacent = Has(i);
                // External edges cover seam ends so the block's silhouette stays continuous.
                edges[i].sortingOrder = adjacent ? 1 : 2;
                var thickness = adjacent ? Seam : border;
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
                var size = Gap + border;
                Place(corners[i], new Vector2(diagonal.X, diagonal.Y) * (0.5f - size / 2),
                    Vector2.one * size, outline);
            }
        }

        /// <summary>
        /// A soft strip of the outline colour just inside each outside edge, fading toward the middle of the block.
        /// </summary>
        private void ConfigureGlow(float glow, float width, float border, Color outline,
            float left, float right, float bottom, float top)
        {
            if (glow <= 0f)
            {
                if (glows != null) foreach (var strip in glows) strip.enabled = false;
                return;
            }
            if (glows == null || glows.Length == 0)
            {
                glows = new SpriteRenderer[4];
                for (var i = 0; i < 4; i++)
                {
                    glows[i] = NewPart("Glow " + (CellNeighbours)(1 << i), GlowSprite(), fill.sharedMaterial, 0);
                    // The sprite is opaque along its bottom edge: turn that edge to face the outside.
                    glows[i].transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 180f : i == 1 ? 90f : i == 2 ? 0f : -90f);
                }
            }
            var colour = outline;
            colour.a = glow;
            for (var i = 0; i < 4; i++)
            {
                glows[i].enabled = !Has(i);
                if (Has(i)) continue;
                var vertical = i == 1 || i == 3;
                var distance = 0.5f - Gap - border - width / 2;
                var position = new Vector2(Steps[i].X * distance, Steps[i].Y * distance);
                if (vertical) position.y = (bottom - top) / 2;
                else position.x = (left - right) / 2;
                var length = vertical ? 1 - bottom - top : 1 - left - right;
                var bounds = glows[i].sprite.bounds;
                glows[i].transform.localPosition = position;
                // Scale is applied before the rotation: x runs along the edge, y points into the block.
                glows[i].transform.localScale = new Vector3(length / bounds.size.x, width / bounds.size.y, 1f);
                glows[i].color = colour;
            }
        }

        /// <summary>White, fully opaque along the bottom edge and fading to nothing at the top.</summary>
        private static Sprite GlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Block Glow", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            {
                var fade = 1f - y / (size - 1f);
                var alpha = (byte)Mathf.RoundToInt(255f * fade * fade);
                for (var x = 0; x < size; x++) pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _glowSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _glowSprite.name = "Block Glow";
            _glowSprite.hideFlags = HideFlags.HideAndDontSave;
            return _glowSprite;
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
