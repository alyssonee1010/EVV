# Maintained Backlog

Keep this file limited to actionable work that is not already implemented. Put balance values in level, catalog, prefab, or scene data rather than copying them here.

## Gameplay

- Add defender and Viking death presentation.
- Add fire/status-effect support through a reusable status module, then integrate it with the relevant attacks.
- Add the confirmed future potion types using the shared EVVWorldPointer targeting utility and focused effect controllers.
- Improve pickup readability and collection feedback.

## Content

- Add and validate more level YAML files.
- Register additional enemy variants with EVVWaveDirector and use them in level wave data.

## UI And Audio

- Improve in-game UI feedback without duplicating gameplay state.
- Complete missing combat and character sound coverage.
- Verify audio timing against animation events.

## Engineering

- Rename PlantPlacementManager and CharPlacementManagement.cs together.
- Decide whether the standalone EVVEnemyLaneSpawner remains necessary beside EVVWaveDirector.
- Enforce or remove the currently unused available_units level field.
- Add edit-mode tests for level parsing, wallet changes, health, and target filters.
- Add play-mode coverage for placement, removal, pickups, potions, wave completion, and menu hand-off.
