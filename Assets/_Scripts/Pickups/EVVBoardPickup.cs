using System.Collections;
using UnityEngine;

public class EVVBoardPickup : MonoBehaviour
{
    public enum PickupResource { Diamonds, HealingPotions, SpeedPotions }

    [SerializeField] int value = 1;
    [SerializeField] PickupResource resource = PickupResource.Diamonds;
    [SerializeField] EVVAnimationSoundPlayer collectSoundPlayer;

    [Header("Lifetime")]
    [SerializeField, Min(0f)] float lifetimeSeconds = 6f;

    [Header("Collect Effect")]
    [SerializeField] bool playCollectEffect = true;
    [SerializeField] float collectEffectDuration = 0.18f;
    [SerializeField] float collectEffectScaleMultiplier = 1.55f;
    [SerializeField] Vector3 collectEffectDrift = new Vector3(0f, 0.2f, 0f);
    [SerializeField] Color collectEffectTint = new Color(0.55f, 0.9f, 1f, 1f);

    [Header("Fly To Wallet")]
    [SerializeField] bool flyToWalletOnCollect = true;
    [SerializeField] float flyDuration = 0.45f;
    [SerializeField] float flyArcHeight = 1.2f;
    [SerializeField] float flyEndScale = 0.15f;
    [SerializeField] AnimationCurve flyEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    static int lastCollectedFrame = -1;
    bool collected;
    Coroutine lifetimeRoutine;

    public int Value => value;
    public PickupResource Resource => resource;

    void OnEnable()
    {
        StartLifetimeTimer();
    }

    void OnDisable()
    {
        StopLifetimeTimer();
    }

    public void Initialize(int pickupValue)
    {
        value = pickupValue;
    }

    public void Initialize(int pickupValue, PickupResource pickupResource)
    {
        value = pickupValue;
        resource = pickupResource;
    }

    public void Initialize(int pickupValue, EVVAnimationSoundPlayer soundPlayer)
    {
        value = pickupValue;
        collectSoundPlayer = soundPlayer;
    }

    public void Initialize(int pickupValue, PickupResource pickupResource, EVVAnimationSoundPlayer soundPlayer)
    {
        value = pickupValue;
        resource = pickupResource;
        collectSoundPlayer = soundPlayer;
    }

    public bool Collect()
    {
        if (collected || lastCollectedFrame == Time.frameCount)
        {
            return false;
        }

        collected = true;
        lastCollectedFrame = Time.frameCount;
        StopLifetimeTimer();
        SetClickable(false);

        EVVUsableWallet wallet = EVVUsableWallet.Instance != null ? EVVUsableWallet.Instance : FindAnyObjectByType<EVVUsableWallet>();
        if (wallet != null)
        {
            if (resource == PickupResource.HealingPotions)
            {
                wallet.AddHealingPotions(value);
            }
            else if (resource == PickupResource.SpeedPotions)
            {
                wallet.AddSpeedPotions(value);
            }
            else
            {
                wallet.AddDiamonds(value);
            }
        }

        if (collectSoundPlayer != null)
        {
            collectSoundPlayer.PlayCollectSounds();
        }

        PlayCollectEffect();

        Transform flyTarget = flyToWalletOnCollect ? GetFlyTarget() : null;
        if (flyTarget != null)
        {
            StartCoroutine(FlyToWalletThenDestroy(flyTarget));
        }
        else
        {
            Destroy(gameObject);
        }

        return true;
    }

    Transform GetFlyTarget()
    {
        if (resource == PickupResource.HealingPotions)
        {
            return EVVUsableCounterUI.HealingPotionTarget;
        }

        if (resource == PickupResource.SpeedPotions)
        {
            return EVVUsableCounterUI.SpeedPotionTarget;
        }

        return EVVUsableCounterUI.DiamondTarget;
    }

    void SetClickable(bool clickable)
    {
        foreach (Collider2D pickupCollider in GetComponentsInChildren<Collider2D>())
        {
            pickupCollider.enabled = clickable;
        }
    }

    IEnumerator FlyToWalletThenDestroy(Transform target)
    {
        EVVThrownPickup thrownPickup = GetComponent<EVVThrownPickup>();
        if (thrownPickup != null)
        {
            thrownPickup.enabled = false;
        }

        Vector3 startPosition = transform.position;
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;

        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flyDuration);
            float eased = flyEase.Evaluate(t);

            Vector3 nextPosition = Vector3.Lerp(startPosition, target.position, eased);
            nextPosition.y += Mathf.Sin(t * Mathf.PI) * flyArcHeight;
            transform.position = nextPosition;
            transform.localScale = Vector3.Lerp(startScale, startScale * flyEndScale, eased);

            yield return null;
        }

        Destroy(gameObject);
    }

    void StartLifetimeTimer()
    {
        StopLifetimeTimer();
        if (lifetimeSeconds <= 0f)
        {
            return;
        }

        lifetimeRoutine = StartCoroutine(LifetimeRoutine());
    }

    void StopLifetimeTimer()
    {
        if (lifetimeRoutine == null)
        {
            return;
        }

        StopCoroutine(lifetimeRoutine);
        lifetimeRoutine = null;
    }

    IEnumerator LifetimeRoutine()
    {
        yield return new WaitForSeconds(lifetimeSeconds);

        if (!collected)
        {
            Destroy(gameObject);
        }
    }

    void PlayCollectEffect()
    {
        if (!playCollectEffect)
        {
            return;
        }

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        GameObject effectObject = new GameObject(name + " Collect Effect");
        EVVPickupCollectEffect effect = effectObject.AddComponent<EVVPickupCollectEffect>();
        effect.Initialize(spriteRenderer, collectEffectDuration, collectEffectScaleMultiplier, collectEffectDrift, collectEffectTint);
    }
}
