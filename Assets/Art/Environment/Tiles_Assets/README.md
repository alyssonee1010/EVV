# Cement Modular Packs

These packs are built for a square-by-square board matrix.

Use the sprites inside either `Painted` or `Anime` as individual 512x512 cells. The visible tile art is normalized to 496x496 inside each image, so every tile can use the same Unity scale.

Each pack contains:

- `cement_cell_clean.png`
- `cement_cell_clean_alt.png`
- `cement_cell_cracked_01.png`
- `cement_cell_cracked_02.png`
- `cement_edge_top.png`
- `cement_edge_bottom.png`
- `cement_edge_left.png`
- `cement_edge_right.png`
- `cement_corner_top_left.png`
- `cement_corner_top_right.png`
- `cement_corner_bottom_left.png`
- `cement_corner_bottom_right.png`

`board_preview_9x5.png` is only a visual example of a repeated board.
`board_20x6.png` is only a flat visual preview assembled from the normalized modules.
`source_4x3_tilesheet.png` is the original generated sheet kept for reference.
`AssetBackups/CementModular_BeforeNormalize` contains backups of the painted edits before the size normalization pass.

For gameplay, use `Assets/Prefabs/EVV Board 20x6.prefab`. It is a real 20 column by 6 row board made from separate tile GameObjects. Each tile has a `EVVTile` component with `Row` and `Column`, and the parent has a `EVVBoard` component with `GetTile(row, column)`.
