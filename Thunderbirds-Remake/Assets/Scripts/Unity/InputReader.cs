using System;
using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using Object = UnityEngine.Object;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Owns an independent copy of the project's actions. Controllers receive named commands, never keys.
    /// Move changes are delivered immediately, including release, preserving the simulation's short-tap latch.
    /// </summary>
    public sealed class InputReader : IDisposable
    {
        private readonly InputActionAsset _asset;
        private readonly InputActionMap _player;
        private readonly InputAction _move, _switch, _restart, _pause;
        private readonly List<InputActionReference> _uiReferences = new List<InputActionReference>();
        private int _x, _y;
        private bool _horizontalLast = true;
        private bool _enabled, _gameplayEnabled = true, _disposed;

        public event Action<Direction?> MoveChanged;
        public event Action SwitchShip;
        public event Action Restart;
        public event Action Pause;
        public Direction? HeldDirection { get; private set; }
        public float RestartProgress => _restart.phase == InputActionPhase.Started
            ? _restart.GetTimeoutCompletionPercentage() : 0f;

        public InputReader(InputActionAsset source, float restartHoldSeconds = 0.5f)
        {
            if (source == null) throw new ArgumentNullException(nameof(source), "Assign the project Input Actions asset.");
            if (float.IsNaN(restartHoldSeconds) || float.IsInfinity(restartHoldSeconds) || restartHoldSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(restartHoldSeconds));
            _asset = Object.Instantiate(source);
            _asset.Disable();
            _player = _asset.FindActionMap("Player", true);
            _move = _player.FindAction("Move", true);
            _switch = _player.FindAction("SwitchShip", true);
            _restart = _player.FindAction("Restart", true);
            _pause = _player.FindAction("Pause", true);
            _restart.ApplyParameterOverride("hold:duration", restartHoldSeconds);
            _move.performed += OnMove;
            _move.canceled += OnMove;
            _switch.performed += OnSwitch;
            _restart.performed += OnRestart;
            _pause.performed += OnPause;
        }

        public void Enable()
        {
            _enabled = true;
            _restart.Enable();
            _pause.Enable();
            SetGameplayEnabled(_gameplayEnabled);
        }

        public void Disable()
        {
            _enabled = false;
            _player.Disable();
            ResetDirection();
        }

        /// <summary>Pause gates movement/switching while keeping Pause and hold-to-Restart available.</summary>
        public void SetGameplayEnabled(bool enabled)
        {
            _gameplayEnabled = enabled;
            if (_enabled && enabled) { _move.Enable(); _switch.Enable(); }
            else { _move.Disable(); _switch.Disable(); ResetDirection(); }
        }

        private void OnMove(InputAction.CallbackContext context)
        {
            if (!_enabled || !_gameplayEnabled) return;
            var value = context.ReadValue<Vector2>();
            var x = Axis(value.x);
            var y = Axis(value.y);
            // Only a new press/sign change claims priority. Analog noise on a held axis cannot steal it.
            if (x != 0 && x != _x) _horizontalLast = true;
            if (y != 0 && y != _y && !(x != 0 && x != _x)) _horizontalLast = false;
            _x = x;
            _y = y;
            Direction? direction = null;
            if (x != 0 && (y == 0 || _horizontalLast)) direction = x > 0 ? Direction.Right : Direction.Left;
            else if (y != 0) direction = y > 0 ? Direction.Up : Direction.Down;
            if (HeldDirection == direction) return;
            HeldDirection = direction;
            MoveChanged?.Invoke(direction);
        }

        private static int Axis(float value) => value > 0.5f ? 1 : value < -0.5f ? -1 : 0;

        private void ResetDirection()
        {
            _x = _y = 0;
            _horizontalLast = true;
            HeldDirection = null;
            MoveChanged?.Invoke(null);
        }

        private void OnSwitch(InputAction.CallbackContext context) => SwitchShip?.Invoke();
        private void OnRestart(InputAction.CallbackContext context) => Restart?.Invoke();
        private void OnPause(InputAction.CallbackContext context) => Pause?.Invoke();

        /// <summary>Use the same asset's UI map for mouse, keyboard and gamepad menu navigation.</summary>
        public void ConfigureUI(InputSystemUIInputModule module)
        {
            module.actionsAsset = _asset;
            module.move = UI("Navigate");
            module.submit = UI("Submit");
            module.cancel = UI("Cancel");
            module.point = UI("Point");
            module.leftClick = UI("Click");
            module.rightClick = UI("RightClick");
            module.middleClick = UI("MiddleClick");
            module.scrollWheel = UI("ScrollWheel");
            module.trackedDevicePosition = UI("TrackedDevicePosition");
            module.trackedDeviceOrientation = UI("TrackedDeviceOrientation");
        }

        private InputActionReference UI(string name)
        {
            var reference = InputActionReference.Create(_asset.FindAction("UI/" + name, true));
            _uiReferences.Add(reference);
            return reference;
        }

        public void Dispose()
        {
            if (_disposed) return;
            Disable();
            _asset.Disable();
            _move.performed -= OnMove;
            _move.canceled -= OnMove;
            _switch.performed -= OnSwitch;
            _restart.performed -= OnRestart;
            _pause.performed -= OnPause;
            foreach (var reference in _uiReferences) Destroy(reference);
            Destroy(_asset);
            _disposed = true;
        }

        private static void Destroy(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
    }
}
