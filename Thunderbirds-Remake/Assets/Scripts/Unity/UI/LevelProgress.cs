using System;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>Persistent campaign progress; catalog order is the stable L1-L5 identity.</summary>
    public static class LevelProgress
    {
        public const int LevelCount = 5;
        public static string Key(int index) => "Thunderbirds.LevelCompleted.v1." + index;
        public static bool IsCompleted(int index) => index >= 0 && index < LevelCount && PlayerPrefs.GetInt(Key(index), 0) == 1;
        public static bool IsUnlocked(int index) => index >= 0 && index < LevelCount && (index == 0 || IsCompleted(index) || IsCompleted(index - 1));
        public static void Complete(int index)
        {
            if (index < 0 || index >= LevelCount) throw new ArgumentOutOfRangeException(nameof(index));
            PlayerPrefs.SetInt(Key(index), 1);
            PlayerPrefs.Save();
        }
    }

    /// <summary>One-shot scene handoff, not a persistent scene object.</summary>
    public static class LevelLaunch
    {
        private static LevelData _pending;
        private static int _index = -1;
        public static bool Select(LevelCatalog catalog, int index)
        {
            var data = catalog != null ? catalog.Get(index) : null;
            if (data == null || !LevelProgress.IsUnlocked(index)) return false;
            _pending = data;
            _index = index;
            return true;
        }
        public static LevelData Take(out int index)
        {
            var data = _pending;
            index = _index;
            Clear();
            return data;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() { _pending = null; _index = -1; }
    }
}
