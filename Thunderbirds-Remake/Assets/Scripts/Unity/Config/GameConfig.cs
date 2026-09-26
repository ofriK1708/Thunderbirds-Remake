using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// Every global tuning value and the palette (GDD §3 Parameters, §6 Palette). This asset is the
    /// source of truth while playing; the rules only ever see the plain-C# copy from <see cref="ToSimulationConfig"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Thunderbirds/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Ships")]
        public ShipConfig kestrel;
        public ShipConfig atlas;

        [Header("Timing")]
        [Tooltip("Atlas's time per cell - the reference ship speed.")]
        [Min(0.01f)] public float atlasMoveStepSeconds = 0.16f;

        [Tooltip("Kestrel speed / Atlas speed (2 = Kestrel twice as fast).")]
        [Min(0.1f)] public float kestrelSpeedRatio = 2f;

        [Tooltip("Time per cell of falling. Decides which ship can outrun a falling block.")]
        [Min(0.01f)] public float fallStepSeconds = 0.10f;

        [Tooltip("Time to save a stressed ship before it is crushed.")]
        [Min(0.1f)] public float crushGraceSeconds = 3f;

        [Header("Level")]
        [Min(1)] public int livesPerLevel = 3;

        [Tooltip("Oxygen when a LevelData sets timeLimitSeconds = 0.")]
        [Min(1f)] public float defaultTimeLimitSeconds = 90f;

        [Header("Input & feedback")]
        [Min(0f)] public float restartHoldSeconds = 0.5f;
        [Min(0f)] public float switchHighlightSeconds = 1.5f;
        public float shipTiltDegrees = 8f;
        public float hoverBobAmplitude = 0.05f;
        public bool showPushPreview;

        [Header("Palette")]
        public Color tombVoid = Hex(0x101010);
        public Color sandstone = Hex(0xE0A040);
        public Color sandShadow = Hex(0x785828);
        public Color blockTeal = Hex(0x008080);
        public Color blockYellow = Hex(0xFFFF40);
        public Color blockRed = Hex(0xE04040);
        public Color dockLit = Hex(0x40E040);
        public Color uiMuted = Hex(0x888888);
        public Color uiText = Hex(0xFFFFEC);

        /// <summary>
        /// A fresh plain-C# copy for the rules layer (R4). LevelController passes this as the
        /// Simulation's buildConfig, so it runs again on every Restart and picks up Inspector edits.
        /// </summary>
        public SimulationConfig ToSimulationConfig()
        {
            return new SimulationConfig
            {
                AtlasMoveStepSeconds = atlasMoveStepSeconds,
                KestrelSpeedRatio = kestrelSpeedRatio,
                FallStepSeconds = fallStepSeconds,
                CrushGraceSeconds = crushGraceSeconds,
                KestrelPushCapacity = kestrel.pushCapacity,
                KestrelLoadCapacity = kestrel.loadCapacity,
                AtlasPushCapacity = atlas.pushCapacity,
                AtlasLoadCapacity = atlas.loadCapacity,
            };
        }

        /// <summary>Oxygen for a level: its own limit, or the default when it sets 0.</summary>
        public float OxygenFor(LevelData level) =>
            level != null && level.timeLimitSeconds > 0f ? level.timeLimitSeconds : defaultTimeLimitSeconds;

        public Color ColourOf(ColourClass colour)
        {
            switch (colour)
            {
                case ColourClass.Teal: return blockTeal;
                case ColourClass.Yellow: return blockYellow;
                default: return blockRed;
            }
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
