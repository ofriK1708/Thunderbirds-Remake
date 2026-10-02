using System;
using UnityEngine;

namespace Thunderbirds.Unity
{
    /// <summary>Persistent campaign progress; catalog order is the stable L1-L8 identity.</summary>
    public static class LevelProgress
    {
        public const int LevelCount = 8;
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
}
