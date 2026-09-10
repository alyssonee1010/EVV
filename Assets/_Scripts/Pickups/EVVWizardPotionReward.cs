using UnityEngine;

public class EVVWizardPotionReward : MonoBehaviour
{
    [Header("Reward Timing")]
    [Min(1)]
    [SerializeField] int eventCallsPerPotion = 5;
    [SerializeField] bool useAutomaticBrewing = true;
    [SerializeField, Min(0.1f)] float automaticBrewInterval = 3f;

    [Header("Spawn Assets")]
    [SerializeField] Sprite potionSprite;
    [SerializeField] EVVBoardPickup.PickupResource potionResource = EVVBoardPickup.PickupResource.HealingPotions;
    [SerializeField] string templatePotionName = "";

    [Header("Spawn Positions")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] Vector2 potionStartOffset = new Vector2(0.28f, 0.25f);
    [SerializeField] Vector2 potionLandingOffset = new Vector2(0.22f, -0.36f);
    [SerializeField] Vector2 potionLandingRandomRange = new Vector2(0.16f, 0.08f);
    [SerializeField] float potionDepthNudge = -0.1f;

    [Header("Visuals")]
    [SerializeField] Vector3 potionScale = new Vector3(1.4f, 1.4f, 1f);

    [Header("Throw")]
    [SerializeField] float potionThrowDuration = 0.32f;
    [SerializeField] float potionThrowArcHeight = 0.45f;

    [Header("Collection")]
    [SerializeField] int potionValue = 1;
    [SerializeField] float potionColliderRadius = 0.3f;

    [Header("Audio")]
    [SerializeField] EVVAnimationSoundPlayer soundPlayer;

    [Header("Editor Preview")]
    [SerializeField] bool showPotionPreview = true;
    [SerializeField] Color potionVisualPreviewColor = new Color(1f, 0.25f, 0.35f, 0.9f);
    [SerializeField] Color potionColliderPreviewColor = new Color(0.1f, 1f, 0.25f, 0.9f);

    int callsUntilPotion;
    float automaticBrewTimer;
    SpriteRenderer templatePotionRenderer;

    void Awake()
    {
        CacheSoundPlayer();
        ResetCounter();
        automaticBrewTimer = automaticBrewInterval;
    }

    void Update()
    {
        if (!useAutomaticBrewing)
        {
            return;
        }

        automaticBrewTimer -= Time.deltaTime * EVVActionSpeedModifier.GetMultiplier(this);
        if (automaticBrewTimer > 0f)
        {
            return;
        }

        automaticBrewTimer = automaticBrewInterval;
        SpawnPotion();
    }

    public void Brew()
    {
        HandlePotionEvent();
    }

    public void PotionEvent()
    {
        HandlePotionEvent();
    }

    public void OnPotionEvent()
    {
        HandlePotionEvent();
    }

    public void MiningEvent()
    {
        HandlePotionEvent();
    }

    public void Mine()
    {
        HandlePotionEvent();
    }

    void HandlePotionEvent()
    {
        callsUntilPotion--;
        if (callsUntilPotion > 0)
        {
            return;
        }

        SpawnPotion();
        ResetCounter();
    }

    void ResetCounter()
    {
        callsUntilPotion = Mathf.Max(1, eventCallsPerPotion);
    }

    void SpawnPotion()
    {
        Sprite sprite = GetPotionSprite();
        if (sprite == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 startPosition = origin.TransformPoint(potionStartOffset);
        Vector3 landingPosition = origin.TransformPoint(GetRandomizedLandingOffset());
        startPosition.z += potionDepthNudge;
        landingPosition.z += potionDepthNudge;
        GameObject potion = SpawnPotionObject(startPosition, sprite);
        if (potion == null)
        {
            return;
        }

        EVVThrownPickup thrownPickup = potion.AddComponent<EVVThrownPickup>();
        thrownPickup.Launch(startPosition, landingPosition, potionThrowArcHeight, potionThrowDuration);
    }

    GameObject SpawnPotionObject(Vector3 worldPosition, Sprite sprite)
    {
        GameObject spawned = new GameObject(GetPotionName());
        spawned.transform.position = worldPosition;
        spawned.transform.rotation = Quaternion.identity;
        spawned.transform.localScale = GetPotionScale();

        SpriteRenderer spriteRenderer = spawned.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        ApplySorting(spriteRenderer);

        CircleCollider2D collider = spawned.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = potionColliderRadius;

        EVVBoardPickup pickup = spawned.AddComponent<EVVBoardPickup>();
        pickup.Initialize(potionValue, potionResource, CacheSoundPlayer());
        return spawned;
    }

    string GetPotionName()
    {
        return potionResource == EVVBoardPickup.PickupResource.SpeedPotions
            ? "Speed Potion"
            : "Healing Potion";
    }

    void ApplySorting(SpriteRenderer spawnedRenderer)
    {
        EVVLaneDepth.ApplyGameplayRenderer(spawnedRenderer);
    }

    Vector2 GetRandomizedLandingOffset()
    {
        float randomX = Random.Range(-potionLandingRandomRange.x, potionLandingRandomRange.x);
        float randomY = Random.Range(-potionLandingRandomRange.y, potionLandingRandomRange.y);
        return potionLandingOffset + new Vector2(randomX, randomY);
    }

    EVVAnimationSoundPlayer CacheSoundPlayer()
    {
        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }

        return soundPlayer;
    }

    Sprite GetPotionSprite()
    {
        SpriteRenderer templateRenderer = GetTemplatePotionRenderer();
        if (templateRenderer != null && templateRenderer.sprite != null)
        {
            return templateRenderer.sprite;
        }

        return potionSprite;
    }

    Vector3 GetPotionScale()
    {
        return potionScale;
    }

    SpriteRenderer GetTemplatePotionRenderer()
    {
        if (templatePotionRenderer != null)
        {
            return templatePotionRenderer;
        }

        if (string.IsNullOrEmpty(templatePotionName))
        {
            return null;
        }

        GameObject templatePotion = GameObject.Find(templatePotionName);
        if (templatePotion != null)
        {
            templatePotionRenderer = templatePotion.GetComponentInChildren<SpriteRenderer>();
        }

        return templatePotionRenderer;
    }

    void OnDrawGizmosSelected()
    {
        Sprite sprite = GetPotionSprite();
        if (!showPotionPreview || sprite == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 startPosition = origin.TransformPoint(potionStartOffset);
        Vector3 landingPosition = origin.TransformPoint(potionLandingOffset);
        Vector3 scale = GetPotionScale();
        Vector3 visualSize = Vector3.Scale(sprite.bounds.size, scale);
        float colliderPreviewRadius = potionColliderRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

        Gizmos.color = potionVisualPreviewColor;
        Gizmos.DrawWireCube(startPosition, visualSize);
        Gizmos.DrawWireCube(landingPosition, visualSize);
        Gizmos.DrawLine(startPosition, landingPosition);

        Gizmos.color = potionColliderPreviewColor;
        Gizmos.DrawWireSphere(startPosition, colliderPreviewRadius);
        Gizmos.DrawWireSphere(landingPosition, colliderPreviewRadius);
    }
}
