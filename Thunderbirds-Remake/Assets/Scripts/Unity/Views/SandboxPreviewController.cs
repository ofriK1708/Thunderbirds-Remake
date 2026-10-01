using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Temporary playable preview for LevelView.previewLevel. Delegates all movement to Simulation;
    /// creates simple reusable visuals once, without editing either team-owned scene.
    /// Replaced by the production LevelController and entity views as they arrive. Uses the shared InputReader.
    /// </summary>
    public sealed class SandboxPreviewController : MonoBehaviour
    {
        private GameConfig _config;
        private Simulation _simulation;
        private InputReader _input;
        private readonly Dictionary<ShipId, ShipView> _ships = new Dictionary<ShipId, ShipView>();
        private readonly Dictionary<BlockId, BlockView> _blocks = new Dictionary<BlockId, BlockView>();
        private Camera _camera;
        private Sprite _blockSprite;
        private Material _blockMaterial;
        private bool _paused;
        private string _feedback = "Move beside a block and push left or right.";
        private float _feedbackUntil;
        private string _levelTitle;
        private string _levelHint;
        private LevelDefinition _definition;
        private LevelData _data;
        private LevelOverlayView _overlays;
        private GameObject _ownedEventSystem;
        private InputReader _uiInput;

        public IReadOnlySimulationState State => _simulation?.State;
        public bool IsPaused => _paused;
        public float RestartProgress => _input?.RestartProgress ?? 0f;

        private int _campaignIndex = -1;

        public void Initialize(GameConfig config, LevelData data, LevelDefinition definition, SpriteRenderer cellPrefab, int campaignIndex = -1)
        {
            _campaignIndex = campaignIndex;
            _data = data;
            _definition = definition;
            _levelTitle = data.displayName;
            _levelHint = string.IsNullOrWhiteSpace(data.hintText)
                ? "Move and push blocks. Unsupported blocks and stacks fall automatically." : data.hintText;
            _config = config;
            _simulation = new Simulation(
                () => _definition.CreateState(config.ToSimulationConfig(), config.livesPerLevel, config.OxygenFor(_data)),
                config.ToSimulationConfig);
            _simulation.Events.MoveRefused += OnMoveRefused;
            _simulation.Events.LevelComplete += OnLevelComplete;
            _simulation.Events.LevelFailed += OnLevelFailed;
            _camera = Camera.main;
            _blockSprite = cellPrefab.sprite;
            _blockMaterial = cellPrefab.sharedMaterial;

            foreach (var block in State.Blocks)
            {
                var view = new GameObject($"Block {block.Id.Letter}").AddComponent<BlockView>();
                view.transform.SetParent(transform, false);
                view.Build(block, config, cellPrefab.sprite, cellPrefab.sharedMaterial);
                _blocks.Add(block.Id, view);
            }
            foreach (var ship in State.Ships)
            {
                var view = new GameObject(ship.Id.ToString()).AddComponent<ShipView>();
                view.transform.SetParent(transform, false);
                var body = Instantiate(cellPrefab, view.transform);
                body.name = "Body";
                body.sortingLayerName = "Ships";
                body.transform.localScale = new Vector3(ship.Width - 0.12f, ship.Height - 0.12f, 1);
                body.color = ship.Id == ShipId.Kestrel ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.65f, 0.3f);
                view.Bind(ship, _simulation.Events, config, body, ship.Id == State.ActiveShip);
                _ships.Add(ship.Id, view);
            }
            CreateActions();
            CreateOverlays();
            RefreshVisuals();
        }

        private void CreateActions()
        {
            _input = new InputReader(InputSystem.actions, _config.restartHoldSeconds);
            _input.MoveChanged += _simulation.SetHeldDirection;
            _input.SwitchShip += _simulation.SwitchShip;
            _input.Restart += Restart;
            _input.Pause += TogglePause;
            _input.Enable();
        }

        private void CreateOverlays()
        {
            var prefab = Resources.Load<LevelOverlayView>("LevelOverlays");
            if (prefab == null) throw new System.InvalidOperationException("Missing LevelOverlays prefab.");
            _overlays = Instantiate(prefab, transform);
            if (EventSystem.current == null)
                _ownedEventSystem = new GameObject("Game EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = EventSystem.current.GetComponent<InputSystemUIInputModule>();
            if (_ownedEventSystem != null)
            {
                _uiInput = new InputReader(InputSystem.actions);
                _uiInput.ConfigureUI(module);
            }
            _overlays.Bind(() => SetPaused(false), Restart, NextLevel, () => LeaveLevel(true), () => LeaveLevel(false), module);
        }

        private void TogglePause()
        {
            if (State.Status != SimStatus.Playing) return;
            if (_paused) _overlays.Cancel();
            else SetPaused(true);
        }

        private void Update()
        {
            if (_simulation == null) return;
            if (!_paused) _simulation.Tick(Time.deltaTime);
            _input.SetGameplayEnabled(!_paused && State.Status == SimStatus.Playing);
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            foreach (var block in State.Blocks)
                _blocks[block.Id].SyncPosition(block);
            if (_camera == null) return;
            _camera.orthographic = true;
            _camera.rect = new Rect(0, 0.16f, 1, 0.66f);
            _camera.backgroundColor = _config.tombVoid;
            _camera.transform.position = transform.TransformPoint(new Vector3(State.Width * 0.5f, State.Height * 0.5f, -10));
            _camera.orthographicSize = Mathf.Max(State.Height * 0.5f + 0.75f, (State.Width * 0.5f + 0.75f) / _camera.aspect);
        }

        private void OnMoveRefused(MoveRefused refused)
        {
            _feedback = refused.Reason == RefuseReason.FallWonTie
                ? "A falling block got there first. Try moving again."
                : refused.Reason == RefuseReason.TooHeavy
                ? "Too heavy for this ship. Switch to Atlas."
                : "Blocked. Try another direction. Lifting is not available yet.";
            _feedbackUntil = Time.unscaledTime + 3f;
        }

        private void Restart()
        {
            if (State.Status != SimStatus.Playing && !_overlays.IsReady) return;
            _simulation.Restart();
            foreach (var view in _blocks.Values) view.gameObject.SetActive(false);
            foreach (var block in State.Blocks)
            {
                if (!_blocks.TryGetValue(block.Id, out var view))
                {
                    view = new GameObject($"Block {block.Id.Letter}").AddComponent<BlockView>();
                    view.transform.SetParent(transform, false); _blocks.Add(block.Id, view);
                }
                view.gameObject.SetActive(true);
                view.Build(block, _config, _blockSprite, _blockMaterial);
            }
            // Restart builds new ShipState objects: rebind so each view follows the new ship.
            foreach (var ship in State.Ships)
                _ships[ship.Id].Bind(ship, _simulation.Events, _config, isActive: ship.Id == State.ActiveShip);
            _input.Enable();
            SetPaused(false);
            _feedbackUntil = 0;
            RefreshVisuals();
        }

        private void SetPaused(bool paused)
        {
            if (State.Status != SimStatus.Playing) return;
            _paused = paused;
            _input.SetGameplayEnabled(!paused && State.Status == SimStatus.Playing);
            if (paused) _simulation.ClearInput();
            if (_overlays != null) { if (paused) _overlays.ShowPause(); else _overlays.Hide(); }
        }

        private bool HasNext => _campaignIndex >= 0 && _overlays.Catalog != null &&
            _overlays.Catalog.Get(_campaignIndex + 1) != null && LevelProgress.IsUnlocked(_campaignIndex + 1);

        private void NextLevel()
        {
            if (State.Status != SimStatus.Complete || !_overlays.IsReady || !HasNext) return;
            _data = _overlays.Catalog.Get(++_campaignIndex);
            _definition = LevelParser.Parse(_data.grid, _config.atlas.pushCapacity);
            _levelTitle = _data.displayName; _levelHint = _data.hintText;
            GetComponent<LevelView>().Build(_definition);
            Restart();
        }

        private void LeaveLevel(bool select)
        {
            _simulation.ClearInput(); _input.Disable();
            LevelLaunch.Clear(); LevelLaunch.OpenSelection = select;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _simulation != null) SetPaused(true);
        }

        private void OnGUI()
        {
            if (_simulation == null || (_overlays != null && _overlays.IsVisible)) return;
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            GUI.Box(new Rect(16, 12, 1248, 100), _levelTitle);
            GUI.Label(new Rect(32, 38, 800, 25), $"Active ship: {State.ActiveShip}    |    {(_paused ? "PAUSED" : "Move and push blocks to test the level")}");
            if (RestartProgress > 0f)
                GUI.Label(new Rect(920, 38, 320, 25), $"Hold to restart: {RestartProgress:P0}");
            GUI.Label(new Rect(32, 67, 1200, 30), "WASD / arrows: move    Space / Tab: switch    Hold R: restart    Esc: pause    |    Gamepad: stick / D-pad, A, View, Menu");
            GUI.Box(new Rect(16, 610, 1248, 98), "");
            GUI.Label(new Rect(32, 620, 930, 26), Time.unscaledTime < _feedbackUntil ? _feedback : _levelHint);
            var outcome = State.Status == SimStatus.Complete ? "RESCUE COMPLETE - Restart to play again"
                : State.Status == SimStatus.Failed ? (State.LivesLeft <= 0 ? "CRUSHED" : "OUT OF OXYGEN") + " - Restart to retry"
                : "Dock both ships before oxygen runs out.";
            GUI.Label(new Rect(32, 652, 900, 26), $"Oxygen: {Mathf.CeilToInt(State.OxygenRemaining)}s    |    {outcome}");
            if (GUI.Button(new Rect(1000, 625, 245, 32), "Pause")) SetPaused(true);
            GUI.matrix = previousMatrix;
        }

        private void OnEnable() => _input?.Enable();

        private void OnDisable()
        {
            _input?.Disable();
            _simulation?.ClearInput();
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.Events.LevelComplete -= OnLevelComplete;
            if (_simulation != null) _simulation.Events.LevelFailed -= OnLevelFailed;
            if (_simulation != null) _simulation.Events.MoveRefused -= OnMoveRefused;
            _input?.Dispose();
            _uiInput?.Dispose();
            if (_ownedEventSystem != null) Destroy(_ownedEventSystem);
        }

        private void OnLevelComplete(LevelComplete completed)
        {
            if (_campaignIndex >= 0) LevelProgress.Complete(_campaignIndex);
            ShowOutcome(true, FailReason.OutOfOxygen);
        }

        private void OnLevelFailed(LevelFailed failed) => ShowOutcome(false, failed.Reason);
        private void ShowOutcome(bool complete, FailReason reason)
        {
            _simulation.ClearInput(); _input.Disable();
            _overlays.ShowOutcome(complete, reason, State.OxygenRemaining, complete && HasNext);
        }
    }
}
