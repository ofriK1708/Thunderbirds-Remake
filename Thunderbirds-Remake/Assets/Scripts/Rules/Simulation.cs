using System;
using System.Collections.Generic;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// The real rules. Runs the rule systems in the fixed tick order (GDD §3) and raises events;
    /// it holds no rules itself. SKELETON: each step below is filled in by the issue named on it.
    /// </summary>
    public sealed class Simulation : ISimulation
    {
        private readonly Func<SimulationState> _buildState;
        private readonly Func<SimulationConfig> _buildConfig;
        private SimulationConfig _config;
        private readonly EventHub _events = new EventHub();

        private SimulationState _state;

        // Input (Model 2, issue #3): the held direction, plus a latch so a tap shorter
        // than one step still produces one step. Consumed by the ship-move step.
        private Direction? _heldDirection;
        private Direction? _pressedSinceLastStep;

        // Ship stepping (#7). Seconds until the active ship may step; <= 0 means ready.
        private float _stepTimer;

        // Refusal feedback (#7, move-flow.md Q5): bump once per hold, retry silently while held,
        // and raise RefusalHint once after RefusalHintSeconds of pushing into the refusal.
        private Direction? _refusedDirection;
        private float _refusedSeconds;
        private bool _hintRaised;

        public IReadOnlySimulationState State => _state;
        public ISimulationEvents Events => _events;

        /// <param name="buildState">
        /// Builds the starting state; called on construction and on every Restart.
        /// Will come from LevelDefinition once the parser (#4) exists.
        /// </param>
        /// <param name="buildConfig">
        /// Reads the tuning values; called on construction and on every Restart, so Inspector edits to
        /// the GameConfig / ShipConfig assets apply on the next Restart without leaving Play mode.
        /// </param>
        public Simulation(Func<SimulationState> buildState, Func<SimulationConfig> buildConfig)
        {
            _buildState = buildState ?? throw new ArgumentNullException(nameof(buildState));
            _buildConfig = buildConfig ?? throw new ArgumentNullException(nameof(buildConfig));
            _config = LoadConfig();
            _state = _buildState();
        }

        /// <summary>Fixed config (tests); Restart keeps the same values.</summary>
        public Simulation(Func<SimulationState> buildState, SimulationConfig config)
            : this(buildState, () => config)
        {
        }

        /// <summary>The tuning values in use since the last start or Restart.</summary>
        internal SimulationConfig Config => _config;

        private SimulationConfig LoadConfig()
        {
            var config = _buildConfig() ?? throw new InvalidOperationException("buildConfig returned null");
            config.Validate();
            return config;
        }

        public void Tick(float dt)
        {
            if (_state.Status != SimStatus.Playing) return; // frozen after Complete / Failed

            ApplyGravity(dt);           // 1
            MoveActiveShip(dt);         // 2
            UpdateCarryStates();        // 3
            UpdateLoadsAndCrush(dt);    // 4
            ResolveCrushesAndRespawns(); // 5
            TickOxygen(dt);             // 6
            if (!CheckFailure())        // 7 — failure is checked before success
                CheckSuccess();         // 8

            _events.Flush(); // listeners see the finished tick, never a half-updated grid
        }

        /// <summary>
        /// The direction the next ship step should take: the held one, or a tap that was
        /// released before its step came. Null means stay. The ship-move step (#7) clears the latch.
        /// </summary>
        internal Direction? NextStepDirection => _heldDirection ?? _pressedSinceLastStep;

        public void SetHeldDirection(Direction? direction)
        {
            if (direction.HasValue && direction != _heldDirection)
                _pressedSinceLastStep = direction;
            _heldDirection = direction;
        }

        public void SwitchShip()
        {
            if (_state.Status != SimStatus.Playing) return;

            var other = _state.ActiveShip == ShipId.Kestrel ? ShipId.Atlas : ShipId.Kestrel;
            if (_state.GetShip(other).IsGhost) return; // a ghost is not selectable (GDD §3)

            _state.ActiveShip = other;
            ForgetRefusal();
            _events.Raise(new ActiveShipChanged(other, automatic: false));
            _events.Flush();
        }

        public void Restart()
        {
            _config = LoadConfig(); // live tuning: pick up Inspector edits
            _state = _buildState();
            _heldDirection = null;
            _pressedSinceLastStep = null;
            _stepTimer = 0f;
            ForgetRefusal();
            _events.Clear();
        }

        // ------------------------------------------------------------ tick steps ----

        /// <summary>1. Blocks due to fall move one cell (falls win ties). Issue #8.</summary>
        private void ApplyGravity(float dt)
        {
            // TODO(#8): per-block fall timer (accumulator, _config.FallStepSeconds);
            // raise BlockFell per cell and BlockLanded when it stops.
        }

        /// <summary>2. The active ship steps in the held/latched direction. Issue #7.</summary>
        private void MoveActiveShip(float dt)
        {
            _stepTimer -= dt;
            var dir = NextStepDirection;
            if (dir == null)
            {
                if (_stepTimer < 0f) _stepTimer = 0f; // no banking steps while idle
                ForgetRefusal();
                return;
            }

            if (_refusedDirection.HasValue && _refusedDirection != dir) ForgetRefusal(); // a new push
            if (_refusedDirection.HasValue) _refusedSeconds += dt;

            if (_stepTimer > 0f) return;

            var shipId = _state.ActiveShip;
            var stepSeconds = _config.StepSeconds(shipId);
            _stepTimer += stepSeconds; // accumulator, not "= stepSeconds": speed stays frame-rate independent
            _pressedSinceLastStep = null;

            var from = _state.GetShip(shipId).Position;
            var result = MoveResolver.TryMove(_state, shipId, dir.Value, _config);
            if (result.Accepted)
            {
                ForgetRefusal();
                _events.Raise(new ShipMoved(shipId, from, _state.GetShip(shipId).Position, stepSeconds));
                var offset = dir.Value.ToOffset();
                foreach (var block in result.Chain)
                    _events.Raise(new BlockMoved(block.Id, block.Position - offset, block.Position, stepSeconds));
                return;
            }

            if (!_refusedDirection.HasValue)
            {
                _refusedDirection = dir;
                _events.Raise(new MoveRefused(shipId, dir.Value, result.Reason, IdsOf(result.Chain),
                    LevelDefinition.ColourOf(result.ChainWeight, _config)));
            }
            else if (!_hintRaised && _refusedSeconds >= _config.RefusalHintSeconds)
            {
                _hintRaised = true;
                _events.Raise(new RefusalHint(shipId, dir.Value, result.Reason, IdsOf(result.Chain)));
            }
        }

        private void ForgetRefusal()
        {
            _refusedDirection = null;
            _refusedSeconds = 0f;
            _hintRaised = false;
        }

        private static BlockId[] IdsOf(IReadOnlyList<BlockState> blocks)
        {
            var ids = new BlockId[blocks.Count];
            for (var i = 0; i < ids.Length; i++) ids[i] = blocks[i].Id;
            return ids;
        }

        /// <summary>3. Each block's CarriedBy from its supports. Issue #12.</summary>
        private void UpdateCarryStates()
        {
            // TODO(#12): CarryTracker.
        }

        /// <summary>4. Ship loads; start, reset or tick crush countdowns. Issue #13.</summary>
        private void UpdateLoadsAndCrush(float dt)
        {
            // TODO(#13): LoadTracker + CountdownTimer (_config.CrushGraceSeconds);
            // raise ShipStressed / ShipRelieved.
        }

        /// <summary>5. Crushes cost a life; ghosts materialise when their start area is clear. Issue #14.</summary>
        private void ResolveCrushesAndRespawns()
        {
            // TODO(#14): LivesAndRespawn; raise ShipCrushed, ShipRespawned, ActiveShipChanged(automatic: true).
        }

        /// <summary>6. Oxygen ticks down. Issue #15.</summary>
        private void TickOxygen(float dt)
        {
            // TODO(#15): CountdownTimer; raise OxygenChanged only when the whole second changes.
        }

        /// <summary>7. No lives left, or oxygen = 0. Returns true if the level failed. Issue #15.</summary>
        private bool CheckFailure()
        {
            // TODO(#15): set Status = Failed, raise LevelFailed(reason).
            return false;
        }

        /// <summary>8. Both ships on their docks. Issue #15.</summary>
        private void CheckSuccess()
        {
            // TODO(#15): WinChecker; set Status = Complete, raise LevelComplete.
        }
    }
}
