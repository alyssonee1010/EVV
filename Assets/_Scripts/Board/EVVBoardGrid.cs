using System.Collections.Generic;
using UnityEngine;

// Procedurally builds a rows x columns grid of EVVTile cells sized by cellSize, instead of a
// hand-painted Tilemap. [ExecuteAlways] + the Update() diff check (rather than OnValidate) is
// deliberate: Tilemap/GameObject edits triggered from inside OnValidate are unreliable in the
// Unity editor, but rebuilding from a normal Update tick behaves like any other runtime code and
// reflects Inspector changes (Rows, Columns, CellSize) immediately, in both edit mode and play mode.
[ExecuteAlways]
public class EVVBoardGrid : MonoBehaviour
{
    [SerializeField] int rows = 6;
    [SerializeField] int columns = 10;
    [SerializeField] Vector2 cellSize = new Vector2(1f, 1f);
    [SerializeField] Sprite[] whiteCellSprites;
    [SerializeField] Sprite[] blackCellSprites;
    [SerializeField] int randomSeed;
    [SerializeField] Color cellColor = Color.white;
    [SerializeField] string sortingLayerName = "Default";
    [SerializeField] int sortingOrder = 0;

    // World Y the board's vertical middle is pinned to, authored per scene (set it to the
    // gameplay camera's Y to centre the board on screen). Levels change the row count, so
    // without this a one-lane level would grow up from a fixed bottom edge and sit low.
    [SerializeField] float verticalCenterY;

    readonly List<EVVTile> cells = new List<EVVTile>();
    int builtRows = -1;
    int builtColumns = -1;
    Vector2 builtCellSize = Vector2.zero;
    int builtContentSignature;

    public int Rows => rows;
    public int Columns => columns;
    public Vector2 CellSize => cellSize;
    public float BoardWidth => columns * cellSize.x;
    public float BoardHeight => rows * cellSize.y;

    public void SetDimensions(int newRows, int newColumns)
    {
        newRows = Mathf.Max(1, newRows);
        newColumns = Mathf.Max(1, newColumns);

        if (rows == newRows && columns == newColumns)
        {
            return;
        }

        rows = newRows;
        columns = newColumns;
        Rebuild();
    }

    void OnValidate()
    {
        rows = Mathf.Max(0, rows);
        columns = Mathf.Max(0, columns);
        cellSize = new Vector2(Mathf.Max(0.01f, cellSize.x), Mathf.Max(0.01f, cellSize.y));
    }

    void OnEnable()
    {
        if (TryAdoptExistingCells())
        {
            return;
        }

        Rebuild();
    }

    // The built* bookkeeping below is plain runtime state, so a scene load or a domain reload
    // (any script recompile) clears it even though the tile children are still sitting in the
    // scene, exactly as this board wants them. Rebuilding then destroys and recreates 60
    // identical tiles with brand-new object ids, which rewrites the entire scene file the next
    // time it is saved. Re-adopt the existing tiles instead when they already match.
    bool TryAdoptExistingCells()
    {
        if (rows <= 0 || columns <= 0 || transform.childCount != rows * columns)
        {
            return false;
        }

        cells.Clear();
        foreach (Transform child in transform)
        {
            EVVTile tile = child.GetComponent<EVVTile>();
            if (tile == null)
            {
                cells.Clear();
                return false;
            }

            cells.Add(tile);
        }

        builtRows = rows;
        builtColumns = columns;
        builtCellSize = cellSize;
        builtContentSignature = ComputeContentSignature();
        return true;
    }

    void Update()
    {
        int contentSignature = ComputeContentSignature();
        if (rows != builtRows || columns != builtColumns || cellSize != builtCellSize || contentSignature != builtContentSignature)
        {
            Rebuild();
        }
    }

    void Rebuild()
    {
        ApplyVerticalCentering();

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
        }

        cells.Clear();

        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                GameObject cellObject = new GameObject($"Tile R{row:00} C{column:00}");
                cellObject.transform.SetParent(transform, false);
                cellObject.transform.localPosition = GetCellCenterLocal(row, column);
                cellObject.transform.localScale = new Vector3(cellSize.x, cellSize.y, 1f);

                bool isWhiteCell = (row + column) % 2 == 0;
                Sprite[] cellSpriteSet = isWhiteCell ? whiteCellSprites : blackCellSprites;

                SpriteRenderer spriteRenderer = cellObject.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = PickSprite(cellSpriteSet, row, column);
                spriteRenderer.color = cellColor;
                spriteRenderer.sortingLayerName = sortingLayerName;
                spriteRenderer.sortingOrder = sortingOrder;

                EVVTile tile = cellObject.AddComponent<EVVTile>();
                tile.Setup(row, column);
                cells.Add(tile);
            }
        }

        builtRows = rows;
        builtColumns = columns;
        builtCellSize = cellSize;
        builtContentSignature = ComputeContentSignature();
    }

    // Keeps the board's vertical middle on verticalCenterY, so changing the row count per level
    // recentres around that point instead of growing up from a fixed bottom edge. The centre is
    // authored rather than captured from the transform at load: the captured version read whatever
    // position the board happened to be at, which both centred on the wrong point and went stale
    // the moment the board was moved in the editor, silently snapping it back on the next rebuild.
    void ApplyVerticalCentering()
    {
        Vector3 position = transform.localPosition;
        position.y = verticalCenterY - BoardHeight / 2f;
        transform.localPosition = position;
    }

    // Picks a sprite from the set deterministically from (randomSeed, row, column), so a given
    // cell always shows the same sprite for a given seed regardless of rebuild order or how many
    // times the board has been resized.
    Sprite PickSprite(Sprite[] spriteSet, int row, int column)
    {
        if (spriteSet == null || spriteSet.Length == 0)
        {
            return null;
        }

        int cellHash = HashCell(randomSeed, row, column);
        int index = (int)((uint)cellHash % (uint)spriteSet.Length);
        return spriteSet[index];
    }

    static int HashCell(int seed, int row, int column)
    {
        unchecked
        {
            int hash = seed;
            hash = hash * 397 ^ row;
            hash = hash * 397 ^ column;
            return hash;
        }
    }

    int ComputeContentSignature()
    {
        unchecked
        {
            int hash = randomSeed;
            hash = hash * 397 ^ SpriteArrayHash(whiteCellSprites);
            hash = hash * 397 ^ SpriteArrayHash(blackCellSprites);
            return hash;
        }
    }

    static int SpriteArrayHash(Sprite[] sprites)
    {
        unchecked
        {
            int hash = sprites?.Length ?? 0;
            if (sprites != null)
            {
                foreach (Sprite sprite in sprites)
                {
                    hash = hash * 397 ^ (sprite != null ? sprite.GetHashCode() : 0);
                }
            }

            return hash;
        }
    }

    public Vector3 GetCellCenterLocal(int row, int column)
    {
        return new Vector3((column + 0.5f) * cellSize.x, (row + 0.5f) * cellSize.y, 0f);
    }

    public Vector3 GetCellCenterWorld(int row, int column)
    {
        return transform.TransformPoint(GetCellCenterLocal(row, column));
    }

    public bool IsValidCell(int row, int column)
    {
        return row >= 0 && row < rows && column >= 0 && column < columns;
    }

    public EVVTile GetTile(int row, int column)
    {
        if (!IsValidCell(row, column))
        {
            return null;
        }

        int index = row * columns + column;
        return index >= 0 && index < cells.Count ? cells[index] : null;
    }

    public bool TryGetCellFromWorldPosition(Vector3 worldPosition, out int row, out int column)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        column = Mathf.FloorToInt(local.x / cellSize.x);
        row = Mathf.FloorToInt(local.y / cellSize.y);
        return IsValidCell(row, column);
    }
}
