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

        public ShipId Ship => _ship.Id;
        public SpriteRenderer Body => body;

        /// <summary>Where the sprite is heading: always the model position's footprint centre.</summary>
        public Vector3 Target => _target;

        public bool IsSliding => transform.localPosition != _target;

        /// <summary>Attach to a ship and its simulation's events, and snap to where the model says it is.</summary>
        public void Bind(ShipState ship, ISimulationEvents events, GameConfig config,
            SpriteRenderer bodyRenderer = null)
        {
            Unbind();
            _ship = ship ?? throw new ArgumentNullException(nameof(ship));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _config = config ? config : throw new ArgumentNullException(nameof(config));
            if (bodyRenderer != null) body = bodyRenderer;
            if (body == null) throw new InvalidOperationException("ShipView needs a body SpriteRenderer child.");

            // Ships don't bob in sync: offset the phase per ship.
            _bobTime = (int)ship.Id * 0.37f / BobHertz;
            _events.ShipMoved += OnShipMoved;
            _events.ShipRespawned += OnShipRespawned;
            SnapToModel();
        }

        public void Unbind()
        {
            if (_events == null) return;
            _events.ShipMoved -= OnShipMoved;
            _events.ShipRespawned -= OnShipRespawned;
            _events = null;
        }

        /// <summary>Jump straight to the model position (Restart, respawn): no slide, no lean.</summary>
        public void SnapToModel()
        {
            StopSlide();
            _target = CentreOf(_ship.Position);
            transform.localPosition = _target;
            _travelX = 0;
            _tilt = 0;
            ApplyPose();
        }

        private void OnShipMoved(ShipMoved e)
        {
            if (e.Ship != _ship.Id) return;
            _travelX = Math.Sign(e.To.X - e.From.X);
            BeginStep(CentreOf(e.To), e.StepSeconds);
            EnsureSliding();
        }

        private void OnShipRespawned(ShipRespawned e)
        {
            if (e.Ship == _ship.Id) SnapToModel();
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
            body.transform.localPosition = new Vector3(0f, bob, 0f);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, _tilt);
        }

        private Vector3 CentreOf(GridPos bottomLeft) =>
            GridSpace.FootprintCenter(bottomLeft, _ship.Width, _ship.Height);
    }
}