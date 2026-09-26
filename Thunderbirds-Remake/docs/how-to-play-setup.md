# How to Play

## Use it in the existing menu

1. Exit Play mode, then let Unity finish importing and compiling.
2. Open the MainMenu scene with the existing MainMenu prefab instance.
3. Enter Play mode and select How to Play. Back, Escape, or the gamepad Cancel action
   returns to the menu and restores focus to the How to Play button.

There is no second scene to create and no additional prefab to drag into the scene.
The updated MainMenu prefab contains a hidden HowToPlayPanel prefab instance and the
button is already enabled and connected. Existing scene changes are preserved.

If the button was manually overridden as disabled on the scene instance, revert only
its Button > Interactable override. Do not revert the entire prefab instance.

## Editing the screen

Open `Assets/Prefabs/HowToPlayPanel.prefab` in Prefab Mode to edit the six content cards.
The prefab starts inactive. For a static editor preview, enable it temporarily, then
restore its inactive state before saving. The menu activates it when requested.
The Controls card is populated at runtime. It switches between keyboard/mouse and gamepad
when a button is pressed on the corresponding device.

The builder menu recreates the default layout and overwrites prefab edits. It is only
needed to regenerate the asset, not for ordinary use.

## Integration still pending in the game

- There is no ShipConfig asset in this checkout. The screen describes ship size and roles
  but deliberately does not hard-code numeric speed/capacity tuning. Once the shared
  ShipConfig/GameConfig work lands, populate those statistics from the actual assets.
- The existing InputSystem_Actions asset still contains template actions. The screen uses
  the GDD's default bindings for now. Assign the real gameplay InputActionAsset to the
  HowToPlayView `Gameplay Actions` field when Move/SwitchShip/Restart/Pause are implemented.
  Present bindings are then read from that asset, including binding overrides.
- To open from Pause later, the owner of Game.unity adds this prefab and calls
  `HowToPlayView.Open(pausePanel)`. The screen remembers and restores its caller and selection;
  LevelController remains responsible for keeping the simulation paused.

## Verification

The editor validation helpers build assets without saving scenes, render reference previews,
and exercise opening, Back, Cancel and repeated navigation in Play mode in a disposable copy.
They do not run automatically during normal development.
