using Thunderbirds.Rules;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>
    /// One asset per ship (Kestrel, Atlas). Capacities also set the block colour thresholds (GDD §3).
    /// </summary>
    [CreateAssetMenu(fileName = "ShipConfig", menuName = "Thunderbirds/Ship Config")]
    public sealed class ShipConfig : ScriptableObject
    {
        public ShipId ship;
        public string displayName = "Kestrel";

        [Tooltip("Max chain weight (cells) this ship can push sideways.")]
        [Min(1)] public int pushCapacity = 4;

        [Tooltip("Max weight (cells) this ship can lift and carry before it is stressed.")]
        [Min(1)] public int loadCapacity = 4;
    }
}
