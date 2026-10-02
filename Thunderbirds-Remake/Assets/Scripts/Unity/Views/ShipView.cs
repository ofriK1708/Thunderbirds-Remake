using System;
using System.Collections;
using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Slides, tilts and bobs one ship toward its model position (GDD §3 Ships &amp; movement, §7 ShipView).
    /// The model position is authoritative: this view only catches up with it, it never decides anything.
    /// Two transforms keep the motions apart: the root carries the slide, the body child carries tilt and bob.
    /// </summary>
    public sealed class ShipView : MonoBehaviour
    {
        // Slow and gentle: a hovering craft, not a jitter. Not a tuning value, so not in GameConfig.
        private const float BobHertz = 0.6f;

        // How fast tilt and bob blend in and out (per second). Higher = snappier.
        private const float PoseBlendSpeed = 10f;

        [SerializeField] private SpriteRenderer body;

        private ShipState _ship;
        private ISimulationEvents _events;
        private GameConfig _config;

        private Vector3 _target;
        private float _speed; // world units per second of the current slide
        private float _travelX; // -1 left, +1 right, 0 vertical: which way to lean
        private Coroutine _slide;

        private float _tilt; // current lean, degrees
        private float _bobWeight; // 0 while moving, 1 when fully idle
        private float _bobTime;
        private Color _baseColour;
        private bool _active, _stressed, _ghost;
        private float _feedbackTime, _highlightLeft, _bumpLeft;
        private Vector3 _bumpDirection;
        private const float BumpSeconds = 0.18f;

        // Art (optional). The three frames are drawn facing right; facing left is the mirror image.
        private Sprite _sideSprite, _turnSprite, _frontSprite;
        private Sprite _sideFlame, _turnFlame, _frontFlame;
        private SpriteRenderer _flames;
        private float _flameTime;
        private float _artScale = 1f;
        private int _facing = 1; // +1 right, -1 left
        private float _turnLeft; // seconds left of the turn-around animation

        public bool HasArt => _sideSprite != null;

        /// <summary>+1 when the ship faces right, -1 when it faces left.</summary>
        public int Facing => _facing;

        public ShipId Ship => _ship.Id;
        public SpriteRenderer Body => body;

        /// <summary>Where the sprite is heading: always the model position's footprint centre.</summary>
        public Vector3 Target => _target;

        public bool IsSliding => transform.localPosition != _target;

        /// <summary>
        /// Use real sprites instead of the placeholder rectangle. Call before <see cref="Bind"/>.
        /// <paramref name="turn"/> and <paramref name="front"/> may be null: the side view is used instead.
        /// </summary>
        public void SetArt(Sprite side, Sprite turn, Sprite front,
            Sprite sideFlame = null, Sprite turnFlame = null, Sprite frontFlame = null)
        {
            _sideSprite = side;
            _turnSprite = turn != null ? turn : side;
            _frontSprite = front != null ? front : side;
            // A frame without its own flame sprite shows no flames, rather than another frame's.
            _sideFlame = sideFlame;
            _turnFlame = turn != null ? turnFlame : sideFlame;
            _frontFlame = front != null ? frontFlame : sideFlame;
        }

        /// <summary>The thruster flame layer, or null when the art has no flame sprites. Read by tests.</summary>
        public SpriteRenderer Flames => _flames;

        /// <summary>
        /// The largest uniform scale at which <paramref name="sprite"/> still fits inside a footprint of
        /// <paramref name="widthCells"/> x <paramref name="heightCells"/>. Uniform, so the art is never stretched.
        /// </summary>
        public static float FitScale(Sprite sprite, int widthCells, int heightCells)
        {
            var size = sprite.bounds.size;
            return Mathf.Min(widthCells / size.x, heightCells / size.y);
        }

        /// <summary>Attach to a ship and its simulation's events, and snap to where the model says it is.</summary>
        public void Bind(ShipState ship, ISimulationEvents events, GameConfig config,
            SpriteRenderer bodyRenderer = null, bool isActive = false)
        {
            Unbind();
            _ship = ship ?? throw new ArgumentNullException(nameof(ship));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            if (bodyRenderer != null) body = bodyRenderer;
            if (body == null) throw new InvalidOperationException("ShipView needs a body SpriteRenderer child.");
            if (HasArt)
            {
                body.sprite = _sideSprite;
                body.color = Color.white; // the art carries its own colours; tints multiply from white
                _artScale = FitScale(_sideSprite, ship.Width, ship.Height);
                body.transform.localScale = Vector3.one * _artScale;
                if (_sideFlame != null && _flames == null)
                {
                    // A child of the body, so it inherits the fit scale, the tilt and the bob.
                    _flames = new GameObject("Flames").AddComponent<SpriteRenderer>();
                    _flames.transform.SetParent(body.transform, false);
                    _flames.sharedMaterial = body.sharedMaterial;
                    _flames.sortingLayerID = body.sortingLayerID;
                    _flames.sortingOrder = body.sortingOrder - 1; // behind the hull: the nozzles overlap the flame tops
                }
            }
            _baseColour = body.color;
            _active = isActive;
            _stressed = ship.IsStressed;
            _ghost = ship.IsGhost;
            _feedbackTime = _highlightLeft = _bumpLeft = 0f;

            // Ships don't bob in sync: offset the phase per ship.
            _bobTime = (int)ship.Id * 0.37f / BobHertz;
            _events.ShipMoved += OnShipMoved;
            _events.ShipRespawned += OnShipRespawned;
            _events.ActiveShipChanged += OnActiveShipChanged;
            _events.ShipStressed += OnShipStressed;
            _events.ShipRelieved += OnShipRelieved;
            _events.ShipCrushed += OnShipCrushed;
            _events.MoveRefused += OnMoveRefused;
            SnapToModel();
        }

        public void Unbind()
        {
            if (_events == null) return;
            _events.ShipMoved -= OnShipMoved;
            _events.ShipRespawned -= OnShipRespawned;
            _events.ActiveShipChanged -= OnActiveShipChanged;
            _events.ShipStressed -= OnShipStressed;
            _events.ShipRelieved -= OnShipRelieved;
            _events.ShipCrushed -= OnShipCrushed;
            _events.MoveRefused -= OnMoveRefused;
            StopSlide();
            if (body != null)
            {
                body.color = _baseColour;
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
            }
            _events = null;
            _ship = null;
        }

        /// <summary>Jump straight to the model position (Restart, respawn): no slide, no lean.</summary>
        public void SnapToModel()
        {
            StopSlide();
            _target = CentreOf(_ship.Position);
            transform.localPosition = _target;
            _travelX = 0;
            _tilt = 0;
            _bobWeight = 0;
            _highlightLeft = _bumpLeft = _turnLeft = 0f;
            ApplyPose();
        }

        private void OnShipMoved(ShipMoved e)
        {
            if (e.Ship != _ship.Id) return;
            _travelX = Math.Sign(e.To.X - e.From.X);
            if (_travelX != 0 && (int)_travelX != _facing)
            {
                _facing = (int)_travelX; // vertical moves keep the facing: the deck must stay on top
                _turnLeft = HasArt ? _config.shipTurnSeconds : 0f;
            }
            BeginStep(CentreOf(e.To), e.StepSeconds);
            EnsureSliding();
        }

        private void OnShipRespawned(ShipRespawned e)
        {
            if (e.Ship != _ship.Id) return;
            _ghost = e.IsGhost;
            _stressed = false;
            _feedbackTime = 0f;
            SnapToModel();
        }

        private void OnActiveShipChanged(ActiveShipChanged e)
        {
            _active = e.Active == _ship.Id;
            _highlightLeft = _active ? _config.switchHighlightSeconds : 0f;
            ApplyPose();
        }

        private void OnShipStressed(ShipStressed e)
        {
            if (e.Ship != _ship.Id) return;
            _stressed = true;
            _feedbackTime = 0f;
            ApplyPose();
        }

        private void OnShipRelieved(ShipRelieved e)
        {
            if (e.Ship != _ship.Id) return;
            _stressed = false;
            ApplyPose();
        }

        private void OnShipCrushed(ShipCrushed e)
        {
            if (e.Ship != _ship.Id) return;
            _stressed = false;
            _highlightLeft = _bumpLeft = 0f;
            ApplyPose();
        }

        private void OnMoveRefused(MoveRefused e)
        {
            if (e.Ship != _ship.Id || _ghost) return;
            var offset = e.Direction.ToOffset();
            _bumpDirection = new Vector3(offset.X, offset.Y, 0f);
            _bumpLeft = BumpSeconds;
        }

        /// <summary>
        /// A new step has started in the model: the sprite must now head for <paramref name="target"/>
        /// and should get there in <paramref name="stepSeconds"/>.
        /// Called while a previous slide may still be running — that is how steps chain.
        /// </summary>
        public void BeginStep(Vector3 target, float stepSeconds)
        {
            if (stepSeconds <= 0)
            {
                SnapToModel();
            }
            else
            {
                _target = target;
                float distanceLeft = Vector3.Distance(transform.localPosition, target);
                _speed = distanceLeft / stepSeconds;
            }
        }

        private IEnumerator Slide()
        {
            // AdvanceSlide runs once now, then after each Update (i.e. after the Simulation ticked this frame).
            while (AdvanceSlide(Time.deltaTime))
                yield return null;
            _slide = null;
        }

        /// <summary>
        /// Moves the sprite <paramref name="dt"/> seconds further along the current slide.
        /// Returns true while the sprite has not yet reached <see cref="Target"/>.
        /// The coroutine calls this once per frame; tests call it directly.
        /// </summary>
        public bool AdvanceSlide(float dt)
        {
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, _target, dt * _speed);
            return IsSliding;
        }

        private void EnsureSliding()
        {
            // One running coroutine per ship. A new step only moves the target, it doesn't restart the loop.
            if (_slide == null && isActiveAndEnabled && Application.isPlaying)
                _slide = StartCoroutine(Slide());
        }

        private void StopSlide()
        {
            if (_slide != null) StopCoroutine(_slide);
            _slide = null;
        }

        private void OnDisable()
        {
            // A disabled object's coroutines die; don't leave the sprite between two cells.
            StopSlide();
            if (_ship != null) transform.localPosition = _target;
        }

        private void OnDestroy() => Unbind();

        private void LateUpdate()
        {
            if (_ship != null) UpdatePose(Time.deltaTime);
        }

        /// <summary>Tilt while travelling sideways, bob while idle. Presentation only; public for tests.</summary>
        public void UpdatePose(float dt)
        {
            if (_ship == null) return;
            dt = Mathf.Max(0f, dt);
            _feedbackTime += dt;
            _highlightLeft = Mathf.Max(0f, _highlightLeft - dt);
            _bumpLeft = Mathf.Max(0f, _bumpLeft - dt);
            _turnLeft = Mathf.Max(0f, _turnLeft - dt);
            _flameTime += dt;
            var moving = IsSliding;
            var targetTilt = moving ? -_travelX * _config.shipTiltDegrees : 0f; // lean into the direction of travel
            var blend = 1f - Mathf.Exp(-PoseBlendSpeed * dt); // frame-rate independent smoothing
            _tilt = Mathf.Lerp(_tilt, targetTilt, blend);
            _bobWeight = Mathf.MoveTowards(_bobWeight, moving ? 0f : 1f, dt * PoseBlendSpeed * 0.25f);
            _bobTime += dt;
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (body == null) return;
            var bob = Mathf.Sin(_bobTime * BobHertz * 2f * Mathf.PI) * _config.hoverBobAmplitude * _bobWeight;
            var offset = new Vector3(0f, bob, 0f);
            var colour = _active ? Color.Lerp(_baseColour, Color.white, 0.35f) : _baseColour * 0.7f;
            colour.a = _baseColour.a;
            // Single compositor: ghost overrides stress, stress overrides switch tint.
            // Refusal motion can accompany stress, but never a ghost. Timers still expire underneath.
            if (_ghost)
            {
                colour = _baseColour;
                colour.a *= 0.2f + 0.35f * (0.5f + 0.5f * Mathf.Cos(_feedbackTime * 6f * Mathf.PI));
            }
            else
            {
                if (_stressed)
                {
                    colour = Color.Lerp(colour, _config.blockRed, 0.65f + 0.35f * Mathf.Cos(_feedbackTime * 8f * Mathf.PI));
                    offset += new Vector3(Mathf.Sin(_feedbackTime * 86f), Mathf.Sin(_feedbackTime * 113f), 0f) * 0.035f;
                }
                else if (_highlightLeft > 0f)
                {
                    // HDR white also brightens a normally white renderer tint on textured sprites.
                    var pulse = 0.65f + 0.35f * Mathf.Cos((_config.switchHighlightSeconds - _highlightLeft) * 6f * Mathf.PI);
                    colour = Color.Lerp(colour, new Color(2f, 2f, 2f, colour.a), pulse);
                }
                colour.a = _baseColour.a;
                if (_bumpLeft > 0f)
                    offset += _bumpDirection * (0.12f * Mathf.Sin(Mathf.PI * (1f - _bumpLeft / BumpSeconds)));
            }
            body.color = colour;
            body.transform.localPosition = ArtRestPosition() + offset;
            body.transform.localRotation = Quaternion.Euler(0f, 0f, _tilt);
        }

        /// <summary>
        /// Picks the frame for the current facing / turn and returns where the body rests inside the footprint:
        /// centred sideways, with the top of the sprite (the cargo deck) on the top edge, so carried blocks
        /// sit on the deck. Zero for the placeholder rectangle.
        /// </summary>
        private Vector3 ArtRestPosition()
        {
            if (!HasArt) return Vector3.zero;
            var frame = _sideSprite;
            var mirrored = _facing < 0;
            if (_turnLeft > 0f)
            {
                var progress = 1f - _turnLeft / _config.shipTurnSeconds; // 0 -> 1 through the turn
                if (progress < 1f / 3f) { frame = _turnSprite; mirrored = _facing > 0; } // still mostly the old way
                else if (progress < 2f / 3f) { frame = _frontSprite; mirrored = false; }
                else frame = _turnSprite;
            }
            body.sprite = frame;
            body.flipX = mirrored;
            ApplyFlames(frame, frame == _sideSprite ? _sideFlame : frame == _frontSprite ? _frontFlame : _turnFlame, mirrored);
            var bounds = frame.bounds;
            return new Vector3(-bounds.center.x * _artScale * (mirrored ? -1f : 1f),
                _ship.Height * 0.5f - bounds.max.y * _artScale, 0f);
        }

        /// <summary>
        /// The flames breathe: they stretch and shrink downward from the nozzles, a little longer while flying.
        /// The flame sprite shares the hull frame's width and bottom edge, so its top row is where the flames start;
        /// that row stays put and only the tips move.
        /// </summary>
        private void ApplyFlames(Sprite hull, Sprite flame, bool mirrored)
        {
            if (_flames == null) return;
            _flames.enabled = flame != null;
            if (flame == null) return;
            _flames.sprite = flame;
            _flames.flipX = mirrored;
            _flames.color = body.color; // ghost blink, stress flash and the idle dimming reach the flames too

            var stretch = FlameStretch();
            var top = hull.bounds.min.y + flame.bounds.size.y; // in the body's local (sprite) units
            _flames.transform.localScale = new Vector3(1f, stretch, 1f);
            _flames.transform.localPosition = new Vector3(hull.bounds.center.x - flame.bounds.center.x,
                top - flame.bounds.max.y * stretch, 0f);
        }

        /// <summary>Current flame length as a multiple of the drawn length. Public for tests.</summary>
        public float FlameStretch()
        {
            var amplitude = _config.flameBreathAmplitude;
            var phase = _flameTime * _config.flameBreathHertz * 2f * Mathf.PI + (int)_ship.Id * 1.7f;
            var breath = Mathf.Sin(phase) + 0.35f * Mathf.Sin(phase * 3.3f + 1.3f); // slow breath plus a flicker
            return 1f + amplitude * breath + (IsSliding ? amplitude * 0.8f : 0f);
        }

        private Vector3 CentreOf(GridPos bottomLeft) =>
            GridSpace.FootprintCenter(bottomLeft, _ship.Width, _ship.Height);
    }
}
