# Everyone vs Vikings

Everyone vs Vikings is a Unity 2D lane-defense game. Players build a defender loadout, place defenders on a tilemap board, collect generated resources, use potions, and stop Viking waves from reaching the chest.

## Run The Project

1. Open the repository with Unity 6000.5.0f1 or a compatible Unity 6 editor.
2. Let Unity restore the packages in Packages.
3. Open Assets/Scenes/MainMenu.unity.
4. Enter Play Mode.

MainMenu is the build entry point. All selected levels run inside Assets/Scenes/Level 1.unity using data loaded from Assets/StreamingAssets/Levels.

## Controls

| Action | Input |
| --- | --- |
| Select a defender | Left click its loadout slot |
| Place a defender | Left click a valid empty board cell |
| Collect a board pickup | Left click the pickup |
| Start healing-potion aiming | Left click the potion counter |
| Heal a defender | While aiming, left click a damaged defender |
| Cancel selection or potion aiming | Right click |
| Toggle removal mode | X |
| Temporarily enable removal mode | Hold Left Shift |

## Project Map

| Path | Purpose |
| --- | --- |
| Assets/StreamingAssets/Levels | Data-driven level and wave definitions |
| Assets/Scenes | Main menu and gameplay scenes |
| Assets/Prefabs/Defenders | Placeable defender prefabs |
| Assets/Prefabs/Vikings | Enemy prefabs |
| Assets/_Scripts/Board | Board entities, defender state, and base life |
| Assets/_Scripts/Combat | Health, attacks, projectiles, and hit reactions |
| Assets/_Scripts/Enemies | Lane-enemy behavior |
| Assets/_Scripts/Levels | Level loading, waves, unlocks, and loadouts |
| Assets/_Scripts/Pickups | Resource generation and collectible pickups |
| Assets/_Scripts/Placement | Placement, removal, and gameplay click routing |
| Assets/_Scripts/Rendering | Silhouette outline renderer feature and its character marker |
| Assets/_Scripts/UI | Menus, counters, potion presentation, and health bars |

## Documentation

- docs/GAMEPLAY.md describes the current player-facing flow and mechanics.
- docs/ARCHITECTURE.md explains system ownership and runtime communication.
- docs/DEVELOPMENT.md contains extension workflows and validation guidance.
- Assets/StreamingAssets/Levels/manual.md documents the level YAML format.
- docs/TODO.md contains the maintained backlog.
- AGENTS.md records the project coding guidelines.
