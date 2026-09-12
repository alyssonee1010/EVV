# Development Guide

## Setup

Use Unity 6000.5.0f1 or a compatible Unity 6 editor. MainMenu.unity is the entry scene; Level 1.unity is the shared gameplay scene used by all level definitions.

For a quick C# verification outside Unity, run:

    dotnet build VikingsVsEveryone.slnx

Unity remains the source of truth for scene serialization, asset imports, animation events, and Play Mode behavior.

## Code Design

- Keep fixes small, simple, and easy to understand.
- Put shared mechanics in focused reusable modules instead of duplicating them.
- Generalize for existing behavior or confirmed future features, not speculative possibilities.
- Keep feature-specific rules in the owning feature.
- Reuse an existing utility before creating another implementation.

Example: VVEWorldPointer owns mouse conversion and generic world hit-testing. Healing decides what counts as a valid healing target.

## Adding A Level

1. Add an NN-NN.yml or NN-NN_name.yml file under Assets/StreamingAssets/Levels.
2. Follow Assets/StreamingAssets/Levels/manual.md.
3. Use unit ids configured on VVEWaveDirector.
4. Use defender ids configured in VVEDefenderCatalog for unlocks.
5. Confirm the level appears in MainMenu stage selection.
6. Test wave timing, completion, reset, unlocks, and next-level flow.

The available_units field is currently parsed but not enforced by the runtime. Do not rely on it to restrict the defender loadout without implementing that behavior.

## Adding A Defender

1. Create the prefab under Assets/Prefabs/Defenders.
2. Add VVEDefender and VVEHealth.
3. Add the relevant focused role component:
   - VVERowProjectileShooter for ranged combat
   - VVEBoardMeleeAttacker for melee combat
   - VVEMinerMiningReward for diamond generation
   - VVEWizardPotionReward for potion generation
4. Add colliders and SpriteRenderers needed for combat and pointer interaction.
5. Register a stable id, prefab, display name, cost, and default-unlock state in VVEDefenderCatalog.
6. Test loadout display, placement, lane sorting, damage, health-bar behavior, potion targeting, and removal.

Do not create a separate health or pointer system for one defender.

## Adding An Enemy

1. Create the prefab under Assets/Prefabs/Vikings or another enemy folder.
2. Add VVEHealth.
3. Implement IVVEEnemyLaneWalker or reuse VVEEnemyVikingWalker.
4. Add the collider, renderers, animator, and required animation events.
5. Register a stable unit id and prefab in VVEWaveDirector's unit options.
6. Reference that id from level YAML.
7. Test spawning, lane movement, defender attacks, death, director tracking, and base damage on exit.

## Adding A Potion

Keep potion effects feature-specific while reusing shared infrastructure:

1. Represent collectible potion resources through VVEBoardPickup or a focused extension of it.
2. Store inventory in the appropriate wallet/resource system.
3. Use VVEWorldPointer for world target selection.
4. Supply a target type and validity filter specific to the potion.
5. Keep aiming, spending, effect application, and feedback in a focused potion controller.
6. Test invalid targets, cancellation, inventory spending, edge-of-board targets, and interaction priority.

Do not put potion rules into VVEWorldPointer.

## Validation

Before considering a gameplay change complete, verify the affected path and its neighboring shared behavior:

- scripts compile without errors
- MainMenu can discover and hand off a selected level
- the loadout opens and starts the level
- defenders can be placed only on valid cells
- removal affects only placed defenders
- pickups can be collected
- potion targeting accepts only valid targets
- defenders and enemies remain lane-correct
- health bars appear while damaged and hide at full health
- wave completion waits for tracked enemies to clear
- board life reaches the game-over state correctly

## Known Technical Debt

- PlantPlacementManager and CharPlacementManagement.cs do not share a name.
- VVELevelSelectUI still provides the in-game selection/continuation flow alongside MainMenu stage selection.
- available_units is parsed from level YAML but is not enforced.
- Automated edit-mode and play-mode coverage is limited.
