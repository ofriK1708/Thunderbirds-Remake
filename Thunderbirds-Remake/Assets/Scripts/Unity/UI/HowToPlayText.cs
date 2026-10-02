using System.Globalization;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// The config-dependent How to Play cards (GDD §5 screen 3). Built from the live GameConfig / ShipConfig
    /// values each time the screen opens, so retuning a capacity never leaves the instructions stale.
    /// </summary>
    public static class HowToPlayText
    {
        private const string Gold = "#E0A040";

        public static string Ships(ShipConfig kestrel, ShipConfig atlas, float kestrelSpeedRatio) =>
            $"<color={Gold}>{kestrel.displayName.ToUpperInvariant()}  /  2 x 2 cells</color>\n" +
            $"{SpeedLine(kestrelSpeedRatio)} Fits narrow gaps.\n" +
            $"Pushes {kestrel.pushCapacity}, carries {kestrel.loadCapacity} cells.\n\n" +
            $"<color={Gold}>{atlas.displayName.ToUpperInvariant()}  /  4 x 2 cells</color>\n" +
            "Large. Clears the way for Kestrel.\n" +
            $"Pushes {atlas.pushCapacity}, carries {atlas.loadCapacity} cells.\n\n" +
            "Both ships hover when stopped.";

        public static string Blocks(ShipConfig kestrel, ShipConfig atlas) =>
            $"<color=#25BCBC>TEAL</color>     up to {kestrel.pushCapacity}: either ship\n" +
            $"<color=#FFFF40>YELLOW</color>  up to {atlas.pushCapacity}: Atlas only\n" +
            $"<color=#E04040>RED</color>       over {atlas.pushCapacity}: too heavy\n\n" +
            "Weight = number of occupied cells.\n" +
            "A push counts the whole chain,\nincluding blocks resting on top.\n" +
            "Unsupported blocks fall.\nBlocks never rotate or merge.";

        public static string Survival(float crushGraceSeconds, int lives, float respawnGhostSeconds) =>
            "Overloaded? A crush ring gives you\n" +
            $"{Seconds(crushGraceSeconds)} s: release the load, or switch\nships and push it away.\n\n" +
            $"A crush costs one of your {lives} {(lives == 1 ? "life" : "lives")}.\n" +
            $"It returns as a ghost for {Seconds(respawnGhostSeconds)} s:\nblocks fall through, it cannot move.\n\n" +
            "No lives or no oxygen = level failed.";

        private static string SpeedLine(float ratio)
        {
            if (ratio > 1.01f) return $"Small, {Seconds(ratio)}x as fast.";
            if (ratio < 0.99f) return $"Small, {Seconds(1f / ratio)}x slower.";
            return "Small, same speed.";
        }

        private static string Seconds(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
