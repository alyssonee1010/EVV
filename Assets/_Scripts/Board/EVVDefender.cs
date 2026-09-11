using UnityEngine;

[RequireComponent(typeof(EVVHealth))]
public class EVVDefender : MonoBehaviour
{
    [Header("Character Stats")]
    [SerializeField] public int cost = 12;
    [SerializeField] int maxHealth = 100;

    EVVHealth health;
    EVVWorldHealthBar healthBar;

    public Vector2Int Cell { get; private set; }
    public bool HasCell { get; private set; }
    public EVVHealth Health => health;

    void Awake()
    {
        health = GetComponent<EVVHealth>();
        if (health == null)
        {
            health = gameObject.AddComponent<EVVHealth>();
        }

        health.SetMaxHealth(maxHealth);
        healthBar = GetComponent<EVVWorldHealthBar>();
        if (healthBar == null)
        {
            healthBar = gameObject.AddComponent<EVVWorldHealthBar>();
        }
    }

    void OnEnable()
    {
        EVVTargetRegistry.Add(this);
    }

    void OnDisable()
    {
        EVVTargetRegistry.Remove(this);
    }

    public void SetCell(Vector2Int cell)
    {
        Cell = cell;
        HasCell = true;
        ApplyLaneDepth(cell.y);
    }

    public void ApplyRowSorting(int row)
    {
        ApplyLaneDepth(row);
    }

    public void ApplyLaneDepth(int laneIndex)
    {
        transform.position = EVVLaneDepth.WithLaneZ(transform.position, laneIndex);
        EVVLaneDepth.ApplyGameplaySortingGroup(gameObject);
    }
}
