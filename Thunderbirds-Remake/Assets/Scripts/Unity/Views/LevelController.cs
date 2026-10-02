using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Runs one level in Game.unity (GDD §7 LevelController): owns the Simulation and ticks it every frame,
    /// connects the shared InputReader, creates and rebinds the ship and block views, shows the overlays and
    /// reports completion to GameManager. Used for campaign levels and the sandbox alike (campaignIndex -1).
    /// Holds no rules: every decision about a move is the Simulation's.
    /// </summary>
    public sealed class LevelController : MonoBehaviour
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
        private string _levelTitle;
        private string _levelHint;
        private LevelDefinition _definition;
        private LevelData _data;
        private LevelOverlayView _overlays;
        private HudView _hud;
        private LevelAudio _audio;
        private LevelView _levelView;
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
            _simulation.Events.RefusalHint += OnRefusalHint;
            _simulation.Events.LevelComplete += OnLevelComplete;
            _simulation.Events.LevelFailed += OnLevelFailed;
            _camera = Camera.main;
            _levelView = GetComponent<LevelView>();
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
                var art = ship.Id == ShipId.Kestrel ? config.kestrel : config.atlas;
                if (art != null && art.sideSprite != null)
                    view.SetArt(art.sideSprite, art.turnSprite, art.frontSprite,
                        art.sideFlame, art.turnFlame, art.frontFlame);
                else
                {
                    // Placeholder rectangle until the ShipConfig has sprites.
                    body.transform.localScale = new Vector3(ship.Width - 0.12f, ship.Height - 0.12f, 1);
                    body.color = PlaceholderColour(ship.Id);
                }
                view.Bind(ship, _simulation.Events, config, body, ship.Id == State.ActiveShip);
                _ships.Add(ship.Id, view);
            }
            CreateActions();
            CreateOverlays();
            CreateHud();
            var sound = AudioManager.Instance;
            _audio = new LevelAudio(sound, () => State, _simulation.Events, sound.IdleEngineLevel);
            RefreshVisuals();
        }

        private static Color PlaceholderColour(ShipId ship) =>
            ship == ShipId.Kestrel ? new Color(0.35f, 0.8f, 1f) : new Color(1f, 0.65f, 0.3f);

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

        private void CreateHud()
        {
            var font = _overlays.GetComponentInChildren<TMPro.TMP_Text>(true)?.font; // same face as the menus
            _hud = HudView.Create(transform, font, _config, InputSystem.actions);
            foreach (var ship in State.Ships)
            {
                // A fixed picture for the HUD: the side view, never the live frame (which turns, flips and flashes).
                var art = ship.Id == ShipId.Kestrel ? _config.kestrel : _config.atlas;
                var body = _ships[ship.Id].Body;
                var hasArt = art != null && art.sideSprite != null;
                _hud.AttachShip(ship.Id, _ships[ship.Id].transform, ship.Height,
                    !hasArt ? body.sprite : art.portraitSprite != null ? art.portraitSprite : art.sideSprite,
                    hasArt ? Color.white : PlaceholderColour(ship.Id));
            }
            _hud.Bind(() => State, _levelTitle, _levelHint);
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
            _hud.SetRestartProgress(RestartProgress);
            _hud.Tick(_paused ? 0f : Time.deltaTime); // the hint and messages wait while paused
            _audio.Update(Time.deltaTime, _paused);
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            foreach (var block in State.Blocks)
                _blocks[block.Id].SyncPosition(block);
            foreach (var ship in State.Ships)
                _levelView.SetDockLit(ship.Id, !ship.IsGhost && ship.Position == ship.Dock);
            if (_camera != null) CameraFit.Apply(_camera, transform, State.Width, State.Height, _config.tombVoid);
        }

        private void OnMoveRefused(MoveRefused refused)
        {
            _hud.ShowMessage(RefusalMessage(refused.Ship, refused.Reason, refused.ChainColour), 3f);
            if (refused.Reason != RefuseReason.TooHeavy) return;
            // The whole chain shows the class of its total weight: red when no ship could move it (GDD §3 Push).
            var colour = _config.ColourOf(refused.ChainColour);
            foreach (var id in refused.Chain)
                if (_blocks.TryGetValue(id, out var view)) view.Flash(colour, _config.refusalFlashSeconds);
        }

        /// <summary>The player kept pushing into a refusal: say what would work instead.</summary>
        private void OnRefusalHint(RefusalHint hint)
        {
            var message = hint.Reason == RefuseReason.TooHeavy
                ? "Still too heavy. Push fewer blocks at once, or move one out of the row first."
                : "Still blocked. Something solid is behind it: try from the other side.";
            _hud.ShowMessage(message, 5f);
        }

        /// <summary>
        /// What to tell the player after a refused move. <paramref name="chainColour"/> is the class of the whole
        /// chain's weight, so "try Atlas" is only said when Atlas really could push it.
        /// </summary>
        public static string RefusalMessage(ShipId ship, RefuseReason reason, ColourClass chainColour)
        {
            if (reason == RefuseReason.FallWonTie) return "A falling block got there first. Try again.";
            if (reason != RefuseReason.TooHeavy) return "Blocked. Try another direction.";
            if (chainColour == ColourClass.TooHeavy) return "Too heavy for either ship. Push fewer blocks at once.";
            return ship == ShipId.Kestrel ? "Too heavy for Kestrel. Try Atlas." : "Too heavy for Atlas.";
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
            _hud.Bind(() => State, _levelTitle, _levelHint); // new state object, hint shown again
            _audio.Reset();
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
            _overlays.Catalog.Get(_campaignIndex + 1) != null && GameManager.Instance.IsUnlocked(_campaignIndex + 1);

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
            GameManager.Instance.ReturnToMenu(openLevelSelect: select);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && _simulation != null) SetPaused(true);
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
            if (_simulation != null) _simulation.Events.RefusalHint -= OnRefusalHint;
            _input?.Dispose();
            _audio?.Dispose(); // engines and the overload alarm must not carry on into the menu
            _uiInput?.Dispose();
            if (_ownedEventSystem != null) Destroy(_ownedEventSystem);
        }

        private void OnLevelComplete(LevelComplete completed)
        {
            if (_campaignIndex >= 0) GameManager.Instance.CompleteLevel(_campaignIndex);
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
