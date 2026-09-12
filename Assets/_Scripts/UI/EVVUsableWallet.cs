using System;
using UnityEngine;

public class EVVUsableWallet : MonoBehaviour
{
    [SerializeField] int startingDiamonds = 5;
    [SerializeField] int startingHealingPotions;
    [SerializeField] int startingSpeedPotions;

    public static EVVUsableWallet Instance { get; private set; }

    public int Diamonds { get; private set; }
    public int HealingPotions { get; private set; }
    public int SpeedPotions { get; private set; }

    public event Action<int> DiamondsChanged;
    public event Action<int> HealingPotionsChanged;
    public event Action<int> SpeedPotionsChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        Diamonds = Mathf.Max(0, startingDiamonds);
        HealingPotions = Mathf.Max(0, startingHealingPotions);
        SpeedPotions = Mathf.Max(0, startingSpeedPotions);
    }

    void OnEnable()
    {
        DiamondsChanged?.Invoke(Diamonds);
        HealingPotionsChanged?.Invoke(HealingPotions);
        SpeedPotionsChanged?.Invoke(SpeedPotions);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F3))
        {
            AddDiamonds(100);
        }
    }

    public bool CanAfford(int cost)
    {
        return Diamonds >= Mathf.Max(0, cost);
    }

    public bool TrySpendDiamonds(int cost)
    {
        cost = Mathf.Max(0, cost);
        if (!CanAfford(cost))
        {
            return false;
        }

        Diamonds -= cost;
        DiamondsChanged?.Invoke(Diamonds);
        return true;
    }

    public void AddDiamonds(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        Diamonds += amount;
        DiamondsChanged?.Invoke(Diamonds);
    }

    public void SetDiamonds(int amount)
    {
        Diamonds = Mathf.Max(0, amount);
        DiamondsChanged?.Invoke(Diamonds);
    }

    public bool CanUseHealingPotion(int cost = 1)
    {
        return HealingPotions >= Mathf.Max(1, cost);
    }

    public bool TrySpendHealingPotion(int cost = 1)
    {
        cost = Mathf.Max(1, cost);
        if (!CanUseHealingPotion(cost))
        {
            return false;
        }

        HealingPotions -= cost;
        HealingPotionsChanged?.Invoke(HealingPotions);
        return true;
    }

    public void AddHealingPotions(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        HealingPotions += amount;
        HealingPotionsChanged?.Invoke(HealingPotions);
    }

    public void SetHealingPotions(int amount)
    {
        HealingPotions = Mathf.Max(0, amount);
        HealingPotionsChanged?.Invoke(HealingPotions);
    }

    public bool CanUseSpeedPotion(int cost = 1)
    {
        return SpeedPotions >= Mathf.Max(1, cost);
    }

    public bool TrySpendSpeedPotion(int cost = 1)
    {
        cost = Mathf.Max(1, cost);
        if (!CanUseSpeedPotion(cost))
        {
            return false;
        }

        SpeedPotions -= cost;
        SpeedPotionsChanged?.Invoke(SpeedPotions);
        return true;
    }

    public void AddSpeedPotions(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SpeedPotions += amount;
        SpeedPotionsChanged?.Invoke(SpeedPotions);
    }

    public void SetSpeedPotions(int amount)
    {
        SpeedPotions = Mathf.Max(0, amount);
        SpeedPotionsChanged?.Invoke(SpeedPotions);
    }
}
