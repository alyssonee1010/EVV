using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Marks the sprites of this character for the outline that EVVSilhouetteOutlineFeature
/// draws: one line around the whole character, never between its own parts. Add it to a
/// character whose art has no drawn outline. Health bar sprites are left out.
///
/// The mark is a rendering layer bit on each renderer, which the feature filters the key
/// pass by. It is deliberately not a MaterialPropertyBlock: per-renderer property data
/// breaks sprite batching and measured about 8 ms of extra frame time at 60 characters,
/// which was most of what the outline used to cost.
/// </summary>
[DisallowMultipleComponent]
public class EVVSilhouetteOutline : MonoBehaviour
{
    /// <summary>Rendering layer bit that marks a sprite as outlined. Must match the feature.</summary>
    public const uint OutlinedRenderingLayer = 1u << 1;

    const string HealthBarRootName = "Health Bar";

    // Enabled markers, for the renderer feature: it skips its passes while nothing is
    // outlined (menus, loadout screen) and uses these to size the outline quads.
    public static readonly List<EVVSilhouetteOutline> Active = new List<EVVSilhouetteOutline>();

    readonly List<SpriteRenderer> renderers = new List<SpriteRenderer>();
    readonly List<uint> originalMasks = new List<uint>();

    void OnEnable()
    {
        Active.Add(this);

        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (IsHealthBarRenderer(renderer))
            {
                continue;
            }

            renderers.Add(renderer);
            originalMasks.Add(renderer.renderingLayerMask);
            // Replaced, not or-ed: the feature draws outlined sprites and occluders as two
            // disjoint renderer lists, and a rendering layer mask can only be matched, not
            // excluded, so an outlined sprite must not also match the occluder list.
            renderer.renderingLayerMask = OutlinedRenderingLayer;
        }
    }

    void OnDisable()
    {
        Active.Remove(this);

        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].renderingLayerMask = originalMasks[i];
            }
        }

        renderers.Clear();
        originalMasks.Clear();
    }

    /// <summary>World bounds of the outlined sprites; false while none is visible.</summary>
    public bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
            {
                continue;
            }

            if (found)
            {
                bounds.Encapsulate(renderer.bounds);
            }
            else
            {
                bounds = renderer.bounds;
                found = true;
            }
        }

        return found;
    }

    bool IsHealthBarRenderer(SpriteRenderer renderer)
    {
        Transform candidate = renderer.transform;
        while (candidate != null && candidate != transform)
        {
            if (candidate.name == HealthBarRootName)
            {
                return true;
            }

            candidate = candidate.parent;
        }

        return false;
    }
}
