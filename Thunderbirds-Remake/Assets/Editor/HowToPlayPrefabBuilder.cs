using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Thunderbirds.Unity;
using Object = UnityEngine.Object;

namespace Thunderbirds.Editor
{
    public static class HowToPlayPrefabBuilder
    {
        private const string Path = "Assets/Prefabs/HowToPlayPanel.prefab";
        private static TMP_FontAsset font;
        private static readonly Color Paper = Hex("FFFFEC");
        private static readonly Color Gold = Hex("E0A040");

        [MenuItem("Thunderbirds/Build How To Play Prefab")]
        public static void Build()
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Art/Fonts/Orbitron SDF.asset");
            if (font == null) throw new InvalidOperationException("Build the main menu first to install Orbitron.");
            var root = new GameObject("HowToPlayPanel", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(HowToPlayView));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            root.GetComponent<Canvas>().sortingOrder = 10;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var background = Rect("Background", root.transform, 0, 0, 0, 0);
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.one;
            background.gameObject.AddComponent<Image>().color = Hex("101010");
            var content = Rect("Content", root.transform, 0, 0, 1920, 1080);
            Label(content, "Title", "HOW TO PLAY", 49, 0, 451, 1720, 74, Gold, TextAlignmentOptions.Center);
            Label(content, "Goal", "Bring BOTH ships to their own docks before oxygen runs out.", 23,
                0, 384, 1750, 50, Paper, TextAlignmentOptions.Center);

            var controls = Card(content, "01  CONTROLS", -580, 163);
            var controlsText = Label(controls, "Bindings",
                "<color=#E0A040>KEYBOARD + MOUSE</color>\nMove     WASD / Arrow keys\nSwitch   Space / Tab\nRestart  Hold R\nPause    Esc\n\nHold a direction to keep moving.\nOnly the selected ship moves.",
                21, 0, -23, 486, 249, Paper);

            var ships = Card(content, "02  TWO SHIPS, ONE PILOT", 0, 163);
            Label(ships, "Ships",
                "<color=#E0A040>KESTREL  /  2 x 2 cells</color>\nSmall and fast. Fits narrow gaps\nand handles lighter loads.\n\n<color=#E0A040>ATLAS  /  4 x 2 cells</color>\nLarger and slower. Moves heavier\nloads to clear the way.\n\nBoth ships hover when stopped.",
                21, 0, -23, 486, 249, Paper);

            var blocks = Card(content, "03  READ THE BLOCKS", 580, 163);
            Label(blocks, "Weights",
                "<color=#25BCBC>TEAL</color>     Either ship can push\n<color=#FFFF40>YELLOW</color>  Atlas is needed\n<color=#E04040>RED</color>       Too heavy for either\n\nWeight = number of occupied cells.\nA push counts the whole chain,\nincluding blocks resting on top.\nUnsupported blocks fall.\nBlocks never rotate or merge.",
                21, 0, -23, 486, 249, Paper);

            var carry = Card(content, "04  CARRY & RELEASE", -580, -192);
            Label(carry, "Rules",
                "Lift by moving up below a block.\nA load supported only by your ship\ntravels with it in all directions.\n\nMove down onto a ledge to set it down.\nMove sideways against a wall to\nrelease the block and slide away.\n\nA ceiling or excess weight stops a lift.",
                21, 0, -23, 486, 249, Paper);

            var example = Card(content, "05  USE THE WALLS", 0, -192);
            Label(example, "Description", "Lower an L-block into a slot. Fly right:\nthe wall holds the block, freeing the ship.",
                20, 0, 66, 486, 61, Paper);
            Diagram(example, -162, new[] { "...LLLLL..", "...L.KK...", "...L.KK...", "..#.#.....", "..#.#....." });
            Diagram(example, 0, new[] { "..........", "...LLLLL..", "...L.KK...", "..#L#KK...", "..#.#....." });
            Diagram(example, 162, new[] { "..........", "...LLLLL..", "...L...KK.", "..#L#..KK.", "..#.#....." });
            Label(example, "Steps", "1. CARRY       2. LOWER       3. RELEASE", 16, 0, -72, 486, 38, Gold, TextAlignmentOptions.Center);
            Label(example, "Legend", "Teal: block    Light: ship    Gold: wall", 16, 0, -123, 490, 30, Paper, TextAlignmentOptions.Center);

            var survival = Card(content, "06  KEEP THE RESCUE ALIVE", 580, -192);
            Label(survival, "Danger",
                "Overloaded? A crush ring counts down.\nRelease the load or switch ships\nto push it away before time runs out.\n\nA crush costs one of your 3 lives.\nA blinking ghost waits for its start\narea to clear before returning.\n\nNo lives or no oxygen = level failed.",
                21, 0, -23, 486, 249, Paper);

            var backRect = Rect("Back Button", content, 0, -444, 310, 66);
            var backImage = backRect.gameObject.AddComponent<Image>();
            var back = backRect.gameObject.AddComponent<Button>();
            back.targetGraphic = backImage;
            var colors = back.colors;
            colors.normalColor = Hex("253535");
            colors.highlightedColor = colors.selectedColor = Hex("008080");
            colors.pressedColor = Hex("785828");
            back.colors = colors;
            Label(backRect, "Label", "BACK", 25, 0, 0, 270, 54, Paper, TextAlignmentOptions.Center);
            var handler = backRect.gameObject.AddComponent<HowToPlayBackButton>();
            var view = root.GetComponent<HowToPlayView>();
            Set(view, "content", content);
            Set(view, "backButton", back);
            Set(view, "controlsText", controlsText);
            Set(handler, "screen", view);
            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, Path);
            Object.DestroyImmediate(root);
            AttachToMainMenu();
            AssetDatabase.SaveAssets();
            Debug.Log("How to Play built and connected to MainMenu prefab. No scene files changed.");
        }

        public static void AttachToMainMenu()
        {
            const string menuPath = "Assets/Prefabs/MainMenu.prefab";
            var menu = PrefabUtility.LoadPrefabContents(menuPath);
            try
            {
                var panel = menu.GetComponentInChildren<HowToPlayView>(true);
                if (panel == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
                    panel = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, menu.transform)).GetComponent<HowToPlayView>();
                }
                var rect = (RectTransform)panel.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                rect.localScale = Vector3.one;
                panel.gameObject.SetActive(false);
                var view = menu.GetComponent<MainMenuView>();
                Set(view, "howToPlayPanel", panel);
                var button = menu.transform.Find("Content/How to Play Button").GetComponent<Button>();
                button.interactable = true;
                button.GetComponentInChildren<TMP_Text>().color = Paper;
                button.GetComponent<Outline>().effectColor = Gold;
                PrefabUtility.SaveAsPrefabAsset(menu, menuPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(menu); }
        }

        private static RectTransform Card(Transform parent, string title, float x, float y)
        {
            var card = Rect(title, parent, x, y, 550, 330);
            card.gameObject.AddComponent<Image>().color = Hex("1B2222");
            Label(card, "Heading", title, 21, 0, 125, 490, 42, Gold);
            return card;
        }

        private static void Diagram(Transform parent, float x, string[] rows)
        {
            var diagram = Rect("Slot example", parent, x, -8, 144, 76);
            for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
            {
                var cell = rows[row][column];
                if (cell == '.') continue;
                var tile = Rect("Cell", diagram, (column - 4.5f) * 14, (2 - row) * 14, 13, 13);
                var image = tile.gameObject.AddComponent<Image>();
                image.color = cell == 'L' ? Hex("008080") : cell == '#' ? Gold : Paper;
                image.raycastTarget = false;
            }
        }

        private static TMP_Text Label(Transform parent, string name, string text, float size,
            float x, float y, float width, float height, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.alignment = alignment;
            label.color = color;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private static void Set(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
