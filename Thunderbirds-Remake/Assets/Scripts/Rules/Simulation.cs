using System;

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
            _events.Raise(new ActiveShipChanged(other, automatic: false));
            _events.Flush();
        }

        public void Restart()
        {
            _config = LoadConfig(); // live tuning: pick up Inspector edits
            _state = _buildState();
            _heldDirection = null;
            _pressedSinceLastStep = null;
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
            // TODO(#7): per-ship step accumulator (timer -= _config.StepSeconds(ship), never = 0);
            // direction = NextStepDirection; after stepping, set _pressedSinceLastStep = null.
            // Push / lift / carry / release via MoveResolver; raise ShipMoved, BlockMoved,
            // BlockReleased or MoveRefused.
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
