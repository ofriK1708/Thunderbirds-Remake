using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Applies the shared hover treatment to buttons in every scene and newly opened UI.</summary>
    public static class ButtonHoverDefaults
    {
        private static Selectable[] selectables = new Selectable[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            // Also safe when Enter Play Mode has domain reload disabled.
            Canvas.willRenderCanvases -= Apply;
            Canvas.willRenderCanvases += Apply;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Apply();

        private static void Apply()
        {
            if (!Application.isPlaying) return;
            var required = Selectable.allSelectableCount;
            if (selectables.Length < required)
                selectables = new Selectable[Mathf.NextPowerOfTwo(required)];
            var count = Selectable.AllSelectablesNoAlloc(selectables);
            for (var i = 0; i < count; i++)
            {
                var button = selectables[i] as Button;
                if (button != null && button.GetComponent<ButtonHoverEffect>() == null)
                    button.gameObject.AddComponent<ButtonHoverEffect>();
                selectables[i] = null;
            }
        }
    }
}
