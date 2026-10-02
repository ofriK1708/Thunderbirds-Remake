using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Thunderbirds.Tests.EditMode
{
    /// <summary>Issue #16: the HUD shows what the simulation state says, and nothing it has to work out itself.</summary>
    public class HudTests
    {
        private const string Hint = "Dock both ships.";

        private GameConfig _config;
        private SimulationState _state;
        private HudView _hud;
        private GameObject _kestrelView;
        private SpriteRenderer _kestrelBody;
        private Sprite _portrait;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _state = TestStates.SmallRoom(); // 3 lives, 90 s, Kestrel active
            _hud = HudView.Create(null, null, _config, null);
            _kestrelView = new GameObject("Kestrel");
            _kestrelBody = new GameObject("Body").AddComponent<SpriteRenderer>();
            _kestrelBody.transform.SetParent(_kestrelView.transform, false);
            _portrait = Sprite.Create(new Texture2D(8, 4), new Rect(0, 0, 8, 4), Vector2.one * 0.5f);
            _hud.AttachShip(ShipId.Kestrel, _kestrelView.transform, 2, _portrait, Color.white);
            _hud.Bind(() => _state, "L1", Hint);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hud.gameObject);
            Object.DestroyImmediate(_kestrelView);
            Object.DestroyImmediate(_portrait.texture);
            Object.DestroyImmediate(_portrait);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Model_OxygenAndRingMaths()
        {
            Assert.AreEqual(0.5f, HudModel.OxygenFraction(45f, 90f));
            Assert.AreEqual(0f, HudModel.OxygenFraction(10f, 0f), "no division by zero");
            Assert.AreEqual(1f, HudModel.OxygenFraction(120f, 90f), "clamped");
            Assert.AreEqual(15, HudModel.OxygenSeconds(14.2f), "rounds up");
            Assert.AreEqual(0, HudModel.OxygenSeconds(-1f));
            Assert.IsFalse(HudModel.IsOxygenLow(15f, 15f));
            Assert.IsTrue(HudModel.IsOxygenLow(14.99f, 15f));
            Assert.IsTrue(HudModel.IsOxygenLow(25f, 30f), "the threshold comes from GameConfig");
            Assert.AreEqual(0.5f, HudModel.CrushRingFill(1.5f, 3f));
            Assert.AreEqual(0f, HudModel.CrushRingFill(1f, 0f));
        }

        [Test]
        public void Oxygen_ShowsBarAndSeconds_AndTurnsRedUnder15()
        {
            Assert.AreEqual("90", _hud.OxygenText);
            Assert.AreEqual(1f, _hud.OxygenBarFraction);
            Assert.AreEqual(_config.uiText, _hud.OxygenColour);

            _state.OxygenRemaining = 14.2f;
            _hud.Refresh();

            Assert.AreEqual("15", _hud.OxygenText);
            Assert.AreEqual(14.2f / 90f, _hud.OxygenBarFraction, 1e-4f);
            Assert.AreEqual(_config.blockRed, _hud.OxygenColour);
        }

        [Test]
        public void Lives_FollowTheState()
        {
            Assert.AreEqual(3, _hud.LivesShown);
            _state.LivesLeft = 1;
            _hud.Refresh();
            Assert.AreEqual(1, _hud.LivesShown);
        }

        [Test]
        public void ActiveShip_AndSwitchHint_FollowTheStateAndTheDevice()
        {
            Assert.AreEqual("KESTREL", _hud.ActiveShipText);
            Assert.AreEqual("Switch: Space", _hud.SwitchHintText, "keyboard fallback when no actions asset is given");

            _state.ActiveShip = ShipId.Atlas;
            _hud.SetGamepad(true);

            Assert.AreEqual("ATLAS", _hud.ActiveShipText);
            Assert.AreEqual("Switch: A", _hud.SwitchHintText);
        }

        [Test]
        public void Portrait_IsAFixedPicture_NotTheLiveShipSprite()
        {
            Assert.AreSame(_portrait, _hud.PortraitSprite);

            // The ship turns, mirrors and flashes red while stressed: none of it may reach the HUD.
            var turnFrame = Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            try
            {
                _kestrelBody.sprite = turnFrame;
                _kestrelBody.flipX = true;
                _kestrelBody.color = Color.red;
                _hud.Refresh();

                Assert.AreSame(_portrait, _hud.PortraitSprite);
                Assert.AreEqual(Color.white, _hud.PortraitColour);
            }
            finally
            {
                Object.DestroyImmediate(turnFrame.texture);
                Object.DestroyImmediate(turnFrame);
            }
        }

        [Test]
        public void SwitchHint_UsesTheRealBindings_PerDevice()
        {
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var hud = HudView.Create(null, null, _config, actions);
            try
            {
                hud.Bind(() => _state, "L1", "");
                var keyboard = hud.SwitchHintText;
                hud.SetGamepad(true);
                var gamepad = hud.SwitchHintText;

                StringAssert.StartsWith("Switch: ", keyboard);
                Assert.Greater(keyboard.Length, "Switch: ".Length);
                Assert.Greater(gamepad.Length, "Switch: ".Length);
                Assert.AreNotEqual(keyboard, gamepad);
            }
            finally
            {
                Object.DestroyImmediate(hud.gameObject);
            }
        }

        [Test]
        public void HintBanner_ShowsAtLevelStart_ThenHides()
        {
            Assert.AreEqual(Hint, _hud.BannerText);
            _hud.Tick(_config.hintBannerSeconds - 0.1f);
            Assert.AreEqual(Hint, _hud.BannerText);
            _hud.Tick(0.2f);
            Assert.AreEqual("", _hud.BannerText);
        }

        [Test]
        public void Message_ReplacesTheHint_ThenTheHintComesBack()
        {
            _hud.ShowMessage("Blocked.", 3f);
            Assert.AreEqual("Blocked.", _hud.BannerText);
            _hud.Tick(3f);
            Assert.AreEqual(Hint, _hud.BannerText, "the hint's own time did not run while the message was up");
        }

        [Test]
        public void LevelWithoutHint_ShowsNoBanner()
        {
            _hud.Bind(() => _state, "L0", "");
            Assert.AreEqual("", _hud.BannerText);
        }

        [Test]
        public void CrushRing_AppearsOnlyWhileStressed_AndDrainsWithTheCountdown()
        {
            var kestrel = _state.GetShip(ShipId.Kestrel);
            Assert.IsFalse(_hud.IsRingVisible(ShipId.Kestrel));

            kestrel.IsStressed = true;
            kestrel.CrushSecondsLeft = _config.crushGraceSeconds * 0.5f;
            _hud.Refresh();
            Assert.IsTrue(_hud.IsRingVisible(ShipId.Kestrel));
            Assert.AreEqual(0.5f, _hud.RingFill(ShipId.Kestrel), 1e-4f);

            kestrel.IsGhost = true;
            _hud.Refresh();
            Assert.IsFalse(_hud.IsRingVisible(ShipId.Kestrel), "a ghost has no countdown");
        }

        [Test]
        public void CrushRing_LivesInTheWorld_AboveItsShip()
        {
            var ring = _kestrelView.GetComponentInChildren<Canvas>(true);
            Assert.AreEqual(RenderMode.WorldSpace, ring.renderMode);
            Assert.AreSame(_kestrelView.transform, ring.transform.parent, "it travels with the ship view");
            Assert.Greater(ring.transform.localPosition.y, 1f, "above the top edge of a 2-cell-high ship");
        }

        [Test]
        public void Bind_AfterRestart_ReadsTheNewState()
        {
            _state.OxygenRemaining = 10f;
            _hud.Refresh();
            _state = TestStates.SmallRoom(); // Restart builds a new state object
            _hud.Refresh();
            Assert.AreEqual("90", _hud.OxygenText);
        }
    }
}
