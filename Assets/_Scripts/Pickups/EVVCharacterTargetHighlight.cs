using System.Collections.Generic;
using UnityEngine;

// Reusable hover/selection feedback for potions and other character-targeting tools.
// It can render a colored ghost or temporarily reduce the target's transparency,
// while leaving its health bar alone.
public class EVVCharacterTargetHighlight : MonoBehaviour
{
    enum HighlightStyle
    {
        Ghost,
        Transparency
    }

    [SerializeField] HighlightStyle highlightStyle = HighlightStyle.Ghost;
    [SerializeField] Color highlightColor = new Color(1f, 0.82f, 0.12f, 0.65f);
    [SerializeField, Min(1f)] float scaleMultiplier = 1.08f;
    [SerializeField] int sortingOrderOffset = 2;
    [SerializeField] float depthOffset = -0.02f;
    [SerializeField, Range(0f, 1f)] float transparencyMultiplier = 0.45f;

    readonly Dictionary<SpriteRenderer, SpriteRenderer> ghosts =
        new Dictionary<SpriteRenderer, SpriteRenderer>();
    readonly Dictionary<SpriteRenderer, Color> originalColors =
        new Dictionary<SpriteRenderer, Color>();

    EVVDefender currentTarget;

    public EVVDefender CurrentTarget => currentTarget;

    public void ConfigureTransparency(float alphaMultiplier)
    {
        Clear();
        highlightStyle = HighlightStyle.Transparency;
        transparencyMultiplier = Mathf.Clamp01(alphaMultiplier);
    }

    void OnDisable()
    {
        Clear();
    }

    public void Show(EVVDefender target)
    {
        if (target != currentTarget)
        {
            Clear();
            currentTarget = target;
        }

        if (target == null)
        {
            return;
        }

        HashSet<SpriteRenderer> visibleSources = new HashSet<SpriteRenderer>();
        foreach (SpriteRenderer source in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (source == null || IsHealthBarRenderer(source) || IsHighlightRenderer(source))
            {
                continue;
            }

            visibleSources.Add(source);
            if (highlightStyle == HighlightStyle.Transparency)
            {
                ApplyTransparency(source);
            }
            else
            {
                EnsureGhost(source);
            }
        }

        List<SpriteRenderer> staleSources = new List<SpriteRenderer>();
        IEnumerable<SpriteRenderer> trackedSources = highlightStyle == HighlightStyle.Transparency
            ? originalColors.Keys
            : ghosts.Keys;
        foreach (SpriteRenderer source in trackedSources)
        {
            if (source == null || !visibleSources.Contains(source))
            {
                staleSources.Add(source);
            }
        }

        foreach (SpriteRenderer source in staleSources)
        {
            RemoveHighlight(source);
        }
    }

    public void Clear()
    {
        foreach (SpriteRenderer ghost in ghosts.Values)
        {
            if (ghost != null)
            {
                Destroy(ghost.gameObject);
            }
        }

        ghosts.Clear();

        foreach (KeyValuePair<SpriteRenderer, Color> entry in originalColors)
        {
            if (entry.Key != null)
            {
                entry.Key.color = entry.Value;
            }
        }

        originalColors.Clear();
        currentTarget = null;
    }

    public bool IsHighlightRenderer(SpriteRenderer renderer)
    {
        return renderer != null
            && (ghosts.ContainsValue(renderer) || renderer.name == "Character Target Highlight");
    }

    void EnsureGhost(SpriteRenderer source)
    {
        if (!ghosts.TryGetValue(source, out SpriteRenderer ghost) || ghost == null)
        {
            GameObject ghostObject = new GameObject("Character Target Highlight");
            ghostObject.transform.SetParent(source.transform, false);
            ghostObject.transform.localPosition = new Vector3(0f, 0f, depthOffset);
            ghostObject.transform.localRotation = Quaternion.identity;
            ghostObject.transform.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);
            ghost = ghostObject.AddComponent<SpriteRenderer>();
            ghosts[source] = ghost;
        }

        ghost.sprite = source.sprite;
        ghost.flipX = source.flipX;
        ghost.flipY = source.flipY;
        ghost.sortingLayerID = source.sortingLayerID;
        ghost.sortingOrder = source.sortingOrder + sortingOrderOffset;
        ghost.color = highlightColor;
    }

    void ApplyTransparency(SpriteRenderer source)
    {
        if (!originalColors.TryGetValue(source, out Color originalColor))
        {
            originalColor = source.color;
            originalColors[source] = originalColor;
        }

        source.color = new Color(
            originalColor.r,
            originalColor.g,
            originalColor.b,
            originalColor.a * transparencyMultiplier);
    }

    void RemoveHighlight(SpriteRenderer source)
    {
        if (highlightStyle == HighlightStyle.Transparency)
        {
            if (source != null && originalColors.TryGetValue(source, out Color originalColor))
            {
                source.color = originalColor;
            }

            originalColors.Remove(source);
            return;
        }

        RemoveGhost(source);
    }

    void RemoveGhost(SpriteRenderer source)
    {
        if (ghosts.TryGetValue(source, out SpriteRenderer ghost) && ghost != null)
        {
            Destroy(ghost.gameObject);
        }

        ghosts.Remove(source);
    }

    static bool IsHealthBarRenderer(SpriteRenderer renderer)
    {
        Transform candidate = renderer.transform;
        while (candidate != null)
        {
            if (candidate.name == "Health Bar")
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }
}
