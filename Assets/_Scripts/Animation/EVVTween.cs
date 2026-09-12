using UnityEngine;
using PrimeTween;

public static class EVVTween
{
    public static Tween TweenTo(this Transform transform, Vector3 position, float duration = 0.5f, Ease ease = Ease.Default, int cycles = 1, CycleMode cycleMode = CycleMode.Restart)
    {
        return Tween.Position(transform, new TweenSettings<Vector3>(position, duration, ease, cycles: cycles, cycleMode: cycleMode));
    }

public static Tween TweenArcTo(this Transform transform, Vector3 endPosition, float maxHeight, float duration = 0.5f, Ease ease = Ease.Linear)
    {
        // 1. Capture the starting position when the tween is called
        Vector3 startPosition = transform.position;

        // 2. Safety check: Ensure the requested peak is actually higher than start and end
        if (maxHeight < startPosition.y || maxHeight < endPosition.y)
        {
            Debug.LogError($"[EVVTween] Absolute maxHeight ({maxHeight}) must be higher than both start Y ({startPosition.y}) and end Y ({endPosition.y})!");
            return default;
        }

        // Handle the edge case where the peak matches the points perfectly (flat line)
        if (Mathf.Approximately(maxHeight, startPosition.y) && Mathf.Approximately(maxHeight, endPosition.y))
        {
            return Tween.Position(transform, endPosition, duration, ease);
        }

        // 3. Parabola Geometry Calculation
        // We find the normalized timeline point (tPeak) where the apex should occur.
        // In clean physics: time is proportional to the square root of the drop distance.
        float hStart = maxHeight - startPosition.y; 
        float hEnd = maxHeight - endPosition.y;     

        float sqrtHStart = Mathf.Sqrt(hStart);
        float sqrtHEnd = Mathf.Sqrt(hEnd);
        
        // This is the exact frame (0.0 to 1.0) where the object must hit its maximum height
        float tPeak = sqrtHStart / (sqrtHStart + sqrtHEnd);

        // Calculate the quadratic coefficient 'a' based on our vertex conditions:
        // when t = 0, yOffset must equal -hStart
        float a = -hStart / (tPeak * tPeak);

        // 4. Return the active PrimeTween custom handle
        return Tween.Custom(0f, 1f, duration, t =>
        {
            if (transform == null) return; // Safety check if object is destroyed mid-tween

            // Smoothly slide across space over time (handles jumping left, right, forward, or backward)
            float x = Mathf.Lerp(startPosition.x, endPosition.x, t);
            float z = Mathf.Lerp(startPosition.z, endPosition.z, t);

            // Calculate the absolute geometric parabola Y position
            // Vertex form equation: y = a * (t - tPeak)^2 + maxHeight
            float tDiff = t - tPeak;
            float y = (a * tDiff * tDiff) + maxHeight;

            transform.position = new Vector3(x, y, z);
            
        }, ease: ease);
    }
}
