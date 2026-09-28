using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Keeps pointer highlighting separate from the toggle's checked state and UI focus.</summary>
    [RequireComponent(typeof(Toggle))]
    public sealed class CheckboxHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Toggle toggle;
        private ColorBlock restingColors;

        private void Awake()
        {
            toggle = GetComponent<Toggle>();
            restingColors = toggle.colors;
            restingColors.selectedColor = restingColors.normalColor;
            toggle.colors = restingColors;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var colors = restingColors;
            // Unity gives Selected precedence over Highlighted after a click.
            colors.selectedColor = colors.highlightedColor;
            toggle.colors = colors;
        }

        public void OnPointerExit(PointerEventData eventData) => Restore();
        private void OnDisable() => Restore();

        private void Restore()
        {
            if (toggle != null) toggle.colors = restingColors;
        }
    }
}
