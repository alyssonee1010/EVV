using UnityEngine;

/// <summary>
/// Marks every sprite of this character for the silhouette outline that
/// EVVSilhouetteOutlineFeature draws around the whole body. Add it to a character
/// whose art has no drawn outline. Health bar sprites are left out.
/// </summary>
[DisallowMultipleComponent]
public class EVVSilhouetteOutline : MonoBehaviour
{
    // Rendering layer "Outline" (Project Settings > Tags and Layers > Rendering Layers).
    public const uint RenderingLayerMask = 1u << 1;

    const string HealthBarRootName = "Health Bar";

    // Lets the renderer feature skip its passes while nothing is outlined (menus, loadout screen).
    public static int ActiveCount { get; private set; }

    void OnEnable()
    {
        ActiveCount++;
        SetOutlined(true);
    }

    void OnDisable()
    {
        ActiveCount--;
        SetOutlined(false);
    }

    void SetOutlined(bool outlined)
    {
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
        {
            if (IsHealthBarRenderer(renderer))
            {
                continue;
            }

            if (outlined)
            {
                renderer.renderingLayerMask |= RenderingLayerMask;
            }
            else
            {
                renderer.renderingLayerMask &= ~RenderingLayerMask;
            }
        }
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
