using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Marks a ship's dock: the ship's initial (K or A) inside a softly glowing ring, so a dock reads as
    /// "Kestrel goes here" instead of a block of colour. Dim and slowly pulsing while empty, bright and steady
    /// once its ship is docked. The letter and ring are drawn in code, so no font or image asset is needed.
    /// </summary>
    public sealed class DockMarkerView : MonoBehaviour
    {
        private const float PulseHertz = 0.5f;
        private const float RingDiameterCells = 1.7f; // fits inside the 2-cell height of both docks
        private const float LetterHeightCells = 0.8f;

        // 5 x 7 bitmap letters, top row first. '#' = lit pixel.
        private static readonly string[] LetterK = { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" };
        private static readonly string[] LetterA = { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };

        private static Sprite _ringSprite, _kSprite, _aSprite;

        private SpriteRenderer _ring, _letter;
        private Color _colour;
        private float _time;

        public ShipId Ship { get; private set; }
        public bool IsLit { get; private set; }

        /// <summary>Overall brightness right now, 0..1. Read by tests.</summary>
        public float Brightness => _letter.color.a;

        public static DockMarkerView Create(Transform parent, ShipId ship, Color colour)
        {
            var view = new GameObject(ship + " Dock Marker").AddComponent<DockMarkerView>();
            view.transform.SetParent(parent, false);
            view.Ship = ship;
            view._colour = colour;
            view._ring = view.NewRenderer("Ring", RingSprite(), 0);
            view._ring.transform.localScale = Vector3.one * RingDiameterCells; // the ring sprite is 1 unit wide
            view._letter = view.NewRenderer("Letter", LetterSprite(ship), 1);
            view._letter.transform.localScale = Vector3.one * LetterHeightCells; // the letter sprite is 1 unit tall
            view._time = (int)ship * 0.9f; // the two docks don't pulse in step
            view.Apply();
            return view;
        }

        /// <summary>Centre the marker on a dock whose bottom-left cell is <paramref name="bottomLeft"/>.</summary>
        public void Place(GridPos bottomLeft, int widthCells, int heightCells)
        {
            transform.localPosition = GridSpace.FootprintCenter(bottomLeft, widthCells, heightCells);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        public void SetLit(bool lit)
        {
            if (IsLit == lit) return;
            IsLit = lit;
            Apply();
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>Time is passed in, so tests don't wait.</summary>
        public void Advance(float dt)
        {
            _time += dt;
            Apply();
        }

        private void Apply()
        {
            // Empty: a slow breath between 45 % and 75 %. Docked: full and steady.
            var brightness = IsLit ? 1f : 0.6f + 0.15f * Mathf.Sin(_time * PulseHertz * 2f * Mathf.PI);
            var colour = _colour;
            colour.a = brightness;
            _letter.color = colour;
            _ring.color = colour;
        }

        private SpriteRenderer NewRenderer(string objectName, Sprite sprite, int order)
        {
            var renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
            renderer.transform.SetParent(transform, false);
            renderer.sprite = sprite;
            renderer.sortingLayerName = "Docks";
            renderer.sortingOrder = order;
            return renderer;
        }

        private static Sprite LetterSprite(ShipId ship)
        {
            if (ship == ShipId.Kestrel) return _kSprite != null ? _kSprite : _kSprite = BitmapSprite("Dock K", LetterK);
            return _aSprite != null ? _aSprite : _aSprite = BitmapSprite("Dock A", LetterA);
        }

        /// <summary>A crisp pixel letter, 1 world unit tall.</summary>
        private static Sprite BitmapSprite(string spriteName, string[] rows)
        {
            int width = rows[0].Length, height = rows.Length;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = spriteName, hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                pixels[y * width + x] = rows[height - 1 - y][x] == '#'
                    ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            texture.SetPixels32(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * 0.5f, height);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>A thin bright ring with a soft halo inside and outside it, 1 world unit wide.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 128;
            const float radius = 50f, coreHalfWidth = 2.5f, glowWidth = 11f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Dock Ring", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            var centre = new Vector2(size * 0.5f, size * 0.5f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var fromRing = Mathf.Abs(Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) - radius);
                var core = Mathf.Clamp01(coreHalfWidth + 0.5f - fromRing);
                var glow = 0.55f * Mathf.Exp(-(fromRing * fromRing) / (glowWidth * glowWidth));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, glow)));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _ringSprite.name = "Dock Ring";
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }
    }
}
