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
using UnityEditor;
using System.Reflection;

namespace Thunderbirds.Tests.EditMode
{
    public class SandboxPreviewTests
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private InputSettings _originalSettings;
        private InputSettings _testSettings;
        private bool? _originalRunInBackground;

        [UnityTest]
        public IEnumerator PlayButton_OpensSandbox_WithMovementSwitchPauseAndRestart()
        {
            yield return new EnterPlayMode();
            // An unfocused Editor does not tick the player loop, so Start (and LevelView's sandbox) never runs.
            _originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
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
            Assert.IsNotNull(preview, $"LevelView.Start has not run yet (frame {Time.frameCount}).");
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
            var kestrelView = preview.transform.Find("Kestrel").GetComponent<ShipView>();
            var slideDeadline = Time.realtimeSinceStartup + 1f;
            while (kestrelView.IsSliding && Time.realtimeSinceStartup < slideDeadline)
                yield return null;
            Assert.AreEqual(GridSpace.FootprintCenter(preview.State.GetShip(ShipId.Kestrel).Position, 2, 2),
                kestrelView.transform.localPosition, "the sprite lands exactly on the model position");

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
            Assert.AreEqual("L1 Button", EventSystem.current.currentSelectedGameObject.name);
            yield return TapGamepad(GamepadButton.South);
            yield return null;
            var gamepadPreview = Object.FindFirstObjectByType<SandboxPreviewController>();
            Assert.IsNotNull(gamepadPreview);
            var campaignStart = gamepadPreview.State.GetShip(ShipId.Kestrel).Position;
            Assert.AreEqual(new GridPos(2, 6), campaignStart, "gamepad launches the first campaign level");
            if (gamepadPreview.IsPaused) yield return TapGamepad(GamepadButton.Start);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState { leftStick = Vector2.right });
            InputSystem.Update();
            var moveDeadline = Time.realtimeSinceStartup + 3f;
            while (gamepadPreview.State.GetShip(ShipId.Kestrel).Position.X == campaignStart.X && Time.realtimeSinceStartup < moveDeadline)
                yield return null;
            Assert.Greater(gamepadPreview.State.GetShip(ShipId.Kestrel).Position.X, campaignStart.X);
            InputSystem.QueueStateEvent(_gamepad, new GamepadState());
            InputSystem.Update();
        }

        [UnityTest]
        public IEnumerator CampaignOverlays_NextLevelRetryAndLevelSelect_WorkWithoutReloadingGame()
        {
            yield return new EnterPlayMode();
            _originalRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            _originalSettings = InputSystem.settings;
            _testSettings = Object.Instantiate(_originalSettings);
            _testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = _testSettings;
            _gamepad = InputSystem.AddDevice<Gamepad>();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            var catalog = AssetDatabase.LoadAssetAtPath<LevelCatalog>("Assets/Levels/LevelCatalog.asset");
            var progressKey = LevelProgress.Key(0);
            var savedProgress = PlayerPrefs.HasKey(progressKey) ? (int?)PlayerPrefs.GetInt(progressKey) : null;
            Assert.IsTrue(LevelLaunch.Select(catalog, 0));
            yield return SceneManager.LoadSceneAsync("Game");
            yield return null;
            var preview = Object.FindFirstObjectByType<SandboxPreviewController>();
            var view = preview.GetComponentInChildren<LevelOverlayView>();
            var sim = (Simulation)typeof(SandboxPreviewController).GetField("_simulation", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(preview);
            try
            {
                preview.SendMessage("OnApplicationFocus", false);
                Assert.IsTrue(preview.IsPaused);
                var oxygen = sim.State.OxygenRemaining;
                yield return new WaitForSecondsRealtime(0.1f);
                Assert.AreEqual(oxygen, sim.State.OxygenRemaining);
                OverlayButton(view, "resume").onClick.Invoke();
                Assert.IsFalse(preview.IsPaused);
                foreach (var ship in sim.State.Ships) ship.Position = ship.Dock;
                sim.Tick(0);
                Assert.IsTrue(LevelProgress.IsCompleted(0));
                Assert.IsFalse(view.IsReady);
                InputSystem.QueueStateEvent(_gamepad, new GamepadState().WithButton(GamepadButton.South));
                InputSystem.Update();
                yield return new WaitForSecondsRealtime(1.2f);
                Assert.IsFalse(view.IsReady, "held submit must not activate Next Level when the timer expires");
                InputSystem.QueueStateEvent(_gamepad, new GamepadState());
                InputSystem.Update();
                yield return WaitForOverlay(view);
                var sceneHandle = SceneManager.GetActiveScene().handle;
                OverlayButton(view, "next").onClick.Invoke();
                Assert.AreEqual(sceneHandle, SceneManager.GetActiveScene().handle);
                Assert.AreSame(preview, Object.FindFirstObjectByType<SandboxPreviewController>());
                Assert.AreEqual(new GridPos(8, 1), sim.State.GetShip(ShipId.Kestrel).Start);
                Assert.AreEqual(SimStatus.Playing, sim.State.Status);
                Assert.IsFalse(view.IsVisible);
                sim.Tick(130);
                Assert.AreEqual(SimStatus.Failed, sim.State.Status);
                OverlayButton(view, "retry").onClick.Invoke();
                Assert.AreEqual(SimStatus.Failed, sim.State.Status, "early retry is ignored");
                yield return WaitForOverlay(view);
                OverlayButton(view, "retry").onClick.Invoke();
                Assert.AreEqual(SimStatus.Playing, sim.State.Status);
                Assert.AreEqual(120, sim.State.OxygenRemaining);
                Assert.AreEqual(3, sim.State.LivesLeft);
                SetKeys(Key.D);
                yield return new WaitForSecondsRealtime(0.2f);
                SetKeys();
                Assert.Greater(sim.State.GetShip(ShipId.Kestrel).Position.X, 8, "Retry restores gameplay input");
                preview.SendMessage("OnApplicationFocus", false);
                OverlayButton(view, "pauseSelect").onClick.Invoke();
                yield return null; yield return null;
                Assert.AreEqual("MainMenu", SceneManager.GetActiveScene().name);
                Assert.IsNotNull(Object.FindFirstObjectByType<LevelSelectView>());
            }
            finally
            {
                if (savedProgress.HasValue) PlayerPrefs.SetInt(progressKey, savedProgress.Value);
                else PlayerPrefs.DeleteKey(progressKey);
                PlayerPrefs.Save();
            }
        }

        private static Button OverlayButton(LevelOverlayView view, string name) =>
            view.GetComponentsInChildren<Button>(true).Single(b => b.name == name + " Button");
        private static IEnumerator WaitForOverlay(LevelOverlayView view)
        {
            var deadline = Time.realtimeSinceStartup + 4;
            while (!view.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(view.IsReady);
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
            if (_originalRunInBackground.HasValue) Application.runInBackground = _originalRunInBackground.Value;
            _originalRunInBackground = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
