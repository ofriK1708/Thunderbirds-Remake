# Main menu — first screen

1. Open `Assets/Scenes/MainMenu.unity` in Unity.
2. Drag `Assets/Prefabs/MainMenu.prefab` into the scene hierarchy once and save.
3. Enter Play mode. Quit stops Play mode in the Editor and exits in a standalone build.

The prefab contains an editable Canvas, background, Orbitron title and four buttons.
How to Play is enabled and opens the connected instructions panel. Play and Options remain
disabled until their screens are implemented.
When adding each screen, enable its button and connect the corresponding event on MainMenuView
in the Inspector. The runtime creates an Input System EventSystem only if none exists.

Canvas reference: 1920 × 1080, match 0.5. Content scales to fit the available area.
Mouse and the Input System's default UI navigation are supported. How to Play is selected
initially; Play becomes the initial selection when enabled.

The team-owned scene files are unchanged. This prefab is the handoff for Rotem to place.
`Thunderbirds > Build Main Menu Prefab` rebuilds the original layout and overwrites prefab edits;
it is not required for normal use or Inspector editing.

Orbitron comes from Google Fonts (`ofl/orbitron`) and its OFL license is beside the font.
TextMesh Pro Essential Resources are from the project's installed UGUI package.
