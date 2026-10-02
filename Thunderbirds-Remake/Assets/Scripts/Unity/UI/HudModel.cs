using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>The numbers behind the HUD (GDD §5 Game HUD), kept out of the view so they can be tested.</summary>
    public static class HudModel
    {
        /// <summary>How full the oxygen bar is, 0..1.</summary>
        public static float OxygenFraction(float remaining, float total) =>
            total <= 0f ? 0f : Mathf.Clamp01(remaining / total);

        /// <summary>Whole seconds shown next to the bar: rounds up, so "1" is shown until the level actually fails.</summary>
        public static int OxygenSeconds(float remaining) => Mathf.Max(0, Mathf.CeilToInt(remaining));

        /// <summary>The read-out turns red below <paramref name="lowSeconds"/> (GameConfig.lowOxygenSeconds).</summary>
        public static bool IsOxygenLow(float remaining, float lowSeconds) => remaining < lowSeconds;

        /// <summary>How much of the crush ring is left, 1 = just stressed, 0 = crushed.</summary>
        public static float CrushRingFill(float secondsLeft, float graceSeconds) =>
            graceSeconds <= 0f ? 0f : Mathf.Clamp01(secondsLeft / graceSeconds);
    }
}
