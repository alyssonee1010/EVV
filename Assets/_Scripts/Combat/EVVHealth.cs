using System;
using System.Collections.Generic;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Events;

public class EVVHealth : MonoBehaviour
{
    [SerializeField] int maxHealth = 100;
    [SerializeField] bool destroyWhenDead = true;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;
    public float LastDamageRecoilMultiplier { get; private set; } = 1f;

    public event Action<EVVHealth> Died;
    public event Action<EVVHealth, int> HealthChanged;
    // Fired only by TakeDamage. HealthChanged also fires on SetMaxHealth refills and heals.
    public event Action<EVVHealth, int> Damaged;

    [SerializeField] UnityEvent onTakeDamage;

    void Awake()
    {
        CurrentHealth = Mathf.Max(1, maxHealth);
    }


    public void SetMaxHealth(int value, bool refill = true)
    {
        maxHealth = Mathf.Max(1, value);
        if (refill)
        {
            CurrentHealth = maxHealth;
            HealthChanged?.Invoke(this, CurrentHealth);
        }
        else
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, maxHealth);
        }
    }

    public void TakeDamage(int amount, float recoilMultiplier = 1f)
    {
        onTakeDamage.Invoke();
        if (amount <= 0 || !IsAlive)
        {
            return;
        }

        LastDamageRecoilMultiplier = Mathf.Max(0f, recoilMultiplier);
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(this, CurrentHealth);
        Damaged?.Invoke(this, CurrentHealth);

        if (CurrentHealth == 0)
        {
            Died?.Invoke(this);
            if (destroyWhenDead)
            {
                Destroy(gameObject);
            }
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || !IsAlive)
        {
            return;
        }

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(this, CurrentHealth);
    }
}
