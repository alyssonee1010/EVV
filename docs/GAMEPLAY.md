# Gameplay

## Session Flow

1. MainMenu discovers level files through EVVLevelLoader.
2. Selecting a level loads the shared gameplay scene and carries the selected level id through EVVPendingLevelSelection.
3. The gameplay scene opens the defender-loadout screen before the wave begins.
4. Starting the level gives EVVWaveDirector the selected level definition.
5. The player places defenders, collects pickups, and uses consumables while scheduled waves spawn.
6. A level completes after every scheduled wave has spawned and all tracked enemies have left the board or been defeated.
7. Completion records the level, resets the board, applies defender unlocks, and opens the loadout for the next discovered level.

## Defenders

Defenders are EVVDefender components backed by EVVHealth. Their behavior comes from focused role components:

- EVVRowProjectileShooter attacks enemies in the same lane.
- EVVBoardMeleeAttacker handles close-range lane attacks.
- EVVMinerMiningReward creates diamond pickups.
- EVVWizardPotionReward creates potion pickups.
- EVVCharmLure (the Girl) charms a Viking that reaches it.

EVVWorldHealthBar is created for defenders at runtime. It appears after damage and hides again when health returns to full. Vikings do not receive this defender health bar.

The defender catalog maps stable ids to prefabs, display names, costs, and default unlock state. Unlocked ids persist across sessions; the player's selected loadout contains up to six defenders and resets when the game restarts.

## Board Interaction

PlantPlacementManager coordinates board input:

- placement maps the shared pointer position to a tilemap cell
- removal finds a placed defender under the pointer
- pickup collection finds a EVVBoardPickup under the pointer
- potion clicks are routed to the active potion controller

EVVWorldPointer owns the shared mouse-to-world conversion and reusable visual hit-testing. Feature rules remain outside the utility; for example, healing accepts only living defenders below full health.

## Resources And Potions

EVVUsableWallet stores diamonds and healing potions.

- Placing defenders spends diamonds.
- Miners generate collectible diamond pickups.
- Potion makers generate collectible healing-potion pickups.
- Healing potion aiming highlights valid damaged defenders.
- Using a potion spends one potion and heals the selected defender.

Balance values belong to level data, catalog entries, prefabs, or serialized scene fields and are intentionally not duplicated in documentation.

## Enemies And Waves

EVVWaveDirector reads the selected level definition, resolves board lanes from the gameplay tilemap, and schedules enemy groups. Unit ids in level data are resolved through the director's configured unit options.

EVVEnemyVikingWalker is the current lane-walker implementation. It moves along its assigned lane, attacks defenders that block it, and damages EVVBoardLife if it reaches the exit.

A Viking that reaches a charm lure rolls once against his EVVCharmResistance. A charmed Viking shows a heart, plays his grab animation, picks the lure up, turns around and walks back to where he spawned, where he leaves the board without damaging the chest. Only the first charmed Viking gets to pick her up: one arriving during his grab is charmed too but has to wait, takes over the claim if the first one falls before grabbing, and otherwise ends up face to face with the carrier and fights him for her. He has switched sides: defenders and projectiles ignore him, while the other Vikings in his lane want the lure too and fight him, and he fights them back on his way out. If he is killed, the lure drops to the ground and runs along the row towards the defenders' side until it is off screen; if he makes it home, it leaves with him. A Viking that resists, or is immune, attacks the lure like any other defender and never rolls for that lure again. A carried lure is spent either way: its cell frees up and it can no longer be healed, removed or targeted. The level still waits for the charmed Viking to leave or die before it completes.

Flag and final waves can display banners. The level progress UI reads the active wave schedule from EVVWaveDirector.

## Failure And Completion

EVVBoardLife represents the chest. Enemy leaks reduce it; reaching zero shows the game-over state and can pause gameplay.

Level completion is separate from board life: it occurs only after the wave schedule finishes and the director's tracked enemies are cleared.
