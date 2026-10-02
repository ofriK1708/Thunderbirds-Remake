using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// ShipView is driven by hand here: coroutines don't run in Edit Mode, so each "frame" is an explicit
    /// AdvanceSlide + UpdatePose call, in the same order the Player Loop uses (Simulation first, view after).
    /// </summary>
    public class ShipViewTests
    {
        private const float Frame = 1f / 60f;

        private GameObject _root;
        private GameConfig _config;
        private EventHub _events;
        private ShipState _kestrel;
        private ShipView _view;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Ship view tests");
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _events = new EventHub();
            _kestrel = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), new GridPos(8, 1));

            _view = new GameObject("Kestrel").AddComponent<ShipView>();
            _view.transform.SetParent(_root.transform, false);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(_view.transform, false);
            _view.Bind(_kestrel, _events, _config, body);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Bind_SnapsToTheModelFootprintCentre()
        {
            Assert.AreEqual(new Vector3(2f, 2f, 0f), _view.transform.localPosition);
            Assert.IsFalse(_view.IsSliding);
        }

        [TestCase(0.08f)] // Kestrel at the default speed ratio
        [TestCase(0.16f)] // Atlas
        public void Step_TakesExactlyStepSeconds_AndIsHalfwayAtHalfTime(float stepSeconds)
        {
            Move(1, 0, stepSeconds);

            _view.AdvanceSlide(stepSeconds * 0.5f);
            Assert.AreEqual(2.5f, _view.transform.localPosition.x, 1e-4f, "halfway after half the step");
            Assert.IsTrue(_view.IsSliding);

            _view.AdvanceSlide(stepSeconds * 0.5f);
            Assert.AreEqual(3f, _view.transform.localPosition.x, 1e-4f, "arrived after the whole step");
            Assert.IsFalse(_view.AdvanceSlide(Frame), "nothing left to slide");
            Assert.AreEqual(3f, _view.transform.localPosition.x, 1e-4f, "never overshoots");
        }

        [Test]
        public void TargetIsTheModelPosition_TheMomentTheStepStarts()
        {
            Move(0, 1, 0.08f);
            Assert.AreEqual(GridSpace.FootprintCenter(_kestrel.Position, 2, 2), _view.Target);
        }

        [Test]
        public void OtherShipsMoves_AreIgnored()
        {
            _events.Raise(new ShipMoved(ShipId.Atlas, new GridPos(5, 1), new GridPos(6, 1), 0.16f));
            _events.Flush();
            Assert.IsFalse(_view.IsSliding);
        }

        [Test]
        public void HeldDirection_ChainsSteps_WithNoFrameStandingStill()
        {
            // Mirror the Simulation's step accumulator: a step every 0.08 s while held, 60 fps.
            const float step = 0.08f;
            var timer = 0f;
            var previousX = _view.transform.localPosition.x;
            for (var frame = 0; frame < 60; frame++) // 1 second of holding →
            {
                timer -= Frame;
                if (timer <= 0f)
                {
                    timer += step;
                    Move(1, 0, step);
                }
                _view.AdvanceSlide(Frame);
                var x = _view.transform.localPosition.x;
                Assert.Greater(x, previousX, $"sprite stood still on frame {frame}");
                previousX = x;
            }
            // Feel target (GDD §3): about 12 cells in one second for Kestrel.
            Assert.AreEqual(13, _kestrel.Position.X - 1, "model took 13 steps (one on the first frame)");
            Assert.That(previousX - 2f, Is.InRange(11.5f, 13f));
        }

        [Test]
        public void SlidingRight_LeansRight_ThenLevelsOutWhenIdle()
        {
            Move(1, 0, 0.16f);
            for (var i = 0; i < 6; i++) { _view.AdvanceSlide(Frame); _view.UpdatePose(Frame); }
            Assert.Less(Tilt(), -_config.shipTiltDegrees * 0.4f, "leans clockwise while moving right");

            for (var i = 0; i < 60; i++) { _view.AdvanceSlide(Frame); _view.UpdatePose(Frame); }
            Assert.AreEqual(0f, Tilt(), 0.1f, "level again when idle");
        }

        [Test]
        public void Idle_BobsWithinTheAmplitude_WithoutMovingTheShip()
        {
            var maxBob = 0f;
            for (var i = 0; i < 240; i++)
            {
                _view.UpdatePose(Frame);
                maxBob = Mathf.Max(maxBob, Mathf.Abs(_view.Body.transform.localPosition.y));
            }
            Assert.Greater(maxBob, _config.hoverBobAmplitude * 0.5f, "visibly bobs");
            Assert.LessOrEqual(maxBob, _config.hoverBobAmplitude + 1e-5f);
            Assert.AreEqual(new Vector3(2f, 2f, 0f), _view.transform.localPosition, "bob is on the body only");
        }

        [Test]
        public void Respawn_SnapsInsteadOfSliding()
        {
            Move(1, 0, 0.08f);
            _kestrel.Position = _kestrel.Start;
            _events.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Start, false));
            _events.Flush();
            Assert.IsFalse(_view.IsSliding);
            Assert.AreEqual(new Vector3(2f, 2f, 0f), _view.transform.localPosition);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Switch_PulsesWithoutHidingOrBlockingMovement_ThenExpires(bool automatic)
        {
            _events.Raise(new ActiveShipChanged(ShipId.Kestrel, automatic));
            _events.Flush();
            Assert.Greater(_view.Body.color.r, 1f);
            Move(1, 0, 0.08f);
            _view.AdvanceSlide(0.08f);
            Assert.IsFalse(_view.IsSliding);
            for (var i = 0; i < 120; i++)
            {
                _view.UpdatePose(Frame);
                Assert.AreEqual(1f, _view.Body.color.a);
                Assert.IsTrue(_view.Body.enabled);
            }
            Assert.AreEqual(Color.white, _view.Body.color);
        }

        [Test]
        public void Stress_OverridesSwitch_AndReliefRestoresIt()
        {
            _config.hoverBobAmplitude = 0;
            _events.Raise(new ActiveShipChanged(ShipId.Kestrel, false));
            _events.Raise(new ShipStressed(ShipId.Kestrel, 8, 3));
            _events.Flush();
            _view.UpdatePose(0.03f);
            Assert.Greater(_view.Body.color.r, _view.Body.color.g);
            Assert.Greater(_view.Body.transform.localPosition.sqrMagnitude, 0);
            Assert.AreEqual(new Vector3(2, 2, 0), _view.transform.localPosition);
            _events.Raise(new ShipRelieved(ShipId.Kestrel));
            _events.Flush();
            Assert.Greater(_view.Body.color.g, 1f);
            Assert.AreEqual(Vector3.zero, _view.Body.transform.localPosition);
        }

        [Test]
        public void Ghost_OverridesStressAndSwitch_BlinksTranslucent_ThenMaterialises()
        {
            _config.hoverBobAmplitude = 0;
            _events.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Start, true));
            _events.Raise(new ShipStressed(ShipId.Kestrel, 8, 3));
            _events.Raise(new ActiveShipChanged(ShipId.Kestrel, true));
            _events.Raise(new MoveRefused(ShipId.Kestrel, Direction.Right, RefuseReason.Blocked, null, ColourClass.Light));
            _events.Flush();
            var alpha = _view.Body.color.a;
            _view.UpdatePose(0.1f);
            Assert.AreNotEqual(alpha, _view.Body.color.a);
            Assert.That(_view.Body.color.a, Is.InRange(0.2f, 0.55f));
            Assert.AreEqual(_view.Body.color.r, _view.Body.color.g);
            Assert.AreEqual(Vector3.zero, _view.Body.transform.localPosition);
            _events.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Start, false));
            _events.Flush();
            Assert.AreEqual(Color.white, _view.Body.color);
        }

        [TestCase(Direction.Up, 0, 1)]
        [TestCase(Direction.Down, 0, -1)]
        [TestCase(Direction.Left, -1, 0)]
        [TestCase(Direction.Right, 1, 0)]
        public void Refusal_BumpsOnlyTheBody_ThenReturns(Direction direction, int x, int y)
        {
            _config.hoverBobAmplitude = 0;
            _events.Raise(new MoveRefused(ShipId.Kestrel, direction, RefuseReason.Blocked, null, ColourClass.Light));
            _events.Flush();
            _view.UpdatePose(0.09f);
            Assert.That(Vector3.Distance(new Vector3(x, y, 0) * 0.12f, _view.Body.transform.localPosition), Is.LessThan(1e-5f));
            Assert.AreEqual(new Vector3(2, 2, 0), _view.transform.localPosition);
            Assert.AreEqual(_kestrel.Start, _kestrel.Position);
            _view.UpdatePose(0.2f);
            Assert.AreEqual(Vector3.zero, _view.Body.transform.localPosition);
        }

        [Test]
        public void Rebind_ClearsEffectsAndOldSubscriptions_WithoutChangingOriginalTint()
        {
            _events.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Start, true));
            _events.Flush();
            var oldEvents = _events;
            _events = new EventHub();
            _view.Bind(_kestrel, _events, _config, isActive: true);
            oldEvents.Raise(new ShipStressed(ShipId.Kestrel, 8, 3));
            oldEvents.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Start, true));
            oldEvents.Flush();
            _view.UpdatePose(0.05f);
            Assert.AreEqual(Color.white, _view.Body.color);
            _view.Unbind();
            _events.Raise(new ActiveShipChanged(ShipId.Kestrel, false));
            _events.Flush();
            Assert.AreEqual(Color.white, _view.Body.color);
        }

        [Test]
        public void OtherShipsFeedback_IsIgnored()
        {
            var colour = _view.Body.color;
            _events.Raise(new ShipStressed(ShipId.Atlas, 9, 3));
            _events.Raise(new ShipRespawned(ShipId.Atlas, new GridPos(5, 1), true));
            _events.Raise(new MoveRefused(ShipId.Atlas, Direction.Right, RefuseReason.Blocked, null, ColourClass.Light));
            _events.Flush();
            _view.UpdatePose(0.09f);
            Assert.AreEqual(colour, _view.Body.color);
            Assert.AreEqual(0f, _view.Body.transform.localPosition.x);
        }

        [Test]
        public void RepeatedRestarts_PreserveAuthoredColourAndOpacity()
        {
            _view.Unbind();
            var original = new Color(0.3f, 0.6f, 0.9f, 0.8f);
            _view.Body.color = original;
            for (var i = 0; i < 3; i++)
            {
                _view.Bind(_kestrel, _events, _config, isActive: true);
                _events.Raise(new ActiveShipChanged(ShipId.Kestrel, false));
                _events.Flush();
                _view.UpdatePose(0.1f);
                Assert.AreEqual(original.a, _view.Body.color.a);
            }
            _view.Unbind();
            Assert.AreEqual(original, _view.Body.color);
        }

        [Test]
        public void SwitchPulse_ExpiresEvenWhileStressOverridesIt()
        {
            _events.Raise(new ActiveShipChanged(ShipId.Kestrel, false));
            _events.Raise(new ShipStressed(ShipId.Kestrel, 8, 3));
            _events.Flush();
            _view.UpdatePose(_config.switchHighlightSeconds + 0.1f);
            _events.Raise(new ShipRelieved(ShipId.Kestrel));
            _events.Flush();
            Assert.AreEqual(Color.white, _view.Body.color);
        }

        private void Move(int dx, int dy, float stepSeconds)
        {
            var from = _kestrel.Position;
            _kestrel.Position = from + new GridPos(dx, dy); // the model moves first...
            _events.Raise(new ShipMoved(ShipId.Kestrel, from, _kestrel.Position, stepSeconds));
            _events.Flush();                                // ...then the view hears about it
        }

        private float Tilt()
        {
            var z = _view.Body.transform.localEulerAngles.z;
            return z > 180f ? z - 360f : z;
        }
    }
}
