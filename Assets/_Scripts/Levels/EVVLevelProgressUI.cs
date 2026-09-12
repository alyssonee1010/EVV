using UnityEngine;

// Plants-vs-Zombies-style level progress bar: fills from level start (0%) to the start of the
// last wave (100%), with small markers along the bar for "flag" and "final" waves.
public class EVVLevelProgressUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] EVVWaveDirector waveDirector;

    [Header("Placement")]
    [SerializeField] Vector3 screenAnchorOffset = new Vector3(0f, 0.6f, 0f);
    [SerializeField] float cameraPlaneDistance = 5f;

    [Header("Bar")]
    [SerializeField] Vector2 barSize = new Vector2(6f, 0.35f);
    [SerializeField] Color backgroundColor = new Color(0.08f, 0.08f, 0.1f, 0.9f);
    [SerializeField] Color fillColor = new Color(0.85f, 0.7f, 0.15f, 1f);

    [Header("Markers")]
    [SerializeField] float iconSize = 0.32f;
    [SerializeField] Color flagIconColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] Color finalIconColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    [SerializeField] float progressMarkerSize = 0.28f;
    [SerializeField] Color progressMarkerColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    GameObject root;
    Transform fillTransform;
    Transform progressMarkerTransform;
    float lastWaveTime = 1f;

    void Awake()
    {
        if (waveDirector == null)
        {
            waveDirector = FindAnyObjectByType<EVVWaveDirector>();
        }

        if (waveDirector != null)
        {
            waveDirector.LevelStarted += OnLevelStarted;
            waveDirector.LevelCompleted += OnLevelCompleted;
        }

        SetVisible(false);
    }

    void OnDestroy()
    {
        if (waveDirector != null)
        {
            waveDirector.LevelStarted -= OnLevelStarted;
            waveDirector.LevelCompleted -= OnLevelCompleted;
        }
    }

    void Update()
    {
        if (root == null || !root.activeSelf || waveDirector == null || waveDirector.CurrentLevel == null)
        {
            return;
        }

        UpdatePosition();

        float elapsed = Time.time - waveDirector.LevelStartTime;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, lastWaveTime));
        ApplyProgress(progress);
    }

    void OnLevelStarted(EVVLevelDefinition level)
    {
        // An endless level has no last wave to fill towards.
        if (level.Endless)
        {
            SetVisible(false);
            return;
        }

        BuildBar(level);
        SetVisible(true);
    }

    void OnLevelCompleted(EVVLevelDefinition level)
    {
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.SetActive(visible);
        }
    }

    void BuildBar(EVVLevelDefinition level)
    {
        if (root != null)
        {
            Destroy(root);
        }

        root = new GameObject("Level Progress Bar");

        lastWaveTime = 1f;
        foreach (EVVWaveDefinition wave in level.Waves)
        {
            lastWaveTime = Mathf.Max(lastWaveTime, wave.Time);
        }

        BuildBackground();
        BuildFill();

        foreach (EVVWaveDefinition wave in level.Waves)
        {
            if (wave.Type != EVVWaveType.Normal)
            {
                BuildWaveIcon(wave);
            }
        }

        BuildProgressMarker();
        ApplyProgress(0f);
    }

    void BuildBackground()
    {
        GameObject background = new GameObject("Background");
        background.transform.SetParent(root.transform, false);
        background.transform.localScale = new Vector3(barSize.x, barSize.y, 1f);

        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = EVVFlatSpriteFactory.GetWhiteSprite();
        backgroundRenderer.color = backgroundColor;
        backgroundRenderer.sortingLayerName = "UI";
        backgroundRenderer.sortingOrder = 20000;
    }

    void BuildFill()
    {
        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(root.transform, false);
        fill.transform.localPosition = new Vector3(-barSize.x / 2f, 0f, -0.01f);

        SpriteRenderer fillRenderer = fill.AddComponent<SpriteRenderer>();
        fillRenderer.sprite = EVVFlatSpriteFactory.GetLeftAnchoredWhiteSprite();
        fillRenderer.color = fillColor;
        fillRenderer.sortingLayerName = "UI";
        fillRenderer.sortingOrder = 20001;
        fillTransform = fill.transform;
    }

    void BuildWaveIcon(EVVWaveDefinition wave)
    {
        float t = Mathf.Clamp01(wave.Time / lastWaveTime);
        float x = -barSize.x / 2f + t * barSize.x;

        GameObject icon = new GameObject(wave.Type == EVVWaveType.Final ? "Final Wave Icon" : "Flag Wave Icon");
        icon.transform.SetParent(root.transform, false);
        icon.transform.localPosition = new Vector3(x, 0f, -0.02f);
        icon.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        icon.transform.localScale = new Vector3(iconSize, iconSize, 1f);

        SpriteRenderer iconRenderer = icon.AddComponent<SpriteRenderer>();
        iconRenderer.sprite = EVVFlatSpriteFactory.GetWhiteSprite();
        iconRenderer.color = wave.Type == EVVWaveType.Final ? finalIconColor : flagIconColor;
        iconRenderer.sortingLayerName = "UI";
        iconRenderer.sortingOrder = 20002;
    }

    void BuildProgressMarker()
    {
        GameObject marker = new GameObject("Progress Marker");
        marker.transform.SetParent(root.transform, false);
        marker.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        marker.transform.localScale = new Vector3(progressMarkerSize, progressMarkerSize, 1f);

        SpriteRenderer markerRenderer = marker.AddComponent<SpriteRenderer>();
        markerRenderer.sprite = EVVFlatSpriteFactory.GetWhiteSprite();
        markerRenderer.color = progressMarkerColor;
        markerRenderer.sortingLayerName = "UI";
        markerRenderer.sortingOrder = 20003;
        progressMarkerTransform = marker.transform;
    }

    void ApplyProgress(float progress)
    {
        if (fillTransform != null)
        {
            fillTransform.localScale = new Vector3(barSize.x * progress, barSize.y * 0.82f, 1f);
        }

        if (progressMarkerTransform != null)
        {
            progressMarkerTransform.localPosition = new Vector3(-barSize.x / 2f + progress * barSize.x, 0f, -0.03f);
        }
    }

    void UpdatePosition()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 screenBottomCenter = new Vector3(Screen.width / 2f, 0f, cameraPlaneDistance);
        Vector3 worldBottomCenter = camera.ScreenToWorldPoint(screenBottomCenter);
        worldBottomCenter.z = 0f;
        root.transform.position = worldBottomCenter + screenAnchorOffset;
    }
}
