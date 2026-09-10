using UnityEngine;
using UnityEngine.Rendering;

public static class EVVLaneDepth
{
    public const string GameplaySortingLayerName = "Gameplay";
    public const float LaneCenterOffset = 0.5f;
    public const float DefaultDepthTolerance = 0.3f;

    static readonly Vector3 SortAxis = Vector3.forward;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ConfigureTransparencySorting()
    {
        GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
        GraphicsSettings.transparencySortAxis = SortAxis;
    }

    public static float LaneToZ(int laneIndex)
    {
        return laneIndex + LaneCenterOffset;
    }

    public static int ZToLane(float z)
    {
        return Mathf.FloorToInt(z);
    }

    public static Vector3 WithLaneZ(Vector3 position, int laneIndex)
    {
        position.z = LaneToZ(laneIndex);
        return position;
    }

    public static bool IsSameDepth(float a, float b, float tolerance = DefaultDepthTolerance)
    {
        return Mathf.Abs(a - b) < tolerance;
    }

    public static bool IsSameDepth(Transform a, Transform b, float tolerance = DefaultDepthTolerance)
    {
        return a != null && b != null && IsSameDepth(a.position.z, b.position.z, tolerance);
    }

    public static void ApplyGameplaySortingGroup(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        SortingGroup sortingGroup = target.GetComponent<SortingGroup>();
        if (sortingGroup == null)
        {
            sortingGroup = target.AddComponent<SortingGroup>();
        }

        sortingGroup.sortingLayerName = GameplaySortingLayerName;
        sortingGroup.sortingOrder = 0;

        foreach (SpriteRenderer spriteRenderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            spriteRenderer.sortingLayerName = GameplaySortingLayerName;
        }
    }

    public static void ApplyGameplayRenderer(SpriteRenderer spriteRenderer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sortingLayerName = GameplaySortingLayerName;
        spriteRenderer.sortingOrder = 0;
    }
}
