using System.Linq;
using UnityEngine;

public class EVVDefenderSelectionUi : MonoBehaviour
{

    public int Cols = 3;
    public int Rows = 3;
    public float Gap = 0.2f;
    public Vector2 CellSize => cardPrefab.GetComponent<BoxCollider2D>().size;

    [SerializeField] EVVDefenderCard cardPrefab;

    void Start()
    {
        EVVManager.OnToggleMenu += ToggleDefenderSelectionUI;
        EVVDefenderUnlocks.UnlocksChanged += RefreshUnlockedCards;
        gameObject.SetActive(EVVManager.Instance.MenuIsOpen);
    }

    void  OnDestroy() {
        EVVManager.OnToggleMenu -= ToggleDefenderSelectionUI;
        EVVDefenderUnlocks.UnlocksChanged -= RefreshUnlockedCards;
    }

    public Vector3 GetDefenderPositionInCatalog(EVVDefender type)
    {
        var entries = EVVDefenderCatalog.Instance.Entries
            .Where(IsAvailable)
            .ToList();
        var index = entries.TakeWhile(entry => entry.prefab != type).Count();

        var width = CellSize.x * Cols;
        var height = CellSize.y * Rows;

        var col = index % Cols;
        var row = index / Cols;

        var x = (-0.5f*width) + (CellSize.x + 0.5f*Gap) * (col + 0.5f);
        var y = -1 * ((-0.5f*height) + (CellSize.y + 0.5f*Gap) * (row + 0.5f));

        return new Vector3(x,y,0);
    }

    public Vector3 GetDefenderPosition(EVVDefender type)
    {
        return transform.position + GetDefenderPositionInCatalog(type);
    }

    void BuildGrid()
    {
        foreach (var entry in EVVDefenderCatalog.Instance.Entries)
        {
            if (!IsAvailable(entry))
                continue;

            if (EVVManager.Instance.SelectedDefenders.Contains(entry.prefab))
                continue;

            var obj = Instantiate(cardPrefab, transform);
            obj.defenderType = entry.prefab;
            obj.transform.position = GetDefenderPosition(entry.prefab);
        }
    }

    bool IsAvailable(EVVDefenderCatalog.Entry entry)
    {
        return entry != null
            && entry.prefab != null
            && !string.IsNullOrEmpty(entry.id)
            && EVVDefenderUnlocks.IsUnlocked(entry.id);
    }

    void RefreshUnlockedCards()
    {
        if (!gameObject.activeSelf)
            return;

        transform.DestroyChildren();
        BuildGrid();
    }

    void ToggleDefenderSelectionUI(bool isOpen)
    {
        gameObject.SetActive(isOpen);
        if (isOpen)
        {
            transform.DestroyChildren();
            BuildGrid();
        }
    }
}
