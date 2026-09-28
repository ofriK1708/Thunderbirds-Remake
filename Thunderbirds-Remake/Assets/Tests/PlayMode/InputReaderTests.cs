using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Thunderbirds.Tests.PlayMode
{
    public class InputReaderTests : InputTestFixture
    {
        private InputReader _reader;
        private InputActionAsset _source;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
#if UNITY_EDITOR
            _source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
#else
            _source = InputSystem.actions;
#endif
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _reader = new InputReader(_source);
            _reader.Enable();
        }

        public override void TearDown()
        {
            _reader?.Dispose();
            base.TearDown();
        }

        [TestCase(Key.W, Direction.Up)]
        [TestCase(Key.S, Direction.Down)]
        [TestCase(Key.A, Direction.Left)]
        [TestCase(Key.D, Direction.Right)]
        [TestCase(Key.UpArrow, Direction.Up)]
        [TestCase(Key.DownArrow, Direction.Down)]
        [TestCase(Key.LeftArrow, Direction.Left)]
        [TestCase(Key.RightArrow, Direction.Right)]
        public void Keyboard_MoveIsHeldAndReleaseClearsIt(Key key, Direction direction)
        {
            Press(_keyboard[key]);
            Assert.AreEqual(direction, _reader.HeldDirection);
            InputSystem.Update();
            Assert.AreEqual(direction, _reader.HeldDirection);
            Release(_keyboard[key]);
            Assert.IsNull(_reader.HeldDirection);
        }

        [Test]
        public void LastPressedAxisWins_AndReleaseReturnsToStillHeldAxis()
        {
            Press(_keyboard.dKey);
            Press(_keyboard.wKey);
            Assert.AreEqual(Direction.Up, _reader.HeldDirection);
            Release(_keyboard.wKey);
            Assert.AreEqual(Direction.Right, _reader.HeldDirection);
            Press(_keyboard.downArrowKey); // Arrow keys can combine with WASD.
            Assert.AreEqual(Direction.Down, _reader.HeldDirection);
            Release(_keyboard.dKey);
            Press(_keyboard.dKey);
            Assert.AreEqual(Direction.Right, _reader.HeldDirection);
        }

        [Test]
        public void GamepadStick_UsesLastPressedAxis_NotMagnitudeOrJitter()
        {
            Set(_gamepad.leftStick, new Vector2(1, 0));
            Assert.AreEqual(Direction.Right, _reader.HeldDirection);
            Set(_gamepad.leftStick, new Vector2(1, 1));
            Assert.AreEqual(Direction.Up, _reader.HeldDirection);
            Set(_gamepad.leftStick, new Vector2(0.95f, 0.8f));
            Assert.AreEqual(Direction.Up, _reader.HeldDirection);
            Set(_gamepad.leftStick, new Vector2(1, 0));
            Assert.AreEqual(Direction.Right, _reader.HeldDirection);
            Set(_gamepad.leftStick, new Vector2(0.1f, 0.1f));
            Assert.IsNull(_reader.HeldDirection);
        }

        [Test]
        public void Dpad_DirectionsAndLatestAxis()
        {
            Press(_gamepad.dpad.left);
            Assert.AreEqual(Direction.Left, _reader.HeldDirection);
            Press(_gamepad.dpad.down);
            Assert.AreEqual(Direction.Down, _reader.HeldDirection);
            Release(_gamepad.dpad.down);
            Release(_gamepad.dpad.left);
            Press(_gamepad.dpad.up);
            Assert.AreEqual(Direction.Up, _reader.HeldDirection);
            Press(_gamepad.dpad.right);
            Assert.AreEqual(Direction.Right, _reader.HeldDirection);
        }

        [Test]
        public void SwitchAndPause_WorkFromKeyboardAndGamepad()
        {
            var switches = 0;
            var pauses = 0;
            _reader.SwitchShip += () => switches++;
            _reader.Pause += () => pauses++;
            PressAndRelease(_keyboard.spaceKey);
            PressAndRelease(_keyboard.tabKey);
            PressAndRelease(_gamepad.buttonSouth);
            PressAndRelease(_keyboard.escapeKey);
            PressAndRelease(_gamepad.startButton);
            Assert.AreEqual(3, switches);
            Assert.AreEqual(2, pauses);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Restart_RequiresHalfSecond_AndFiresOnceUntilReleased(bool gamepad)
        {
            var count = 0;
            _reader.Restart += () => count++;
            var button = gamepad ? _gamepad.selectButton : _keyboard.rKey;
            Press(button);
            currentTime += 0.2;
            InputSystem.Update();
            Assert.That(_reader.RestartProgress, Is.GreaterThan(0).And.LessThan(1));
            Release(button);
            Assert.AreEqual(0, count, "A short press must not restart.");
            Press(button);
            currentTime += 0.49;
            InputSystem.Update();
            Assert.AreEqual(0, count);
            currentTime += 0.02;
            InputSystem.Update();
            Assert.AreEqual(1, count);
            currentTime += 2;
            InputSystem.Update();
            Assert.AreEqual(1, count);
        }

        [Test]
        public void RestartDurationOverride_DoesNotChangeSourceAsset()
        {
            _reader.Dispose();
            var before = _source.ToJson();
            _reader = new InputReader(_source, 0.8f);
            _reader.Enable();
            var count = 0;
            _reader.Restart += () => count++;
            Press(_keyboard.rKey);
            currentTime += 0.6;
            InputSystem.Update();
            Assert.AreEqual(0, count);
            currentTime += 0.21;
            InputSystem.Update();
            Assert.AreEqual(1, count);
            Assert.AreEqual(before, _source.ToJson());
        }

        private static Simulation Simulation()
        {
            const string grid = "##############\n#AAAA....2222#\n#AAAA....2222#\n#............#\n#KK........11#\n#KK........11#\n##############";
            var definition = LevelParser.Parse(grid, 8);
            return new Simulation(() => definition.CreateState(new SimulationConfig(), 3, 90), new SimulationConfig());
        }

        [Test]
        public void TapReleasedBeforeSimulationTick_StillMakesExactlyOneStep()
        {
            var sim = Simulation();
            _reader.MoveChanged += sim.SetHeldDirection;
            PressAndRelease(_keyboard.dKey);
            Assert.IsNull(_reader.HeldDirection);
            sim.Tick(0.01f);
            Assert.AreEqual(new GridPos(2, 1), sim.State.GetShip(ShipId.Kestrel).Position);
            sim.Tick(0.3f);
            Assert.AreEqual(new GridPos(2, 1), sim.State.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void PausingClearsPendingTap_AndBlocksMoveAndSwitch_ButAllowsPause()
        {
            var sim = Simulation();
            _reader.MoveChanged += sim.SetHeldDirection;
            var switches = 0;
            var pauses = 0;
            _reader.SwitchShip += () => switches++;
            _reader.Pause += () => pauses++;
            PressAndRelease(_keyboard.dKey);
            _reader.SetGameplayEnabled(false);
            sim.ClearInput();
            PressAndRelease(_keyboard.aKey);
            PressAndRelease(_keyboard.spaceKey);
            PressAndRelease(_keyboard.escapeKey);
            Assert.IsNull(_reader.HeldDirection);
            Assert.AreEqual(0, switches);
            Assert.AreEqual(1, pauses);
            _reader.SetGameplayEnabled(true);
            sim.Tick(0.1f);
            Assert.AreEqual(new GridPos(1, 1), sim.State.GetShip(ShipId.Kestrel).Position);
        }

        [Test]
        public void DisableAndReenable_DoNotDuplicateCallbacks()
        {
            var switches = 0;
            _reader.SwitchShip += () => switches++;
            _reader.Disable();
            PressAndRelease(_keyboard.spaceKey);
            Assert.AreEqual(0, switches);
            _reader.Enable();
            _reader.Enable();
            PressAndRelease(_keyboard.spaceKey);
            Assert.AreEqual(1, switches);
        }
    }
}
