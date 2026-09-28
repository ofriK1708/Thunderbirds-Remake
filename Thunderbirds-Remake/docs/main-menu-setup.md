# Main menu — first screen

1. Open `Assets/Scenes/MainMenu.unity` in Unity.
2. The scene already contains `Assets/Prefabs/MainMenu.prefab`; do not add a second copy.
3. Enter Unity Play mode, then press the menu's **Play** button to open the movement sandbox.
   Click inside the Game view to give it keyboard focus. Quit stops Play mode in the Editor and exits in a standalone build.

The prefab contains an editable Canvas, background, Orbitron title and four buttons.
How to Play and Options are enabled and open their connected panels. Play opens `Game` with `L0_Sandbox`.
The sandbox shows placeholder ships and blocks and uses the real simulation for movement, sideways pushes and gravity.
To see a fall, switch to Atlas and move left until it clears the yellow block above it. The unsupported block drops.
WASD/arrows move, Space/Tab switches ships, holding R restarts, and Escape pauses.
Gamepad controls are stick/D-pad, south button, View/Select (hold) and Menu/Start respectively.
Bindings now live in `Assets/InputSystem_Actions.inputactions`; `InputReader` supplies gameplay commands
and the menu's UI actions. A short movement tap still steps once, the last pressed axis wins on diagonals,
and holding Restart shows its progress. Pausing discards queued taps so they cannot move a ship on resume.
The sandbox also has Resume/Pause, Restart and Main menu buttons. Restart reuses the visuals.
Lifting/carrying, oxygen, lives and mission completion are not implemented in this preview.
Options contains Fullscreen, Music Volume, Sound Effects and Push Preview controls. These
are interactive visual previews only: they do not apply or save settings. Back or Cancel
returns to the caller. Existing scene instances inherit the panel from MainMenu.prefab;
no extra scene object or manual button wiring is required.
When adding each screen, enable its button and connect the corresponding event on MainMenuView
in the Inspector. The runtime creates an Input System EventSystem only if none exists.

Canvas reference: 1920 × 1080, match 0.5. Content scales to fit the available area.
Mouse and the Input System's default UI navigation are supported. Play is selected initially.

The team-owned scene files are unchanged. This prefab is the handoff for Rotem to place.
`Thunderbirds > Build Main Menu Prefab` rebuilds the original layout and overwrites prefab edits;
it is not required for normal use or Inspector editing.

Orbitron comes from Google Fonts (`ofl/orbitron`) and its OFL license is beside the font.
TextMesh Pro Essential Resources are from the project's installed UGUI package.
