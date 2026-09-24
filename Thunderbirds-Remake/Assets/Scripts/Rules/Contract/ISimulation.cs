namespace Thunderbirds.Rules
{
    /// <summary>
    /// The contract between the rules layer and the Unity layer (GDD §7, issue #3).
    /// Implemented by the real Simulation and by FakeSimulation; LevelController only knows this interface.
    /// </summary>
    public interface ISimulation
    {
        IReadOnlySimulationState State { get; }
        ISimulationEvents Events { get; }

        /// <summary>Advance time. Runs the fixed tick order, then notifies listeners.</summary>
        void Tick(float dt);

        /// <summary>
        /// The direction the player is holding, or null when released. The simulation decides
        /// when the active ship steps; a press shorter than one step still produces one step.
        /// </summary>
        void SetHeldDirection(Direction? direction);

        void SwitchShip();

        /// <summary>Full reset: rebuild from the level definition, lives and oxygen refilled, log cleared.</summary>
        void Restart();
    }
}
