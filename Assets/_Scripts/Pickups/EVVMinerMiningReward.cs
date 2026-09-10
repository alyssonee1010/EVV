using UnityEngine;

public class EVVMinerMiningReward : MonoBehaviour
{
    [Header("Reward Timing")]
    [Min(1)]
    [SerializeField] int eventCallsPerGem = 5;

    [Header("Spawn Assets")]
    [SerializeField] Sprite flareSprite;
    [SerializeField] GameObject diamondPickupPrefab;

    [Header("Spawn Positions")]
    [SerializeField] Transform spawnPoint;
    [SerializeField] Vector2 flareOffset = new Vector2(0.45f, -0.1f);
    [SerializeField] Vector2 diamondStartOffset = new Vector2(0.35f, 0.25f);
    [SerializeField] Vector2 diamondLandingOffset = new Vector2(0.25f, -0.32f);
    [SerializeField] Vector2 diamondLandingRandomRange = new Vector2(0.2f, 0.08f);
    [SerializeField] float diamondDepthNudge = -0.1f;

    [Header("Visuals")]
    [SerializeField] Vector3 flareScale = new Vector3(0.6f, 0.6f, 1f);
    [SerializeField] Vector3 diamondScale = new Vector3(0.55f, 0.55f, 1f);
    [SerializeField] float flareLifetime = 0.05f;

    [Header("Throw")]
    [SerializeField] float diamondThrowDuration = 0.32f;
    [SerializeField] float diamondThrowArcHeight = 0.45f;

    [Header("Collection")]
    [SerializeField] int diamondValue = 1;

    [Header("Audio")]
    [SerializeField] EVVAnimationSoundPlayer soundPlayer;

    [Header("Editor Preview")]
    [SerializeField] bool showDiamondPreview = true;
    [SerializeField] Color diamondPreviewColor = new Color(0.1f, 0.55f, 1f, 0.9f);

    int callsUntilGem;

    void Awake()
    {
        CacheSoundPlayer();
        ResetCounter();
    }

    public void MiningEvent()
    {
        HandleMiningEvent();
    }

    public void Mine()
    {
        HandleMiningEvent();
    }

    public void MineEvent()
    {
        HandleMiningEvent();
    }

    public void OnMineHit()
    {
        HandleMiningEvent();
    }

    public void OnMiningEvent()
    {
        HandleMiningEvent();
    }

    void HandleMiningEvent()
    {
        SpawnFlare();

        callsUntilGem--;
        if (callsUntilGem > 0)
        {
            return;
        }

        SpawnDiamond();
        ResetCounter();
    }

    void ResetCounter()
    {
        callsUntilGem = Mathf.Max(1, eventCallsPerGem);
    }

    void SpawnFlare()
    {
        if (flareSprite == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 worldPosition = origin.TransformPoint(flareOffset);

        GameObject spawned = new GameObject("Mining Flare");
        spawned.transform.position = worldPosition;
        spawned.transform.rotation = Quaternion.identity;
        spawned.transform.localScale = flareScale;

        SpriteRenderer spriteRenderer = spawned.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = flareSprite;
        EVVLaneDepth.ApplyGameplayRenderer(spriteRenderer);

        Destroy(spawned, flareLifetime);
    }

    void SpawnDiamond()
    {
        if (diamondPickupPrefab == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 startPosition = origin.TransformPoint(diamondStartOffset);
        Vector3 landingPosition = origin.TransformPoint(GetRandomizedLandingOffset());
        startPosition.z += diamondDepthNudge;
        landingPosition.z += diamondDepthNudge;

        GameObject diamond = Instantiate(diamondPickupPrefab, startPosition, Quaternion.identity);
        diamond.transform.localScale = diamondScale;
        EVVLaneDepth.ApplyGameplaySortingGroup(diamond);

        EVVBoardPickup pickup = diamond.GetComponent<EVVBoardPickup>();
        if (pickup != null)
        {
            pickup.Initialize(diamondValue, CacheSoundPlayer());
        }

        EVVThrownPickup thrownPickup = diamond.AddComponent<EVVThrownPickup>();
        thrownPickup.Launch(startPosition, landingPosition, diamondThrowArcHeight, diamondThrowDuration);
    }

    Vector2 GetRandomizedLandingOffset()
    {
        float randomX = Random.Range(-diamondLandingRandomRange.x, diamondLandingRandomRange.x);
        float randomY = Random.Range(-diamondLandingRandomRange.y, diamondLandingRandomRange.y);
        return diamondLandingOffset + new Vector2(randomX, randomY);
    }

    EVVAnimationSoundPlayer CacheSoundPlayer()
    {
        if (soundPlayer == null)
        {
            soundPlayer = GetComponent<EVVAnimationSoundPlayer>();
        }

        return soundPlayer;
    }

    void OnDrawGizmosSelected()
    {
        if (!showDiamondPreview || diamondPickupPrefab == null)
        {
            return;
        }

        Transform origin = spawnPoint != null ? spawnPoint : transform;
        Vector3 startPosition = origin.TransformPoint(diamondStartOffset);
        Vector3 landingPosition = origin.TransformPoint(diamondLandingOffset);

        Gizmos.color = diamondPreviewColor;
        Gizmos.DrawWireSphere(startPosition, 0.2f);
        Gizmos.DrawWireSphere(landingPosition, 0.2f);
        Gizmos.DrawLine(startPosition, landingPosition);
    }
}
