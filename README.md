# Thunderbirds: Remake

**Two ships. One rescue.** A real-time 2D puzzle game made in Unity 6, inspired by *Thunderbirds*
(Firebird, 1985, Commodore 64).

![Atlas carries a six-cell stone bar across the upper chamber of level 3](Thunderbirds-Remake/docs/images/screenshots/l3-atlas-carries-the-bar.png)

You fly two rescue ships through a side-view tomb: the small, fast **Kestrel** and the big, strong
**Atlas**. You control one at a time and switch whenever you like. Stone blocks are in the way. Each ship
can reach places and move weight the other cannot, so they have to clear the way for each other. Dock both
ships before the oxygen runs out.

This is a university final project by **Ofri Kuperberg** and **Rotem Saraf**.

## The three ideas behind the game

1. **Weight is the only rule.** A block's weight is its number of cells. Every push and every lift compares
   the full weight being moved with that ship's capacity. There are no special blocks.
2. **Real-time, never turn-based.** Gravity and oxygen do not wait for you. There is no undo.
3. **Two ships, one pilot.** No level can be finished with one ship.

## How it plays

| | Kestrel | Atlas |
|---|---|---|
| Size | 2 × 2 cells | 4 × 2 cells |
| Speed | Twice as fast | Slower |
| Can push or carry | Up to 4 cells of stone | Up to 8 cells of stone |
| Good at | Narrow shafts and tunnels | Heavy blocks |

**Blocks** show their class on a glowing edge:

| Edge colour | Meaning |
|---|---|
| Purple | Light: either ship can move it |
| Blue | Heavy: only Atlas can move it |
| Red flash | Too heavy for any ship (a group of blocks pushed together) |

- **Push:** fly sideways into a block. Everything it would shove along counts toward the weight.
- **Lift and carry:** fly up under a block to pick it up. It rides on the ship's deck in every direction.
- **Set down:** a carried block is only released by an obstacle in its path. Fly down into a gap too
  narrow for the block, or scrape it off against a wall.
- **Gravity:** a block with nothing under it falls. Ships do not; they hover.
- **Overload:** if more than a ship can carry lands on it, a countdown ring appears and you have
  3 seconds to get rid of the load. If you do not, the ship is crushed and you lose one of 3 lives.
- **Win:** Kestrel on the **K** dock and Atlas on the **A** dock. **Lose:** no lives left, or no oxygen.

### Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Move (hold) | WASD or arrow keys | Left stick or D-pad |
| Switch ship | Space or Tab | A / ✕ |
| Restart (hold) | R | View / Select |
| Pause | Esc | Menu / Start |

### Levels

| Level | Teaches | Oxygen |
|---|---|---|
| L1 - First Flight | Moving, switching, docking | 90 s |
| L2 - Shared Strength | Pushing; what each ship can move | 120 s |
| L3 - Heavy Lift | Lifting, carrying and setting a block down | 120 s |
| L4 - Rockfall | Falling blocks and the crush countdown | 90 s |
| L5 - The Vault | Everything at once, with little oxygen | 60 s |

There is also a **Sandbox** level for trying things out. It does not affect progress.

## Screenshots

| | |
|---|---|
| ![Main menu](Thunderbirds-Remake/docs/images/screenshots/main-menu.png) | ![Level select with five missions and the sandbox](Thunderbirds-Remake/docs/images/screenshots/level-select.png) |
| Main menu | Level select |
| ![Level 3 at the start, with its hint banner](Thunderbirds-Remake/docs/images/screenshots/l3-heavy-lift-start.png) | ![Level 4: a slab has landed on Kestrel, which is red with a countdown ring above it](Thunderbirds-Remake/docs/images/screenshots/l4-rockfall-crush-countdown.png) |
| L3: a blue bar covers the only shaft | L4: Kestrel is overloaded and the crush countdown is running |
| ![Pause menu](Thunderbirds-Remake/docs/images/screenshots/pause-menu.png) | ![The 1985 Commodore 64 original](Thunderbirds-Remake/docs/images/reference-thunderbirds-c64.png) |
| Pause menu | The 1985 original that inspired it |

## Running the project

1. Install **Unity 6000.3.20f1** with Unity Hub. The exact version is in
   `Thunderbirds-Remake/ProjectSettings/ProjectVersion.txt`.
2. Install [Git LFS](https://git-lfs.com) and run `git lfs install` once. Images, audio, fonts and PDFs are
   stored with LFS.
3. Clone the repository. In Unity Hub choose **Add → Add project from disk** and select the
   `Thunderbirds-Remake/` folder (the one that contains `Assets/`).
4. The first open takes a while, because Unity rebuilds its `Library/` cache.
5. Open `Assets/Scenes/MainMenu.unity` and press Play.

To make a Windows build, use **File → Build Profiles** with both scenes (`MainMenu`, then `Game`).

Every tuning value (ship speeds, capacities, crush time, oxygen, colours, volumes) is editable in the
Inspector: menu **Thunderbirds → Open Game Settings**.

## How it is built

The code has two layers.

- **Rules layer** (`Assets/Scripts/Rules`): plain C# with no Unity dependency. It holds the whole game:
  movement, pushing, lifting, gravity, load, lives, oxygen, winning. It runs in a fixed eight-step tick and
  announces what happened as events.
- **Unity layer** (`Assets/Scripts/Unity`): reads input, ticks the rules, and draws and plays what the
  events say. Views never decide whether a move is allowed.

Because the rules do not need a scene, they are covered by 348 Edit Mode tests, including tests that play
every level from start to finish.

Levels are text grids, one character per cell, stored in `LevelData` assets:

```
################################
#..............................#
#..........................cccc#
#......bbb.................cccc#
#KK....b...................AAAA#
#KK...######...............AAAA#
#11...#....#............dd.2222#
#11...#....#............d..2222#
################################
```

`#` wall, `K` / `A` the ships, `1` / `2` their docks, and each letter is one block.

Course topics used: object pooling, singletons (`GameManager`, `AudioManager`), coroutines,
ScriptableObjects, the observer pattern with C# events, state machines, Input System action maps, and
building for a platform.

### Documentation

| Document | What is in it |
|---|---|
| [Game Design Document](Thunderbirds-Remake/docs/GDD.md) | The design: rules, controls, screens, art, architecture, scope |
| [Game flow](Thunderbirds-Remake/docs/game-flow.md) | Which class calls which, from the menu to the end of a level, and how sound works |
| [Move flow](Thunderbirds-Remake/docs/move-flow.md) | What happens inside one move |
| [Collaboration rules](Thunderbirds-Remake/docs/COLLABORATION.md) | The team's working rules, checked by `tools/check-rules.ps1` and CI |
| [Mind map](Thunderbirds-Remake/docs/mindmap.md) | Where to find everything else |

Course lecture slides are not in the repository. Place them locally in
`Thunderbirds-Remake/docs/study materials/`.

## Credits and licences

### Third-party material in the game

| What | Author and source | Licence | Notes |
|---|---|---|---|
| Music: "Fantasy Tomb Scene OST" | Markus Schroeder ([markus-projects.net](http://markus-projects.net/)), from [OpenGameArt](https://opengameart.org/content/fantasy-tomb-scene-ost) | [CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/) | Used unchanged |
| Font: Orbitron | The Orbitron Project Authors ([theleagueof/orbitron](https://github.com/theleagueof/orbitron)) | [SIL Open Font License 1.1](Thunderbirds-Remake/Assets/Art/Fonts/OFL.txt) | Licence file kept next to the font |

### Made by the team

| What | How |
|---|---|
| Ship, wall, block and background art | Generated with Google Gemini, then cut and cleaned with our own scripts. *Void - Fleet Pack 2* by Baldur (distributed by Foozle, CC0) was used as a style reference only; none of its sprites are in the game. Details: [ships](Thunderbirds-Remake/Assets/Art/Ships/SOURCE.md), [tiles](Thunderbirds-Remake/Assets/Art/Tiles/SOURCE.md). |
| Sound effects | Synthesised with a script. No samples and no third-party material. Details: [audio](Thunderbirds-Remake/Assets/Audio/SOURCE.md). |
| Code, levels and documentation | Written by the team, with AI coding assistance. |

### Tools

| What | Licence | Notes |
|---|---|---|
| Unity 6 and its packages (Input System, URP, UGUI, TextMeshPro, Test Framework) | Unity terms and the Unity Companion License | Downloaded by the Unity Package Manager; not stored in this repository |
| [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) by Ivan Murzak | Apache 2.0 | Editor tooling used during development; not part of the game |

### Reference material

The screenshot of the 1985 game in `Thunderbirds-Remake/docs/images/` belongs to its original publisher.
It is included only to show what the remake is based on.

### The game itself

No open-source licence has been chosen for this repository. The code, levels and the art and sound made
by the team are © 2026 Ofri Kuperberg and Rotem Saraf.

### Trademark

*Thunderbirds* is a trademark of ITV. This is a non-commercial student project, not affiliated with or
endorsed by ITV. The game contains no music, images or other assets from the television series or from
the 1985 game. The ship names Kestrel and Atlas are our own.
