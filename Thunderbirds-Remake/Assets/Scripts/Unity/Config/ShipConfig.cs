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

        [Header("Art (drawn facing right, flat cargo deck on top)")]
        [Tooltip("Side profile, hull only. Leave empty to use the placeholder rectangle.")]
        public Sprite sideSprite;
        [Tooltip("Three-quarter view, shown for a moment while turning around. Optional.")]
        public Sprite turnSprite;
        [Tooltip("Front view, the middle frame of a turn. Optional.")]
        public Sprite frontSprite;

        [Header("Thruster flames (same width as the hull frame, cut from the flame tops down)")]
        public Sprite sideFlame;
        public Sprite turnFlame;
        public Sprite frontFlame;

        [Tooltip("The still picture shown in the HUD. Falls back to the side sprite.")]
        public Sprite portraitSprite;
    }
}
