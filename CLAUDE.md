# Repository Guidance

Read AGENTS.md before changing this project. The project preference is small, simple fixes; focused reusable modules; and generalization only for real or confirmed uses.

## Project

Everyone vs Vikings is a Unity 2D lane-defense game.

- Unity: 6000.5.0f1
- Entry scene: Assets/Scenes/MainMenu.unity
- Shared gameplay scene: Assets/Scenes/Level 1.unity
- Level data: Assets/Levels/*.yml
- C# verification: dotnet build EveryoneVsVikings.slnx

Unity Play Mode remains required for scene, animation, asset-import, and runtime interaction verification.

## Read Before Non-Trivial Changes

- README.md for the project map and controls
- docs/GAMEPLAY.md for the current game flow
- docs/ARCHITECTURE.md for system ownership
- docs/DEVELOPMENT.md for extension and validation workflows
- Assets/Levels/manual.md for the level schema
- docs/TODO.md for maintained unfinished work

## Important Ownership Rules

- EVVLevelLoader parses and discovers level YAML.
- EVVMainMenuController owns scene-authored menu behavior, not gameplay state.
- EVVLevelSelectUI owns the pre-level and between-level gameplay-scene flow.
- EVVWaveDirector owns YAML wave scheduling and enemy tracking.
- EVVDefenderCatalog owns defender id, prefab, cost, and display-name mappings.
- EVVDefenderUnlocks owns unlock persistence and loadout state.
- PlantPlacementManager owns board placement/removal routing.
- EVVWorldPointer owns generic mouse-to-world conversion and world hit-testing.
- Feature controllers own their target-validity and effect rules.

Do not introduce parallel level loaders, wave schedulers, wallets, health models, or pointer utilities without a demonstrated need.

## Current Constraints

- PlantPlacementManager is defined in CharPlacementManagement.cs.
- available_units is parsed but not enforced.
- MainMenu and Level 1 are the only enabled build scenes.
- Legacy platformer scripts remain in the repository but are not part of the lane-defense runtime unless referenced by a scene.
