using UnityEngine;

public class EVVTile : MonoBehaviour
{
    [SerializeField] int row;
    [SerializeField] int column;

    public int Row => row;
    public int Column => column;
    public bool IsOccupied => Occupant != null;
    public GameObject Occupant { get; private set; }

    public void Setup(int rowIndex, int columnIndex)
    {
        row = rowIndex;
        column = columnIndex;
    }

    public bool CanPlace(GameObject occupant)
    {
        return occupant != null && !IsOccupied;
    }

    public bool TryPlace(GameObject occupant)
    {
        if (!CanPlace(occupant))
        {
            return false;
        }

        Occupant = occupant;
        occupant.transform.SetParent(transform, true);
        occupant.transform.position = transform.position;
        return true;
    }

    public void ClearOccupant()
    {
        Occupant = null;
    }
}
