# Main menu — first screen

1. Open `Assets/Scenes/MainMenu.unity` in Unity.
2. The scene already contains `Assets/Prefabs/MainMenu.prefab`; do not add a second copy.
3. Enter Unity Play mode, then press **Play** to open Level Select, then **Sandbox** to try the existing test level.
   Click inside the Game view to give it keyboard focus. Quit stops Play mode in the Editor and exits in a standalone build.

The prefab contains an editable Canvas, background, Orbitron title and four buttons.
How to Play and Options are enabled and open their connected panels. Play opens Level Select. Its L1-L8 tiles read `Assets/Levels/LevelCatalog.asset` in campaign order. The catalog is currently empty: missing missions show COMING SOON and cannot launch. Sandbox remains available separately and never changes campaign progress. Add authored LevelData assets to the catalog to enable the campaign slots.
The sandbox shows placeholder ships and blocks and uses the real simulation for movement, sideways pushes and gravity.
To see a fall, switch to Atlas and move left until it clears the yellow block above it. The unsupported block drops.
WASD/arrows move, Space/Tab switches ships, holding R restarts, and Escape pauses.
Gamepad controls are stick/D-pad, south button, View/Select (hold) and Menu/Start respectively.
Bindings now live in `Assets/InputSystem_Actions.inputactions`; `InputReader` supplies gameplay commands
and the menu's UI actions. A short movement tap still steps once, the last pressed axis wins on diagonals,
and holding Restart shows its progress. Pausing discards queued taps so they cannot move a ship on resume.
The sandbox also has Resume/Pause, Restart and Main menu buttons. Restart reuses the visuals.
Lifting/carrying, load countdowns, oxygen and mission completion are connected; life loss and respawn remain separate work. Completing a selected campaign mission saves its completed flag and unlocks the next mission in PlayerPrefs. Restarting or replaying never clears progress.
Options contains Fullscreen, Music Volume, Sound Effects and Push Preview controls. These
apply and save Fullscreen immediately in a standalone player. Unity Editor saves the preference but does not change the editor window. Audio and Push Preview remain disabled until their features are implemented. Back or Cancel
returns to the caller. Existing scene instances inherit the panel from MainMenu.prefab;
no extra scene object or manual button wiring is required.
Level Select supports mouse, keyboard and gamepad navigation; Back/Escape/gamepad Cancel returns focus to Play. MainMenuView has a serialized LevelCatalog reference, preserved by the prefab builder. The runtime creates an Input System EventSystem only if none exists.

Canvas reference: 1920 × 1080, match 0.5. Content scales to fit the available area.
Mouse and the Input System's default UI navigation are supported. Play is selected initially.

The team-owned scene files are unchanged. This prefab is the handoff for Rotem to place.
`Thunderbirds > Build Main Menu Prefab` rebuilds the original layout and overwrites prefab edits;
it is not required for normal use or Inspector editing.

Orbitron comes from Google Fonts (`ofl/orbitron`) and its OFL license is beside the font.
TextMesh Pro Essential Resources are from the project's installed UGUI package.
