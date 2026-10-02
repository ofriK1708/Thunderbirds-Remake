using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>
    /// Ship sprites: fitted into the ship's footprint without stretching, cargo deck on the top edge,
    /// facing the direction of travel, with a short turn-around animation.
    /// </summary>
    public class ShipArtTests
    {
        private GameObject _root;
        private GameConfig _config;
        private EventHub _events;
        private ShipState _kestrel;
        private ShipView _view;
        private SpriteRenderer _body;
        private Sprite _side, _turn, _front, _sideFlame, _turnFlame, _frontFlame;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Ship art tests");
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _events = new EventHub();
            _kestrel = new ShipState(ShipId.Kestrel, 2, 2, new GridPos(1, 1), new GridPos(8, 1));
            _side = NewSprite(200, 100);  // 2 x 1 world units: twice as wide as tall
            _turn = NewSprite(150, 120);
            _front = NewSprite(100, 100);
            // Flame sprites: as wide as their hull frame, and as tall as the flames (cut from the flame tops down).
            _sideFlame = NewSprite(200, 30);
            _turnFlame = NewSprite(150, 40);
            _frontFlame = NewSprite(100, 30);

            _view = new GameObject("Kestrel").AddComponent<ShipView>();
            _view.transform.SetParent(_root.transform, false);
            _body = new GameObject("Body").AddComponent<SpriteRenderer>();
            _body.transform.SetParent(_view.transform, false);
            _view.SetArt(_side, _turn, _front, _sideFlame, _turnFlame, _frontFlame);
            _view.Bind(_kestrel, _events, _config, _body);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var sprite in new[] { _side, _turn, _front, _sideFlame, _turnFlame, _frontFlame })
            {
                Object.DestroyImmediate(sprite.texture);
                Object.DestroyImmediate(sprite);
            }
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_config);
        }

        private static Sprite NewSprite(int width, int height) =>
            Sprite.Create(new Texture2D(width, height), new Rect(0, 0, width, height), Vector2.one * 0.5f, 100f);

        private void Step(int dx, int dy)
        {
            var from = _kestrel.Position;
            _kestrel.Position = from + new GridPos(dx, dy);
            _events.Raise(new ShipMoved(ShipId.Kestrel, from, _kestrel.Position, 0.08f));
            _events.Flush();
        }

        private float BodyTop => _body.transform.localPosition.y + _body.sprite.bounds.max.y * _body.transform.localScale.y;

        [Test]
        public void FitScale_NeverStretches_AndNeverLeavesTheFootprint()
        {
            Assert.AreEqual(1f, ShipView.FitScale(_side, 2, 2), 1e-4f, "2 x 1 sprite in 2 x 2: limited by width");
            Assert.AreEqual(2f, ShipView.FitScale(_side, 4, 2), 1e-4f, "2 x 1 sprite in 4 x 2: fills it exactly");
            Assert.AreEqual(2f, ShipView.FitScale(_front, 4, 2), 1e-4f, "1 x 1 sprite in 4 x 2: limited by height");
        }

        [Test]
        public void Bind_ShowsTheSideView_Unstretched_WithTheDeckOnTheTopEdge()
        {
            Assert.AreSame(_side, _body.sprite);
            Assert.IsTrue(_body.color.r == _body.color.g && _body.color.g == _body.color.b, "tinted from white: no hue shift");
            Assert.AreEqual(_body.transform.localScale.x, _body.transform.localScale.y, "uniform scale");
            Assert.AreEqual(_kestrel.Height * 0.5f, BodyTop, 1e-4f, "top of the sprite = top of the footprint");
            Assert.AreEqual(0f, _body.transform.localPosition.x, 1e-4f, "centred sideways");
        }

        [Test]
        public void FacesTheDirectionOfTravel_AndVerticalMovesKeepTheFacing()
        {
            Assert.AreEqual(1, _view.Facing);
            Assert.IsFalse(_body.flipX);

            Step(-1, 0);
            _view.UpdatePose(_config.shipTurnSeconds + 0.01f); // let the turn finish
            Assert.AreEqual(-1, _view.Facing);
            Assert.AreSame(_side, _body.sprite);
            Assert.IsTrue(_body.flipX, "left is the mirrored side view");

            Step(0, 1);
            _view.UpdatePose(0.05f);
            Assert.AreEqual(-1, _view.Facing, "moving up does not turn the ship");
            Assert.IsTrue(_body.flipX);
        }

        [Test]
        public void TurningAround_PlaysThreeQuarter_Front_ThreeQuarter_ThenTheSideView()
        {
            var third = _config.shipTurnSeconds / 3f;
            Step(-1, 0); // was facing right, now turning left

            _view.UpdatePose(third * 0.5f);
            Assert.AreSame(_turn, _body.sprite);
            Assert.IsFalse(_body.flipX, "first still turned the old way (right)");

            _view.UpdatePose(third);
            Assert.AreSame(_front, _body.sprite);

            _view.UpdatePose(third);
            Assert.AreSame(_turn, _body.sprite);
            Assert.IsTrue(_body.flipX, "then turned the new way (left)");

            _view.UpdatePose(third);
            Assert.AreSame(_side, _body.sprite);
            Assert.IsTrue(_body.flipX);
        }

        [Test]
        public void EveryTurnFrame_KeepsTheDeckOnTheTopEdge()
        {
            Step(-1, 0);
            for (var i = 0; i < 12; i++)
            {
                _view.UpdatePose(_config.shipTurnSeconds / 10f);
                // bob and bump are zero here: the ship was just moved and nothing was refused
                Assert.AreEqual(_kestrel.Height * 0.5f, BodyTop, 0.06f, $"frame {i}: {_body.sprite.name}");
            }
        }

        [Test]
        public void SteppingTheSameWay_DoesNotRestartTheTurn()
        {
            Step(1, 0);
            _view.UpdatePose(0.01f);
            Assert.AreSame(_side, _body.sprite, "already facing right: no turn");
        }

        // ---- thruster flames ----

        /// <summary>Where the top row of the flame layer sits, in the body's local units.</summary>
        private float FlameTop()
        {
            var flames = _view.Flames;
            return flames.transform.localPosition.y + flames.sprite.bounds.max.y * flames.transform.localScale.y;
        }

        [Test]
        public void Flames_AreALayerBehindTheHull_ThatFollowsItsFrameAndMirror()
        {
            var flames = _view.Flames;
            Assert.IsNotNull(flames);
            Assert.AreSame(_body.transform, flames.transform.parent, "inherits the hull's scale, tilt and bob");
            Assert.Less(flames.sortingOrder, _body.sortingOrder);
            Assert.AreSame(_sideFlame, flames.sprite);

            Step(-1, 0);
            _view.UpdatePose(_config.shipTurnSeconds * 0.5f);
            Assert.AreSame(_frontFlame, flames.sprite, "front frame, front flames");
            _view.UpdatePose(_config.shipTurnSeconds);
            Assert.AreSame(_sideFlame, flames.sprite);
            Assert.IsTrue(flames.flipX, "mirrored with the hull");
        }

        [Test]
        public void Flames_Breathe_StretchingDownFromTheNozzles()
        {
            // The flame layer shares the hull frame's bottom edge, so its top row is at: hull bottom + flame height.
            var nozzles = _side.bounds.min.y + _sideFlame.bounds.size.y;
            float shortest = float.MaxValue, longest = 0f;
            for (var i = 0; i < 120; i++)
            {
                _view.UpdatePose(1f / 60f);
                var stretch = _view.Flames.transform.localScale.y;
                shortest = Mathf.Min(shortest, stretch);
                longest = Mathf.Max(longest, stretch);
                Assert.AreEqual(nozzles, FlameTop(), 1e-4f, "the flame tops never leave the nozzles");
                Assert.AreEqual(1f, _view.Flames.transform.localScale.x, "only the length changes");
            }
            Assert.Greater(longest - shortest, _config.flameBreathAmplitude, "visibly breathing");
            Assert.Greater(shortest, 0.5f);
        }

        [Test]
        public void Flames_AreStill_WhenTheAmplitudeIsZero_AndLonger_WhileFlying()
        {
            _config.flameBreathAmplitude = 0f;
            _view.UpdatePose(0.3f);
            Assert.AreEqual(1f, _view.Flames.transform.localScale.y, 1e-5f);

            _config.flameBreathAmplitude = 0.2f;
            _config.flameBreathHertz = 0f; // freeze the breath to isolate the thrust boost
            _view.UpdatePose(0.01f);
            var idle = _view.FlameStretch();
            Step(1, 0); // now sliding
            Assert.Greater(_view.FlameStretch(), idle + 0.1f);
        }

        [Test]
        public void Flames_TakeTheHullsTint_SoAGhostsFlamesFadeToo()
        {
            _kestrel.IsGhost = true;
            _events.Raise(new ShipRespawned(ShipId.Kestrel, _kestrel.Position, isGhost: true));
            _events.Flush();
            _view.UpdatePose(0.01f);
            Assert.Less(_body.color.a, 0.7f);
            Assert.AreEqual(_body.color, _view.Flames.color);
        }

        [Test]
        public void ArtWithoutFlameSprites_HasNoFlameLayer()
        {
            var view = new GameObject("No flames").AddComponent<ShipView>();
            view.transform.SetParent(_root.transform, false);
            var body = new GameObject("Body").AddComponent<SpriteRenderer>();
            body.transform.SetParent(view.transform, false);
            view.SetArt(_side, _turn, _front);
            view.Bind(_kestrel, _events, _config, body);
            view.UpdatePose(0.1f);
            Assert.IsNull(view.Flames);
        }

        [TestCase("Assets/Config/Kestrel.asset", 2, 2)]
        [TestCase("Assets/Config/Atlas.asset", 4, 2)]
        public void RealShipConfigs_HaveAllThreeSprites_ThatFitTheirFootprint(string path, int width, int height)
        {
            var config = AssetDatabase.LoadAssetAtPath<ShipConfig>(path);
            Assert.IsNotNull(config.sideSprite, "sideSprite");
            Assert.IsNotNull(config.turnSprite, "turnSprite");
            Assert.IsNotNull(config.frontSprite, "frontSprite");
            Assert.IsNotNull(config.portraitSprite, "portraitSprite");
            foreach (var (hull, flame, name) in new[]
                     {
                         (config.sideSprite, config.sideFlame, "side"), (config.turnSprite, config.turnFlame, "turn"),
                         (config.frontSprite, config.frontFlame, "front")
                     })
            {
                Assert.IsNotNull(flame, name + " flame");
                Assert.AreEqual(hull.bounds.size.x, flame.bounds.size.x, 1e-3f, name + ": flame layer is as wide as its hull frame");
                Assert.Less(flame.bounds.size.y, hull.bounds.size.y, name);
            }

            var scale = ShipView.FitScale(config.sideSprite, width, height);
            var size = config.sideSprite.bounds.size * scale;
            Assert.LessOrEqual(size.x, width + 1e-3f);
            Assert.LessOrEqual(size.y, height + 1e-3f);
            Assert.Greater(size.x, width * 0.9f, "the cargo deck should span (nearly) the whole footprint width");
        }
    }
}
