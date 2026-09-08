using UnityEngine;

// Applies independently expiring action-speed stacks to any defender. The shared
// multiplier drives animation-event actions (such as mining), while timer-driven
// actions query GetMultiplier so their gameplay timing stays in sync with the visuals.
[RequireComponent(typeof(EVVStackingTimedEffect))]
public class EVVActionSpeedModifier : MonoBehaviour
{
    EVVStackingTimedEffect stackingEffect;
    Animator[] animators;
    float[] baseAnimatorSpeeds;

    EVVStackingTimedEffect StackingEffect
    {
        get
        {
            if (stackingEffect == null)
            {
                stackingEffect = GetComponent<EVVStackingTimedEffect>();
                if (stackingEffect == null)
                {
                    stackingEffect = gameObject.AddComponent<EVVStackingTimedEffect>();
                }
            }

            return stackingEffect;
        }
    }

    public float Multiplier => Mathf.Max(1f, 1f + StackingEffect.TotalValue);

    void Awake()
    {
        CacheAnimators();
    }

    void Update()
    {
        ApplyAnimatorSpeed();
    }

    void OnDisable()
    {
        RestoreAnimatorSpeed();
    }

    public void AddPercent(float percent, float? durationSeconds = null)
    {
        if (percent <= 0f)
        {
            return;
        }

        StackingEffect.AddStack(percent, durationSeconds);
        ApplyAnimatorSpeed();
    }

    public static float GetMultiplier(Component action)
    {
        if (action == null)
        {
            return 1f;
        }

        EVVActionSpeedModifier modifier = action.GetComponent<EVVActionSpeedModifier>();
        return modifier != null ? modifier.Multiplier : 1f;
    }

    public static bool CanAffect(EVVDefender defender)
    {
        return defender != null
            && defender.isActiveAndEnabled
            && defender.Health != null
            && defender.Health.IsAlive
            && (defender.GetComponentInChildren<Animator>(true) != null
                || defender.GetComponent<EVVBoardMeleeAttacker>() != null
                || defender.GetComponent<EVVRowProjectileShooter>() != null
                || defender.GetComponent<EVVMinerMiningReward>() != null
                || defender.GetComponent<EVVWizardPotionReward>() != null);
    }

    void CacheAnimators()
    {
        animators = GetComponentsInChildren<Animator>(true);
        baseAnimatorSpeeds = new float[animators.Length];
        for (int i = 0; i < animators.Length; i++)
        {
            baseAnimatorSpeeds[i] = animators[i] != null ? animators[i].speed : 1f;
        }
    }

    void ApplyAnimatorSpeed()
    {
        if (animators == null)
        {
            CacheAnimators();
        }

        float multiplier = Multiplier;
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = baseAnimatorSpeeds[i] * multiplier;
            }
        }
    }

    void RestoreAnimatorSpeed()
    {
        if (animators == null || baseAnimatorSpeeds == null)
        {
            return;
        }

        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
            {
                animators[i].speed = baseAnimatorSpeeds[i];
            }
        }
    }
}
