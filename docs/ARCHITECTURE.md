# Architecture

## Runtime Flow

1. VVEMainMenuController displays scene-authored menu panels and discovers levels through VVELevelLoader.
2. VVEPendingLevelSelection carries the selected level id into the shared gameplay scene.
3. VVELevelSelectUI and VVEDefenderLoadoutUI prepare the loadout and start the selected level.
4. VVEWaveDirector schedules YAML-authored waves and resolves unit ids to configured enemy prefabs.
5. PlantPlacementManager handles placement and routes world interactions.
6. Defenders, enemies, projectiles, pickups, health, and wallet systems communicate through focused components and events.
7. Level completion persists progress, resets board state, applies unlocks, and advances to the next discovered level.

## Level And Menu Layer

### VVELevelLoader

Discovers and parses Assets/StreamingAssets/Levels/*.yml into VVELevelDefinition objects. Discovery order is stage then level.

### VVEMainMenuController

Controls the scene-authored MainMenu Canvas, including panel transitions, stage-list population, settings, scene hand-off, and the optional defender showcase. Layout and artwork remain scene data.

### VVELevelSelectUI

Owns the pre-level and between-level flow inside the gameplay scene. It coordinates the loadout, starts VVEWaveDirector, resets completed levels, applies unlocks, and selects the next level.

### VVEDefenderCatalog, VVEDefenderUnlocks, And VVELevelCompletion

VVEDefenderCatalog is the scene source of truth for defender ids, prefabs, display names, costs, and default unlocks. VVEDefenderUnlocks persists unlocked ids while keeping the current loadout in memory for the game session. VVELevelCompletion persists completed level ids for progression and menu presentation.

### VVEWaveDirector

Builds lanes from VVETilemapBoard, schedules wave groups, creates configured enemies, tracks living enemies, and emits level start/completion events.

VVEEnemyLaneSpawner is a separate time-ramping spawner and is not the YAML wave scheduler. New level work should use VVEWaveDirector unless a scene intentionally uses the alternate spawner.

## Board And Input Layer

### PlantPlacementManager

Coordinates defender selection, placement previews, tile validation, spending, occupied-cell state, defender removal, pickup collection, and potion click routing.

The class currently lives in CharPlacementManagement.cs; the mismatch is retained for compatibility and should be corrected in a dedicated rename.

### VVEWorldPointer

Shared utility for:

- converting the mouse position to the world plane
- exact Collider2D target detection
- visual SpriteRenderer-bounds fallback
- optional feature-specific target filters

Current consumers are placement pointer conversion, defender removal, board-pickup collection, and healing-potion targeting. Creation uses the shared position but still maps it through the tilemap because it selects a cell rather than an existing object.

### VVETilemapBoard And VVELaneDepth

VVETilemapBoard exposes the gameplay grid and tilemap. VVELaneDepth maps lane indices to Z depth and applies gameplay sorting. Pointer distance remains an X/Y operation because Z represents lane/sorting state rather than cursor position.

## Defender And Combat Layer

### VVEDefender

Stores placed-cell state, initializes VVEHealth and VVEWorldHealthBar, and applies lane depth/sorting.

### VVEHealth

Shared health model with damage, healing, death, and health-change events.

### VVEWorldHealthBar

Runtime world-space bar that listens to VVEHealth. Defender bars are hidden at full health and appear while damaged.

### Attack Components

- VVERowProjectileShooter performs lane-aware ranged targeting.
- VVEBoardMeleeAttacker performs lane-aware melee targeting.
- VVEDamageProjectile handles projectile movement and damage.
- VVEHitRecoil handles hit response and stun presentation.

## Enemy Layer

IVVEEnemyLaneWalker defines the contract used by lane scheduling and targeting. VVEEnemyVikingWalker is the current concrete walker and owns movement, defender attacks, death handling, and base damage on exit.

## Economy And Pickup Layer

VVEUsableWallet owns resource counts and change events. VVEBoardPickup represents collectible diamonds or healing potions. VVEMinerMiningReward and VVEWizardPotionReward create those pickups, while VVEThrownPickup animates their launch.

## UI Layer

VVEHealingPotionUseController owns healing-specific state: counter activation, aiming, valid-target filtering, highlighting, spending, healing, and feedback. Generic pointer behavior stays in VVEWorldPointer.

VVEUsableCounterUI, VVELevelProgressUI, and the loadout/select components present wallet, wave, and defender-selection state.

## Legacy Content

The repository still contains platformer-era scripts such as PlayerController, PhysicsObject, Enemy, Door, and Breakable. They are not part of the lane-defense runtime unless a scene references them.
