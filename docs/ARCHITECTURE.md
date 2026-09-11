# Architecture

## Runtime Flow

1. EVVMainMenuController displays scene-authored menu panels and discovers levels through EVVLevelLoader.
2. EVVPendingLevelSelection carries the selected level id into the shared gameplay scene.
3. EVVLevelSelectUI and EVVDefenderLoadoutUI prepare the loadout and start the selected level.
4. EVVWaveDirector schedules YAML-authored waves and resolves unit ids to configured enemy prefabs.
5. PlantPlacementManager handles placement and routes world interactions.
6. Defenders, enemies, projectiles, pickups, health, and wallet systems communicate through focused components and events.
7. Level completion persists progress, resets board state, applies unlocks, and advances to the next discovered level.

## Level And Menu Layer

### EVVLevelLoader

Discovers and parses Assets/Levels/*.yml into EVVLevelDefinition objects. Discovery order is stage then level.

### EVVMainMenuController

Controls the scene-authored MainMenu Canvas, including panel transitions, stage-list population, settings, scene hand-off, and the optional defender showcase. Layout and artwork remain scene data.

### EVVLevelSelectUI

Owns the pre-level and between-level flow inside the gameplay scene. It coordinates the loadout, starts EVVWaveDirector, resets completed levels, applies unlocks, and selects the next level.

### EVVDefenderCatalog, EVVDefenderUnlocks, And EVVLevelCompletion

EVVDefenderCatalog is the scene source of truth for defender ids, prefabs, display names, costs, and default unlocks. EVVDefenderUnlocks persists unlocked ids while keeping the current loadout in memory for the game session. EVVLevelCompletion persists completed level ids for progression and menu presentation.

### EVVWaveDirector

Builds lanes from EVVTilemapBoard, schedules wave groups, creates configured enemies, tracks living enemies, and emits level start/completion events.

EVVEnemyLaneSpawner is a separate time-ramping spawner and is not the YAML wave scheduler. New level work should use EVVWaveDirector unless a scene intentionally uses the alternate spawner.

## Board And Input Layer

### PlantPlacementManager

Coordinates defender selection, placement previews, tile validation, spending, occupied-cell state, defender removal, pickup collection, and potion click routing.

The class currently lives in CharPlacementManagement.cs; the mismatch is retained for compatibility and should be corrected in a dedicated rename.

### EVVWorldPointer

Shared utility for:

- converting the mouse position to the world plane
- exact Collider2D target detection
- visual SpriteRenderer-bounds fallback
- optional feature-specific target filters

Current consumers are placement pointer conversion, defender removal, board-pickup collection, and healing-potion targeting. Creation uses the shared position but still maps it through the tilemap because it selects a cell rather than an existing object.

### EVVTilemapBoard And EVVLaneDepth

EVVTilemapBoard exposes the gameplay grid and tilemap. EVVLaneDepth maps lane indices to Z depth and applies gameplay sorting. Pointer distance remains an X/Y operation because Z represents lane/sorting state rather than cursor position.

## Defender And Combat Layer

### EVVDefender

Stores placed-cell state, initializes EVVHealth and EVVWorldHealthBar, and applies lane depth/sorting.

### EVVHealth

Shared health model with damage, healing, death, and health-change events.

### EVVWorldHealthBar

Runtime world-space bar that listens to EVVHealth. Defender bars are hidden at full health and appear while damaged.

### Attack Components

- EVVRowProjectileShooter performs lane-aware ranged targeting.
- EVVBoardMeleeAttacker performs lane-aware melee targeting.
- EVVDamageProjectile handles projectile movement and damage.
- EVVHitRecoil handles hit response and stun presentation.

## Enemy Layer

IEVVEnemyLaneWalker defines the contract used by lane scheduling and targeting. EVVEnemyVikingWalker is the current concrete walker and owns movement, defender attacks, death handling, and base damage on exit.

## Economy And Pickup Layer

EVVUsableWallet owns resource counts and change events. EVVBoardPickup represents collectible diamonds or healing potions. EVVMinerMiningReward and EVVWizardPotionReward create those pickups, while EVVThrownPickup animates their launch.

## UI Layer

EVVHealingPotionUseController owns healing-specific state: counter activation, aiming, valid-target filtering, highlighting, spending, healing, and feedback. Generic pointer behavior stays in EVVWorldPointer.

EVVUsableCounterUI, EVVLevelProgressUI, and the loadout/select components present wallet, wave, and defender-selection state.

## Rendering Layer

EVVSilhouetteOutlineFeature is a 2D renderer feature on Assets/Settings/Renderer2D.asset. It draws an outline around every character marked with EVVSilhouetteOutline, for art that has no drawn outline (currently the arrogant Viking): around the whole body, and around each limb group (an arm with its joint pieces, a leg, the head) where it moves in front of another part of the same character, so an arm swinging across the torso keeps its outline while joint pieces stay seamless. Outline color and width live on the feature; the limb groups live on the marker.

The feature runs right after the Gameplay sorting layer; the renderer's Camera Sorting Layer Texture is bound to Gameplay only to make that layer its own render batch. It re-renders the Gameplay sprites into a key texture holding each sprite's lane depth, outline id (character + limb group) and sorting order, then draws the outline on a quad around each marked character, only where the outlined part is in front of the visible sprite, so lane sorting stays correct. EVVSilhouetteOutline pushes the outline id and the current sorting order of each of its SpriteRenderers through a MaterialPropertyBlock while it is enabled.

## Legacy Content

The repository still contains platformer-era scripts such as PlayerController, PhysicsObject, Enemy, Door, and Breakable. They are not part of the lane-defense runtime unless a scene references them.
