using UnityEngine;

public class EVVThrownPickup : MonoBehaviour
{
    Vector3 startPosition;
    Vector3 landingPosition;
    float arcHeight;
    float duration;
    float elapsed;
    bool moving;

    public void Launch(Vector3 start, Vector3 landing, float height, float seconds)
    {
        startPosition = start;
        landingPosition = landing;
        arcHeight = Mathf.Max(0f, height);
        duration = Mathf.Max(0.01f, seconds);
        elapsed = 0f;
        moving = true;
        transform.position = startPosition;
    }

    void Update()
    {
        if (!moving)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        Vector3 nextPosition = Vector3.Lerp(startPosition, landingPosition, t);
        nextPosition.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = nextPosition;

        if (t >= 1f)
        {
            transform.position = landingPosition;
            moving = false;
            enabled = false;
        }
    }
}
