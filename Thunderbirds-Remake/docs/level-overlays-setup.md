# Level overlays (#21)

Open `Assets/Resources/LevelOverlays.prefab` in Prefab Mode to edit the three panels, button layouts, colours and labels. Expand Shade / Controls and activate one panel temporarily to preview it; save with Shade and all three panels inactive. Help and Options are nested reusable prefabs.

The current playable controller loads this prefab automatically; no changes to Game.unity are needed. Its serialized catalog points to the campaign catalog. The production GameManager can bind the same callbacks later.

Pause includes Resume, Restart, How to Play, Options, Level Select and Main Menu. Escape / the pause action opens Pause; UI Cancel returns from a submenu or resumes Pause. Losing window focus pauses a running level. Simulation time stays frozen while menus are open.

Failed displays CRUSHED or OUT OF OXYGEN with Retry, Level Select and Main Menu. Complete displays RESCUE COMPLETE, oxygen remaining, Next Level, Level Select and Main Menu. No Next Level is offered as an interactable action for Sandbox or the last available level.

Both outcomes wait 0.5 seconds before revealing the panel, then ignore activation for at least 0.5 seconds after it appears. A held submit or mouse click must also be released. Restart/Retry clear the old outcome; Next Level reuses the game scene. Level Select opens mission selection directly in the menu scene.

`Thunderbirds > Build Level Overlays Prefab` is an optional editor authoring tool: it overwrites this prefab's layout. It is not needed to play or to edit the prefab. `CapturePreviews` checks text overflow and renders Pause/Failed/Complete at 16:9, 4:3 and ultrawide in a disposable test project.
