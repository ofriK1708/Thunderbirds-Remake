using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

        public IReadOnlySimulationState State => _simulation?.State;
        public bool IsPaused => _paused;
        public float RestartProgress => _input?.RestartProgress ?? 0f;

        public void Initialize(GameConfig config, LevelData data, LevelDefinition definition, SpriteRenderer cellPrefab)
        {
            _config = config;
            _simulation = new Simulation(
                () => definition.CreateState(config.ToSimulationConfig(), config.livesPerLevel, config.OxygenFor(data)),
                config.ToSimulationConfig);
            _simulation.Events.MoveRefused += OnMoveRefused;
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
                view.Bind(ship, _simulation.Events, config, body);
                _ships.Add(ship.Id, view);
            }
            CreateActions();
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

        private void TogglePause() => SetPaused(!_paused);

        private void Update()
        {
            if (_simulation == null) return;
            if (!_paused) _simulation.Tick(Time.deltaTime);
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            foreach (var block in State.Blocks)
                _blocks[block.Id].SyncPosition(block);
            foreach (var ship in State.Ships)
            {
                // ShipView slides itself from ShipMoved events; the sandbox only tints the active ship.
                var renderer = _ships[ship.Id].Body;
                var colour = ship.Id == ShipId.Kestrel ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.65f, 0.3f);
                renderer.color = ship.Id == State.ActiveShip ? Color.Lerp(colour, Color.white, 0.35f) : colour * 0.7f;
            }
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
            _simulation.Restart();
            foreach (var block in State.Blocks)
            {
                var view = _blocks[block.Id];
                view.Build(block, _config, _blockSprite, _blockMaterial);
            }
            // Restart builds new ShipState objects: rebind so each view follows the new ship.
            foreach (var ship in State.Ships)
                _ships[ship.Id].Bind(ship, _simulation.Events, _config);
            SetPaused(false);
            _feedbackUntil = 0;
            RefreshVisuals();
        }

        private void SetPaused(bool paused)
        {
            _paused = paused;
            _input.SetGameplayEnabled(!paused);
            if (paused) _simulation.ClearInput();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _simulation != null) SetPaused(true);
        }

        private void OnGUI()
        {
            if (_simulation == null) return;
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width / 1280f, Screen.height / 720f, 1));
            GUI.Box(new Rect(16, 12, 1248, 100), "MOVEMENT SANDBOX");
            GUI.Label(new Rect(32, 38, 800, 25), $"Active ship: {State.ActiveShip}    |    {(_paused ? "PAUSED" : "Move and push blocks to test the level")}");
            if (RestartProgress > 0f)
                GUI.Label(new Rect(920, 38, 320, 25), $"Hold to restart: {RestartProgress:P0}");
            GUI.Label(new Rect(32, 67, 1200, 30), "WASD / arrows: move    Space / Tab: switch    Hold R: restart    Esc: pause    |    Gamepad: stick / D-pad, A, View, Menu");
            GUI.Box(new Rect(16, 610, 1248, 98), "");
            GUI.Label(new Rect(32, 620, 930, 26), Time.unscaledTime < _feedbackUntil ? _feedback : "Move and push blocks. Unsupported blocks and stacks fall automatically.");
            GUI.Label(new Rect(32, 652, 900, 26), "Partial game: lifting, oxygen and mission endings are not active yet.");
            if (GUI.Button(new Rect(1000, 625, 110, 32), _paused ? "Resume" : "Pause")) SetPaused(!_paused);
            if (GUI.Button(new Rect(1120, 625, 125, 32), "Restart")) Restart();
            if (GUI.Button(new Rect(1000, 663, 245, 30), "Main menu")) SceneManager.LoadScene("MainMenu");
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
            if (_simulation != null) _simulation.Events.MoveRefused -= OnMoveRefused;
            _input?.Dispose();
        }
    }
}
