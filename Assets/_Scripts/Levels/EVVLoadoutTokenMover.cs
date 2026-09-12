using UnityEngine;

// Animates a defender loadout tray token between world positions with a small arc and scale
// pop, matching the feel of EVVThrownPickup/EVVBoardPickup's fly-to-wallet animation. Also
// supports a shrink-and-destroy exit animation for when a defender is removed from the tray.
public class EVVLoadoutTokenMover : MonoBehaviour
{
    Vector3 startPosition;
    Vector3 targetPosition;
    Vector3 startScale;
    Vector3 targetScale;
    float duration;
    float elapsed;
    bool moving;
    bool destroyOnArrival;
    float arcHeight;

    public void MoveTo(Vector3 target, float seconds, float arc = 0f)
    {
        startPosition = transform.position;
        targetPosition = target;
        startScale = transform.localScale;
        targetScale = startScale;
        duration = Mathf.Max(0.01f, seconds);
        arcHeight = arc;
        elapsed = 0f;
        moving = true;
        destroyOnArrival = false;
    }

    public void PlayEnterFrom(Vector3 origin, Vector3 target, Vector3 finalScale, float seconds, float arc)
    {
        transform.position = origin;
        startPosition = origin;
        targetPosition = target;
        startScale = Vector3.zero;
        targetScale = finalScale;
        duration = Mathf.Max(0.01f, seconds);
        arcHeight = arc;
        elapsed = 0f;
        moving = true;
        destroyOnArrival = false;
    }

    public void PlayExitAndDestroy(float seconds)
    {
        startPosition = transform.position;
        targetPosition = transform.position;
        startScale = transform.localScale;
        targetScale = Vector3.zero;
        duration = Mathf.Max(0.01f, seconds);
        arcHeight = 0f;
        elapsed = 0f;
        moving = true;
        destroyOnArrival = true;
    }

    void Update()
    {
        if (!moving)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float eased = 1f - (1f - t) * (1f - t);

        Vector3 nextPosition = Vector3.Lerp(startPosition, targetPosition, eased);
        nextPosition.y += Mathf.Sin(t * Mathf.PI) * arcHeight;
        transform.position = nextPosition;
        transform.localScale = Vector3.Lerp(startScale, targetScale, eased);

        if (t >= 1f)
        {
            moving = false;
            if (destroyOnArrival)
            {
                Destroy(gameObject);
            }
        }
    }
}
