using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// One level: the text grid (GDD §7 Level format), its oxygen and the start-of-level hint.
    /// The rules layer parses <see cref="grid"/> into a LevelDefinition (#4).
    /// </summary>
    [CreateAssetMenu(fileName = "LevelData", menuName = "Thunderbirds/Level Data")]
    public sealed class LevelData : ScriptableObject
    {
        public string displayName = "L1";

        [Tooltip("# wall  . empty  K/A ship starts  1/2 docks  a-z blocks. Top line = top row.")]
        [TextArea(8, 30)] public string grid =
            "##########\n" +
            "#........#\n" +
            "#KK....AA#\n" +
            "#KK....AA#\n" +
            "##########";

        [Tooltip("Oxygen in seconds. 0 = use GameConfig.defaultTimeLimitSeconds.")]
        [Min(0f)] public float timeLimitSeconds;

        [TextArea(2, 4)] public string hintText;
    }
}
