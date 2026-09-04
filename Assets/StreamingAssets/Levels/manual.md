# Level Files

Level definitions live in Assets/Levels and are loaded by VVELevelLoader.

## File Naming

Use NN-NN.yml or NN-NN_name.yml. Other YAML filenames are ignored.

Levels are displayed in stage/level order using their stage and level fields.

## Top-Level Fields

| Field | Purpose |
| --- | --- |
| id | Stable level identifier used during scene hand-off |
| name | Player-facing level name |
| stage | Numeric stage grouping |
| level | Numeric order inside the stage |
| settings.rows | Playable board rows/lane count |
| settings.columns | Playable board columns |
| settings.starting_currency | Diamond wallet value applied when the level starts |
| available_units | Parsed unit-id list; currently not enforced |
| waves | Ordered wave definitions |
| unlocks | Defender ids unlocked after completion |

Unlock ids must exist in VVEDefenderCatalog.

The board is resized to `settings.rows` x `settings.columns` before the level's enemy lanes are built. The legacy `settings.lanes` field remains supported as an alias for `settings.rows`.

## Wave Fields

| Field | Purpose |
| --- | --- |
| time | Legacy absolute start time from level start |
| time_offset | Delay after the previous wave ends |
| tier | normal, flag, or final |
| spawns | One or more spawn groups |

Use either time or time_offset for a wave. Relative time_offset scheduling is preferred for maintainable sequences.

## Spawn Fields

| Field | Purpose |
| --- | --- |
| unit | Unit id configured on VVEWaveDirector |
| count | Number of enemies in the group |
| lane | Lane selector; random is the default |
| time_offset | Delay after this wave starts |
| duration | Window across which the group is distributed |
| spacing | even or random distribution inside duration |
| interval | Legacy fixed spacing used when duration is not positive |

A wave ends at the latest end time of its spawn groups. A relative next-wave offset starts after that point.

After editing a level, verify discovery, unit-id resolution, lane selection, wave timing, completion, unlocks, and next-level order in Play Mode.
