# First block mechanics

Open `Assets/Scenes/Game.unity` and press Play. WASD/arrows move;
Space switches ships. Push by flying into the side of the teal block.
Fly beneath it and move up to lift it. Blocks supported only by a ship
travel with it. Blocks with no support fall one cell every 0.1 seconds.
A falling block can land on either ship, including the inactive ship,
and can then be lifted. No grab button is needed.

The sandstone platforms are fixed walls: they never fall or move.
They block ships and movable blocks on every side. The gap below the
floating shelves is a drop to the existing bottom floor, not a death pit.

## Try a two-ship catch

1. Switch to Atlas, move right one cell and upward two cells (center X = -4, Y = -5).
2. Switch to Kestrel. Move left two cells (center X = -13).
3. Move up five cells (center Y = -2), beside the left shelf.
4. Move right to push the teal block off the right edge of the shelf.
5. Atlas catches it. Switch to Atlas and move upward to lift it.

Movement repeats while held, so use the Inspector coordinates if setting
up this exact example. Stop and restart Play Mode to reset the demo.

## Authoring

- Drag `Assets/Prefabs/FixedWall.prefab` or `LightBlock.prefab` into the scene.
- Use the root's Position to place it. Keep root Scale at (1, 1, 1) and
  Rotation at zero. Set dimensions using `Level Block > Size` before Play.
- The lower-left corner must be on whole-number cells: even dimensions
  use an integer center, odd dimensions use a half-unit center.
- Light blocks may have at most four cells, so both ships can handle one.
  Kestrel's total capacity is 4; Atlas's is 8. Chains include their riders.
- A wall can be any rectangular size that fits inside the board.
- Board bounds are X = -16 to 16 and Y = -8 to 9. Initial bodies must not overlap.
- Expand `Visuals` to edit geometry parts. Scenery under `Tomb Background`
  is decorative; only objects with `LevelBlock` enter the rules model.
- A ceiling refuses a lift. A sideways obstruction or supporting ledge
  releases carried cargo so the ship can slide away. Partly supported
  blocks remain on the ledge rather than following a ship sideways.

The plain C# rules are in `Scripts/Rules/BlockWorld.cs`; `ShipController`
feeds input to them and copies positions to the scene. No Rigidbody2D is
used. Edit Mode tests cover pushing, lifting, gravity, catches, carrying,
obstacles, capacity, invalid setups and the authored demo route.

This increment does not implement crush damage, lives, oxygen, arbitrary
block shapes, pull/grab, smooth movement or level progression yet.
