using System;
using UnityEngine;

public static class EVVWorldPointer
{
    public static Vector3 GetPosition(Camera worldCamera = null)
    {
        worldCamera ??= Camera.main;
        if (worldCamera == null)
        {
            return Vector3.zero;
        }

        Vector3 screenPosition = Input.mousePosition;
        screenPosition.z = Mathf.Abs(worldCamera.transform.position.z);

        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = 0f;
        return worldPosition;
    }

    public static T FindClosest<T>(Vector2 point, float radius, Predicate<T> canSelect = null)
        where T : Component
    {
        foreach (Collider2D hit in Physics2D.OverlapPointAll(point))
        {
            T target = hit != null ? hit.GetComponentInParent<T>() : null;
            target ??= hit != null ? hit.GetComponentInChildren<T>() : null;
            if (target != null && (canSelect == null || canSelect(target)))
            {
                return target;
            }
        }

        T closest = null;
        float closestSqrDistance = radius * radius;
        foreach (T target in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude))
        {
            if (target == null || (canSelect != null && !canSelect(target)))
            {
                continue;
            }

            float sqrDistance = SqrDistanceToVisual(target, point);
            if (sqrDistance <= closestSqrDistance)
            {
                closest = target;
                closestSqrDistance = sqrDistance;
            }
        }

        return closest;
    }

    static float SqrDistanceToVisual(Component target, Vector2 point)
    {
        float closest = ((Vector2)target.transform.position - point).sqrMagnitude;
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(false))
        {
            Vector3 pointAtRendererDepth = new Vector3(point.x, point.y, renderer.bounds.center.z);
            closest = Mathf.Min(closest, renderer.bounds.SqrDistance(pointAtRendererDepth));
        }

        return closest;
    }
}
