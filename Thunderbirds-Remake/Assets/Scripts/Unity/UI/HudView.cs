using System;
using System.Collections.Generic;
using Thunderbirds.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// The in-game HUD (GDD §5 Game HUD): oxygen bar and seconds, life icons, the active ship with its Switch
    /// hint, the level-start hint banner, and a crush countdown ring drawn in the world above a stressed ship.
    /// Built in code under the Game scene, like LevelSelectView. It only reads the simulation state; it decides
    /// nothing. Deliberately absent: weight numbers on blocks (weight is read from colour and cell seams).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private const float RingAboveShip = 0.7f; // world units above the ship's top edge

        private Func<IReadOnlySimulationState> _state;
        private GameConfig _config;
        private InputActionAsset _actions;
        private TMP_FontAsset _font;
        private RectTransform _frame;

        private RectTransform _oxygenFill;
        private Image _oxygenFillImage;
        private TMP_Text _oxygenText, _title, _shipName, _switchHint, _bannerText, _restartText;
        private Image[] _lives = Array.Empty<Image>();
        private Image _portrait;
        private GameObject _banner;

        private readonly Dictionary<ShipId, Image> _rings = new Dictionary<ShipId, Image>();
        private readonly Dictionary<ShipId, Sprite> _portraitSprites = new Dictionary<ShipId, Sprite>();
        private readonly Dictionary<ShipId, Color> _portraitColours = new Dictionary<ShipId, Color>();

        private string _hint = "", _message = "";
        private float _hintLeft, _messageLeft;
        private bool _gamepad;
        private IDisposable _deviceSubscription;
        private static Sprite _ringSprite;

        // Read by tests.
        public string OxygenText => _oxygenText.text;
        public Color OxygenColour => _oxygenFillImage.color;
        public float OxygenBarFraction => _oxygenFill.anchorMax.x;
        public int LivesShown { get; private set; }
        public string ActiveShipText => _shipName.text;
        public Sprite PortraitSprite => _portrait.sprite;
        public Color PortraitColour => _portrait.color;
        public string SwitchHintText => _switchHint.text;
        public string BannerText => _banner.activeSelf ? _bannerText.text : "";
        public bool IsRingVisible(ShipId ship) => _rings.TryGetValue(ship, out var ring) && ring.transform.parent.gameObject.activeSelf;
        public float RingFill(ShipId ship) => _rings[ship].fillAmount;

        public static HudView Create(Transform parent, TMP_FontAsset font, GameConfig config, InputActionAsset actions)
        {
            var root = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(HudView));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5; // under the pause / outcome overlays (20)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var view = root.GetComponent<HudView>();
            view._config = config;
            view._font = font;
            view._actions = actions;
            view._frame = (RectTransform)root.transform;
            view.Build();
            return view;
        }

        private void Build()
        {
            // Top-left: oxygen bar, seconds and life icons.
            Label("OXYGEN", new Vector2(0, 1), new Vector2(48, -34), new Vector2(200, 34), 24, TextAlignmentOptions.Left);
            var bar = Box("Oxygen Bar", new Vector2(0, 1), new Vector2(48, -78), new Vector2(420, 30), new Color(1f, 1f, 1f, 0.12f));
            _oxygenFill = Rect("Fill", bar, Vector2.zero, Vector2.one);
            _oxygenFillImage = _oxygenFill.gameObject.AddComponent<Image>();
            _oxygenFillImage.raycastTarget = false;
            _oxygenText = Label("90", new Vector2(0, 1), new Vector2(484, -76), new Vector2(120, 36), 30, TextAlignmentOptions.Left);

            _lives = new Image[Mathf.Max(1, _config.livesPerLevel)];
            for (var i = 0; i < _lives.Length; i++)
            {
                var icon = Box("Life " + (i + 1), new Vector2(0, 1), new Vector2(48 + i * 44, -124), new Vector2(34, 22), _config.uiText);
                _lives[i] = icon.GetComponent<Image>();
            }

            // Top centre: level name, and the hold-to-restart progress under it.
            _title = Label("", new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(760, 44), 32, TextAlignmentOptions.Center);
            _restartText = Label("", new Vector2(0.5f, 1), new Vector2(0, -88), new Vector2(760, 32), 22, TextAlignmentOptions.Center);

            // Top-right: active ship portrait, name and the Switch hint for the device in use.
            var portrait = Box("Portrait", new Vector2(1, 1), new Vector2(-48, -34), new Vector2(120, 96), Color.white);
            _portrait = portrait.GetComponent<Image>();
            _portrait.preserveAspect = true;
            _shipName = Label("", new Vector2(1, 1), new Vector2(-188, -40), new Vector2(360, 40), 30, TextAlignmentOptions.Right);
            _switchHint = Label("", new Vector2(1, 1), new Vector2(-188, -88), new Vector2(360, 32), 22, TextAlignmentOptions.Right);
            _switchHint.color = _config.uiMuted;

            // Bottom: hint banner (level-start hint, then short feedback messages).
            var banner = Box("Hint Banner", new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(1500, 88), new Color(0f, 0f, 0f, 0.55f));
            _banner = banner.gameObject;
            _bannerText = Label("", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1440, 80), 26, TextAlignmentOptions.Center);
            _bannerText.transform.SetParent(banner, false);
            ((RectTransform)_bannerText.transform).anchoredPosition = Vector2.zero;
            _banner.SetActive(false);
        }

        /// <summary>
        /// Point the HUD at a level. <paramref name="state"/> is a function because Restart replaces the state object.
        /// </summary>
        public void Bind(Func<IReadOnlySimulationState> state, string title, string hint)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _title.text = title ?? "";
            _hint = hint ?? "";
            _hintLeft = string.IsNullOrWhiteSpace(_hint) ? 0f : _config.hintBannerSeconds;
            _message = "";
            _messageLeft = 0f;
            SetRestartProgress(0f);
            Refresh();
        }

        /// <summary>
        /// Give a ship its crush ring and portrait. The ring is parented to the ship's view so it travels with it.
        /// The portrait is a fixed picture: it does not follow the ship's turn frames, mirroring or colour flashes.
        /// </summary>
        public void AttachShip(ShipId ship, Transform shipView, int shipHeightCells, Sprite portrait, Color portraitColour)
        {
            _portraitSprites[ship] = portrait;
            portraitColour.a = 1f;
            _portraitColours[ship] = portraitColour;
            if (_rings.TryGetValue(ship, out var old) && old != null) DestroyNow(old.transform.parent.gameObject);

            var root = new GameObject("Crush Ring", typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(shipView, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "UI";
            var rect = (RectTransform)root.transform;
            rect.sizeDelta = Vector2.one; // 1 x 1 world unit
            rect.localPosition = new Vector3(0f, shipHeightCells * 0.5f + RingAboveShip, 0f);
            rect.localScale = Vector3.one;

            var track = RingImage("Track", rect, new Color(0f, 0f, 0f, 0.5f));
            track.type = Image.Type.Simple;
            var fill = RingImage("Fill", rect, _config.blockRed);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false; // empties clockwise, like a clock running out
            _rings[ship] = fill;
            root.SetActive(false);
        }

        /// <summary>A short message in the banner (a refused move, for example). It replaces the hint while shown.</summary>
        public void ShowMessage(string message, float seconds)
        {
            _message = message ?? "";
            _messageLeft = seconds;
            RefreshBanner();
        }

        /// <summary>0 hides the hold-to-restart read-out.</summary>
        public void SetRestartProgress(float progress)
        {
            _restartText.text = progress > 0f ? $"Hold to restart  {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f)}%" : "";
        }

        /// <summary>Advance the banner timers. Time is passed in, so tests don't wait.</summary>
        public void Tick(float dt)
        {
            if (_messageLeft > 0f) _messageLeft = Mathf.Max(0f, _messageLeft - dt);
            else if (_hintLeft > 0f) _hintLeft = Mathf.Max(0f, _hintLeft - dt);
            Refresh();
        }

        /// <summary>Copy the current simulation state onto the screen.</summary>
        public void Refresh()
        {
            var state = _state?.Invoke();
            if (state == null) return;

            var low = HudModel.IsOxygenLow(state.OxygenRemaining, _config.lowOxygenSeconds);
            _oxygenFill.anchorMax = new Vector2(HudModel.OxygenFraction(state.OxygenRemaining, state.OxygenTotal), 1f);
            _oxygenFillImage.color = low ? _config.blockRed : _config.uiText;
            _oxygenText.text = HudModel.OxygenSeconds(state.OxygenRemaining).ToString();
            _oxygenText.color = low ? _config.blockRed : _config.uiText;

            LivesShown = Mathf.Clamp(state.LivesLeft, 0, _lives.Length);
            for (var i = 0; i < _lives.Length; i++)
            {
                var colour = i < LivesShown ? _config.uiText : _config.uiMuted;
                colour.a = i < LivesShown ? 1f : 0.35f;
                _lives[i].color = colour;
            }

            _shipName.text = state.ActiveShip.ToString().ToUpperInvariant();
            if (_portraitSprites.TryGetValue(state.ActiveShip, out var portrait))
            {
                _portrait.sprite = portrait;
                _portrait.color = _portraitColours[state.ActiveShip];
            }
            _switchHint.text = "Switch: " + SwitchBinding();

            foreach (var ship in state.Ships)
            {
                if (!_rings.TryGetValue(ship.Id, out var ring) || ring == null) continue;
                var show = ship.IsStressed && !ship.IsGhost && state.Status == SimStatus.Playing;
                ring.transform.parent.gameObject.SetActive(show);
                if (show) ring.fillAmount = HudModel.CrushRingFill(ship.CrushSecondsLeft, _config.crushGraceSeconds);
            }

            RefreshBanner();
        }

        private void RefreshBanner()
        {
            var text = _messageLeft > 0f ? _message : _hintLeft > 0f ? _hint : "";
            _banner.SetActive(text.Length > 0);
            _bannerText.text = text;
        }

        /// <summary>The SwitchShip binding for the device used last, e.g. "Space" or "A".</summary>
        private string SwitchBinding()
        {
            var fallback = _gamepad ? "A" : "Space";
            var action = _actions != null ? _actions.FindAction("SwitchShip", false) : null;
            if (action == null) return fallback;
            var display = action.GetBindingDisplayString(group: _gamepad ? "Gamepad" : "Keyboard&Mouse");
            return string.IsNullOrEmpty(display) ? fallback : display;
        }

        /// <summary>Tests and the device listener: which device's binding to show.</summary>
        public void SetGamepad(bool gamepad)
        {
            _gamepad = gamepad;
            if (_state != null) Refresh();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _deviceSubscription = InputSystem.onAnyButtonPress.Call(control =>
            {
                if (control.device is Gamepad) SetGamepad(true);
                else if (control.device is Keyboard || control.device is Mouse) SetGamepad(false);
            });
        }

        private void OnDisable()
        {
            _deviceSubscription?.Dispose();
            _deviceSubscription = null;
        }

        // ------------------------------------------------------------- builders ----

        private Image RingImage(string name, RectTransform parent, Color colour)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.one);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = RingSprite();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A white ring on a transparent square, drawn once and shared by every crush ring.</summary>
        private static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;
            const int size = 64;
            const float outer = 30f, inner = 21f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Crush Ring", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                var alpha = Mathf.Clamp01(outer - distance) * Mathf.Clamp01(distance - inner); // 1-pixel soft edges
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _ringSprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
            _ringSprite.hideFlags = HideFlags.HideAndDontSave;
            return _ringSprite;
        }

        /// <summary>A coloured rectangle anchored to one corner or edge of the screen.</summary>
        private RectTransform Box(string name, Vector2 anchor, Vector2 position, Vector2 size, Color colour)
        {
            var rect = Anchored(name, anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return rect;
        }

        private TMP_Text Label(string text, Vector2 anchor, Vector2 position, Vector2 size, int fontSize, TextAlignmentOptions alignment)
        {
            var label = Anchored("Label", anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            if (_font != null) label.font = _font;
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = _config.uiText;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Anchor and pivot share one point, so <paramref name="position"/> is the offset from that screen corner.</summary>
        private RectTransform Anchored(string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(_frame, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void DestroyNow(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
