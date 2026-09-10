using UnityEngine;

[RequireComponent(typeof(EVVHealth))]
public class EVVWorldHealthBar : MonoBehaviour
{
    const string BarRootName = "Health Bar";

    [SerializeField] Vector3 localOffset = new Vector3(0f, 0.72f, 0f);
    [SerializeField, Min(0.05f)] float width = 0.48f;
    [SerializeField, Min(0.01f)] float height = 0.055f;
    [SerializeField, Min(0f)] float inset = 0.008f;
    [SerializeField] Color backgroundColor = new Color(0.05f, 0f, 0f, 0.85f);
    [SerializeField] Color fillColor = new Color(0.95f, 0.05f, 0.03f, 1f);
    [SerializeField] int sortingOrder = 10000;
    [SerializeField] bool hideWhenFull = true;

    EVVHealth health;
    Transform barRoot;
    SpriteRenderer backgroundRenderer;
    SpriteRenderer fillRenderer;

    static Sprite sharedSprite;

    void Awake()
    {
        health = GetComponent<EVVHealth>();
        EnsureBarObjects();
        UpdateBar();
    }

    void OnEnable()
    {
        if (health == null)
        {
            health = GetComponent<EVVHealth>();
        }

        if (health != null)
        {
            health.HealthChanged += OnHealthChanged;
        }

        EnsureBarObjects();
        UpdateBar();
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.HealthChanged -= OnHealthChanged;
        }
    }

    void OnHealthChanged(EVVHealth changedHealth, int currentHealth)
    {
        UpdateBar();
    }

    public void Refresh()
    {
        EnsureBarObjects();
        UpdateBar();
    }

    void EnsureBarObjects()
    {
        if (barRoot == null)
        {
            Transform existingRoot = transform.Find(BarRootName);
            barRoot = existingRoot != null ? existingRoot : new GameObject(BarRootName).transform;
            barRoot.SetParent(transform, false);
        }

        barRoot.localPosition = localOffset;
        barRoot.localRotation = Quaternion.identity;
        barRoot.localScale = Vector3.one;

        backgroundRenderer = EnsureRenderer("Background", backgroundRenderer, backgroundColor, sortingOrder);
        fillRenderer = EnsureRenderer("Fill", fillRenderer, fillColor, sortingOrder + 1);
    }

    SpriteRenderer EnsureRenderer(string childName, SpriteRenderer existingRenderer, Color color, int rendererSortingOrder)
    {
        if (existingRenderer == null && barRoot != null)
        {
            Transform existingChild = barRoot.Find(childName);
            if (existingChild != null)
            {
                existingRenderer = existingChild.GetComponent<SpriteRenderer>();
            }
        }

        if (existingRenderer == null)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(barRoot, false);
            existingRenderer = child.AddComponent<SpriteRenderer>();
        }

        existingRenderer.sprite = GetSharedSprite();
        existingRenderer.color = color;
        existingRenderer.sortingOrder = rendererSortingOrder;
        return existingRenderer;
    }

    void UpdateBar()
    {
        if (health == null || barRoot == null || backgroundRenderer == null || fillRenderer == null)
        {
            return;
        }

        float healthPercent = health.MaxHealth > 0 ? Mathf.Clamp01((float)health.CurrentHealth / health.MaxHealth) : 0f;
        bool shouldShow = health.IsAlive && (!hideWhenFull || healthPercent < 1f);
        barRoot.gameObject.SetActive(shouldShow);

        backgroundRenderer.color = backgroundColor;
        fillRenderer.color = fillColor;

        float clampedInset = Mathf.Min(inset, width * 0.45f, height * 0.45f);
        float fillWidth = Mathf.Max(0f, (width - clampedInset * 2f) * healthPercent);
        float fillHeight = Mathf.Max(0f, height - clampedInset * 2f);

        backgroundRenderer.transform.localPosition = Vector3.zero;
        backgroundRenderer.transform.localRotation = Quaternion.identity;
        backgroundRenderer.transform.localScale = new Vector3(width, height, 1f);

        fillRenderer.transform.localPosition = new Vector3(-width * 0.5f + clampedInset + fillWidth * 0.5f, 0f, -0.001f);
        fillRenderer.transform.localRotation = Quaternion.identity;
        fillRenderer.transform.localScale = new Vector3(fillWidth, fillHeight, 1f);
    }

    static Sprite GetSharedSprite()
    {
        if (sharedSprite != null)
        {
            return sharedSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        sharedSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        sharedSprite.hideFlags = HideFlags.HideAndDontSave;
        return sharedSprite;
    }
}
