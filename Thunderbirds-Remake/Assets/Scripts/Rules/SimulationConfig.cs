using System;

namespace Thunderbirds.Rules
{
    /// <summary>
    /// Tuning values the rules need, as plain C# (R4: the rules layer can't read Unity config assets).
    /// Tune the game in the Inspector, on the GameConfig / ShipConfig assets (#2): LevelController copies
    /// those values into a new SimulationConfig on every level start and Restart. The defaults below
    /// (GDD §3 first guesses) are only used by tests; editing them does not change the game.
    /// </summary>
    public sealed class SimulationConfig
    {
        public float AtlasMoveStepSeconds = 0.16f;
        public float KestrelSpeedRatio = 2f;
        public float FallStepSeconds = 0.10f;
        public float CrushGraceSeconds = 3f;

        /// <summary>Keep pushing into a refused move this long and the simulation raises RefusalHint.</summary>
        public float RefusalHintSeconds = 5f;

        public int KestrelPushCapacity = 4;
        public int AtlasPushCapacity = 8;
        public int KestrelLoadCapacity = 4;
        public int AtlasLoadCapacity = 8;

        public float StepSeconds(ShipId ship) =>
            ship == ShipId.Atlas ? AtlasMoveStepSeconds : AtlasMoveStepSeconds / KestrelSpeedRatio;

        public int PushCapacity(ShipId ship) => ship == ShipId.Atlas ? AtlasPushCapacity : KestrelPushCapacity;

        public int LoadCapacity(ShipId ship) => ship == ShipId.Atlas ? AtlasLoadCapacity : KestrelLoadCapacity;

        public void Validate()
        {
            if (AtlasMoveStepSeconds <= 0f) throw new ArgumentException("AtlasMoveStepSeconds must be > 0");
            if (KestrelSpeedRatio <= 0f) throw new ArgumentException("KestrelSpeedRatio must be > 0");
            if (FallStepSeconds <= 0f) throw new ArgumentException("FallStepSeconds must be > 0");
        }
    }
}
