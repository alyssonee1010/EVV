using UnityEngine;

public class EVVPickupCollectEffect : MonoBehaviour
{
    SpriteRenderer spriteRenderer;
    Vector3 startScale;
    Vector3 endScale;
    Vector3 startPosition;
    Vector3 endPosition;
    Color startColor;
    float duration = 0.18f;
    float elapsed;

    public void Initialize(SpriteRenderer sourceRenderer, float effectDuration, float scaleMultiplier, Vector3 drift, Color tint)
    {
        if (sourceRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sourceRenderer.sprite;
        spriteRenderer.flipX = sourceRenderer.flipX;
        spriteRenderer.flipY = sourceRenderer.flipY;
        spriteRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        spriteRenderer.sortingOrder = sourceRenderer.sortingOrder;
        spriteRenderer.color = tint;

        transform.position = sourceRenderer.transform.position;
        transform.rotation = sourceRenderer.transform.rotation;
        transform.localScale = sourceRenderer.transform.lossyScale;

        duration = Mathf.Max(0.01f, effectDuration);
        startScale = transform.localScale;
        endScale = startScale * Mathf.Max(0.01f, scaleMultiplier);
        startPosition = transform.position;
        endPosition = startPosition + drift;
        startColor = tint;
    }

    void Update()
    {
        if (spriteRenderer == null)
        {
            Destroy(gameObject);
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float easedT = 1f - (1f - t) * (1f - t);

        transform.position = Vector3.Lerp(startPosition, endPosition, easedT);
        transform.localScale = Vector3.Lerp(startScale, endScale, easedT);

        Color color = startColor;
        color.a *= 1f - easedT;
        spriteRenderer.color = color;

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
