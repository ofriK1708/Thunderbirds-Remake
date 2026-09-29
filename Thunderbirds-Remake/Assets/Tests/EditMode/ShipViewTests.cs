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
