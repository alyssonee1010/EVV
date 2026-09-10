using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(EVVHealth))]
public class EVVHitRecoil : MonoBehaviour
{
    [SerializeField] Transform recoilTarget;
    [SerializeField] bool applyInWorldSpace = true;
    [SerializeField] bool returnAfterRecoil = true;
    [SerializeField] Vector2 recoilDirection = Vector2.right;
    [SerializeField] float recoilDistance = 0.12f;
    [SerializeField] float recoilOutSeconds = 0.05f;
    [SerializeField] float recoilReturnSeconds = 0.12f;

    [Header("Stun Animation")]
    [SerializeField] bool triggerStunAnimation = true;
    const string StunTriggerName = "Stun";

    [Header("Stun Gameplay")]
    [Range(0f, 1f)]
    [SerializeField] float stunHitChance;
    [Min(0f)]
    [SerializeField] float stunResistancePercent;

    [Header("Hit Feedback")]
    [SerializeField] Color hitFlashTint = new Color(1f, 0.22f, 0.16f, 1f);
    const float HitFlashSeconds = 0.08f;

    [Header("Audio")]
    [SerializeField] EVVAnimationSoundPlayer soundPlayer;

    EVVHealth health;
    Animator animator;
    int lastHealth;
    float recoilTimer;
    Vector3 appliedOffset;
    float currentRecoilMultiplier = 1f;
    Coroutine hitFlashRoutine;
    readonly Dictionary<SpriteRenderer, Color> hitFlashOriginalColors = new Dictionary<SpriteRenderer, Color>();

    float TotalSeconds => Mathf.Max(0.001f, recoilOutSeconds + recoilReturnSeconds);

    public float StunHitChance => Mathf.Clamp01(stunHitChance);
    public float StunResistancePercent => Mathf.Max(0f, stunResistancePercent);

    void Awake()
    {
        health = GetComponent<EVVHealth>();
        if (recoilTarget == null)
        {
            recoilTarget = transform;
        }

        animator = GetComponent<Animator>();

        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }
    }

    void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<EVVHealth>();
        }

        lastHealth = health != null ? health.CurrentHealth : 0;
        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
        }
    }

    void OnDisable()
    {
        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            hitFlashRoutine = null;
        }

        RestoreHitFlashColors();

        if (returnAfterRecoil)
        {
            RemoveAppliedOffset();
        }

        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    void LateUpdate()
    {
        if (recoilTarget == null)
        {
            return;
        }

        if (!returnAfterRecoil)
        {
            ApplyPermanentRecoil();
            return;
        }

        RemoveAppliedOffset();
        if (recoilTimer <= 0f)
        {
            return;
        }

        recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
        appliedOffset = GetCurrentOffset();
        ApplyOffset(appliedOffset);
    }

    void ApplyPermanentRecoil()
    {
        if (recoilTimer <= 0f)
        {
            appliedOffset = Vector3.zero;
            return;
        }

        recoilTimer = Mathf.Max(0f, recoilTimer - Time.deltaTime);
        Vector3 currentOffset = GetCurrentPushOffset();
        Vector3 deltaOffset = currentOffset - appliedOffset;
        appliedOffset = currentOffset;
        ApplyOffset(deltaOffset);
    }

    void OnHealthChanged(EVVHealth changedHealth, int currentHealth)
    {
        if (currentHealth < lastHealth)
        {
            if (!returnAfterRecoil)
            {
                appliedOffset = Vector3.zero;
            }

            PlayHitSound();
            PlayHitFlash();
            currentRecoilMultiplier = changedHealth != null ? changedHealth.LastDamageRecoilMultiplier : 1f;
            recoilTimer = TotalSeconds;
        }

        lastHealth = currentHealth;
    }

    bool PlayStunAnimation(int currentHealth)
    {
        if (!triggerStunAnimation
            || currentHealth <= 0
            || animator == null
            || !HasAnimatorTrigger(StunTriggerName))
        {
            return false;
        }

        animator.ResetTrigger(StunTriggerName);
        animator.SetTrigger(StunTriggerName);
        return true;
    }

    bool HasAnimatorTrigger(string triggerName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        foreach (AnimatorControllerParameter parameter in parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryStunTarget(GameObject target)
    {
        if (target == null || StunHitChance <= 0f)
        {
            return false;
        }

        EVVHitRecoil targetStun = target.GetComponent<EVVHitRecoil>();
        if (targetStun == null)
        {
            targetStun = target.GetComponentInParent<EVVHitRecoil>();
        }

        return targetStun != null && targetStun.TryReceiveStun(StunHitChance);
    }

    public bool TryReceiveStun(float incomingChance)
    {
        if (health == null)
        {
            health = GetComponent<EVVHealth>();
        }

        if (health != null && !health.IsAlive)
        {
            return false;
        }

        float resistanceMultiplier = Mathf.Clamp01(1f - StunResistancePercent / 200f);
        float effectiveChance = Mathf.Clamp01(incomingChance) * resistanceMultiplier;
        if (effectiveChance <= 0f || Random.value > effectiveChance)
        {
            return false;
        }

        return PlayStunAnimation(health != null ? health.CurrentHealth : 1);
    }

    void PlayHitSound()
    {
        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }

        if (soundPlayer != null)
        {
            soundPlayer.PlayHitSounds();
        }
    }

    void PlayHitFlash()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (hitFlashRoutine != null)
        {
            StopCoroutine(hitFlashRoutine);
            RestoreHitFlashColors();
        }

        hitFlashRoutine = StartCoroutine(HitFlashRoutine());
    }

    IEnumerator HitFlashRoutine()
    {
        CaptureHitFlashRenderers();
        if (hitFlashOriginalColors.Count == 0)
        {
            hitFlashRoutine = null;
            yield break;
        }

        foreach (SpriteRenderer renderer in hitFlashOriginalColors.Keys)
        {
            if (renderer != null)
            {
                renderer.color = hitFlashTint;
            }
        }

        yield return new WaitForSeconds(HitFlashSeconds);

        RestoreHitFlashColors();
        hitFlashRoutine = null;
    }

    void CaptureHitFlashRenderers()
    {
        hitFlashOriginalColors.Clear();
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || IsHealthBarRenderer(renderer))
            {
                continue;
            }

            hitFlashOriginalColors[renderer] = renderer.color;
        }
    }

    void RestoreHitFlashColors()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> entry in hitFlashOriginalColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = entry.Value;
            }
        }

        hitFlashOriginalColors.Clear();
    }

    bool IsHealthBarRenderer(SpriteRenderer renderer)
    {
        Transform candidate = renderer.transform;
        while (candidate != null && candidate != transform)
        {
            if (candidate.name == "Health Bar")
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }

    Vector3 GetCurrentPushOffset()
    {
        Vector2 direction = recoilDirection.sqrMagnitude > 0f ? recoilDirection.normalized : Vector2.right;
        float elapsed = TotalSeconds - recoilTimer;
        float t = recoilOutSeconds <= 0f ? 1f : elapsed / recoilOutSeconds;
        float amount = Mathf.Lerp(0f, recoilDistance * currentRecoilMultiplier, Smooth(t));
        return new Vector3(direction.x, direction.y, 0f) * amount;
    }

    Vector3 GetCurrentOffset()
    {
        Vector2 direction = recoilDirection.sqrMagnitude > 0f ? recoilDirection.normalized : Vector2.right;
        float elapsed = TotalSeconds - recoilTimer;
        float amount;

        if (elapsed <= recoilOutSeconds)
        {
            float t = recoilOutSeconds <= 0f ? 1f : elapsed / recoilOutSeconds;
            amount = Mathf.Lerp(0f, recoilDistance * currentRecoilMultiplier, Smooth(t));
        }
        else
        {
            float t = recoilReturnSeconds <= 0f ? 1f : (elapsed - recoilOutSeconds) / recoilReturnSeconds;
            amount = Mathf.Lerp(recoilDistance * currentRecoilMultiplier, 0f, Smooth(t));
        }

        return new Vector3(direction.x, direction.y, 0f) * amount;
    }

    float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    void ApplyOffset(Vector3 offset)
    {
        if (applyInWorldSpace)
        {
            recoilTarget.position += offset;
            return;
        }

        recoilTarget.localPosition += offset;
    }

    void RemoveAppliedOffset()
    {
        if (recoilTarget == null || appliedOffset == Vector3.zero)
        {
            appliedOffset = Vector3.zero;
            return;
        }

        if (applyInWorldSpace)
        {
            recoilTarget.position -= appliedOffset;
        }
        else
        {
            recoilTarget.localPosition -= appliedOffset;
        }

        appliedOffset = Vector3.zero;
    }
}
