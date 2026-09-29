using System.Collections;
using System.Linq;
using NUnit.Framework;
using Thunderbirds.Rules;
using Thunderbirds.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Thunderbirds.Tests.EditMode
{
    public class SandboxPreviewTests
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private InputSettings _originalSettings;
        private InputSettings _testSettings;

        [UnityTest]
        public IEnumerator PlayButton_OpensSandbox_WithMovementSwitchPauseAndRestart()
        {
            yield return new EnterPlayMode();
            // Batch tests have no focused Game view. Use an isolated settings copy for virtual input.
            _originalSettings = InputSystem.settings;
            _testSettings = Object.Instantiate(_originalSettings);
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var play = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.name == "Play Button");
            Assert.IsTrue(play.interactable);
            play.onClick.Invoke();
            yield return null;
            var selection = Object.FindFirstObjectByType<LevelSelectView>();
            Assert.IsNotNull(selection);
            selection.OnCancel(new BaseEventData(EventSystem.current));
            Assert.AreEqual(play.gameObject, EventSystem.current.currentSelectedGameObject);
            play.onClick.Invoke();
            Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b => b.name == "Sandbox Button").onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("Game", SceneManager.GetActiveScene().name);
            var preview = Object.FindFirstObjectByType<SandboxPreviewController>();
            Assert.IsNotNull(preview);
            Assert.IsNotNull(preview.State);
            Assert.AreEqual(2, preview.State.Ships.Count);
            Assert.AreEqual(3, preview.State.Blocks.Count);
            var rendererCount = preview.GetComponentsInChildren<SpriteRenderer>().Length;
            Assert.Greater(rendererCount, 5);

            _keyboard = InputSystem.AddDevice<Keyboard>();
            if (preview.IsPaused) { yield return Tap(Key.Escape); }
            var start = preview.State.GetShip(ShipId.Kestrel).Position;
            SetKeys(Key.D);
            yield return new WaitForSeconds(0.18f);
            SetKeys();
            yield return null;
            Assert.IsFalse(preview.IsPaused, "Sandbox should remain unpaused during movement.");
            Assert.Greater(preview.State.GetShip(ShipId.Kestrel).Position.X, start.X, "Initial keyboard movement");
            var renderer = preview.transform.Find("Kestrel").GetComponent<SpriteRenderer>();
            Assert.AreEqual(GridSpace.FootprintCenter(preview.State.GetShip(ShipId.Kestrel).Position, 2, 2),
                renderer.transform.localPosition);

            yield return Tap(Key.Space);
            Assert.AreEqual(ShipId.Atlas, preview.State.ActiveShip);
            // Block c rests only on Atlas, so Atlas carries it (GDD §3 Carrying, #12): it travels along, it doesn't fall.
            var overheadBlock = preview.State.GetBlock(new BlockId('c'));
            var atlas = preview.State.GetShip(ShipId.Atlas);
            Assert.AreEqual(ShipId.Atlas, overheadBlock.CarriedBy);
            var blockStart = overheadBlock.Position;
            var atlasStartX = atlas.Position.X;
            SetKeys(Key.A);
            var carryDeadline = Time.realtimeSinceStartup + 2f;
            while (atlas.Position.X == atlasStartX && Time.realtimeSinceStartup < carryDeadline)
                yield return null;
            SetKeys();
            yield return null;
            var travelled = atlasStartX - atlas.Position.X;
            Assert.Greater(travelled, 0, $"Atlas should fly left. Atlas: {atlas.Position}, paused: {preview.IsPaused}");
            Assert.AreEqual(new GridPos(blockStart.X - travelled, blockStart.Y), overheadBlock.Position,
                "the carried block travels with Atlas");
            Assert.AreEqual(overheadBlock.Position.X, preview.transform.Find("Block c").localPosition.x);
            yield return Tap(Key.Escape);
            Assert.IsTrue(preview.IsPaused);
            var atlasPosition = preview.State.GetShip(ShipId.Atlas).Position;
            SetKeys(Key.A);
            yield return new WaitForSeconds(0.2f);
            SetKeys();
            Assert.AreEqual(atlasPosition, preview.State.GetShip(ShipId.Atlas).Position);

            // Restart while paused must also re-enable movement, and reuse the visual objects.
            SetKeys(Key.R);
            // Hold interactions use real input time, independently of the test runner's game time.
            var restartDeadline = Time.realtimeSinceStartup + 2f;
            while (preview.IsPaused && Time.realtimeSinceStartup < restartDeadline)
            {
                InputSystem.Update();
                yield return null;
            }
            Assert.IsFalse(preview.IsPaused, $"Restart hold: {preview.RestartProgress}");
            SetKeys();
            yield return null;
            Assert.IsFalse(preview.IsPaused);
            Assert.AreEqual(ShipId.Kestrel, preview.State.ActiveShip);
            Assert.AreEqual(start, preview.State.GetShip(ShipId.Kestrel).Position);
            Assert.AreEqual(rendererCount, preview.GetComponentsInChildren<SpriteRenderer>().Length);
            SetKeys(Key.D);
            yield return new WaitForSeconds(0.18f);
            SetKeys();
            Assert.Greater(preview.State.GetShip(ShipId.Kestrel).Position.X, start.X);

            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            Assert.IsNull(Object.FindFirstObjectByType<SandboxPreviewController>());
            Assert.IsNotNull(Object.FindFirstObjectByType<MainMenuView>());

            // Exercise the real menu's shared UI map, including submit/cancel and a gamepad launch.
            yield return TapMenu(Key.DownArrow);
            Assert.AreEqual("How to Play Button", EventSystem.current.currentSelectedGameObject.name);
            yield return TapMenu(Key.Enter);
            Assert.IsNotNull(Object.FindFirstObjectByType<HowToPlayView>());
            yield return TapMenu(Key.Escape);
            Assert.IsNull(Object.FindFirstObjectByType<HowToPlayView>());
            _gamepad = InputSystem.AddDevice<Gamepad>();
            yield return TapGamepad(GamepadButton.DpadUp);
            Assert.AreEqual("Play Button", EventSystem.current.currentSelectedGameObject.name);
            yield return TapGamepad(GamepadButton.South);
            Assert.IsNotNull(Object.FindFirstObjectByType<LevelSelectView>());
            Assert.AreEqual("Sandbox Button", EventSystem.current.currentSelectedGameObject.name);
            yield return TapGamepad(GamepadButton.South);
            yield return null;
            var gamepadPreview = Object.FindFirstObjectByType<SandboxPreviewController>();
            Assert.IsNotNull(gamepadPreview);
            if (gamepadPreview.IsPaused) yield return TapGamepad(GamepadButton.Start);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = Vector2.right });
            InputSystem.Update();
            var moveDeadline = Time.realtimeSinceStartup + 3f;
            while (gamepadPreview.State.GetShip(ShipId.Kestrel).Position.X == 1 && Time.realtimeSinceStartup < moveDeadline)
                yield return null;
            Assert.Greater(gamepadPreview.State.GetShip(ShipId.Kestrel).Position.X, 1);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        private void SetKeys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private IEnumerator Tap(Key key)
        {
            SetKeys(key);
            yield return null;
            SetKeys();
            yield return null;
        }

        private IEnumerator TapGamepad(GamepadButton button)
        {
            InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            yield return null;
        }

        private IEnumerator TapMenu(Key key)
        {
            // UI submit is polled during EventSystem.Update. Let the normal player loop process
            // the event so a manual input update cannot clear WasPerformedThisFrame too early.
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_gamepad != null && _gamepad.added) InputSystem.RemoveDevice(_gamepad);
            _gamepad = null;
            if (_originalSettings != null) InputSystem.settings = _originalSettings;
            if (_testSettings != null) Object.DestroyImmediate(_testSettings);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
