using System.Collections.Generic;
using UnityEngine;

// Reusable component for potion effects whose additive stacks expire independently.
// Callers may omit a duration to use the shared 60-second default, or provide a
// positive duration for that specific stack.
public class EVVStackingTimedEffect : MonoBehaviour
{
    struct EffectStack
    {
        public float Value;
        public float ExpiresAt;
    }

    [SerializeField, Min(0.01f)] float defaultDurationSeconds = 60f;

    readonly List<EffectStack> stacks = new List<EffectStack>();

    public float DefaultDurationSeconds => Mathf.Max(0.01f, defaultDurationSeconds);

    public float TotalValue
    {
        get
        {
            RemoveExpiredStacks();

            float total = 0f;
            foreach (EffectStack stack in stacks)
            {
                total += stack.Value;
            }

            return total;
        }
    }

    public int ActiveStackCount
    {
        get
        {
            RemoveExpiredStacks();
            return stacks.Count;
        }
    }

    void Update()
    {
        RemoveExpiredStacks();
    }

    public void AddStack(float value, float? durationSeconds = null)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return;
        }

        float duration = durationSeconds.HasValue && durationSeconds.Value > 0f
            ? durationSeconds.Value
            : DefaultDurationSeconds;

        stacks.Add(new EffectStack
        {
            Value = value,
            ExpiresAt = Time.time + duration
        });
    }

    public void Clear()
    {
        stacks.Clear();
    }

    void RemoveExpiredStacks()
    {
        float now = Time.time;
        for (int i = stacks.Count - 1; i >= 0; i--)
        {
            if (now >= stacks[i].ExpiresAt)
            {
                stacks.RemoveAt(i);
            }
        }
    }
}
