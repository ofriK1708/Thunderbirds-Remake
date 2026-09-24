using System;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// An <see cref="ISimulation"/> with no rules: it replays a <see cref="FakeScript"/> so views can be
    /// built and tuned before the real rules exist. Views cannot tell it from the real Simulation.
    /// </summary>
    public sealed class FakeSimulation : ISimulation
    {
        private readonly Func<SimulationState> _buildState;
        private readonly FakeScript _script;
        private readonly EventHub _events = new EventHub();

        private SimulationState _state;
        private float _time;
        private int _nextStep;

        public IReadOnlySimulationState State => _state;
        public ISimulationEvents Events => _events;

        /// <summary>The last direction passed in; lets the input person check InputReader against the fake.</summary>
        public Direction? HeldDirection { get; private set; }

        /// <param name="buildState">Called on construction and on every Restart, so each run starts clean.</param>
        public FakeSimulation(Func<SimulationState> buildState, FakeScript script)
        {
            _buildState = buildState ?? throw new ArgumentNullException(nameof(buildState));
            _script = script ?? throw new ArgumentNullException(nameof(script));
            _state = _buildState();
        }

        public void Tick(float dt)
        {
            _time += dt;

            // while, not if: one long frame can pass several scripted steps.
            var steps = _script.Steps;
            while (_nextStep < steps.Count && steps[_nextStep].Time <= _time)
            {
                var e = steps[_nextStep].Event;
                Apply(e);
                _events.Raise(e);
                _nextStep++;
            }

            // Once per tick, like the real Simulation: listeners see the finished state.
            _events.Flush();
        }

        public void SetHeldDirection(Direction? direction)
        {
            HeldDirection = direction;
        }

        public void SwitchShip()
        {
            var next = _state.ActiveShip == ShipId.Kestrel ? ShipId.Atlas : ShipId.Kestrel;
            var e = new ActiveShipChanged(next, automatic: false);
            Apply(e);
            _events.Raise(e);
            _events.Flush();
        }

        public void Restart()
        {
            _state = _buildState();
            _time = 0f;
            _nextStep = 0;
            HeldDirection = null;
            _events.Clear();
        }

        /// <summary>Keep State consistent with what the script announced, so views reading State agree.</summary>
        private void Apply(SimEvent e)
        {
            switch (e)
            {
                case ShipMoved x: _state.GetShip(x.Ship).Position = x.To; break;
                case BlockMoved x: _state.GetBlock(x.Block).Position = x.To; break;
                case BlockFell x: _state.GetBlock(x.Block).Position = x.To; break;
                case ShipStressed x:
                {
                    var ship = _state.GetShip(x.Ship);
                    ship.IsStressed = true;
                    ship.Load = x.Load;
                    ship.CrushSecondsLeft = x.GraceSeconds;
                    break;
                }
                case ShipRelieved x:
                {
                    var ship = _state.GetShip(x.Ship);
                    ship.IsStressed = false;
                    ship.CrushSecondsLeft = 0f;
                    break;
                }
                case ShipCrushed x:
                {
                    _state.LivesLeft = x.LivesLeft;
                    var ship = _state.GetShip(x.Ship);
                    ship.IsStressed = false;
                    ship.CrushSecondsLeft = 0f;
                    break;
                }
                case ShipRespawned x:
                {
                    var ship = _state.GetShip(x.Ship);
                    ship.Position = x.At;
                    ship.IsGhost = x.IsGhost;
                    break;
                }
                case ActiveShipChanged x: _state.ActiveShip = x.Active; break;
                case OxygenChanged x: _state.OxygenRemaining = x.Remaining; break;
                case LevelComplete _: _state.Status = SimStatus.Complete; break;
                case LevelFailed _: _state.Status = SimStatus.Failed; break;
            }
        }
    }
}
