using System.Collections.Generic;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>The levels in play order. Level Select and "Next Level" read this list.</summary>
    [CreateAssetMenu(fileName = "LevelCatalog", menuName = "Thunderbirds/Level Catalog")]
    public sealed class LevelCatalog : ScriptableObject
    {
        [SerializeField] private List<LevelData> levels = new List<LevelData>();

        public IReadOnlyList<LevelData> Levels => levels;
        public int Count => levels.Count;

        public LevelData Get(int index) => index >= 0 && index < levels.Count ? levels[index] : null;

        public int IndexOf(LevelData level) => levels.IndexOf(level);
    }
}
