using System.Collections.Generic;
using UnityEngine;

public static class TransformExtensions
{
    public static List<Transform> GetChildren(this Transform transform)
    {
        List<Transform> items = new();
        foreach (Transform t in transform)
        {
            items.Add(t);
        }
        return items;
    } 

    public static void DestroyChildren(this Transform transform)
    {
        // Loop backward to safely avoid index shifting bugs
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            // Access the child by index and destroy its GameObject
            Object.Destroy(transform.GetChild(i).gameObject);
        }
    }

    // Sets the local scale so the world scale matches worldScale, undoing the parents' scale.
    // A mirrored parent (negative scale) keeps mirroring the child.
    public static void SetLossyScale(this Transform transform, Vector3 worldScale)
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            DivideByMagnitude(worldScale.x, parentScale.x),
            DivideByMagnitude(worldScale.y, parentScale.y),
            DivideByMagnitude(worldScale.z, parentScale.z));
    }

    static float DivideByMagnitude(float value, float divisor)
    {
        float magnitude = Mathf.Abs(divisor);
        return magnitude > 0.0001f ? value / magnitude : value;
    }
}