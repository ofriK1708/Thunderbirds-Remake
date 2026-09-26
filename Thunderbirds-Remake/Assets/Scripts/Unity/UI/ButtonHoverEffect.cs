using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Thunderbirds.Unity
{
    /// <summary>Pointer feedback that remains visible on a keyboard-selected button.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Color hoverColor = new Color(0.1f, 0.75f, 0.7f, 1f);
        private Button button;
        private ColorBlock originalColors;
        private Texture2D handCursor;
        private bool pointerInside;
        private bool hoverActive;

        private void Awake()
        {
            button = GetComponent<Button>();
            originalColors = button.colors;
            handCursor = CreateHandCursor();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            RefreshHover();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            RefreshHover();
        }

        private void Update() => RefreshHover();

        private void RefreshHover()
        {
            var active = pointerInside && button.IsInteractable();
            if (active == hoverActive) return;
            hoverActive = active;
            var colors = originalColors;
            if (active)
            {
                // Selected normally takes precedence over Highlighted in Unity UI.
                // Tint both states so pointer feedback remains visible with keyboard focus.
                colors.normalColor = colors.highlightedColor = colors.selectedColor = hoverColor;
            }
            button.colors = colors;
            Cursor.SetCursor(active ? handCursor : null, active ? new Vector2(6, 1) : Vector2.zero, CursorMode.Auto);
        }

        private void OnDisable()
        {
            pointerInside = false;
            if (button != null) RefreshHover();
        }

        private void OnDestroy()
        {
            if (handCursor != null) Destroy(handCursor);
        }

        private static Texture2D CreateHandCursor()
        {
            // Original pixel hand: transparent background, dark outline, light fill.
            // Kept local so the menu needs no external cursor asset or platform API.
            var rows = new[]
            {
                "     ###", "    #WWW#", "    #WWW#", "    #WWW#",
                "    #WWW#", "    #WWW#", "    #WWW####",
                "    #WWW#WW###", "    #WWW#WW#WW##",
                "    #WWW#WW#WW#W#", " ## #WWWWWWWWWWW#",
                "#WW##WWWWWWWWWWW#", "#WWW#WWWWWWWWWWW#",
                " #WWWWWWWWWWWWWW#", "  #WWWWWWWWWWWWW#",
                "  #WWWWWWWWWWWWW#", "   #WWWWWWWWWWW#",
                "    #WWWWWWWWWW#", "     #WWWWWWWW#",
                "     #WWWWWWWW#", "     ##########"
            };
            var texture = new Texture2D(20, 24, TextureFormat.RGBA32, false);
            texture.name = "Menu hand cursor";
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[20 * 24];
            for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++)
            {
                var mark = rows[y][x];
                if (mark == ' ') continue;
                pixels[(23 - y) * 20 + x] = mark == '#'
                    ? new Color32(16, 16, 16, 255) : new Color32(255, 255, 236, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }
    }
}
