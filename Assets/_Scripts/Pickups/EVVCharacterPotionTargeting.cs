using UnityEngine;

public class EVVCharacterPotionTargeting : MonoBehaviour
{
    [SerializeField] EVVUsableWallet wallet;
    [SerializeField] Sprite potionIcon;
    [SerializeField, Min(0.01f)] float speedPercent = 0.3f;
    [SerializeField, Min(0f), Tooltip("Seconds before each boost expires. Leave at 0 to use the effect component's 60-second default.")]
    float removeEffectAfterSeconds;
    [SerializeField, Min(0.05f)] float targetSearchRadius = 0.55f;
    [SerializeField] EVVCharacterTargetHighlight targetHighlight;
    [SerializeField] Color validTargetColor = new Color(0.55f, 1f, 0.55f, 0.85f);
    [SerializeField] Color invalidTargetColor = new Color(1f, 0.9f, 0.25f, 0.65f);

    static EVVCharacterPotionTargeting instance;

    SpriteRenderer iconRenderer;
    SpriteRenderer cursorRenderer;
    bool isAiming;

    public static bool IsAiming => instance != null && instance.isAiming;

    void Awake()
    {
        instance = this;
        wallet = wallet != null
            ? wallet
            : EVVUsableWallet.Instance != null
                ? EVVUsableWallet.Instance
                : FindAnyObjectByType<EVVUsableWallet>();
        iconRenderer = GetComponent<SpriteRenderer>();
        if (targetHighlight == null)
        {
            targetHighlight = GetComponent<EVVCharacterTargetHighlight>();
        }

        if (targetHighlight == null)
        {
            targetHighlight = gameObject.AddComponent<EVVCharacterTargetHighlight>();
        }

        if (potionIcon == null && iconRenderer != null)
        {
            potionIcon = iconRenderer.sprite;
        }
    }

    void Update()
    {
        if (!isAiming)
        {
            return;
        }

        if (Input.GetMouseButtonDown(1))
        {
            Cancel();
            return;
        }

        UpdateTargetHighlight();
        UpdateCursor();
    }

    void OnDisable()
    {
        StopAiming();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static bool TryHandlePrimaryClick(Vector3 worldPosition)
    {
        return instance != null && instance.HandlePrimaryClick(worldPosition);
    }

    public static void Cancel()
    {
        if (instance != null)
        {
            instance.StopAiming();
        }
    }

    bool HandlePrimaryClick(Vector3 worldPosition)
    {
        if (IsPotionIconClick(worldPosition))
        {
            if (wallet != null && wallet.CanUseSpeedPotion())
            {
                BeginAiming();
            }

            return true;
        }

        if (!isAiming)
        {
            return false;
        }

        EVVDefender target = FindTarget(worldPosition);
        if (target == null)
        {
            return true;
        }

        if (wallet == null || !wallet.TrySpendSpeedPotion())
        {
            StopAiming();
            return true;
        }

        EVVActionSpeedModifier modifier = target.GetComponent<EVVActionSpeedModifier>();
        if (modifier == null)
        {
            modifier = target.gameObject.AddComponent<EVVActionSpeedModifier>();
        }

        float? durationOverride = removeEffectAfterSeconds > 0f
            ? removeEffectAfterSeconds
            : (float?)null;
        modifier.AddPercent(speedPercent, durationOverride);

        float appliedDuration = durationOverride
            ?? target.GetComponent<EVVStackingTimedEffect>().DefaultDurationSeconds;
        Debug.Log(
            $"{target.name} action speed increased by {speedPercent:P0} for {appliedDuration:0.##} seconds.",
            target);
        StopAiming();
        return true;
    }

    bool IsPotionIconClick(Vector3 worldPosition)
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(worldPosition))
        {
            if (hit != null && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                return true;
            }
        }

        return iconRenderer != null && iconRenderer.bounds.Contains(worldPosition);
    }

    EVVDefender FindTarget(Vector3 worldPosition)
    {
        return EVVWorldPointer.FindClosest<EVVDefender>(
            worldPosition,
            targetSearchRadius,
            EVVActionSpeedModifier.CanAffect);
    }

    void BeginAiming()
    {
        isAiming = true;
        if (cursorRenderer == null)
        {
            GameObject cursorObject = new GameObject("Speed Potion Cursor");
            cursorRenderer = cursorObject.AddComponent<SpriteRenderer>();
        }

        cursorRenderer.sprite = potionIcon;
        cursorRenderer.sortingLayerName = "UI";
        cursorRenderer.sortingOrder = 6000;
        cursorRenderer.transform.localScale = transform.lossyScale;
        UpdateTargetHighlight();
        UpdateCursor();
    }

    void StopAiming()
    {
        isAiming = false;
        ClearTargetHighlight();
        if (cursorRenderer != null)
        {
            Destroy(cursorRenderer.gameObject);
            cursorRenderer = null;
        }
    }

    void UpdateCursor()
    {
        if (cursorRenderer == null)
        {
            return;
        }

        Vector3 pointerPosition = EVVWorldPointer.GetPosition();
        cursorRenderer.transform.position = pointerPosition;
        cursorRenderer.color = FindTarget(pointerPosition) != null
            ? validTargetColor
            : invalidTargetColor;
    }

    void UpdateTargetHighlight()
    {
        if (targetHighlight != null)
        {
            targetHighlight.Show(FindTarget(EVVWorldPointer.GetPosition()));
        }
    }

    void ClearTargetHighlight()
    {
        if (targetHighlight != null)
        {
            targetHighlight.Clear();
        }
    }
}
